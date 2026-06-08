using Mirror;
using UnityEngine;

/// <summary>
/// Handles the vacuum aspiration logic, trigger zone activation,
/// and spits/launches collected items using the player's arm components.
/// </summary>
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerArmsController))]
[RequireComponent(typeof(PlayerInventory))]
public class PlayerVacuumController : NetworkBehaviour
{
    [Header("References")]
    [Tooltip("The audio controller for vacuum sound feedback.")]
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
    /// Gets a value indicating whether the vacuum is currently active (synced over network).
    /// </summary>
    public bool IsVacuuming => _isVacuuming;

    /// <summary>
    /// Awake callback. Caches input, arms, and inventory components.
    /// </summary>
    private void Awake()
    {
        _input = GetComponent<PlayerInputHandler>();
        _armsController = GetComponent<PlayerArmsController>();
        _inventory = GetComponent<PlayerInventory>();
    }

    /// <summary>
    /// Start callback.
    /// </summary>
    private void Start()
    {
        // Try caching suction zone immediately if hand transforms are ready
        TryCacheSuctionZone();
    }

    /// <summary>
    /// Update callback. Handles local inputs for vacuuming and spitting, and manages trigger activation on all clients.
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
    /// Commands the server to absorb a targeted vacuumed object into the inventory.
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
    /// Instantly deactivates the object locally for responsive feedback,
    /// and requests the server to absorb the object.
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
    /// Ensure the audio state is correct when a player joins or the object is spawned.
    /// </summary>
    public override void OnStartClient()
    {
        if (_audioController != null)
        {
            _audioController.SetVacuumState(_isVacuuming);
        }
    }

    /// <summary>
    /// Attempts to spit out a stored item from the Left Hand tip/nozzle.
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
    /// Attempts to cache the VacuumSuctionZone component by checking the Right Hand hierarchy.
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
    /// Commands the server to spit out the last item from the inventory.
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
    /// Syncs the vacuum state across the network.
    /// </summary>
    [Command]
    private void CmdSetVacuumState(bool state)
    {
        _isVacuuming = state;
    }

    #endregion

    #region Hook Handlers

    /// <summary>
    /// Hook callback triggered when vacuum state changes over the network.
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
