using Mirror;
using UnityEngine;

/// <summary>
/// Description: Handles the base (Hips) locomotion and jumping.
/// Context: Attached to Player_V2.
/// Justification: Separates continuous movement (FixedUpdate) from instant impulses such as the jump (Update) for perfectly responsive input.
/// </summary>
[RequireComponent(typeof(PlayerV2_Controller))]
public class PlayerV2_Movement : NetworkBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Role: Target movement speed of the player.")]
    public float MoveSpeed = 5f;

    [Tooltip("Role: Absolute maximum horizontal speed (safety cap against external forces).")]
    public float MaxSpeed = 10f;

    [Tooltip("Role: Acceleration used to reach the target speed.")]
    public float Acceleration = 15f;

    [Tooltip("Role: Braking strength when there is no input.")]
    public float Deceleration = 15f;

    [Header("Sprint Settings")]
    [Tooltip("Role: Speed multiplier while the player sprints.")]
    public float SprintSpeedMultiplier = 1.5f;

    [Header("Crouch Settings")]
    [Tooltip("Role: Speed multiplier while the player crouches.")]
    public float CrouchSpeedMultiplier = 0.5f;
    [Tooltip("Role: Total vertical head offset while crouching.")]
    public float CrouchHeadOffset = -0.3f;

    [Header("Jump Settings")]
    [Tooltip("Role: Instant velocity (mass independent) applied to the Hips on jump.")]
    public float JumpForce = 8f;

    [Header("Gravity Settings")]
    [Tooltip("Role: Gravity multiplier while falling (heavier fall, Hollow Knight style).")]
    public float FallGravityMultiplier = 3f;

    [Tooltip("Role: Radius of the ground detection sphere (should roughly match the wheel size).")]
    public float WheelRadius = 0.15f;

    [Tooltip("Role: Ground layer used by the ground check.")]
    public LayerMask GroundLayer;

    [Header("Jump Compression Scaling")]
    [Tooltip("Role: Y distance (Hips -> wheel) when the suspension is fully extended (relaxed spring = weak jump).")]
    public float JumpMaxExtensionDistance = 0.6f;

    [Tooltip("Role: Y distance (Hips -> wheel) when the suspension is compressed (compressed spring = strong jump).")]
    public float JumpMinCompressionDistance = 0.2f;

    [Header("Debug")]
    [Tooltip("Role: Enables verbose debug logging for this component.")]
    public bool EnableDebugLogs = false;

    private PlayerV2_Controller _controller;
    private PlayerInputHandler _input;
    private PlayerV2_Suspension _suspension;

    private bool _isGrounded;
    private bool _wasGrounded;
    private float _lastVerticalVelocity;
    private float _currentCompressionMultiplier;

    private void Awake()
    {
        _controller = GetComponent<PlayerV2_Controller>();
        _input = GetComponent<PlayerInputHandler>();
        _suspension = GetComponent<PlayerV2_Suspension>();
    }

    private void Start()
    {
        if (_controller != null && _controller.HipsRigidbody != null)
        {
            // Fully freeze the Hips rotation so it NEVER rotates
            _controller.HipsRigidbody.constraints |= RigidbodyConstraints.FreezeRotation;
        }
    }

    private void Update()
    {
        if (!isOwned) return;
        if (_input == null || _controller.HipsRigidbody == null) return;

        // The ground check runs in Update to stay perfectly in sync with the input (which lasts 1 frame)
        UpdateGroundCheckAndCompression();

        // --- Crouch (Head & Arms Retraction) ---
        if (_input.IsCrouching != _controller.IsCrouching)
        {
            _controller.CmdSetCrouching(_input.IsCrouching);
        }

        // --- Jump ---
        if (_input.JumpTriggered)
        {
            if (_isGrounded)
            {
                float finalJumpForce = JumpForce * _currentCompressionMultiplier;

                // Safety: do not jump if the force is too low (dangling wheels)
                if (finalJumpForce > 1f)
                {
                    Vector3 currentVel = _controller.HipsRigidbody.linearVelocity;

                    // Compute the force needed to reach EXACTLY the desired velocity.
                    // This keeps the player from stacking the suspension rebound with the jump and flying off.
                    float forceToApply = finalJumpForce - currentVel.y;

                    if (forceToApply > 0)
                    {
                        _controller.HipsRigidbody.AddForce(Vector3.up * forceToApply, ForceMode.VelocityChange);
                    }

                    if (_suspension != null)
                    {
                        _suspension.TriggerJumpRetraction();
                    }
                }
            }
        }
    }

    private void UpdateGroundCheckAndCompression()
    {
        _wasGrounded = _isGrounded;
        _isGrounded = false;
        float totalWheelY = 0f;
        int wheelCount = 0;

        if (_suspension != null && _suspension.WheelJoints != null)
        {
            foreach (var joint in _suspension.WheelJoints)
            {
                if (joint == null) continue;

                Vector3 wheelPos = joint.transform.position;
                // CheckSphere at the exact wheel position
                if (Physics.CheckSphere(wheelPos, WheelRadius, GroundLayer))
                {
                    _isGrounded = true;
                }

                totalWheelY += wheelPos.y;
                wheelCount++;
            }
        }

        _currentCompressionMultiplier = 0f;
        if (wheelCount > 0)
        {
            float avgWheelY = totalWheelY / wheelCount;
            float distanceToWheels = _controller.HipsRigidbody.position.y - avgWheelY;

            // InverseLerp: distance = MaxExtension gives multiplier 0, distance = MinCompression gives multiplier 1.
            _currentCompressionMultiplier = Mathf.InverseLerp(JumpMaxExtensionDistance, JumpMinCompressionDistance, distanceToWheels);
        }

        // Landing detection
        if (_isGrounded && !_wasGrounded)
        {
            if (_lastVerticalVelocity < -2f)
            {
                if (_suspension != null)
                {
                    _suspension.OnHardLanding(Mathf.Abs(_lastVerticalVelocity));
                }
            }
        }
    }

    private void FixedUpdate()
    {
        if (!isOwned) return;
        if (_input == null || _controller.HipsRigidbody == null) return;

        if (_controller.CameraTransform == null)
        {
            Debug.LogError("[PlayerV2_Movement] Critical error: CameraTransform is not assigned on PlayerV2_Controller!");
            return;
        }

        Vector2 moveInput = _input.MoveInput;

        // FIX: do NOT use _controller.TorsoRigidbody.transform.forward,
        // because the arm physics pulls on the Torso and twists it, which deflects the movement!
        // Use the purely mathematical yaw from the Look component for a perfect trajectory, as in V1.
        PlayerV2_Look look = GetComponent<PlayerV2_Look>();
        float yaw = look != null ? look.CurrentYaw : _controller.TorsoRigidbody.transform.eulerAngles.y;

        Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;

        Vector3 moveDirection = (forward * moveInput.y + right * moveInput.x).normalized;
        Vector3 currentVelocity = _controller.HipsRigidbody.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);

        if (moveInput.sqrMagnitude > 0.01f)
        {
            // Speed modified by sprint / crouch
            float currentMoveSpeed = MoveSpeed;
            if (_input.IsCrouching)
            {
                currentMoveSpeed *= CrouchSpeedMultiplier;
            }
            else if (_input.IsSprinting)
            {
                currentMoveSpeed *= SprintSpeedMultiplier;
            }

            // Acceleration-based movement toward a target velocity
            Vector3 targetVelocity = moveDirection * currentMoveSpeed;
            Vector3 velocityChange = targetVelocity - horizontalVelocity;

            // Continuous forces are applied in FixedUpdate
            _controller.HipsRigidbody.AddForce(velocityChange * Acceleration, ForceMode.Acceleration);
        }
        else
        {
            // Controlled braking
            if (horizontalVelocity.magnitude > 0.1f)
            {
                Vector3 brakingForce = -horizontalVelocity * Deceleration;
                _controller.HipsRigidbody.AddForce(brakingForce, ForceMode.Acceleration);
            }
            else
            {
                // Full stop to compensate for the zero friction
                _controller.HipsRigidbody.linearVelocity = new Vector3(0f, currentVelocity.y, 0f);
            }
        }

        // Strictly enforce MaxSpeed to cap the absolute speed
        currentVelocity = _controller.HipsRigidbody.linearVelocity;
        horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
        if (horizontalVelocity.magnitude > MaxSpeed)
        {
            horizontalVelocity = horizontalVelocity.normalized * MaxSpeed;
            _controller.HipsRigidbody.linearVelocity = new Vector3(horizontalVelocity.x, currentVelocity.y, horizontalVelocity.z);
        }

        // Heavier fall effect (Hollow Knight style)
        if (currentVelocity.y < 0 && !_isGrounded)
        {
            _controller.HipsRigidbody.AddForce(Physics.gravity * (FallGravityMultiplier - 1f), ForceMode.Acceleration);
        }

        _lastVerticalVelocity = currentVelocity.y;
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        if (_controller == null || _controller.HipsRigidbody == null) return;
        if (_suspension == null || _suspension.WheelJoints == null) return;

        // Wheel ground check gizmos (downward raycast)
        foreach (var joint in _suspension.WheelJoints)
        {
            if (joint == null) continue;
            Vector3 wheelPos = joint.transform.position;

            bool isWheelGrounded = Physics.Raycast(wheelPos, Vector3.down, WheelRadius + 0.05f, GroundLayer);
            Gizmos.color = isWheelGrounded ? new Color(0, 1, 0, 0.4f) : new Color(1, 0, 0, 0.4f);

            Gizmos.DrawLine(wheelPos, wheelPos + Vector3.down * (WheelRadius + 0.05f));
            Gizmos.DrawWireSphere(wheelPos + Vector3.down * WheelRadius, 0.05f);
        }

        // Gizmo showing the jump compression multiplier
        Vector3 hipsPos = _controller.HipsRigidbody.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(hipsPos, hipsPos + Vector3.up * _currentCompressionMultiplier);
    }
}

