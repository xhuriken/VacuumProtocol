using System.Collections.Generic;
using Mirror;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Description: Controls the physics-based movement of the player's arms.
/// Context: Integrates with the PlayerInputHandler to extend individual arms on left/right click.
/// Justification: Applies target forces and alignment torques to the hand (last child of the arm chain) to point in the direction of the player's head, relying on Unity ConfigurableJoints for natural joint behavior rather than rigid inverse kinematics.
/// </summary>
[RequireComponent(typeof(PlayerInputHandler))]
public class PlayerArmsController : NetworkBehaviour
{
    [Header("Arm Hierarchy Root Transforms")]
    [Tooltip("Role: The root transform of the Left Arm.\nUse Case: Hierarchy traversal.\nJustification: Used to calculate the arm's maximum physical reach distance dynamically.")]
    [SerializeField]
    private Transform _leftArmRoot;

    [Tooltip("Role: The root transform of the Right Arm.\nUse Case: Hierarchy traversal.\nJustification: Used to calculate the arm's maximum physical reach distance dynamically.")]
    [SerializeField]
    private Transform _rightArmRoot;

    [Header("Head/Camera Reference")]
    [Tooltip("Role: The transform representing the player's head or look source.\nUse Case: Target direction vector.\nJustification: The arms use this forward vector to determine where to reach.")]
    [SerializeField]
    private Transform _headTransform;

    [Header("Physics Tuning Parameters")]
    [Tooltip("Role: The force multiplier applied to pull the hands toward the target position.\nUse Case: Spring stiffness.\nJustification: High values snap the arm instantly, low values make it sluggish.")]
    [SerializeField]
    private float _extendForce = 350f;

    [Tooltip("Role: Linear damping applied to the hands.\nUse Case: Stabilize extension.\nJustification: Prevents jitter and infinite bouncing when the arm reaches its target.")]
    [SerializeField]
    private float _extendDamping = 12f;

    [Tooltip("Role: Torque multiplier applied to align the hands.\nUse Case: Pointing the nozzle.\nJustification: Ensures the nozzle physically faces where the player is looking, rather than tumbling freely.")]
    [SerializeField]
    private float _alignmentTorque = 20f;

    [Tooltip("Role: Angular damping applied to hand alignment torque.\nUse Case: Stabilize rotation.\nJustification: Prevents the hand from oscillating rapidly around its target angle.")]
    [SerializeField]
    private float _alignmentDamping = 3f;

    [Header("Real-time Tweakable Reaching Settings")]
    [Tooltip("Role: Multiplier applied to the arm's base physical length.\nUse Case: Reach distance limit.\nJustification: Allows tuning the maximum extension distance without changing the bone structure.")]
    [SerializeField]
    private float _reachLengthFactor = 1.0f;

    [Tooltip("Role: Extra forward distance offset applied to the target reach position.\nUse Case: Depth adjustment.\nJustification: Useful for pushing the hands slightly past their physical limit to ensure the joints pull completely taut.")]
    [SerializeField]
    private float _forwardOffset = 0f;

    [Tooltip("Role: Vertical height offset applied to the target reach position.\nUse Case: Height adjustment.\nJustification: Lowers the hands so they don't block the camera view.")]
    [SerializeField]
    private float _verticalOffset = -0.1f;

    [Tooltip("Role: Additional manual rotation offset applied to the hand rotation.\nUse Case: Grip offset.\nJustification: Allows rotating the vacuum nozzle if the bone is not aligned exactly with the forward axis.")]
    [SerializeField]
    private Vector3 _handRotationOffset = Vector3.zero;

    [Header("Shoulder Rotation Settings")]
    [SerializeField]
    [Tooltip("Role: Left shoulder transform.\nUse Case: Rotates Y by 90 when left arm is extended.")]
    private Transform _leftShoulder;

    [SerializeField]
    [Tooltip("Role: Right shoulder transform.\nUse Case: Rotates Y by -90 when right arm is extended.")]
    private Transform _rightShoulder;

