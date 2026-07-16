using Mirror;
using UnityEngine;

namespace VacuumProtocol.Player.Visuals
{
    /// <summary>
    /// Description: Controls eye orientation and pupil tracking using Quaternions to avoid Euler-related axis issues.
    /// Context: Attached to the physical eye bone in the player's head.
    /// Justification: Gives the player a sense of life by pointing their eye (70% speed) and pupil (100% speed) at the targets.
    /// </summary>
    public class Eye : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField, Tooltip("Role: Reference to the script detecting targets.\nUse Case: Target acquisition.")]
        private PlayerViewRange _playerViewRange;

        [SerializeField, Tooltip("Role: Pupil bone transform inside the eye.\nUse Case: Instant 100% tracking.")]
        private Transform _pupilBone;

        [Header("Settings")]
        [SerializeField, Tooltip("Role: How fast the eye bone follows the target.\nUse Case: Slerp speed.\nJustification: Simulates biological saccadic movement constraints.")]
        private float _rotationSpeed = 8f;

        [Tooltip("Role: Enable eye debug logs.\nUse Case: Target tracking debug.")]
        [SerializeField] private bool _enableDebugLogs = true;

        private Quaternion _initialLocalRotation;
        private Quaternion _targetLocalRotation;

        private Quaternion _pupilInitialLocalRot;
        private Quaternion _pupilInitialWorldRotOffset;

        /// <summary>
        /// Description: Start callback. Caches initial orientations and asserts references.
        /// </summary>
        private void Start()
        {
            if (_playerViewRange == null)
            {
                throw new System.NullReferenceException($"[Eye] Missing required PlayerViewRange component on {name}!");
            }

            if (_pupilBone == null)
            {
                throw new System.NullReferenceException($"[Eye] Pupil bone transform (_pupilBone) is NOT assigned in the Inspector on {name}!");
            }

            _initialLocalRotation = transform.localRotation;
            _targetLocalRotation = _initialLocalRotation;

            _pupilInitialLocalRot = _pupilBone.localRotation;
            
            // Cache the initial pupil offset relative to the eye transform
            _pupilInitialWorldRotOffset = Quaternion.Inverse(transform.rotation) * _pupilBone.rotation;
        }

        /// <summary>
        /// Description: Update callback. Updates eye and pupil tracking.
        /// </summary>
        private void Update()
        {
            if (!isLocalPlayer) return;

            CalculateTargetRotation();
            ApplyRotation();
        }

        /// <summary>
        /// Description: Determines the target rotation for the eye (70% follow) and applies 100% rotation to the pupil.
        /// </summary>
        private void CalculateTargetRotation()
        {
            if (_playerViewRange.HighestPriorityEntity != null)
            {
                Transform targetPoint = _playerViewRange.HighestPriorityEntity.LookAtPoint;
                Vector3 directionToTarget = targetPoint.position - transform.position;

                if (directionToTarget.sqrMagnitude > 0.001f)
                {
                    Quaternion worldLookRot = Quaternion.LookRotation(directionToTarget.normalized, Vector3.up);

                    // Eye bone target local rotation (slerped later)
                    _targetLocalRotation = Quaternion.Inverse(transform.parent.rotation) * worldLookRot * _initialLocalRotation;

                    // Pupil bone gets 100% instant world rotation matching the target
                    _pupilBone.rotation = worldLookRot * _pupilInitialWorldRotOffset;

                    if (_enableDebugLogs)
                    {
                        Debug.Log($"[Eye] {name} tracking target: {targetPoint.name}");
                    }
                }
            }
            else
            {
                // Reset both to forward-facing orientations if target is lost
                _targetLocalRotation = _initialLocalRotation;
                _pupilBone.localRotation = _pupilInitialLocalRot;
            }
        }

        /// <summary>
        /// Description: Smoothly slerps the eye bone local rotation.
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
}
