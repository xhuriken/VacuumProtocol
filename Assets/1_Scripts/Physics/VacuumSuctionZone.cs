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
    private float _suctionForce = 25f;

    [Tooltip("Role: Distance threshold for absorption.\nUse Case: Absorption trigger.\nJustification: When the object is closer than this to the nozzle, it gets sucked into inventory.")]
    [SerializeField]
    private float _absorbDistance = 0.25f;

    [Tooltip("Role: Transform representing the vacuum nozzle tip.\nUse Case: Calculation origin.\nJustification: Used to calculate the center of the suction cone and the target destination.")]
    [SerializeField]
    private Transform _nozzleTransform;

    [Header("Animation & Physical Vortex Settings")]
    [Tooltip("Role: Distance from nozzle where items begin shrinking.\nUse Case: Scale transition start.\nJustification: Defines the boundaries of the visual vortex/suction tunnel.")]
    [SerializeField]
    private float _shrinkStartDistance = 1.2f;

    [Tooltip("Role: Interpolation speed of the scale transitions (Lerp factor).\nUse Case: Visual smoothing.\nJustification: Prevents scaling jitter (yo-yo effect) when items collide or wobble.")]
    [SerializeField]
    private float _shrinkLerpSpeed = 12f;

    [Tooltip("Role: Torque applied to spin the item around the suction axis.\nUse Case: Vortex effect.\nJustification: Creates the mechanical spinning look while letting it physically interact with objects.")]
    [SerializeField]
    private float _vortexTorque = 15f;

    [Tooltip("Role: Lateral force bringing the item toward the central suction axis.\nUse Case: Guiding target to nozzle center.\nJustification: Prevents objects from getting stuck on the outer colliders of the nozzle.")]
    [SerializeField]
    private float _centripetalForce = 15f;
    
    [Header("Dirt Stains Settings")]
    [Tooltip("Vitesse de drainage d'une tache (en quantité par seconde).")]
    public float DirtDrainRatePerSecond = 500f;

    [Header("Debug Settings")]
    [Tooltip("Role: Toggles editor wireframe drawing.\nUse Case: Tuning suction parameters.\nJustification: Visualizes the cone shape, shrink boundaries, and absorption radius in the Scene tab.")]
    [SerializeField]
    private bool _drawGizmos = true;

    private PlayerVacuumController _playerVacuum;
    
    // Set of collectibles currently being tracked for smooth scale changes (in-out)
    private readonly HashSet<Collectible> _trackedCollectibles = new HashSet<Collectible>();
    
    // Temporarily stored list of items found in the current physics tick
    private readonly List<Collectible> _currentTickCollectibles = new List<Collectible>();

    /// <summary>
    /// Gets or sets a value indicating whether the suction zone is actively polling and pulling items.
    /// </summary>
    public bool IsActive { get; set; } = false;

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

                // Check occlusion/surface exposure via optimized multi-raycast
                float visibilityFactor = CalculateVisibility(col.transform, toItem);
                if (visibilityFactor <= 0.01f)
                {
                    continue;
                }

                // Handle DirtStain (taches statiques sans physique)
                DirtStain stain = col.GetComponent<DirtStain>();
                if (stain != null)
                {
                    if (_playerVacuum != null && _playerVacuum.isLocalPlayer)
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
                }

                // --- 1. Apply Suction Forces ---
                float resistance = Mathf.Max(0.05f, collectible.PullResistance);
                float forceAmount = (_suctionForce / resistance) * visibilityFactor;

                // Scale force slightly based on proximity (pulls stronger when close)
                float distanceScale = Mathf.Clamp(2.0f - (distance / _suctionRange), 0.5f, 2.0f);
                Vector3 attractionForce = -direction * (forceAmount * distanceScale);

                collectible.Rb.AddForce(attractionForce, ForceMode.Force);

                // --- 2. Apply Centripetal Alignment Forces ---
                // Projects the collectible's position onto the nozzle suction axis line
                float dotForward = Vector3.Dot(toItem, suctionDir);
                Vector3 projectionOnAxis = _nozzleTransform.position + suctionDir * dotForward;
                
                // Direction pushing the object directly to the center line of the nozzle
                Vector3 toAxis = projectionOnAxis - collectible.transform.position;
                Vector3 centripetalForce = toAxis * _centripetalForce * visibilityFactor;
                
                collectible.Rb.AddForce(centripetalForce, ForceMode.Force);

                // --- 3. Apply Vortex Torque ---
                // Spins the object physically around the nozzle axis
                Vector3 vortexTorque = suctionDir * (_vortexTorque * visibilityFactor);
                collectible.Rb.AddTorque(vortexTorque, ForceMode.Force);

                // --- 4. Process Smooth Scale Shrinking ---
                if (distance < _shrinkStartDistance)
                {
                    // Compute target scale ratio based on proximity
                    float targetT = Mathf.Clamp01((distance - _absorbDistance) / (_shrinkStartDistance - _absorbDistance));
                    Vector3 targetScale = collectible.OriginalScale * targetT;

                    // Smooth transition to target scale
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

                // --- 5. Perform authoritative absorption ---
                if (distance <= _absorbDistance && _playerVacuum != null && _playerVacuum.isLocalPlayer)
                {
                    _trackedCollectibles.Remove(collectible);
                    _playerVacuum.AbsorbObject(collectible.gameObject);
                }
            }
        }

        // --- 6. Process Retraction / Reversion ---
        // Restore scale of any collectibles that escaped or when suction deactivated
        List<Collectible> toRemove = new List<Collectible>();
        foreach (Collectible collectible in _trackedCollectibles)
        {
            // If the item left the cone or if suction is off
            if (!IsActive || !_currentTickCollectibles.Contains(collectible))
            {
                if (collectible != null)
                {
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
    }

    /// <summary>
    /// Description: Computes the visibility factor of a collectible from the nozzle using multiple raycasts.
    /// Context: Called by FixedUpdate.
    /// Justification: Simulates how much of the object's surface is exposed to the suction nozzle, reducing force if partially blocked.
    /// </summary>
    private float CalculateVisibility(Transform targetTransform, Vector3 toItem)
    {
        int raysPassed = 0;
        int totalRays = 3;

        Vector3 toItemDirection = toItem.normalized;
        Vector3 orthoRight = Vector3.Cross(toItemDirection, Vector3.up).normalized;
        if (orthoRight.sqrMagnitude < 0.001f)
        {
            orthoRight = Vector3.Cross(toItemDirection, Vector3.forward).normalized;
        }

        // 15cm offset to check lateral occlusion
        Vector3 offset = orthoRight * 0.15f;

        Vector3[] targets = new Vector3[]
        {
            targetTransform.position,
            targetTransform.position + offset,
            targetTransform.position - offset
        };

        foreach (Vector3 target in targets)
        {
            Vector3 rayDir = target - _nozzleTransform.position;
            float rayDist = rayDir.magnitude;

            if (Physics.Raycast(_nozzleTransform.position, rayDir.normalized, out RaycastHit hit, rayDist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                if (hit.transform == targetTransform || hit.transform.IsChildOf(targetTransform))
                {
                    raysPassed++;
                }
            }
            else
            {
                // No obstruction hit on this line
                raysPassed++;
            }
        }

        return (float)raysPassed / totalRays;
    }

    /// <summary>
    /// Description: Resolves the world space direction vector of the selected local suction axis.
    /// </summary>
    private Vector3 GetSuctionDirection()
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

    /// <summary>
    /// Description: Resolves two orthogonal vectors in world space relative to the suction direction to draw the base circle.
    /// </summary>
    private void GetOrthogonalAxes(Vector3 suctionDir, out Vector3 right, out Vector3 up)
    {
        right = Vector3.Cross(suctionDir, _nozzleTransform.up).normalized;
        if (right.sqrMagnitude < 0.001f)
        {
            right = Vector3.Cross(suctionDir, _nozzleTransform.forward).normalized;
        }
        up = Vector3.Cross(right, suctionDir).normalized;
    }

    /// <summary>
    /// Description: OnDrawGizmos callback. Visualizes the precise suction cone, shrink boundary, and absorb sphere in the editor.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!_drawGizmos || _nozzleTransform == null)
        {
            return;
        }

        Vector3 pos = _nozzleTransform.position;
        Vector3 forward = GetSuctionDirection();

        // Draw absorption sphere (red)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(pos, _absorbDistance);

        // Draw shrink boundary sphere (orange)
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(pos, _shrinkStartDistance);

        // Draw suction cone boundaries (yellow)
        Gizmos.color = Color.yellow;

        float radAngle = _coneAngle * Mathf.Deg2Rad;
        float baseRadius = _suctionRange * Mathf.Tan(radAngle);
        Vector3 baseCenter = pos + forward * _suctionRange;

        // Draw base circle using dynamically resolved orthogonal axes
        int segments = 24;
        GetOrthogonalAxes(forward, out Vector3 right, out Vector3 up);
        Vector3 prevPoint = baseCenter + right * baseRadius;

        for (int i = 1; i <= segments; i++)
        {
            float angle = (i * 360f / segments) * Mathf.Deg2Rad;
            Vector3 nextPoint = baseCenter + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * baseRadius;
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }

        // Draw cone side lines
        Gizmos.DrawLine(pos, baseCenter + right * baseRadius);
        Gizmos.DrawLine(pos, baseCenter - right * baseRadius);
        Gizmos.DrawLine(pos, baseCenter + up * baseRadius);
        Gizmos.DrawLine(pos, baseCenter - up * baseRadius);
    }
}
