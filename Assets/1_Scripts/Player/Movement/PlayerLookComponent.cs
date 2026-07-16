using Mirror;
using UnityEngine;

namespace VacuumProtocol.Player
{
    /// <summary>
    /// Description: Handles mouse-look rotation for the camera (pitch/yaw) and the torso bone (yaw).
    /// Context: Attached to the player prefab.
    /// Justification: Decouples view logic from movement logic. Rotates the torso bone for looking yaw instead of the root.
    /// </summary>
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerLookComponent : NetworkBehaviour
    {
        [Header("Settings")]
        [Tooltip("Role: The camera transform to rotate (pitch & yaw).\nUse Case: View rotation.\nJustification: Camera is a child of Head. We override its world rotation to prevent sensitivity multiplier or double rotation.")]
        [SerializeField] private Transform _cameraTransform;
        
        [Tooltip("Role: Mouse look sensitivity.\nUse Case: Input scaling.\nJustification: Directly multiplies raw delta mouse inputs.")]
        [SerializeField] private float _sensitivity = 0.15f;
        
        [Tooltip("Role: Minimum pitch angle.\nUse Case: Look constraint.\nJustification: Prevents the player from looking too far up.")]
        [SerializeField] private float _minPitch = -85f;
        
        [Tooltip("Role: Maximum pitch angle.\nUse Case: Look constraint.\nJustification: Prevents the player from looking too far down.")]
        [SerializeField] private float _maxPitch = 85f;

        [Header("Wheels Visual Counter-Rotation")]
        [Tooltip("Role: Wheels chassis visual transform to counter-rotate.\nUse Case: Decoupling wheels rotation from look direction.\nJustification: Keep wheels facing travel direction independently of look yaw.")]
        [SerializeField] private Transform _wheelsChassisVisual;

        [Tooltip("Role: Enable detailed look logging.\nUse Case: Debugging yaw/pitch ranges.\nJustification: Allows developer inspection of local and synced look variables.")]
        [SerializeField] private bool _enableDebugLogs = false;

        [SyncVar]
        private float _syncedCameraPitch;

        [SyncVar]
        private float _syncedTorsoYaw;

        private PlayerInputHandler _input;
        private float _cameraPitch;
        private float _cameraYaw;
        private Rigidbody _rb;
        private Quaternion _originalWheelsLocalRot;



        /// <summary>
        /// Description: Gets the current look pitch angle (local or synced remote).
        /// </summary>
        public float CurrentPitch => isLocalPlayer ? _cameraPitch : _syncedCameraPitch;

        /// <summary>
        /// Description: Gets the current look yaw angle (local or synced remote).
        /// </summary>
        public float CurrentYaw => isLocalPlayer ? _cameraYaw : _syncedTorsoYaw;

        /// <summary>
        /// Description: Awake callback. Caches input handler.
        /// </summary>
        private void Awake()
        {
            _input = GetComponent<PlayerInputHandler>();
        }

        private void Start()
        {
            _rb = GetComponent<Rigidbody>();
            if (_rb == null)
            {
                throw new UnityEngine.MissingComponentException("[PlayerLookComponent] Player root must have a Rigidbody component!");
            }

            if (_wheelsChassisVisual != null)
            {
                _originalWheelsLocalRot = _wheelsChassisVisual.localRotation;
            }
        }

        /// <summary>
        /// Description: Initializes local player view settings.
        /// </summary>
        public override void OnStartLocalPlayer()
        {
            // Strict check: Camera transform cannot be null
            if (_cameraTransform == null)
            {
                throw new System.NullReferenceException("[PlayerLookComponent] Camera transform (_cameraTransform) is NOT assigned in the Inspector!");
            }
            
            Cursor.lockState = CursorLockMode.Locked;
        }

        /// <summary>
        /// Description: Command sent to server to replicate look pitch.
        /// </summary>
        [Command]
        private void CmdSyncCameraPitch(float pitch)
        {
            _syncedCameraPitch = pitch;
        }

        /// <summary>
        /// Description: Command sent to server to replicate torso yaw.
        /// </summary>
        [Command]
        private void CmdSyncTorsoYaw(float yaw)
        {
            _syncedTorsoYaw = yaw;
        }

        /// <summary>
        /// Description: Update callback. Handles local inputs and sync command execution.
        /// </summary>
        private void Update()
        {
            if (isLocalPlayer)
            {
                HandleInputRotation();

                // Replicate look angles if changed significantly
                if (Mathf.Abs(_syncedCameraPitch - _cameraPitch) > 0.1f)
                {
                    CmdSyncCameraPitch(_cameraPitch);
                }

                if (Mathf.Abs(_syncedTorsoYaw - _cameraYaw) > 0.1f)
                {
                    CmdSyncTorsoYaw(_cameraYaw);
                }

                if (_enableDebugLogs)
                {
                    Debug.Log($"[PlayerLookComponent] Local Pitch: {_cameraPitch:F2} | Yaw: {_cameraYaw:F2}");
                }
            }
        }

        /// <summary>
        /// Description: LateUpdate callback. Ensures the camera has 100% accurate look rotation in world space.
        /// Context: Applied in LateUpdate to override any wiggles from the physical head bone without lag.
        /// </summary>
        private void LateUpdate()
        {
            float targetYaw = isLocalPlayer ? _cameraYaw : _syncedTorsoYaw;
            float targetPitch = isLocalPlayer ? _cameraPitch : _syncedCameraPitch;

            if (_cameraTransform != null)
            {
                // Force camera world rotation to 100% accuracy matching look direction
                _cameraTransform.rotation = Quaternion.Euler(targetPitch, targetYaw, 0f);
            }

            if (_wheelsChassisVisual != null)
            {
                // Counter-rotate using the parent's actual interpolated visual world rotation to prevent any physics-vs-render jitter
                float parentYaw = transform.eulerAngles.y;
                _wheelsChassisVisual.localRotation = _originalWheelsLocalRot * Quaternion.Euler(0f, -parentYaw, 0f);
            }
        }

        /// <summary>
        /// Description: FixedUpdate callback. Updates Torso Rigidbody yaw using physical MoveRotation.
        /// </summary>
        private void FixedUpdate()
        {
            float targetYaw = isLocalPlayer ? _cameraYaw : _syncedTorsoYaw;

            if (_rb != null)
            {
                // Rotate the entire player root Rigidbody
                _rb.MoveRotation(Quaternion.Euler(0f, targetYaw, 0f));
            }
        }

        /// <summary>
        /// Description: Processes camera pitch and accumulates mouse yaw input.
        /// </summary>
        private void HandleInputRotation()
        {
            Vector2 lookInput = _input.LookInput;

            _cameraYaw += lookInput.x * _sensitivity;
            _cameraPitch -= lookInput.y * _sensitivity;
            _cameraPitch = Mathf.Clamp(_cameraPitch, _minPitch, _maxPitch);
        }
    }
}
