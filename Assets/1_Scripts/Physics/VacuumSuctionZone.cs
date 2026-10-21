using System.Collections.Generic;
using UnityEngine;
using VacuumProtocol.Mechanics.Dirt;

/// <summary>
/// Description: Defines the available local axes of a transform.
/// </summary>
public enum LocalAxis
{
    Forward,  // +Z
    Backward, // -Z
    Right,    // +X
    Left,     // -X
    Up,       // +Y
    Down      // -Y
}

/// <summary>
/// Description: Controls the physics-based suction cone to attract collectibles towards the nozzle tip with vortex rotation and centripetal alignment.
/// Context: Attached as a child of the player nozzle (e.g. on the hand).
/// Justification: Detects items within a cone geometry, checks for static/dynamic occlusion via multi-raycast, and pulls them.
/// Supports configurable local suction axes, smoothed scale transitions, and active physical vortex collisions.
/// </summary>
public class VacuumSuctionZone : MonoBehaviour
{
    [Header("Cone Settings")]
    [Tooltip("Role: Maximum range of the suction cone.\nUse Case: Overlap detection sphere radius.\nJustification: Prevents testing objects outside the functional gameplay limits.")]
    [SerializeField]
    private float _suctionRange = 3.0f;

    [Tooltip("Role: Opening angle of the cone (in degrees, from center axis).\nUse Case: Angular filter.\nJustification: Controls how wide the suction area spreads.")]
    [SerializeField]
    private float _coneAngle = 35f;

    [Tooltip("Role: Local axis of the nozzle that represents the suction direction.\nUse Case: Arm setup custom orientation.\nJustification: Decouples the rig default T-pose mesh axis from the physics query axis.")]
    [SerializeField]
    private LocalAxis _suctionAxis = LocalAxis.Left;

    [Tooltip("Role: Layer mask to filter collectible objects.\nUse Case: Collision query optimization.\nJustification: Ignores player bodies and world layout elements during the initial overlap query.")]
    [SerializeField]
    private LayerMask _collectibleLayer;

    [Header("Suction Settings")]
    [Tooltip("Role: Base attraction force magnitude.\nUse Case: Physics pull strength.\nJustification: High values attract items instantly, low values require steady focus.")]
    [SerializeField]
    private float _suctionForce = 35f;

    [Tooltip("Role: Distance threshold for absorption.\nUse Case: Absorption trigger.\nJustification: When the object is closer than this to the nozzle, it gets sucked into inventory.")]
    [SerializeField]
    private float _absorbDistance = 0.12f;

    [Header("Wingardium Leviosa Settings")]
    [Tooltip("Role: Cancels gravity on objects being vacuumed.\nUse Case: Lifts items off the ground immediately.\nJustification: Creates a magical, floating suction effect without ground friction.")]
    [SerializeField]
    private bool _cancelGravity = true;

    [Tooltip("Role: Damping applied to neutralize unwanted momentum (pendulum effect or sliding under the nozzle).\nUse Case: Stabilizes objects during pull.\nJustification: High damping makes the item strictly follow the suction forces without overshooting. Scales inversely with pull resistance.")]
    [SerializeField]
    private float _suctionDamping = 12f;

    [Tooltip("Role: Transform representing the vacuum nozzle tip.\nUse Case: Calculation origin.\nJustification: Used to calculate the center of the suction cone and the target destination.")]
    [SerializeField]
    private Transform _nozzleTransform;

    [Header("Animation & Physical Vortex Settings")]
    [Tooltip("Role: Distance from nozzle where items begin shrinking.\nUse Case: Scale transition start.\nJustification: Defines the boundaries of the visual vortex/suction tunnel.")]
    [SerializeField]
    private float _shrinkStartDistance = 0.8f;

    [Tooltip("Role: Interpolation speed of the scale transitions (Lerp factor).\nUse Case: Visual smoothing.\nJustification: Prevents scaling jitter (yo-yo effect) when items collide or wobble.")]
    [SerializeField]
    private float _shrinkLerpSpeed = 14f;

    [Tooltip("Role: Torque applied to spin the item around the suction axis.\nUse Case: Vortex effect.\nJustification: Creates the mechanical spinning look while letting it physically interact with objects.")]
    [SerializeField]
    private float _vortexTorque = 15f;

