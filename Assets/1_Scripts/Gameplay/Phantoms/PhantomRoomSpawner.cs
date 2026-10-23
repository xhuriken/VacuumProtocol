using System.Collections.Generic;
using Mirror;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;

// Disambiguate attributes shared between Mirror and Sirenix.OdinInspector
using ShowInInspector = Sirenix.OdinInspector.ShowInInspectorAttribute;
using ReadOnly = Sirenix.OdinInspector.ReadOnlyAttribute;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Gameplay.Phantoms
{
    /// <summary>
    /// Description: Method used to choose which PhantomSpawnPoint to activate during a spawn event.
    /// </summary>
    public enum SpawnPointSelectionMode
    {
        Random,
        RoundRobin,
        FurthestFromPlayers
    }

    /// <summary>
    /// Description: Intelligent room spawner managing phantom encounters on a per-room basis.
    /// Context: Placed on a room GameObject or trigger volume in gameplay levels. Runs server-side in Mirror.
    /// Justification: Detects when players are inside room bounds, spawns entities up to a concurrent quota
    /// on a regular interval (e.g. 5s), aligns them to wall/floor/ceiling spawn plates, and recycles quota
    /// automatically when phantoms are defeated via delegate callbacks.
    /// </summary>
    [SelectionBase]
    public class PhantomRoomSpawner : NetworkBehaviour
    {
        #region Inspector Settings
        [Title("Room Boundaries & Detection")]
        [Tooltip("Role: Collider defining the physical bounds of this room.\nUse Case: Detects if any player is currently inside.\nJustification: Mandatory reference; fail loudly if not configured per AGENTS.md rules.")]
        [Required]
        [SerializeField] private Collider _roomCollider;

        [Tooltip("Role: Frequency in seconds at which player presence is evaluated.")]
        [SerializeField] private float _playerCheckInterval = 0.3f;

        [Title("Cadence & Quota Settings")]
        [Tooltip("Role: Delay in seconds between spawn attempts while players are in the room.")]
        [SerializeField] private float _spawnInterval = 5.0f;

        [Tooltip("Role: Maximum number of alive phantoms allowed concurrently from this room.")]
        [SerializeField] private int _maxConcurrentPhantoms = 3;

        [Tooltip("Role: Total budget of phantoms this room can spawn before depleting (-1 = infinite respawns).")]
        [SerializeField] private int _totalRoomBudget = -1;

        [Tooltip("Role: If true, attempts to spawn immediately when a player first enters an empty room.")]
        [SerializeField] private bool _spawnImmediatelyOnFirstEntry = true;

        [Tooltip("Role: Suspense delay before the initial spawn triggers when a player enters.")]
        [SerializeField] private float _firstEntryDelay = 1.0f;

        [Title("Prefabs & Spawn Points")]
        [Tooltip("Role: Pool of phantom prefabs eligible to spawn in this room.")]
        [Required]
        [SerializeField] private List<GameObject> _phantomPrefabs = new List<GameObject>();

        [Tooltip("Role: Oriented surface spawn points (walls, floors, ceiling portals) in this room.")]
        [SerializeField] private List<PhantomSpawnPoint> _spawnPoints = new List<PhantomSpawnPoint>();

        [Tooltip("Role: Strategy for selecting among available spawn points.")]
        [SerializeField] private SpawnPointSelectionMode _selectionMode = SpawnPointSelectionMode.Random;

        [Title("Gizmos & Diagnostics")]
        [SerializeField] private bool _drawGizmos = true;
        [SerializeField] private Color _roomEmptyColor = new Color(0.2f, 0.8f, 0.4f, 0.15f);
        [SerializeField] private Color _roomOccupiedColor = new Color(1f, 0.2f, 0.2f, 0.25f);
        #endregion

        #region Runtime State
        [Title("Runtime State (Read Only)")]
        [ShowInInspector, ReadOnly]
        private readonly List<PhantomBase> _activeRoomPhantoms = new List<PhantomBase>();

        [ShowInInspector, ReadOnly]
        private int _totalSpawnedCount = 0;

        [ShowInInspector, ReadOnly]
        private bool _hasPlayersInRoom = false;

        [ShowInInspector, ReadOnly]
        private float _spawnTimer = 0f;

        [ShowInInspector, ReadOnly]
        private int _roundRobinIndex = 0;

        private float _lastPlayerCheckTime = -999f;
        #endregion

        #region Public Properties
        public bool HasPlayersInRoom => _hasPlayersInRoom;
        public int ActiveCount => _activeRoomPhantoms.Count;
        public int MaxConcurrent => _maxConcurrentPhantoms;
        public int TotalSpawned => _totalSpawnedCount;
        public int TotalBudget => _totalRoomBudget;
        public bool IsDepleted => _totalRoomBudget >= 0 && _totalSpawnedCount >= _totalRoomBudget;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // Auto-fallback search if not assigned in inspector
            if (_roomCollider == null)
            {
                _roomCollider = GetComponent<Collider>();
            }

            // STRICT: Fail loudly if missing required components
            if (_roomCollider == null)
            {
                Debug.LogError($"[PhantomRoomSpawner] Critical Configuration Error: '_roomCollider' is NOT assigned on '{gameObject.name}'! Fail loudly per workspace rules.", this);
            }

            if (_phantomPrefabs == null || _phantomPrefabs.Count == 0)
            {
                Debug.LogError($"[PhantomRoomSpawner] Critical Configuration Error: '_phantomPrefabs' list is empty on '{gameObject.name}'! Fail loudly per workspace rules.", this);
            }

            if (_spawnPoints == null || _spawnPoints.Count == 0)
            {
                // Attempt to auto-collect from children
                AutoCollectChildSpawnPoints();
                if (_spawnPoints.Count == 0)
                {
                    Debug.LogError($"[PhantomRoomSpawner] Critical Configuration Error: No 'PhantomSpawnPoint' found on or under '{gameObject.name}'! Fail loudly per workspace rules.", this);
                }
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _spawnTimer = _firstEntryDelay;
        }

        [ServerCallback]
        private void Update()
        {
            if (!isServer) return;

            // 1. Maintain clean list of alive phantoms (in case of unexpected destruction)
            _activeRoomPhantoms.RemoveAll(p => p == null);

            // 2. Throttled player presence check
            if (Time.time - _lastPlayerCheckTime >= _playerCheckInterval)
            {
                _lastPlayerCheckTime = Time.time;
                UpdatePlayerPresence();
            }

            // 3. If room is depleted or no players are inside, hold timers
            if (!_hasPlayersInRoom || IsDepleted)
            {
                return;
            }

            // 4. Tick spawn timer
            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer <= 0f)
            {
                _spawnTimer = _spawnInterval;

                if (_activeRoomPhantoms.Count < _maxConcurrentPhantoms)
                {
                    SpawnNextPhantom();
                }
            }
        }
        #endregion

        #region Player Detection Logic
        /// <summary>
        /// Description: Evaluates PlayerV2_Controller.ActivePlayers registry to check if any player is inside the room collider.
        /// Zero allocation, high-performance O(N_players) check.
        /// </summary>
        private void UpdatePlayerPresence()
        {
            if (_roomCollider == null) return;

            bool wasOccupied = _hasPlayersInRoom;
            bool isNowOccupied = false;

            var activePlayers = PlayerV2_Controller.ActivePlayers;
            for (int i = 0; i < activePlayers.Count; i++)
            {
                PlayerV2_Controller player = activePlayers[i];
                if (player == null) continue;

                Vector3 playerPos = player.WorldPosition;
                if (_roomCollider.bounds.Contains(playerPos))
                {
                    // Exact check for mesh / non-box colliders
                    Vector3 closest = _roomCollider.ClosestPoint(playerPos);
                    if ((closest - playerPos).sqrMagnitude < 0.04f)
                    {
                        isNowOccupied = true;
                        break;
                    }
                }
            }

            _hasPlayersInRoom = isNowOccupied;

            // Fresh entry: trigger immediate spawn or quick suspense timer
            if (!wasOccupied && isNowOccupied)
            {
                Debug.Log($"<color=yellow>[PhantomRoomSpawner]</color> Players entered room '{gameObject.name}'. Arming spawner.");
                if (_spawnImmediatelyOnFirstEntry && _activeRoomPhantoms.Count < _maxConcurrentPhantoms)
                {
                    _spawnTimer = _firstEntryDelay;
                }
                else
                {
                    _spawnTimer = _spawnInterval;
                }
            }
        }
        #endregion

        #region Spawning Execution
        /// <summary>
        /// Description: Authoritative server method instantiating a phantom at a selected spawn point.
        /// </summary>
        [Button("Force Spawn Now", ButtonSizes.Medium)]
        [GUIColor(0.2f, 0.9f, 0.4f)]
        public GameObject SpawnNextPhantom()
        {
            if (!isServer)
            {
                Debug.LogWarning("[PhantomRoomSpawner] Spawning can only occur on the Server/Host.");
                return null;
            }

            if (IsDepleted)
            {
                Debug.LogWarning($"[PhantomRoomSpawner] Room '{gameObject.name}' has reached its total budget ({_totalRoomBudget}). No more spawns.");
                return null;
            }

            if (_activeRoomPhantoms.Count >= _maxConcurrentPhantoms)
            {
                Debug.LogWarning($"[PhantomRoomSpawner] Max concurrent limit ({_maxConcurrentPhantoms}) reached for room '{gameObject.name}'.");
                return null;
            }

            if (_phantomPrefabs == null || _phantomPrefabs.Count == 0)
            {
                Debug.LogError($"[PhantomRoomSpawner] Cannot spawn: '_phantomPrefabs' is empty on '{gameObject.name}'.");
                return null;
            }

            if (_spawnPoints == null || _spawnPoints.Count == 0)
            {
                Debug.LogError($"[PhantomRoomSpawner] Cannot spawn: '_spawnPoints' is empty on '{gameObject.name}'.");
                return null;
            }

            // 1. Select Spawn Point
            PhantomSpawnPoint selectedPoint = SelectSpawnPoint();
            if (selectedPoint == null)
            {
                Debug.LogError($"[PhantomRoomSpawner] Failed to select a valid spawn point in room '{gameObject.name}'.");
                return null;
            }

            // 2. Select Prefab (random pool)
            GameObject prefab = _phantomPrefabs[Random.Range(0, _phantomPrefabs.Count)];
            if (prefab == null)
            {
                Debug.LogError($"[PhantomRoomSpawner] Selected prefab at random index was null in room '{gameObject.name}'!");
                return null;
            }

            // 3. Compute Position & Emergence Orientation
            Vector3 spawnPos = selectedPoint.SurfacePosition + selectedPoint.EmergenceDirection * 0.15f;
            Quaternion spawnRot = Quaternion.LookRotation(selectedPoint.EmergenceDirection, Vector3.up);

            // If the prefab requires a NavMeshAgent (e.g. ground-based PhantomStandard), ensure it snaps to the nearest valid NavMesh
            if (prefab.GetComponent<NavMeshAgent>() != null)
            {
                if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 4.0f, NavMesh.AllAreas))
                {
                    spawnPos = hit.position;
                }
                else
                {
                    Debug.LogError($"[PhantomRoomSpawner] Could not sample NavMesh near spawn point '{selectedPoint.name}' for NavMeshAgent prefab '{prefab.name}'!");
                }
            }

            // 4. Instantiate & Spawn on NetworkServer
            GameObject instance = Instantiate(prefab, spawnPos, spawnRot);
            NetworkServer.Spawn(instance);

            // 5. Register with local room tracker & listen for defeat delegate
            PhantomBase phantomBase = instance.GetComponent<PhantomBase>();
            if (phantomBase != null)
            {
                _activeRoomPhantoms.Add(phantomBase);
                phantomBase.OnDefeated += HandlePhantomDefeated;
            }
            else
            {
                Debug.LogWarning($"[PhantomRoomSpawner] Spawned prefab '{prefab.name}' does not contain a 'PhantomBase' component. Defeat callbacks won't trigger.");
            }

            _totalSpawnedCount++;
            Debug.Log($"<color=cyan>[PhantomRoomSpawner]</color> Spawned '{instance.name}' from portal '{selectedPoint.name}'. Active: {_activeRoomPhantoms.Count}/{_maxConcurrentPhantoms} (Total: {_totalSpawnedCount})");

            return instance;
        }

        /// <summary>
        /// Description: Delegate handler invoked when a tracked phantom is defeated or destroyed.
        /// Immediately frees the concurrency quota for this room.
        /// </summary>
        private void HandlePhantomDefeated(PhantomBase phantom)
        {
            if (phantom == null) return;

            phantom.OnDefeated -= HandlePhantomDefeated;
            if (_activeRoomPhantoms.Remove(phantom))
            {
                Debug.Log($"<color=magenta>[PhantomRoomSpawner]</color> Phantom '{phantom.name}' defeated! Room '{gameObject.name}' active count: {_activeRoomPhantoms.Count}/{_maxConcurrentPhantoms}");
            }
        }

        /// <summary>
        /// Description: Chooses a spawn point based on the configured selection strategy.
        /// </summary>
        private PhantomSpawnPoint SelectSpawnPoint()
        {
            // Filter null entries
            _spawnPoints.RemoveAll(p => p == null);
            if (_spawnPoints.Count == 0) return null;

            switch (_selectionMode)
            {
                case SpawnPointSelectionMode.RoundRobin:
                    PhantomSpawnPoint rrPoint = _spawnPoints[_roundRobinIndex % _spawnPoints.Count];
                    _roundRobinIndex = (_roundRobinIndex + 1) % _spawnPoints.Count;
                    return rrPoint;

                case SpawnPointSelectionMode.FurthestFromPlayers:
                    return GetFurthestSpawnPointFromPlayers();

                case SpawnPointSelectionMode.Random:
                default:
                    return _spawnPoints[Random.Range(0, _spawnPoints.Count)];
            }
        }

        private PhantomSpawnPoint GetFurthestSpawnPointFromPlayers()
        {
            var activePlayers = PlayerV2_Controller.ActivePlayers;
            if (activePlayers == null || activePlayers.Count == 0)
            {
                return _spawnPoints[Random.Range(0, _spawnPoints.Count)];
            }

            PhantomSpawnPoint bestPoint = null;
            float maxMinDistSqr = -1f;

            for (int i = 0; i < _spawnPoints.Count; i++)
            {
                PhantomSpawnPoint pt = _spawnPoints[i];
                if (pt == null) continue;

                // Find closest player to this point
                float closestPlayerDistSqr = float.MaxValue;
                for (int p = 0; p < activePlayers.Count; p++)
                {
                    PlayerV2_Controller player = activePlayers[p];
                    if (player == null) continue;

                    float dSqr = (player.WorldPosition - pt.SurfacePosition).sqrMagnitude;
                    if (dSqr < closestPlayerDistSqr)
                    {
                        closestPlayerDistSqr = dSqr;
                    }
                }

                if (closestPlayerDistSqr > maxMinDistSqr)
                {
                    maxMinDistSqr = closestPlayerDistSqr;
                    bestPoint = pt;
                }
            }

            return bestPoint != null ? bestPoint : _spawnPoints[0];
        }

        /// <summary>
        /// Description: Cleans up and destroys all active phantoms spawned by this room.
        /// </summary>
        [Button("Clear Room Phantoms", ButtonSizes.Small)]
        [GUIColor(1f, 0.4f, 0.4f)]
        public void ClearRoomPhantoms()
        {
            if (!isServer)
            {
                Debug.LogWarning("[PhantomRoomSpawner] ClearRoomPhantoms can only be executed on the Server/Host.");
                return;
            }

            for (int i = _activeRoomPhantoms.Count - 1; i >= 0; i--)
            {
                PhantomBase phantom = _activeRoomPhantoms[i];
                if (phantom != null)
                {
                    phantom.OnDefeated -= HandlePhantomDefeated;
                    NetworkServer.Destroy(phantom.gameObject);
                }
            }

            _activeRoomPhantoms.Clear();
            Debug.Log($"<color=orange>[PhantomRoomSpawner]</color> All active phantoms in '{gameObject.name}' cleared.");
        }
        #endregion

        #region Level Design Utilities
        /// <summary>
        /// Description: Scans all children for PhantomSpawnPoint components and populates the spawn points list.
        /// </summary>
        [Button("Auto-Collect Child Spawn Points", ButtonSizes.Medium)]
        [GUIColor(0.4f, 0.7f, 1f)]
        public void AutoCollectChildSpawnPoints()
        {
            PhantomSpawnPoint[] found = GetComponentsInChildren<PhantomSpawnPoint>(true);
            _spawnPoints.Clear();
            _spawnPoints.AddRange(found);

            Debug.Log($"<color=cyan>[PhantomRoomSpawner]</color> Collected {_spawnPoints.Count} child spawn points on '{gameObject.name}'.");
        }
        #endregion

        #region Editor Gizmos
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_drawGizmos) return;

            // 1. Draw Room Collider Bounds
            if (_roomCollider != null)
            {
                Bounds b = _roomCollider.bounds;
                Color roomCol = _hasPlayersInRoom ? _roomOccupiedColor : _roomEmptyColor;

                Gizmos.color = roomCol;
                Gizmos.DrawCube(b.center, b.size);

                Gizmos.color = new Color(roomCol.r, roomCol.g, roomCol.b, 0.8f);
                Gizmos.DrawWireCube(b.center, b.size);

                // 2. Draw lines to child spawn points
                if (_spawnPoints != null)
                {
                    Gizmos.color = new Color(0.7f, 0.3f, 1f, 0.5f);
                    for (int i = 0; i < _spawnPoints.Count; i++)
                    {
                        if (_spawnPoints[i] != null)
                        {
                            Gizmos.DrawLine(b.center, _spawnPoints[i].SurfacePosition);
                        }
                    }
                }

                // 3. Status Scene GUI Badge
                GUIStyle labelStyle = new GUIStyle();
                labelStyle.normal.textColor = _hasPlayersInRoom ? Color.red : Color.green;
                labelStyle.fontStyle = FontStyle.Bold;
                labelStyle.fontSize = 11;

                string statusText = $"[ROOM SPAWNER: {gameObject.name}]\n" +
                                    $"Players: {(_hasPlayersInRoom ? "OCCUPIED" : "EMPTY")}\n" +
                                    $"Alive: {_activeRoomPhantoms.Count}/{_maxConcurrentPhantoms}\n" +
                                    $"Spawn Timer: {_spawnTimer:F1}s";

                Handles.Label(b.center + Vector3.up * (b.extents.y + 0.5f), statusText, labelStyle);
            }
        }
#endif
        #endregion
    }
}
