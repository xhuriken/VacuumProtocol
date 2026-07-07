using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Description: Helper utility to migrate components and references from an old 3D model hierarchy to a new one.
/// Context: Editor-only script attached to a temporary migration object.
/// Justification: Re-rigging a player model manually is tedious. This automates transferring colliders, scripts, and manual children (like cameras) to a new bone hierarchy while preserving references.
/// </summary>
public class ModelMigrator : MonoBehaviour
{
    [Title("Migration Setup")]
    [Required, Tooltip("Role: The old model currently inside the Player hierarchy.\nUse Case: Source object.\nJustification: The script will pull components and children from this hierarchy.")]
    public GameObject oldModel;
    
    [Required, Tooltip("Role: The new model (Prefab or scene object) to migrate to.\nUse Case: Destination object.\nJustification: The script will paste components and reparent children to matching bones in this hierarchy.")]
    public GameObject newModel;

    /// <summary>
    /// Description: Executes the migration process.
    /// Context: Triggered via Odin Inspector button in the editor.
    /// Justification: Automates the entire process in one click: instantiation, component copying, child reparenting, and reference updating.
    /// </summary>
    [Button(ButtonSizes.Large), GUIColor(0, 1, 0)]
    public void PerfectMigration()
    {
        if (oldModel == null || newModel == null) return;

        Debug.Log("<color=green><b>--- Starting Perfect Migration ---</b></color>");

        GameObject instantiatedModel = newModel;

        // 1. If the new model is a Prefab asset, instantiate it properly in the scene
        #if UNITY_EDITOR
        if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(newModel))
        {
            instantiatedModel = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(newModel);
        }
        #endif

        // 2. Position the new model at the same root transform as the old one
        instantiatedModel.transform.SetParent(transform);
        instantiatedModel.transform.localPosition = oldModel.transform.localPosition;
        instantiatedModel.transform.localRotation = oldModel.transform.localRotation;
        instantiatedModel.transform.localScale = oldModel.transform.localScale;

        // 3. Map all nodes in the new model by their name for easy lookup
        Dictionary<string, Transform> newNodes = new Dictionary<string, Transform>();
        foreach (Transform t in instantiatedModel.GetComponentsInChildren<Transform>(true))
        {
            if (!newNodes.ContainsKey(t.name)) newNodes.Add(t.name, t);
        }

        // 4. Traverse the old hierarchy to find components and custom objects to migrate
        Transform[] oldTransforms = oldModel.GetComponentsInChildren<Transform>(true);
        
        foreach (Transform oldT in oldTransforms)
        {
            if (oldT == oldModel.transform) continue;

            if (newNodes.TryGetValue(oldT.name, out Transform newT))
            {
                // A. Transfer non-structural components (scripts, colliders, etc.)
                TransferComponents(oldT.gameObject, newT.gameObject);

                // B. Move "Unity-Only" objects (Cameras, Lights, etc.) that don't exist in the new model's source
                List<Transform> childrenToMove = new List<Transform>();
                for (int i = 0; i < oldT.childCount; i++)
                {
                    Transform child = oldT.GetChild(i);
                    // If this child is NOT part of the new model's name map, it's a manual addition
                    if (!newNodes.ContainsKey(child.name))
                    {
                        childrenToMove.Add(child);
                    }
                }

                foreach (Transform child in childrenToMove)
                {
                    child.SetParent(newT);
                    Debug.Log($"<color=cyan>[Migration] Moving: {child.name} relocated to {newT.name}</color>");
                }
            }
        }

        // 5. Update all script references in the Player hierarchy to point to the new nodes
        UpdateAllReferencesInPlayer(newNodes);

        // 6. Cleanup
        oldModel.SetActive(false);
        Debug.Log("<color=green><b>--- Migration completed successfully! ---</b></color>");
    }

    /// <summary>
    /// Description: Copies components from a source object to a destination object.
    /// Context: Internal helper.
    /// Justification: Explicitly avoids copying structural components (Transforms, Renderers, Animators) so that the new model's geometry isn't corrupted by the old one.
    /// </summary>
    private void TransferComponents(GameObject source, GameObject destination)
    {
        foreach (var comp in source.GetComponents<Component>())
        {
            // Ignore structural components belonging to the 3D model itself
            if (comp is Transform || comp is SkinnedMeshRenderer || comp is MeshFilter || comp is MeshRenderer || comp is Animator)
                continue;

            #if UNITY_EDITOR
            UnityEditorInternal.ComponentUtility.CopyComponent(comp);
            Component existing = destination.GetComponent(comp.GetType());
            if (existing) UnityEditorInternal.ComponentUtility.PasteComponentValues(existing);
            else UnityEditorInternal.ComponentUtility.PasteComponentAsNew(destination);
            #endif
        }
    }

    /// <summary>
    /// Description: Scans all scripts on the Player and updates Transform/GameObject fields to point to the new model.
    /// Context: Internal helper.
    /// Justification: Uses Reflection to find all fields in all MonoBehaviours and remaps them automatically so designers don't have to manually drag-and-drop references in the inspector after migration.
    /// </summary>
    private void UpdateAllReferencesInPlayer(Dictionary<string, Transform> mapping)
    {
        MonoBehaviour[] allScripts = GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var script in allScripts)
        {
            if (script == this) continue;

            FieldInfo[] fields = script.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(Transform) || field.FieldType == typeof(GameObject))
                {
                    Object val = field.GetValue(script) as Object;
                    if (val != null && IsChildOf(val, oldModel.transform))
                    {
                        if (mapping.TryGetValue(val.name, out Transform newTarget))
                        {
                            field.SetValue(script, field.FieldType == typeof(GameObject) ? newTarget.gameObject : (Object)newTarget);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Description: Checks if an object is a child of a given parent transform.
    /// Context: Used during reference remapping.
    /// Justification: We only want to remap references that point to the old model; external references (like global managers) should be left alone.
    /// </summary>
    private bool IsChildOf(Object obj, Transform potentialParent)
    {
        Transform t = null;
        if (obj is GameObject go) t = go.transform;
        else if (obj is Component c) t = c.transform;
        return t != null && t.IsChildOf(potentialParent);
    }
}
