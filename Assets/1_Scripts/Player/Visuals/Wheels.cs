using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Description: Controls wheel orientation based on movement direction.
/// Context: Attached to the player visuals root (Hips).
/// Justification: Wheels pivot on the Y-axis to face the direction of travel. Decoupled from physics.
/// </summary>
public class WheelSteering : NetworkBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("Role: List of wheel transforms/bones to orient.\nUse Case: Wheel references.")]
    private List<Transform> _wheels = new List<Transform>();

    [Header("Settings")]
    [SerializeField, Tooltip("Role: How fast the wheels rotate to face the movement direction.\nUse Case: Lerp speed.")]
    private float _steeringSpeed = 10f;

    [SerializeField, Tooltip("Role: Minimum distance to trigger orientation update.\nUse Case: Threshold.")]
    private float _minMoveThreshold = 0.02f;

    private Vector3 _lastPosition;
    
    // The target and current steering angle applied to all wheels (0 = forward)
    private float _targetSteeringAngle;
    private float _currentSteeringAngle;
    
    // Cache the default Y rotation of each wheel so they can have different base orientations
    private float[] _initialWheelY;

    private void Start()
    {
        _lastPosition = transform.position;
        
        _initialWheelY = new float[_wheels.Count];
        for (int i = 0; i < _wheels.Count; i++)
        {
            if (_wheels[i] != null)
            {
                _initialWheelY[i] = _wheels[i].localEulerAngles.y;
            }
        }
    }

    private void LateUpdate()
    {
        Vector3 currentPosition = transform.position;
        Vector3 movement = currentPosition - _lastPosition;
        _lastPosition = currentPosition;
        
        movement.y = 0f;

        if (movement.sqrMagnitude > _minMoveThreshold * _minMoveThreshold)
        {
            Vector3 localMovement = transform.InverseTransformDirection(movement);
            if (localMovement.sqrMagnitude > 0f)
            {
                // Angle relative to the Hips' forward direction
                _targetSteeringAngle = Mathf.Atan2(localMovement.x, localMovement.z) * Mathf.Rad2Deg;
            }
        }

        // On calcule la distance parcourue à cette frame sur le plan XZ
        float distanceMoved = movement.magnitude;

        // L'interpolation se fait en fonction de la distance, PAS du temps !
        // Comme les vraies roulettes de bureau, si on ne bouge pas, la roulette ne pivote pas.
        // (On multiplie la speed pour qu'elle réagisse bien sur de courtes distances)
        _currentSteeringAngle = Mathf.LerpAngle(_currentSteeringAngle, _targetSteeringAngle, _steeringSpeed * distanceMoved * 2f);

        for (int i = 0; i < _wheels.Count; i++)
        {
            Transform wheel = _wheels[i];
            if (wheel != null)
            {
                Vector3 euler = wheel.localEulerAngles;
                // Add the steering angle to the wheel's initial base rotation
                euler.y = _initialWheelY[i] + _currentSteeringAngle;
                wheel.localEulerAngles = euler;
            }
        }
    }
}
