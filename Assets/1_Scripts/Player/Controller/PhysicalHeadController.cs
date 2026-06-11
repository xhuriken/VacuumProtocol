using UnityEngine;

namespace VacuumProtocol.Player
{
    /// <summary>
    /// Controls the physical head using a ConfigurableJoint to simulate a vertical spring-crouch and a "boing boing" wiggle.
    /// </summary>
    [RequireComponent(typeof(ConfigurableJoint))]
    public class PhysicalHeadController : MonoBehaviour
    {
        [Header("Follow Settings")]
        [Tooltip("The camera transform steered by the player's mouse look.")]
        [SerializeField] private Transform _cameraTransform;

        [Tooltip("Ratio of camera look rotation that the head should follow.")]
        [SerializeField] private float _followRatio = 0.7f;

        [Header("Arc Settings")]
        [Tooltip("Radius of the virtual circle arc representing the neck bend.")]
        [SerializeField] private float _arcRadius = 0.2f;

        [Tooltip("Downward sag multiplier when the head bends in pitch.")]
        [SerializeField] private float _sagFactor = 0.05f;

        private ConfigurableJoint _joint;
        private Transform _originalParent;
        private float _crouchYOffset = 0f;

        /// <summary>
        /// Gets the current crouch vertical offset of the head.
        /// </summary>
        public float CrouchYOffset
        {
            get
            {
                return _crouchYOffset;
            }
        }

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
        /// Updates the target crouch vertical offset.
        /// </summary>
        /// <param name="crouchOffset">The offset along the Y axis.</param>
        public void SetCrouchOffset(float crouchOffset)
        {
            _crouchYOffset = crouchOffset;
        }

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
        /// Calculates and applies the target rotation and target position to the joint.
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
