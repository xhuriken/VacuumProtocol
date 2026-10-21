using System;
using Mirror;
using UnityEngine;

/// <summary>
/// Description: Abstract base class for all Phantom entities in the game.
/// Context: Attached to Phantom enemy prefabs. Inherits from NetworkBehaviour and implements IEntity.
/// Justification: Centralizes physical presence, network-synced health, visual scaling on drain, player proximity health draining, and mouth suction absorption.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public abstract class PhantomBase : NetworkBehaviour, IEntity
{
    #region IEntity Implementation

    [Header("Entity Settings (IEntity)")]
    [Tooltip("Role: The display name of the phantom.\nUse Case: UI Label rendering and targeting.")]
    [SerializeField]
    private string _phantomName = "Phantom";

    [Tooltip("Role: Priority level for entity vision/targeting systems.\nUse Case: Player eye tracking priority.")]
    [SerializeField]
    private int _priorityLevel = 5;

    [Tooltip("Role: Focus point for player eye-tracking and targeting.\nUse Case: LookAt system.\nJustification: Allows targeting the head or core of the phantom.")]
    [SerializeField]
    private Transform _lookAtPoint;

    /// <summary>
    /// Description: Gets or sets the display name of the phantom entity.
    /// </summary>
    public string Name
    {
        get => _phantomName;
        set => _phantomName = value;
    }

    /// <summary>
    /// Description: Gets or sets the vision priority level.
    /// </summary>
    public int PriorityLevel
    {
        get => _priorityLevel;
        set => _priorityLevel = value;
    }

    /// <summary>
    /// Description: Transform point that entities and cameras focus on when looking at this Phantom.
    /// </summary>
    public Transform LookAtPoint => _lookAtPoint != null ? _lookAtPoint : transform;

    #endregion

    #region Health & Shrinking Settings

    [Header("Health & Shrinking")]
    [Tooltip("Role: Total maximum health of the phantom.\nUse Case: Determines how long the player must vacuum it before capture.")]
    [SerializeField]
    private float _maxHealth = 100f;

    [Tooltip("Role: Minimum visual scale ratio when health is near zero (e.g. 0.15 = 15% of original size).\nUse Case: Visual shrinkage feedback.\nJustification: Gives direct visual cues of remaining life before absorption.")]
    [SerializeField]
    private float _minScaleRatio = 0.15f;

    [Tooltip("Role: Speed at which the visual model scales down towards its target size.\nUse Case: Visual smoothing.")]
    [SerializeField]
    private float _scaleLerpSpeed = 15f;

    [Tooltip("Role: The visual transform to scale down as health drains.\nUse Case: Shrinking mesh.\nJustification: Decoupled from root to keep physics colliders or root origin stable if needed.")]
    [SerializeField]
    private Transform _visualModel;

    [Tooltip("Role: Current real-time health value (synced across network).\nUse Case: Inspector observation / runtime health tracking.")]
    [SyncVar(hook = nameof(OnHealthChangedHook))]
    [SerializeField]
    private float _currentHealth;

    [SyncVar(hook = nameof(OnAbsorbedHook))]
    private bool _isAbsorbed = false;

    #endregion

    #region Proximity Attack (Player Drain) Settings

    [Header("Player Health Drain (Proximity Attack)")]
    [Tooltip("Role: Radius within which the phantom drains nearby players' health.\nUse Case: Close-range threat.")]
    [SerializeField]
    private float _proximityDrainRadius = 3.0f;

    [Tooltip("Role: Health drained per second at the farthest edge of the proximity radius.\nUse Case: Minimum DPS at radius boundary.")]
    [SerializeField]
    private float _minProximityDrainRate = 10f;

    [Tooltip("Role: Health drained per second at the exact center (contact with core).\nUse Case: Maximum DPS at point blank.")]
    [SerializeField]
    private float _maxProximityDrainRate = 45f;

    [Tooltip("Role: Layer mask used to detect players for proximity draining.")]
    [SerializeField]
    private LayerMask _playerLayer;

    #endregion

    #region Physics & Suction Awareness

    [Header("Physics & Suction Reaction")]
    [Tooltip("Role: If true, phantom colliders act as triggers allowing players to pass through them.\nUse Case: Ethereal ghost body.")]
    [SerializeField]
    private bool _isPassThrough = true;

    [Tooltip("Role: Movement/velocity slowdown factor applied while actively being vacuumed.\nUse Case: Sucking resistance / struggle.")]
    [SerializeField]
    [Range(0.05f, 1f)]
    private float _vacuumSlowdownFactor = 0.35f;

    [Tooltip("Role: Anti-suction pull resistance used in physics calculations.")]
    [SerializeField]
    private float _pullResistance = 1.5f;

    private Rigidbody _rigidbody;
    private Vector3 _initialVisualScale;
    private bool _isBeingVacuumed = false;
    private float _vacuumTimeoutTimer = 0f;
    private const float VacuumTimeoutDuration = 0.15f;

    #endregion

    #region Events & Properties

    /// <summary>
    /// Description: Triggered when health changes. Parameters: (currentHealth, maxHealth).
    /// </summary>
    public event Action<float, float> OnHealthChanged;

    /// <summary>
    /// Description: Triggered when the phantom is successfully absorbed by a player.
    /// </summary>
    public event Action<GameObject> OnAbsorbed;

    /// <summary>
    /// Description: Gets the maximum health of this phantom.
    /// </summary>
    public float MaxHealth => _maxHealth;

    /// <summary>
    /// Description: Gets the current health of this phantom.
    /// </summary>
    public float CurrentHealth => _currentHealth;

    /// <summary>
    /// Description: Gets whether this phantom has been defeated and absorbed.
    /// </summary>
    public bool IsAbsorbed => _isAbsorbed;

    /// <summary>
    /// Description: Gets whether the phantom is currently in a suction cone.
    /// </summary>
    public bool IsBeingVacuumed => _isBeingVacuumed;

    /// <summary>
    /// Description: Gets the slowdown multiplier applied during active vacuuming.
    /// </summary>
    public float VacuumSlowdownFactor => _vacuumSlowdownFactor;

    /// <summary>
    /// Description: Gets the pull resistance for physics forces.
    /// </summary>
    public float PullResistance => _pullResistance;

    /// <summary>
    /// Description: Gets the attached Rigidbody.
    /// </summary>
    public Rigidbody Rb => _rigidbody;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Description: Awake callback. Caches components and validates configurations.
    /// </summary>
    protected virtual void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            Debug.LogError($"[PhantomBase] Rigidbody missing on {gameObject.name}! Phantoms require a Rigidbody for physics simulation.");
        }
        else
        {
            // Keep phantom upright at all times (prevent tipping over on X and Z)
            _rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        if (_isPassThrough)
        {
            // Configure colliders to triggers so players can pass through the ethereal phantom
            Collider[] colliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].isTrigger = true;
            }
        }

        if (_visualModel == null)
        {
            _visualModel = transform;
        }

        _initialVisualScale = _visualModel.localScale;

        if (_lookAtPoint == null)
        {
            _lookAtPoint = transform;
        }

        if (_maxHealth <= 0f)
        {
            Debug.LogError($"[PhantomBase] Invalid MaxHealth ({_maxHealth}) on {gameObject.name}. Resetting to 100.");
            _maxHealth = 100f;
        }

        _currentHealth = _maxHealth;
    }

    /// <summary>
    /// Description: Start callback on server. Initializes health.
    /// </summary>
    public override void OnStartServer()
    {
        base.OnStartServer();
        _currentHealth = _maxHealth;
        _isAbsorbed = false;
    }

    /// <summary>
    /// Description: Update callback. Updates visual scaling and vacuum timeout.
    /// </summary>
    protected virtual void Update()
    {
        UpdateVisualScale();

        // Check if vacuum state has timed out (no tick received recently)
        if (_isBeingVacuumed)
        {
            _vacuumTimeoutTimer -= Time.deltaTime;
            if (_vacuumTimeoutTimer <= 0f)
            {
                SetVacuumedState(false);
            }
        }

        if (!_isAbsorbed)
        {
            UpdateAI();
        }
    }

    /// <summary>
    /// Description: FixedUpdate callback. Handles proximity player drain and vacuum damping on the authoritative server.
    /// </summary>
    protected virtual void FixedUpdate()
    {
        if (_isBeingVacuumed && _rigidbody != null && !_isAbsorbed)
        {
            // Apply vacuum resistance slowdown damping to self movement
            _rigidbody.linearVelocity = Vector3.Lerp(_rigidbody.linearVelocity, _rigidbody.linearVelocity * _vacuumSlowdownFactor, Time.fixedDeltaTime * 5f);
        }

        if (!isServer || _isAbsorbed)
        {
            return;
        }

        HandleProximityPlayerDrain();
    }

    #endregion

    #region Visual Scaling

    /// <summary>
    /// Description: Smoothly interpolates the visual model's local scale based on remaining health percentage, adding struggle jitter while vacuumed.
    /// </summary>
    private void UpdateVisualScale()
    {
        if (_visualModel == null || _isAbsorbed)
        {
            return;
        }

        float healthNormalized = _maxHealth > 0f ? Mathf.Clamp01(_currentHealth / _maxHealth) : 0f;
        float scaleFactor = Mathf.Lerp(_minScaleRatio, 1f, healthNormalized);
        Vector3 targetScale = _initialVisualScale * scaleFactor;

        // Physical struggle vibration when actively vacuumed
        if (_isBeingVacuumed)
        {
            float jitterIntensity = (1f - healthNormalized) * 0.08f + 0.04f;
            Vector3 jitter = new Vector3(
                (Mathf.PerlinNoise(Time.time * 30f, 0f) - 0.5f) * jitterIntensity,
                (Mathf.PerlinNoise(0f, Time.time * 30f) - 0.5f) * jitterIntensity,
                (Mathf.PerlinNoise(Time.time * 30f, Time.time * 30f) - 0.5f) * jitterIntensity
            );
            targetScale += jitter;
        }

        _visualModel.localScale = Vector3.Lerp(_visualModel.localScale, targetScale, Time.deltaTime * _scaleLerpSpeed);
    }

    #endregion

    #region Proximity Attack Logic

    /// <summary>
    /// Description: Authoritative server check for nearby players within drain radius.
    /// Context: Drains player health continuously with distance gradient (stronger closer to center).
    /// </summary>
    [Server]
    private void HandleProximityPlayerDrain()
    {
        if (_proximityDrainRadius <= 0f || _maxProximityDrainRate <= 0f)
        {
            return;
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, _proximityDrainRadius, _playerLayer, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            PlayerHealth playerHealth = hits[i].GetComponentInParent<PlayerHealth>();
            if (playerHealth != null && !playerHealth.IsDead)
            {
                float distance = Vector3.Distance(transform.position, hits[i].transform.position);
                float t = 1f - Mathf.Clamp01(distance / _proximityDrainRadius);
                float drainRate = Mathf.Lerp(_minProximityDrainRate, _maxProximityDrainRate, t);

                playerHealth.DrainHealth(drainRate, Time.fixedDeltaTime);
            }
        }
    }

    #endregion

    #region Vacuum Reaction & Drainage

    /// <summary>
    /// Description: Notifies the phantom that it is currently receiving vacuum suction from a player's mouth.
    /// </summary>
    /// <param name="vacuumed">Whether it is being vacuumed.</param>
    public virtual void SetVacuumedState(bool vacuumed)
    {
        if (_isBeingVacuumed != vacuumed)
        {
            _isBeingVacuumed = vacuumed;
            if (_isBeingVacuumed)
            {
                OnVacuumStart();
            }
            else
            {
                OnVacuumEnd();
            }
        }

        if (vacuumed)
        {
            _vacuumTimeoutTimer = VacuumTimeoutDuration;
        }
    }

    /// <summary>
    /// Description: Server-authoritative method to drain health from this phantom via mouth suction.
    /// </summary>
    /// <param name="drainAmount">Amount of health to drain in this tick.</param>
    /// <param name="suctionPlayer">The player GameObject performing the suction.</param>
    [Server]
    public virtual void DrainHealthServer(float drainAmount, GameObject suctionPlayer)
    {
        if (_isAbsorbed || drainAmount <= 0f)
        {
            return;
        }

        _currentHealth = Mathf.Max(0f, _currentHealth - drainAmount);

        if (_currentHealth <= 0f)
        {
            AbsorbPhantomServer(suctionPlayer);
        }
    }

    /// <summary>
    /// Description: Server-authoritative trigger when the phantom's health reaches zero and it gets absorbed.
    /// </summary>
    /// <param name="suctionPlayer">The player who captured the phantom.</param>
    [Server]
    protected virtual void AbsorbPhantomServer(GameObject suctionPlayer)
    {
        if (_isAbsorbed)
        {
            return;
        }

        _isAbsorbed = true;
        _currentHealth = 0f;

        Debug.Log($"[PhantomBase] Phantom {gameObject.name} fully drained and absorbed by {(suctionPlayer != null ? suctionPlayer.name : "Player")}!");

        // Disable physics simulation and colliders to prevent further interactions
        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.isKinematic = true;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        RpcOnAbsorbed(suctionPlayer);
        OnAbsorbedServer(suctionPlayer);
    }

    /// <summary>
    /// Description: Server extension hook for custom absorption logic (e.g. giving score/items, despawning, pooling).
    /// </summary>
    [Server]
    protected virtual void OnAbsorbedServer(GameObject suctionPlayer)
    {
        // Default behavior: destroy network object after short delay for animation
        Destroy(gameObject, 0.5f);
    }

    #endregion

    #region Virtual Hooks for Archetypes

    /// <summary>
    /// Description: Virtual AI loop executed on update when alive and unabsorbed.
    /// </summary>
    protected virtual void UpdateAI()
    {
    }

    /// <summary>
    /// Description: Hook called when suction starts affecting this phantom.
    /// </summary>
    protected virtual void OnVacuumStart()
    {
    }

    /// <summary>
    /// Description: Hook called when suction stops affecting this phantom.
    /// </summary>
    protected virtual void OnVacuumEnd()
    {
    }

    /// <summary>
    /// Description: Hook called when absorption RPC is received on clients.
    /// </summary>
    protected virtual void OnAbsorbedClient(GameObject suctionPlayer)
    {
    }

    #endregion

    #region Network Synchronization Hooks

    /// <summary>
    /// Description: SyncVar hook for health changes.
    /// </summary>
    private void OnHealthChangedHook(float oldHealth, float newHealth)
    {
        OnHealthChanged?.Invoke(newHealth, _maxHealth);
    }

    /// <summary>
    /// Description: SyncVar hook for absorbed state changes.
    /// </summary>
    private void OnAbsorbedHook(bool oldAbsorbed, bool newAbsorbed)
    {
        if (newAbsorbed && !oldAbsorbed)
        {
            OnAbsorbed?.Invoke(null);
        }
    }

    /// <summary>
    /// Description: ClientRpc invoked on all clients when absorbed by a player.
    /// </summary>
    [ClientRpc]
    private void RpcOnAbsorbed(GameObject suctionPlayer)
    {
        _isAbsorbed = true;
        StartCoroutine(AbsorptionRushCoroutine(suctionPlayer));
        OnAbsorbed?.Invoke(suctionPlayer);
        OnAbsorbedClient(suctionPlayer);
    }

    /// <summary>
    /// Description: Animates the phantom rushing at high speed directly into the player's mouth nozzle while shrinking down to zero.
    /// </summary>
    private System.Collections.IEnumerator AbsorptionRushCoroutine(GameObject suctionPlayer)
    {
        float duration = 0.35f;
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        Vector3 startScale = _visualModel != null ? _visualModel.localScale : Vector3.one;

        Transform targetNozzle = null;
        if (suctionPlayer != null)
        {
            MouthSuctionZone mouthZone = suctionPlayer.GetComponentInChildren<MouthSuctionZone>(true);
            if (mouthZone != null && mouthZone.NozzleTransform != null)
            {
                targetNozzle = mouthZone.NozzleTransform;
            }
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Cubic ease-in for high-speed rushing effect
            float easeT = t * t * t;

            Vector3 dest = targetNozzle != null ? targetNozzle.position : (suctionPlayer != null ? suctionPlayer.transform.position + Vector3.up * 1.2f : startPos);
            transform.position = Vector3.Lerp(startPos, dest, easeT);

            if (_visualModel != null)
            {
                _visualModel.localScale = Vector3.Lerp(startScale, Vector3.zero, easeT);
            }

            yield return null;
        }

        if (_visualModel != null)
        {
            _visualModel.localScale = Vector3.zero;
        }
    }

    #endregion

    #region Debug & Gizmos

    /// <summary>
    /// Description: Visualizes the phantom's danger aura gradient, LookAtPoint, and suction state in the Editor.
    /// </summary>
    protected virtual void OnDrawGizmosSelected()
    {
        Vector3 pos = transform.position;

        // 1. Outer Drain Aura (Minimum damage edge) - Orange/Red
        Gizmos.color = _isBeingVacuumed ? Color.yellow : new Color(1f, 0.4f, 0.1f, 0.75f);
        Gizmos.DrawWireSphere(pos, _proximityDrainRadius);

        // 2. Inner Drain Core (Maximum damage point blank) - Deep Red
        float coreRadius = Mathf.Max(0.5f, _proximityDrainRadius * 0.35f);
        Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.9f);
        Gizmos.DrawWireSphere(pos, coreRadius);

        // 3. LookAtPoint Focal Marker - Cyan
        if (_lookAtPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_lookAtPoint.position, 0.15f);
            Gizmos.DrawRay(_lookAtPoint.position, _lookAtPoint.forward * 0.5f);
        }

        // 4. In-Play Vacuumed State feedback
        if (Application.isPlaying && _isBeingVacuumed)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(pos, Vector3.one * 0.6f);
        }
    }

    #endregion
}
