using Mirror;
using UnityEngine;

/// <summary>
/// Description: Main hub for the V2 (multi-body) player.
/// Context: Attached to the root of the Player_V2 prefab.
/// Justification: Centralizes critical references so sub-scripts avoid redundant or complex GetComponent calls.
/// </summary>
public class PlayerV2_Controller : NetworkBehaviour, IEntity
{

    #region Entity
    [Header("Entity Settings")]
    [Tooltip("Role: The display name of this player entity.\nUse Case: UI Label rendering.\nJustification: Gives other players readable text when looking at this player.")]
    [SerializeField]
    private string _name = "PlayerName";

    [Tooltip("Role: Priority level for entity detection systems.\nUse Case: Vision targeting.\nJustification: Allows important items to take focus over generic clutter.")]
    [SerializeField]
    private int _priorityLevel = 1;

    /// <summary>
    /// Description: Gets or sets the display name of the entity.
    /// Context: Satisfies IEntity interface. Used by UI to render text.
    /// Justification: Wrapped in a property to allow potential future localization hooks.
    /// </summary>
    public string Name
    {
        get
        {
            return _name;
        }
        set
        {
            _name = value;
        }
    }

    /// <summary>
    /// Description: Gets or sets the priority level for detection (higher means more important).
    /// Context: Satisfies IEntity interface. Used by PlayerV2_Look.
    /// Justification: Resolves conflicts when multiple objects are within the player's field of view.
    /// </summary>
    public int PriorityLevel
    {
        get
        {
            return _priorityLevel;
        }
        set
        {
            _priorityLevel = value;
        }
    }

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
    #endregion

    [Tooltip("Role: The network connection ID of this client.\nUse Case: Mirror syncing.\nJustification: Allows scripts like the voice chat system to map this specific avatar to a UniVoice network stream.")]
    [SyncVar] public int ConnectionId = -1;

    [Header("Network States")]
    [Tooltip("Role: Crouch state, synchronized from the server.\nUse Case: Drives head height offset and arm retraction on every client.")]
    [SyncVar(hook = nameof(OnCrouchStateChanged))] 
    public bool IsCrouching;

    /// <summary>Asks the server to set the synchronized crouch state.</summary>
    [Command]
    public void CmdSetCrouching(bool crouchState)
    {
        IsCrouching = crouchState;
    }

    private void OnCrouchStateChanged(bool oldVal, bool newVal)
    {
        if (HeadController != null && ArmsController != null)
        {
            if (newVal)
            {
                float crouchOffset = -0.5f;
                var movement = GetComponent<PlayerV2_Movement>();
                if (movement != null) crouchOffset = movement.CrouchHeadOffset;

                HeadController.SetHeadHeightOffset(crouchOffset);
                ArmsController.SetArmRetraction(true);
            }
            else
            {
                HeadController.SetHeadHeightOffset(0f);
                ArmsController.SetArmRetraction(false);
            }
        }
    }

    [Header("Physics Bodies")]
    [Tooltip("Role: Rigidbody of the base (movement).\nUse Case: Locomotion and wheel attachment.")]
    public Rigidbody HipsRigidbody;

    [Tooltip("Role: Rigidbody of the turret (view).\nUse Case: Free, unlimited camera rotation.")]
    public Rigidbody TorsoRigidbody;

    /// <summary>
    /// Description: Gets the true world position of the player's physical body.
    /// Context: HipsRigidbody moves in physics space while root transform remains at spawn.
    /// Justification: Critical for AI and tracking systems to locate the true player position.
    /// </summary>
    public Vector3 WorldPosition => HipsRigidbody != null ? HipsRigidbody.position : transform.position;

    [Header("Camera & Visuals")]
    [Tooltip("Role: The player camera.\nUse Case: Assigned to the Look script.")]
    public Transform CameraTransform;

    [Tooltip("Role: Head and neck controller.\nUse Case: Receives the pitch computed by the Look script.")]
    public PlayerV2_Head HeadController;

    [Header("Arms System")]
    [Tooltip("Role: Physical root of the left arm.\nUse Case: Hierarchy traversal and max reach distance.")]
    public Transform LeftArmRoot;

    [Tooltip("Role: Physical root of the right arm.\nUse Case: Hierarchy traversal and max reach distance.")]
    public Transform RightArmRoot;

    [Tooltip("Role: Left shoulder (visual/physical).\nUse Case: Rotates to 90 degrees when extended.")]
    public Transform LeftShoulder;

    [Tooltip("Role: Right shoulder (visual/physical).\nUse Case: Rotates to -90 degrees when extended.")]
    public Transform RightShoulder;

    [Tooltip("Role: Physical arms controller.\nUse Case: Centralized reference for other systems.")]
    public PlayerV2_Arms ArmsController;

    /// <summary>
    /// Description: Global list of currently active player controllers in the scene.
    /// Context: Maintained automatically on enable/disable.
    /// Justification: Allows AI entities (such as Phantoms) to find valid target players without costly scene-wide searches.
    /// </summary>
    public static readonly System.Collections.Generic.List<PlayerV2_Controller> ActivePlayers = new System.Collections.Generic.List<PlayerV2_Controller>();

    private void OnEnable()
    {
        if (!ActivePlayers.Contains(this))
        {
            ActivePlayers.Add(this);
        }
    }

    private void OnDisable()
    {
        ActivePlayers.Remove(this);
    }

    /// <summary>
    /// Description: The player controller owned by this client.
    /// Context: Set in OnStartLocalPlayer, cleared on destroy.
    /// Justification: Gives local-only systems (pause menu, HUD) direct access to the local robot without scene searches.
    /// </summary>
    public static PlayerV2_Controller LocalInstance { get; private set; }

    /// <summary>Registers this controller as the local player instance.</summary>
    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        LocalInstance = this;
    }

    private void OnDestroy()
    {
        if (LocalInstance == this)
        {
            LocalInstance = null;
        }
    }

    /// <summary>True while the local player is paused (Escape menu open).</summary>
    public bool IsPaused { get; private set; }

    /// <summary>
    /// Description: Pauses or resumes the local player: blocks input and puts the eyes in their paused state.
    /// </summary>
    public void SetPaused(bool isPaused)
    {
        IsPaused = isPaused;

        PlayerInputHandler input = GetComponent<PlayerInputHandler>();
        if (input == null)
        {
            Debug.LogError($"[PlayerV2_Controller] '{name}' has no PlayerInputHandler: input cannot be blocked while paused.", this);
        }
        else
        {
            input.IsInputBlocked = isPaused;
        }

        Eye[] eyes = GetComponentsInChildren<Eye>();
        for (int i = 0; i < eyes.Length; i++)
        {
            eyes[i].SetPaused(isPaused);
        }
    }

    private void Start()
    {
        if (HipsRigidbody == null || TorsoRigidbody == null)
        {
            Debug.LogError("[PlayerV2_Controller] HipsRigidbody or TorsoRigidbody is missing! Assign them in the Inspector.");
        }

        // Disable the camera and AudioListener for remote players
        if (!isOwned && CameraTransform != null)
        {
            Camera cam = CameraTransform.GetComponent<Camera>();
            if (cam != null) cam.enabled = false;


            AudioListener listener = CameraTransform.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;
        }
    }
}

