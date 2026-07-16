using System.Collections.Generic;
using UnityEngine;

namespace VacuumProtocol.Player
{
    /// <summary>
    /// Description: Controls the active ragdoll physical head and neck joints based on look pitch.
    /// Context: Attached to the player prefab.
    /// Justification: Implements physics-based head tilting (pitch) using ConfigurableJoint target rotations, allowing intermediate bones to bend organically via spring dynamics.
    /// </summary>
    public class PhysicalHeadController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The look component to read the current pitch from.")]
        [SerializeField] private PlayerLookComponent _lookComponent;

        [Header("Bones and Joints Settings")]
        [Tooltip("The root transform of the neck/head bone hierarchy. Used to auto-configure physics for all neck joints.")]
        [SerializeField] private Transform _neckRootTransform;

        [Tooltip("The list of joints that should actively rotate with the pitch input.")]
        [SerializeField] private List<JointRotationSetting> _controlledJoints = new List<JointRotationSetting>();

        [Header("Physics Configuration")]
        [Tooltip("Force applied by the joint's slerp drive to return to target rotation.")]
        [SerializeField] private float _jointSpringForce = 1500f;

        [Tooltip("Damping applied to the joint's slerp drive to prevent oscillation.")]
        [SerializeField] private float _jointDamping = 100f;

        [Tooltip("Angular drag applied to the Rigidbody of neck and head bones to control wobbliness.")]
        [SerializeField] private float _neckAngularDrag = 15f;

        [Tooltip("If true, automatically configures all child ConfigurableJoints and Rigidbodies at Start.")]
        [SerializeField] private bool _autoConfigurePhysics = true;

        [Tooltip("Enable diagnostic logs.")]
        [SerializeField] private bool _enableDebugLogs = false;

        /// <summary>
        /// Description: Structure to define how much pitch rotation is applied to a specific joint.
        /// </summary>
        [System.Serializable]
        public class JointRotationSetting
        {
            [Tooltip("The configurable joint of the bone.")]
            public ConfigurableJoint Joint;

            [Tooltip("Multiplier for the pitch angle applied to this bone. Positive values rotate forward on local X.")]
            [Range(-2f, 2f)]
            public float PitchMultiplier = 0.5f;

            /// <summary>
            /// Cached starting local rotation of the bone relative to its parent.
            /// </summary>
            [HideInInspector] public Quaternion StartLocalRotation;
        }

        /// <summary>
        /// Description: Start callback. Resolves components, caches initial orientations, and configures active ragdoll physics.
        /// </summary>
        private void Start()
        {
            if (_lookComponent == null)
            {
                _lookComponent = GetComponentInParent<PlayerLookComponent>();
            }

            if (_lookComponent == null)
            {
                Debug.LogError("[PhysicalHeadController] PlayerLookComponent is not assigned and could not be found in parent!");
                return;
            }

            // Cache start local rotations of controlled joints
            foreach (var setting in _controlledJoints)
            {
                if (setting.Joint != null)
                {
                    setting.StartLocalRotation = setting.Joint.transform.localRotation;
                }
            }

            if (_autoConfigurePhysics)
            {
                ConfigurePhysicsSettings();
            }

            IgnorePlayerCollisions();
        }

        /// <summary>
        /// Description: FixedUpdate callback. Drives the controlled joints using the player's look pitch.
        /// </summary>
        private void FixedUpdate()
        {
            if (_lookComponent == null) return;

            float pitch = _lookComponent.CurrentPitch;

            foreach (var setting in _controlledJoints)
            {
                if (setting.Joint == null) continue;

                // Calculate the target rotation offset based on look pitch
                float targetAngle = pitch * setting.PitchMultiplier;
                Quaternion targetOffset = Quaternion.Euler(targetAngle, 0f, 0f);
                
                // Desired local rotation of the bone relative to its parent
                Quaternion targetLocalRotation = setting.StartLocalRotation * targetOffset;

                // Apply target rotation to the ConfigurableJoint
                SetJointTargetRotation(setting.Joint, setting.StartLocalRotation, targetLocalRotation);
            }
        }

