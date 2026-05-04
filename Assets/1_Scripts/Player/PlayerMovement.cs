using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Advanced Rigidbody FPS Controller designed for a "Heavy & Fast" robot feel.
/// Handles acceleration, deceleration, sprinting, and physical bouncing.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerPhysicsMovement : NetworkBehaviour
{
    [Header("Movement Constants")]
    [SerializeField] private float _accelerationForce = 150f; // High force for "heavy" feel
    [SerializeField] private float _maxSpeed = 10f;
    [SerializeField] private float _sprintMultiplier = 1.6f;
    [SerializeField] private float _decelerationDamping = 8f; // High damping for snappy stops
    [SerializeField] private float _airControlFactor = 0.3f;

    [Header("Jump & Gravity")]
    [SerializeField] private float _jumpImpulse = 12f;
    [SerializeField] private float _gravityMultiplier = 3.5f; // Makes falling much faster

    [Header("Physics Interaction")]
    [SerializeField] private float _bounceMultiplier = 0.6f;
    [SerializeField] private float _minImpactForBounce = 3f;

    [Header("Look Settings")]
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private float _sensitivity = 0.15f;
    [SerializeField] private bool _flipCamera = false;


    private Rigidbody _rb;
    public Rigidbody Rb => _rb;
    private Vector2 _moveInput;
    private Vector2 _lookInput;
    private float _cameraPitch;
    private bool _isSprinting;

    private void Start()
    {
        if (!isLocalPlayer)
        {
            GetComponentInChildren<Camera>().gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Configures the Rigidbody for high-performance physical movement.
    /// </summary>
    public override void OnStartLocalPlayer()
    {
        _rb = GetComponent<Rigidbody>();


        // Rigidbody setup for snappier feel
        _rb.useGravity = true;
        _rb.freezeRotation = true;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Drag handles the deceleration
        _rb.linearDamping = _decelerationDamping;

        if (_cameraTransform == null)
        {
            _cameraTransform = GetComponentInChildren<Camera>().transform;
        }

        Cursor.lockState = CursorLockMode.Locked;
    }

    private void Update()
    {
        if (!isLocalPlayer) return;

        HandleRotation();
    }

    private void FixedUpdate()
    {
        if (!isLocalPlayer) return;

        ApplyMovementPhysics();
        ApplyCustomGravity();
    }

    private void HandleRotation()
    {
        transform.Rotate(Vector3.up * (_lookInput.x * _sensitivity));

        _cameraPitch -= _lookInput.y * _sensitivity;
        _cameraPitch = Mathf.Clamp(_cameraPitch, -85f, 85f);
        
        float yRotation = _flipCamera ? 180f : 0f;
        _cameraTransform.localRotation = Quaternion.Euler(_cameraPitch, yRotation, 0);
    }

    private void ApplyMovementPhysics()
    {
        bool isGrounded = Mathf.Abs(_rb.linearVelocity.y) < 0.05f;
        Vector3 moveDirection = transform.right * _moveInput.x + transform.forward * _moveInput.y;


        float currentMaxSpeed = _isSprinting ? _maxSpeed * _sprintMultiplier : _maxSpeed;

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

    /// <summary>
    /// Increases gravity force when falling to create a "snappy" jump descent.
    /// </summary>
    private void ApplyCustomGravity()
    {
        if (_rb.linearVelocity.y < 0)
        {
            _rb.AddForce(Vector3.up * (Physics.gravity.y * _gravityMultiplier), ForceMode.Acceleration);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isLocalPlayer) return;

        //float impactForce = collision.relativeVelocity.magnitude;

        //// Ensure we only bounce off vertical surfaces (walls), not floors
        //if (impactForce > _minImpactForBounce && Mathf.Abs(collision.contacts[0].normal.y) < 0.5f)
        //{
        //    Vector3 normal = collision.contacts[0].normal;
        //    Vector3 reflectDir = Vector3.Reflect(collision.relativeVelocity, normal);

        //    _rb.AddForce(reflectDir * _bounceMultiplier, ForceMode.Impulse);
        //}
    }

    #region Input Events
    public void OnMove(InputAction.CallbackContext context) => _moveInput = context.ReadValue<Vector2>();
    public void OnLook(InputAction.CallbackContext context) => _lookInput = context.ReadValue<Vector2>();
    public void OnSprint(InputAction.CallbackContext context) => _isSprinting = context.ReadValueAsButton();
    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed && Mathf.Abs(_rb.linearVelocity.y) < 0.05f)
        {
            _rb.AddForce(Vector3.up * _jumpImpulse, ForceMode.Impulse);
        }
    }
    #endregion
}