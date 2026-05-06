using Mirror;
using UnityEngine;

/// <summary>
    /// Handles mouse-look rotation for the camera (pitch) and the player body (yaw).
    /// </summary>
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerLookComponent : NetworkBehaviour
    {
        [Header("Settings")]
        [SerializeField] private Transform _cameraTransform;
        [SerializeField] private float _sensitivity = 0.15f;
        [SerializeField] private float _minPitch = -85f;
        [SerializeField] private float _maxPitch = 85f;

        private PlayerInputHandler _input;
        private float _cameraPitch;

        private void Awake()
        {
            _input = GetComponent<PlayerInputHandler>();
        }

        public override void OnStartLocalPlayer()
        {
            if (_cameraTransform == null)
            {
                Camera cam = GetComponentInChildren<Camera>();
                if (cam != null) _cameraTransform = cam.transform;
            }
            
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void Update()
        {
            if (!isLocalPlayer) return;

            HandleRotation();
        }

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
