using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Reflection;

public class ModelMigrator : MonoBehaviour
{
    [Title("Migration Setup")]
    [Required, Tooltip("L'ancien modèle (ex: unit_vacuum) actuellement dans le Player")]
    public GameObject oldModel;
    
    [Required, Tooltip("Le nouveau modèle (Prefab ou objet de scène)")]
    public GameObject newModel;

    [Button(ButtonSizes.Large), GUIColor(0, 1, 0)]
    public void PerfectMigration()
    {
        if (oldModel == null || newModel == null) return;

        Debug.Log("<color=green><b>--- Début de la Migration Parfaite ---</b></color>");

        GameObject instantiatedModel = newModel;

        // 1. Si le nouveau modèle est un Prefab dans les dossiers, on l'instancie proprement
        #if UNITY_EDITOR
        if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(newModel))
        {
            instantiatedModel = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(newModel);
        }
        #endif

        // 2. On place le nouveau modèle au même endroit que l'ancien root
        instantiatedModel.transform.SetParent(transform);
        instantiatedModel.transform.localPosition = oldModel.transform.localPosition;
        instantiatedModel.transform.localRotation = oldModel.transform.localRotation;
        instantiatedModel.transform.localScale = oldModel.transform.localScale;

        // 3. Mapper tous les nouveaux nodes par nom
        Dictionary<string, Transform> newNodes = new Dictionary<string, Transform>();
        foreach (Transform t in instantiatedModel.GetComponentsInChildren<Transform>(true))
        {
            if (!newNodes.ContainsKey(t.name)) newNodes.Add(t.name, t);
        }

        // 4. Parcourir l'ancienne hiérarchie
        Transform[] oldTransforms = oldModel.GetComponentsInChildren<Transform>(true);
        
        foreach (Transform oldT in oldTransforms)
        {
            if (oldT == oldModel.transform) continue;

            if (newNodes.TryGetValue(oldT.name, out Transform newT))
            {
                // A. Transférer les composants (scripts, colliders, etc.)
                // NOTE: On ne touche pas au Transform ici !
                TransferComponents(oldT.gameObject, newT.gameObject);

                // B. Déménager les objets "Unity-Only" (ex: Caméra, Lights)
                List<Transform> childrenToMove = new List<Transform>();
                for (int i = 0; i < oldT.childCount; i++)
                {
                    Transform child = oldT.GetChild(i);
                    // Si cet enfant n'existe pas dans le nouveau modèle, c'est un objet manuel
                    if (!newNodes.ContainsKey(child.name))
                    {
                        childrenToMove.Add(child);
                    }
                }

                foreach (Transform child in childrenToMove)
                {
                    child.SetParent(newT);
                    Debug.Log($"<color=cyan>Déménagement : {child.name} déplacé vers {newT.name}</color>");
                }
            }
        }

        // 5. Mettre à jour toutes les références dans les scripts du Player
        UpdateAllReferencesInPlayer(newNodes);

        // 6. Finalisation
        oldModel.SetActive(false);
        Debug.Log("<color=green><b>--- Migration terminée avec succès ! ---</b></color>");
    }

    private void TransferComponents(GameObject source, GameObject destination)
    {
        foreach (var comp in source.GetComponents<Component>())
        {
            // On ignore ABSOLUMENT tout ce qui touche à la structure du nouveau modèle
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
