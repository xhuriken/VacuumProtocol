using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Utils.Editor
{
    /// <summary>
    /// Description: Editor window and Hierarchy context menu utilities to batch-generate and manage colliders on child meshes.
    /// Context: Editor-only tool. Accessible via Hierarchy right-click ('GameObject/Colliders/...') and 'Tools/Vacuum Protocol/Collider Helper'.
    /// Justification: Eliminates manual expansion and collider addition on modular 3D environment assets (like imported FBXs
    /// where geometry sits on nested child nodes like 'default').
    /// </summary>
    public class ChildColliderUtility : EditorWindow
    {
        public enum TargetColliderType
        {
            MeshCollider,
            BoxCollider
        }

        #region Window Settings
        private TargetColliderType _colliderType = TargetColliderType.MeshCollider;
        private bool _convex = false;
        private int _selectedLayerIndex = 0; // 0 = Keep Current
        private PhysicsMaterial _physicsMaterial;
        private bool _includeInactive = true;
        private bool _replaceExisting = false;
        private Vector2 _scrollPos;
        #endregion

        #region Menu Item Entrypoints
        [MenuItem("Tools/Vacuum Protocol/Collider Helper", false, 10)]
        [MenuItem("Window/Vacuum Protocol/Collider Helper", false, 20)]
        public static void OpenWindow()
        {
            ChildColliderUtility window = GetWindow<ChildColliderUtility>("Collider Helper");
            window.minSize = new Vector2(360, 480);
            window.Show();
        }

        [MenuItem("GameObject/Colliders/Add MeshColliders to Children", false, 20)]
        public static void QuickAddMeshColliders()
        {
            ProcessSelectedObjects(TargetColliderType.MeshCollider, false, -1, null, true, false);
        }

        [MenuItem("GameObject/Colliders/Add MeshColliders to Children", true)]
        private static bool ValidateQuickAddMeshColliders()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
        }

        [MenuItem("GameObject/Colliders/Add MeshColliders to Children (Convex)", false, 21)]
        public static void QuickAddMeshCollidersConvex()
        {
            ProcessSelectedObjects(TargetColliderType.MeshCollider, true, -1, null, true, false);
        }

        [MenuItem("GameObject/Colliders/Add MeshColliders to Children (Convex)", true)]
        private static bool ValidateQuickAddMeshCollidersConvex()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
        }

        [MenuItem("GameObject/Colliders/Add BoxColliders to Children", false, 22)]
        public static void QuickAddBoxColliders()
        {
            ProcessSelectedObjects(TargetColliderType.BoxCollider, false, -1, null, true, false);
        }

        [MenuItem("GameObject/Colliders/Add BoxColliders to Children", true)]
        private static bool ValidateQuickAddBoxColliders()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
        }

        [MenuItem("GameObject/Colliders/Set Layer/Ground", false, 35)]
        public static void QuickSetLayerGround()
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer == -1) groundLayer = 3;
            SetLayerOnSelectedChildren(groundLayer);
        }

        [MenuItem("GameObject/Colliders/Set Layer/Ground", true)]
        private static bool ValidateQuickSetLayerGround()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
        }

        [MenuItem("GameObject/Colliders/Set Layer/Obstacle", false, 36)]
        public static void QuickSetLayerObstacle()
        {
            int obstacleLayer = LayerMask.NameToLayer("Obstacle");
            if (obstacleLayer == -1) obstacleLayer = 8;
            SetLayerOnSelectedChildren(obstacleLayer);
        }

        [MenuItem("GameObject/Colliders/Set Layer/Obstacle", true)]
        private static bool ValidateQuickSetLayerObstacle()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
        }

        [MenuItem("GameObject/Colliders/Remove All Colliders from Children", false, 50)]
        public static void QuickRemoveColliders()
        {
            RemoveCollidersFromSelectedObjects(true);
        }

        [MenuItem("GameObject/Colliders/Remove All Colliders from Children", true)]
        private static bool ValidateQuickRemoveColliders()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
        }
        #endregion

        #region GUI Implementation
        private void OnEnable()
        {
            Selection.selectionChanged += Repaint;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= Repaint;
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // Title Banner
            EditorGUILayout.Space(8);
            GUILayout.Label("Vacuum Protocol - Child Collider Helper", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Batch assigns Mesh or Box colliders to nested child meshes.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4);

            DrawSelectionOverview();

            EditorGUILayout.Space(6);
            DrawSettings();

            EditorGUILayout.Space(10);
            DrawActionButtons();

            EditorGUILayout.EndScrollView();
        }

        private void DrawSelectionOverview()
        {
            EditorGUILayout.BeginVertical("helpbox");
            GUILayout.Label("Target Selection", EditorStyles.boldLabel);

            GameObject[] selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
            {
                EditorGUILayout.HelpBox("No GameObjects selected in Hierarchy.\nPlease select a parent object (e.g. 'mesh' or room parts).", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            int totalMeshes = 0;
            int withColliders = 0;
            int withoutColliders = 0;

            foreach (GameObject go in selected)
            {
                if (go == null) continue;
                MeshFilter[] mfs = go.GetComponentsInChildren<MeshFilter>(_includeInactive);
                foreach (MeshFilter mf in mfs)
                {
                    if (mf == null || mf.sharedMesh == null) continue;
                    totalMeshes++;
                    if (mf.GetComponent<Collider>() != null)
                    {
                        withColliders++;
                    }
                    else
                    {
                        withoutColliders++;
                    }
                }
            }

            EditorGUILayout.LabelField($"Selected Objects: {selected.Length} (e.g. '{selected[0].name}')");
            EditorGUILayout.LabelField($"Total Child Meshes: {totalMeshes}");
            EditorGUILayout.LabelField($"Meshes With Colliders: {withColliders}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Meshes Missing Colliders: {withoutColliders}", EditorStyles.boldLabel);

            EditorGUILayout.EndVertical();
        }

        private void DrawSettings()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Collider & Layer Configuration", EditorStyles.boldLabel);

            _colliderType = (TargetColliderType)EditorGUILayout.EnumPopup("Collider Type", _colliderType);

            if (_colliderType == TargetColliderType.MeshCollider)
            {
                _convex = EditorGUILayout.Toggle(new GUIContent("Convex", "Required for dynamic Rigidbodies or triggers. Leave unchecked for static floors/walls."), _convex);
            }

            // Layer popup
            string[] layerNames = GetAvailableLayerNames();
            _selectedLayerIndex = EditorGUILayout.Popup("Target Layer", _selectedLayerIndex, layerNames);

            _physicsMaterial = (PhysicsMaterial)EditorGUILayout.ObjectField("Physics Material", _physicsMaterial, typeof(PhysicsMaterial), false);

            EditorGUILayout.Space(4);
            _includeInactive = EditorGUILayout.Toggle("Include Inactive Children", _includeInactive);
            _replaceExisting = EditorGUILayout.Toggle(new GUIContent("Replace Existing Colliders", "If checked, overwrites colliders already present on child objects."), _replaceExisting);

            EditorGUILayout.EndVertical();
        }

        private void DrawActionButtons()
        {
            GameObject[] selected = Selection.gameObjects;
            bool hasSelection = selected != null && selected.Length > 0;

            GUI.enabled = hasSelection;

            GUI.backgroundColor = new Color(0.25f, 0.95f, 0.4f);
            if (GUILayout.Button("Add / Update Colliders on Children", GUILayout.Height(38)))
            {
                int targetLayer = GetTargetLayerIndex();
                ProcessSelectedObjects(_colliderType, _convex, targetLayer, _physicsMaterial, _includeInactive, _replaceExisting);
                Repaint();
            }

            GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
            if (GUILayout.Button("Select Meshes Missing Colliders in Hierarchy", GUILayout.Height(26)))
            {
                SelectChildrenMissingColliders(_includeInactive);
            }

            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);
            if (GUILayout.Button("Remove Colliders from Children", GUILayout.Height(28)))
            {
                if (EditorUtility.DisplayDialog("Confirm Collider Removal",
                    $"Remove all colliders from children of the {selected.Length} selected object(s)?\nThis can be undone with Ctrl+Z.",
                    "Remove", "Cancel"))
                {
                    RemoveCollidersFromSelectedObjects(_includeInactive);
                    Repaint();
                }
            }

            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
        }

        private string[] GetAvailableLayerNames()
        {
            List<string> list = new List<string> { "Keep Current Layer" };
            for (int i = 0; i < 32; i++)
            {
                string layerName = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(layerName))
                {
                    list.Add($"{i}: {layerName}");
                }
            }
            return list.ToArray();
        }

        private int GetTargetLayerIndex()
        {
            if (_selectedLayerIndex == 0) return -1; // Keep current
            string[] layers = GetAvailableLayerNames();
            string selectedText = layers[_selectedLayerIndex];
            int colonIndex = selectedText.IndexOf(':');
            if (colonIndex > 0 && int.TryParse(selectedText.Substring(0, colonIndex), out int layerNum))
            {
                return layerNum;
            }
            return -1;
        }
        #endregion

        #region Core Processing Logic
        public static void ProcessSelectedObjects(
            TargetColliderType colliderType,
            bool convex,
            int targetLayer,
            PhysicsMaterial physicsMaterial,
            bool includeInactive,
            bool replaceExisting)
        {
            GameObject[] targets = Selection.gameObjects;
            if (targets == null || targets.Length == 0)
            {
                EditorUtility.DisplayDialog("Collider Helper", "Please select at least one GameObject in the Hierarchy.", "OK");
                return;
            }

            Undo.SetCurrentGroupName("Batch Add Colliders to Children");
            int undoGroup = Undo.GetCurrentGroup();

            int totalAdded = 0;
            int totalUpdated = 0;
            int totalScanned = 0;

            foreach (GameObject rootGo in targets)
            {
                if (rootGo == null) continue;

                MeshFilter[] meshFilters = rootGo.GetComponentsInChildren<MeshFilter>(includeInactive);
                totalScanned += meshFilters.Length;

                foreach (MeshFilter mf in meshFilters)
                {
                    if (mf == null || mf.sharedMesh == null) continue;
                    GameObject childGo = mf.gameObject;

                    Collider existing = childGo.GetComponent<Collider>();
                    if (existing != null)
                    {
                        if (!replaceExisting)
                        {
                            if (targetLayer != -1 && childGo.layer != targetLayer)
                            {
                                Undo.RecordObject(childGo, "Change Layer");
                                childGo.layer = targetLayer;
                                totalUpdated++;
                            }
                            if (physicsMaterial != null && existing.sharedMaterial != physicsMaterial)
                            {
                                Undo.RecordObject(existing, "Assign PhysicsMaterial");
                                existing.sharedMaterial = physicsMaterial;
                                totalUpdated++;
                            }
                            continue;
                        }
                        Undo.DestroyObjectImmediate(existing);
                    }

                    if (colliderType == TargetColliderType.MeshCollider)
                    {
                        MeshCollider mc = Undo.AddComponent<MeshCollider>(childGo);
                        mc.sharedMesh = mf.sharedMesh;
                        mc.convex = convex;
                        if (physicsMaterial != null) mc.sharedMaterial = physicsMaterial;
                        totalAdded++;
                    }
                    else if (colliderType == TargetColliderType.BoxCollider)
                    {
                        BoxCollider box = Undo.AddComponent<BoxCollider>(childGo);
                        box.center = mf.sharedMesh.bounds.center;
                        box.size = mf.sharedMesh.bounds.size;
                        if (physicsMaterial != null) box.sharedMaterial = physicsMaterial;
                        totalAdded++;
                    }

                    if (targetLayer != -1)
                    {
                        Undo.RecordObject(childGo, "Change Layer");
                        childGo.layer = targetLayer;
                    }

                    EditorUtility.SetDirty(childGo);
                }

                if (!Application.isPlaying && rootGo.scene.IsValid())
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rootGo.scene);
                }
            }

            Undo.CollapseUndoOperations(undoGroup);

            string layerInfo = targetLayer != -1 ? $" (Layer: {LayerMask.LayerToName(targetLayer)})" : "";
            Debug.Log($"<color=green>[ColliderUtility]</color> Scanned {totalScanned} child meshes across {targets.Length} object(s): <b>Added {totalAdded}</b> {colliderType}s, <b>Updated {totalUpdated}</b>{layerInfo}. (Undo with Ctrl+Z)");
        }

        public static void SetLayerOnSelectedChildren(int targetLayer)
        {
            GameObject[] targets = Selection.gameObjects;
            if (targets == null || targets.Length == 0) return;

            Undo.SetCurrentGroupName("Set Layer on Children");
            int undoGroup = Undo.GetCurrentGroup();

            int count = 0;
            foreach (GameObject rootGo in targets)
            {
                if (rootGo == null) continue;
                Collider[] colliders = rootGo.GetComponentsInChildren<Collider>(true);
                foreach (Collider c in colliders)
                {
                    if (c == null) continue;
                    Undo.RecordObject(c.gameObject, "Set Layer");
                    c.gameObject.layer = targetLayer;
                    EditorUtility.SetDirty(c.gameObject);
                    count++;
                }

                if (!Application.isPlaying && rootGo.scene.IsValid())
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rootGo.scene);
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"<color=green>[ColliderUtility]</color> Set layer to '<b>{LayerMask.LayerToName(targetLayer)}</b>' on {count} child objects.");
        }

        public static void RemoveCollidersFromSelectedObjects(bool includeInactive)
        {
            GameObject[] targets = Selection.gameObjects;
            if (targets == null || targets.Length == 0) return;

            Undo.SetCurrentGroupName("Remove Colliders from Children");
            int undoGroup = Undo.GetCurrentGroup();

            int count = 0;
            foreach (GameObject rootGo in targets)
            {
                if (rootGo == null) continue;
                Collider[] colliders = rootGo.GetComponentsInChildren<Collider>(includeInactive);
                foreach (Collider c in colliders)
                {
                    if (c == null) continue;
                    if (c.gameObject == rootGo) continue; // Don't delete on selection root itself

                    Undo.DestroyObjectImmediate(c);
                    count++;
                }

                if (!Application.isPlaying && rootGo.scene.IsValid())
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rootGo.scene);
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"<color=orange>[ColliderUtility]</color> Removed {count} colliders from children across {targets.Length} object(s).");
        }

        public static void SelectChildrenMissingColliders(bool includeInactive)
        {
            GameObject[] targets = Selection.gameObjects;
            if (targets == null || targets.Length == 0) return;

            List<GameObject> missing = new List<GameObject>();
            foreach (GameObject rootGo in targets)
            {
                if (rootGo == null) continue;
                MeshFilter[] mfs = rootGo.GetComponentsInChildren<MeshFilter>(includeInactive);
                foreach (MeshFilter mf in mfs)
                {
                    if (mf == null || mf.sharedMesh == null) continue;
                    if (mf.GetComponent<Collider>() == null)
                    {
                        missing.Add(mf.gameObject);
                    }
                }
            }

            if (missing.Count > 0)
            {
                Selection.objects = missing.ToArray();
                Debug.Log($"<color=cyan>[ColliderUtility]</color> Selected {missing.Count} child meshes missing colliders.");
            }
            else
            {
                EditorUtility.DisplayDialog("Collider Helper", "All child meshes already have colliders!", "OK");
            }
        }
        #endregion
    }
}
