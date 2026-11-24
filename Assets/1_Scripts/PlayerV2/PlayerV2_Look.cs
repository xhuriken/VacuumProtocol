using UnityEngine;
using Mirror;

/// <summary>
/// Description: Handles the turret (Torso) rotation and the camera pitch.
/// Context: Attached to Player_V2.
/// Justification: Rotates the Torso through physics (MoveRotation), which lets the Torso-Hips joint handle the link without glitches.
/// </summary>
[RequireComponent(typeof(PlayerV2_Controller))]
public class PlayerV2_Look : NetworkBehaviour
{
    [Header("Settings")]
    [Tooltip("Role: Multiplier applied to the raw look input (degrees per input unit).")]
    public float Sensitivity = 0.15f;
    [Tooltip("Role: Maximum rotation speed (in degrees per second) so the torso never turns too fast for the physics.")]
    public float MaxTurnSpeed = 720f;
    [Tooltip("Role: Lowest camera pitch, in degrees.")]
    public float MinPitch = -85f;
    [Tooltip("Role: Highest camera pitch, in degrees.")]
    public float MaxPitch = 85f;

    [Header("Camera Fluidity")]
    [Tooltip("Role: 0 = fully stable camera (direct mouse motion). 1 = camera attached to the physical head bounce.")]
    [Range(0f, 1f)]
    public float HeadMovementBlend = 0f;

    [Header("Debug")]
    [Tooltip("Role: Prints this component's informational Debug.Log messages. Warnings and errors always print.")]
    [SerializeField] private bool _enableDebugLogs = false;

    [Header("Network Sync")]
    [Tooltip("Role: Camera pitch synchronized from the owner, used by remote clients to orient the camera.")]
    [SyncVar]
    public float SyncPitch;

    private PlayerV2_Controller _controller;
    private PlayerInputHandler _input; // Reuses the existing input handler
    
    private float _cameraPitch;
    private float _torsoYaw;
    private Camera _playerCamera;

    /// <summary>Current torso yaw accumulated from the look input, in degrees.</summary>
    public float CurrentYaw => _torsoYaw;

    private void Awake()
    {
        _controller = GetComponent<PlayerV2_Controller>();
        _input = GetComponent<PlayerInputHandler>();
    }

    /// <summary>Locks the cursor, initializes the yaw, applies the settings FOV and binds every Screen Space - Camera Canvas to the local player camera.</summary>
    public override void OnStartLocalPlayer()
    {
        Cursor.lockState = CursorLockMode.Locked;
        // Initialize the yaw from the current Torso rotation
        if (_controller.TorsoRigidbody != null)
        {
            _torsoYaw = _controller.TorsoRigidbody.rotation.eulerAngles.y;
        }
        
        // Apply the FOV from the settings and bind the camera to the Canvases
        if (_enableDebugLogs) Debug.Log($"[CanvasBinding] PlayerV2_Look.OnStartLocalPlayer executed. Controller={_controller?.name}, CameraTransform={_controller?.CameraTransform?.name}");

        Camera cam = null;
        if (_controller != null && _controller.CameraTransform != null)
        {
            cam = _controller.CameraTransform.GetComponentInChildren<Camera>(true);
        }
        if (cam == null && _controller != null)
        {
            cam = _controller.GetComponentInChildren<Camera>(true);
        }
        if (cam == null)
        {
            cam = Camera.main;
        }

        _playerCamera = cam;
        if (_enableDebugLogs) Debug.Log($"[CanvasBinding] Camera found for the player: {(_playerCamera != null ? _playerCamera.name : "NONE (NULL)")}");

        if (_playerCamera != null)
        {
            if (SettingsManager.Instance != null && SettingsManager.Instance.CurrentSettings != null)
            {
                _playerCamera.fieldOfView = SettingsManager.Instance.CurrentSettings.FieldOfView;
                SettingsManager.Instance.OnSettingsChanged += HandleSettingsChanged;
                if (_enableDebugLogs) Debug.Log($"[PlayerV2_Look] Initial FOV applied to '{_playerCamera.name}': {SettingsManager.Instance.CurrentSettings.FieldOfView}° and OnSettingsChanged listener enabled.");
            }

            // Automatically bind every Screen Space - Camera Canvas of the scene (active or inactive)
            Canvas[] sceneCanvases = Resources.FindObjectsOfTypeAll<Canvas>();
            if (_enableDebugLogs) Debug.Log($"[CanvasBinding] Scanning {sceneCanvases.Length} Canvases found in resources...");

            int boundCount = 0;
            foreach (Canvas canvas in sceneCanvases)
            {
                if (canvas != null && canvas.gameObject.scene.isLoaded)
                {
                    if (_enableDebugLogs) Debug.Log($"[CanvasBinding] Canvas '{canvas.name}' detected | Mode={canvas.renderMode} | WorldCam={canvas.worldCamera?.name ?? "null"}");
                    if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                    {
                        canvas.worldCamera = _playerCamera;
                        canvas.planeDistance = 0.5f;
                        boundCount++;
                        if (_enableDebugLogs) Debug.Log($"[CanvasBinding] -> SUCCESS: Camera '{_playerCamera.name}' assigned to '{canvas.name}'");
                    }
                }
            }
            if (_enableDebugLogs) Debug.Log($"[CanvasBinding] Total ScreenSpaceCamera Canvases bound: {boundCount}");
        }
    }

