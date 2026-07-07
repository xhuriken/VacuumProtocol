using Mirror;
using UnityEngine;

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
    /// Description: Computes and applies spring/damping attraction forces and look-alignment torques.
    /// Context: Called by FixedUpdate.
    /// Justification: Pulls the physical joint chain towards the looking target (center line) while dampening the forces to avoid violent snapping.
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
