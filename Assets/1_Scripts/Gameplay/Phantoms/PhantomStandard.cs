using UnityEngine;
using UnityEngine.AI;

namespace Gameplay.Phantoms
{
    /// <summary>
    /// Description: Standard Phantom controller utilizing Unity's NavMesh system for grounded corridor navigation.
    /// Context: Runs server-side for AI logic and NavMesh pathing, while disabling client-side agent logic to avoid conflicts with Mirror's NetworkTransform.
    /// Justification: Implements standard ghost movement along map hallways with obstacle avoidance.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class PhantomStandard : PhantomBase
    {
        [Header("NavMesh Agent Configuration")]
        [Tooltip("Role: Movement speed when chasing a player.\nUse Case: High-intensity approach in pursuit mode.")]
        [SerializeField] private float _chaseSpeed = 3.8f;

        [Tooltip("Role: Movement speed when patrolling / wandering.\nUse Case: Slow atmospheric movement when unprovoked.")]
        [SerializeField] private float _wanderSpeed = 2.0f;

        [Tooltip("Role: Turning speed of the agent in degrees per second.\nUse Case: Smooth cornering through corridors.")]
        [SerializeField] private float _angularSpeed = 240f;

        [Tooltip("Role: Acceleration rate of the agent.\nUse Case: How quickly the phantom accelerates to max speed.")]
        [SerializeField] private float _acceleration = 8f;

        private NavMeshAgent _agent;

        #region Unity Lifecycle
        protected override void Awake()
        {
            base.Awake();

            _agent = GetComponent<NavMeshAgent>();
            if (_agent == null)
            {
                Debug.LogError($"[PhantomStandard] Critical Error: NavMeshAgent component missing on '{gameObject.name}'! Fail loudly per workspace rules.");
                return;
            }

            _agent.speed = _wanderSpeed;
            _agent.angularSpeed = _angularSpeed;
            _agent.acceleration = _acceleration;
            _agent.stoppingDistance = _attackRadius * 0.8f;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // On remote clients, disable NavMeshAgent to avoid fighting Mirror's NetworkTransform
            if (!isServer && _agent != null)
            {
                _agent.enabled = false;
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (!EnsureAgentOnNavMesh())
            {
                Debug.LogError($"[PhantomStandard] Critical Error: '{gameObject.name}' is not placed on a baked NavMesh! Ensure a NavMeshSurface is baked (or 'NavMesh-AmongUsMap.asset' assigned) and the spawn point is valid.");
            }
        }
        #endregion

        #region NavMesh Recovery & Verification
        /// <summary>
        /// Description: Verifies if the NavMeshAgent is placed on a valid NavMesh.
        /// If off the NavMesh (e.g. spawned slightly above or offset), attempts to recover by sampling the nearest NavMesh point and warping.
        /// </summary>
        private bool EnsureAgentOnNavMesh()
        {
            if (_agent == null)
            {
                Debug.LogError($"[PhantomStandard] Critical Error: NavMeshAgent missing on '{gameObject.name}'! Fail loudly per workspace rules.");
                return false;
            }

            if (!_agent.isOnNavMesh)
            {
                // Attempt recovery by searching for the closest NavMesh polygon within 5 meters
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
                {
                    _agent.Warp(hit.position);
                    Debug.Log($"<color=green>[PhantomStandard]</color> Successfully warped '{gameObject.name}' onto NavMesh at {hit.position}.");
                    return true;
                }

                return false;
            }

            return true;
        }
        #endregion

        #region Movement Implementation
        private float _lastDestinationUpdateTime = 0f;
        private Vector3 _lastDestination = Vector3.positiveInfinity;
        private const float DESTINATION_UPDATE_INTERVAL = 0.2f;

        /// <summary>
        /// Description: Updates destination for chasing the target player.
        /// Throttles SetDestination calls to avoid canceling path calculations every frame.
        /// </summary>
        protected override void MoveTowards(Vector3 destination)
        {
            if (!EnsureAgentOnNavMesh())
            {
                Debug.LogError($"[PhantomStandard] Cannot MoveTowards: Agent on '{gameObject.name}' is missing or not placed on a NavMesh.");
                return;
            }

            float targetSpeed = (_currentState == PhantomState.BeingVacuumed)
                ? (_chaseSpeed * _suctionSlowdownRatio)
                : _chaseSpeed;
            _agent.speed = targetSpeed;

            if (_agent.isStopped)
            {
                _agent.isStopped = false;
            }

            // Only update destination if enough time elapsed or target moved sufficiently
            if (Time.time - _lastDestinationUpdateTime >= DESTINATION_UPDATE_INTERVAL || Vector3.SqrMagnitude(_lastDestination - destination) > 1.0f)
            {
                _lastDestinationUpdateTime = Time.time;
                _lastDestination = destination;

                Vector3 targetPos = destination;
                if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                {
                    targetPos = hit.position;
                }

                bool pathAssigned = _agent.SetDestination(targetPos);
                if (!pathAssigned)
                {
                    Debug.LogWarning($"[PhantomStandard] '{gameObject.name}' could not compute path to destination {targetPos}.");
                }
            }
        }

        /// <summary>
        /// Description: Stops the agent's movement.
        /// </summary>
        protected override void StopMovement()
        {
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
            }
        }

        /// <summary>
        /// Description: Evaluates if the agent has reached its target destination.
        /// </summary>
        protected override bool IsDestinationReached()
        {
            if (_agent == null) return true;
            if (!_agent.isOnNavMesh)
            {
                if (!EnsureAgentOnNavMesh()) return false;
            }

            if (!_agent.pathPending)
            {
                if (_agent.remainingDistance <= _agent.stoppingDistance)
                {
                    if (!_agent.hasPath || _agent.velocity.sqrMagnitude < 0.05f)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Description: Finds a random reachable position on the NavMesh around the phantom for wandering.
        /// </summary>
        protected override void PickAndStartWanderDestination()
        {
            if (!EnsureAgentOnNavMesh())
            {
                Debug.LogError($"[PhantomStandard] Cannot PickAndStartWanderDestination: Agent on '{gameObject.name}' is missing or not on a NavMesh.");
                return;
            }

            Vector3 randomDirection = Random.insideUnitSphere * _wanderRadius;
            randomDirection += transform.position;

            if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, _wanderRadius, NavMesh.AllAreas))
            {
                _agent.speed = _wanderSpeed;
                _agent.isStopped = false;
                _agent.SetDestination(hit.position);
            }
            else
            {
                Debug.LogWarning($"[PhantomStandard] '{gameObject.name}' could not find a valid NavMesh point within {_wanderRadius}m for wandering. Will retry next cycle.");
            }
        }
        #endregion
    }
}
