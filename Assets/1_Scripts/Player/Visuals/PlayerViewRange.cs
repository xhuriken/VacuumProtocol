using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// Description: Detects objects of type IEntity within a defined vision cone and range.
/// Context: Attached to the player prefab.
/// Justification: Handles priority-based target selection for the eye to look at, using physics overlaps and raycasts for line-of-sight.
/// </summary>
public class PlayerViewRange : NetworkBehaviour
{
    [Header("Vision Settings")]
    [Tooltip("Role: The maximum distance the player can see.\nUse Case: Sphere overlap radius.\nJustification: Caps detection range to save performance and prevent seeing across the map.")]
    [SerializeField] private float _viewDistance = 10f;
    
    [Tooltip("Role: The angle of the vision cone.\nUse Case: FOV check.\nJustification: Rejects entities that are behind the player or outside peripheral vision.")]
    [SerializeField] private float _viewAngle = 45f;
    
    [Tooltip("Role: Layer mask for valid entities.\nUse Case: Physics filtering.\nJustification: We only care about objects that can actually be looked at (collectibles, players).")]
    [SerializeField] private LayerMask _entityLayer;
    
    [Tooltip("Role: Layer mask for objects that block line-of-sight.\nUse Case: Physics filtering.\nJustification: Prevents the eye from tracking objects hidden behind walls.")]
    [SerializeField] private LayerMask _obstacleLayer;

    [Header("References")]
    [Tooltip("Role: The origin point and forward direction of the vision cone.\nUse Case: View orientation.\nJustification: Should be the player's head or camera, not the root body, to accurately reflect where they are looking.")]
    [SerializeField] private Transform _viewReference;
    
    [Tooltip("Role: Enable debug logs.\nUse Case: Debugging.\nJustification: Helps trace why an entity might be rejected (angle vs distance vs obstacle).")]
    [SerializeField] private bool _showDebugLogs = true;

    [Tooltip("Role: List of entities currently in view.\nUse Case: State tracking.\nJustification: Serialized for inspector debugging only.")]
    [SerializeField] private readonly List<IEntity> _detectedEntities = new List<IEntity>();
    
    [Tooltip("Role: The current best entity to look at.\nUse Case: State tracking.\nJustification: Serialized for inspector debugging only.")]
    [SerializeField] private IEntity _highestPriorityEntity;

    /// <summary>
    /// Description: Accessor for the currently highest priority detected entity.
    /// Context: Read by external visual scripts (like Eye.cs).
    /// Justification: Provides a single source of truth for the current target.
    /// </summary>
    public IEntity HighestPriorityEntity => _highestPriorityEntity;

    /// <summary>
    /// Description: Start callback. Disables logic for remote clients.
    /// Context: Lifecycle event.
    /// Justification: Remote players don't need to run detection logic locally since their eye movements aren't networked (to save bandwidth).
    /// </summary>
    private void Start()
    {
        // Disable by default for non-local players to save performance
        if (!isLocalPlayer)
        {
            enabled = false;
        }
    }

    /// <summary>
    /// Description: Re-enables logic for the local player.
    /// Context: Mirror NetworkBehaviour callback.
    /// Justification: Start might run before network authority is established, so we double-check here.
    /// </summary>
    public override void OnStartLocalPlayer()
    {
        enabled = true;
    }

    /// <summary>
    /// Description: Update callback. Triggers detection.
    /// Context: Update lifecycle event.
    /// Justification: Continuously updates the target list every frame.
    /// </summary>
    private void Update()
    {
        DetectEntities();
    }

    /// <summary>
    /// Description: Scans the environment for entities, then filters them by angle and line-of-sight.
    /// Context: Called during Update.
    /// Justification: Uses OverlapSphere first to quickly cull distant objects, then uses math (Vector3.Angle) and raycasts to precisely filter the remaining candidates.
    /// </summary>
    private void DetectEntities()
    {
        if (!isLocalPlayer || _viewReference == null) return;

        // Reset list for the current frame
        _detectedEntities.Clear();
        _highestPriorityEntity = null;

        // Perform a spherical overlap check to find potential targets in range
        Collider[] targetsInRadius = Physics.OverlapSphere(_viewReference.position, _viewDistance, _entityLayer);

        foreach (Collider target in targetsInRadius)
        {
            if (_showDebugLogs) Debug.Log($"<color=cyan>[ViewRange]</color> Checking {target.name}...");

            Vector3 directionToTarget = (target.transform.position - _viewReference.position).normalized;

            // Cone / FOV check
            float angle = Vector3.Angle(_viewReference.forward, directionToTarget);
            if (angle < _viewAngle / 2f)
            {
                float distanceToTarget = Vector3.Distance(_viewReference.position, target.transform.position);

                // Line-of-sight check to ensure no obstacles are blocking the view
                if (!Physics.Raycast(_viewReference.position, directionToTarget, distanceToTarget, _obstacleLayer))
                {
                    if (target.TryGetComponent(out IEntity entity))
                    {
                        // Don't detect ourselves
                        if (entity.gameObject == this.gameObject) continue;

                        if (_showDebugLogs) Debug.Log($"<color=green>[ViewRange] Entity Accepted:</color> {entity.Name}, Prio: {entity.PriorityLevel}");
                        _detectedEntities.Add(entity);
                    }
                }
                else if (_showDebugLogs)
                {
                    Debug.Log($"<color=red>[ViewRange] Entity {target.name} Rejected:</color> Obstacle in the way.");
                }
            }
            else if (_showDebugLogs)
            {
                Debug.Log($"<color=orange>[ViewRange] Entity {target.name} Rejected:</color> Outside FOV (Angle: {angle} > {_viewAngle / 2f}).");
            }
        }
        UpdatePriority();
    }

    /// <summary>
    /// Description: Selects the entity with the highest priority level from the detected list.
    /// Context: Called after DetectEntities completes.
    /// Justification: Ensures the eye looks at the most "interesting" object (e.g. another player) rather than just the closest object.
    /// </summary>
    private void UpdatePriority()
    {
        if (_detectedEntities.Count == 0)
        {
            return;
        }

        IEntity bestEntity = _detectedEntities[0];
        // Compare priorities to find the best target
        for (int i = 1; i < _detectedEntities.Count; i++)
        {
            if (_detectedEntities[i].PriorityLevel > bestEntity.PriorityLevel)
            {
                bestEntity = _detectedEntities[i];
            }
        }

        _highestPriorityEntity = bestEntity;
        if (_showDebugLogs && _highestPriorityEntity != null)
        {
            Debug.Log($"<color=yellow>[ViewRange] Target locked on:</color> {_highestPriorityEntity.Name}");
        }
    }

    /// <summary>
    /// Description: Draws debug visualizers in the editor.
    /// Context: Editor-only callback.
    /// Justification: Visually confirms the OverlapSphere radius and the Vision Cone angles.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (_viewReference == null) return;

        // Draw the detection sphere
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_viewReference.position, _viewDistance);

        // Draw the vision cone boundaries
        Vector3 forward = _viewReference.forward;
        Vector3 leftBoundary = Quaternion.AngleAxis(-_viewAngle / 2f, _viewReference.up) * forward;
        Vector3 rightBoundary = Quaternion.AngleAxis(_viewAngle / 2f, _viewReference.up) * forward;

        Gizmos.color = Color.red;
        Gizmos.DrawRay(_viewReference.position, leftBoundary * _viewDistance);
        Gizmos.DrawRay(_viewReference.position, rightBoundary * _viewDistance);
    }
}