    [SerializeField]
    [Tooltip("Role: Duration of the shoulder rotation animation.")]
    private float _shoulderRotateDuration = 0.25f;

    [SerializeField]
    [Tooltip("Role: Ease curve for shoulder rotation (e.g. OutBack for overshoot snappy feel).")]
    private Ease _shoulderEase = Ease.OutBack;

    [Header("Joint Tuning Parameters (Auto-Configured at Runtime)")]
    [SerializeField]
    [Tooltip("Role: Lock twist/roll rotation on X-axis for all joint segments.\nUse Case: Stiffening.\nJustification: Prevents arm segments from spinning on themselves.")]
    private bool _lockAngularX = true;

    [SerializeField]
    [Tooltip("Role: Enable position and rotation projection.\nUse Case: Stiffening.\nJustification: Prevents arms from stretching or separating when moving fast.")]
    private bool _enableJointProjection = true;

    [Header("Shoulder Joint Tuning")]
    [Tooltip("Spring force applied to the shoulder joint when extended/reaching.")]
    [SerializeField] private float _shoulderExtendSpringForce = 1500f;

    [Tooltip("Damping applied to the shoulder joint when extended/reaching.")]
    [SerializeField] private float _shoulderExtendDamping = 20f;

    [Tooltip("Spring force applied to the shoulder joint when at rest.")]
    [SerializeField] private float _shoulderRestSpringForce = 800f;

    [Tooltip("Damping applied to the shoulder joint when at rest (higher damping stabilizes rotation).")]
    [SerializeField] private float _shoulderRestDamping = 40f;

    [Header("Elbow / Wrist Joint Tuning")]
    [Tooltip("Spring force applied to elbow and wrist joints when extended/reaching.")]
    [SerializeField] private float _elbowWristExtendSpringForce = 1500f;

    [Tooltip("Damping applied to elbow and wrist joints when extended/reaching.")]
    [SerializeField] private float _elbowWristExtendDamping = 20f;

    [Tooltip("Spring force applied to elbow and wrist joints when at rest (lower values = looser/more relaxed arms).")]
    [SerializeField] private float _elbowWristRestSpringForce = 150f;

    [Tooltip("Damping applied to elbow and wrist joints when at rest.")]
    [SerializeField] private float _elbowWristRestDamping = 15f;

    [SerializeField]
    [Tooltip("Role: Angular drag applied to all arm Rigidbody components.\nUse Case: Control swing lag.\nJustification: Higher values prevent infinite swinging/floppiness.")]
    private float _armAngularDrag = 15f;

    [Header("Retraction / Rest Physics Settings")]
    [SerializeField]
    [Tooltip("Role: Return animation duration in seconds.")]
    private float _retractTransitionDuration = 0.5f;

    [SerializeField]
    [Tooltip("Role: Strong force applied right after release to pull the arm back to T-pose.")]
    private float _releaseTransientForce = 350f;

    [SerializeField]
    [Tooltip("Role: Loose resting force applied continuously to keep the arm floating above the floor.")]
    private float _releaseRestForce = 40f;

    [SerializeField]
    [Tooltip("Role: Loose resting torque applied to keep the nozzle aligned without being rigid.")]
    private float _releaseRestTorque = 4f;

    [SerializeField]
    [Tooltip("Role: Distance within which the retraction forces/torques smoothly fade to 0.\nUse Case: Eliminates jitter/vibrations and wrist bending.")]
    private float _restFadeDistance = 0.35f;

    [SerializeField]
    [Tooltip("Role: Strict deadzone radius in meters around the rest position where all external forces/torques are cut to 0.\nUse Case: Eliminates hand jitter at rest.")]
    private float _restDeadzone = 0.05f;


    [Header("Debug & Diagnostics")]
    [Tooltip("Role: Enables diagnostic logging for arm movement lifecycle states.\nUse Case: Network tracing.\nJustification: Helps diagnose client-to-server sync issues with arm inputs.")]
    [SerializeField]
    private bool _enableDebugLogs = true;

