using UnityEngine;

namespace Gameplay.Phantoms
{
    /// <summary>
    /// Description: Handles visual levitation, floating bobbing, and gentle tilting for Phantom entities.
    /// Context: Attached to the visual model/mesh child of a phantom.
    /// Justification: Decouples the visual floating effect from the ground-level physics/NavMeshAgent root, ensuring smooth aesthetics without causing navigation jitter.
    /// </summary>
    public class PhantomFloatingVisual : MonoBehaviour
    {
        [Header("Hover Configuration")]
        [Tooltip("Role: Base height offset above the parent root.\nUse Case: Keeps the ghost floating in the air above the floor.")]
        [SerializeField] private float _baseHeight = 1.0f;

        [Tooltip("Role: Vertical bobbing amplitude (distance in meters).\nUse Case: Visual up and down oscillation.")]
        [SerializeField] private float _bobAmplitude = 0.18f;

        [Tooltip("Role: Speed/frequency of the vertical bobbing cycle.\nUse Case: Controls how fast the floating rhythm feels.")]
        [SerializeField] private float _bobFrequency = 2.2f;

        [Header("Sway & Tilt Configuration")]
        [Tooltip("Role: Maximum tilt angle in degrees during floating.\nUse Case: Gives an organic, ghostly sway to the model.")]
        [SerializeField] private float _swayAngle = 6.0f;

        [Tooltip("Role: Speed of the rotation sway cycle.\nUse Case: Pacing of the rotational drift.")]
        [SerializeField] private float _swayFrequency = 1.4f;

        private float _randomOffset;

        private void Awake()
        {
            // Give each phantom instance a unique phase offset so multiple phantoms don't bob in mechanical unison
            _randomOffset = Random.Range(0f, 10f);
        }

        private void Update()
        {
            float time = Time.time + _randomOffset;

            // 1. Calculate vertical bobbing
            float verticalOffset = _baseHeight + Mathf.Sin(time * _bobFrequency) * _bobAmplitude;
            transform.localPosition = new Vector3(0f, verticalOffset, 0f);

            // 2. Calculate gentle organic sway
            float rollAngle = Mathf.Sin(time * _swayFrequency) * _swayAngle;
            float pitchAngle = Mathf.Cos(time * _swayFrequency * 0.7f) * (_swayAngle * 0.6f);
            transform.localRotation = Quaternion.Euler(pitchAngle, 0f, rollAngle);
        }
    }
}
