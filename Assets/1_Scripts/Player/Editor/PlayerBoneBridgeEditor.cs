using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VacuumProtocol.Player.Editor
{
    /// <summary>
    /// Description: Custom Inspector for PlayerBoneBridge.
    /// Context: Editor-only script.
    /// Justification: Provides designers with immediate feedback on bone matching layout success before running the game, and automates populating custom followers like wheels.
    /// </summary>
    [CustomEditor(typeof(PlayerBoneBridge))]
    public class PlayerBoneBridgeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw default fields
            DrawDefaultInspector();

            PlayerBoneBridge bridge = (PlayerBoneBridge)target;

            SerializedProperty bridgeRootProp = serializedObject.FindProperty("_boneBridgeRoot");
            SerializedProperty meshRootProp = serializedObject.FindProperty("_importedMeshRoot");
            SerializedProperty customFollowersProp = serializedObject.FindProperty("_customFollowers");

            Transform bridgeRoot = bridgeRootProp.objectReferenceValue as Transform;
            Transform meshRoot = meshRootProp.objectReferenceValue as Transform;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Editor Automation Utilities", EditorStyles.boldLabel);

            if (bridgeRoot == null || meshRoot == null)
            {
                EditorGUILayout.HelpBox("Please assign both Bone Bridge Root and Imported Mesh Root to enable validation utilities.", MessageType.Info);
                return;
            }

            // Button 1: Validate name matching
            if (GUILayout.Button("Validate Bone Name Matching", GUILayout.Height(30)))
            {
                bridge.InitializeBridgeMap();
                ValidateBones(meshRoot, bridge);
            }

            // Button 2: Auto-detect and populate Wheel/Object followers
            if (GUILayout.Button("Auto-Detect & Populate Non-Skinned Followers", GUILayout.Height(30)))
            {
                bridge.InitializeBridgeMap();
                AutoDetectFollowers(meshRoot, bridge, customFollowersProp);
            }
        }

        /// <summary>
        /// Description: Scans all SkinnedMeshRenderers in the mesh and counts matching bone names in the Bone Bridge.
        /// </summary>
        private void ValidateBones(Transform meshRoot, PlayerBoneBridge bridge)
        {
            SkinnedMeshRenderer[] smrs = meshRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (smrs.Length == 0)
            {
                Debug.LogWarning("[PlayerBoneBridgeEditor] No SkinnedMeshRenderer found under the Imported Mesh Root.");
                return;
            }

            foreach (var smr in smrs)
            {
                Transform[] bones = smr.bones;
                int matched = 0;
                List<string> missing = new List<string>();

                foreach (var bone in bones)
                {
                    if (bone == null) continue;
                    if (bridge.FindBoneInBridge(bone.name) != null)
                    {
                        matched++;
                    }
                    else
                    {
                        missing.Add(bone.name);
                    }
                }

                if (missing.Count == 0)
                {
                    Debug.Log($"<color=green><b>[PlayerBoneBridge] Validation SUCCESS for '{smr.name}'!</b> All {bones.Length} bones mapped perfectly by name.</color>");
                }
                else
                {
                    Debug.LogWarning($"[PlayerBoneBridge] Validation WARNING for '{smr.name}': {matched}/{bones.Length} bones mapped. Missing matches for: {string.Join(", ", missing)}");
                }
            }
        }

        /// <summary>
        /// Description: Searches the imported mesh for transforms containing common non-skinned keywords (like 'wheel') and maps them if matching bone exists.
        /// </summary>
        private void AutoDetectFollowers(Transform meshRoot, PlayerBoneBridge bridge, SerializedProperty followersProp)
        {
            List<string> keywords = new List<string> { "wheel", "caster", "attachment", "nozzle" };
            int addedCount = 0;

            // Clear list first or ask user? We can just append unique items.
            HashSet<Transform> existingVisuals = new HashSet<Transform>();
            for (int i = 0; i < followersProp.arraySize; i++)
            {
                SerializedProperty element = followersProp.GetArrayElementAtIndex(i);
                Transform visualT = element.FindPropertyRelative("VisualTransform").objectReferenceValue as Transform;
                if (visualT != null) existingVisuals.Add(visualT);
            }

            foreach (Transform child in meshRoot.GetComponentsInChildren<Transform>(true))
            {
                // Skip if it's already in the followers list
                if (existingVisuals.Contains(child)) continue;

                // Check if name matches keywords
                bool matchesKeyword = false;
                foreach (string kw in keywords)
                {
                    if (child.name.ToLower().Contains(kw))
                    {
                        matchesKeyword = true;
                        break;
                    }
                }

                if (matchesKeyword)
                {
                    // Check if a corresponding bone exists in the Bone Bridge
                    Transform bridgeBone = bridge.FindBoneInBridge(child.name);
                    if (bridgeBone != null)
                    {
                        int nextIdx = followersProp.arraySize;
                        followersProp.InsertArrayElementAtIndex(nextIdx);
                        SerializedProperty newElem = followersProp.GetArrayElementAtIndex(nextIdx);
                        newElem.FindPropertyRelative("SourceBoneName").stringValue = child.name;
                        newElem.FindPropertyRelative("VisualTransform").objectReferenceValue = child;
                        addedCount++;
                    }
                }
            }

            if (addedCount > 0)
            {
                serializedObject.ApplyModifiedProperties();
                Debug.Log($"<color=green><b>[PlayerBoneBridge] Auto-detection completed!</b> Added {addedCount} new non-skinned followers.</color>");
            }
            else
            {
                Debug.Log("[PlayerBoneBridge] Auto-detection finished. No new matching non-skinned followers found.");
            }
        }
    }
}