    [Tooltip("Role: Lateral force bringing the item toward the central suction axis.\nUse Case: Guiding target to nozzle center.\nJustification: Prevents objects from getting stuck on the outer colliders of the nozzle.")]
    [SerializeField]
    private float _centripetalForce = 18f;


    [Header("Dirt Stains Settings")]
    [Tooltip("Vitesse de drainage d'une tache (en quantité par seconde).")]
    public float DirtDrainRatePerSecond = 500f;

    [Header("Visuals & Effects")]
    [Tooltip("Role: The particle system for the vacuum suction effect.\nUse Case: Plays when active, stops when inactive.")]
    [SerializeField] private ParticleSystem _vacuumParticles;


    [Tooltip("Role: The force field GameObjects for the particles.\nUse Case: Enabled when active, disabled when inactive.")]
    [SerializeField] private GameObject[] _vacuumForceFields;

    private PlayerVacuumController _playerVacuum;

    /// <summary>
    /// Description: Gets the suction range of the arm nozzle.
    /// </summary>
    public float SuctionRange => _suctionRange;

    /// <summary>
    /// Description: Gets the cone opening angle.
    /// </summary>
    public float ConeAngle => _coneAngle;

    /// <summary>
    /// Description: Gets the absorb distance threshold.
    /// </summary>
    public float AbsorbDistance => _absorbDistance;

    /// <summary>
    /// Description: Gets the shrink start distance threshold.
    /// </summary>
    public float ShrinkStartDistance => _shrinkStartDistance;

    /// <summary>
    /// Description: Gets the nozzle tip transform.
    /// </summary>
    public Transform NozzleTransform => _nozzleTransform != null ? _nozzleTransform : transform;

    // Set of collectibles currently being tracked for smooth scale changes (in-out)

    private readonly HashSet<Collectible> _trackedCollectibles = new HashSet<Collectible>();

    // Temporarily stored list of items found in the current physics tick

    private readonly List<Collectible> _currentTickCollectibles = new List<Collectible>();

    private bool _isActive = false;
    private bool _isDrainingDirt = false;


    /// <summary>
    /// Gets or sets a value indicating whether the suction zone is actively polling and pulling items.
    /// Handles enabling/disabling force fields, and safely stopping particles if deactivated.
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                UpdateForceFields();

                // If the vacuum stops, we are no longer draining dirt