        /// <summary>
        /// Description: Sets the target rotation of a ConfigurableJoint relative to its starting local rotation.
        /// </summary>
        /// <param name="joint">The joint component to modify.</param>
        /// <param name="startLocalRotation">The starting local rotation of the joint's transform.</param>
        /// <param name="targetLocalRotation">The target local rotation we want the transform to reach.</param>
        private void SetJointTargetRotation(ConfigurableJoint joint, Quaternion startLocalRotation, Quaternion targetLocalRotation)
        {
            // The relative rotation from the initial rotation to the target rotation
            Quaternion relativeRotation = Quaternion.Inverse(startLocalRotation) * targetLocalRotation;
            
            // Joint space axes relative to the local transform
            Vector3 jointRight = joint.axis;
            Vector3 jointUp = joint.secondaryAxis;
            Vector3 jointForward = Vector3.Cross(jointRight, jointUp).normalized;
            jointUp = Vector3.Cross(jointForward, jointRight).normalized;
            
            Quaternion localToJointSpace = Quaternion.LookRotation(jointForward, jointUp);
            
            // Transform relative rotation into joint space and invert it
            Quaternion targetRotationInJointSpace = Quaternion.Inverse(localToJointSpace) * relativeRotation * localToJointSpace;
            joint.targetRotation = Quaternion.Inverse(targetRotationInJointSpace);
        }

        /// <summary>
        /// Description: Auto-configures joint drives, projection settings, and Rigidbody parameters for stability.
        /// </summary>
        private void ConfigurePhysicsSettings()
        {
            Transform searchRoot = _neckRootTransform != null ? _neckRootTransform : transform;

            ConfigurableJoint[] joints = searchRoot.GetComponentsInChildren<ConfigurableJoint>(true);
            Rigidbody[] rigidbodies = searchRoot.GetComponentsInChildren<Rigidbody>(true);

            JointDrive drive = new JointDrive
            {
                positionSpring = _jointSpringForce,
                positionDamper = _jointDamping,
                maximumForce = float.MaxValue
            };

            foreach (ConfigurableJoint joint in joints)
            {
                if (joint == null) continue;

                // Use Slerp drive for smooth 3D active ragdoll rotation
                joint.rotationDriveMode = RotationDriveMode.Slerp;
                joint.slerpDrive = drive;
                joint.angularXDrive = drive;
                joint.angularYZDrive = drive;

                // Enable projection to prevent bone separation under force
                joint.projectionMode = JointProjectionMode.PositionAndRotation;
                joint.projectionDistance = 0.01f;
                joint.projectionAngle = 0.1f;
            }

            foreach (Rigidbody rb in rigidbodies)
            {
                if (rb == null) continue;

                // Apply high angular drag to stabilize spring movements and damp oscillations
                rb.angularDamping = _neckAngularDrag;

                // Increase solver iterations for physics stability
                rb.solverIterations = 25;
                rb.solverVelocityIterations = 15;
            }

            if (_enableDebugLogs)
            {
                Debug.Log($"[PhysicalHeadController] Configured physics for {joints.Length} joints and {rigidbodies.Length} rigidbodies under {searchRoot.name}.");
            }
        }

        /// <summary>
        /// Description: Dynamically ignores collisions between all neck/head colliders and other player colliders.
        /// </summary>
        private void IgnorePlayerCollisions()
        {
            Transform root = transform.root;
            Collider[] playerColliders = root.GetComponentsInChildren<Collider>(true);
            
            Transform neckRoot = _neckRootTransform != null ? _neckRootTransform : transform;
            Collider[] neckColliders = neckRoot.GetComponentsInChildren<Collider>(true);

            int ignoredCount = 0;
            foreach (Collider neckColl in neckColliders)
            {
                if (neckColl == null) continue;
                foreach (Collider otherColl in playerColliders)
                {
                    if (otherColl == null || otherColl == neckColl) continue;
                    
                    // If the other collider is NOT part of the neck hierarchy, ignore collisions
                    if (!otherColl.transform.IsChildOf(neckRoot))
                    {
                        Physics.IgnoreCollision(neckColl, otherColl, true);
                        ignoredCount++;
                    }
                }
            }

            if (_enableDebugLogs)
            {
                Debug.Log($"[PhysicalHeadController] Ignored {ignoredCount} collision pairs between neck bones and player body.");
            }
        }
    }
}