    // Synchronized variables representing arm extension states across the network
    [SyncVar(hook = nameof(OnLeftArmStateChanged))]
    private bool _isLeftArmExtended = false;

    [SyncVar(hook = nameof(OnRightArmStateChanged))]
    private bool _isRightArmExtended = false;

    // Cached references
    private PlayerInputHandler _input;
    private Transform _leftHand;
    private Rigidbody _leftHandRb;
    private Transform _rightHand;
    private Rigidbody _rightHandRb;

    // Pre-calculated lengths of the arm hierarchies
    private float _leftArmLength = 1.5f;
    private float _rightArmLength = 1.5f;

    // Cached T-pose design-time offsets relative to player root
    private Vector3 _leftHandLocalRestPos;
    private Quaternion _leftHandLocalRestRot;
    private Vector3 _rightHandLocalRestPos;
    private Quaternion _rightHandLocalRestRot;

    // Release timestamps for return animation interpolation
    private float _leftReleaseTime = -100f;
    private float _rightReleaseTime = -100f;

    // Joint caching for dynamic stiffness
    private ConfigurableJoint _leftShoulderJoint;
    private readonly List<ConfigurableJoint> _leftElbowWristJoints = new List<ConfigurableJoint>();

    private ConfigurableJoint _rightShoulderJoint;
    private readonly List<ConfigurableJoint> _rightElbowWristJoints = new List<ConfigurableJoint>();

    private bool _lastLeftExtended;
    private bool _lastRightExtended;

    /// <summary>
    /// Gets the left hand/tip Transform, used for launching items.
    /// </summary>
    public Transform LeftHand => _leftHand;

    /// <summary>
    /// Gets the right hand/tip Transform, used for suction.
    /// </summary>
    public Transform RightHand => _rightHand;

    /// <summary>
    /// Gets a value indicating whether the left arm is extended.
    /// </summary>
    public bool IsLeftArmExtended => _isLeftArmExtended;

    /// <summary>
    /// Gets a value indicating whether the right arm is extended.
    /// </summary>
    public bool IsRightArmExtended => _isRightArmExtended;

    /// <summary>
    /// Gets a value indicating whether the left hand is physically close to its reaching target.
    /// </summary>
    public bool IsLeftHandExtendedPhysically
    {
        get
        {
            if (_leftHand == null || _leftArmRoot == null || !_isLeftArmExtended)
            {
                return false;
            }

            float currentDist = Vector3.Distance(_leftHand.position, _leftArmRoot.position);
            // Consider extended when it reaches at least 80% of target extension
            return currentDist >= (_leftArmLength * _reachLengthFactor * 0.8f);
        }
    }

    /// <summary>
    /// Description: Awake callback. Caches the input handler.
    /// Context: Lifecycle event.
    /// Justification: Guaranteed to run before Start, making input available for initial state setup.
    /// </summary>
    private void Awake()
    {
        _input = GetComponent<PlayerInputHandler>();
    }

