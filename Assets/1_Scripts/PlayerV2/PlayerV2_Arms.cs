using System.Collections.Generic;
using Mirror;
using UnityEngine;
using DG.Tweening;
using VacuumProtocol.Player; // Pour PlayerInputHandler

namespace VacuumProtocol.PlayerV2
{
    /// <summary>
    /// Description: Contrôle physique des bras pour le Player_V2.
    /// Context: Attaché au Player_V2 (doit être au même niveau que PlayerV2_Controller).
    /// Justification: Applique des forces et torques sur la main (dernier bone) pour étirer le bras physique vers la direction regardée.
    /// </summary>
    [RequireComponent(typeof(PlayerV2_Controller))]
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerV2_Arms : NetworkBehaviour
    {
        [Header("Physics Tuning Parameters")]
        public float ExtendForce = 350f;
        public float ExtendDamping = 12f;
        public float AlignmentTorque = 20f;
        public float AlignmentDamping = 3f;

        [Header("Reach Settings")]
        public float ReachLengthFactor = 1.0f;
        public float ForwardOffset = 0f;
        public float VerticalOffset = -0.1f;
        public Vector3 HandRotationOffset = Vector3.zero;

        [Header("Shoulder Animation")]
        public float ShoulderRotateDuration = 0.25f;
        public Ease ShoulderEase = Ease.OutBack;

        [Header("Joint Tuning (Auto-Configured)")]
        public bool LockAngularX = true;
        public bool EnableJointProjection = true;
        
        public float ShoulderExtendSpringForce = 1500f;
        public float ShoulderExtendDamping = 20f;
        public float ShoulderRestSpringForce = 800f;
        public float ShoulderRestDamping = 40f;
        
        public float ElbowWristExtendSpringForce = 1500f;
        public float ElbowWristExtendDamping = 20f;
        public float ElbowWristRestSpringForce = 150f;
        public float ElbowWristRestDamping = 15f;
        
        public float ArmAngularDrag = 15f;

        [Header("Retraction / Rest Physics")]
        [Tooltip("Si activé, aucune force n'est appliquée sur la main au repos. Elle pend librement avec la gravité.")]
        public bool FreeHangAtRest = true;

        // Synchronized Variables
        [SyncVar(hook = nameof(OnLeftArmStateChanged))]
        private bool _isLeftArmExtended = false;

        [SyncVar(hook = nameof(OnRightArmStateChanged))]
        private bool _isRightArmExtended = false;

        // References
        private PlayerV2_Controller _controller;
        private PlayerInputHandler _input;
        
        private Transform _leftHand;
        private Rigidbody _leftHandRb;
        private Transform _rightHand;
        private Rigidbody _rightHandRb;

        // Lengths
        private float _leftArmLength = 1.5f;
        private float _rightArmLength = 1.5f;

        // Release timestamps
        private float _leftReleaseTime = -100f;
        private float _rightReleaseTime = -100f;

        // Joints
        private ConfigurableJoint _leftShoulderJoint;
        private readonly List<ConfigurableJoint> _leftElbowWristJoints = new List<ConfigurableJoint>();

        private ConfigurableJoint _rightShoulderJoint;
        private readonly List<ConfigurableJoint> _rightElbowWristJoints = new List<ConfigurableJoint>();

        private bool _lastLeftExtended;
        private bool _lastRightExtended;

        public Transform LeftHand => _leftHand;
        public Transform RightHand => _rightHand;
        public bool IsLeftArmExtended => _isLeftArmExtended;
        public bool IsRightArmExtended => _isRightArmExtended;

        public bool IsLeftHandExtendedPhysically
        {
            get
            {
                if (_leftHand == null || _controller.LeftArmRoot == null || !_isLeftArmExtended) return false;
                float currentDist = Vector3.Distance(_leftHand.position, _controller.LeftArmRoot.position);
                return currentDist >= (_leftArmLength * ReachLengthFactor * 0.8f);
            }
        }

        private void Awake()
        {
            _controller = GetComponent<PlayerV2_Controller>();
            _input = GetComponent<PlayerInputHandler>();
        }

        private void Start()
        {
            // Initialisation Bras Gauche
            if (_controller.LeftArmRoot != null)
            {
                _leftHand = FindLastChild(_controller.LeftArmRoot);
                if (_leftHand != null)
                {
                    _leftHandRb = _leftHand.GetComponent<Rigidbody>();
                    _leftArmLength = CalculateHierarchyLength(_controller.LeftArmRoot);

                    _leftShoulderJoint = _controller.LeftArmRoot.GetComponent<ConfigurableJoint>();
                    foreach (var joint in _controller.LeftArmRoot.GetComponentsInChildren<ConfigurableJoint>(true))
                    {
                        if (joint != _leftShoulderJoint) _leftElbowWristJoints.Add(joint);
                    }
                }
            }

            // Initialisation Bras Droit
            if (_controller.RightArmRoot != null)
            {
                _rightHand = FindLastChild(_controller.RightArmRoot);
                if (_rightHand != null)
                {
                    _rightHandRb = _rightHand.GetComponent<Rigidbody>();
                    _rightArmLength = CalculateHierarchyLength(_controller.RightArmRoot);

                    _rightShoulderJoint = _controller.RightArmRoot.GetComponent<ConfigurableJoint>();
                    foreach (var joint in _controller.RightArmRoot.GetComponentsInChildren<ConfigurableJoint>(true))
                    {
                        if (joint != _rightShoulderJoint) _rightElbowWristJoints.Add(joint);
                    }
                }
            }

            // Setup Snap Épaules Initial
            if (_controller.LeftShoulder != null)
                _controller.LeftShoulder.localRotation = Quaternion.Euler(0f, _isLeftArmExtended ? 90f : 0f, 0f);
            
            if (_controller.RightShoulder != null)
                _controller.RightShoulder.localRotation = Quaternion.Euler(0f, _isRightArmExtended ? -90f : 0f, 0f);

            ConfigureArmJointsPhysics();

            _lastLeftExtended = _isLeftArmExtended;
            _lastRightExtended = _isRightArmExtended;
            UpdateJointDrives(true, _isLeftArmExtended);
            UpdateJointDrives(false, _isRightArmExtended);
        }

        private void Update()
        {
            if (!isLocalPlayer) return;

            bool leftInput = _input.LeftArmPressed;
            bool rightInput = _input.RightArmPressed;

            if (_input.IsVacuuming)
            {
                leftInput = false;
                rightInput = false;
            }

            if (leftInput != _isLeftArmExtended) CmdSetLeftArmExtended(leftInput);
            if (rightInput != _isRightArmExtended) CmdSetRightArmExtended(rightInput);
        }

        private void FixedUpdate()
        {
            // Bras Gauche
            if (_leftHandRb != null)
            {
                if (_isLeftArmExtended != _lastLeftExtended)
                {
                    UpdateJointDrives(true, _isLeftArmExtended);
                    _lastLeftExtended = _isLeftArmExtended;
                }
                ApplyArmPhysicsForces(_leftHandRb, _leftArmLength, true, _isLeftArmExtended);
            }

            // Bras Droit
            if (_rightHandRb != null)
            {
                if (_isRightArmExtended != _lastRightExtended)
                {
                    UpdateJointDrives(false, _isRightArmExtended);
                    _lastRightExtended = _isRightArmExtended;
                }
                ApplyArmPhysicsForces(_rightHandRb, _rightArmLength, false, _isRightArmExtended);
            }
        }

        private void ApplyArmPhysicsForces(Rigidbody handRb, float armLength, bool isLeft, bool isExtended)
        {
            if (!isExtended && FreeHangAtRest)
            {
                // On n'applique aucune force externe, on laisse le bras pendre par la gravité et la raideur des joints.
                return;
            }

            Transform headTrans = _controller.CameraTransform != null ? _controller.CameraTransform : transform;
            
            if (isExtended)
            {
                Vector3 forward = headTrans.forward;
                Vector3 up = headTrans.up;

                Vector3 targetPosition = headTrans.position 
                    + forward * (armLength * ReachLengthFactor + ForwardOffset) 
                    + up * VerticalOffset;
                
                // Force d'attraction
                Vector3 toTarget = targetPosition - handRb.position;
                Vector3 extensionForce = toTarget * ExtendForce;
                Vector3 dampingForce = -handRb.linearVelocity * ExtendDamping;
                Vector3 netForce = (extensionForce + dampingForce) * handRb.mass;
                handRb.AddForce(netForce, ForceMode.Force);

                // Alignement Rotation
                Quaternion targetRotation = Quaternion.LookRotation(forward, up) * Quaternion.Euler(HandRotationOffset);
                Quaternion deltaRotation = targetRotation * Quaternion.Inverse(handRb.rotation);
                deltaRotation.ToAngleAxis(out float angle, out Vector3 axis);

                if (!float.IsNaN(axis.x) && !float.IsNaN(axis.y) && !float.IsNaN(axis.z) && axis.sqrMagnitude > 0.001f)
                {
                    if (angle > 180f) angle -= 360f;
                    Vector3 alignmentTorque = axis * (angle * AlignmentTorque * Mathf.Deg2Rad);
                    Vector3 rotationalDamping = -handRb.angularVelocity * AlignmentDamping;
                    Vector3 netTorque = (alignmentTorque + rotationalDamping) * handRb.mass;
                    handRb.AddTorque(netTorque, ForceMode.Force);
                }
            }
        }

        private Transform FindLastChild(Transform parent)
        {
            Transform current = parent;
            Transform lastWithRb = parent.GetComponent<Rigidbody>() != null ? parent : null;

            while (current.childCount > 0)
            {
                current = current.GetChild(0);
                if (current.GetComponent<Rigidbody>() != null) lastWithRb = current;
            }
            return lastWithRb != null ? lastWithRb : current;
        }

        private float CalculateHierarchyLength(Transform root)
        {
            float totalLength = 0f;
            Transform current = root;

            while (current.childCount > 0)
            {
                Transform next = current.GetChild(0);
                totalLength += Vector3.Distance(current.position, next.position);
                current = next;
            }
            return totalLength > 0.05f ? totalLength : 1.5f;
        }

        #region Mirror Commands & Hooks

        [Command]
        private void CmdSetLeftArmExtended(bool extended) => _isLeftArmExtended = extended;

        [Command]
        private void CmdSetRightArmExtended(bool extended) => _isRightArmExtended = extended;

        private void OnLeftArmStateChanged(bool oldState, bool newState)
        {
            AnimateShoulder(true, newState);
            if (oldState && !newState) _leftReleaseTime = Time.time;
        }

        private void OnRightArmStateChanged(bool oldState, bool newState)
        {
            AnimateShoulder(false, newState);
            if (oldState && !newState) _rightReleaseTime = Time.time;
        }

        private void AnimateShoulder(bool isLeft, bool extended)
        {
            Transform shoulder = isLeft ? _controller.LeftShoulder : _controller.RightShoulder;
            if (shoulder == null) return;

            float targetY = 0f;
            if (extended) targetY = isLeft ? 90f : -90f;

            shoulder.DOKill();
            shoulder.DOLocalRotate(new Vector3(0f, targetY, 0f), ShoulderRotateDuration)
                .SetEase(ShoulderEase)
                .SetUpdate(UpdateType.Normal, true);
        }

        #endregion

        #region Physics Configuration

        private void ConfigureArmJointsPhysics()
        {
            List<ConfigurableJoint> allJoints = new List<ConfigurableJoint>();
            if (_controller.LeftArmRoot != null) allJoints.AddRange(_controller.LeftArmRoot.GetComponentsInChildren<ConfigurableJoint>(true));
            if (_controller.RightArmRoot != null) allJoints.AddRange(_controller.RightArmRoot.GetComponentsInChildren<ConfigurableJoint>(true));

            foreach (var joint in allJoints)
            {
                if (joint == null) continue;
                if (LockAngularX) joint.angularXMotion = ConfigurableJointMotion.Locked;
                if (EnableJointProjection)
                {
                    joint.projectionMode = JointProjectionMode.PositionAndRotation;
                    joint.projectionDistance = 0.01f;
                    joint.projectionAngle = 0.1f;
                }
            }

            List<Rigidbody> allRbs = new List<Rigidbody>();
            if (_controller.LeftArmRoot != null) allRbs.AddRange(_controller.LeftArmRoot.GetComponentsInChildren<Rigidbody>(true));
            if (_controller.RightArmRoot != null) allRbs.AddRange(_controller.RightArmRoot.GetComponentsInChildren<Rigidbody>(true));

            foreach (var rb in allRbs)
            {
                if (rb == null) continue;
                rb.angularDamping = ArmAngularDrag;
                rb.solverIterations = 25;
                rb.solverVelocityIterations = 15;
            }
        }

        private void UpdateJointDrives(bool isLeft, bool isExtended)
        {
            ConfigurableJoint shoulderJoint = isLeft ? _leftShoulderJoint : _rightShoulderJoint;
            List<ConfigurableJoint> elbowWristJoints = isLeft ? _leftElbowWristJoints : _rightElbowWristJoints;

            float shoulderSpring = isExtended ? ShoulderExtendSpringForce : ShoulderRestSpringForce;
            float shoulderDamp = isExtended ? ShoulderExtendDamping : ShoulderRestDamping;

            float elbowSpring = isExtended ? ElbowWristExtendSpringForce : ElbowWristRestSpringForce;
            float elbowDamp = isExtended ? ElbowWristExtendDamping : ElbowWristRestDamping;

            if (shoulderJoint != null)
            {
                JointDrive drive = new JointDrive { positionSpring = shoulderSpring, positionDamper = shoulderDamp, maximumForce = float.MaxValue };
                shoulderJoint.slerpDrive = drive;
                shoulderJoint.angularXDrive = drive;
                shoulderJoint.angularYZDrive = drive;
            }

            foreach (var joint in elbowWristJoints)
            {
                if (joint != null)
                {
                    JointDrive drive = new JointDrive { positionSpring = elbowSpring, positionDamper = elbowDamp, maximumForce = float.MaxValue };
                    joint.slerpDrive = drive;
                    joint.angularXDrive = drive;
                    joint.angularYZDrive = drive;
                }
            }
        }

        #endregion
    }
}
