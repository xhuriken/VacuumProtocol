using UnityEngine;

namespace VacuumProtocol.Player
{
    /// <summary>
    /// Description: Controls the physical head using a ConfigurableJoint to simulate a vertical spring-crouch and a "boing boing" wiggle.
    /// Context: Attached to the player's head mesh, separate from the camera.
    /// Justification: Gives the player character's head physical weight and momentum when moving or crouching, rather than snapping rigidly to the camera rotation.
    /// </summary>
    [RequireComponent(typeof(ConfigurableJoint))]
    public class PhysicalHeadController : MonoBehaviour
    {
        [Header("Follow Settings")]
        [Tooltip("Role: The camera transform to track.\nUse Case: Look target.\nJustification: The head needs to physically spring towards the direction the player is aiming the camera.")]
        [SerializeField] private Transform _cameraTransform;

        [Tooltip("Role: Ratio of camera look rotation that the head should follow.\nUse Case: Turning stiffness.\nJustification: A ratio < 1 means the head doesn't snap instantly, creating a slight lag/drag effect.")]
        [SerializeField] private float _followRatio = 0.7f;

        [Header("Arc Settings")]
        [Tooltip("Role: Radius of the virtual circle arc representing the neck bend.\nUse Case: Looking up/down.\nJustification: Simulates the cervical vertebrae arc so the head doesn't just rotate in place, but physically shifts forward/backward.")]
        [SerializeField] private float _arcRadius = 0.2f;

        [Tooltip("Role: Downward sag multiplier when the head bends in pitch.\nUse Case: Looking down.\nJustification: Adds extra weight simulation so the head drops slightly when looking down.")]
        [SerializeField] private float _sagFactor = 0.05f;

        private ConfigurableJoint _joint;
        private Transform _originalParent;
        private float _crouchYOffset = 0f;

        /// <summary>
        /// Description: Gets the current crouch vertical offset of the head.
        /// Context: Read by UI or debug systems to track current crouch state.
        /// Justification: Exposed for external scripts to know the physical offset state without calculating it.
        /// </summary>
        public float CrouchYOffset
        {
            get
            {
                return _crouchYOffset;
            }
        }

        /// <summary>
        /// Description: Unparents the head from the body and sets up collision rules.
        /// Context: Start lifecycle event.
        /// Justification: The head must be detached from the body hierarchy at runtime so that its ConfigurableJoint can act freely in world space without inheriting parent transforms recursively.
        /// </summary>
        private void Start()
        {
            // Cache the configurable joint component.
            _joint = GetComponent<ConfigurableJoint>();

            // Detach from parent at runtime to avoid transform propagation conflicts.
            _originalParent = transform.parent;

            // Dynamically ignore collisions between the head and player body/arms colliders.
            Collider headCollider = GetComponent<Collider>();
            if (headCollider != null && _originalParent != null)
            {
                Collider[] bodyColliders = _originalParent.GetComponentsInChildren<Collider>();
                foreach (Collider bodyCollider in bodyColliders)
                {
                    if (bodyCollider != headCollider)
                    {
                        Physics.IgnoreCollision(headCollider, bodyCollider, true);
                    }
                }
            }

            transform.SetParent(null);
        }

        /// <summary>
        /// Description: Updates the target crouch vertical offset.
        /// Context: Called by PlayerMovementComponent when the crouch button is held.
        /// Justification: Applies a downward target offset to the joint, which will cause the physics spring to bounce the head downwards.
        /// </summary>
        /// <param name="crouchOffset">The offset along the Y axis.</param>
        public void SetCrouchOffset(float crouchOffset)
        {
            _crouchYOffset = crouchOffset;
        }

        /// <summary>
        /// Description: Continuously updates the joint's target position and rotation to follow the camera.
        /// Context: FixedUpdate physics event.
        /// Justification: Must be done in FixedUpdate because we are modifying a physics joint's target state.
        /// </summary>
        private void FixedUpdate()
        {
            // If the original parent (player body) was destroyed, destroy this detached head.
            if (_originalParent == null)
            {
                Destroy(gameObject);
                return;
            }

            if (_cameraTransform == null)
            {
                return;
            }

            ApplyJointTargetState();
        }

        /// <summary>
        /// Description: Calculates and applies the target rotation and target position to the joint.
        /// Context: Called every FixedUpdate.
        /// Justification: Uses trigonometry to calculate the correct translation offset along the Z and Y axes based on the pitch angle, simulating a realistic neck.
        /// </summary>
        private void ApplyJointTargetState()
        {
            // Obtain relative rotation from camera compared to joint parent.
            Quaternion relativeCamRot = Quaternion.Inverse(_originalParent.rotation) * _cameraTransform.rotation;
            Vector3 camAngles = relativeCamRot.eulerAngles;

            // Clamp angles between -180 and 180 degrees.
            float pitch = Mathf.DeltaAngle(0f, camAngles.x) * _followRatio;
            float yaw = Mathf.DeltaAngle(0f, camAngles.y) * _followRatio;
            float roll = 0f; // Let the physical Slerp drive handle roll oscillations.

            // ConfigurableJoint targetRotation is defined as the INVERSE of the desired local rotation.
            Quaternion targetRot = Quaternion.Euler(pitch, yaw, roll);
            _joint.targetRotation = Quaternion.Inverse(targetRot);

            // Compute arc of circle translation offset.
            float pitchRad = pitch * Mathf.Deg2Rad;
            float arcZ = _arcRadius * Mathf.Sin(pitchRad);
            
            // Y sag should always pull downward (positive magnitude), whether looking up or down.
            float arcY = _arcRadius * (1f - Mathf.Cos(pitchRad)) + _sagFactor * Mathf.Abs(Mathf.Sin(pitchRad));

            // ConfigurableJoint targetPosition is defined as the INVERSE of the desired local offset:
            // - To move the head down (negative Y), we set a positive target Y.
            // - To move the head forward (positive Z), we set a negative target Z.
            _joint.targetPosition = new Vector3(0f, _crouchYOffset + arcY, -arcZ);
        }
    }
}
