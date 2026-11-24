using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Mirror;
using UnityEngine;

/// <summary>
/// Description: Physics-driven arm control for Player_V2.
/// Context: Attached to Player_V2 (must sit on the same GameObject as PlayerV2_Controller).
/// Justification: Applies forces and torques along the arm chain and on the hand (last bone) to stretch the physical arm toward the look direction.
/// </summary>
[RequireComponent(typeof(PlayerV2_Controller))]
[RequireComponent(typeof(PlayerInputHandler))]
public class PlayerV2_Arms : NetworkBehaviour
{
    [Header("Physics Tuning Parameters")]
    [Tooltip("Role: Height of the arc (curve) formed by the extended arm, for a more organic look.")]
    public float ArcHeight = 1f;
    [Tooltip("Role: Spring force pulling each arm segment toward its target point on the curve.")]
    public float ExtendForce = 350f;
    [Tooltip("Role: Linear damping applied to arm segments while extending.")]
    public float ExtendDamping = 12f;
    [Tooltip("Role: Torque strength aligning the hand rotation with the look direction.")]
    public float AlignmentTorque = 20f;
    [Tooltip("Role: Angular damping applied to the hand during rotation alignment.")]
    public float AlignmentDamping = 3f;

    [Header("Reach Settings")]
    [Tooltip("Role: Multiplier on the measured arm length used to place the reach target.")]
    public float ReachLengthFactor = 1.0f;
    [Tooltip("Role: Extra distance added to the reach target along the look direction.")]
    public float ForwardOffset = 0f;
    [Tooltip("Role: Vertical offset of the reach target along the camera up axis.")]
    public float VerticalOffset = -0.1f;
    [Tooltip("Role: Euler offset applied to the hand target rotation.")]
    public Vector3 HandRotationOffset = Vector3.zero;

    [Header("Animation")]
    [Tooltip("Role: Duration of the shoulder rotation.\nUse Case: Animation.")]
    public float ShoulderRotateDuration = 0.25f; // Kept short to feel snappier and stutter less against the physics
    [Tooltip("Role: Easing type for the shoulder animation.\nUse Case: Smoothness.")]
    public Ease ShoulderEase = Ease.OutQuad; // OutQuad instead of OutBack to avoid the overshoot bounce (jitter)

    [Header("Crouch Retraction")]
    [Tooltip("Role: Number of segments to skip at the start of the arm (0 = the shoulder is the first affected, 1 = the first joint is skipped).")]
    public int RetractedSegmentsOffset = 1;
    [Tooltip("Role: Number of segments that retract after the offset.")]
    public int RetractedSegmentsCount = 2;
    [Tooltip("Role: Duration of the retraction animation, in seconds.")]
    public float RetractionDuration = 0.25f;

    [Header("Joint Tuning (Auto-Configured)")]
    [Tooltip("Role: Locks angular X motion on every arm joint except the hand.")]
    public bool LockAngularX = false;
    [Tooltip("Role: Enables joint projection (position and rotation) to keep the chain from separating.")]
    public bool EnableJointProjection = true;

    [Tooltip("Role: Shoulder joint drive spring while the arm is extended.")]
    public float ShoulderExtendSpringForce = 1500f;
    [Tooltip("Role: Shoulder joint drive damper while the arm is extended.")]
    public float ShoulderExtendDamping = 20f;
    [Tooltip("Role: Shoulder joint drive spring while the arm is at rest.")]
    public float ShoulderRestSpringForce = 800f;
    [Tooltip("Role: Shoulder joint drive damper while the arm is at rest.")]
    public float ShoulderRestDamping = 40f;

    [Tooltip("Role: Elbow/wrist joint drive spring while the arm is extended.")]
    public float ElbowWristExtendSpringForce = 1500f;
    [Tooltip("Role: Elbow/wrist joint drive damper while the arm is extended.")]
    public float ElbowWristExtendDamping = 20f;
    [Tooltip("Role: Elbow/wrist joint drive spring while the arm is at rest.")]
    public float ElbowWristRestSpringForce = 150f;
    [Tooltip("Role: Elbow/wrist joint drive damper while the arm is at rest.")]
    public float ElbowWristRestDamping = 15f;

    [Tooltip("Role: Angular damping applied to every arm rigidbody.")]
    public float ArmAngularDrag = 15f;