    /// <summary>
    /// Description: Start callback. Handles auto-discovery of references and calculates arm properties.
    /// Context: Lifecycle event.
    /// Justification: Discovers the camera and dynamically calculates the total bone length so designers don't have to manually update lengths if they change the 3D model.
    /// </summary>
    private void Start()
    {
        // 1. Resolve look direction source
        if (_headTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>(true);
            if (cam != null)
            {
                _headTransform = cam.transform;
            }
            else
            {
                _headTransform = transform;
                if (_enableDebugLogs)
                {
                    Debug.LogWarning($"[PlayerArmsController] No Camera found in children of '{gameObject.name}'. Falling back to main transform for look directions.");
                }
            }
        }

        // 2. Initialize and cache Left Arm details
        if (_leftArmRoot != null)
        {
            _leftHand = FindLastChild(_leftArmRoot);
            if (_leftHand != null)
            {
                _leftHandRb = _leftHand.GetComponent<Rigidbody>();
                _leftArmLength = CalculateHierarchyLength(_leftArmRoot);
                _leftHandLocalRestPos = transform.InverseTransformPoint(_leftHand.position);
                _leftHandLocalRestRot = Quaternion.Inverse(transform.rotation) * _leftHand.rotation;

                // Cache Left Arm joints
                _leftShoulderJoint = _leftArmRoot.GetComponent<ConfigurableJoint>();
                foreach (ConfigurableJoint joint in _leftArmRoot.GetComponentsInChildren<ConfigurableJoint>(true))
                {
                    if (joint != _leftShoulderJoint)
                    {
                        _leftElbowWristJoints.Add(joint);
                    }
                }

                if (_leftHandRb == null && _enableDebugLogs)
                {
                    Debug.LogWarning($"[PlayerArmsController] Left hand '{_leftHand.name}' does not have a Rigidbody! Physics forces cannot be applied.");
                }
            }
        }
        else if (_enableDebugLogs)
        {
            Debug.LogError($"[PlayerArmsController] Left Arm Root is not assigned on '{gameObject.name}'!");
        }

        // 3. Initialize and cache Right Arm details
        if (_rightArmRoot != null)
        {
            _rightHand = FindLastChild(_rightArmRoot);
            if (_rightHand != null)
            {
                _rightHandRb = _rightHand.GetComponent<Rigidbody>();
                _rightArmLength = CalculateHierarchyLength(_rightArmRoot);
                _rightHandLocalRestPos = transform.InverseTransformPoint(_rightHand.position);
                _rightHandLocalRestRot = Quaternion.Inverse(transform.rotation) * _rightHand.rotation;

                // Cache Right Arm joints
                _rightShoulderJoint = _rightArmRoot.GetComponent<ConfigurableJoint>();
                foreach (ConfigurableJoint joint in _rightArmRoot.GetComponentsInChildren<ConfigurableJoint>(true))
                {
                    if (joint != _rightShoulderJoint)
                    {
                        _rightElbowWristJoints.Add(joint);
                    }
                }

                if (_rightHandRb == null && _enableDebugLogs)
                {
                    Debug.LogWarning($"[PlayerArmsController] Right hand '{_rightHand.name}' does not have a Rigidbody! Physics forces cannot be applied.");
                }
            }
        }
        else if (_enableDebugLogs)
        {
            Debug.LogError($"[PlayerArmsController] Right Arm Root is not assigned on '{gameObject.name}'!");
        }

        if (_enableDebugLogs)
        {
            Debug.Log($"[PlayerArmsController] Initialization complete on '{gameObject.name}'. Left Reach: {_leftArmLength:F2}m, Right Reach: {_rightArmLength:F2}m");
        }

        // Snap shoulders instantly on startup to match initial state
        if (_leftShoulder != null)
        {
            _leftShoulder.localRotation = Quaternion.Euler(0f, _isLeftArmExtended ? 90f : 0f, 0f);
        }
        if (_rightShoulder != null)
        {
            _rightShoulder.localRotation = Quaternion.Euler(0f, _isRightArmExtended ? -90f : 0f, 0f);
        }

        // Centralized collision ignoring is now handled by PlayerCollisionManager on the player root

        // Stiffen and lock joints and configure rig drag values dynamically
        ConfigureArmJointsPhysics();

        // Initialize joint state trackers and apply initial drives
        _lastLeftExtended = _isLeftArmExtended;
        _lastRightExtended = _isRightArmExtended;
        UpdateJointDrives(true, _isLeftArmExtended);
        UpdateJointDrives(false, _isRightArmExtended);
    }

