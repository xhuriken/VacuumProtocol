using Mirror;
using UnityEngine;

/// <summary>
/// Controls eye orientation using Quaternions to avoid Euler-related axis issues.
/// Uses the initial editor rotation as the reference for "looking straight ahead".
/// </summary>
public class Eye : NetworkBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("Reference to the script detecting targets.")]
    private PlayerViewRange _playerViewRange;

    [Header("Settings")]
    [SerializeField, Tooltip("How fast the eye follows the target.")]
    private float _rotationSpeed = 8f;

    [SerializeField] private bool _showDebugLogs = true;

    private Quaternion _initialLocalRotation;
    private Quaternion _targetLocalRotation;

    private void Start()
    {
        // Store the initial rotation as our "looking forward" reference.
        // This makes the script bone-orientation agnostic.
        _initialLocalRotation = transform.localRotation;
        _targetLocalRotation = _initialLocalRotation;
    }

    private void Update()
    {
        // Only the local player calculates their own eye movement for responsiveness.
        if (!isLocalPlayer) return;

        CalculateTargetRotation();
        ApplyRotation();
    }

    /// <summary>
    /// Determines the rotation needed to look at the highest priority target.
    /// </summary>
    private void CalculateTargetRotation()
    {
        if (_playerViewRange != null && _playerViewRange.HighestPriorityEntity != null)
        {
            Transform targetPoint = _playerViewRange.HighestPriorityEntity.LookAtPoint;
            Vector3 directionToTarget = targetPoint.position - transform.position;

            if (directionToTarget.sqrMagnitude > 0.001f)
            {
                // 1. Calculate the world rotation that faces the target.
                Quaternion worldLookRot = Quaternion.LookRotation(directionToTarget.normalized, Vector3.up);

                // 2. Convert to local rotation and apply the initial offset.
                // This ensures that whatever direction the bone was facing at Start is 
                // what now faces the target.
                _targetLocalRotation = Quaternion.Inverse(transform.parent.rotation) * worldLookRot * _initialLocalRotation;
                
                if (_showDebugLogs) Debug.Log($"<color=magenta>[Eye] Looking at:</color> {targetPoint.name}, TargetLocalRot: {_targetLocalRotation.eulerAngles}");
            }
        }
        else
        {
            if (_showDebugLogs && _targetLocalRotation != _initialLocalRotation) Debug.Log("<color=magenta>[Eye] Resetting to forward.</color>");
            // Reset to the original forward-facing position.
            _targetLocalRotation = _initialLocalRotation;
        }
    }

    /// <summary>
    /// Smoothly rotates the eye towards the target rotation.
    /// </summary>
    private void ApplyRotation()
    {
        transform.localRotation = Quaternion.Slerp(
            transform.localRotation,
            _targetLocalRotation,
            Time.deltaTime * _rotationSpeed
        );
    }
}