    [Header("Retraction / Rest Physics")]
    [Tooltip("Role: When enabled, no force is applied to the hand at rest; it hangs freely under gravity.")]
    public bool FreeHangAtRest = true;

    // Synchronized Variables
    [Header("Network Sync")]
    [SyncVar(hook = nameof(OnLeftArmStateChanged))]
    private bool _isLeftArmExtended = false;

    [SyncVar(hook = nameof(OnRightArmStateChanged))]
    private bool _isRightArmExtended = false;

    private bool _isRetracted = false;
    private Coroutine _leftRetractionCoroutine;
    private Coroutine _rightRetractionCoroutine;
    private Dictionary<ConfigurableJoint, Vector3> _originalConnectedAnchors = new Dictionary<ConfigurableJoint, Vector3>();

    // References
    private PlayerV2_Controller _controller;
    private PlayerInputHandler _input;

    private Transform _leftHand;
    private Rigidbody _leftHandRb;
    private Transform _rightHand;
    private Rigidbody _rightHandRb;

    private List<Rigidbody> _leftArmRbs = new List<Rigidbody>();
    private List<Rigidbody> _rightArmRbs = new List<Rigidbody>();

    // Lengths
    private float _leftArmLength = 1.5f;
    private float _rightArmLength = 1.5f;

    // Release timestamps
    private float _leftReleaseTime = -100f;
    private float _rightReleaseTime = -100f;

    // Joints
    private ConfigurableJoint _leftShoulderJoint;
    private readonly List<ConfigurableJoint> _leftElbowWristJoints = new List<ConfigurableJoint>();

    private ConfigurableJoint _rightShoulderJoint;
    private readonly List<ConfigurableJoint> _rightElbowWristJoints = new List<ConfigurableJoint>();

    private bool _lastLeftExtended;
    private bool _lastRightExtended;

    /// <summary>Last bone (hand) of the left arm chain, resolved at Start.</summary>
    public Transform LeftHand => _leftHand;
    /// <summary>Last bone (hand) of the right arm chain, resolved at Start.</summary>
    public Transform RightHand => _rightHand;
    /// <summary>True while the left arm extension input is active (network synchronized).</summary>
    public bool IsLeftArmExtended => _isLeftArmExtended;
    /// <summary>True while the right arm extension input is active (network synchronized).</summary>
    public bool IsRightArmExtended => _isRightArmExtended;

    /// <summary>
    /// True when the left arm is extended and the hand actually reached at least 80% of the target reach distance.
    /// </summary>
    public bool IsLeftHandExtendedPhysically
    {
        get
        {
            if (_leftHand == null || _controller.LeftArmRoot == null || !_isLeftArmExtended) return false;
            float currentDist = Vector3.Distance(_leftHand.position, _controller.LeftArmRoot.position);
            return currentDist >= (_leftArmLength * ReachLengthFactor * 0.8f);
        }
    }

    private void Awake()
    {
        _controller = GetComponent<PlayerV2_Controller>();
        _input = GetComponent<PlayerInputHandler>();
    }

    private void Start()
    {
        // 1. Resolve and cache joints
        if (_controller.LeftArmRoot != null)
        {
            // GetComponentInChildren in case the root is set to Shoulder (which has no joint) instead of Arm.Base
            _leftShoulderJoint = _controller.LeftArmRoot.GetComponentInChildren<ConfigurableJoint>();
            if (_leftShoulderJoint != null)
            {
                foreach (var joint in _controller.LeftArmRoot.GetComponentsInChildren<ConfigurableJoint>(true))
                {
                    if (joint != _leftShoulderJoint) _leftElbowWristJoints.Add(joint);
                }
            }
        }

        if (_controller.RightArmRoot != null)
        {
            // GetComponentInChildren in case the root is set to Shoulder (which has no joint) instead of Arm.Base
            _rightShoulderJoint = _controller.RightArmRoot.GetComponentInChildren<ConfigurableJoint>();
            if (_rightShoulderJoint != null)
            {
                foreach (var joint in _controller.RightArmRoot.GetComponentsInChildren<ConfigurableJoint>(true))
                {
                    if (joint != _rightShoulderJoint) _rightElbowWristJoints.Add(joint);
                }
            }
        }

        // Left arm initialization
        if (_controller.LeftArmRoot != null)
        {
            _leftArmRbs.AddRange(_controller.LeftArmRoot.GetComponentsInChildren<Rigidbody>());
            _leftHand = FindLastChild(_controller.LeftArmRoot);
            if (_leftHand != null)
            {
                _leftHandRb = _leftHand.GetComponent<Rigidbody>();
                _leftArmLength = CalculateHierarchyLength(_controller.LeftArmRoot, _leftHand);
            }
        }

        // Right arm initialization
        if (_controller.RightArmRoot != null)
        {
            _rightArmRbs.AddRange(_controller.RightArmRoot.GetComponentsInChildren<Rigidbody>());
            _rightHand = FindLastChild(_controller.RightArmRoot);
            if (_rightHand != null)
            {
                _rightHandRb = _rightHand.GetComponent<Rigidbody>();
                _rightArmLength = CalculateHierarchyLength(_controller.RightArmRoot, _rightHand);
            }
        }

        // Snap shoulders to their initial rotation
        if (_controller.LeftShoulder != null)
            _controller.LeftShoulder.localRotation = Quaternion.Euler(0f, _isLeftArmExtended ? 90f : 0f, 0f);

        if (_controller.RightShoulder != null)
            _controller.RightShoulder.localRotation = Quaternion.Euler(0f, _isRightArmExtended ? -90f : 0f, 0f);

        ConfigureArmJointsPhysics();

        _lastLeftExtended = _isLeftArmExtended;
        _lastRightExtended = _isRightArmExtended;
        UpdateJointDrives(true, _isLeftArmExtended);
        UpdateJointDrives(false, _isRightArmExtended);
    }

