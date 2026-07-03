using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Description: Attached to a trigger volume (e.g. SphereCollider) representing the vacuum suction field.
/// Context: Exists as a child of the Player prefab, usually at the end of the vacuum nozzle.
/// Justification: Handles the complex physics interactions of pulling rigidbodies towards a point, applying visual squish/shrink effects, and bridging the collision to the player's inventory system.
/// </summary>
[RequireComponent(typeof(Collider))]
public class VacuumSuctionZone : MonoBehaviour
{
    [Header("Suction Settings")]
    [Tooltip("Role: Base pull force multiplier.\nUse Case: Physics attraction.\nJustification: Determines how fast objects fly towards the nozzle.")]
    [SerializeField]
    private float _suctionForce = 25f;

    [Tooltip("Role: Distance threshold for shrinking.\nUse Case: Visual feedback.\nJustification: Starts scaling the object down so it fits into the small vacuum hole visually.")]
    [SerializeField]
    private float _shrinkStartDistance = 1.0f;

    [Tooltip("Role: Distance threshold for absorption.\nUse Case: Inventory triggers.\nJustification: When the object is this close, we consider it 'sucked up' and destroy/disable it in the world.")]
    [SerializeField]
    private float _absorbDistance = 0.25f;

    [Tooltip("Role: The transform representing the nozzle tip.\nUse Case: Target position.\nJustification: The mathematical point all objects are pulled towards. Separated from the collider center to allow offset suction zones.")]
    [SerializeField]
    private Transform _nozzleTransform;

    [Header("Debug")]
    [Tooltip("Role: Enable editor wireframes.\nUse Case: Level design.\nJustification: Helps visualize the shrink and absorb radii to ensure they make sense for the nozzle mesh.")]
    [SerializeField]
    private bool _drawGizmos = true;

    // Track original scales of objects currently being vacuumed to restore them safely if they escape
    private readonly Dictionary<Collectible, Vector3> _trackedScales = new Dictionary<Collectible, Vector3>();
    private Collider _zoneCollider;
    private PlayerVacuumController _playerVacuum;

    /// <summary>
    /// Gets or sets a value indicating whether the suction zone is actively pulling items.
    /// </summary>
    public bool IsActive { get; set; } = false;

    /// <summary>
    /// Description: Awake callback. Initializes trigger references.
    /// Context: Lifecycle event.
    /// Justification: Caches references to avoid GetComponent calls during the physics loop.
    /// </summary>
    private void Awake()
    {
        _zoneCollider = GetComponent<Collider>();
        _zoneCollider.isTrigger = true;

        if (_nozzleTransform == null)
        {
            _nozzleTransform = transform;
        }

        // Cache the player controller from the parent hierarchy
        _playerVacuum = GetComponentInParent<PlayerVacuumController>();
    }

    /// <summary>
    /// Description: Update callback. If the zone was deactivated, restores scales of any objects currently inside.
    /// Context: Lifecycle event.
    /// Justification: If the player stops vacuuming while an object is halfway sucked in (and thus half size), we must restore its scale so it drops to the floor normally.
    /// </summary>
    private void Update()
    {
        if (!IsActive && _trackedScales.Count > 0)
        {
            ResetAllTrackedScales();
        }
    }

    /// <summary>
    /// Description: OnTriggerStay callback. Processes attraction forces and scale shrinking.
    /// Context: Unity Physics callback, fires every FixedUpdate while a collider remains in the trigger volume.
    /// Justification: Continuous force application ensures objects smoothly ride the gravity well towards the nozzle. Handles shrinking locally before delegating absorption to the PlayerVacuumController.
    /// </summary>
    private void OnTriggerStay(Collider other)
    {
        if (!IsActive)
        {
            return;
        }

        // Check if the object is vacuumable
        Collectible vacuumable = other.GetComponent<Collectible>();
        if (vacuumable == null || vacuumable.Rb == null)
        {
            return;
        }

        // Compute direction and distance to nozzle
        Vector3 nozzlePos = _nozzleTransform.position;
        Vector3 toNozzle = nozzlePos - other.transform.position;
        float distance = toNozzle.magnitude;

        if (distance < 0.01f)
        {
            return;
        }

        Vector3 direction = toNozzle.normalized;

        // Calculate pull force (resistance-adjusted)
        float resistance = Mathf.Max(0.05f, vacuumable.PullResistance);
        float forceAmount = _suctionForce / resistance;

        // Apply a gentle scaling force based on proximity to feel snappy near the nozzle
        float distanceScale = Mathf.Clamp(2.0f - (distance / 3.0f), 0.5f, 2.0f);
        Vector3 force = direction * (forceAmount * distanceScale);

        // Apply force to target Rigidbody
        vacuumable.Rb.AddForce(force, ForceMode.Force);

        // Process visual shrinking as it approaches the nozzle
        if (distance < _shrinkStartDistance)
        {
            // Store original scale if we haven't already
            if (!_trackedScales.ContainsKey(vacuumable))
            {
                _trackedScales.Add(vacuumable, vacuumable.OriginalScale);
            }

            // Interpolate scale down to zero at the absorption boundary
            float t = Mathf.Clamp01((distance - _absorbDistance) / (_shrinkStartDistance - _absorbDistance));
            other.transform.localScale = vacuumable.OriginalScale * t;

            // Trigger absorption if close enough (only done for local player to avoid dual absorb triggers)
            if (distance <= _absorbDistance && _playerVacuum != null && _playerVacuum.isLocalPlayer)
            {
                // Remove from local tracked dictionary before deactivation to avoid ghost references
                _trackedScales.Remove(vacuumable);
                _playerVacuum.AbsorbObject(vacuumable.gameObject);
            }
        }
    }

    /// <summary>
    /// Description: OnTriggerExit callback. Restores object scale if it manages to break free of the vacuum flow.
    /// Context: Unity Physics callback.
    /// Justification: Ensures objects don't remain permanently tiny if they bounce out of the suction zone.
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        Collectible vacuumable = other.GetComponent<Collectible>();
        if (vacuumable != null && _trackedScales.ContainsKey(vacuumable))
        {
            vacuumable.ResetScale();
            _trackedScales.Remove(vacuumable);
        }
    }

    /// <summary>
    /// Description: Restores the original scales of all tracked objects and clears the dictionary.
    /// Context: Internal helper called during deactivation.
    /// Justification: A clean sweep to prevent memory leaks and ghost references when the suction zone shuts down.
    /// </summary>
    private void ResetAllTrackedScales()
    {
        foreach (var pair in _trackedScales)
        {
            if (pair.Key != null)
            {
                pair.Key.ResetScale();
            }
        }
        _trackedScales.Clear();
    }

    /// <summary>
    /// Description: OnDrawGizmos callback. Visualizes suction thresholds in the Unity editor.
    /// Context: Unity Editor drawing callback.
    /// Justification: Crucial for visualizing the invisible physics interactions and tuning the `_shrinkStartDistance` and `_absorbDistance` variables.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!_drawGizmos || _nozzleTransform == null)
        {
            return;
        }

        Vector3 pos = _nozzleTransform.position;

        // Draw absorption sphere (red)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(pos, _absorbDistance);

        // Draw start of shrink sphere (yellow)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(pos, _shrinkStartDistance);
    }
}
