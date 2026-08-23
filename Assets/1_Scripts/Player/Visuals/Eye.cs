using Mirror;
using UnityEngine;

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
    
    [SyncVar]
    private Quaternion _syncedTargetLocalRotation;

    private PlayerInputHandler _input;
    private Quaternion _pupilInitialLocalRot;
    
    [SyncVar]
    private Quaternion _syncedPupilTargetLocalRotation;
    
    private Quaternion _pupilInitialWorldRotOffset;

    [Header("Saccades (Idle Movement)")]
    [SerializeField, Tooltip("Activer les micro-mouvements (accoups) réalistes des yeux.")]
    private bool _enableSaccades = true;
    [SerializeField, Tooltip("Rayon maximum de la saccade (en mètres, projeté sur la cible).")]
    private float _saccadeRadius = 1.5f;
    [SerializeField, Tooltip("Temps min/max entre deux saccades (en secondes).")]
    private Vector2 _saccadeInterval = new Vector2(0.3f, 2.0f);
    [SerializeField, Tooltip("Vitesse de rotation de la tête au-delà de laquelle les saccades s'arrêtent (deg/sec).")]
    private float _headMovementThreshold = 30f;

    private Vector3 _currentSaccadeOffset = Vector3.zero;
    private float _nextSaccadeTime = 0f;
    private Quaternion _lastCameraRotation;
    private float _headAngularSpeed = 0f;

    // Synchronisation des deux yeux
    private Eye _masterEye;

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
        _syncedTargetLocalRotation = _initialLocalRotation;

        _pupilInitialLocalRot = _pupilBone.localRotation;
        _syncedPupilTargetLocalRotation = _pupilInitialLocalRot;
        
        // Cache the initial pupil offset relative to the eye transform
        _pupilInitialWorldRotOffset = Quaternion.Inverse(transform.rotation) * _pupilBone.rotation;
        
        _input = GetComponentInParent<PlayerInputHandler>();

        // Synchronisation des saccades : Le premier oeil trouvé devient le cerveau
        PlayerV2_Controller controller = GetComponentInParent<PlayerV2_Controller>();
        if (controller != null)
        {
            Eye[] allEyes = controller.GetComponentsInChildren<Eye>();
            if (allEyes.Length > 0)
            {
                _masterEye = allEyes[0];
            }
        }
    }

    /// <summary>
    /// Description: Update callback. Updates eye and pupil tracking.
    /// </summary>
    private void Update()
    {
        if (isLocalPlayer)
        {
            if (_cameraTransform != null)
            {
                float angleDelta = Quaternion.Angle(_lastCameraRotation, _cameraTransform.rotation);
                _headAngularSpeed = angleDelta / Time.deltaTime;
                _lastCameraRotation = _cameraTransform.rotation;
            }

            CalculateTargetRotation();
        }

        ApplyRotation();
    }

    [Command]
    private void CmdSetEyeTargets(Quaternion eyeRot, Quaternion pupilRot)
    {
        _syncedTargetLocalRotation = eyeRot;
        _syncedPupilTargetLocalRotation = pupilRot;
    }

    private void UpdateSyncedTargets(Quaternion newEyeRot, Quaternion newPupilRot)
    {
        if (Quaternion.Angle(_syncedTargetLocalRotation, newEyeRot) > 1f ||
            Quaternion.Angle(_syncedPupilTargetLocalRotation, newPupilRot) > 1f)
        {
            CmdSetEyeTargets(newEyeRot, newPupilRot);
            _syncedTargetLocalRotation = newEyeRot;
            _syncedPupilTargetLocalRotation = newPupilRot;
        }
    }

    private void UpdateSaccades()
    {
        // Synchronisation : Si je ne suis pas le maitre, je copie exactement sa saccade
        if (_masterEye != null && _masterEye != this)
        {
            _currentSaccadeOffset = _masterEye._currentSaccadeOffset;
            _headAngularSpeed = _masterEye._headAngularSpeed;
            return;
        }

        if (!_enableSaccades || _cameraTransform == null)
        {
            _currentSaccadeOffset = Vector3.zero;
            return;
        }

        // Si on tourne la tête rapidement, on annule les saccades pour recentrer le regard (Focus)
        if (_headAngularSpeed > _headMovementThreshold)
        {
            _currentSaccadeOffset = Vector3.Lerp(_currentSaccadeOffset, Vector3.zero, Time.deltaTime * 10f);
            _nextSaccadeTime = Time.time + Random.Range(0.2f, 0.5f); // Pause avant de reprendre
            return;
        }

        // Déclenchement d'un accoup sec aléatoire
        if (Time.time >= _nextSaccadeTime)
        {
            Vector2 randomCircle = Random.insideUnitCircle * _saccadeRadius;
            _currentSaccadeOffset = _cameraTransform.right * randomCircle.x + _cameraTransform.up * randomCircle.y;
            _nextSaccadeTime = Time.time + Random.Range(_saccadeInterval.x, _saccadeInterval.y);
        }
    }

    /// <summary>
    /// Description: Determines the target rotation for the eye (70% follow) and the pupil.
    /// </summary>
    private void CalculateTargetRotation()
    {
        bool hasTarget = false;
        Quaternion worldLookRot = Quaternion.identity;

        // Si on est accroupi, on ignore les entités pour forcer la vue vers la caméra
        bool isCrouching = _input != null && _input.IsCrouching;

        if (_playerViewRange.HighestPriorityEntity != null && !isCrouching)
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
            UpdateSaccades();

            // Cible la direction exacte de ce que la caméra regarde (un point virtuel à distance)
            // + l'offset de saccade aléatoire
            Vector3 virtualTarget = _cameraTransform.position + _cameraTransform.forward * 20f + _currentSaccadeOffset;
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
            Quaternion newTargetLocal = Quaternion.Inverse(transform.parent.rotation) * eyeTargetWorld;

            // La pupille tourne à Y% vers la cible
            Quaternion pupilTargetWorld = Quaternion.Slerp(baseWorldRot, worldLookRot, _pupilTargetWeight);
            Quaternion newPupilTargetLocal = Quaternion.Inverse(_pupilBone.parent.rotation) * (pupilTargetWorld * _pupilInitialWorldRotOffset);

            UpdateSyncedTargets(newTargetLocal, newPupilTargetLocal);

            if (_enableDebugLogs && _playerViewRange.HighestPriorityEntity != null)
            {
                Debug.Log($"[Eye] {name} tracking target: {_playerViewRange.HighestPriorityEntity.LookAtPoint.name}");
            }
        }
        else
        {
            // Reset both to forward-facing orientations if target is lost
            UpdateSyncedTargets(_initialLocalRotation, _pupilInitialLocalRot);
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
            _syncedTargetLocalRotation,
            Time.deltaTime * _rotationSpeed
        );

        _pupilBone.localRotation = Quaternion.Slerp(
            _pupilBone.localRotation,
            _syncedPupilTargetLocalRotation,
            Time.deltaTime * _pupilRotationSpeed
        );
    }
}

