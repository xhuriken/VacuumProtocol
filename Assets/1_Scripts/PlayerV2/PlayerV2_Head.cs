using Mirror;
using UnityEngine;

/// <summary>
/// Description: Drives the physical neck and head motion as a torsion spring.
/// Context: Attached to the player (PlayerV2); works with the Look script, which drives the pitch.
/// Justification: Lets the head be affected by physics (external forces) while still following the player's look direction.
/// </summary>
public class PlayerV2_Head : NetworkBehaviour
{
    [Header("References")]
    [Tooltip("Role: Neck joints, from the base up to the head.\nUse Case: Spreads the pitch evenly across the whole chain.")]
    public ConfigurableJoint[] NeckJoints;

    [Header("Alignment Settings (Physics)")]
    [Tooltip("Role: Enables the alignment torque that keeps the head aligned with the torso yaw and target pitch.")]
    public bool UseAlignmentForce = true;
    [Tooltip("Role: Rotation torque keeping the head aligned with the torso. Same principle as the arms.")]
    public float HeadAlignmentTorque = 1500f;
    [Tooltip("Role: Angular damping applied by the head alignment torque.")]
    public float HeadAlignmentDamping = 100f;

    private PlayerV2_Controller _controller;
    private Rigidbody _headRb;

    [Header("Pitch Scaling")]
    [Tooltip("Role: Fraction of the pitch input followed by the physical head (e.g. 0.7 = 70%).")]
    public float PitchMultiplier = 0.7f;

    [Tooltip("Role: Scaled head pitch target, synchronized so remote clients apply it to their neck joints.")]
    [SyncVar(hook = nameof(OnPitchChanged))]
    public float CurrentTargetPitch;

    private float _lastSentPitch = -999f;

    [Header("Spring Settings (Muscles)")]
    [Tooltip("Role: Rotation spring pushing the head toward the target. Higher = stronger muscle.")]
    public float SpringForce = 5000f;
    [Tooltip("Role: Rotation damping. Higher = less sway.")]
    public float SpringDamper = 500f;
    [Tooltip("Role: Maximum force of the neck joint drives (rotation and vertical).")]
    public float MaxForce = 10000f;

    [Header("Spring Settings (Vertical Boing)")]
    [Tooltip("Role: Vertical (Y) spring force. The lower it is, the more the head bounces (boing-boing).")]
    public float YSpringForce = 1500f;
    [Tooltip("Role: Vertical damping. Keeps the head from bouncing like an endless yo-yo.")]
    public float YSpringDamper = 50f;
    [Tooltip("Role: Maximum distance (in meters) the neck can stretch or compress.")]
    public float YLimit = 0.15f;

    [Header("Limits & Tracking")]
    [Tooltip("Role: Angle limit for each bone (in degrees).")]
    public float JointAngleLimit = 30f;

    private void Awake()
    {
        _controller = GetComponentInParent<PlayerV2_Controller>();
        if (_controller == null) _controller = GetComponent<PlayerV2_Controller>();
    }

    private void Start()
    {
        // Auto-configure the joint springs at startup
        if (NeckJoints != null && NeckJoints.Length > 0)
        {
            JointDrive drive = new JointDrive
            {
                positionSpring = SpringForce,
                positionDamper = SpringDamper,
                maximumForce = MaxForce
            };

            JointDrive yDrive = new JointDrive
            {
                positionSpring = YSpringForce,
                positionDamper = YSpringDamper,
                maximumForce = MaxForce
            };

            SoftJointLimit linearLimit = new SoftJointLimit { limit = YLimit };
            SoftJointLimit limit = new SoftJointLimit { limit = JointAngleLimit };

            _headRb = NeckJoints[NeckJoints.Length - 1].GetComponent<Rigidbody>();

            foreach (var joint in NeckJoints)
            {
                if (joint != null)
                {
                    // CRITICAL FIX: keeps the head/neck from pulling on the Torso and making the robot drift
                    joint.connectedMassScale = 0.00001f;

                    // SlerpDrive gives spring behaviour on all 3 axes
                    joint.rotationDriveMode = RotationDriveMode.Slerp;
                    joint.slerpDrive = drive;

                    // Vertical spring setup (local Y axis)
                    joint.yDrive = yDrive;
                    joint.linearLimit = linearLimit;

                    // Unlock the Y axis to allow the bounce (boing-boing) and crouch
                    joint.xMotion = ConfigurableJointMotion.Locked;
                    joint.yMotion = ConfigurableJointMotion.Limited;
                    joint.zMotion = ConfigurableJointMotion.Locked;

                    // Limit rotations so the neck does not twist in every direction
                    joint.angularXMotion = ConfigurableJointMotion.Limited;
                    joint.angularYMotion = ConfigurableJointMotion.Locked;
                    joint.angularZMotion = ConfigurableJointMotion.Locked;

                    joint.lowAngularXLimit = new SoftJointLimit { limit = -JointAngleLimit };
                    joint.highAngularXLimit = limit;

                    // Keep the two connected bones from pushing each other apart (can cause stretching)
                    joint.enableCollision = false;
                }
            }
        }
    }

