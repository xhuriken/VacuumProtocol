using System.Collections.Generic;
using UnityEngine;
using VacuumProtocol.Player.Visuals;

namespace VacuumProtocol.Player
{
    /// <summary>
    /// Description: Serves as the dynamic link between the stable physics Bone Bridge hierarchy (SSOT) and the visual imported mesh.
    /// Context: Attached to the player prefab root or parent visual container.
    /// Justification: Decouples mesh imports from control scripts. At Awake, it maps SkinnedMeshRenderer bones to the Bone Bridge transforms by name, and dynamically wires renderers/animators to control scripts.
    /// </summary>
    [DefaultExecutionOrder(-10)] // Ensure this runs before control scripts start caching references
    public class PlayerBoneBridge : MonoBehaviour
    {
        [Header("Bone Bridge Setup")]
        [Tooltip("Role: The root transform containing the stable physics bones (the Bone Bridge).\nUse Case: Lookup source.\nJustification: All bones in this hierarchy serve as targets for visual mesh mapping.")]
        [SerializeField] private Transform _boneBridgeRoot;

        [Tooltip("Role: The root of the imported visual mesh (FBX hierarchy).\nUse Case: Scan target.\nJustification: We scan this object at startup to locate SkinnedMeshRenderers and visual bones.")]
        [SerializeField] private Transform _importedMeshRoot;

        [Header("Dynamic Script Wiring")]
        [Tooltip("Role: The PlayerCustomization component that needs reference to the visual mesh renderer.\nUse Case: Color synchronization.\nJustification: Automatically gets its _modelRenderer assigned at runtime to prevent configuration issues.")]
        [SerializeField] private PlayerCustomization _playerCustomization;

        [Header("Custom Follower Rules")]
        [Tooltip("Role: Explicit mappings for non-skinned objects (e.g. wheels, separate gear mesh parts) that should copy a bone's movement.\nUse Case: Precision overrides.\nJustification: Allows driving non-skinned mesh pivots without manually reparenting them.")]
        [SerializeField] private List<CustomFollowMapping> _customFollowers = new List<CustomFollowMapping>();

        [Header("Debug")]
        [Tooltip("Role: Enable diagnostic warnings.\nUse Case: Troubleshooting mismatched bones.")]
        [SerializeField] private bool _enableDebugLogs = true;

        // Cached lookup map of the Bone Bridge bones for fast name-based searches
        private Dictionary<string, Transform> _bridgeBonesMap = new Dictionary<string, Transform>();

        [System.Serializable]
        public struct CustomFollowMapping
        {
            [Tooltip("Name of the bone in the Bone Bridge hierarchy to follow.")]
            public string SourceBoneName;
            [Tooltip("The visual transform that should follow the bone's position/rotation.")]
            public Transform VisualTransform;
        }

        /// <summary>
        /// Description: Awake callback. Performs bone rebinding and script wiring.
        /// Context: Startup binding phase.
        /// Justification: Must be done early so when gameplay scripts (like PlayerArmsController) initialize in Start(), the visual meshes are already aligned.
        /// </summary>
        private void Awake()
        {
            InitializeBridgeMap();
            PerformRuntimeBinding();
            WireControlScripts();
        }

        /// <summary>
        /// Description: Populates the internal bridge bone dictionary.
        /// Context: Initialization.
        /// Justification: Using recursion to populate a Dictionary ensures O(1) bone lookup speed during mapping.
        /// </summary>
        public void InitializeBridgeMap()
        {
            _bridgeBonesMap.Clear();
            Transform root = _boneBridgeRoot != null ? _boneBridgeRoot : transform;

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                // To support duplicates of same names in different paths safely, we log warnings, but keep the first/highest parent
                if (!string.IsNullOrEmpty(child.name) && !_bridgeBonesMap.ContainsKey(child.name))
                {
                    _bridgeBonesMap.Add(child.name, child);
                }
            }
        }

        /// <summary>
        /// Description: Finds matching Bone Bridge transform by name.
        /// Context: Internal lookup helper.
        /// </summary>
        public Transform FindBoneInBridge(string boneName)
        {
            if (string.IsNullOrEmpty(boneName)) return null;
            return _bridgeBonesMap.TryGetValue(boneName, out Transform t) ? t : null;
        }

