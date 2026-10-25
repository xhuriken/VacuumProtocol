using System.Collections.Generic;
using Mirror;
using Player.Mechanics;
using UnityEngine;

namespace Gameplay.Phantoms
{
    /// <summary>
    /// Description: Defines the possible behavioral states for Phantom AI entities.
    /// Context: Used by PhantomBase and derived phantom controllers to drive state-machine decisions.
    /// </summary>
    public enum PhantomState
    {
        Idle,
        Wandering,
        Chasing,
        Investigating,
        BeingVacuumed,
        Attacking
    }

    /// <summary>
    /// Description: Abstract base class for all Phantom entities in the game.
    /// Context: Runs server-side in Mirror multiplayer. Drives target detection via FOV and Line-of-Sight,
    /// last-known-position tracking, state machine transitions, and IEntity exposure.
    /// Justification: Provides a common extensible foundation so specialized phantoms can share core vision,
    /// multiplayer targeting, and investigation logic while customizing movement.
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    public abstract class PhantomBase : NetworkBehaviour, IEntity
    {
        #region IEntity Implementation
        [Header("Entity Settings")]
        [Tooltip("Role: The display name of this phantom.\nUse Case: UI label rendering and logging.\nJustification: Identifies the enemy to players and debugging logs.")]
        [SerializeField] private string _entityName = "Phantom";

        [Tooltip("Role: Priority level for player visual detection.\nUse Case: Vision targeting and fear animations.\nJustification: Values >= 3 trigger fear saccades in robot eyes as defined in game design.")]
        [SerializeField] private int _priorityLevel = 3;

        [Tooltip("Role: The focal transform target for eye tracking and vision origin.\nUse Case: Where player heads/cameras focus, and raycast origin point for the phantom's vision.\nJustification: Required by IEntity interface. Must be assigned in prefab.")]
        [SerializeField] private Transform _lookAtPoint;

        public string Name
        {
            get => _entityName;
            set => _entityName = value;
        }

        public int PriorityLevel
        {
            get => _priorityLevel;
            set => _priorityLevel = value;
        }

        public Transform LookAtPoint => _lookAtPoint != null ? _lookAtPoint : transform;
        #endregion

        #region Vision & Detection Settings
        [Header("Vision & Field of View")]
        [Tooltip("Role: Horizontal field of view angle in degrees.\nUse Case: Players outside this cone cannot be seen.")]
        [SerializeField] protected float _fieldOfViewAngle = 110f;

        [Tooltip("Role: Maximum distance at which this phantom can see a player.\nUse Case: Vision cone range limit.")]
        [SerializeField] protected float _detectionRadius = 15f;

        [Tooltip("Role: Maximum distance before losing interest even if target is still in line of sight.\nUse Case: Max chase range cutoff.")]
        [SerializeField] protected float _loseTargetRadius = 25f;

        [Tooltip("Role: Layers that block line-of-sight (walls, obstacles, ground).\nUse Case: If a wall is between phantom and player, player remains undetected.")]
        [SerializeField] protected LayerMask _obstacleLayer;

        [Header("Multiplayer Target Switch Settings")]
        [Tooltip("Role: Allows the phantom to switch to a much closer visible player while chasing.\nUse Case: False = tunnel-vision on current prey; True = switches if another player gets much closer.")]
        [SerializeField] protected bool _allowTargetSwitchWhileChasing = false;

        [Tooltip("Role: Proximity ratio required to trigger a target switch during chase.\nUse Case: e.g. 0.6 means new target must be at least 40% closer than current target.")]
        [SerializeField] protected float _switchDistanceRatio = 0.6f;

        [Header("Combat & Investigation Settings")]
        [Tooltip("Role: Distance required to trigger an attack.\nUse Case: Transitions from Chasing to Attacking.")]
        [SerializeField] protected float _attackRadius = 1.8f;

        [Tooltip("Role: Cooldown in seconds between attacks.\nUse Case: Pacing attacks.")]
        [SerializeField] protected float _attackCooldown = 2.0f;

        [Tooltip("Role: Time in seconds to inspect the last known position before returning to Idle.\nUse Case: Gives players a window to escape and hide.")]
        [SerializeField] protected float _investigateWaitTime = 3.0f;

        [Tooltip("Role: Maximum time allowed to reach the last known position before giving up.")]
        [SerializeField] protected float _maxInvestigateTravelTime = 8.0f;

        [Header("Wander Settings")]
        [Tooltip("Role: Radius within which random wander destinations are picked.\nUse Case: Exploration when no player is detected.")]
        [SerializeField] protected float _wanderRadius = 10f;

        [Tooltip("Role: Time in seconds to wait at a wander destination before picking a new one.\nUse Case: Natural pacing in idle/patrol state.")]
        [SerializeField] protected float _wanderWaitTime = 3f;

        [Header("Mouth Vacuum Capture & Struggle Settings")]
        [Tooltip("Role: Maximum ectoplasm / capture threshold of the phantom.\nUse Case: Depleted by mouth dual-click suction.")]
        [SerializeField] protected float _maxEctoplasm = 150f;

        [SyncVar(hook = nameof(OnEctoplasmChanged))]
        [SerializeField] protected float _currentEctoplasm = 150f;

        [Tooltip("Role: Speed reduction ratio when caught in mouth vacuum (0 = stopped, 1 = full speed).")]
        [SerializeField] protected float _suctionSlowdownRatio = 0.25f;

        [Tooltip("Role: Centripetal force dragging the phantom towards the central axis of the suction cone.")]
        [SerializeField] protected float _centeringStrength = 7.0f;

        [Tooltip("Role: Maximum speed at which the phantom can be pulled laterally towards the center axis.")]
        [SerializeField] protected float _maxCenteringSpeed = 5.0f;

        [Tooltip("Role: Axial drag speed pulling the phantom towards the mouth nozzle.")]
        [SerializeField] protected float _suctionPullSpeed = 2.4f;

        [Tooltip("Role: Frequency of panic jitter / vibration while caught in the suction vortex.")]
        [SerializeField] protected float _shakeFrequency = 35.0f;

        [Tooltip("Role: Amplitude of panic jitter / vibration in meters.")]
        [SerializeField] protected float _shakeAmplitude = 0.08f;

        [Tooltip("Role: Resistance factor (0 to 1) reducing the effectiveness of suction centering and drag.")]
        [Range(0f, 0.9f)]
        [SerializeField] protected float _struggleResistance = 0.35f;

        [Tooltip("Role: Impulse force applied when the phantom performs an escape dash.")]
        [SerializeField] protected float _escapeDashForce = 5.2f;

        [Tooltip("Role: Minimum interval in seconds between escape dash attempts.")]
        [SerializeField] protected float _escapeIntervalMin = 1.0f;

        [Tooltip("Role: Maximum interval in seconds between escape dash attempts.")]
        [SerializeField] protected float _escapeIntervalMax = 2.2f;

        [Tooltip("Role: Duration in seconds of the escape dash impulse burst.")]
        [SerializeField] protected float _escapeDashDuration = 0.35f;

        [Tooltip("Role: Size/health threshold ratio (e.g. 0.25 = 25%) where the phantom gives up, collapses, and is swallowed into the nozzle.")]
        [Range(0.1f, 0.5f)]
        [SerializeField] protected float _swallowThresholdRatio = 0.25f;

        [Tooltip("Role: Speed at which the defeated phantom dives directly into the nozzle once the swallow threshold is reached.")]
        [SerializeField] protected float _swallowDiveSpeed = 12.0f;

        [Header("Proximity Terror Drain Aura")]
        [Tooltip("Role: Enables continuous health drain for players within proximity aura.")]
        [SerializeField] protected bool _enableProximityDrain = true;

        [Tooltip("Role: Radius of the terror / miasma aura in meters.")]
        [SerializeField] protected float _proximityDrainRadius = 6.0f;

        [Tooltip("Role: Maximum health drain rate in PV per second at point-blank range.")]
        [SerializeField] protected float _maxDrainRate = 20.0f;

        [Tooltip("Role: Falloff curve for damage rate based on normalized distance (0 = point blank, 1 = max radius).")]
        [SerializeField] protected AnimationCurve _drainFalloffCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [Tooltip("Role: Layers that block the drain aura (walls, obstacles). If blocked by a wall, no damage.")]
        [SerializeField] protected LayerMask _drainObstacleLayer;

        [Header("Direct Attack Settings")]
        [Tooltip("Role: Direct damage dealt to player on physical attack.")]
        [SerializeField] protected float _attackDamage = 25.0f;
        #endregion

        #region State Machine Variables
        [Header("Current State (Debug)")]
        [SerializeField] protected PhantomState _currentState = PhantomState.Idle;
        [SerializeField] protected PlayerV2_Controller _currentTarget;

        protected float _stateTimer = 0f;
        protected float _lastAttackTime = -999f;

        protected Vector3 _lastKnownPosition;
        protected bool _hasLastKnownPosition = false;
        protected float _investigateWaitTimer = 0f;
        protected bool _hasReachedLastKnown = false;

        protected float _lastMouthSuctionTime = -999f;
        protected Vector3 _lastSuctionNozzlePos;
        protected Vector3 _lastConeForward = Vector3.forward;
        protected Vector3 _lastConeUp = Vector3.up;
        protected Vector3 _lastConeRight = Vector3.right;

        protected Vector3 _initialLocalScale = Vector3.one;
        protected float _escapeTimer = 0f;
        protected float _currentEscapeInterval = 1.5f;
        protected Vector3 _currentEscapeVelocity = Vector3.zero;
        protected float _escapeDurationTimer = 0f;

        [SyncVar]
        protected bool _isBeingSwallowed = false;
        protected bool _isDefeatedInvoked = false;
        #endregion

        #region Public Accessors (for Gizmos and Debug Tools)
        public PhantomState CurrentState => _currentState;
        public PlayerV2_Controller CurrentTarget => _currentTarget;
        public float FieldOfViewAngle => _fieldOfViewAngle;
        public float DetectionRadius => _detectionRadius;
        public float LoseTargetRadius => _loseTargetRadius;
        public Vector3 LastKnownPosition => _lastKnownPosition;
        public bool HasLastKnownPosition => _hasLastKnownPosition;
        public LayerMask ObstacleLayer => _obstacleLayer;
        public float CurrentEctoplasm => _currentEctoplasm;
        public float MaxEctoplasm => _maxEctoplasm;
        public float SuctionSlowdownRatio => _suctionSlowdownRatio;
        public float ProximityDrainRadius => _proximityDrainRadius;
        public float MaxDrainRate => _maxDrainRate;
        public bool EnableProximityDrain => _enableProximityDrain;
        public float AttackDamage => _attackDamage;

        /// <summary>
        /// Global registry of active phantoms in the scene for O(1) high-performance queries without Physics.OverlapSphere.
        /// </summary>
        public static readonly List<PhantomBase> ActivePhantoms = new List<PhantomBase>();

        protected virtual void OnEnable()
        {
            if (!ActivePhantoms.Contains(this))
            {
                ActivePhantoms.Add(this);
            }
        }

        protected virtual void OnDisable()
        {
            ActivePhantoms.Remove(this);
        }
        #endregion

        #region Unity Callbacks
        protected virtual void Awake()
        {
            _initialLocalScale = transform.localScale;
            _currentEscapeInterval = Random.Range(_escapeIntervalMin, _escapeIntervalMax);

            if (_lookAtPoint == null)
            {
                Debug.LogError($"[PhantomBase] Critical Configuration Error: '{gameObject.name}' is missing a '_lookAtPoint' assignment! Please set it on the prefab to satisfy IEntity requirements. Fail loudly per workspace rules.");
            }

            // Default obstacle layer mask if unassigned in inspector
            if (_obstacleLayer.value == 0)
            {
                _obstacleLayer = LayerMask.GetMask("Default", "Ground", "Obstacle", "Obstable");
                if (_obstacleLayer.value == 0)
                {
                    _obstacleLayer = (1 << 0) | (1 << 3) | (1 << 8);
                }
            }

            if (_drainObstacleLayer.value == 0)
            {
                _drainObstacleLayer = _obstacleLayer;
            }
        }

        [ServerCallback]
        protected virtual void Update()
        {
            if (!isServer) return;

            UpdateTargetDetection();
            UpdateStateMachine();

            if (_enableProximityDrain)
            {
                UpdateProximityDrain();
            }
        }
        #endregion

        #region Vision & Detection Logic
        /// <summary>
        /// Description: Checks if a player is within the phantom's field of view and not obstructed by an obstacle.
        /// Context: Runs server-side. Used for both initial target acquisition and maintaining chase.
        /// </summary>
        public virtual bool IsPlayerVisible(PlayerV2_Controller player, out float distance)
        {
            distance = float.MaxValue;
            if (player == null || !player.gameObject.activeInHierarchy) return false;

            Vector3 eyeOrigin = _lookAtPoint != null ? _lookAtPoint.position : (transform.position + Vector3.up * 1.5f);
            Vector3 targetPoint = player.LookAtPoint != null ? player.LookAtPoint.position : (player.WorldPosition + Vector3.up * 0.8f);

            Vector3 directionToPlayer = targetPoint - eyeOrigin;
            distance = directionToPlayer.magnitude;

            float maxRange = (_currentTarget == player) ? _loseTargetRadius : _detectionRadius;
            if (distance > maxRange) return false;

            // FOV cone check
            float angle = Vector3.Angle(transform.forward, directionToPlayer.normalized);
            if (angle > _fieldOfViewAngle / 2f) return false;

            // Line-of-sight raycast against obstacle layer
            if (Physics.Raycast(eyeOrigin, directionToPlayer.normalized, out RaycastHit hit, distance, _obstacleLayer, QueryTriggerInteraction.Ignore))
            {
                // If the ray hit the player or their child colliders, it's not an obstruction
                if (!hit.transform.IsChildOf(player.transform))
                {
                    // Blocked by an obstacle/wall!
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Description: Scans all active players and selects the closest visible one within FOV.
        /// Context: Resolves multiplayer targeting conflicts when multiple players are present.
        /// </summary>
        protected virtual PlayerV2_Controller GetClosestVisiblePlayer()
        {
            IReadOnlyList<PlayerV2_Controller> players = PlayerV2_Controller.ActivePlayers;
            PlayerV2_Controller bestPlayer = null;
            float minDistance = float.MaxValue;

            for (int i = 0; i < players.Count; i++)
            {
                PlayerV2_Controller player = players[i];
                if (player == null || !player.gameObject.activeInHierarchy) continue;

                if (IsPlayerVisible(player, out float dist))
                {
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        bestPlayer = player;
                    }
                }
            }

            return bestPlayer;
        }

        /// <summary>
        /// Description: Drives target acquisition, tracking, and vision loss server-side.
        /// </summary>
        protected virtual void UpdateTargetDetection()
        {
            switch (_currentState)
            {
                case PhantomState.Idle:
                case PhantomState.Wandering:
                case PhantomState.Investigating:
                    // Look for any visible player
                    PlayerV2_Controller spottedPlayer = GetClosestVisiblePlayer();
                    if (spottedPlayer != null)
                    {
                        _currentTarget = spottedPlayer;
                        _lastKnownPosition = spottedPlayer.WorldPosition;
                        _hasLastKnownPosition = true;
                        OnPlayerDetected(_currentTarget);
                        TransitionToState(PhantomState.Chasing);
                    }
                    break;

                case PhantomState.Chasing:
                    // Validate current target
                    if (_currentTarget == null || !_currentTarget.gameObject.activeInHierarchy)
                    {
                        OnPlayerLost();
                        _currentTarget = null;
                        if (_hasLastKnownPosition)
                        {
                            TransitionToState(PhantomState.Investigating);
                        }
                        else
                        {
                            TransitionToState(PhantomState.Idle);
                        }
                        return;
                    }

                    // Check if current target is still visible
                    if (IsPlayerVisible(_currentTarget, out float currentDist))
                    {
                        // Target still in sight: update last known position
                        _lastKnownPosition = _currentTarget.WorldPosition;
                        _hasLastKnownPosition = true;

                        // Optional: switch to another player if much closer
                        if (_allowTargetSwitchWhileChasing)
                        {
                            PlayerV2_Controller closerPlayer = GetClosestVisiblePlayer();
                            if (closerPlayer != null && closerPlayer != _currentTarget)
                            {
                                float closerDist = Vector3.Distance(transform.position, closerPlayer.WorldPosition);
                                if (closerDist < currentDist * _switchDistanceRatio)
                                {
                                    Debug.Log($"[PhantomBase] '{gameObject.name}' switched focus to closer player '{closerPlayer.name}'.");
                                    _currentTarget = closerPlayer;
                                    _lastKnownPosition = closerPlayer.WorldPosition;
                                }
                            }
                        }
                    }
                    else
                    {
                        // Lost line-of-sight! Target went behind a wall or out of FOV
                        OnPlayerLost();
                        TransitionToState(PhantomState.Investigating);
                    }
                    break;

                case PhantomState.Attacking:
                    // In attack range, keep target updated
                    if (_currentTarget != null && _currentTarget.gameObject.activeInHierarchy)
                    {
                        _lastKnownPosition = _currentTarget.WorldPosition;
                        _hasLastKnownPosition = true;
                    }
                    break;
            }
        }
        #endregion

        #region State Machine Logic
        /// <summary>
        /// Description: Executes state-specific logic for the current AI state.
        /// </summary>
        protected virtual void UpdateStateMachine()
        {
            switch (_currentState)
            {
                case PhantomState.Idle:
                    UpdateIdleState();
                    break;

                case PhantomState.Wandering:
                    UpdateWanderingState();
                    break;

                case PhantomState.Chasing:
                    UpdateChasingState();
                    break;

                case PhantomState.Investigating:
                    UpdateInvestigatingState();
                    break;

                case PhantomState.BeingVacuumed:
                    UpdateBeingVacuumedState();
                    break;

                case PhantomState.Attacking:
                    UpdateAttackingState();
                    break;
            }
        }

        protected virtual void UpdateIdleState()
        {
            _stateTimer += Time.deltaTime;
            if (_stateTimer >= _wanderWaitTime)
            {
                TransitionToState(PhantomState.Wandering);
            }
        }

        protected virtual void UpdateWanderingState()
        {
            if (IsDestinationReached())
            {
                StopMovement();
                TransitionToState(PhantomState.Idle);
            }
        }

        protected virtual void UpdateChasingState()
        {
            if (_currentTarget == null)
            {
                StopMovement();
                TransitionToState(PhantomState.Idle);
                return;
            }

            Vector3 targetPosition = _currentTarget.WorldPosition;
            float distanceToTarget = Vector3.Distance(transform.position, targetPosition);

            if (distanceToTarget <= _attackRadius)
            {
                StopMovement();
                TransitionToState(PhantomState.Attacking);
                return;
            }

            MoveTowards(targetPosition);
        }

        /// <summary>
        /// Description: Moves towards the last known position where the player was last seen.
        /// If reached, pauses briefly to look around before returning to Idle.
        /// </summary>
        protected virtual void UpdateInvestigatingState()
        {
            _stateTimer += Time.deltaTime;

            if (!_hasReachedLastKnown)
            {
                MoveTowards(_lastKnownPosition);

                // Check if arrived at last known spot or timed out
                if (IsDestinationReached() || _stateTimer >= _maxInvestigateTravelTime)
                {
                    _hasReachedLastKnown = true;
                    StopMovement();
                    _investigateWaitTimer = 0f;
                }
            }
            else
            {
                // Inspecting area
                _investigateWaitTimer += Time.deltaTime;
                if (_investigateWaitTimer >= _investigateWaitTime)
                {
                    _currentTarget = null;
                    _hasLastKnownPosition = false;
                    TransitionToState(PhantomState.Idle);
                }
            }
        }

        protected virtual void UpdateAttackingState()
        {
            if (_currentTarget == null || !_currentTarget.gameObject.activeInHierarchy)
            {
                TransitionToState(PhantomState.Idle);
                return;
            }

            float distanceToTarget = Vector3.Distance(transform.position, _currentTarget.WorldPosition);
            if (distanceToTarget > _attackRadius * 1.3f)
            {
                TransitionToState(PhantomState.Chasing);
                return;
            }

            if (Time.time - _lastAttackTime >= _attackCooldown)
            {
                _lastAttackTime = Time.time;
                OnAttackPlayer(_currentTarget);
            }
        }

        /// <summary>
        /// Description: Handles transition logic when entering a new state.
        /// </summary>
        protected virtual void TransitionToState(PhantomState newState)
        {
            _currentState = newState;
            _stateTimer = 0f;

            if (newState == PhantomState.Wandering)
            {
                PickAndStartWanderDestination();
            }
            else if (newState == PhantomState.Investigating)
            {
                _hasReachedLastKnown = false;
                _investigateWaitTimer = 0f;
                if (_hasLastKnownPosition)
                {
                    MoveTowards(_lastKnownPosition);
                }
            }
        }
        #endregion

        #region Abstract / Virtual Methods
        /// <summary>
        /// Description: Orders the movement controller to move towards a target world position.
        /// </summary>
        protected abstract void MoveTowards(Vector3 destination);

        /// <summary>
        /// Description: Commands the movement controller to stop immediately.
        /// </summary>
        protected abstract void StopMovement();

        /// <summary>
        /// Description: Checks if the phantom has reached its current navigation destination.
        /// </summary>
        protected abstract bool IsDestinationReached();

        /// <summary>
        /// Description: Picks a random valid destination on the navigable space and initiates movement.
        /// </summary>
        protected abstract void PickAndStartWanderDestination();

        /// <summary>
        /// Description: Hook called when a player enters vision.
        /// </summary>
        protected virtual void OnPlayerDetected(PlayerV2_Controller player)
        {
            Debug.Log($"[PhantomBase] '{gameObject.name}' spotted player '{player.name}' in FOV! Initiating chase.");
        }

        /// <summary>
        /// Description: Hook called when line-of-sight to target player is lost.
        /// </summary>
        protected virtual void OnPlayerLost()
        {
            Debug.Log($"[PhantomBase] '{gameObject.name}' lost line-of-sight. Investigating last known position: {_lastKnownPosition}.");
        }

        /// <summary>
        private void OnEctoplasmChanged(float oldVal, float newVal)
        {
            UpdateVisualScaleFromEctoplasm();
        }

        /// <summary>
        /// Description: Dynamically shrinks the phantom proportional to remaining ectoplasm health,
        /// reaching _swallowThresholdRatio right as the swallow threshold is attained.
        /// </summary>
        protected virtual void UpdateVisualScaleFromEctoplasm()
        {
            if (_isBeingSwallowed) return;

            float thresholdEctoplasm = _maxEctoplasm * _swallowThresholdRatio;
            if (_currentEctoplasm <= thresholdEctoplasm)
            {
                transform.localScale = _initialLocalScale * _swallowThresholdRatio;
                return;
            }

            float remainingRatio = Mathf.Clamp01((_currentEctoplasm - thresholdEctoplasm) / Mathf.Max(0.01f, _maxEctoplasm - thresholdEctoplasm));
            float scaleFactor = Mathf.Lerp(_swallowThresholdRatio, 1.0f, remainingRatio);
            transform.localScale = _initialLocalScale * scaleFactor;
        }

        /// <summary>
        /// Description: Updates behavior while trapped inside the player's mouth suction stream.
        /// Incorporates centripetal axis attraction, nozzle drag, panic jitter, struggle escape dashes,
        /// combat in suction, progressive scale shrinking, and high-speed swallow plunge.
        /// </summary>
        protected virtual void UpdateBeingVacuumedState()
        {
            // If suction stopped for more than 0.3s and we are not in final swallow dive, break free!
            if (!_isBeingSwallowed && Time.time - _lastMouthSuctionTime > 0.3f)
            {
                _currentEscapeVelocity = Vector3.zero;
                _escapeDurationTimer = 0f;

                if (_currentTarget != null && IsPlayerVisible(_currentTarget, out _))
                {
                    TransitionToState(PhantomState.Chasing);
                }
                else if (_hasLastKnownPosition)
                {
                    TransitionToState(PhantomState.Investigating);
                }
                else
                {
                    TransitionToState(PhantomState.Idle);
                }
                return;
            }

            // --- 1. Final Swallow Sequence (Gulp) ---
            if (_isBeingSwallowed)
            {
                Vector3 toNozzle = _lastSuctionNozzlePos - transform.position;
                float distToNozzle = toNozzle.magnitude;

                if (distToNozzle > 0.15f)
                {
                    // High-speed plunge directly into mouth nozzle
                    transform.position += toNozzle.normalized * (_swallowDiveSpeed * Time.deltaTime);

                    // Rapidly shrink scale to zero as it approaches the nozzle
                    float shrinkProgress = Mathf.Clamp01(distToNozzle / 1.5f);
                    transform.localScale = _initialLocalScale * (_swallowThresholdRatio * shrinkProgress);

                    // Spiral / vortex roll towards the mouth
                    transform.Rotate(Vector3.forward, 720f * Time.deltaTime, Space.Self);
                }
                else
                {
                    // Plunged into mouth! Disappear and capture
                    if (isServer)
                    {
                        OnPhantomCaptured();
                    }
                    return;
                }
                return;
            }

            // --- 2. Active Combat & Attack while in Suction ---
            if (_currentTarget != null && _currentTarget.gameObject.activeInHierarchy)
            {
                float distToTarget = Vector3.Distance(transform.position, _currentTarget.WorldPosition);
                if (distToTarget <= _attackRadius)
                {
                    if (Time.time - _lastAttackTime >= _attackCooldown)
                    {
                        _lastAttackTime = Time.time;
                        OnAttackPlayer(_currentTarget);
                    }
                }
            }

            // --- 3. Centering Force (Attraction towards central cone axis) ---
            Vector3 fromNozzle = transform.position - _lastSuctionNozzlePos;
            float axialDistance = Vector3.Dot(fromNozzle, _lastConeForward);
            Vector3 centerAxisPoint = _lastSuctionNozzlePos + _lastConeForward * Mathf.Max(0.01f, axialDistance);

            Vector3 toAxis = centerAxisPoint - transform.position;
            Vector3 centeringVel = Vector3.zero;
            if (toAxis.sqrMagnitude > 0.001f)
            {
                float centeringSpeed = Mathf.Min(toAxis.magnitude * _centeringStrength, _maxCenteringSpeed);
                centeringVel = toAxis.normalized * (centeringSpeed * (1f - _struggleResistance));
            }

            // --- 4. Axial Suction Drag towards Mouth Nozzle ---
            Vector3 toNozzleDir = (_lastSuctionNozzlePos - transform.position);
            Vector3 pullVel = Vector3.zero;
            if (toNozzleDir.sqrMagnitude > 0.01f)
            {
                pullVel = toNozzleDir.normalized * (_suctionPullSpeed * (1f - _struggleResistance * 0.5f));
            }

            // --- 5. Panic Jitter / Shake ---
            float shakeTime = Time.time * _shakeFrequency;
            Vector3 shakeOffset = (_lastConeRight * Mathf.Sin(shakeTime) + _lastConeUp * Mathf.Cos(shakeTime * 1.35f)) * _shakeAmplitude;

            // --- 6. Struggle / Escape Dash AI ---
            _escapeTimer += Time.deltaTime;
            if (_escapeTimer >= _currentEscapeInterval)
            {
                _escapeTimer = 0f;
                _currentEscapeInterval = Random.Range(_escapeIntervalMin, _escapeIntervalMax);

                // Select escape direction (Up, Down, Left, Right, or Backwards)
                int dirChoice = Random.Range(0, 5);
                Vector3 escapeDir = _lastConeRight;
                switch (dirChoice)
                {
                    case 0: escapeDir = _lastConeRight; break;
                    case 1: escapeDir = -_lastConeRight; break;
                    case 2: escapeDir = _lastConeUp; break;
                    case 3: escapeDir = -_lastConeUp; break;
                    case 4: escapeDir = _lastConeForward; break; // pushing back away from player
                }

                _currentEscapeVelocity = escapeDir.normalized * _escapeDashForce;
                _escapeDurationTimer = _escapeDashDuration;
            }

            if (_escapeDurationTimer > 0f)
            {
                _escapeDurationTimer -= Time.deltaTime;
                _currentEscapeVelocity = Vector3.Lerp(_currentEscapeVelocity, Vector3.zero, Time.deltaTime * 5f);
            }
            else
            {
                _currentEscapeVelocity = Vector3.zero;
            }

            // Combine all physical forces
            Vector3 totalVelocity = centeringVel + pullVel + _currentEscapeVelocity;
            transform.position += (totalVelocity * Time.deltaTime) + shakeOffset;

            // Orient towards the nozzle with slight panic tilt
            if (toNozzleDir.sqrMagnitude > 0.05f)
            {
                Quaternion targetRot = Quaternion.LookRotation(toNozzleDir.normalized, _lastConeUp);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 8f);
            }
        }

        /// <summary>
        /// Description: Hook called when the player actively vacuums this phantom with the mouth.
        /// Passes nozzle position and cone coordinate frame for centering and struggle calculations.
        /// </summary>
        public virtual void OnMouthSuctionApplied(Vector3 nozzlePos, Vector3 coneForward, Vector3 coneUp, Vector3 coneRight, float drainAmount)
        {
            _lastMouthSuctionTime = Time.time;
            _lastSuctionNozzlePos = nozzlePos;
            _lastConeForward = coneForward;
            _lastConeUp = coneUp;
            _lastConeRight = coneRight;

            if (_currentState != PhantomState.BeingVacuumed && _currentState != PhantomState.Attacking && !_isBeingSwallowed)
            {
                TransitionToState(PhantomState.BeingVacuumed);
            }

            if (isServer)
            {
                if (!_isBeingSwallowed)
                {
                    _currentEctoplasm -= drainAmount * Time.deltaTime;
                    UpdateVisualScaleFromEctoplasm();

                    if (_currentEctoplasm <= _maxEctoplasm * _swallowThresholdRatio)
                    {
                        _isBeingSwallowed = true;
                    }
                }
            }
        }

        /// <summary>
        /// Description: Backward compatible overload for mouth suction application.
        /// </summary>
        public virtual void OnMouthSuctionApplied(Vector3 nozzlePos, float drainAmount)
        {
            Vector3 forward = (transform.position - nozzlePos).normalized;
            Vector3 up = Vector3.up;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            OnMouthSuctionApplied(nozzlePos, forward, up, right, drainAmount);
        }

        /// <summary>
        /// Event invoked when this phantom is defeated or destroyed, notifying parent spawners.
        /// </summary>
        public event System.Action<PhantomBase> OnDefeated;

        /// <summary>
        /// Description: Authoritative hook called when the phantom's ectoplasm is completely depleted.
        /// </summary>
        protected virtual void OnPhantomCaptured()
        {
            Debug.Log($"<color=magenta>[PhantomBase]</color> '{gameObject.name}' was completely captured by mouth vacuum!");
            if (!_isDefeatedInvoked)
            {
                _isDefeatedInvoked = true;
                OnDefeated?.Invoke(this);
            }
            NetworkServer.Destroy(gameObject);
        }

        protected virtual void OnDestroy()
        {
            if (!_isDefeatedInvoked)
            {
                _isDefeatedInvoked = true;
                OnDefeated?.Invoke(this);
            }
        }

        /// <summary>
        /// Description: Evaluates distance to all active players and applies proximity terror drain if line-of-sight is unobstructed.
        /// Runs server-side. Zero allocation via PlayerV2_Controller.ActivePlayers registry.
        /// </summary>
        protected virtual void UpdateProximityDrain()
        {
            var activePlayers = PlayerV2_Controller.ActivePlayers;
            if (activePlayers == null || activePlayers.Count == 0) return;

            Vector3 eyeOrigin = _lookAtPoint != null ? _lookAtPoint.position : (transform.position + Vector3.up * 1.5f);

            for (int i = 0; i < activePlayers.Count; i++)
            {
                PlayerV2_Controller player = activePlayers[i];
                if (player == null || !player.gameObject.activeInHierarchy) continue;

                Vector3 playerTargetPos = player.LookAtPoint != null ? player.LookAtPoint.position : (player.WorldPosition + Vector3.up * 0.8f);
                Vector3 toPlayer = playerTargetPos - eyeOrigin;
                float distance = toPlayer.magnitude;

                if (distance > _proximityDrainRadius || distance < 0.01f) continue;

                // Line of sight check: solid obstacles block the terror aura!
                if (Physics.Raycast(eyeOrigin, toPlayer.normalized, out RaycastHit hit, distance, _drainObstacleLayer, QueryTriggerInteraction.Ignore))
                {
                    if (!hit.transform.IsChildOf(player.transform))
                    {
                        // Player is safe behind cover
                        continue;
                    }
                }

                // Compute damage rate from distance falloff curve
                float normalizedDist = Mathf.Clamp01(distance / _proximityDrainRadius);
                float drainRate = _maxDrainRate * _drainFalloffCurve.Evaluate(normalizedDist);
                float damageThisFrame = drainRate * Time.deltaTime;

                if (damageThisFrame > 0f)
                {
                    PlayerHealth health = player.GetComponent<PlayerHealth>();
                    if (health != null)
                    {
                        health.TakeDamage(damageThisFrame, Name);
                    }
                    else
                    {
                        Debug.LogError($"[PhantomBase] Critical Error: 'PlayerHealth' component is missing on player '{player.name}'! Fail loudly per workspace rules.");
                    }
                }
            }
        }

        /// <summary>
        /// Description: Hook called when executing an attack on the target player.
        /// </summary>
        protected virtual void OnAttackPlayer(PlayerV2_Controller player)
        {
            if (player == null) return;
            Debug.Log($"<color=red>[PhantomBase]</color> '{gameObject.name}' attacked player '{player.name}' dealing {_attackDamage} damage!");

            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.TakeDamage(_attackDamage, Name);
            }
            else
            {
                Debug.LogError($"[PhantomBase] Critical Error: 'PlayerHealth' is missing on player '{player.name}' during attack! Fail loudly per workspace rules.");
            }
        }
        #endregion

#if UNITY_EDITOR
        #region Editor Gizmos
        protected virtual void OnDrawGizmosSelected()
        {
            // If PhantomGizmos component is attached, let it handle the dedicated visual rendering
            if (GetComponent<PhantomGizmos>() != null) return;

            Vector3 eyeOrigin = _lookAtPoint != null ? _lookAtPoint.position : (transform.position + Vector3.up * 1.5f);

            // 0. Proximity Terror Drain Radius
            if (_enableProximityDrain)
            {
                Gizmos.color = new Color(1f, 0f, 0.3f, 0.15f);
                Gizmos.DrawWireSphere(transform.position, _proximityDrainRadius);
            }

            // 1. Detection sphere
            Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, _detectionRadius);

            // 2. Vision cone
            Vector3 forward = transform.forward;
            Vector3 leftBoundary = Quaternion.AngleAxis(-_fieldOfViewAngle / 2f, Vector3.up) * forward;
            Vector3 rightBoundary = Quaternion.AngleAxis(_fieldOfViewAngle / 2f, Vector3.up) * forward;

            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(eyeOrigin, leftBoundary * _detectionRadius);
            Gizmos.DrawRay(eyeOrigin, rightBoundary * _detectionRadius);

            // 3. Last Known Position marker
            if (_hasLastKnownPosition)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(_lastKnownPosition, 0.5f);
                Gizmos.DrawLine(transform.position, _lastKnownPosition);
            }

            // 4. Target Chase Ray
            if (_currentTarget != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(eyeOrigin, _currentTarget.WorldPosition + Vector3.up * 0.8f);
            }
        }
        #endregion
#endif
    }
}