    /// <summary>
    /// Description: Update callback. Polls player input to trigger synced state updates across the server.
    /// Context: Update lifecycle event. Only processed for the local player.
    /// Justification: Converts continuous input holding into discrete state changes that are sent via Mirror Commands only when the state flips, saving bandwidth.
    /// </summary>
    private void Update()
    {
        if (!isLocalPlayer) return;

        bool leftInput = _input.LeftArmPressed;
        bool rightInput = _input.RightArmPressed;

        // If both arms are pressed, it triggers mouth vacuuming and arms should not extend/reach
        if (_input.IsVacuuming)
        {
            leftInput = false;
            rightInput = false;
        }

        // Sync Left Arm click state with the server if a change occurred
        if (leftInput != _isLeftArmExtended)
        {
            CmdSetLeftArmExtended(leftInput);
        }

        // Sync Right Arm click state with the server if a change occurred
        if (rightInput != _isRightArmExtended)
        {
            CmdSetRightArmExtended(rightInput);
        }
    }

    /// <summary>
    /// Description: FixedUpdate callback. Processes dynamic joint physics forces on all clients.
    /// Context: Physics lifecycle event.
    /// Justification: Every player (local or remote) runs local physical simulations of target reaching driven by the synced replication states, providing smooth movement without network jitter.
    /// </summary>
    private void FixedUpdate()
    {
        // 1. Process Left Arm Physics
        if (_leftHandRb != null)
        {
            if (_isLeftArmExtended != _lastLeftExtended)
            {
                UpdateJointDrives(true, _isLeftArmExtended);
                _lastLeftExtended = _isLeftArmExtended;
            }
            ApplyArmPhysicsForces(_leftHandRb, _leftArmLength, true, _isLeftArmExtended);
        }

        // 2. Process Right Arm Physics
        if (_rightHandRb != null)
        {
            if (_isRightArmExtended != _lastRightExtended)
            {
                UpdateJointDrives(false, _isRightArmExtended);
                _lastRightExtended = _isRightArmExtended;
            }
            ApplyArmPhysicsForces(_rightHandRb, _rightArmLength, false, _isRightArmExtended);
        }
    }

