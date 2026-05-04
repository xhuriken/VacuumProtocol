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
    private List<Quaternion> _initialRotations = new List<Quaternion>();

    /// <summary>
    /// Initialize references and store initial rotations.
    /// </summary>
    public override void OnStartLocalPlayer()
    {
        _playerMovement = GetComponentInParent<PlayerPhysicsMovement>();

        if (_playerMovement != null)
        {
            _rb = _playerMovement.GetComponent<Rigidbody>();
        }

        // Store the initial rotation of each wheel to use it as the "zero" point
        _initialRotations.Clear();
        foreach (Transform wheel in _wheels)
        {
            if (wheel != null)
                _initialRotations.Add(wheel.localRotation);
        }
    }

    private void Update()
    {
        if (!isLocalPlayer || _rb == null || _initialRotations.Count == 0)
        {
            return;
        }

        RotateWheelsToMovementDirection();
    }

    /// <summary>
    /// Calculates the target rotation relative to the initial orientation.
    /// </summary>
    private void RotateWheelsToMovementDirection()
    {
        Vector3 velocity = _rb.linearVelocity;
        velocity.y = 0;

        if (velocity.magnitude < _minVelocityThreshold)
        {
            return;
        }

        // Convert world velocity to local direction relative to the ROOT player (not the potentially flipped model)
        Vector3 localDirection = _playerMovement.transform.InverseTransformDirection(velocity.normalized);
        
        // Target steering rotation
        Quaternion steeringRotation = Quaternion.LookRotation(localDirection, Vector3.up);

        for (int i = 0; i < _wheels.Count; i++)
        {
            if (_wheels[i] == null) continue;

            // Apply steering relative to the initial "zero" rotation of the wheel
            Quaternion targetRotation = _initialRotations[i] * steeringRotation;

            _wheels[i].localRotation = Quaternion.Slerp(
                _wheels[i].localRotation,
                targetRotation,
                _steeringSpeed * Time.deltaTime
            );
        }
    }
}