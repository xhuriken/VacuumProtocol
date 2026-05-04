using UnityEngine;

/// <summary>
/// Centralized configuration for the player model orientation.
/// Useful when the 3D model is not aligned with the engine's forward axis.
/// </summary>
public class PlayerModelConfiguration : MonoBehaviour
{
    [Header("Model Orientation")]
    [Tooltip("Offset applied to the 'Forward' direction of the robot.")]
    [SerializeField] private Vector3 _forwardRotationOffset = new Vector3(0, 0, 0);

    public Vector3 ForwardRotationOffset => _forwardRotationOffset;
    public Quaternion RotationOffset => Quaternion.Euler(_forwardRotationOffset);

    /// <summary>
    /// Gets the corrected forward vector based on a reference transform.
    /// </summary>
    public Vector3 GetCorrectedForward(Transform reference)
    {
        return reference.rotation * RotationOffset * Vector3.forward;
    }

    /// <summary>
    /// Gets the corrected right vector based on a reference transform.
    /// </summary>
    public Vector3 GetCorrectedRight(Transform reference)
    {
        return reference.rotation * RotationOffset * Vector3.right;
    }
}
