using Mirror;
using UnityEngine;

/// <summary>
/// Description: Controls eye orientation using Quaternions to avoid Euler-related axis issues.
/// Context: Attached to the physical eye bone in the player's head.
/// Justification: Gives the player a sense of life and focus by physically pointing their eye at the highest priority entity in view.
/// </summary>
public class Eye : NetworkBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("Role: Reference to the script detecting targets.\nUse Case: Target acquisition.\nJustification: Decouples the physical rotation logic from the detection logic.")]
    private PlayerViewRange _playerViewRange;

    [Header("Settings")]
    [SerializeField, Tooltip("Role: How fast the eye follows the target.\nUse Case: Slerp speed.\nJustification: Simulates biological saccadic movement constraints.")]
    private float _rotationSpeed = 8f;

    [Tooltip("Role: Enable eye debug logs.\nUse Case: Target tracking debug.\nJustification: Verifies if the eye logic is properly receiving the target from ViewRange.")]
    [SerializeField] private bool _showDebugLogs = true;

    private Quaternion _initialLocalRotation;
    private Quaternion _targetLocalRotation;

    /// <summary>
    /// Description: Start callback. Caches initial orientation.
    /// Context: Lifecycle event.
    /// Justification: Storing the initial rotation as our "looking forward" reference makes the script bone-orientation agnostic.
    /// </summary>
    private void Start()
    {
        // Store the initial rotation as our "looking forward" reference.
        // This makes the script bone-orientation agnostic.
        _initialLocalRotation = transform.localRotation;
        _targetLocalRotation = _initialLocalRotation;
    }

    /// <summary>
    /// Description: Update callback. Triggers rotation math.
    /// Context: Update lifecycle event.
    /// Justification: Only the local player calculates their own eye movement for responsiveness and to save network bandwidth.
    /// </summary>
    private void Update()
    {
        // Only the local player calculates their own eye movement for responsiveness.
        if (!isLocalPlayer) return;

        CalculateTargetRotation();
        ApplyRotation();
    }

    /// <summary>
    /// Description: Determines the rotation needed to look at the highest priority target.
    /// Context: Called during Update.
    /// Justification: Calculates a target quaternion in local space based on the original offset, preventing gimbal lock and weird twisting.
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
    /// Description: Smoothly rotates the eye towards the target rotation.
    /// Context: Called during Update.
    /// Justification: Uses Slerp to create an organic, non-linear tracking motion.
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
