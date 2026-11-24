using UnityEngine;

/// <summary>
/// Description: HUD showing where the camera aims (Target) and where the nozzle actually points (Actual).
/// Context: Can be attached to the player or to a Canvas.
/// Justification: Helps the player understand the gap between their aim and the physical orientation of their arm.
/// </summary>
public class PlayerV2_DynamicCrosshair : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Role: Player whose camera aim is projected. Falls back to the first PlayerV2_Controller found if empty.")]
    public PlayerV2_Controller Player;
    [Tooltip("Role: Camera used for world-to-screen projection. Falls back to Camera.main if empty.")]
    public Camera MainCamera;

    [Header("UI Elements / Transforms")]
    [Tooltip("Role: Transform representing the desired target (pink).")]
    public Transform CameraTargetCrosshair; 

    [Header("Settings")]
    [Tooltip("Role: Distance in front of the camera at which the virtual aim target is projected.")]
    public float ProjectionDistance = 20f;
    [Tooltip("Role: Lerp speed of the crosshair toward its target position.")]
    public float SmoothSpeed = 25f;
    [Tooltip("Role: If true, moves the objects in screen coordinates (UI Canvas). If false, moves them in 3D world space in front of the camera.")]
    public bool UseScreenSpace = true;

    private PlayerV2_Arms _arms;

    private void Start()
    {
        if (MainCamera == null) MainCamera = Camera.main;
        if (Player == null) Player = FindFirstObjectByType<PlayerV2_Controller>();
        if (Player != null) _arms = Player.GetComponent<PlayerV2_Arms>();

        if (Player != null && !Player.isOwned)
        {
            gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (Player == null || MainCamera == null) return;

        // Hide the dynamic crosshair while the player is paused (Escape menu)
        if (Player.IsPaused)
        {
            if (CameraTargetCrosshair != null && CameraTargetCrosshair.gameObject.activeSelf)
            {
                CameraTargetCrosshair.gameObject.SetActive(false);
            }
            return;
        }

        // 1. Desired position (camera)
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
                // Optional: make the crosshair face the camera
                CameraTargetCrosshair.rotation = Quaternion.LookRotation(CameraTargetCrosshair.position - MainCamera.transform.position);
                CameraTargetCrosshair.gameObject.SetActive(true);
            }
        }
    }
}
