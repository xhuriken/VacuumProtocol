using Mirror;
using UnityEngine;

    /// <summary>
    /// Description: Handles jumping logic and custom gravity for snappy descent.
    /// Context: Attached to the player prefab alongside PlayerMovementComponent.
    /// Justification: Decoupled from horizontal movement to keep physics domains separate. Custom gravity ensures jumps don't feel floaty.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerJumpComponent : NetworkBehaviour
    {
        [Header("Jump Settings")]
        [Tooltip("Role: The vertical force applied on jump.\nUse Case: Jumping.\nJustification: Configured as an Impulse force to provide immediate upward velocity.")]
        [SerializeField] private float _jumpImpulse = 12f;
        
        [Tooltip("Role: Custom gravity multiplier during descent.\nUse Case: Snappy falling.\nJustification: Standard Unity gravity feels too floaty for responsive platforming. This accelerates the player downwards when falling.")]
        [SerializeField] private float _gravityMultiplier = 3.5f;

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
        /// Description: FixedUpdate callback. Processes jump and gravity.
        /// Context: Physics lifecycle event. Only executed for the local player.
        /// Justification: Modifies rigidbody velocities so it must run in the physics loop to remain deterministic.
        /// </summary>
        private void FixedUpdate()
        {
            if (!isLocalPlayer) return;

            HandleJump();
            ApplyCustomGravity();
        }

        /// <summary>
        /// Description: Processes the jump input and applies upward force if grounded.
        /// Context: Called during FixedUpdate.
        /// Justification: Uses a simple Y-velocity check as a proxy for groundedness to prevent infinite double jumping, without needing expensive Raycasts.
        /// </summary>
        private void HandleJump()
        {
            if (_input.JumpTriggered)
            {
                // Simple ground check
                if (Mathf.Abs(_rb.linearVelocity.y) < 0.05f)
                {
                    _rb.AddForce(Vector3.up * _jumpImpulse, ForceMode.Impulse);
                }
            }
        }

        /// <summary>
        /// Description: Applies extra gravity when moving downwards.
        /// Context: Called during FixedUpdate.
        /// Justification: Creates a "Super Mario" style snappy jump curve instead of a perfect parabola.
        /// </summary>
        private void ApplyCustomGravity()
        {
            if (_rb.linearVelocity.y < 0)
            {
                _rb.AddForce(Vector3.up * (Physics.gravity.y * _gravityMultiplier), ForceMode.Acceleration);
            }
        }
    }