                if (!_isActive)
                {
                    SetDrainingDirt(false);
                }
            }
        }
    }

    private void UpdateForceFields()
    {
        if (_vacuumForceFields != null)
        {
            foreach (var ff in _vacuumForceFields)
            {
                if (ff != null)
                {
                    ff.SetActive(_isActive);
                }
            }
        }
    }

    private void SetDrainingDirt(bool draining)
    {
        if (_isDrainingDirt != draining)
        {
            _isDrainingDirt = draining;
            if (_vacuumParticles != null)
            {
                if (_isDrainingDirt) _vacuumParticles.Play();
                else _vacuumParticles.Stop();
            }
        }
    }

    /// <summary>
    /// Description: Awake callback. Caches components and validates nozzle reference.
    /// </summary>
    private void Awake()
    {
        if (_nozzleTransform == null)
        {
            _nozzleTransform = transform;
        }

        _playerVacuum = GetComponentInParent<PlayerVacuumController>();
    }

    /// <summary>
    /// Description: FixedUpdate callback. Performs the physics search, filters by cone, checks occlusion, and applies forces.
    /// </summary>
    private void FixedUpdate()
    {
        _currentTickCollectibles.Clear();
        bool isDrainingDirtThisFrame = false;

        if (IsActive)
        {
            // Search for all colliders inside the suction range sphere
            Collider[] colliders = Physics.OverlapSphere(_nozzleTransform.position, _suctionRange, _collectibleLayer, QueryTriggerInteraction.Collide);
            Vector3 suctionDir = GetSuctionDirection();

            foreach (Collider col in colliders)
            {
                Vector3 toItem = col.transform.position - _nozzleTransform.position;
                float distance = toItem.magnitude;

                if (distance < 0.01f)
                {
                    continue;
                }

                Vector3 direction = toItem.normalized;

                // Check angular alignment (Cone containment)
                float dot = Vector3.Dot(suctionDir, direction);
                float minDot = Mathf.Cos(_coneAngle * Mathf.Deg2Rad);

                if (dot < minDot)
                {
                    continue;
                }

                // Check occlusion/surface exposure via bounds multi-point raycasts (supports items on shelves/commodes)
                float visibilityFactor = CalculateVisibility(col, toItem);
                if (visibilityFactor <= 0.01f)
                {
                    continue;
                }

                // Phantoms are immune to arm suction (only vacuumed via mouth dual-click)
                if (col.GetComponentInParent<PhantomBase>() != null)
                {
                    continue;
                }

                // Handle DirtStain (taches statiques sans physique)
                DirtStain stain = col.GetComponent<DirtStain>();
                if (stain != null)
                {
                    isDrainingDirtThisFrame = true;
                    if (_playerVacuum != null && _playerVacuum.isOwned)
                    {
                        float amountToDrain = DirtDrainRatePerSecond * Time.fixedDeltaTime * visibilityFactor;
                        _playerVacuum.DrainDirt(stain, amountToDrain);
                    }
                    continue; // On passe à l'objet suivant, pas de physique pour les taches
                }

                // Handle Physical Collectibles
                Collectible collectible = col.GetComponent<Collectible>();
                if (collectible == null || collectible.Rb == null)
                {
                    continue;
                }

                // Record this item as active in the current physics tick
                _currentTickCollectibles.Add(collectible);
                if (!_trackedCollectibles.Contains(collectible))
                {
                    _trackedCollectibles.Add(collectible);
                    SetIgnoreCollisionsWithArm(collectible, true);
                }

                // --- 1. Wingardium Leviosa (Anti-Gravity & Anti-Momentum) ---
                float resistance = Mathf.Max(0.05f, collectible.PullResistance);

                if (_cancelGravity)
                {
                    Vector3 antiGravity = -Physics.gravity * collectible.Rb.mass;
                    collectible.Rb.AddForce(antiGravity, ForceMode.Force);
                }

                if (_suctionDamping > 0f)
                {
                    float dragStrength = _suctionDamping / resistance;
                    Vector3 dragForce = -collectible.Rb.linearVelocity * (collectible.Rb.mass * dragStrength);
                    collectible.Rb.AddForce(dragForce, ForceMode.Force);
                }

                // --- 2. Streamlined Funnel Suction Forces (Curving into Nozzle Mouth) ---
                float forceAmount = (_suctionForce / resistance) * visibilityFactor;
                float distanceScale = Mathf.Clamp(2.2f - (distance / _suctionRange), 0.8f, 2.5f);

                Vector3 toNozzle = _nozzleTransform.position - collectible.transform.position;
                Vector3 pullDir = toNozzle.normalized;

                // As distance decreases, blend pull direction with -suctionDir so objects enter straight into the nozzle throat
                float entryT = Mathf.Clamp01(1f - (distance / 1.2f));
                Vector3 streamFlowDir = Vector3.Slerp(pullDir, -suctionDir, entryT * 0.75f);
                Vector3 attractionForce = streamFlowDir * (forceAmount * distanceScale);
                collectible.Rb.AddForce(attractionForce, ForceMode.Force);

                // --- 3. Centripetal Funnel Confinement (Guides objects onto the central nozzle stream) ---
                float dotForward = Vector3.Dot(toItem, suctionDir);
                Vector3 projectionOnAxis = _nozzleTransform.position + suctionDir * Mathf.Max(0f, dotForward);
                Vector3 toAxis = projectionOnAxis - collectible.transform.position;
                float lateralDist = toAxis.magnitude;

                if (lateralDist > 0.01f)
                {
                    Vector3 centripetalDir = toAxis / lateralDist;
                    float funnelMultiplier = Mathf.Lerp(1.0f, 2.5f, entryT);
                    Vector3 centripetalForce = centripetalDir * (lateralDist * _centripetalForce * funnelMultiplier * visibilityFactor);
                    collectible.Rb.AddForce(centripetalForce, ForceMode.Force);
                }

                // --- 4. Anti-Overshoot Transverse Velocity Damping (Prevents flying past the nozzle) ---
                if (distance < 1.0f)
                {
                    Vector3 alongAxisVel = Vector3.Project(collectible.Rb.linearVelocity, suctionDir);
                    Vector3 transverseVel = collectible.Rb.linearVelocity - alongAxisVel;
                    collectible.Rb.AddForce(-transverseVel * (collectible.Rb.mass * 16f * entryT), ForceMode.Force);
                }

                // --- 5. Apply Vortex Torque ---
                Vector3 vortexTorque = suctionDir * (_vortexTorque * visibilityFactor);
                collectible.Rb.AddTorque(vortexTorque, ForceMode.Force);

                // --- 6. Non-linear Scale Shrinking (Preserves size until nozzle entry, then compresses into mouth) ---
                if (distance < _shrinkStartDistance)
                {
                    float targetT = Mathf.Clamp01((distance - _absorbDistance) / (_shrinkStartDistance - _absorbDistance));
                    // Power curve: keeps object large (~75-80%) until entering the nozzle throat
                    float scaleCurve = Mathf.Pow(targetT, 0.45f);
                    Vector3 targetScale = collectible.OriginalScale * scaleCurve;

                    collectible.transform.localScale = Vector3.Lerp(
                        collectible.transform.localScale,
                        targetScale,
                        _shrinkLerpSpeed * Time.fixedDeltaTime
                    );
                }
                else
                {
                    // Outside the shrink zone but tracked: interpolate back to original scale
                    collectible.transform.localScale = Vector3.Lerp(
                        collectible.transform.localScale,
                        collectible.OriginalScale,
                        _shrinkLerpSpeed * Time.fixedDeltaTime
                    );
                }

                // --- 7. Perform authoritative absorption inside nozzle opening ---
                if (distance <= _absorbDistance && _playerVacuum != null && _playerVacuum.isOwned)
                {
                    SetIgnoreCollisionsWithArm(collectible, false);
                    _trackedCollectibles.Remove(collectible);
                    _playerVacuum.AbsorbObject(collectible.gameObject);
                }
            }
        }

        // --- 8. Process Retraction / Reversion ---
        // Restore scale and re-enable collisions for any collectibles that escaped or when suction deactivated
        List<Collectible> toRemove = new List<Collectible>();
        foreach (Collectible collectible in _trackedCollectibles)
        {
            // If the item left the cone or if suction is off
            if (!IsActive || !_currentTickCollectibles.Contains(collectible))
            {
                if (collectible != null)
                {
                    // Re-enable collisions immediately upon release so it falls and collides with arm/environment
                    SetIgnoreCollisionsWithArm(collectible, false);

                    // Interpolate back to original scale smoothly
                    collectible.transform.localScale = Vector3.Lerp(
                        collectible.transform.localScale,
                        collectible.OriginalScale,
                        _shrinkLerpSpeed * Time.fixedDeltaTime
                    );

                    // Check if it's close enough to its original scale to stop tracking
                    if (Vector3.Distance(collectible.transform.localScale, collectible.OriginalScale) < 0.01f)
                    {
                        collectible.ResetScale();
                        toRemove.Add(collectible);
                    }
                }
                else
                {
                    // Cleanup null references (e.g. if destroyed/collected by other systems)
                    toRemove.Add(collectible);
                }
            }
        }

        foreach (Collectible col in toRemove)
        {
            _trackedCollectibles.Remove(col);
        }

        SetDrainingDirt(isDrainingDirtThisFrame);
    }

    /// <summary>
    /// Description: Computes the visibility factor of a collectible from the nozzle using multi-point bounds raycasts.
    /// Context: Called by FixedUpdate.
    /// Justification: Checks center, top, bottom, and lateral points to allow suction over obstacles (e.g. commodes, shelves).
    /// </summary>
    private float CalculateVisibility(Collider col, Vector3 toItem)
    {
        if (col == null) return 0f;

        Transform targetTransform = col.transform;
        Bounds b = col.bounds;
        Vector3 nozzlePos = _nozzleTransform.position;

        Vector3 toItemDirection = toItem.normalized;
        Vector3 orthoRight = Vector3.Cross(toItemDirection, Vector3.up).normalized;
        if (orthoRight.sqrMagnitude < 0.001f)
        {
            orthoRight = Vector3.Cross(toItemDirection, Vector3.forward).normalized;
        }

        Vector3 center = b.center;
        Vector3 extents = b.extents;

        // Sample 5 key spatial points: Center, Top (crucial for furniture edges), Bottom, Left, Right
        Vector3[] targets = new Vector3[]
        {
            center,
            center + Vector3.up * (extents.y * 0.85f),
            center - Vector3.up * (extents.y * 0.5f),
            center + orthoRight * (extents.x * 0.7f),
            center - orthoRight * (extents.x * 0.7f)
        };

        int raysPassed = 0;
        int totalRays = targets.Length;

        for (int i = 0; i < totalRays; i++)
        {
            Vector3 rayDir = targets[i] - nozzlePos;
            float rayDist = rayDir.magnitude;

            if (Physics.Raycast(nozzlePos, rayDir.normalized, out RaycastHit hit, rayDist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform == targetTransform || hit.transform.IsChildOf(targetTransform) || hit.collider == col)
                {
                    raysPassed++;
                }
            }
            else
            {
                // No solid obstacle hit
                raysPassed++;
            }
        }

        if (raysPassed > 0)
        {
            return Mathf.Clamp01((float)raysPassed / totalRays + 0.25f);
        }

        return 0f;
    }

    /// <summary>
    /// Description: Resolves the world space direction vector of the selected local suction axis.
    /// </summary>
    public Vector3 GetSuctionDirection()
    {
        switch (_suctionAxis)
        {
            case LocalAxis.Forward: return _nozzleTransform.forward;
            case LocalAxis.Backward: return -_nozzleTransform.forward;
            case LocalAxis.Right: return _nozzleTransform.right;
            case LocalAxis.Left: return -_nozzleTransform.right;
            case LocalAxis.Up: return _nozzleTransform.up;
            case LocalAxis.Down: return -_nozzleTransform.up;
            default: return _nozzleTransform.forward;
        }
    }

    #region Arm Collision Filtering

    private Collider[] _cachedArmColliders;

    /// <summary>
    /// Description: Caches all colliders of the player's right arm and nozzle hierarchy.
    /// </summary>
    private void CacheArmColliders()
    {
        if (_cachedArmColliders == null || _cachedArmColliders.Length == 0)
        {
            PlayerV2_Controller controller = GetComponentInParent<PlayerV2_Controller>();
            if (controller != null && controller.RightArmRoot != null)
            {
                _cachedArmColliders = controller.RightArmRoot.GetComponentsInChildren<Collider>();
            }
            else if (_playerVacuum != null)
            {
                _cachedArmColliders = _playerVacuum.GetComponentsInChildren<Collider>();
            }
            else
            {
                _cachedArmColliders = GetComponentsInChildren<Collider>();
            }
        }
    }

    /// <summary>
    /// Description: Ignores or restores physics collisions between a collectible and the player's right arm.
    /// </summary>
    private void SetIgnoreCollisionsWithArm(Collectible collectible, bool ignore)
    {
        if (collectible == null) return;
        CacheArmColliders();
        if (_cachedArmColliders == null) return;

        Collider[] itemColliders = collectible.GetComponentsInChildren<Collider>();
        for (int i = 0; i < itemColliders.Length; i++)
        {
            Collider itemCol = itemColliders[i];
            if (itemCol == null) continue;

            for (int j = 0; j < _cachedArmColliders.Length; j++)
            {
                Collider armCol = _cachedArmColliders[j];
                if (armCol != null && armCol.enabled && itemCol.enabled)
                {
                    Physics.IgnoreCollision(itemCol, armCol, ignore);
                }
            }
        }
    }

    /// <summary>
    /// Description: OnDisable callback. Restores collisions and resets scales for all tracked items.
    /// </summary>
    private void OnDisable()
    {
        foreach (Collectible collectible in _trackedCollectibles)
        {
            if (collectible != null)
            {
                SetIgnoreCollisionsWithArm(collectible, false);
                collectible.ResetScale();
            }
        }
        _trackedCollectibles.Clear();
    }

    #endregion
}
