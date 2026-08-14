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

        [SerializeField, Tooltip("Role: Reference to the player's camera.\nUse Case: Eye tracking when no target is present.")]
        private Transform _cameraTransform;

        [Header("Settings")]
        [SerializeField, Range(0f, 1f), Tooltip("Combien l'oeil tourne vers la cible (0.75 = 75%)")]
        private float _eyeTargetWeight = 0.75f;

        [SerializeField, Range(0f, 1f), Tooltip("Combien la pupille tourne vers la cible (1.0 = 100%)")]
        private float _pupilTargetWeight = 1.0f;

        [SerializeField, Tooltip("Role: How fast the eye bone follows the target.\nUse Case: Slerp speed.\nJustification: Simulates biological saccadic movement constraints.")]
        private float _rotationSpeed = 8f;

        [SerializeField, Tooltip("Role: How fast the pupil bone follows the target.\nUse Case: Slerp speed for pupil.")]
        private float _pupilRotationSpeed = 12f;

        [Tooltip("Role: Enable eye debug logs.\nUse Case: Target tracking debug.")]
        [SerializeField] private bool _enableDebugLogs = false;

        private Quaternion _initialLocalRotation;
        private Quaternion _targetLocalRotation;

        private Quaternion _pupilInitialLocalRot;
        private Quaternion _pupilTargetLocalRotation;
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

            if (_cameraTransform == null)
            {
                Debug.LogError($"[Eye] Camera transform (_cameraTransform) is NOT assigned in the Inspector on {name}! Fallback tracking will fail.");
            }

            _initialLocalRotation = transform.localRotation;
            _targetLocalRotation = _initialLocalRotation;

            _pupilInitialLocalRot = _pupilBone.localRotation;
            _pupilTargetLocalRotation = _pupilInitialLocalRot;
            
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
        /// Description: Determines the target rotation for the eye (70% follow) and the pupil.
        /// </summary>
        private void CalculateTargetRotation()
        {
            bool hasTarget = false;
            Quaternion worldLookRot = Quaternion.identity;

            if (_playerViewRange.HighestPriorityEntity != null)
            {
                Vector3 targetPosition = _playerViewRange.HighestPriorityEntity.LookAtPoint.position;
                Vector3 directionToTarget = targetPosition - transform.position;

                if (directionToTarget.sqrMagnitude > 0.001f)
                {
                    // Use parent's up to avoid twisting the eye when the head is tilted
                    worldLookRot = Quaternion.LookRotation(directionToTarget.normalized, transform.parent.up);
                    hasTarget = true;
                }
            }
            else if (_cameraTransform != null)
            {
                // Cible la direction exacte de ce que la caméra regarde (un point virtuel à distance)
                // On utilise Quaternion.LookRotation pour forcer l'alignement avec le Up de la tête
                // Cela garantit que l'oeil ne part pas en vrille (roll) tout en récupérant le pitch complet de la cam.
                Vector3 virtualTarget = _cameraTransform.position + _cameraTransform.forward * 20f;
                Vector3 directionToTarget = virtualTarget - transform.position;
                
                if (directionToTarget.sqrMagnitude > 0.001f)
                {
                    worldLookRot = Quaternion.LookRotation(directionToTarget.normalized, transform.parent.up);
                    hasTarget = true;
                }
            }

            if (hasTarget)
            {
                // Calcul de la rotation de base (tout droit par rapport à la tête)
                Quaternion baseWorldRot = transform.parent.rotation * _initialLocalRotation;

                // L'oeil ne tourne qu'à X% vers la cible
                Quaternion eyeTargetWorld = Quaternion.Slerp(baseWorldRot, worldLookRot, _eyeTargetWeight);
                _targetLocalRotation = Quaternion.Inverse(transform.parent.rotation) * eyeTargetWorld;

                // La pupille tourne à Y% vers la cible
                Quaternion pupilTargetWorld = Quaternion.Slerp(baseWorldRot, worldLookRot, _pupilTargetWeight);
                _pupilTargetLocalRotation = Quaternion.Inverse(_pupilBone.parent.rotation) * (pupilTargetWorld * _pupilInitialWorldRotOffset);

                if (_enableDebugLogs && _playerViewRange.HighestPriorityEntity != null)
                {
                    Debug.Log($"[Eye] {name} tracking target: {_playerViewRange.HighestPriorityEntity.LookAtPoint.name}");
                }
            }
            else
            {
                // Reset both to forward-facing orientations if target is lost
                _targetLocalRotation = _initialLocalRotation;
                _pupilTargetLocalRotation = _pupilInitialLocalRot;
            }
        }

        public Transform GetPupilBone() => _pupilBone;
        public float GetEyeTargetWeight() => _eyeTargetWeight;
        public float GetPupilTargetWeight() => _pupilTargetWeight;

        /// <summary>
        /// Description: Smoothly slerps the eye bone and pupil bone local rotations.
        /// </summary>
        private void ApplyRotation()
        {
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation,
                _targetLocalRotation,
                Time.deltaTime * _rotationSpeed
            );

            _pupilBone.localRotation = Quaternion.Slerp(
                _pupilBone.localRotation,
                _pupilTargetLocalRotation,
                Time.deltaTime * _pupilRotationSpeed
            );
        }
    }
}
