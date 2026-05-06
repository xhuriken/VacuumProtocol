using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Helper utility to migrate components and references from an old 3D model hierarchy to a new one.
/// Useful when replacing character models while keeping script references and custom objects intact.
/// </summary>
public class ModelMigrator : MonoBehaviour
{
    [Title("Migration Setup")]
    [Required, Tooltip("The old model currently inside the Player hierarchy.")]
    public GameObject oldModel;
    
    [Required, Tooltip("The new model (Prefab or scene object) to migrate to.")]
    public GameObject newModel;

    /// <summary>
    /// Executes the migration process by instantiating the new model, transferring components, 
    /// moving manual objects, and updating script references.
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
    /// Copies components from a source object to a destination object, avoiding core structural components.
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
    /// Scans all scripts on the Player and updates Transform/GameObject fields to point to the new model.
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

    private bool IsChildOf(Object obj, Transform potentialParent)
    {
        Transform t = null;
        if (obj is GameObject go) t = go.transform;
        else if (obj is Component c) t = c.transform;
        return t != null && t.IsChildOf(potentialParent);
    }
}