    [Command]
    private void CmdSetPitch(float targetPitch)
    {
        CurrentTargetPitch = targetPitch;
    }

    /// <summary>
    /// Spreads the desired pitch evenly across all neck bones (owner only: predicted locally, then sent to the server).
    /// </summary>
    /// <param name="targetPitch">Total desired pitch angle (up/down).</param>
    public void SetTargetPitch(float targetPitch)
    {
        if (NeckJoints == null || NeckJoints.Length == 0) return;

        float newPitch = targetPitch * PitchMultiplier;

        if (isOwned)
        {
            // Apply locally right away (prediction)
            CurrentTargetPitch = newPitch;
            ApplyPitchToJoints(newPitch);

            // Only send the command when the change is noticeable (0.5 degree)
            if (Mathf.Abs(newPitch - _lastSentPitch) > 0.5f)
            {
                CmdSetPitch(newPitch);
                _lastSentPitch = newPitch;
            }
        }
    }

    private void OnPitchChanged(float oldPitch, float newPitch)
    {
        if (!isOwned)
        {
            ApplyPitchToJoints(newPitch);
        }
    }

    private void ApplyPitchToJoints(float pitch)
    {
        if (NeckJoints == null || NeckJoints.Length == 0) return;

        // Divide the angle by the joint count for a smooth curve
        float pitchPerJoint = pitch / NeckJoints.Length;

        // Warning: in Unity, ConfigurableJoint.targetRotation is inverted relative to the local frame.
        // The sign is flipped here to correct the mouse motion.
        Quaternion targetRot = Quaternion.Euler(-pitchPerJoint, 0f, 0f);

        foreach (var joint in NeckJoints)
        {
            if (joint != null)
            {
                joint.targetRotation = targetRot;
            }
        }
    }

    /// <summary>
    /// Sets the rest position of the vertical spring on every joint (used for crouching).
    /// </summary>
    /// <param name="offsetY">Total desired vertical offset (e.g. -0.3 m to lower the head).</param>
    public void SetHeadHeightOffset(float offsetY)
    {
        if (NeckJoints == null || NeckJoints.Length == 0) return;

        // Spread the offset evenly across every joint
        float offsetPerJoint = offsetY / NeckJoints.Length;
        Vector3 targetPos = new Vector3(0f, offsetPerJoint, 0f);

        foreach (var joint in NeckJoints)
        {
            if (joint != null)
            {
                joint.targetPosition = targetPos;
            }
        }
    }

    private void FixedUpdate()
    {
        if (!isOwned || !UseAlignmentForce || _headRb == null || _controller == null || _controller.TorsoRigidbody == null) return;

        // Target: Torso rotation (yaw) + desired pitch (X)
        Quaternion torsoRot = _controller.TorsoRigidbody.rotation;
        Quaternion targetRotation = torsoRot * Quaternion.Euler(CurrentTargetPitch, 0f, 0f);

        Quaternion deltaRotation = targetRotation * Quaternion.Inverse(_headRb.rotation);
        deltaRotation.ToAngleAxis(out float angle, out Vector3 axis);

        if (!float.IsNaN(axis.x) && !float.IsNaN(axis.y) && !float.IsNaN(axis.z) && axis.sqrMagnitude > 0.001f)
        {
            if (angle > 180f) angle -= 360f;

            Vector3 alignmentTorque = axis * (angle * HeadAlignmentTorque * Mathf.Deg2Rad);
            Vector3 rotationalDamping = -_headRb.angularVelocity * HeadAlignmentDamping;
            Vector3 netTorque = (alignmentTorque + rotationalDamping) * _headRb.mass;

            _headRb.AddTorque(netTorque, ForceMode.Force);
        }
    }
}

