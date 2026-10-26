using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Utils
{
    /// <summary>
    /// Description: Inspector utility component to automatically batch-assign colliders to child meshes.
    /// Context: Can be placed on any room, prefab root, or modular hierarchy containing nested mesh children.
    /// Justification: Solves the issue where free/modular 3D assets export mesh geometry on nested child nodes (e.g. 'default'),
    /// allowing 1-click batch generation of MeshColliders or BoxColliders with layer assignment and full Undo support.
    /// </summary>
    [ExecuteInEditMode]
    [SelectionBase]
    public class ChildColliderHelper : MonoBehaviour
    {
        public enum ColliderType
        {
            MeshCollider,
            BoxCollider
        }

        #region Inspector Settings
        [Title("Collider Settings")]
        [Tooltip("Role: Type of collider to generate for each child mesh.")]
        [SerializeField] private ColliderType _colliderType = ColliderType.MeshCollider;

        [ShowIf("_colliderType", ColliderType.MeshCollider)]
        [Tooltip("Role: Whether MeshColliders should be marked Convex (required for dynamic rigidbodies/triggers, false for static walls/floors).")]
        [SerializeField] private bool _convex = false;

        [Title("Layer & Physics Configuration")]
        [Tooltip("Role: Optional layer to assign to child objects with colliders (e.g. 'Ground' for floors, 'Obstacle' for walls).")]
        [ValueDropdown("GetLayerNames")]
        [SerializeField] private string _targetLayer = "Keep Current";

        [Tooltip("Role: Optional PhysicsMaterial to assign to generated colliders.")]
        [SerializeField] private PhysicsMaterial _physicsMaterial;

        [Title("Filter Settings")]
        [Tooltip("Role: If true, inactive/disabled child GameObjects will also receive colliders.")]
        [SerializeField] private bool _includeInactive = true;

        [Tooltip("Role: If true, existing colliders on child objects will be replaced.")]
        [SerializeField] private bool _replaceExisting = false;
        #endregion

        #if UNITY_EDITOR
        private static string[] GetLayerNames()
        {
            List<string> layers = new List<string> { "Keep Current" };
            for (int i = 0; i < 32; i++)
            {
                string name = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(name))
                {
                    layers.Add(name);
                }
            }
            return layers.ToArray();
        }

        #region Inspector Buttons
        [Title("Actions")]
        [Button(ButtonSizes.Large), GUIColor(0.2f, 0.9f, 0.35f)]
        public void AddCollidersToChildren()
        {
            MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>(_includeInactive);
            if (meshFilters == null || meshFilters.Length == 0)
            {
                Debug.LogWarning($"[ChildColliderHelper] No MeshFilter found in children of '{name}'!", this);
                return;
            }

            int addedCount = 0;
            int targetLayerIndex = _targetLayer != "Keep Current" ? LayerMask.NameToLayer(_targetLayer) : -1;

            Undo.SetCurrentGroupName("Add Colliders to Children");
            int undoGroup = Undo.GetCurrentGroup();

            foreach (MeshFilter mf in meshFilters)
            {
                if (mf == null || mf.sharedMesh == null) continue;
                GameObject targetGo = mf.gameObject;

                Collider existing = targetGo.GetComponent<Collider>();
                if (existing != null)
                {
                    if (!_replaceExisting)
                    {
                        if (targetLayerIndex != -1 && targetGo.layer != targetLayerIndex)
                        {
                            Undo.RecordObject(targetGo, "Change Layer");
                            targetGo.layer = targetLayerIndex;
                        }
                        if (_physicsMaterial != null && existing.sharedMaterial != _physicsMaterial)
                        {
                            Undo.RecordObject(existing, "Assign PhysicsMaterial");
                            existing.sharedMaterial = _physicsMaterial;
                        }
                        continue;
                    }
                    Undo.DestroyObjectImmediate(existing);
                }

                if (_colliderType == ColliderType.MeshCollider)
                {
                    MeshCollider mc = Undo.AddComponent<MeshCollider>(targetGo);
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = _convex;
                    if (_physicsMaterial != null) mc.sharedMaterial = _physicsMaterial;
                    addedCount++;
                }
                else if (_colliderType == ColliderType.BoxCollider)
                {
                    BoxCollider box = Undo.AddComponent<BoxCollider>(targetGo);
                    box.center = mf.sharedMesh.bounds.center;
                    box.size = mf.sharedMesh.bounds.size;
                    if (_physicsMaterial != null) box.sharedMaterial = _physicsMaterial;
                    addedCount++;
                }

                if (targetLayerIndex != -1)
                {
                    Undo.RecordObject(targetGo, "Change Layer");
                    targetGo.layer = targetLayerIndex;
                }

                EditorUtility.SetDirty(targetGo);
            }

            Undo.CollapseUndoOperations(undoGroup);

            if (!Application.isPlaying && gameObject.scene.IsValid())
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }

            Debug.Log($"<color=green>[ChildColliderHelper]</color> Scanned {meshFilters.Length} meshes under '<b>{name}</b>': Added {addedCount} colliders (Target Layer: {_targetLayer}).", this);
        }

        [Button(ButtonSizes.Medium), GUIColor(1f, 0.45f, 0.45f)]
        public void RemoveAllCollidersFromChildren()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(_includeInactive);
            if (colliders == null || colliders.Length == 0)
            {
                Debug.Log($"[ChildColliderHelper] No colliders found in children of '{name}'.", this);
                return;
            }

            Undo.SetCurrentGroupName("Remove Colliders from Children");
            int undoGroup = Undo.GetCurrentGroup();

            int removedCount = 0;
            foreach (Collider c in colliders)
            {
                if (c == null) continue;
                if (c.gameObject == gameObject) continue;

                Undo.DestroyObjectImmediate(c);
                removedCount++;
            }

            Undo.CollapseUndoOperations(undoGroup);

            if (!Application.isPlaying && gameObject.scene.IsValid())
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }

            Debug.Log($"<color=orange>[ChildColliderHelper]</color> Removed {removedCount} colliders from children of '<b>{name}</b>'.", this);
        }
        #endregion
        #endif
    }
}
