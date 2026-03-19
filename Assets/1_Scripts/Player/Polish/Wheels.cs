using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Rotates wheels on the Y axis based on movement direction while preserving X and Z rotations.
/// </summary>
public class WheelSteering : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private List<Transform> _wheels = new List<Transform>();
    [SerializeField] private float _steeringSpeed = 10f;
    [SerializeField] private float _minVelocityThreshold = 0.1f;

    private PlayerPhysicsMovement _playerMovement;
    private Rigidbody _rb;

    /// <summary>
    /// Initialize references for the local player.
    /// </summary>
    public override void OnStartLocalPlayer()
    {
        _playerMovement = GetComponentInParent<PlayerPhysicsMovement>();

        if (_playerMovement != null)
        {
            _rb = _playerMovement.GetComponent<Rigidbody>();
        }
    }

    private void Update()
    {
        if (!isLocalPlayer || _rb == null)
        {
            return;
        }

        RotateWheelsToMovementDirection();
    }

    /// <summary>
    /// Calculates the target Y rotation and applies it while keeping X and Z intact.
    /// </summary>
    private void RotateWheelsToMovementDirection()
    {
        Vector3 velocity = _rb.linearVelocity;
        velocity.y = 0;

        // Stop processing if the robot is nearly still
        if (velocity.magnitude < _minVelocityThreshold)
        {
            return;
        }

        // Convert world velocity to local direction
        Vector3 localDirection = transform.InverseTransformDirection(velocity.normalized);

        // Get the Y angle we need to face the movement
        float targetY = Quaternion.LookRotation(localDirection).eulerAngles.y;

        foreach (Transform wheel in _wheels)
        {
            // Simple logic: Get current angles, replace Y, and Slerp to it
            Vector3 currentAngles = wheel.localEulerAngles;
            Quaternion targetRotation = Quaternion.Euler(currentAngles.x, targetY, currentAngles.z);

            wheel.localRotation = Quaternion.Slerp(
                wheel.localRotation,
                targetRotation,
                _steeringSpeed * Time.deltaTime
            );
        }
    }
}