    private void Update()
    {
        if (!isOwned) return;

        bool leftInput = _input.LeftArmPressed;
        bool rightInput = _input.RightArmPressed;

        if (_input.IsVacuuming)
        {
            leftInput = false;
            rightInput = false;
        }

        if (leftInput != _isLeftArmExtended)
        {
            OnLeftArmStateChanged(_isLeftArmExtended, leftInput);
            _isLeftArmExtended = leftInput;
            CmdSetLeftArmExtended(leftInput);
        }

        if (rightInput != _isRightArmExtended)
        {
            OnRightArmStateChanged(_isRightArmExtended, rightInput);
            _isRightArmExtended = rightInput;
            CmdSetRightArmExtended(rightInput);
        }
    }

    private void FixedUpdate()
    {
        // Left arm
        if (_leftArmRbs.Count > 0 && _leftHandRb != null)
        {
            if (_isLeftArmExtended != _lastLeftExtended)
            {
                UpdateJointDrives(true, _isLeftArmExtended);
                _lastLeftExtended = _isLeftArmExtended;
            }
            ApplyArmPhysicsForces(_leftArmRbs, _leftHandRb, _leftArmLength, true, _isLeftArmExtended);
        }

        // Right arm
        if (_rightArmRbs.Count > 0 && _rightHandRb != null)
        {
            if (_isRightArmExtended != _lastRightExtended)
            {
                UpdateJointDrives(false, _isRightArmExtended);
                _lastRightExtended = _isRightArmExtended;
            }
            ApplyArmPhysicsForces(_rightArmRbs, _rightHandRb, _rightArmLength, false, _isRightArmExtended);
        }
    }

