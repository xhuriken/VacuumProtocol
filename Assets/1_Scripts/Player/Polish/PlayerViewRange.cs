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
    
    private PlayerModelConfiguration _modelConfig;

    private Vector3 ViewForward => _modelConfig != null 
        ? _modelConfig.GetCorrectedForward(_viewReference) 
        : _viewReference.forward;


    [SerializeField] private readonly List<IEntity> _detectedEntities = new List<IEntity>();
    [SerializeField] private IEntity _highestPriorityEntity;
    /// <summary>
    /// Accesseur pour l'entité prioritaire actuelle.
    /// </summary>
    public IEntity HighestPriorityEntity => _highestPriorityEntity;
    private void Start()
    {
        _modelConfig = GetComponentInParent<PlayerModelConfiguration>();
    }

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
            Vector3 directionToTarget = (target.transform.position - _viewReference.position).normalized;

            // Cone check
            if (Vector3.Angle(ViewForward, directionToTarget) < _viewAngle / 2f)
            {
                float distanceToTarget = Vector3.Distance(_viewReference.position, target.transform.position);

                // Obstacle check !
                if (!Physics.Raycast(_viewReference.position, directionToTarget, distanceToTarget, _obstacleLayer))
                {
                    if (target.TryGetComponent(out IEntity entity))
                    {
                        if (entity.gameObject == this.gameObject) continue;
                        Debug.Log($"Entity found : {entity.Name}, Prio : {entity.PriorityLevel}");
                        // Stock here in a list of founded, and sort by priority.
                        _detectedEntities.Add(entity);
                    }
                }
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
    }


    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_viewReference.position, _viewDistance);

        Vector3 forward = ViewForward;
        Vector3 leftBoundary = Quaternion.AngleAxis(-_viewAngle / 2f, _viewReference.up) * forward;
        Vector3 rightBoundary = Quaternion.AngleAxis(_viewAngle / 2f, _viewReference.up) * forward;

        Gizmos.color = Color.red;
        Gizmos.DrawRay(_viewReference.position, leftBoundary * _viewDistance);
        Gizmos.DrawRay(_viewReference.position, rightBoundary * _viewDistance);
    }
}