using UnityEngine;

/// <summary>
/// Description: Affiche un HUD pour montrer où la caméra pointe (Cible) et où la buse pointe réellement (Réel).
/// Context: Peut être attaché au joueur ou à un Canvas.
/// Justification: Permet au joueur de comprendre la différence entre sa visée et l'orientation physique de son bras.
/// </summary>
public class PlayerV2_DynamicCrosshair : MonoBehaviour
{
    [Header("References")]
    public PlayerV2_Controller Player;
    public Camera MainCamera;

    [Header("UI Elements / Transforms")]
    [Tooltip("Le Transform qui représente la cible désirée (rose)")]
    public Transform CameraTargetCrosshair; 

    [Header("Settings")]
    public float ProjectionDistance = 20f;
    public float SmoothSpeed = 25f;
    [Tooltip("Si vrai, déplace les objets en coordonnées d'écran (UI Canvas). Si faux, les déplace en 3D dans le monde devant la caméra.")]
    public bool UseScreenSpace = true;

    private PlayerV2_Arms _arms;

    private void Start()
    {
        if (MainCamera == null) MainCamera = Camera.main;
        if (Player == null) Player = FindFirstObjectByType<PlayerV2_Controller>();
        if (Player != null) _arms = Player.GetComponent<PlayerV2_Arms>();
    }

    private void Update()
    {
        if (Player == null || MainCamera == null) return;

        // 1. Position désirée (Caméra)
        if (CameraTargetCrosshair != null && Player.CameraTransform != null)
        {
            Vector3 virtualTarget = Player.CameraTransform.position + Player.CameraTransform.forward * ProjectionDistance;
            
            if (UseScreenSpace)
            {
                Vector3 screenPos = MainCamera.WorldToScreenPoint(virtualTarget);
                if (screenPos.z > 0)
                {
                    CameraTargetCrosshair.position = Vector3.Lerp(CameraTargetCrosshair.position, screenPos, Time.deltaTime * SmoothSpeed);
                    CameraTargetCrosshair.gameObject.SetActive(true);
                }
                else CameraTargetCrosshair.gameObject.SetActive(false);
            }
            else
            {
                CameraTargetCrosshair.position = Vector3.Lerp(CameraTargetCrosshair.position, virtualTarget, Time.deltaTime * SmoothSpeed);
                // Optionnel: Faire pointer le crosshair vers la caméra
                CameraTargetCrosshair.rotation = Quaternion.LookRotation(CameraTargetCrosshair.position - MainCamera.transform.position);
                CameraTargetCrosshair.gameObject.SetActive(true);
            }
        }
    }
}
