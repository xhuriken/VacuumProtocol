using Mirror;
using UnityEngine;

/// <summary>
    /// Handles jumping logic and custom gravity for snappy descent.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerJumpComponent : NetworkBehaviour
    {
        [Header("Jump Settings")]
        [SerializeField] private float _jumpImpulse = 12f;
        [SerializeField] private float _gravityMultiplier = 3.5f;

        private Rigidbody _rb;
        private PlayerInputHandler _input;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _input = GetComponent<PlayerInputHandler>();
        }

        private void FixedUpdate()
        {
            if (!isLocalPlayer) return;

            HandleJump();
            ApplyCustomGravity();
        }

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

        private void ApplyCustomGravity()
        {
            if (_rb.linearVelocity.y < 0)
            {
                _rb.AddForce(Vector3.up * (Physics.gravity.y * _gravityMultiplier), ForceMode.Acceleration);
            }
        }
    }