    private void ApplyArmPhysicsForces(List<Rigidbody> armRbs, Rigidbody handRb, float armLength, bool isLeft, bool isExtended)
    {
        if (!isExtended && FreeHangAtRest)
        {
            // No external force: the arm hangs under gravity and joint stiffness only.
            return;
        }

        Transform headTrans = _controller.CameraTransform != null ? _controller.CameraTransform : transform;

        if (isExtended)
        {
            Vector3 forward = headTrans.forward;
            Vector3 up = headTrans.up;

            Vector3 finalTargetPosition = headTrans.position
                + forward * (armLength * ReachLengthFactor + ForwardOffset)
                + up * VerticalOffset;

            Vector3 armRootPos = armRbs[0].position; // Shoulder base

            // Attraction force spread over every segment for a clean curve with no jerks
            for (int i = 0; i < armRbs.Count; i++)
            {
                Rigidbody rb = armRbs[i];
                if (rb == null) continue;

                float weight = (float)(i + 1) / armRbs.Count; // 0.x to 1.0 (hand)

                // Retraction: folded segments are skipped
                if (_isRetracted)
                {
                    int retractedEnd = Mathf.Min(RetractedSegmentsOffset + RetractedSegmentsCount, armRbs.Count);
                    if (i < retractedEnd)
                    {
                        weight = 0f;
                    }
                    else
                    {
                        float range = armRbs.Count - retractedEnd;
                        weight = range > 0 ? Mathf.Lerp(0.2f, 1f, (float)(i - retractedEnd + 1) / range) : 1f;
                    }
                }

                if (weight > 0f)
                {
                    // FIX NATURAL PITCH CURVE (cubic Bezier):
                    // To keep the base from taking the whole rotation (right angle) when looking up/down,
                    // the curve starts straight ahead of the torso (horizontal) and its end aligns with the camera.

                    Vector3 torsoForward = _controller.TorsoRigidbody.transform.forward;

                    Vector3 p0 = armRootPos;
                    // Control point 1 pushes along the torso axis (creates the vertical / pitch curve)
                    Vector3 p1 = p0 + torsoForward * (armLength * ArcHeight);
                    // Control point 2 pulls back from the target along the look axis
                    Vector3 p2 = finalTargetPosition - forward * (armLength * ArcHeight);
                    Vector3 p3 = finalTargetPosition;

                    float t = weight;
                    float u = 1f - t;
                    float tt = t * t;
                    float uu = u * u;
                    float uuu = uu * u;
                    float ttt = tt * t;

                    Vector3 segmentTargetPos = uuu * p0 + 3f * uu * t * p1 + 3f * u * tt * p2 + ttt * p3;

                    Vector3 toTarget = segmentTargetPos - rb.position;
                    Vector3 extensionForce = toTarget * (ExtendForce * weight);
                    Vector3 dampingForce = -rb.linearVelocity * (ExtendDamping * weight);
                    Vector3 netForce = (extensionForce + dampingForce) * rb.mass;
                    rb.AddForce(netForce, ForceMode.Force);
                }
            }

            // Hand rotation alignment
            Quaternion targetRotation = Quaternion.LookRotation(forward, up) * Quaternion.Euler(HandRotationOffset);
            Quaternion deltaRotation = targetRotation * Quaternion.Inverse(handRb.rotation);
            deltaRotation.ToAngleAxis(out float angle, out Vector3 axis);

            if (!float.IsNaN(axis.x) && !float.IsNaN(axis.y) && !float.IsNaN(axis.z) && axis.sqrMagnitude > 0.001f)
            {
                if (angle > 180f) angle -= 360f;
                Vector3 alignmentTorque = axis * (angle * AlignmentTorque * Mathf.Deg2Rad);
                Vector3 rotationalDamping = -handRb.angularVelocity * AlignmentDamping;
                Vector3 netTorque = (alignmentTorque + rotationalDamping) * handRb.mass;
                handRb.AddTorque(netTorque, ForceMode.Force);
            }
        }
    }

    private Transform FindLastChild(Transform parent)
    {
        Transform current = parent;
        Transform lastWithRb = parent.GetComponent<Rigidbody>() != null ? parent : null;

        while (current.childCount > 0)
        {
            current = current.GetChild(0);
            if (current.GetComponent<Rigidbody>() != null) lastWithRb = current;
        }
        return lastWithRb != null ? lastWithRb : current;
    }

    private float CalculateHierarchyLength(Transform root, Transform lastBone)
    {
        float totalLength = 0f;
        Transform current = root;

        while (current != null && current != lastBone && current.childCount > 0)
        {
            // Find the child leading to lastBone (fall back to the first child if none is found)
            Transform next = current.GetChild(0);
            foreach (Transform child in current)
            {
                if (IsParentOf(child, lastBone) || child == lastBone)
                {
                    next = child;
                    break;
                }
            }

            totalLength += Vector3.Distance(current.position, next.position);
            current = next;
        }
        return totalLength > 0.05f ? totalLength : 1.5f;
    }

    private bool IsParentOf(Transform parent, Transform child)
    {
        if (child == null) return false;
        Transform current = child.parent;
        while (current != null)
        {
            if (current == parent) return true;
            current = current.parent;
        }
        return false;
    }

    #region Mirror Commands & Hooks

    [Command]
    private void CmdSetLeftArmExtended(bool extended) => _isLeftArmExtended = extended;

    [Command]
    private void CmdSetRightArmExtended(bool extended) => _isRightArmExtended = extended;

    private void OnLeftArmStateChanged(bool oldState, bool newState)
    {
        AnimateShoulder(true, newState);
        if (oldState && !newState) _leftReleaseTime = Time.time;
    }