    /// <summary>
    /// Description: Computes and applies spring/damping attraction forces and look-alignment torques.
    /// Context: Called by FixedUpdate.
    /// Justification: Pulls the physical joint chain towards the looking target (center line) when extended, or towards the cached T-pose rest targets when retracted.
    /// </summary>
    /// <param name="handRb">The Rigidbody of the last child segment in the arm.</param>
    /// <param name="armLength">The maximum physical length of the arm hierarchy.</param>
    /// <param name="isLeft">True if computing the Left Arm reaching direction, false for the Right Arm.</param>
    /// <param name="isExtended">True if currently aiming/extended, false if retracted in T-pose.</param>
    private void ApplyArmPhysicsForces(Rigidbody handRb, float armLength, bool isLeft, bool isExtended)
    {
        Vector3 targetPosition;
        float currentForce;
        float distanceFactor = 1f;

        if (isExtended)
        {
            Vector3 forward = _headTransform != null ? _headTransform.forward : transform.forward;
            Vector3 up = _headTransform != null ? _headTransform.up : transform.up;

            // Calculate target location in front of the head at tweaked extension range and vertical/forward offsets
            targetPosition = (_headTransform != null ? _headTransform.position : transform.position)
                + forward * (armLength * _reachLengthFactor + _forwardOffset)
                + up * _verticalOffset;

            currentForce = _extendForce;
        }
        else
        {
            // Retracted: target is cached T-pose position in world space
            Vector3 localRestPos = isLeft ? _leftHandLocalRestPos : _rightHandLocalRestPos;
            targetPosition = transform.TransformPoint(localRestPos);

            // Interpolate force: strong right after release, then decays to loose rest force
            float timeSinceRelease = Time.time - (isLeft ? _leftReleaseTime : _rightReleaseTime);
            float t = Mathf.Clamp01(1f - (timeSinceRelease / _retractTransitionDuration));

            currentForce = Mathf.Lerp(_releaseRestForce, _releaseTransientForce, t);

            // Calculate distance factor to fade out the force/torque as we approach rest position
            float dist = Vector3.Distance(handRb.position, targetPosition);
            if (_restFadeDistance > _restDeadzone)
            {
                if (dist <= _restDeadzone)
                {
                    distanceFactor = 0f;
                }
                else
                {
                    distanceFactor = Mathf.Clamp01((dist - _restDeadzone) / (_restFadeDistance - _restDeadzone));
                }
            }
            else if (_restFadeDistance > 0f)
            {
                distanceFactor = Mathf.Clamp01(dist / _restFadeDistance);
            }
        }

        // Calculate proportional attraction force vector
        Vector3 toTarget = targetPosition - handRb.position;
        Vector3 extensionForce = toTarget * currentForce * distanceFactor;

        // Calculate damping force to control oscillation and maintain joint stability
        Vector3 dampingForce = -handRb.linearVelocity * _extendDamping;

        // Apply net force, scaled by the segment mass to normalize behaviors
        Vector3 netForce = (extensionForce + dampingForce) * handRb.mass;
        handRb.AddForce(netForce, ForceMode.Force);

        // --- Rotational Alignment ---
        Quaternion targetRotation;
        float currentTorque;

        if (isExtended)
        {
            Vector3 forward = _headTransform != null ? _headTransform.forward : transform.forward;
            Vector3 up = _headTransform != null ? _headTransform.up : transform.up;
            targetRotation = Quaternion.LookRotation(forward, up) * Quaternion.Euler(_handRotationOffset);
            currentTorque = _alignmentTorque;
        }
        else
        {
            // Retracted: target is design-time T-pose rotation in world space
            Quaternion localRestRot = isLeft ? _leftHandLocalRestRot : _rightHandLocalRestRot;
            targetRotation = transform.rotation * localRestRot;

            float timeSinceRelease = Time.time - (isLeft ? _leftReleaseTime : _rightReleaseTime);
            float t = Mathf.Clamp01(1f - (timeSinceRelease / _retractTransitionDuration));

            // Fade out the torque to 0 at the rest position so wrist joint springs do the alignment and don't bend it upward
            currentTorque = Mathf.Lerp(_releaseRestTorque, _alignmentTorque, t) * distanceFactor;
        }

        Quaternion deltaRotation = targetRotation * Quaternion.Inverse(handRb.rotation);
        deltaRotation.ToAngleAxis(out float angle, out Vector3 axis);

        if (!float.IsNaN(axis.x) && !float.IsNaN(axis.y) && !float.IsNaN(axis.z) && axis.sqrMagnitude > 0.001f)
        {
            // Normalize angle within range [-180, 180]
            if (angle > 180f)
            {
                angle -= 360f;
            }

            // Calculate alignment torque vector
            Vector3 alignmentTorque = axis * (angle * currentTorque * Mathf.Deg2Rad);
            Vector3 rotationalDamping = -handRb.angularVelocity * _alignmentDamping;

            // Apply net torque to orient the hand/nozzle forward
            Vector3 netTorque = (alignmentTorque + rotationalDamping) * handRb.mass;
            handRb.AddTorque(netTorque, ForceMode.Force);
        }
    }


    /// <summary>
    /// Description: Recursively traverses a hierarchy to find the deepest child node that contains a Rigidbody.
    /// Context: Initialization helper.
    /// Justification: Robustly finds the "hand" or "nozzle" without requiring a direct explicit reference, adapting to different rigged models automatically.
    /// </summary>
    /// <param name="parent">The starting root Transform.</param>
    /// <returns>The deepest child Transform with a Rigidbody, or the absolute deepest child.</returns>
    private Transform FindLastChild(Transform parent)
    {
        Transform current = parent;
        Transform lastWithRb = parent.GetComponent<Rigidbody>() != null ? parent : null;

        while (current.childCount > 0)
        {
            current = current.GetChild(0);
            if (current.GetComponent<Rigidbody>() != null)
            {
                lastWithRb = current;
            }
        }
        return lastWithRb != null ? lastWithRb : current;
    }

