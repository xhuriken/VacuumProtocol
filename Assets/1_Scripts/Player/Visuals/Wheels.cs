using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Description: Controls wheel orientation based on movement direction.
/// Context: Attached to the player visuals root.
/// Justification: Wheels pivot on the Y-axis to face the direction of travel, similar to office chair casters, providing visual feedback for movement direction.
/// </summary>
public class WheelSteering : NetworkBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("Role: List of wheel transforms/bones to orient.\nUse Case: Wheel references.\nJustification: Multiple wheels are iterated and turned simultaneously.")]
    private List<Transform> _wheels = new List<Transform>();

    [Header("Settings")]
    [SerializeField, Tooltip("Role: How fast the wheels rotate to face the movement direction.\nUse Case: Lerp speed.\nJustification: A higher value means the wheels snap faster to new directions.")]
    private float _steeringSpeed = 10f;

    [SerializeField, Tooltip("Role: Minimum velocity to trigger orientation update.\nUse Case: Threshold.\nJustification: Prevents wheels from jittering wildly when the player is barely moving or fully stopped.")]
    private float _minVelocityThreshold = 0.1f;

    [Tooltip("Role: Base rotation offset in degrees.\nUse Case: Alignment correction.\nJustification: Corrects models whose wheel forward axis doesn't perfectly align with the global Z forward.")]
    [SerializeField]
    private float baseOffset = 180f;

    private Rigidbody _rb;

    /// <summary>
    /// Description: Start callback. Caches the Rigidbody.
    /// Context: Lifecycle event.
    /// Justification: We cache the parent Rigidbody once to query its velocity every frame without GC allocation.
    /// </summary>
    private void Start()
    {
        // Cache Rigidbody for all clients to allow synced visuals if velocity is networked
        _rb = GetComponentInParent<Rigidbody>();
    }

    /// <summary>
    /// Description: Update callback. Updates wheel rotation.
    /// Context: Update lifecycle event.
    /// Justification: Polled every frame for smooth visual updates.
    /// </summary>
    private void Update()
    {
        // We update the orientation based on the Rigidbody velocity.
        // This works for the local player and potentially for remote players if velocity is synced.
        if (_rb == null) return;

        UpdateWheelOrientation();
    }

    /// <summary>
    /// Description: Calculates and applies rotation to all wheels.
    /// Context: Called during Update.
    /// Justification: Isolates the velocity on the horizontal plane and aligns the wheel bones independently of the body's rotation.
    /// </summary>
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
