using Mirror;
using UnityEngine;

/// <summary>
/// Description: Handles the vacuum aspiration logic, trigger zone activation, and spits/launches collected items using the player's arm components.
/// Context: Attached to the player prefab.
/// Justification: Coordinates interaction between the physical arms, the inventory, the audio, and the network to unify the vacuuming action.
/// </summary>
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerArmsController))]
[RequireComponent(typeof(PlayerInventory))]
public class PlayerVacuumController : NetworkBehaviour
{
    [Header("References")]
    [Tooltip("Role: The audio controller for vacuum sound feedback.\nUse Case: Sound playback.\nJustification: Required to sync the physical vacuum state with the audio looping system.")]
    [SerializeField]
    private VacuumAudioController _audioController;

    [SyncVar(hook = nameof(OnVacuumStateChanged))]
    private bool _isVacuuming = false;

    // Cached references
    private PlayerInputHandler _input;
    private PlayerArmsController _armsController;
    private PlayerInventory _inventory;
    private VacuumSuctionZone _suctionZone;
    private bool _wasLeftArmPressed = false;
    private float _leftArmPressTime = 0f;
    private bool _hasSpittedForCurrentClick = false;

    /// <summary>
    /// Description: Gets a value indicating whether the vacuum is currently active.
    /// Context: Synced over network.
    /// Justification: Exposed for external systems like animation or UI to know the current active state.
    /// </summary>
    public bool IsVacuuming => _isVacuuming;

    /// <summary>
    /// Description: Awake callback. Caches input, arms, and inventory components.
    /// Context: Lifecycle event.
    /// Justification: Guaranteed to fetch required local components before they are used in Start or Update.
    /// </summary>
    private void Awake()
    {
        _input = GetComponent<PlayerInputHandler>();
        _armsController = GetComponent<PlayerArmsController>();
        _inventory = GetComponent<PlayerInventory>();
    }

    /// <summary>
    /// Description: Start callback.
    /// Context: Lifecycle event.
    /// Justification: Attempts an initial cache of the suction zone, although it may need to be re-attempted if arms initialize late.
    /// </summary>
    private void Start()
    {
        // Try caching suction zone immediately if hand transforms are ready
        TryCacheSuctionZone();
    }

    /// <summary>
    /// Description: Update callback. Handles local inputs for vacuuming and spitting, and manages trigger activation on all clients.
    /// Context: Update lifecycle event.
    /// Justification: Combines both server-side trigger updates and local-side input polling.
    /// </summary>
    private void Update()
    {
        // Keep checking for the suction zone in case of delayed initialization
        if (_suctionZone == null)
        {
            TryCacheSuctionZone();
        }

        // Enable or disable the physical trigger zone on all clients based on the synced right arm extension
        if (_suctionZone != null && _armsController != null)
        {
            _suctionZone.IsActive = _armsController.IsRightArmExtended;
        }

        // Input and networking actions are only handled by the local player
        if (!isLocalPlayer)
        {
            return;
        }

        // Sync vacuuming audio state based on the input vacuum state (both arms pressed)
        bool currentVacuumInput = _input != null && _input.IsVacuuming;
        if (currentVacuumInput != _isVacuuming)
        {
            CmdSetVacuumState(currentVacuumInput);
        }

        // Detect and handle spitting items (Left Arm click held)
        // Spitting is only allowed when not performing the dual-click mouth vacuum
        if (_input.LeftArmPressed && !_input.IsVacuuming)
        {
            if (!_wasLeftArmPressed)
            {
                _leftArmPressTime = Time.time;
                _hasSpittedForCurrentClick = false;
            }

            if (!_hasSpittedForCurrentClick)
            {
                bool isPhysicallyExtended = _armsController != null && _armsController.IsLeftHandExtendedPhysically;
                bool isTimeout = (Time.time - _leftArmPressTime) >= 0.25f;

                if (isPhysicallyExtended || isTimeout)
                {
                    TrySpitItem();
                    _hasSpittedForCurrentClick = true;
                }
            }
        }
        else
        {
            _hasSpittedForCurrentClick = false;
        }
        _wasLeftArmPressed = _input.LeftArmPressed && !_input.IsVacuuming;
    }

