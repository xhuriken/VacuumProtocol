using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;



/// <summary>
    /// Main controller that handles player lifecycle, networking setup, and 
    /// coordinates modular components.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerController : NetworkBehaviour, IEntity
    {
        [SyncVar] public int ConnectionId = -1;

        [Header("Debug")]
        [SerializeField] private bool _showDebugLogs = true;

        private Rigidbody _rb;
        public Rigidbody Rb => _rb;

        public string Name { get; set; } = "Unit";
        public int PriorityLevel { get; set; } = 1;
        public Transform LookAtPoint { get; private set; }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            
            // CRITICAL: Disable PlayerInput immediately on Awake.
            // This prevents remote player clones from "stealing" input devices.
            PlayerInput input = GetComponent<PlayerInput>();
            if (input != null) input.enabled = false;
        }

        private void Start()
        {
            if (!isLocalPlayer)
            {
                CleanupRemotePlayer();
            }
        }

        private void CleanupRemotePlayer()
        {
            // Disable components that should only exist for the local player
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
            {
                cam.gameObject.SetActive(false);
                AudioListener listener = cam.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = false;
            }

            PlayerInput input = GetComponent<PlayerInput>();
            if (input != null) input.enabled = false;

            // Set to kinematic to follow NetworkTransform smoothly
            if (_rb != null) _rb.isKinematic = true;
        }

        public override void OnStartLocalPlayer()
        {
            // --- LOCAL PLAYER SETUP ---
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
            {
                cam.gameObject.SetActive(true);
                AudioListener listener = cam.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = true;
                LookAtPoint = cam.transform;
            }

            PlayerInput input = GetComponent<PlayerInput>();
            if (input != null)
            {
                input.enabled = true;
                input.ActivateInput();
            }

            // Ensure physics are active for the local controller
            if (_rb != null)
            {
                _rb.isKinematic = false;
                _rb.useGravity = true;
                _rb.freezeRotation = true;
                _rb.interpolation = RigidbodyInterpolation.Interpolate;
                _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }

            if (_showDebugLogs) Debug.Log($"<color=green>[Player] Local Player Initialized: {netId}</color>");
            Cursor.lockState = CursorLockMode.Locked;
        }
    }
