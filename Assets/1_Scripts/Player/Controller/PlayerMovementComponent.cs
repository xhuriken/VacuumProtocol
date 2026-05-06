using Mirror;
using UnityEngine;

/// <summary>
    /// Handles horizontal Rigidbody-based movement and sprinting.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerMovementComponent : NetworkBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float _accelerationForce = 150f;
        [SerializeField] private float _maxSpeed = 10f;
        [SerializeField] private float _sprintMultiplier = 1.6f;
        [SerializeField] private float _airControlFactor = 0.3f;
        [SerializeField] private float _decelerationDamping = 8f;

        private Rigidbody _rb;
        private PlayerInputHandler _input;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _input = GetComponent<PlayerInputHandler>();
        }

        public override void OnStartLocalPlayer()
        {
            if (_rb != null)
            {
                _rb.linearDamping = _decelerationDamping;
            }
        }

        private void FixedUpdate()
        {
            if (!isLocalPlayer) return;

            ApplyMovementPhysics();
        }

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
