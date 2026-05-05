using Mirror;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// Détecte les objets de type IEntity dans un cône de vision défini.
/// </summary>
public class PlayerViewRange : NetworkBehaviour
{
    [Header("Vision Settings")]
    [SerializeField] private float _viewDistance = 10f;
    [SerializeField] private float _viewAngle = 45f;
    [SerializeField] private LayerMask _entityLayer;
    [SerializeField] private LayerMask _obstacleLayer;
    [Header("References")]
    [SerializeField] private Transform _viewReference;
    [SerializeField] private bool _showDebugLogs = true;

    [SerializeField] private readonly List<IEntity> _detectedEntities = new List<IEntity>();
    [SerializeField] private IEntity _highestPriorityEntity;
    /// <summary>
    /// Accesseur pour l'entité prioritaire actuelle.
    /// </summary>
    public IEntity HighestPriorityEntity => _highestPriorityEntity;

    private void Update()
    {
        DetectEntities();
    }

    /// <summary>
    /// Recherche les entités dans le rayon, puis filtre par angle et visibilité.
    /// </summary>
    private void DetectEntities()
    {
        if (!isLocalPlayer || _viewReference == null) return;
        // Reset list
        _detectedEntities.Clear();
        _highestPriorityEntity = null;

        // Raycast Sphere
        Collider[] targetsInRadius = Physics.OverlapSphere(_viewReference.position, _viewDistance, _entityLayer);

        foreach (Collider target in targetsInRadius)
        {
            if (_showDebugLogs) Debug.Log($"<color=cyan>[ViewRange]</color> Checking {target.name}...");

            Vector3 directionToTarget = (target.transform.position - _viewReference.position).normalized;

            // Cone check
            float angle = Vector3.Angle(_viewReference.forward, directionToTarget);
            if (angle < _viewAngle / 2f)
            {
                float distanceToTarget = Vector3.Distance(_viewReference.position, target.transform.position);

                // Obstacle check !
                if (!Physics.Raycast(_viewReference.position, directionToTarget, distanceToTarget, _obstacleLayer))
                {
                    if (target.TryGetComponent(out IEntity entity))
                    {
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

    private void UpdatePriority()
    {
        if (_detectedEntities.Count == 0)
        {
            return;
        }

        IEntity bestEntity = _detectedEntities[0];
        // Sort by priority, and keep the best one.
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


    private void OnDrawGizmos()
    {
        if (_viewReference == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_viewReference.position, _viewDistance);

        Vector3 forward = _viewReference.forward;
        Vector3 leftBoundary = Quaternion.AngleAxis(-_viewAngle / 2f, _viewReference.up) * forward;
        Vector3 rightBoundary = Quaternion.AngleAxis(_viewAngle / 2f, _viewReference.up) * forward;

        Gizmos.color = Color.red;
        Gizmos.DrawRay(_viewReference.position, leftBoundary * _viewDistance);
        Gizmos.DrawRay(_viewReference.position, rightBoundary * _viewDistance);
    }
}