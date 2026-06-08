using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attached to a trigger volume (e.g. SphereCollider) representing the vacuum suction field.
/// Applies target pull forces, handles visual shrinking as objects approach the nozzle,
/// and triggers item absorption when they reach the nozzle.
/// </summary>
[RequireComponent(typeof(Collider))]
public class VacuumSuctionZone : MonoBehaviour
{
    [Header("Suction Settings")]
    [Tooltip("Base pull force multiplier applied to Rigidbody objects inside the zone.")]
    [SerializeField]
    private float _suctionForce = 25f;

    [Tooltip("Distance from nozzle at which objects begin shrinking in scale.")]
    [SerializeField]
    private float _shrinkStartDistance = 1.0f;

    [Tooltip("Distance from nozzle at which objects are fully absorbed into the inventory.")]
    [SerializeField]
    private float _absorbDistance = 0.25f;

    [Tooltip("The Transform representing the nozzle tip where items converge. Defaults to this transform if null.")]
    [SerializeField]
    private Transform _nozzleTransform;

    [Header("Debug")]
    [Tooltip("Draws helper wireframes in the Editor scene view.")]
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
    /// Awake callback. Initialise trigger references.
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
    /// Update callback. If the zone was deactivated, restore scales of any objects currently inside.
    /// </summary>
    private void Update()
    {
        if (!IsActive && _trackedScales.Count > 0)
        {
            ResetAllTrackedScales();
        }
    }

    /// <summary>
    /// OnTriggerStay callback. Processes attraction forces and scale shrinking.
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
    /// OnTriggerExit callback. Restore object scale if it manages to break free of the vacuum flow.
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
    /// Restores the original scales of all tracked objects and clears the dictionary.
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
    /// OnDrawGizmos callback. Visualizes suction thresholds in the Unity editor.
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