    /// <summary>
    /// Description: Measures the total length of a single-child linear hierarchy by summing distance gaps.
    /// Context: Initialization helper.
    /// Justification: Pre-calculating the physical max length prevents the extend forces from trying to pull the arm further than its joints allow, which would cause physics stretching or tearing.
    /// </summary>
    /// <param name="root">The root Transform of the arm.</param>
    /// <returns>The sum of segment distances in meters.</returns>
    private float CalculateHierarchyLength(Transform root)
    {
        float totalLength = 0f;
        Transform current = root;

        while (current.childCount > 0)
        {
            Transform next = current.GetChild(0);
            totalLength += Vector3.Distance(current.position, next.position);
            current = next;
        }

        // Return calculated length, or fallback to a default value if hierarchy is trivial
        return totalLength > 0.05f ? totalLength : 1.5f;
    }

    #region Command Network Handlers

    /// <summary>
    /// Server Command. Syncs the left arm extended input state from a client.
    /// </summary>
    [Command]
    private void CmdSetLeftArmExtended(bool extended)
    {
        _isLeftArmExtended = extended;
    }

    /// <summary>
    /// Server Command. Syncs the right arm extended input state from a client.
    /// </summary>
    [Command]
    private void CmdSetRightArmExtended(bool extended)
    {
        _isRightArmExtended = extended;
    }

    #endregion

    #region Sync Hook Callbacks

    /// <summary>
    /// Mirror SyncVar Hook. Triggers diagnostic logging when left arm state replicates.
    /// </summary>
    private void OnLeftArmStateChanged(bool oldState, bool newState)
    {
        AnimateShoulder(true, newState);
        if (oldState && !newState)
        {
            _leftReleaseTime = Time.time;
        }

        if (_enableDebugLogs)
        {
            Debug.Log($"[PlayerArmsController] Left Arm Extension sync: {newState} (Owner: {netId})");
        }
    }

    /// <summary>
    /// Mirror SyncVar Hook. Triggers diagnostic logging when right arm state replicates.
    /// </summary>
    private void OnRightArmStateChanged(bool oldState, bool newState)
    {
        AnimateShoulder(false, newState);
        if (oldState && !newState)
        {
            _rightReleaseTime = Time.time;
        }

        if (_enableDebugLogs)
        {
            Debug.Log($"[PlayerArmsController] Right Arm Extension sync: {newState} (Owner: {netId})");
        }
    }

    /// <summary>
    /// Description: Smoothly animates the shoulder joint rotation on extension state changes.
    /// Context: Triggered by Mirror SyncVar hooks on all clients.
    /// Justification: Uses DOTween to smoothly transition Y rotation to 90/-90 degrees using a Back ease for a snappy overshoot animation feel.
    /// </summary>
    private void AnimateShoulder(bool isLeft, bool extended)
    {
        Transform shoulder = isLeft ? _leftShoulder : _rightShoulder;
        if (shoulder == null) return;

        float targetY = 0f;
        if (extended)
        {
            targetY = isLeft ? 90f : -90f;
        }

        shoulder.DOKill();
        shoulder.DOLocalRotate(new Vector3(0f, targetY, 0f), _shoulderRotateDuration)
            .SetEase(_shoulderEase)
            .SetUpdate(UpdateType.Normal, true);
    }

