using Mirror;
using UnityEngine;

/// <summary>
/// Controls the physics-based movement of the player's arms.
/// Integrates with the PlayerInputHandler to extend individual arms on left/right click.
/// Applies target forces and alignment torques to the hand (last child of the arm chain)
/// to point in the direction of the player's head, relying on Unity ConfigurableJoints for natural joint behavior.
/// </summary>
[RequireComponent(typeof(PlayerInputHandler))]
public class PlayerArmsController : NetworkBehaviour
{
    [Header("Arm Hierarchy Root Transforms")]
    [Tooltip("The root transform of the Left Arm.")]
    [SerializeField]
    private Transform _leftArmRoot;

    [Tooltip("The root transform of the Right Arm.")]
    [SerializeField]
    private Transform _rightArmRoot;

    [Header("Head/Camera Reference")]
    [Tooltip("The transform representing the player's head or look source. Auto-discovered if null.")]
    [SerializeField]
    private Transform _headTransform;

    [Header("Physics Tuning Parameters")]
    [Tooltip("The force multiplier applied to pull the hands toward the target position.")]
    [SerializeField]
    private float _extendForce = 350f;

    [Tooltip("Linear damping applied to the hands to stabilize extension and prevent jitter.")]
    [SerializeField]
    private float _extendDamping = 12f;

    [Tooltip("Torque multiplier applied to align the hands with the head looking direction.")]
    [SerializeField]
    private float _alignmentTorque = 20f;

    [Tooltip("Angular damping applied to hand alignment torque to prevent oscillation.")]
    [SerializeField]
    private float _alignmentDamping = 3f;

    [Header("Real-time Tweakable Reaching Settings")]
    [Tooltip("Multiplier applied to the arm's base physical length to determine reach distance.")]
    [SerializeField]
    private float _reachLengthFactor = 1.0f;

    [Tooltip("Extra forward distance offset applied to the target reach position.")]
    [SerializeField]
    private float _forwardOffset = 0f;

    [Tooltip("Vertical height offset applied to the target reach position.")]
    [SerializeField]
    private float _verticalOffset = -0.1f;

    [Tooltip("Additional manual rotation offset (Pitch, Yaw, Roll) applied to the hand rotation.")]
    [SerializeField]
    private Vector3 _handRotationOffset = Vector3.zero;


    [Header("Debug & Diagnostics")]
    [Tooltip("Enables diagnostic logging for arm movement lifecycle states.")]
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
    /// Awake callback. Caches the input handler and registers core components.
    /// </summary>
    private void Awake()
    {
        _input = GetComponent<PlayerInputHandler>();
    }

    /// <summary>
    /// Start callback. Handles auto-discovery of references and calculates arm properties.
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
    }

    /// <summary>
    /// Update callback. Polls player input to trigger synced state updates across the server.
    /// Only processed for the local player who owns the input authority.
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
    /// FixedUpdate callback. Processes dynamic joint physics forces on all clients.
    /// Every player (local or remote) runs local physical simulations of target reaching
    /// driven by the synced replication states.
    /// </summary>
    private void FixedUpdate()
    {
        // 1. Process Left Arm Physics Reach
        if (_isLeftArmExtended && _leftHandRb != null && _headTransform != null)
        {
            ApplyArmReachingForces(_leftHandRb, _leftArmLength, true);
        }

        // 2. Process Right Arm Physics Reach
        if (_isRightArmExtended && _rightHandRb != null && _headTransform != null)
        {
            ApplyArmReachingForces(_rightHandRb, _rightArmLength, false);
        }
    }

    /// <summary>
    /// Computes and applies spring/damping attraction forces and look-alignment torques
    /// to the hand Rigidbody to pull the physical joint chain towards the looking target (center line).
    /// </summary>
    /// <param name="handRb">The Rigidbody of the last child segment in the arm.</param>
    /// <param name="armLength">The maximum physical length of the arm hierarchy.</param>
    /// <param name="isLeft">True if computing the Left Arm reaching direction, false for the Right Arm.</param>
    private void ApplyArmReachingForces(Rigidbody handRb, float armLength, bool isLeft)
    {
        Vector3 forward = _headTransform.forward;
        Vector3 up = _headTransform.up;

        // Calculate target location in front of the head at tweaked extension range and vertical/forward offsets
        Vector3 targetPosition = _headTransform.position 
            + forward * (armLength * _reachLengthFactor + _forwardOffset)
            + up * _verticalOffset;

        // Calculate proportional attraction force vector
        Vector3 toTarget = targetPosition - handRb.position;
        Vector3 extensionForce = toTarget * _extendForce;

        // Calculate damping force to control oscillation and maintain joint stability
        Vector3 dampingForce = -handRb.linearVelocity * _extendDamping;

        // Apply net force, scaled by the segment mass to normalize behaviors
        Vector3 netForce = (extensionForce + dampingForce) * handRb.mass;
        handRb.AddForce(netForce, ForceMode.Force);

        // Compute rotational target rotation matching the camera/look direction
        Quaternion targetRotation = Quaternion.LookRotation(forward, up);
        
        // Apply additional manual rotation offset (Pitch, Yaw, Roll)
        targetRotation = targetRotation * Quaternion.Euler(_handRotationOffset);

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
            Vector3 alignmentTorque = axis * (angle * _alignmentTorque * Mathf.Deg2Rad);
            Vector3 rotationalDamping = -handRb.angularVelocity * _alignmentDamping;

            // Apply net torque to orient the hand/nozzle forward
            Vector3 netTorque = (alignmentTorque + rotationalDamping) * handRb.mass;
            handRb.AddTorque(netTorque, ForceMode.Force);
        }
    }


    /// <summary>
    /// Recursively traverses a hierarchy to find the deepest child node that contains a Rigidbody,
    /// falling back to the deepest child if no Rigidbody is found.
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
    /// Measures the total length of a single-child linear hierarchy by summing distance gaps.
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
        if (_enableDebugLogs)
        {
            Debug.Log($"[PlayerArmsController] Right Arm Extension sync: {newState} (Owner: {netId})");
        }
    }

    #endregion
}
