using Mirror;
using UnityEngine;

namespace VacuumProtocol.Player
{
    /// <summary>
    /// Description: Handles jumping logic and custom gravity for snappy descent, integrated with WheelSuspensionController.
    /// Context: Attached to the player prefab alongside PlayerMovementComponent and WheelSuspensionController.
    /// Justification: Decoupled from horizontal movement to keep physics domains separate. Interacts with wheel suspensions during jump takeoff and landing.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerJumpComponent : NetworkBehaviour
    {
        [Header("Jump Settings")]
        [Tooltip("Role: The vertical force applied on jump.\nUse Case: Jumping.\nJustification: Configured as an Impulse force to provide immediate upward velocity.")]
        [SerializeField] private float _jumpImpulse = 14f;
        
        [Tooltip("Role: Custom gravity multiplier during descent.\nUse Case: Snappy falling.\nJustification: Standard Unity gravity feels too floaty for responsive platforming. This accelerates the player downwards when falling.")]
        [SerializeField] private float _gravityMultiplier = 3.5f;

        private Rigidbody _rb;
        private PlayerInputHandler _input;
        private WheelSuspensionController _suspension;

        /// <summary>
        /// Description: Awake callback. Caches references.
        /// Context: Lifecycle event.
        /// Justification: Fetches the Rigidbody, InputHandler, and WheelSuspensionController safely before Start.
        /// </summary>
        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _input = GetComponent<PlayerInputHandler>();
            _suspension = GetComponent<WheelSuspensionController>();
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
        /// Justification: Checks ground state from WheelSuspensionController (or fallback vertical velocity) and triggers upward impulse + suspension takeoff effect.
        /// </summary>
        private void HandleJump()
        {
            if (_input.JumpTriggered)
            {
                bool isGrounded = _suspension != null ? _suspension.IsGrounded : Mathf.Abs(_rb.linearVelocity.y) < 0.1f;

                if (isGrounded)
                {
                    _rb.AddForce(Vector3.up * _jumpImpulse, ForceMode.Impulse);

                    if (_suspension != null)
                    {
                        _suspension.TriggerJumpTakeoff();
                    }
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
}

