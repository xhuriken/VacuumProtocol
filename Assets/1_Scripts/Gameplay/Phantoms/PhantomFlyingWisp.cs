using Sirenix.OdinInspector;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Gameplay.Phantoms
{
    /// <summary>
    /// Description: Small 3D aerial phantom (Wasp / Flying Wisp) with freeform flight navigation.
    /// Context: Runs server-side in Mirror multiplayer. Spawned from ceiling, wall, or floor portals by PhantomRoomSpawner.
    /// Justification: Implements agile 3D aerial pursuit without NavMesh dependencies, featuring feeler-ray obstacle avoidance,
    /// dynamic hover height adjustment, ethereal flight bobbing, and low ectoplasm (35 PV) for snappy 3.5s mouth vacuum captures.
    /// </summary>
    [SelectionBase]
    public class PhantomFlyingWisp : PhantomBase
    {
        #region Flight Configuration
        [Title("3D Aerial Flight Speeds")]
        [Tooltip("Role: Movement speed when actively pursuing a player.")]
        [SerializeField] private float _flightChaseSpeed = 4.0f;

        [Tooltip("Role: Movement speed when drifting or wandering around.")]
        [SerializeField] private float _flightWanderSpeed = 2.2f;

        [Tooltip("Role: Rotation and banking smoothing speed.")]
        [SerializeField] private float _turnSpeed = 7.0f;

        [Tooltip("Role: Distance threshold to consider a destination reached.")]
        [SerializeField] private float _stoppingDistance = 0.8f;

        [Title("Hover Altitude Maintenance")]
        [Tooltip("Role: Preferred hover altitude above the floor/obstacles directly below.")]
        [SerializeField] private float _targetHoverAltitude = 1.6f;

        [Tooltip("Role: Minimum permitted altitude above floor before forced upward thrust.")]
        [SerializeField] private float _minHoverAltitude = 1.0f;

        [Tooltip("Role: Maximum permitted altitude above floor.")]
        [SerializeField] private float _maxHoverAltitude = 2.8f;

        [Tooltip("Role: Layers considered ground or walkable surfaces for height tracking.")]
        [SerializeField] private LayerMask _groundLayer;

        [Title("Flight Oscillation & Game Feel")]
        [Tooltip("Role: Speed/frequency of vertical sinusoidal flight bobbing.")]
        [SerializeField] private float _bobFrequency = 3.5f;

        [Tooltip("Role: Amplitude of vertical flight bobbing in meters.")]
        [SerializeField] private float _bobAmplitude = 0.22f;

        [Tooltip("Role: Frequency of lateral buzzing weave.")]
        [SerializeField] private float _weaveFrequency = 2.5f;

        [Tooltip("Role: Amplitude of lateral buzzing weave in meters.")]
        [SerializeField] private float _weaveAmplitude = 0.18f;

        [Title("Feeler Ray Obstacle Avoidance")]
        [Tooltip("Role: Distance ahead to cast feeler rays to detect walls, doors, and ceilings.")]
        [SerializeField] private float _wallAvoidanceDistance = 1.5f;

        [Tooltip("Role: Push strength applied perpendicular to detected obstacles.")]
        [SerializeField] private float _avoidanceForce = 4.5f;

        [Title("Mouth Suction Pull")]
        [Tooltip("Role: Speed at which this lightweight wisp is dragged into the player's mouth nozzle while being vacuumed.")]
        [SerializeField] private float _vacuumDragSpeed = 1.8f;
        #endregion

        #region Runtime Variables
        private Vector3 _currentDestination;
        private Vector3 _currentVelocity;
        private float _phaseOffset;
        private bool _isDestinationSet = false;
        #endregion

        #region Unity Lifecycle
        private void Reset()
        {
            // Preset small flying wisp parameters
            Name = "Phantom Wasp";
            PriorityLevel = 3;
            _maxEctoplasm = 35f;
            _currentEctoplasm = 35f;
            _suctionSlowdownRatio = 0.2f;
            _struggleResistance = 0.25f;
            _escapeDashForce = 4.5f;
            _escapeIntervalMin = 0.9f;
            _escapeIntervalMax = 1.8f;
            _swallowThresholdRatio = 0.25f;

            _fieldOfViewAngle = 135f;
            _detectionRadius = 12f;
            _loseTargetRadius = 18f;
            _attackRadius = 1.2f;
            _attackCooldown = 1.8f;

            _wanderRadius = 8f;
            _wanderWaitTime = 1.5f;

            _proximityDrainRadius = 3.5f;
            _maxDrainRate = 8.0f;
            _attackDamage = 10.0f;

            _groundLayer = LayerMask.GetMask("Ground", "Default");
            _obstacleLayer = LayerMask.GetMask("Default", "Ground", "Obstacle", "Obstable");
        }

        protected override void Awake()
        {
            base.Awake();

            _phaseOffset = Random.Range(0f, 100f);

            if (_groundLayer.value == 0)
            {
                _groundLayer = LayerMask.GetMask("Ground", "Default");
                if (_groundLayer.value == 0) _groundLayer = (1 << 0) | (1 << 3);
            }

            if (_obstacleLayer.value == 0)
            {
                _obstacleLayer = LayerMask.GetMask("Default", "Ground", "Obstacle", "Obstable");
                if (_obstacleLayer.value == 0) _obstacleLayer = (1 << 0) | (1 << 3) | (1 << 8);
            }
        }
        #endregion

        #region Movement & Navigation Implementation
        /// <summary>
        /// Description: Directs the flying wisp towards a 3D destination with obstacle avoidance,
        /// hover altitude adjustment, and organic undulation.
        /// </summary>
        protected override void MoveTowards(Vector3 destination)
        {
            if (!isServer) return;

            _currentDestination = destination;
            _isDestinationSet = true;

            // 1. Calculate base flight speed depending on state
            float speed = (_currentState == PhantomState.Chasing) ? _flightChaseSpeed : _flightWanderSpeed;
            if (_currentState == PhantomState.BeingVacuumed)
            {
                speed *= _suctionSlowdownRatio;
            }

            // 2. Compute desired approach vector
            Vector3 toDestination = destination - transform.position;
            Vector3 desiredDir = toDestination.sqrMagnitude > 0.01f ? toDestination.normalized : Vector3.zero;

            // 3. Keep target hover altitude above floor
            float targetHeightY = CalculateTargetHeight();
            float verticalCorrection = (targetHeightY - transform.position.y) * 2.5f;

            // 4. Calculate obstacle avoidance vector from feeler rays
            Vector3 avoidanceVector = CalculateFeelerAvoidance();

            // 5. Calculate organic sinusoidal weave (bobbing & buzzing)
            float time = Time.time + _phaseOffset;
            Vector3 organicWeave = transform.up * (Mathf.Sin(time * _bobFrequency) * _bobAmplitude) +
                                   transform.right * (Mathf.Cos(time * _weaveFrequency) * _weaveAmplitude);

            // 6. Combine all components into net velocity
            Vector3 netDirection = desiredDir + avoidanceVector * _avoidanceForce;
            netDirection.y += verticalCorrection;

            if (netDirection.sqrMagnitude > 0.01f)
            {
                netDirection.Normalize();
            }

            Vector3 targetVelocity = netDirection * speed + organicWeave;

            // Smooth velocity changes
            _currentVelocity = Vector3.Lerp(_currentVelocity, targetVelocity, Time.deltaTime * 5f);

            // Apply position translation on server (synced to clients via NetworkTransform)
            transform.position += _currentVelocity * Time.deltaTime;

            // Smooth rotation towards flight direction
            Vector3 lookDirection = _currentVelocity;
            lookDirection.y *= 0.5f; // keep tilt reasonable
            if (lookDirection.sqrMagnitude > 0.05f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * _turnSpeed);
            }
        }

        /// <summary>
        /// Description: Stops active directed motion and maintains organic hovering in place.
        /// </summary>
        protected override void StopMovement()
        {
            if (!isServer) return;

            _isDestinationSet = false;

            // Hover gently in place
            float targetHeightY = CalculateTargetHeight();
            float time = Time.time + _phaseOffset;
            float bob = Mathf.Sin(time * _bobFrequency * 0.6f) * (_bobAmplitude * 0.5f);

            Vector3 targetPos = transform.position;
            targetPos.y = Mathf.Lerp(targetPos.y, targetHeightY + bob, Time.deltaTime * 3f);
            transform.position = targetPos;

            _currentVelocity = Vector3.Lerp(_currentVelocity, Vector3.zero, Time.deltaTime * 4f);
        }

        /// <summary>
        /// Description: Evaluates if the flying wisp has reached its destination.
        /// </summary>
        protected override bool IsDestinationReached()
        {
            if (!_isDestinationSet) return true;

            float distSqr = (transform.position - _currentDestination).sqrMagnitude;
            return distSqr <= (_stoppingDistance * _stoppingDistance);
        }

        /// <summary>
        /// Description: Finds a random reachable 3D point in the room with clear line of flight.
        /// </summary>
        protected override void PickAndStartWanderDestination()
        {
            if (!isServer) return;

            Vector3 bestCandidate = transform.position;
            bool candidateFound = false;

            for (int attempt = 0; attempt < 6; attempt++)
            {
                Vector3 randomOffset = Random.insideUnitSphere * _wanderRadius;
                randomOffset.y = Random.Range(-0.8f, 1.2f);
                Vector3 candidate = transform.position + randomOffset;

                // Raycast downward from candidate to ensure it's not below ground or outside map
                if (Physics.Raycast(candidate + Vector3.up * 3f, Vector3.down, out RaycastHit groundHit, 8f, _groundLayer, QueryTriggerInteraction.Ignore))
                {
                    candidate.y = groundHit.point.y + Random.Range(_minHoverAltitude, _maxHoverAltitude);

                    // Check if candidate point is obstructed by a solid wall
                    Vector3 travelRay = candidate - transform.position;
                    if (!Physics.Raycast(transform.position, travelRay.normalized, travelRay.magnitude, _obstacleLayer, QueryTriggerInteraction.Ignore))
                    {
                        bestCandidate = candidate;
                        candidateFound = true;
                        break;
                    }
                }
            }

            if (!candidateFound)
            {
                // Fallback: stay close to current height and nudge forward slightly
                bestCandidate = transform.position + transform.forward * 2f;
            }

            _currentDestination = bestCandidate;
            _isDestinationSet = true;
        }

        protected override void UpdateWanderingState()
        {
            if (!isServer) return;

            // In wandering state, actively fly towards the chosen 3D destination
            MoveTowards(_currentDestination);

            base.UpdateWanderingState();
        }

        protected override void UpdateBeingVacuumedState()
        {
            if (!isServer) return;

            // Execute full base suction physics (centering, nozzle pull, jitter, escape dashes, shrink, swallow)
            base.UpdateBeingVacuumedState();
        }
        #endregion

        #region Feeler Ray & Altitude Calculations
        /// <summary>
        /// Description: Casts a downward raycast to maintain the configured altitude above ground/furniture.
        /// </summary>
        private float CalculateTargetHeight()
        {
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 10f, _groundLayer, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y + _targetHoverAltitude;
            }

            // If no ground detected immediately below, maintain current Y
            return transform.position.y;
        }

        /// <summary>
        /// Description: Casts 5 3D feeler rays (Center, Left 35°, Right 35°, Up 25°, Down 25°)
        /// and computes an avoidance deflection vector away from walls and ceilings.
        /// </summary>
        private Vector3 CalculateFeelerAvoidance()
        {
            Vector3 forward = transform.forward;
            Vector3 up = transform.up;
            Vector3 right = transform.right;

            Vector3[] feelerDirs = new Vector3[]
            {
                forward,
                Quaternion.AngleAxis(-35f, up) * forward,
                Quaternion.AngleAxis(35f, up) * forward,
                Quaternion.AngleAxis(-25f, right) * forward,
                Quaternion.AngleAxis(25f, right) * forward
            };

            Vector3 avoidanceForce = Vector3.zero;

            for (int i = 0; i < feelerDirs.Length; i++)
            {
                Vector3 dir = feelerDirs[i];
                if (Physics.Raycast(transform.position, dir, out RaycastHit hit, _wallAvoidanceDistance, _obstacleLayer, QueryTriggerInteraction.Ignore))
                {
                    float factor = 1.0f - (hit.distance / _wallAvoidanceDistance);
                    avoidanceForce += hit.normal * factor;
                }
            }

            return avoidanceForce;
        }
        #endregion

        #region Editor Gizmos
#if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            // 1. Draw feeler rays
            Gizmos.color = Color.cyan;
            Vector3 forward = transform.forward;
            Vector3 up = transform.up;
            Vector3 right = transform.right;

            Vector3[] feelerDirs = new Vector3[]
            {
                forward,
                Quaternion.AngleAxis(-35f, up) * forward,
                Quaternion.AngleAxis(35f, up) * forward,
                Quaternion.AngleAxis(-25f, right) * forward,
                Quaternion.AngleAxis(25f, right) * forward
            };

            for (int i = 0; i < feelerDirs.Length; i++)
            {
                Gizmos.DrawRay(transform.position, feelerDirs[i] * _wallAvoidanceDistance);
            }

            // 2. Draw hover height anchor
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, Vector3.down * _targetHoverAltitude);

            // 3. Draw destination marker
            if (_isDestinationSet)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(_currentDestination, 0.35f);
                Gizmos.DrawLine(transform.position, _currentDestination);
            }
        }
#endif
        #endregion
    }
}
