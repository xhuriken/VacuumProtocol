using UnityEngine;
using System.Collections;

/// <summary>
/// Description: Gère les paramètres de suspension des roues.
/// Context: Attaché au Player_V2.
/// Justification: Permet de régler facilement les ressorts et limites des ConfigurableJoints des roues depuis l'inspecteur.
/// </summary>
public class PlayerV2_Suspension : MonoBehaviour
{
    [Header("Wheels Configuration")]
    [Tooltip("Les ConfigurableJoints des 4 roues.")]
    public ConfigurableJoint[] WheelJoints;

    [Header("Suspension Settings")]
    public float SpringForce = 500f;
    public float Damper = 50f;
    public float SuspensionTravel = 0.5f;

    [Tooltip("La distance à laquelle le ressort repousse la roue (doit être négatif pour repousser vers le bas).")]
    public float TargetExtension = -0.4f;

    [Header("Jump Animation Settings")]
    [Tooltip("Position cible du ressort quand la roue se rétracte en l'air (ex: 0.1 pour remonter au dessus des hanches).")]
    public float RetractedExtension = 0.1f;
    [Tooltip("Damper forcé sur la roue pendant l'animation de saut. Garantit une belle animation visqueuse même si le Damper principal (pour rouler) est très bas.")]
    public float RetractionDamper = 50f;
    [Tooltip("Délai maximum aléatoire avant que la roue ne se rétracte (crée l'effet désynchronisé).")]
    public float MaxRandomRetractionDelay = 0.15f;
    [Tooltip("Durée pendant laquelle la roue reste rétractée avant de se relâcher.")]
    public float RetractionHoldDuration = 0.15f;
    [Tooltip("Vitesse de relâchement vers l'extension normale.")]
    public float RelaxationSpeed = 4f;

    [Header("Landing Absorption")]
    [Tooltip("Amorti supplémentaire appliqué lors d'un atterrissage violent pour éviter le rebond (multiplicateur du Damper de base).")]
    public float HardLandingDamperMultiplier = 5f;
    [Tooltip("Temps nécessaire pour que l'amorti revienne à la normale après un atterrissage (en secondes).")]
    public float LandingDamperRecoveryTime = 0.25f;

    private Coroutine[] _retractionCoroutines;
    private bool[] _isWheelRetracting;
    private float _currentDamper;
    private float _damperRecoveryVelocity;

    private void Start()
    {
        _currentDamper = Damper;
        ApplySuspensionSettings();
        if (WheelJoints != null)
        {
            _retractionCoroutines = new Coroutine[WheelJoints.Length];
            _isWheelRetracting = new bool[WheelJoints.Length];
        }
    }

