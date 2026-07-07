using UnityEngine;
using Shapes;

/// <summary>
/// Animates the dash offset of a Shapes Disc component to create a seamless, infinite dash-movement effect.
/// </summary>
public class InfiniteRotate : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Shapes Disc component to animate. If left unassigned, it will automatically find one on this GameObject.")]
    [SerializeField] private Disc _disc;

    [Header("Animation Settings")]
    [Tooltip("The speed at which the dash offset moves. Positive values move forward, negative values move backward.")]
    [SerializeField] private float _speed = 1.0f;

    private float _currentDashOffset;

    private void Awake()
    {
        if (_disc == null)
        {
            _disc = GetComponent<Disc>();
        }
    }

    private void OnValidate()
    {
        if (_disc == null)
        {
            _disc = GetComponent<Disc>();
        }
    }

    private void Update()
    {
        if (_disc == null || !_disc.Dashed) return;

        // In Shapes, the DashOffset property is already normalized such that an offset of 1.0f 
        // represents exactly one full dash period (one dash + one spacing), regardless of space settings or dimensions.
        // Therefore, to rotate seamlessly and infinitely, the cycle period is always exactly 1.0f.
        const float dashPeriod = 1.0f;

        // Increment the offset over time scaled by speed
        _currentDashOffset += Time.deltaTime * _speed;

        // Wrap the offset back to 0 once a full cycle (1.0f) is complete to prevent floating point drift.
        _currentDashOffset %= dashPeriod;

        // Keep the offset positive even if speed is negative
        if (_currentDashOffset < 0)
        {
            _currentDashOffset += dashPeriod;
        }

        _disc.DashOffset = _currentDashOffset;
    }
}