    /// <summary>
    /// Description: Dynamically stiffens ConfigurableJoints, locks twist rotation on X-axis, and configures Rigidbody drag.
    /// Context: Run at Start.
    /// Justification: Automatically enforces rigidity, eliminates floppiness, locks self-rotation, and configures drag without manual per-joint inspector work.
    /// </summary>
    private void ConfigureArmJointsPhysics()
    {
        // 1. Resolve and configure all arm joints
        ConfigurableJoint[] leftJoints = _leftArmRoot != null ? _leftArmRoot.GetComponentsInChildren<ConfigurableJoint>(true) : new ConfigurableJoint[0];
        ConfigurableJoint[] rightJoints = _rightArmRoot != null ? _rightArmRoot.GetComponentsInChildren<ConfigurableJoint>(true) : new ConfigurableJoint[0];

        var allJoints = new List<ConfigurableJoint>();
        allJoints.AddRange(leftJoints);
        allJoints.AddRange(rightJoints);

        foreach (ConfigurableJoint joint in allJoints)
        {
            if (joint == null) continue;

            // Lock angular X motion to prevent twisting/spinning on itself
            if (_lockAngularX)
            {
                joint.angularXMotion = ConfigurableJointMotion.Locked;
            }

            // Enable joint projection to prevent stretching/separation under fast movement
            if (_enableJointProjection)
            {
                joint.projectionMode = JointProjectionMode.PositionAndRotation;
                joint.projectionDistance = 0.01f;
                joint.projectionAngle = 0.1f;
            }
        }

        // 2. Adjust Rigidbody drag settings to control floppiness
        Rigidbody[] leftRbs = _leftArmRoot != null ? _leftArmRoot.GetComponentsInChildren<Rigidbody>(true) : new Rigidbody[0];
        Rigidbody[] rightRbs = _rightArmRoot != null ? _rightArmRoot.GetComponentsInChildren<Rigidbody>(true) : new Rigidbody[0];

        var allRbs = new List<Rigidbody>();
        allRbs.AddRange(leftRbs);
        allRbs.AddRange(rightRbs);

        foreach (Rigidbody rb in allRbs)
        {
            if (rb == null) continue;

            // Apply high angular drag to prevent wiggling and floppy oscillation
            rb.angularDamping = _armAngularDrag;

            // Increase solver iterations to eliminate micro-vibrations and jitter
            rb.solverIterations = 25;
            rb.solverVelocityIterations = 15;
        }
    }

    /// <summary>
    /// Description: Updates slerp/angular drive spring and damper configurations dynamically on state changes.
    /// Context: Executed on Start and during FixedUpdate when state transitions.
    /// </summary>
    private void UpdateJointDrives(bool isLeft, bool isExtended)
    {
        ConfigurableJoint shoulderJoint = isLeft ? _leftShoulderJoint : _rightShoulderJoint;
        List<ConfigurableJoint> elbowWristJoints = isLeft ? _leftElbowWristJoints : _rightElbowWristJoints;

        float shoulderSpring = isExtended ? _shoulderExtendSpringForce : _shoulderRestSpringForce;
        float shoulderDamp = isExtended ? _shoulderExtendDamping : _shoulderRestDamping;

        float elbowSpring = isExtended ? _elbowWristExtendSpringForce : _elbowWristRestSpringForce;
        float elbowDamp = isExtended ? _elbowWristExtendDamping : _elbowWristRestDamping;

        if (shoulderJoint != null)
        {
            JointDrive drive = new JointDrive
            {
                positionSpring = shoulderSpring,
                positionDamper = shoulderDamp,
                maximumForce = float.MaxValue
            };
            shoulderJoint.slerpDrive = drive;
            shoulderJoint.angularXDrive = drive;
            shoulderJoint.angularYZDrive = drive;
        }

        foreach (ConfigurableJoint joint in elbowWristJoints)
        {
            if (joint != null)
            {
                JointDrive drive = new JointDrive
                {
                    positionSpring = elbowSpring,
                    positionDamper = elbowDamp,
                    maximumForce = float.MaxValue
                };
                joint.slerpDrive = drive;
                joint.angularXDrive = drive;
                joint.angularYZDrive = drive;
            }
        }
    }

    /// <summary>
    /// Helper to check if a transform is nested inside a parent transform.
    /// </summary>
    private bool IsDescendantOf(Transform child, Transform parent)
    {
        if (child == null || parent == null) return false;
        Transform current = child;
        while (current != null)
        {
            if (current == parent) return true;
            current = current.parent;
        }
        return false;
    }

    #endregion
}