    private void HandleSettingsChanged(SettingsData settings)
    {
        if (settings != null)
        {
            if (_playerCamera == null)
            {
                if (_controller != null && _controller.CameraTransform != null)
                {
                    _playerCamera = _controller.CameraTransform.GetComponentInChildren<Camera>(true);
                }
                if (_playerCamera == null)
                {
                    _playerCamera = Camera.main;
                }
            }

            if (_playerCamera != null)
            {
                _playerCamera.fieldOfView = settings.FieldOfView;
                if (_enableDebugLogs) Debug.Log($"[PlayerV2_Look] HandleSettingsChanged -> FOV of '{_playerCamera.name}' updated to {settings.FieldOfView}°");
            }
            else
            {
                Debug.LogError($"[PlayerV2_Look] HandleSettingsChanged ERROR: _playerCamera not found on '{gameObject.name}'!");
            }
        }
    }

    private void OnDestroy()
    {
        if (SettingsManager.HasInstance && SettingsManager.Instance != null)
        {
            SettingsManager.Instance.OnSettingsChanged -= HandleSettingsChanged;
        }
    }

    /// <summary>Asks the server to update the synchronized camera pitch.</summary>
    [Command]
    public void CmdSetPitch(float pitch)
    {
        SyncPitch = pitch;
    }

    private void Update()
    {
        if (!isOwned) return;
        if (_input == null) return;

        Vector2 lookInput = _input.LookInput;
        
        float targetYawDelta = lookInput.x * Sensitivity;
        float targetPitchDelta = lookInput.y * Sensitivity;

        // Clamp the rotation speed so the physics (head/neck) can keep up:
        // the max speed (degrees/second) is converted to a max delta per frame
        float maxDeltaPerFrame = MaxTurnSpeed * Time.deltaTime;
        float clampedYawDelta = Mathf.Clamp(targetYawDelta, -maxDeltaPerFrame, maxDeltaPerFrame);
        float clampedPitchDelta = Mathf.Clamp(targetPitchDelta, -maxDeltaPerFrame, maxDeltaPerFrame);

        _torsoYaw += clampedYawDelta;
        _cameraPitch -= clampedPitchDelta;
        _cameraPitch = Mathf.Clamp(_cameraPitch, MinPitch, MaxPitch);

        // Sync the pitch over the network only on significant change (saves bandwidth)
        if (Mathf.Abs(SyncPitch - _cameraPitch) > 1f)
        {
            CmdSetPitch(_cameraPitch);
        }
    }

    private void LateUpdate()
    {
        if (_controller.CameraTransform != null)
        {
            float currentPitch = isOwned ? _cameraPitch : SyncPitch;
            float currentYaw = isOwned ? _torsoYaw : (_controller.TorsoRigidbody != null ? _controller.TorsoRigidbody.rotation.eulerAngles.y : 0f);
            bool isCrouching = isOwned ? (_input != null && _input.IsCrouching) : _controller.IsCrouching;

            Quaternion finalTargetRot;

            // Target 0: fully stable, pure math input or network value
            Quaternion targetStable = Quaternion.Euler(currentPitch, currentYaw, 0f);

            if (_controller.HeadController != null && _controller.HeadController.NeckJoints.Length > 0)
            {
                // When crouching the physical head stays at 0 (horizontal), otherwise it takes X% of the pitch
                float headPitch = isCrouching ? 0f : (currentPitch * _controller.HeadController.PitchMultiplier);
                
                if (isOwned)
                {
                    // Tell the head controller the desired target
                    _controller.HeadController.SetTargetPitch(isCrouching ? 0f : currentPitch);
                }

                // Target 1: physical head bounce
                Rigidbody headRb = _controller.HeadController.NeckJoints[_controller.HeadController.NeckJoints.Length - 1].GetComponent<Rigidbody>();
                
                // The camera makes up the share of pitch the head did not take
                float remainingPitch = currentPitch - headPitch;
                
                // Apply the remaining share on top of the physical head transform
                Quaternion targetHead = headRb.rotation * Quaternion.Euler(remainingPitch, 0f, 0f);

                // Blend between "pure FPS" and "physical body" (fully linear)
                finalTargetRot = Quaternion.Slerp(targetStable, targetHead, HeadMovementBlend);
            }
            else
            {
                finalTargetRot = targetStable;
            }

            // Apply the blended rotation directly.
            // NEVER use a Slerp with Time.deltaTime here, otherwise the camera lags behind the physics and stutters.
            _controller.CameraTransform.rotation = finalTargetRot;
        }
    }

    private void FixedUpdate()
    {
        if (!isOwned) return;
        if (_controller.TorsoRigidbody != null)
        {
            // Physical Torso rotation. The ConfigurableJoint with the Hips keeps the torso attached.
            // On a slope the joint enforces the tilt; this only provides the base heading.
            Quaternion targetRot = Quaternion.Euler(0f, _torsoYaw, 0f);
            _controller.TorsoRigidbody.MoveRotation(targetRot);
        }
    }
}