    private void Update()
    {
        // Récupération progressive du Damper après un atterrissage lourd
        if (_currentDamper > Damper)
        {
            _currentDamper = Mathf.SmoothDamp(_currentDamper, Damper, ref _damperRecoveryVelocity, LandingDamperRecoveryTime);
            ApplyDamper(_currentDamper);
        }
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            ApplySuspensionSettings();
        }
    }

    private void ApplySuspensionSettings()
    {
        if (WheelJoints == null) return;

        foreach (var joint in WheelJoints)
        {
            if (joint == null) continue;

            // Configurer la limite de mouvement Y (coulissement)
            SoftJointLimit limit = new SoftJointLimit();
            limit.limit = SuspensionTravel;
            joint.linearLimit = limit;

            // Configurer le ressort (Drive) sur l'axe Y
            JointDrive drive = new JointDrive();
            drive.positionSpring = SpringForce;
            drive.positionDamper = _currentDamper;
            drive.maximumForce = Mathf.Infinity;

            // Unity utilise le XDrive, YDrive, ZDrive pour les ConfigurableJoints
            // Dans le repère local du joint, le coulissement est généralement sur l'axe Y ou Z selon la configuration.
            // Si l'axe est Y (0,1,0), c'est le yDrive.

            joint.yDrive = drive;
            
            // Forcer l'extension du ressort
            joint.targetPosition = new Vector3(0, TargetExtension, 0);

            // Assurons-nous que le reste est bien bloqué
            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Limited;

            joint.angularXMotion = ConfigurableJointMotion.Locked;
            joint.angularYMotion = ConfigurableJointMotion.Locked;
            joint.angularZMotion = ConfigurableJointMotion.Locked;

            // FIX: Projection for multiplayer
            // Forcer le moteur physique à "téléporter" la roue sous le robot si elle commence à traîner (décalage horizontal)
            // à cause du NetworkTransform qui déplace le parent de manière non-physique (téléportation/interpolation).
            joint.projectionMode = JointProjectionMode.PositionAndRotation;
            joint.projectionDistance = 0.02f; // Tolérance de 2cm avant le snap
            joint.projectionAngle = 180f;

            // IMPORTANT : Pour éviter les explosions physiques (le robot expulsé en l'air) et les vrilles,
            // il FAUT que les roues glissent parfaitement, sinon la friction fait levier et détruit le joint.
            Collider wheelCollider = joint.GetComponent<Collider>();
            if (wheelCollider != null)
            {
                PhysicsMaterial zeroFriction = new PhysicsMaterial("ZeroFrictionWheel");
                zeroFriction.dynamicFriction = 0f;
                zeroFriction.staticFriction = 0f;
                zeroFriction.frictionCombine = PhysicsMaterialCombine.Minimum;
                zeroFriction.bounciness = 0f;
                zeroFriction.bounceCombine = PhysicsMaterialCombine.Minimum;
                wheelCollider.material = zeroFriction;
            }
        }
    }

    public void TriggerJumpRetraction()
    {
        if (WheelJoints == null) return;
        for (int i = 0; i < WheelJoints.Length; i++)
        {
            if (WheelJoints[i] != null)
            {
                if (_retractionCoroutines != null && _retractionCoroutines[i] != null)
                {
                    StopCoroutine(_retractionCoroutines[i]);
                }
                _retractionCoroutines[i] = StartCoroutine(AnimateWheelRetraction(i));
            }
        }
    }

    private IEnumerator AnimateWheelRetraction(int wheelIndex)
    {
        ConfigurableJoint joint = WheelJoints[wheelIndex];

        // Mini delay aléatoire pour un effet asynchrone organique
        float delay = Random.Range(0.01f, MaxRandomRetractionDelay);
        yield return new WaitForSeconds(delay);

        _isWheelRetracting[wheelIndex] = true;

        // Force le damper à une valeur idéale (ex: 50) pour avoir la même belle animation "visqueuse" 
        // même si le joueur a mis le damper global à 10 pour la conduite.
        JointDrive drive = joint.yDrive;
        drive.positionDamper = RetractionDamper;
        joint.yDrive = drive;

        // PAF Fonce vers le haut
        joint.targetPosition = new Vector3(0, RetractedExtension, 0);

        // Reste contracté
        yield return new WaitForSeconds(RetractionHoldDuration);

        // Se relâche doucement vers l'extension normale
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime * RelaxationSpeed;
            float currentY = Mathf.Lerp(RetractedExtension, TargetExtension, t);
            joint.targetPosition = new Vector3(0, currentY, 0);
            yield return null;
        }

        joint.targetPosition = new Vector3(0, TargetExtension, 0);

        // Restaure le damper global géré par la suspension
        _isWheelRetracting[wheelIndex] = false;
        drive = joint.yDrive;
        drive.positionDamper = _currentDamper;
        joint.yDrive = drive;
    }

    public void ApplyDamper(float damperValue)
    {
        if (WheelJoints == null) return;
        for (int i = 0; i < WheelJoints.Length; i++)
        {
            if (WheelJoints[i] == null || (_isWheelRetracting != null && _isWheelRetracting[i])) continue;
            JointDrive drive = WheelJoints[i].yDrive;
            drive.positionDamper = damperValue;
            WheelJoints[i].yDrive = drive;
        }
    }

    public void OnHardLanding(float impactSpeed)
    {
        // Calcul d'un Damper cible basé sur la violence du choc
        float targetDamper = Damper * HardLandingDamperMultiplier;
        _currentDamper = Mathf.Max(_currentDamper, targetDamper);
        ApplyDamper(_currentDamper);
    }
}

