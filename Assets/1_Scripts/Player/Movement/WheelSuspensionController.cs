using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace VacuumProtocol.Player
{
    /// <summary>
    /// Description: Controls procedural 4-wheel raycast suspension physics, ground elevation push forces, visual scene gizmos, and jump takeoff/landing dynamics.
    /// Context: Attached to the root player prefab alongside PlayerController, PlayerJumpComponent, and PlayerLookComponent.
    /// Justification: Uses Raycast Spring Suspension with mathematically critical damping, elevated raycast origins, and configurable base height offset.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PlayerInputHandler))]
    public class WheelSuspensionController : NetworkBehaviour
    {
        [Header("Wheel References")]
        [Tooltip("Role: The root transform containing the 4 wheel GameObjects.\nUse Case: Wheel auto-discovery.\nJustification: Discovers child wheel transforms to animate visual extension/compression.")]
        [SerializeField] private Transform _wheelsRoot;

        [Tooltip("Role: Explicit references to the 4 wheel Transforms.\nUse Case: Visual animation.\nJustification: Driven procedurally to match ground distance without physical joint friction.")]
        [SerializeField] private List<Transform> _wheelTransforms = new List<Transform>();

        [Header("Suspension Travel Parameters")]
        [Tooltip("Role: Base height offset shift applied to all suspension mount points and calculations.\nUse Case: Origin pivot correction.\nJustification: Allows shifting the suspension baseline higher or lower if the model origin pivot is offset.")]
        [SerializeField] private float _baseHeightOffset = 0f;

        [Tooltip("Role: Maximum distance wheels can extend downwards along local Y axis from their minimal (highest/retracted) baseline position.\nUse Case: Suspension travel range.\nJustification: Defines how far wheels drop below the body. Default position in editor is minimal (0).")]
        [SerializeField, Range(0.02f, 1.0f)] private float _maxSuspensionDistance = 0.422f;

        [Tooltip("Role: Target rest extension distance for normal floating height during gameplay.\nUse Case: Resting height.\nJustification: Pushes the body upward so it physically sits elevated on its wheels.")]
        [SerializeField, Range(0.01f, 0.8f)] private float _restExtensionDistance = 0.097f;

        [Tooltip("Role: Stiffness force of the suspension springs.\nUse Case: Push force.\nJustification: Higher values create stiffer suspension that holds the body elevated.")]
        [SerializeField] private float _springStiffness = 500f;

        [Tooltip("Role: Enable automatic critical damping based on body mass and spring stiffness.\nUse Case: Anti-bounce guarantee.\nJustification: Calculates exact mathematical damping c = 2*sqrt(m*k) to stop infinite bouncing.")]
        [SerializeField] private bool _useAutoCriticalDamping = true;

        [Tooltip("Role: Damping multiplier applied to critical damping or manual damping.\nUse Case: Shock absorption tuning.\nJustification: Values around 1.0-1.5 create critically damped suspension with zero bounce.")]
        [SerializeField, Range(0.2f, 3.0f)] private float _dampingMultiplier = 1.2f;

        [Tooltip("Role: Manual damping of suspension springs when auto critical damping is disabled.\nUse Case: Manual tuning.\nJustification: Used if _useAutoCriticalDamping is set to false.")]
        [SerializeField] private float _springDamping = 120f;

        [Header("Jump Takeoff Dynamics")]
        [Tooltip("Role: Delay before retracting wheels during jump takeoff.\nUse Case: Suspension lag visual effect.\nJustification: Allows wheels to lag behind in place briefly before being pulled up.")]
        [SerializeField] private float _jumpRetractDelay = 0.12f;

        [Header("Grounding & Spawn Settings")]
        [Tooltip("Role: Raycast distance below wheels to detect ground.\nUse Case: Ground check.\nJustification: Determines whether player is grounded for jumping.")]
        [SerializeField, Range(0.05f, 1.0f)] private float _groundCheckDistance = 0.20f;

        [Tooltip("Role: Height offset above mount point where raycasts originate.\nUse Case: Spawn clipping prevention.\nJustification: Ensures ground is detected even if the robot spawns directly on the floor.")]
        [SerializeField] private float _rayStartUpOffset = 0.5f;

        [Tooltip("Role: LayerMask for ground detection.\nUse Case: Raycasting.\nJustification: Excludes player body colliders from ground checks.")]
        [SerializeField] private LayerMask _groundLayer = ~0;

        [Header("Visual Smoothing")]
        [Tooltip("Role: Speed at which visual wheels Lerp to target Y extension.\nUse Case: Visual smoothing.\nJustification: Creates fluid visual movement for wheel suspension.")]
        [SerializeField] private float _visualLerpSpeed = 25f;

        [Header("Debug & Gizmos")]
        [Tooltip("Role: Enable visual debug gizmos in Scene view.\nUse Case: Visualization.\nJustification: Shows ground check rays, max suspension travel bounds, and pyramid connections.")]
        [SerializeField] private bool _drawGizmos = true;

        [Tooltip("Role: Only draw gizmos when GameObject is actively selected in Scene/Hierarchy.\nUse Case: Visualization preference.\nJustification: Allows toggling between continuous gizmos in Edit Mode vs selection-only.")]
        [SerializeField] private bool _drawGizmosOnlyWhenSelected = true;

        [Tooltip("Role: Enable detailed suspension logging.\nUse Case: Debugging physics.\nJustification: Outputs wheel forces and ground status to console.")]
        [SerializeField] private bool _enableDebugLogs = false;

        private Rigidbody _bodyRb;
        private bool _isGrounded;
        private bool _isJumping;
        private readonly List<Vector3> _initialWheelLocalPos = new List<Vector3>();
        private readonly List<float> _currentWheelTargetY = new List<float>();
        private readonly List<float> _animatedWheelY = new List<float>();
        private readonly HashSet<Collider> _playerColliders = new HashSet<Collider>();

        /// <summary>
        /// Description: Gets whether at least one wheel is touching the ground.
        /// </summary>
        public bool IsGrounded => _isGrounded;

        /// <summary>
        /// Description: Gets the list of registered wheel Transforms.
        /// </summary>
        public IReadOnlyList<Transform> WheelTransforms => _wheelTransforms;

        /// <summary>
        /// Description: Awake callback. Caches root body Rigidbody, excludes player colliders, and discovers wheel transforms.
        /// Context: Lifecycle event.
        /// </summary>
        private void Awake()
        {
            _bodyRb = GetComponent<Rigidbody>();
            CachePlayerColliders();
            DiscoverAndSetupWheels();
        }

        /// <summary>
        /// Description: OnValidate callback. Clamps rest distance to max travel in Editor.
        /// Context: Editor inspector edit event.
        /// </summary>
        private void OnValidate()
        {
            if (_restExtensionDistance > _maxSuspensionDistance)
            {
                _restExtensionDistance = _maxSuspensionDistance;
            }
        }

        /// <summary>
        /// Description: Caches all colliders in the player hierarchy to exclude them from ground raycasts.
        /// Context: Called on Awake.
        /// </summary>
        private void CachePlayerColliders()
        {
            _playerColliders.Clear();
            Collider[] cols = transform.root.GetComponentsInChildren<Collider>(true);
            foreach (Collider col in cols)
            {
                _playerColliders.Add(col);
            }
        }

        /// <summary>
        /// Description: Finds wheel Transforms under _wheelsRoot and removes any child Joint or Rigidbody components to prevent nested physics conflicts.
        /// Context: Called on Awake to ensure SSOT setup.
        /// Justification: Guarantees wheels are pure Transforms without extra Rigidbodies or Joints competing with PlayerLookComponent or WheelSteering.
        /// </summary>
        public void DiscoverAndSetupWheels()
        {
            _wheelTransforms.Clear();
            _initialWheelLocalPos.Clear();
            _currentWheelTargetY.Clear();
            _animatedWheelY.Clear();

            if (_wheelsRoot != null)
            {
                // Clean up any ConfigurableJoints or Rigidbodies on child wheel objects
                ConfigurableJoint[] jointsInRoot = _wheelsRoot.GetComponentsInChildren<ConfigurableJoint>(true);
                foreach (ConfigurableJoint joint in jointsInRoot)
                {
                    Transform t = joint.transform;
                    if (t != null && !_wheelTransforms.Contains(t))
                    {
                        _wheelTransforms.Add(t);
                    }
                    Destroy(joint);
                }

                Rigidbody[] rbs = _wheelsRoot.GetComponentsInChildren<Rigidbody>(true);
                foreach (Rigidbody rb in rbs)
                {
                    if (rb.transform != transform && !_wheelTransforms.Contains(rb.transform))
                    {
                        _wheelTransforms.Add(rb.transform);
                    }
                    if (rb.transform != transform)
                    {
                        Destroy(rb);
                    }
                }

                // Fallback: child transforms under wheels root
                if (_wheelTransforms.Count == 0)
                {
                    foreach (Transform child in _wheelsRoot)
                    {
                        if (child != null && !_wheelTransforms.Contains(child))
                        {
                            _wheelTransforms.Add(child);
                        }
                    }
                }
            }

            // Cache initial local positions (baseline minimal position 0)
            foreach (Transform wheel in _wheelTransforms)
            {
                if (wheel == null) continue;

                // Ensure wheel colliders are removed so they never scrape the floor or interfere
                Collider col = wheel.GetComponent<Collider>();
                if (col != null)
                {
                    Destroy(col);
                }

                _initialWheelLocalPos.Add(wheel.localPosition);
                _currentWheelTargetY.Add(0f);
                _animatedWheelY.Add(0f);
            }

            if (_enableDebugLogs)
            {
                Debug.Log($"<color=cyan>[WheelSuspensionController] Configured {_wheelTransforms.Count} pure Transform wheels for procedural Raycast suspension.</color>");
            }
        }

        /// <summary>
        /// Description: FixedUpdate callback. Evaluates Raycast suspension spring forces on the root Rigidbody.
        /// Context: Physics lifecycle loop. Executes for local player or server.
        /// </summary>
        private void FixedUpdate()
        {
            if (isServer || isLocalPlayer)
            {
                EvaluateRaycastSuspensionPhysics();
            }
        }

        /// <summary>
        /// Description: Update callback. Smoothly animates visual wheel local Y positions without altering Y rotation.
        /// Context: Visual update loop.
        /// </summary>
        private void Update()
        {
            AnimateVisualWheels();
        }

        /// <summary>
        /// Description: Performs downward raycasts under each wheel mount point (including _baseHeightOffset), calculates critically damped spring compression, and applies upward forces on main body Rigidbody.
        /// Context: Called during FixedUpdate.
        /// Justification: Provides critically damped suspension elevation (zero bounce) and pops player up if spawned on ground.
        /// </summary>
        private void EvaluateRaycastSuspensionPhysics()
        {
            int groundedCount = 0;
            float totalRayLength = _maxSuspensionDistance + _groundCheckDistance;
            int wheelCount = Mathf.Max(1, _wheelTransforms.Count);

            // Calculate critical damping: c_crit = 2 * sqrt(m * k)
            float massPerWheel = _bodyRb.mass / wheelCount;
            float criticalDamping = 2f * Mathf.Sqrt(massPerWheel * _springStiffness);
            float effectiveDamping = _useAutoCriticalDamping ? criticalDamping * _dampingMultiplier : _springDamping * _dampingMultiplier;

            for (int i = 0; i < _wheelTransforms.Count; i++)
            {
                Transform wheel = _wheelTransforms[i];
                if (wheel == null) continue;

                Vector3 initialLocalPos = i < _initialWheelLocalPos.Count ? _initialWheelLocalPos[i] : wheel.localPosition;
                Vector3 mountWorldPos = transform.TransformPoint(initialLocalPos + Vector3.up * _baseHeightOffset);

                bool hitGround = CheckRaycastGround(mountWorldPos, totalRayLength, out float distanceToGround, out RaycastHit hit);

                if (hitGround && distanceToGround <= totalRayLength)
                {
                    groundedCount++;

                    // Compression relative to target rest distance
                    float compression = _restExtensionDistance - distanceToGround;

                    // Calculate point velocity along vertical axis
                    Vector3 pointVel = _bodyRb.GetPointVelocity(mountWorldPos);
                    float verticalVel = Vector3.Dot(pointVel, Vector3.up);

                    // Calculate spring & damping forces
                    float springForce = compression > 0f ? compression * _springStiffness : 0f;
                    float damperForce = verticalVel * effectiveDamping;
                    float netForce = springForce - damperForce;

                    netForce = Mathf.Max(0f, netForce); // Pure upward spring force

                    // Apply upward spring force directly to single root Rigidbody
                    _bodyRb.AddForceAtPosition(Vector3.up * netForce, mountWorldPos, ForceMode.Force);

                    if (!_isJumping)
                    {
                        float targetY = Mathf.Clamp(distanceToGround, 0f, _maxSuspensionDistance);
                        _currentWheelTargetY[i] = targetY;
                    }

                    if (_enableDebugLogs)
                    {
                        Debug.DrawLine(mountWorldPos, hit.point, Color.green);
                    }
                }
                else
                {
                    if (!_isJumping)
                    {
                        _currentWheelTargetY[i] = _maxSuspensionDistance;
                    }

                    if (_enableDebugLogs)
                    {
                        Debug.DrawLine(mountWorldPos, mountWorldPos + Vector3.down * totalRayLength, Color.red);
                    }
                }
            }

            _isGrounded = groundedCount > 0;
        }

        /// <summary>
        /// Description: Performs a raycast downward starting above the mount point to prevent ground spawn clipping.
        /// Context: Called by EvaluateRaycastSuspensionPhysics and Gizmos.
        /// </summary>
        private bool CheckRaycastGround(Vector3 mountOrigin, float maxDistanceBelow, out float distanceToMount, out RaycastHit hitResult)
        {
            hitResult = default;
            distanceToMount = float.MaxValue;

            Vector3 rayStart = mountOrigin + Vector3.up * _rayStartUpOffset;
            float totalRayLength = maxDistanceBelow + _rayStartUpOffset;

            RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, totalRayLength, _groundLayer, QueryTriggerInteraction.Ignore);
            
            float minHitDistance = float.MaxValue;
            bool foundGround = false;

            foreach (RaycastHit hit in hits)
            {
                if (_playerColliders.Contains(hit.collider)) continue;

                if (hit.distance < minHitDistance)
                {
                    minHitDistance = hit.distance;
                    hitResult = hit;
                    foundGround = true;
                }
            }

            if (foundGround)
            {
                distanceToMount = minHitDistance - _rayStartUpOffset;
            }

            return foundGround;
        }

        /// <summary>
        /// Description: Smoothly updates visual wheel local Y position while including _baseHeightOffset and preserving X, Z local positions and Y rotation (WheelSteering).
        /// Context: Called during Update.
        /// </summary>
        private void AnimateVisualWheels()
        {
            for (int i = 0; i < _wheelTransforms.Count; i++)
            {
                Transform wheel = _wheelTransforms[i];
                if (wheel == null || i >= _initialWheelLocalPos.Count || i >= _currentWheelTargetY.Count) continue;

                Vector3 baseLocalPos = _initialWheelLocalPos[i];
                float targetYOffset = _currentWheelTargetY[i];

                _animatedWheelY[i] = Mathf.Lerp(_animatedWheelY[i], targetYOffset, Time.deltaTime * _visualLerpSpeed);

                // CRITICAL: Include _baseHeightOffset in localPosition.y! Preserve local X and Z positions, and NEVER modify localRotation!
                wheel.localPosition = new Vector3(baseLocalPos.x, baseLocalPos.y + _baseHeightOffset - _animatedWheelY[i], baseLocalPos.z);
            }
        }

        /// <summary>
        /// Description: Triggers the suspension takeoff sequence during a jump.
        /// Context: Called by PlayerJumpComponent when jump force is applied.
        /// Justification: Keeps wheels extended downward briefly during takeoff, then retracts them smoothly mid-air.
        /// </summary>
        public void TriggerJumpTakeoff()
        {
            if (!gameObject.activeInHierarchy) return;

            StopAllCoroutines();
            StartCoroutine(RoutineJumpSuspensionSequence());
        }

        /// <summary>
        /// Description: Coroutine handling suspension visual targets during jump takeoff and airtime.
        /// Context: Invoked by TriggerJumpTakeoff.
        /// Justification: Holds wheels extended downward during takeoff lag and mid-air trajectory for a dramatic leg stretch effect.
        /// </summary>
        private IEnumerator RoutineJumpSuspensionSequence()
        {
            _isJumping = true;

            // Phase 1: Hold wheels fully extended downward (takeoff lag)
            for (int i = 0; i < _currentWheelTargetY.Count; i++)
            {
                _currentWheelTargetY[i] = _maxSuspensionDistance;
            }

            yield return new WaitForSeconds(_jumpRetractDelay);

            // Phase 2: Maintain stretched wheel extension in mid-air until landing
            for (int i = 0; i < _currentWheelTargetY.Count; i++)
            {
                _currentWheelTargetY[i] = _maxSuspensionDistance;
            }

            // Phase 3: Wait until grounded again
            while (!_isGrounded)
            {
                yield return null;
            }

            _isJumping = false;
        }

        /// <summary>
        /// Description: Retrieves wheel transforms for Gizmo rendering in both Edit Mode and Play Mode.
        /// Context: Gizmo drawing helper.
        /// Justification: In Edit Mode before Awake() runs, _wheelTransforms may be empty if wheels are dynamically discovered under _wheelsRoot.
        /// </summary>
        private List<Transform> GetCandidateWheelTransforms()
        {
            if (_wheelTransforms != null && _wheelTransforms.Count > 0)
            {
                return _wheelTransforms;
            }

            List<Transform> candidateWheels = new List<Transform>();
            if (_wheelsRoot != null)
            {
                foreach (Transform child in _wheelsRoot)
                {
                    if (child != null)
                    {
                        candidateWheels.Add(child);
                    }
                }
            }

            return candidateWheels;
        }

        /// <summary>
        /// Description: OnDrawGizmos callback. Visualizes suspension bounds in Scene view when continuous drawing is enabled.
        /// Context: Unity Editor Gizmos callback.
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!_drawGizmos || _drawGizmosOnlyWhenSelected) return;
            DrawSuspensionGizmos();
        }

        /// <summary>
        /// Description: OnDrawGizmosSelected callback. Visualizes suspension bounds when GameObject is selected.
        /// Context: Unity Editor Gizmos callback.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!_drawGizmos || !_drawGizmosOnlyWhenSelected) return;
            DrawSuspensionGizmos();
        }

        /// <summary>
        /// Description: Core gizmo drawing implementation for wheel suspension bounds, rest height, ground check rays, and pyramid connections.
        /// Context: Gizmo drawing helper.
        /// </summary>
        private void DrawSuspensionGizmos()
        {
            Transform bodyTransform = transform;
            Vector3 bodyPos = bodyTransform.position;
            List<Transform> wheelsToDraw = GetCandidateWheelTransforms();

            for (int i = 0; i < wheelsToDraw.Count; i++)
            {
                Transform wheel = wheelsToDraw[i];
                if (wheel == null) continue;

                Vector3 initialLocalPos = (Application.isPlaying && i < _initialWheelLocalPos.Count) ? _initialWheelLocalPos[i] : wheel.localPosition;
                Vector3 baselineMountPos = bodyTransform.TransformPoint(initialLocalPos + Vector3.up * _baseHeightOffset);

                // 1. Pyramid structure line from body center to baseline mount point (Yellow)
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(bodyPos, baselineMountPos);

                // 2. Baseline minimal position marker (White dot)
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(baselineMountPos, 0.02f);

                // 3. Rest Extension Height (Magenta box & line)
                Gizmos.color = Color.magenta;
                Vector3 restLimitPos = baselineMountPos + Vector3.down * _restExtensionDistance;
                Gizmos.DrawLine(baselineMountPos, restLimitPos);
                Gizmos.DrawWireCube(restLimitPos, new Vector3(0.12f, 0.01f, 0.12f));

                // 4. Max Suspension Travel Distance (Cyan box & line)
                Gizmos.color = Color.cyan;
                Vector3 maxLimitPos = baselineMountPos + Vector3.down * _maxSuspensionDistance;
                Gizmos.DrawLine(restLimitPos, maxLimitPos);
                Gizmos.DrawWireCube(maxLimitPos, new Vector3(0.18f, 0.01f, 0.18f));

                // 5. Ground Check raycast from wheel center (Green if grounded, Red if airborne)
                bool grounded = Application.isPlaying ? CheckRaycastGround(baselineMountPos, _maxSuspensionDistance + _groundCheckDistance, out _, out _) : false;
                Gizmos.color = grounded ? Color.green : Color.red;
                Vector3 rayEnd = baselineMountPos + Vector3.down * (_maxSuspensionDistance + _groundCheckDistance);
                Gizmos.DrawLine(baselineMountPos, rayEnd);
                Gizmos.DrawWireSphere(rayEnd, 0.03f);
            }
        }
    }
}
