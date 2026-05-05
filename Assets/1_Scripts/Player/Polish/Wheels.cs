using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Controls wheel orientation based on movement direction.
/// Wheels pivot on the Y-axis to face the direction of travel, similar to office chair casters.
/// </summary>
public class WheelSteering : NetworkBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("List of wheel transforms/bones to orient.")]
    private List<Transform> _wheels = new List<Transform>();

    [Header("Settings")]
    [SerializeField, Tooltip("How fast the wheels rotate to face the movement direction.")]
    private float _steeringSpeed = 10f;

    [SerializeField, Tooltip("Minimum velocity to trigger orientation update.")]
    private float _minVelocityThreshold = 0.1f;

    [SerializeField]
    private float baseOffset = 180f;

    private Rigidbody _rb;

    private void Start()
    {
        // Cache Rigidbody for all clients to allow synced visuals if velocity is networked
        _rb = GetComponentInParent<Rigidbody>();
    }

    private void Update()
    {
        // We update the orientation based on the Rigidbody velocity.
        // This works for the local player and potentially for remote players if velocity is synced.
        if (_rb == null) return;

        UpdateWheelOrientation();
    }

    private void UpdateWheelOrientation()
    {
        Vector3 velocity = _rb.linearVelocity;
        velocity.y = 0; // Project movement on the horizontal plane

        // Skip update if moving too slowly to determine a reliable direction
        if (velocity.sqrMagnitude < _minVelocityThreshold * _minVelocityThreshold)
            return;

        // 1. Determine the target world rotation (Forward = Movement direction)
        Quaternion targetWorldRot = Quaternion.LookRotation(velocity.normalized, Vector3.up);

        foreach (Transform wheel in _wheels)
        {
            if (wheel == null) continue;

            // 2. Convert the world target to local space relative to the wheel's parent.
            // This ensures the orientation is correct regardless of the player's body rotation.
            Quaternion targetLocalRot = Quaternion.Inverse(wheel.parent.rotation) * targetWorldRot;

            // 3. Handle the Y-axis pivot only (office chair behavior)
            // We use LerpAngle to ensure smooth rotation and handle the 360-degree wrap correctly.
            float currentY = wheel.localEulerAngles.y;
            float targetY = targetLocalRot.eulerAngles.y - baseOffset;
            float smoothedY = Mathf.LerpAngle(currentY, targetY, _steeringSpeed * Time.deltaTime);

            // 4. Apply the new Y rotation while preserving the bone's original X and Z offsets.
            wheel.localEulerAngles = new Vector3(wheel.localEulerAngles.x, smoothedY, wheel.localEulerAngles.z);
        }
    }
}