    private void OnRightArmStateChanged(bool oldState, bool newState)
    {
        AnimateShoulder(false, newState);
        if (oldState && !newState) _rightReleaseTime = Time.time;
    }

    private void AnimateShoulder(bool isLeft, bool extended)
    {
        Transform shoulder = isLeft ? _controller.LeftShoulder : _controller.RightShoulder;
        if (shoulder == null) return;

        float targetY = 0f;
        if (extended) targetY = isLeft ? 90f : -90f;

        shoulder.DOKill();
        shoulder.DOLocalRotate(new Vector3(0f, targetY, 0f), ShoulderRotateDuration)
            .SetEase(ShoulderEase)
            .SetUpdate(UpdateType.Normal, true);
    }

    #endregion

    #region Physics Configuration

    private void ConfigureArmJointsPhysics()
    {
        List<ConfigurableJoint> allJoints = new List<ConfigurableJoint>();
        if (_controller.LeftArmRoot != null) allJoints.AddRange(_controller.LeftArmRoot.GetComponentsInChildren<ConfigurableJoint>(true));
        if (_controller.RightArmRoot != null) allJoints.AddRange(_controller.RightArmRoot.GetComponentsInChildren<ConfigurableJoint>(true));

        foreach (var joint in allJoints)
        {
            if (joint == null) continue;

            // Save the correctly computed connectedAnchor before disabling auto-configure
            if (joint.autoConfigureConnectedAnchor)
            {
                // Compute the exact connectedAnchor manually from the initial pose to avoid spawn glitches
                Vector3 worldAnchor = joint.transform.TransformPoint(joint.anchor);
                Vector3 connectedAnchor = joint.connectedBody != null ? joint.connectedBody.transform.InverseTransformPoint(worldAnchor) : worldAnchor;

                joint.autoConfigureConnectedAnchor = false;
                joint.connectedAnchor = connectedAnchor;
                _originalConnectedAnchors[joint] = connectedAnchor;
            }
            else
            {
                _originalConnectedAnchors[joint] = joint.connectedAnchor;
            }

            // CRITICAL FIX: keep the arms from pushing/rotating the Torso!
            // With a tiny connectedMassScale on the shoulder, the Unity solver treats
            // the Torso as infinitely heavy relative to the arm.
            // The arm can still move, but it can NEVER move the Torso.
            if (joint == _leftShoulderJoint || joint == _rightShoulderJoint)
            {
                joint.connectedMassScale = 0.00001f;
            }

            if (LockAngularX) 
            {
                bool isHand = false;
                if (_leftElbowWristJoints.Count > 0 && joint == _leftElbowWristJoints[_leftElbowWristJoints.Count - 1]) isHand = true;
                if (_rightElbowWristJoints.Count > 0 && joint == _rightElbowWristJoints[_rightElbowWristJoints.Count - 1]) isHand = true;

                if (!isHand) joint.angularXMotion = ConfigurableJointMotion.Locked;
                else joint.angularXMotion = ConfigurableJointMotion.Free;
            }

            if (EnableJointProjection)
            {
                joint.projectionMode = JointProjectionMode.PositionAndRotation;
                // FIX: values that are too low cause infinite teleport forces (physics explosion)
                joint.projectionDistance = 0.1f; // 10 cm tolerance (Unity default)
                joint.projectionAngle = 180f; // 180 degrees tolerance (Unity default)
            }
        }

        List<Rigidbody> allRbs = new List<Rigidbody>();
        if (_controller.LeftArmRoot != null) allRbs.AddRange(_controller.LeftArmRoot.GetComponentsInChildren<Rigidbody>(true));
        if (_controller.RightArmRoot != null) allRbs.AddRange(_controller.RightArmRoot.GetComponentsInChildren<Rigidbody>(true));

        foreach (var rb in allRbs)
        {
            if (rb == null) continue;
            rb.angularDamping = ArmAngularDrag;
            rb.solverIterations = 25;
            rb.solverVelocityIterations = 15;
        }

        // --- Zero friction setup ---
        PhysicsMaterial zeroFrictionMaterial = new PhysicsMaterial("ZeroFrictionArmMaterial");
        zeroFrictionMaterial.dynamicFriction = 0f;
        zeroFrictionMaterial.staticFriction = 0f;
        zeroFrictionMaterial.frictionCombine = PhysicsMaterialCombine.Minimum;

        List<Collider> allColliders = new List<Collider>();
        if (_controller.LeftArmRoot != null) allColliders.AddRange(_controller.LeftArmRoot.GetComponentsInChildren<Collider>(true));
        if (_controller.RightArmRoot != null) allColliders.AddRange(_controller.RightArmRoot.GetComponentsInChildren<Collider>(true));

        foreach (var col in allColliders)
        {
            if (col != null) col.sharedMaterial = zeroFrictionMaterial;
        }
    }

