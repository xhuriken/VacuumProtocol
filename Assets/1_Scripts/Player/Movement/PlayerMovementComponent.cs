using Mirror;
using UnityEngine;

    /// <summary>
    /// Description: Handles horizontal Rigidbody-based movement and sprinting.
    /// Context: Attached to the player prefab alongside PlayerJumpComponent.
    /// Justification: Uses AddForce to provide realistic acceleration and inertia instead of directly translating positions, creating a weighty feel.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerMovementComponent : NetworkBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("Role: The force applied when walking.\nUse Case: Base acceleration.\nJustification: Balanced to provide quick startup time without instantly hitting max speed.")]
        [SerializeField] private float _accelerationForce = 150f;
        
        [Tooltip("Role: Base maximum speed limit.\nUse Case: Velocity clamping.\nJustification: Prevents the physics engine from infinitely accelerating the player.")]
        [SerializeField] private float _maxSpeed = 10f;
        
        [Tooltip("Role: Multiplier for maximum speed when sprinting.\nUse Case: Sprint state.\nJustification: Scales the _maxSpeed during shift-key presses.")]
        [SerializeField] private float _sprintMultiplier = 1.6f;
        
        [Tooltip("Role: Multiplier for acceleration force while airborne.\nUse Case: Jump strafing.\nJustification: Allows minor course correction mid-air without full ground friction.")]
        [SerializeField] private float _airControlFactor = 0.3f;
        
        [Tooltip("Role: Friction applied to slow down the player.\nUse Case: Deceleration.\nJustification: Ensures the player stops quickly when letting go of the keys, mimicking heavy boots.")]
        [SerializeField] private float _decelerationDamping = 8f;

        private Rigidbody _rb;
        private PlayerInputHandler _input;

        /// <summary>
        /// Description: Awake callback. Caches references.
        /// Context: Lifecycle event.
        /// Justification: Fetches the Rigidbody and InputHandler safely before Start.
        /// </summary>
        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _input = GetComponent<PlayerInputHandler>();
        }

        /// <summary>
        /// Description: Applies local player-specific physics settings.
        /// Context: Mirror NetworkBehaviour callback.
        /// Justification: Remote player clones should not have deceleration damping applied, as they are interpolated via NetworkTransform.
        /// </summary>
        public override void OnStartLocalPlayer()
        {
            if (_rb != null)
            {
                _rb.linearDamping = _decelerationDamping;
            }
        }

        /// <summary>
        /// Description: FixedUpdate callback. Processes movement physics.
        /// Context: Physics lifecycle event. Only executed for the local player.
        /// Justification: Modifies rigidbody velocities and forces, so it must run in the physics loop to remain deterministic.
        /// </summary>
        private void FixedUpdate()
        {
            if (!isLocalPlayer) return;

            ApplyMovementPhysics();
        }

        /// <summary>
        /// Description: Calculates and applies horizontal movement forces.
        /// Context: Called during FixedUpdate.
        /// Justification: Calculates target vectors from input, applies force based on grounded state, and manually clamps X/Z velocity while ignoring Y velocity (gravity).
        /// </summary>
        private void ApplyMovementPhysics()
        {
            // Simple ground check based on vertical velocity
            bool isGrounded = Mathf.Abs(_rb.linearVelocity.y) < 0.05f;
            
            Vector2 moveInput = _input.MoveInput;
            Vector3 moveDirection = transform.right * moveInput.x + transform.forward * moveInput.y;

            float currentMaxSpeed = _input.IsSprinting ? _maxSpeed * _sprintMultiplier : _maxSpeed;

            if (moveDirection.magnitude > 0.1f)
            {
                float force = isGrounded ? _accelerationForce : _accelerationForce * _airControlFactor;
                _rb.AddForce(moveDirection.normalized * force, ForceMode.Acceleration);
            }

            // Clamp horizontal velocity only
            Vector3 horizontalVel = new Vector3(_rb.linearVelocity.x, 0, _rb.linearVelocity.z);
            if (horizontalVel.magnitude > currentMaxSpeed)
            {
                Vector3 cappedVel = horizontalVel.normalized * currentMaxSpeed;
                _rb.linearVelocity = new Vector3(cappedVel.x, _rb.linearVelocity.y, cappedVel.z);
            }
        }
    }
