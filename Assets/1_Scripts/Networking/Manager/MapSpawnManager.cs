using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Description: Dynamically manages player spawn points in a map.
/// Context: Placed in the gameplay scene (Level).
/// Justification: Lets designers define spawn positions and orientations for each player from the Inspector, up to 10 players.
/// </summary>
public class MapSpawnManager : MonoBehaviour
{
    /// <summary>
    /// Description: Static singleton instance for easy global access from the NetworkManager.
    /// </summary>
    public static MapSpawnManager Instance { get; private set; }

    [Tooltip("Role: List of spawn points available in the scene.\nUse Case: Assign the spawn Transforms (position and rotation).\nJustification: Enables dynamic spawning based on the number of players.")]
    [SerializeField]

    private List<Transform> _spawnPoints = new List<Transform>();

    private int _nextSpawnIndex = 0;

    private void Awake()
    {
        // Initialize the Singleton instance
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        Instance = this;

        // Auto-register spawn points for standard Mirror NetworkManager (e.g. HUD testing)
        foreach (Transform spawnPoint in _spawnPoints)
        {
            if (spawnPoint != null)
            {
                Mirror.NetworkManager.RegisterStartPosition(spawnPoint);
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        // Unregister spawn points
        foreach (Transform spawnPoint in _spawnPoints)
        {
            if (spawnPoint != null)
            {
                Mirror.NetworkManager.UnRegisterStartPosition(spawnPoint);
            }
        }
    }

    /// <summary>
    /// Description: Returns the next available spawn point.
    /// Context: Called by the NetworkManager when a player spawns in game.
    /// Justification: Distributes players across the spawn points in round-robin order.
    /// </summary>
    /// <returns>Spawn point Transform, or null if none is configured.</returns>
    public Transform GetNextSpawnPoint()
    {
        if (_spawnPoints == null || _spawnPoints.Count == 0)
        {
            Debug.LogError("[MapSpawnManager] No spawn point is configured in the _spawnPoints list!");
            return null;
        }

        Transform spawnPoint = _spawnPoints[_nextSpawnIndex];

        // Increment the index circularly to distribute players

        _nextSpawnIndex = (_nextSpawnIndex + 1) % _spawnPoints.Count;

        return spawnPoint;
    }
}
