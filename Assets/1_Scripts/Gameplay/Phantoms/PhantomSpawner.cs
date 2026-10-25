using System.Collections.Generic;
using Mirror;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;

namespace Gameplay.Phantoms
{
    /// <summary>
    /// Description: Spawns and manages Phantom enemy instances in the level.
    /// Context: Placed in gameplay or test scenes. Runs server-side in Mirror multiplayer.
    /// Justification: Automates phantom instantiation on the NavMesh, enforces max instance limits, and exposes Odin Inspector buttons for testing.
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    public class PhantomSpawner : NetworkBehaviour
    {
        [Header("Prefab & Spawning Settings")]
        [Tooltip("Role: The Phantom prefab to instantiate.\nUse Case: Prefab must contain NetworkIdentity, NetworkTransform, and PhantomStandard.\nJustification: Essential template for spawning.")]
        [Required]
        [SerializeField] private GameObject _phantomPrefab;

        [Tooltip("Role: Optional fixed spawn locations in the scene.\nUse Case: If assigned, cycles through these points; if empty, samples random points on the NavMesh.")]
        [SerializeField] private List<Transform> _spawnPoints = new List<Transform>();

        [Tooltip("Role: Number of phantoms to spawn immediately when the server starts.")]
        [SerializeField] private int _initialSpawnCount = 1;

        [Tooltip("Role: Maximum number of active phantoms allowed concurrently.")]
        [SerializeField] private int _maxAlivePhantoms = 5;

        [Tooltip("Role: Search radius around this spawner when finding random NavMesh spawn positions.")]
        [SerializeField] private float _randomSpawnRadius = 15f;

        [Header("Runtime Status")]
        [Sirenix.OdinInspector.ShowInInspector, Sirenix.OdinInspector.ReadOnly]
        private readonly List<GameObject> _alivePhantoms = new List<GameObject>();
        private int _currentSpawnIndex = 0;

        #region Unity Lifecycle
        public override void OnStartServer()
        {
            base.OnStartServer();

            if (_phantomPrefab == null)
            {
                Debug.LogError($"[PhantomSpawner] Critical Error: '_phantomPrefab' is not assigned on '{gameObject.name}'! Fail loudly per workspace rules.");
                return;
            }

            for (int i = 0; i < _initialSpawnCount; i++)
            {
                SpawnPhantom();
            }
        }

        [ServerCallback]
        private void Update()
        {
            if (!NetworkServer.active || netIdentity == null || !isServer) return;

            // Clean up destroyed or disabled phantoms from the tracker list
            _alivePhantoms.RemoveAll(p => p == null);
        }
        #endregion

        #region Odin Inspector Actions & Spawning Logic
        /// <summary>
        /// Description: Spawns a phantom on the server and registers it with Mirror's NetworkServer.
        /// Context: Can be triggered directly in Unity via the Odin Inspector button during Play Mode.
        /// </summary>
        [Button("Spawn Phantom", ButtonSizes.Medium)]
        [GUIColor(0.3f, 0.8f, 1f)]
        public GameObject SpawnPhantom()
        {
            if (!NetworkServer.active || netIdentity == null || !isServer)
            {
                Debug.LogWarning("[PhantomSpawner] SpawnPhantom can only be executed on the Server/Host (must be in Play Mode with active server).");
                return null;
            }

            if (_phantomPrefab == null)
            {
                Debug.LogError($"[PhantomSpawner] Cannot spawn: '_phantomPrefab' is missing on '{gameObject.name}'.");
                return null;
            }

            if (_alivePhantoms.Count >= _maxAlivePhantoms)
            {
                Debug.LogWarning($"[PhantomSpawner] Max alive phantoms reached ({_maxAlivePhantoms}). Spawn request ignored.");
                return null;
            }

            Vector3 spawnPosition;
            Quaternion spawnRotation = Quaternion.identity;

            // 1. Try predefined spawn points if provided
            if (_spawnPoints != null && _spawnPoints.Count > 0)
            {
                Transform pt = _spawnPoints[_currentSpawnIndex];
                _currentSpawnIndex = (_currentSpawnIndex + 1) % _spawnPoints.Count;

                if (pt != null)
                {
                    spawnPosition = pt.position;
                    spawnRotation = pt.rotation;

                    // If a valid NavMesh exists near this spawn point, snap to the exact floor level
                    if (NavMesh.SamplePosition(spawnPosition, out NavMeshHit navHit, 5.0f, NavMesh.AllAreas))
                    {
                        spawnPosition = navHit.position;
                    }
                }
                else
                {
                    Debug.LogError($"[PhantomSpawner] Spawn point at index was null! Falling back to spawner position loudly.");
                    spawnPosition = transform.position;
                }
            }
            else
            {
                // 2. Pick a valid position on the NavMesh around the spawner
                Vector3 randomTarget = transform.position + Random.insideUnitSphere * _randomSpawnRadius;
                randomTarget.y = transform.position.y;

                if (NavMesh.SamplePosition(randomTarget, out NavMeshHit hit, _randomSpawnRadius, NavMesh.AllAreas))
                {
                    spawnPosition = hit.position;
                }
                else
                {
                    Debug.LogError($"[PhantomSpawner] Could not find valid NavMesh position near '{gameObject.name}'! Placing on spawner position.");
                    spawnPosition = transform.position;
                }
            }

            // 3. Instantiate and network spawn
            GameObject phantomInstance = Instantiate(_phantomPrefab, spawnPosition, spawnRotation);

            // If the prefab uses a NavMeshAgent, ensure it is properly warped onto the NavMesh
            NavMeshAgent agent = phantomInstance.GetComponent<NavMeshAgent>();
            if (agent != null && !agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(spawnPosition, out NavMeshHit agentHit, 5.0f, NavMesh.AllAreas))
                {
                    agent.Warp(agentHit.position);
                }
            }

            NetworkServer.Spawn(phantomInstance);

            _alivePhantoms.Add(phantomInstance);
            Debug.Log($"<color=cyan>[PhantomSpawner]</color> Successfully spawned phantom '{phantomInstance.name}' at position {spawnPosition}. Total alive: {_alivePhantoms.Count}");

            return phantomInstance;
        }

        /// <summary>
        /// Description: Cleans up and destroys all currently alive phantom instances on the server.
        /// </summary>
        [Button("Clear All Phantoms", ButtonSizes.Small)]
        [GUIColor(1f, 0.4f, 0.4f)]
        public void ClearAllPhantoms()
        {
            if (!NetworkServer.active || netIdentity == null || !isServer)
            {
                Debug.LogWarning("[PhantomSpawner] ClearAllPhantoms can only be executed on the Server/Host.");
                return;
            }

            for (int i = _alivePhantoms.Count - 1; i >= 0; i--)
            {
                GameObject phantom = _alivePhantoms[i];
                if (phantom != null)
                {
                    NetworkServer.Destroy(phantom);
                }
            }

            _alivePhantoms.Clear();
            Debug.Log("<color=orange>[PhantomSpawner]</color> All active phantoms destroyed.");
        }
        #endregion
    }
}
