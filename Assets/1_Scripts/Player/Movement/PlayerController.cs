using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;



/// <summary>
/// Description: Main controller that handles player lifecycle, networking setup, and coordinates modular components.
/// Context: Attached to the root of the player prefab.
/// Justification: Acts as the central brain. Instead of a 2000-line monolithic player script, this delegates logic to specialized components (Movement, Look, Input) and just manages their initialization.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerController : NetworkBehaviour, IEntity
{
    [Tooltip("Role: The network connection ID of this client.\nUse Case: Mirror syncing.\nJustification: Allows scripts like the voice chat system to map this specific avatar to a UniVoice network stream.")]
    [SyncVar] public int ConnectionId = -1;

    [Header("Debug")]
    [Tooltip("Role: Enable initialization logs.\nUse Case: Debugging spawn sequence.\nJustification: Helps trace issues where a local player doesn't gain authority properly.")]
    [SerializeField] private bool _showDebugLogs = true;

    private Rigidbody _rb;

    /// <summary>
    /// Description: Gets the player's root Rigidbody.
    /// Context: Used by movement components.
    /// Justification: Centralized caching prevents multiple components from calling GetComponent independently.
    /// </summary>
    public Rigidbody Rb => _rb;

    /// <summary>
    /// Description: Gets or sets the name of the entity.
    /// Context: IEntity implementation.
    /// Justification: Allows the player to be targeted by vision systems.
    /// </summary>
    public string Name { get; set; } = "Unit";

    /// <summary>
    /// Description: Gets or sets the vision priority.
    /// Context: IEntity implementation.
    /// Justification: Players have priority 1, meaning they are less interesting to look at than Collectibles (priority 2).
    /// </summary>
    public int PriorityLevel { get; set; } = 1;
    [Tooltip("Role: Transform target for eye-tracking.\nUse Case: Look direction.\nJustification: Specifies exactly where other entities should focus when looking at this player (usually the camera/head).")]
    [SerializeField] private Transform _lookAtPoint;

    /// <summary>
    /// Description: Gets the point to look at on this entity.
    /// Context: IEntity implementation.
    /// Justification: Assigned dynamically to the camera for local players, and falls back to a prefab transform for remote players.
    /// </summary>
    public Transform LookAtPoint 
    { 
        get => _lookAtPoint; 
        private set => _lookAtPoint = value; 
    }
    /// <summary>
    /// Description: Awake callback. Caches references and secures input.
    /// Context: Lifecycle event.
    /// Justification: CRITICAL: Disables PlayerInput immediately to prevent remote player clones from "stealing" the local user's input devices upon spawn.
    /// </summary>
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        // CRITICAL: Disable PlayerInput immediately on Awake.
        // This prevents remote player clones from "stealing" input devices.
        PlayerInput input = GetComponent<PlayerInput>();
        if (input != null) input.enabled = false;
    }

    /// <summary>
    /// Description: Start callback. Validates network authority.
    /// Context: Mirror NetworkBehaviour lifecycle event.
    /// Justification: Ensures remote player avatars don't run local logic.
    /// </summary>
    private void Start()
    {
        if (!isLocalPlayer)
        {
            CleanupRemotePlayer();
        }
    }

    /// <summary>
    /// Description: Disables local-only components for remote avatars.
    /// Context: Called if the player prefab does not have local authority.
    /// Justification: Two cameras or audio listeners active simultaneously will cause Unity to glitch. Kinematic rigidbodies ensure smooth NetworkTransform interpolation.
    /// </summary>
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

    /// <summary>
    /// Description: Initializes local systems.
    /// Context: Mirror NetworkBehaviour callback fired when this client assumes authority over the object.
    /// Justification: The safe place to lock the cursor, activate the camera, and enable the input system for the real human player.
    /// </summary>
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
