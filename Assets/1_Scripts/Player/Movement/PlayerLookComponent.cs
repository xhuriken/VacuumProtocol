using Mirror;
using UnityEngine;

    /// <summary>
    /// Description: Handles mouse-look rotation for the camera (pitch) and the player body (yaw).
    /// Context: Attached to the player prefab.
    /// Justification: Decouples view logic from movement logic. Uses horizontal mouse movement to rotate the player's body and vertical to rotate only the camera (pitch).
    /// </summary>
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerLookComponent : NetworkBehaviour
    {
        [Header("Settings")]
        [Tooltip("Role: The camera transform to pitch up/down.\nUse Case: View rotation.\nJustification: Must be isolated from the body so the player doesn't tilt forward when looking down.")]
        [SerializeField] private Transform _cameraTransform;
        
        [Tooltip("Role: Mouse look sensitivity.\nUse Case: Input scaling.\nJustification: Directly multiplies raw delta mouse inputs.")]
        [SerializeField] private float _sensitivity = 0.15f;
        
        [Tooltip("Role: Minimum pitch angle.\nUse Case: Look constraint.\nJustification: Prevents the player from breaking their neck looking too far up.")]
        [SerializeField] private float _minPitch = -85f;
        
        [Tooltip("Role: Maximum pitch angle.\nUse Case: Look constraint.\nJustification: Prevents the player from breaking their neck looking too far down.")]
        [SerializeField] private float _maxPitch = 85f;

        [SyncVar]
        private float _syncedCameraPitch;

        private PlayerInputHandler _input;
        private float _cameraPitch;

        /// <summary>
        /// Description: Awake callback. Caches references.
        /// Context: Lifecycle event.
        /// Justification: Fetches the InputHandler safely before Start.
        /// </summary>
        private void Awake()
        {
            _input = GetComponent<PlayerInputHandler>();
        }

        /// <summary>
        /// Description: Initializes local player view settings.
        /// Context: Mirror NetworkBehaviour callback.
        /// Justification: Hides and locks the cursor automatically for the person controlling this avatar, and auto-discovers the camera if it wasn't assigned in the inspector.
        /// </summary>
        public override void OnStartLocalPlayer()
        {
            if (_cameraTransform == null)
            {
                Camera cam = GetComponentInChildren<Camera>();
                if (cam != null) _cameraTransform = cam.transform;
            }
            
            Cursor.lockState = CursorLockMode.Locked;
        }

        /// <summary>
        /// Description: Command sent to server to replicate the local look pitch.
        /// Context: Network input sync.
        /// Justification: Replicates the float to other clients using Mirror's SyncVar.
        /// </summary>
        [Command]
        private void CmdSyncCameraPitch(float pitch)
        {
            _syncedCameraPitch = pitch;
        }

        /// <summary>
        /// Description: Update callback.
        /// Context: Update lifecycle event.
        /// Justification: Runs smoothly every frame to ensure responsive mouse look.
        /// </summary>
        private void Update()
        {
            if (isLocalPlayer)
            {
                HandleRotation();

                // Only send updates if the pitch has moved by a meaningful threshold
                if (Mathf.Abs(_syncedCameraPitch - _cameraPitch) > 0.5f)
                {
                    CmdSyncCameraPitch(_cameraPitch);
                }
            }
            else
            {
                // For remote players, update the local camera transform rotation so the head looks up/down
                if (_cameraTransform != null)
                {
                    _cameraTransform.localRotation = Quaternion.Euler(_syncedCameraPitch, 0, 0);
                }
            }
        }

        /// <summary>
        /// Description: Applies input values to rotation transforms.
        /// Context: Called during Update.
        /// Justification: Uses simple Euler angle modifications. Clamps pitch to prevent camera flipping.
        /// </summary>
        private void HandleRotation()
        {
            Vector2 lookInput = _input.LookInput;

            // Yaw (Body rotation)
            transform.Rotate(Vector3.up * (lookInput.x * _sensitivity));

            // Pitch (Camera rotation)
            _cameraPitch -= lookInput.y * _sensitivity;
            _cameraPitch = Mathf.Clamp(_cameraPitch, _minPitch, _maxPitch);
            
            if (_cameraTransform != null)
            {
                _cameraTransform.localRotation = Quaternion.Euler(_cameraPitch, 0, 0);
            }
        }
    }
