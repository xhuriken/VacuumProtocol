using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Reflection;

public class ModelMigrator : MonoBehaviour
{
    [Required] public GameObject oldModel;
    [Required] public GameObject newModel;

    [Button(ButtonSizes.Large), GUIColor(0, 1, 0)]
    public void Migrate()
    {
        if (oldModel == null || newModel == null) return;

        Debug.Log("--- Début de la migration ---");

        // 1. Créer un dictionnaire de correspondance des noms pour le remapping
        Dictionary<string, Transform> newModelNodes = new Dictionary<string, Transform>();
        foreach (Transform t in newModel.GetComponentsInChildren<Transform>(true))
        {
            if (!newModelNodes.ContainsKey(t.name))
                newModelNodes.Add(t.name, t);
        }

        // 2. Parcourir l'ancien modèle et transférer les composants
        Transform[] oldNodes = oldModel.GetComponentsInChildren<Transform>(true);
        foreach (Transform oldNode in oldNodes)
        {
            if (oldNode == oldModel.transform) continue;

            if (newModelNodes.TryGetValue(oldNode.name, out Transform newNode))
            {
                TransferComponents(oldNode.gameObject, newNode.gameObject);
            }
        }

        // 3. Remapper les références sur le root (le Player)
        RemapReferences(gameObject, newModelNodes);

        Debug.Log("--- Migration terminée ! ---");
    }

    private void TransferComponents(GameObject source, GameObject destination)
    {
        Component[] components = source.GetComponents<Component>();
        foreach (var comp in components)
        {
            // On ignore les composants de base du mesh
            if (comp is Transform || comp is SkinnedMeshRenderer || comp is MeshFilter || comp is MeshRenderer)
                continue;

            // Utilise UnityEditorInternal pour copier/coller proprement si on est en éditeur
            #if UNITY_EDITOR
            UnityEditorInternal.ComponentUtility.CopyComponent(comp);
            Component existing = destination.GetComponent(comp.GetType());
            if (existing)
            {
                UnityEditorInternal.ComponentUtility.PasteComponentValues(existing);
            }
            else
            {
                UnityEditorInternal.ComponentUtility.PasteComponentAsNew(destination);
            }
            #endif
        }
    }

    private void RemapReferences(GameObject root, Dictionary<string, Transform> mapping)
    {
        MonoBehaviour[] scripts = root.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var script in scripts)
        {
            if (script == this) continue;

            FieldInfo[] fields = script.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                // Si le champ est un Transform ou un GameObject
                if (field.FieldType == typeof(Transform) || field.FieldType == typeof(GameObject))
                {
                    Object val = field.GetValue(script) as Object;
                    if (val != null)
                    {
                        // Si l'objet référencé fait partie de l'ancien modèle
                        if (IsChildOf(val, oldModel.transform))
                        {
                            if (mapping.TryGetValue(val.name, out Transform newTarget))
                            {
                                field.SetValue(script, field.FieldType == typeof(GameObject) ? newTarget.gameObject : (Object)newTarget);
                                Debug.Log($"Remappage : {script.GetType().Name}.{field.Name} -> {newTarget.name}");
                            }
                        }
                    }
                }
            }
        }
    }

    private bool IsChildOf(Object obj, Transform root)
    {
        Transform t = null;
        if (obj is GameObject go) t = go.transform;
        else if (obj is Component c) t = c.transform;

        if (t == null) return false;
        return t.IsChildOf(root);
    }
}
