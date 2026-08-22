using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Mirror;
using UnityEngine;
// Pour PlayerInputHandler

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

    [Header("Animation")]
    [Tooltip("Role: Durée de la rotation des épaules.\nUse Case: Animation.")]
    public float ShoulderRotateDuration = 0.25f; // Réduit pour être plus vif et moins bégayer avec la physique
    [Tooltip("Role: Type d'easing pour l'animation.\nUse Case: Fluidité.")]
    public Ease ShoulderEase = Ease.OutQuad; // OutQuad au lieu de OutBack pour éviter le rebond (jitter)

    [Header("Crouch Retraction")]
    [Tooltip("Combien de segments ignorer au début du bras (0 = l'épaule est le premier affecté, 1 = on ignore le premier joint).")]
    public int RetractedSegmentsOffset = 1;
    [Tooltip("Combien de segments doivent se rétracter après l'offset.")]
    public int RetractedSegmentsCount = 2;
    [Tooltip("Durée de l'animation de rétraction en secondes.")]
    public float RetractionDuration = 0.25f;

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

    private bool _isRetracted = false;
    private Coroutine _leftRetractionCoroutine;
    private Coroutine _rightRetractionCoroutine;
    private Dictionary<ConfigurableJoint, Vector3> _originalConnectedAnchors = new Dictionary<ConfigurableJoint, Vector3>();

    // References
    private PlayerV2_Controller _controller;
    private PlayerInputHandler _input;

    private Transform _leftHand;
    private Rigidbody _leftHandRb;
    private Transform _rightHand;
    private Rigidbody _rightHandRb;

    private List<Rigidbody> _leftArmRbs = new List<Rigidbody>();
    private List<Rigidbody> _rightArmRbs = new List<Rigidbody>();

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
        // 1. Resolve and cache joints
        if (_controller.LeftArmRoot != null)
        {
            // Utilise GetComponentInChildren au cas où le script est configuré sur Shoulder (qui n'a pas de joint) au lieu de Arm.Base
            _leftShoulderJoint = _controller.LeftArmRoot.GetComponentInChildren<ConfigurableJoint>();
            if (_leftShoulderJoint != null)
            {
                foreach (var joint in _controller.LeftArmRoot.GetComponentsInChildren<ConfigurableJoint>(true))
                {
                    if (joint != _leftShoulderJoint) _leftElbowWristJoints.Add(joint);
                }
            }
        }

        if (_controller.RightArmRoot != null)
        {
            // Utilise GetComponentInChildren au cas où le script est configuré sur Shoulder (qui n'a pas de joint) au lieu de Arm.Base
            _rightShoulderJoint = _controller.RightArmRoot.GetComponentInChildren<ConfigurableJoint>();
            if (_rightShoulderJoint != null)
            {
                foreach (var joint in _controller.RightArmRoot.GetComponentsInChildren<ConfigurableJoint>(true))
                {
                    if (joint != _rightShoulderJoint) _rightElbowWristJoints.Add(joint);
                }
            }
        }

        // Initialisation Bras Gauche
        if (_controller.LeftArmRoot != null)
        {
            _leftArmRbs.AddRange(_controller.LeftArmRoot.GetComponentsInChildren<Rigidbody>());
            _leftHand = FindLastChild(_controller.LeftArmRoot);
            if (_leftHand != null)
            {
                _leftHandRb = _leftHand.GetComponent<Rigidbody>();
                _leftArmLength = CalculateHierarchyLength(_controller.LeftArmRoot);
            }
        }

        // Initialisation Bras Droit
        if (_controller.RightArmRoot != null)
        {
            _rightArmRbs.AddRange(_controller.RightArmRoot.GetComponentsInChildren<Rigidbody>());
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
        if (!isOwned) return;

        bool leftInput = _input.LeftArmPressed;
        bool rightInput = _input.RightArmPressed;

        if (_input.IsVacuuming)
        {
            leftInput = false;
            rightInput = false;
        }

        if (leftInput != _isLeftArmExtended)
        {
            // Prédiction locale pour supprimer la saccade réseau !
            OnLeftArmStateChanged(_isLeftArmExtended, leftInput);
            _isLeftArmExtended = leftInput;
            CmdSetLeftArmExtended(leftInput);
        }

        if (rightInput != _isRightArmExtended)
        {
            // Prédiction locale pour supprimer la saccade réseau !
            OnRightArmStateChanged(_isRightArmExtended, rightInput);
            _isRightArmExtended = rightInput;
            CmdSetRightArmExtended(rightInput);
        }
    }

    private void FixedUpdate()
    {
        // Bras Gauche
        if (_leftArmRbs.Count > 0 && _leftHandRb != null)
        {
            if (_isLeftArmExtended != _lastLeftExtended)
            {
                UpdateJointDrives(true, _isLeftArmExtended);
                _lastLeftExtended = _isLeftArmExtended;
            }
            ApplyArmPhysicsForces(_leftArmRbs, _leftHandRb, _leftArmLength, true, _isLeftArmExtended);
        }

        // Bras Droit
        if (_rightArmRbs.Count > 0 && _rightHandRb != null)
        {
            if (_isRightArmExtended != _lastRightExtended)
            {
                UpdateJointDrives(false, _isRightArmExtended);
                _lastRightExtended = _isRightArmExtended;
            }
            ApplyArmPhysicsForces(_rightArmRbs, _rightHandRb, _rightArmLength, false, _isRightArmExtended);
        }
    }

    private void ApplyArmPhysicsForces(List<Rigidbody> armRbs, Rigidbody handRb, float armLength, bool isLeft, bool isExtended)
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

            Vector3 finalTargetPosition = headTrans.position
                + forward * (armLength * ReachLengthFactor + ForwardOffset)
                + up * VerticalOffset;

            Vector3 armRootPos = armRbs[0].position; // La base de l'épaule

            // Force d'attraction répartie sur chaque segment pour une belle courbe et aucun à-coup
            for (int i = 0; i < armRbs.Count; i++)
            {
                Rigidbody rb = armRbs[i];
                if (rb == null) continue;

                float weight = (float)(i + 1) / armRbs.Count; // 0.x à 1.0 (main)

                // Rétractation : On ignore les segments repliés
                if (_isRetracted)
                {
                    int retractedEnd = Mathf.Min(RetractedSegmentsOffset + RetractedSegmentsCount, armRbs.Count);
                    if (i < retractedEnd)
                    {
                        weight = 0f;
                    }
                    else
                    {
                        float range = armRbs.Count - retractedEnd;
                        weight = range > 0 ? Mathf.Lerp(0.2f, 1f, (float)(i - retractedEnd + 1) / range) : 1f;
                    }
                }

                if (weight > 0f)
                {
                    // FIX VIBRATION : Au lieu de tirer tous les segments vers le même point (ce qui les écrase les uns dans les autres),
                    // on tire chaque segment vers sa place "naturelle" sur la ligne imaginaire du bras tendu.
                    Vector3 segmentTargetPos = Vector3.Lerp(armRootPos, finalTargetPosition, weight);

                    Vector3 toTarget = segmentTargetPos - rb.position;
                    Vector3 extensionForce = toTarget * (ExtendForce * weight);
                    Vector3 dampingForce = -rb.linearVelocity * (ExtendDamping * weight);
                    Vector3 netForce = (extensionForce + dampingForce) * rb.mass;
                    rb.AddForce(netForce, ForceMode.Force);
                }
            }

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

            // On sauvegarde le connectedAnchor correctement calculé avant de désactiver l'auto-configure
            if (joint.autoConfigureConnectedAnchor)
            {
                // Calcul manuel du connectedAnchor exact basé sur la pose initiale pour éviter les bugs au spawn
                Vector3 worldAnchor = joint.transform.TransformPoint(joint.anchor);
                Vector3 connectedAnchor = joint.connectedBody != null ? joint.connectedBody.transform.InverseTransformPoint(worldAnchor) : worldAnchor;

                joint.autoConfigureConnectedAnchor = false;
                joint.connectedAnchor = connectedAnchor;
                _originalConnectedAnchors[joint] = connectedAnchor;
            }
            else
            {
                _originalConnectedAnchors[joint] = joint.connectedAnchor;
            }

            // CRITICAL FIX: Empêcher les bras de pousser/tourner le Torso !
            // En mettant le connectedMassScale à une valeur minuscule sur l'épaule, le solver Unity 
            // considère que le Torso a une masse infinie par rapport au bras. 
            // Le bras peut bouger, mais il ne pourra plus JAMAIS faire bouger le Torso !
            if (joint == _leftShoulderJoint || joint == _rightShoulderJoint)
            {
                joint.connectedMassScale = 0.00001f;
            }

            if (LockAngularX) joint.angularXMotion = ConfigurableJointMotion.Locked;

            if (EnableJointProjection)
            {
                joint.projectionMode = JointProjectionMode.PositionAndRotation;
                // FIX: Des valeurs trop faibles causent des forces de téléportation infinies (explosion physique)
                joint.projectionDistance = 0.1f; // 10cm de tolérance (Unity par défaut)
                joint.projectionAngle = 180f; // 180 degrés de tolérance (Unity par défaut)
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

        // --- Zéro Friction Setup ---
        PhysicsMaterial zeroFrictionMaterial = new PhysicsMaterial("ZeroFrictionArmMaterial");
        zeroFrictionMaterial.dynamicFriction = 0f;
        zeroFrictionMaterial.staticFriction = 0f;
        zeroFrictionMaterial.frictionCombine = PhysicsMaterialCombine.Minimum;

        List<Collider> allColliders = new List<Collider>();
        if (_controller.LeftArmRoot != null) allColliders.AddRange(_controller.LeftArmRoot.GetComponentsInChildren<Collider>(true));
        if (_controller.RightArmRoot != null) allColliders.AddRange(_controller.RightArmRoot.GetComponentsInChildren<Collider>(true));

        foreach (var col in allColliders)
        {
            if (col != null) col.sharedMaterial = zeroFrictionMaterial;
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

    #region Crouch Retraction

    public void SetArmRetraction(bool isRetracted)
    {
        if (_isRetracted == isRetracted) return;
        _isRetracted = isRetracted;

        if (_leftRetractionCoroutine != null) StopCoroutine(_leftRetractionCoroutine);
        if (_rightRetractionCoroutine != null) StopCoroutine(_rightRetractionCoroutine);

        _leftRetractionCoroutine = StartCoroutine(AnimateArmRetraction(_leftElbowWristJoints, isRetracted));
        _rightRetractionCoroutine = StartCoroutine(AnimateArmRetraction(_rightElbowWristJoints, isRetracted));
    }

    private IEnumerator AnimateArmRetraction(List<ConfigurableJoint> joints, bool isRetracted)
    {
        // Sécurité des bornes
        int startIndex = Mathf.Clamp(RetractedSegmentsOffset, 0, joints.Count);
        int endIndex = Mathf.Min(startIndex + RetractedSegmentsCount, joints.Count);
        int countToAnimate = endIndex - startIndex;

        if (countToAnimate <= 0) yield break;

        float time = 0f;

        // Stocker les positions de départ pour l'interpolation
        Vector3[] startAnchors = new Vector3[countToAnimate];
        Vector3[] targetAnchors = new Vector3[countToAnimate];

        for (int i = 0; i < countToAnimate; i++)
        {
            var joint = joints[startIndex + i];
            if (joint != null && _originalConnectedAnchors.ContainsKey(joint))
            {
                startAnchors[i] = joint.connectedAnchor;
                targetAnchors[i] = isRetracted ? Vector3.zero : _originalConnectedAnchors[joint];
            }
        }

        while (time < RetractionDuration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / RetractionDuration);

            // Un peu de easing basique (SmoothStep)
            float smoothT = t * t * (3f - 2f * t);

            for (int i = 0; i < countToAnimate; i++)
            {
                var joint = joints[startIndex + i];
                if (joint != null)
                {
                    joint.connectedAnchor = Vector3.Lerp(startAnchors[i], targetAnchors[i], smoothT);
                }
            }
            yield return null;
        }

        // S'assurer d'atteindre la valeur finale exactement
        for (int i = 0; i < countToAnimate; i++)
        {
            var joint = joints[startIndex + i];
            if (joint != null)
            {
                joint.connectedAnchor = targetAnchors[i];
            }
        }
    }

    #endregion
}