        /// <summary>
        /// Description: Rebinds the skinned mesh renderers to the Bone Bridge transforms.
        /// Context: Runtime execution.
        /// Justification: Remapping SkinnedMeshRenderer.bones tells Unity to deform the mesh using the Bone Bridge physics bones.
        /// </summary>
        private void PerformRuntimeBinding()
        {
            if (_importedMeshRoot == null)
            {
                if (_enableDebugLogs) Debug.LogWarning("[PlayerBoneBridge] Imported Mesh Root is not assigned. Skipping bone mapping.");
                return;
            }

            // 1. Rebind all SkinnedMeshRenderers
            SkinnedMeshRenderer[] smrs = _importedMeshRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (SkinnedMeshRenderer smr in smrs)
            {
                Transform[] meshBones = smr.bones;
                Transform[] bridgeBones = new Transform[meshBones.Length];
                int mappedCount = 0;

                for (int i = 0; i < meshBones.Length; i++)
                {
                    Transform meshBone = meshBones[i];
                    if (meshBone == null) continue;

                    Transform bridgeBone = FindBoneInBridge(meshBone.name);
                    if (bridgeBone != null)
                    {
                        bridgeBones[i] = bridgeBone;
                        mappedCount++;
                    }
                    else
                    {
                        if (_enableDebugLogs) Debug.LogWarning($"[PlayerBoneBridge] No matching bone in Bone Bridge for mesh bone: {meshBone.name}");
                        bridgeBones[i] = meshBone; // Fallback
                    }
                }

                smr.bones = bridgeBones;

                if (smr.rootBone != null)
                {
                    Transform bridgeRootBone = FindBoneInBridge(smr.rootBone.name);
                    if (bridgeRootBone != null)
                    {
                        smr.rootBone = bridgeRootBone;
                    }
                }

                if (_enableDebugLogs) Debug.Log($"[PlayerBoneBridge] SkinnedMeshRenderer '{smr.name}' re-bound: {mappedCount}/{meshBones.Length} bones mapped.");
            }

            // 2. Automatic rigid/non-skinned matching fallback
            // If the model has no SkinnedMeshRenderer, or if there are rigid pieces, map them directly by name
            Transform[] visualTransforms = _importedMeshRoot.GetComponentsInChildren<Transform>(true);
            int autoRigidCount = 0;
            foreach (Transform visT in visualTransforms)
            {
                // Skip the root itself
                if (visT == _importedMeshRoot) continue;

                // Check if this transform matches a bone in the Bone Bridge by name
                Transform bridgeBone = FindBoneInBridge(visT.name);
                if (bridgeBone != null && bridgeBone != visT)
                {
                    // To avoid interfering with skinned meshes bones deforming, we check if this object has a MeshRenderer/MeshFilter
                    // or is a non-skinned transform child that needs to follow the physics bone.
                    if (visT.GetComponent<MeshRenderer>() != null || visT.childCount > 0)
                    {
                        // Ensure we don't double-add followers
                        if (visT.gameObject.GetComponent<RuntimeFollower>() == null)
                        {
                            RuntimeFollower follower = visT.gameObject.AddComponent<RuntimeFollower>();
                            follower.Target = bridgeBone;
                            autoRigidCount++;
                        }
                    }
                }
            }
            if (autoRigidCount > 0 && _enableDebugLogs)
            {
                Debug.Log($"[PlayerBoneBridge] Automatically mapped {autoRigidCount} unskinned/rigid transforms to Bone Bridge bones by name.");
            }

            // 3. Setup explicit custom followers from inspector
            foreach (var follower in _customFollowers)
            {
                if (follower.VisualTransform == null) continue;

                Transform src = FindBoneInBridge(follower.SourceBoneName);
                if (src != null)
                {
                    if (follower.VisualTransform.gameObject.GetComponent<RuntimeFollower>() == null)
                    {
                        RuntimeFollower comp = follower.VisualTransform.gameObject.AddComponent<RuntimeFollower>();
                        comp.Target = src;
                    }
                }
                else if (_enableDebugLogs)
                {
                    Debug.LogWarning($"[PlayerBoneBridge] Custom follower source bone '{follower.SourceBoneName}' not found in Bone Bridge.");
                }
            }
        }

        /// <summary>
        /// Description: Wires control scripts to the newly resolved visual mesh renderer.
        /// Context: Runtime initialization.
        /// Justification: Automatically connects PlayerCustomization to the correct renderer of the imported mesh.
        /// </summary>
        private void WireControlScripts()
        {
            if (_playerCustomization == null)
            {
                _playerCustomization = GetComponentInChildren<PlayerCustomization>(true);
            }

            if (_playerCustomization != null && _importedMeshRoot != null)
            {
                // Auto-detect the main renderer in the imported mesh to customize
                Renderer meshRenderer = _importedMeshRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (meshRenderer == null)
                {
                    meshRenderer = _importedMeshRoot.GetComponentInChildren<MeshRenderer>(true);
                }

                if (meshRenderer != null)
                {
                    _playerCustomization.ModelRenderer = meshRenderer;
                    if (_enableDebugLogs) Debug.Log($"[PlayerBoneBridge] Wired PlayerCustomization ModelRenderer to: {meshRenderer.name}");
                }
                else if (_enableDebugLogs)
                {
                    Debug.LogWarning("[PlayerBoneBridge] Could not find any Renderer in children of Imported Mesh Root to assign to PlayerCustomization.");
                }
            }
        }

        /// <summary>
        /// Description: Simple follower component added dynamically at runtime to sync non-skinned parts.
        /// </summary>
        private class RuntimeFollower : MonoBehaviour
        {
            public Transform Target;

            private void LateUpdate()
            {
                if (Target != null)
                {
                    transform.position = Target.position;
                    transform.rotation = Target.rotation;
                }
            }
        }
    }
}
