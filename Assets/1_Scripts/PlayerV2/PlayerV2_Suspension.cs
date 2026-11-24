using UnityEngine;
using System.Collections;

/// <summary>
/// Description: Manages the wheel suspension settings, jump retraction and landing absorption.
/// Context: Attached to Player_V2.
/// Justification: Makes it easy to tune the springs and limits of the wheel ConfigurableJoints from the Inspector.
/// </summary>
public class PlayerV2_Suspension : MonoBehaviour
{
    [Header("Wheels Configuration")]
    [Tooltip("Role: ConfigurableJoints of the 4 wheels.")]
    public ConfigurableJoint[] WheelJoints;

    [Header("Suspension Settings")]
    [Tooltip("Role: Suspension spring force on the wheel Y drive.")]
    public float SpringForce = 500f;
    [Tooltip("Role: Base suspension damper on the wheel Y drive.")]
    public float Damper = 50f;
    [Tooltip("Role: Linear limit of the wheel Y slide (suspension travel).")]
    public float SuspensionTravel = 0.5f;

    [Tooltip("Role: Distance the spring pushes the wheel to (must be negative to push downward).")]
    public float TargetExtension = -0.4f;

    [Header("Jump Animation Settings")]
    [Tooltip("Role: Spring target position when the wheel retracts in the air (e.g. 0.1 to rise above the hips).")]
    public float RetractedExtension = 0.1f;
    [Tooltip("Role: Damper forced on the wheel during the jump animation. Guarantees a nice viscous animation even when the main (driving) Damper is very low.")]
    public float RetractionDamper = 50f;
    [Tooltip("Role: Maximum random delay before the wheel retracts (creates the desynchronized effect).")]
    public float MaxRandomRetractionDelay = 0.15f;
    [Tooltip("Role: Time the wheel stays retracted before relaxing.")]
    public float RetractionHoldDuration = 0.15f;
    [Tooltip("Role: Relaxation speed back to the normal extension.")]
    public float RelaxationSpeed = 4f;

    [Header("Landing Absorption")]
    [Tooltip("Role: Extra damping applied on a hard landing to prevent bouncing (multiplier of the base Damper).")]
    public float HardLandingDamperMultiplier = 5f;
    [Tooltip("Role: Time for the damping to return to normal after a landing (in seconds).")]
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
        // Gradual Damper recovery after a hard landing
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

            // Y motion limit (slide)
            SoftJointLimit limit = new SoftJointLimit();
            limit.limit = SuspensionTravel;
            joint.linearLimit = limit;

            // Spring (drive) on the Y axis
            JointDrive drive = new JointDrive();
            drive.positionSpring = SpringForce;
            drive.positionDamper = _currentDamper;
            drive.maximumForce = Mathf.Infinity;

            // Unity uses XDrive, YDrive, ZDrive for ConfigurableJoints.
            // In the joint local frame, the slide is usually on the Y or Z axis depending on the setup.
            // If the axis is Y (0,1,0), it is the yDrive.

            joint.yDrive = drive;
            
            // Force the spring extension
            joint.targetPosition = new Vector3(0, TargetExtension, 0);

            // Make sure everything else is locked
            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Limited;

            joint.angularXMotion = ConfigurableJointMotion.Locked;
            joint.angularYMotion = ConfigurableJointMotion.Locked;
            joint.angularZMotion = ConfigurableJointMotion.Locked;

            // FIX: Projection for multiplayer
            // Force the physics engine to "teleport" the wheel back under the robot if it starts to lag behind (horizontal offset)
            // because the NetworkTransform moves the parent non-physically (teleport/interpolation).
            joint.projectionMode = JointProjectionMode.PositionAndRotation;
            joint.projectionDistance = 0.02f; // 2 cm tolerance before snapping
            joint.projectionAngle = 180f;

            // IMPORTANT: to avoid physics explosions (robot launched into the air) and spins,
            // the wheels MUST slide perfectly, otherwise friction acts as a lever and breaks the joint.
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

    /// <summary>
    /// Starts the jump retraction animation on every wheel (each with a small random delay).
    /// </summary>
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

        // Small random delay for an organic, asynchronous effect
        float delay = Random.Range(0.01f, MaxRandomRetractionDelay);
        yield return new WaitForSeconds(delay);

        _isWheelRetracting[wheelIndex] = true;

        // Force the damper to an ideal value (e.g. 50) to keep the same nice "viscous" animation
        // even if the global damper is set to 10 for driving.
        JointDrive drive = joint.yDrive;
        drive.positionDamper = RetractionDamper;
        joint.yDrive = drive;

        // Snap upward
        joint.targetPosition = new Vector3(0, RetractedExtension, 0);

        // Stay retracted
        yield return new WaitForSeconds(RetractionHoldDuration);

        // Relax smoothly back to the normal extension
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime * RelaxationSpeed;
            float currentY = Mathf.Lerp(RetractedExtension, TargetExtension, t);
            joint.targetPosition = new Vector3(0, currentY, 0);
            yield return null;
        }

        joint.targetPosition = new Vector3(0, TargetExtension, 0);

        // Restore the global damper managed by the suspension
        _isWheelRetracting[wheelIndex] = false;
        drive = joint.yDrive;
        drive.positionDamper = _currentDamper;
        joint.yDrive = drive;
    }

    /// <summary>
    /// Applies a damper value to every wheel Y drive, skipping wheels currently playing the jump retraction.
    /// </summary>
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

    /// <summary>
    /// Raises the damper to Damper * HardLandingDamperMultiplier to absorb a hard landing; it then recovers over LandingDamperRecoveryTime.
    /// </summary>
    public void OnHardLanding(float impactSpeed)
    {
        // Target damper for the impact (impactSpeed is not used yet: the boost is a fixed multiplier)
        float targetDamper = Damper * HardLandingDamperMultiplier;
        _currentDamper = Mathf.Max(_currentDamper, targetDamper);
        ApplyDamper(_currentDamper);
    }
}