    /// <summary>
    /// Description: Commands the server to absorb a targeted vacuumed object into the inventory.
    /// Context: Called by the client when an object enters the suction kill-zone.
    /// Justification: Only the server can manipulate the inventory stack securely.
    /// </summary>
    /// <param name="target">The vacuumable GameObject to store.</param>
    [Command]
    public void CmdAbsorbObject(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (_inventory != null && !_inventory.IsFull)
        {
            _inventory.AddItem(target);
        }
    }

    /// <summary>
    /// Description: Instantly deactivates the object locally for responsive feedback, and requests the server to absorb the object.
    /// Context: Public API called by the VacuumSuctionZone.
    /// Justification: Providing immediate local feedback prevents the object from jittering while waiting for the server's authoritative destruction.
    /// </summary>
    /// <param name="target">The vacuumable GameObject to absorb.</param>
    public void AbsorbObject(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        // Hide immediately on the local client to avoid network latency delay
        target.SetActive(false);

        // Notify the server to officially store it
        CmdAbsorbObject(target);
    }

    /// <summary>
    /// Description: Ensure the audio state is correct when a player joins or the object is spawned.
    /// Context: Mirror NetworkBehaviour lifecycle event.
    /// Justification: Late-joiners need to hear if a player is already vacuuming upon connection.
    /// </summary>
    public override void OnStartClient()
    {
        if (_audioController != null)
        {
            _audioController.SetVacuumState(_isVacuuming);
        }
    }

    /// <summary>
    /// Description: Attempts to spit out a stored item from the Left Hand tip/nozzle.
    /// Context: Called locally when the left arm click conditions are met.
    /// Justification: Computes the proper spawn point to prevent physics clipping, then asks the server to instantiate the spat item.
    /// </summary>
    private void TrySpitItem()
    {
        if (_inventory == null || _inventory.ItemCount == 0)
        {
            return;
        }

        // Resolve looking direction
        Transform lookSource = transform;
        Camera cam = GetComponentInChildren<Camera>(true);
        if (cam != null)
        {
            lookSource = cam.transform;
        }

        Vector3 spitDir = lookSource.forward;
        Vector3 spawnPos = transform.position + spitDir * 1.5f;

        // If Left Hand is cached, launch directly from its nozzle
        if (_armsController != null && _armsController.LeftHand != null)
        {
            // Position it slightly ahead of the nozzle to prevent self-collision
            spawnPos = _armsController.LeftHand.position + _armsController.LeftHand.forward * 0.5f;
        }

        CmdSpitItem(spawnPos, spitDir);
    }

    /// <summary>
    /// Description: Attempts to cache the VacuumSuctionZone component by checking the Right Hand hierarchy.
    /// Context: Called during initialization and update loops.
    /// Justification: Since the arm hierarchy is procedural, we must search dynamically for the trigger zone once the arms are fully built.
    /// </summary>
    private void TryCacheSuctionZone()
    {
        if (_armsController != null && _armsController.RightHand != null)
        {
            _suctionZone = _armsController.RightHand.GetComponentInChildren<VacuumSuctionZone>(true);
        }
    }

    #region Network Commands

    /// <summary>
    /// Description: Commands the server to spit out the last item from the inventory.
    /// Context: Mirror Command.
    /// Justification: Routes the spit request from the local player to the authoritative server inventory.
    /// </summary>
    [Command]
    private void CmdSpitItem(Vector3 spawnPosition, Vector3 spitDirection)
    {
        if (_inventory != null)
        {
            _inventory.SpitItem(spawnPosition, spitDirection);
        }
    }

    /// <summary>
    /// Description: Syncs the vacuum state across the network.
    /// Context: Mirror Command.
    /// Justification: Keeps all clients aware of whether the player is holding the vacuum button, driving audio and VFX.
    /// </summary>
    [Command]
    private void CmdSetVacuumState(bool state)
    {
        _isVacuuming = state;
    }

    #endregion

    #region Hook Handlers

    /// <summary>
    /// Description: Hook callback triggered when vacuum state changes over the network.
    /// Context: Mirror SyncVar Hook.
    /// Justification: Pushes the new state down to the audio controller immediately upon replication.
    /// </summary>
    private void OnVacuumStateChanged(bool oldState, bool newState)
    {
        if (_audioController != null)
        {
            _audioController.SetVacuumState(newState);
        }
    }

    #endregion
}