    private void UpdateJointDrives(bool isLeft, bool isExtended)
    {
        ConfigurableJoint shoulderJoint = isLeft ? _leftShoulderJoint : _rightShoulderJoint;
        List<ConfigurableJoint> elbowWristJoints = isLeft ? _leftElbowWristJoints : _rightElbowWristJoints;

        float shoulderSpring = isExtended ? ShoulderExtendSpringForce : ShoulderRestSpringForce;
        float shoulderDamp = isExtended ? ShoulderExtendDamping : ShoulderRestDamping;

        float elbowSpring = isExtended ? ElbowWristExtendSpringForce : ElbowWristRestSpringForce;
        float elbowDamp = isExtended ? ElbowWristExtendDamping : ElbowWristRestDamping;

        if (shoulderJoint != null)
        {
            JointDrive drive = new JointDrive { positionSpring = shoulderSpring, positionDamper = shoulderDamp, maximumForce = float.MaxValue };
            shoulderJoint.slerpDrive = drive;
            shoulderJoint.angularXDrive = drive;
            shoulderJoint.angularYZDrive = drive;
        }

        foreach (var joint in elbowWristJoints)
        {
            if (joint != null)
            {
                JointDrive drive = new JointDrive { positionSpring = elbowSpring, positionDamper = elbowDamp, maximumForce = float.MaxValue };
                joint.slerpDrive = drive;
                joint.angularXDrive = drive;
                joint.angularYZDrive = drive;
            }
        }
    }

    #endregion

    #region Crouch Retraction

    /// <summary>
    /// Retracts (crouch) or restores both arms by animating the connected anchors of the configured segments.
    /// </summary>
    public void SetArmRetraction(bool isRetracted)
    {
        if (_isRetracted == isRetracted) return;
        _isRetracted = isRetracted;

        if (_leftRetractionCoroutine != null) StopCoroutine(_leftRetractionCoroutine);
        if (_rightRetractionCoroutine != null) StopCoroutine(_rightRetractionCoroutine);

        _leftRetractionCoroutine = StartCoroutine(AnimateArmRetraction(_leftElbowWristJoints, isRetracted));
        _rightRetractionCoroutine = StartCoroutine(AnimateArmRetraction(_rightElbowWristJoints, isRetracted));
    }

    private IEnumerator AnimateArmRetraction(List<ConfigurableJoint> joints, bool isRetracted)
    {
        // Bounds safety
        int startIndex = Mathf.Clamp(RetractedSegmentsOffset, 0, joints.Count);
        int endIndex = Mathf.Min(startIndex + RetractedSegmentsCount, joints.Count);
        int countToAnimate = endIndex - startIndex;

        if (countToAnimate <= 0) yield break;

        float time = 0f;

        // Store start anchors for interpolation
        Vector3[] startAnchors = new Vector3[countToAnimate];
        Vector3[] targetAnchors = new Vector3[countToAnimate];

        for (int i = 0; i < countToAnimate; i++)
        {
            var joint = joints[startIndex + i];
            if (joint != null && _originalConnectedAnchors.ContainsKey(joint))
            {
                startAnchors[i] = joint.connectedAnchor;
                targetAnchors[i] = isRetracted ? Vector3.zero : _originalConnectedAnchors[joint];
            }
        }

        while (time < RetractionDuration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / RetractionDuration);

            // Basic easing (SmoothStep)
            float smoothT = t * t * (3f - 2f * t);

            for (int i = 0; i < countToAnimate; i++)
            {
                var joint = joints[startIndex + i];
                if (joint != null)
                {
                    joint.connectedAnchor = Vector3.Lerp(startAnchors[i], targetAnchors[i], smoothT);
                }
            }
            yield return null;
        }

        // Make sure the final value is reached exactly
        for (int i = 0; i < countToAnimate; i++)
        {
            var joint = joints[startIndex + i];
            if (joint != null)
            {
                joint.connectedAnchor = targetAnchors[i];
            }
        }
    }

    #endregion
}

