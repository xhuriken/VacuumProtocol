using System.Collections.Generic;
using Mirror;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Description: Custom Network Manager that handles player spawning and connection lifecycle.
/// Context: Attached to the NetworkManager GameObject in the persistent Lobby scene.
/// Justification: Overrides Mirror's default NetworkManager to inject Steam lobby parameters and handle different prefabs for Lobby vs Gameplay.
/// </summary>
public class MyNetworkManager : NetworkManager
{
    [Tooltip("Role: The player representation used inside the Steam Lobby.\nUse Case: Spawning the UI proxy.\nJustification: We don't want to spawn the heavy physical robot in the UI lobby.")]
    [SerializeField] private PlayerObjectController _playerPrefab;
    
    [Tooltip("Role: The actual physical robot prefab used in the game level.\nUse Case: Spawning the gameplay avatar.\nJustification: Instantiated only when transitioning to a gameplay scene.")]
    [SerializeField] private GameObject _gamePlayerPrefab;

    /// <summary>
    /// Description: List of all player controllers currently connected to the game.
    /// Context: Maintained by OnClientConnect/Disconnect.
    /// Justification: Centralized access point for the UI to know who is in the lobby.
    /// </summary>
    public List<PlayerObjectController> GamePlayers { get; } = new List<PlayerObjectController>();

    /// <summary>
    /// Description: Called on the server when a new player joins. Spawns the appropriate prefab based on the current scene.
    /// Context: Server-side Mirror callback.
    /// Justification: Essential for distinguishing between a player joining the Lobby vs hot-joining the actual game (though hot-joining might be disabled later).
    /// </summary>
    /// <param name="conn">The connection representing the player.</param>
    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        string sceneName = SceneManager.GetActiveScene().name;

        if (sceneName == "Lobby")
        {
            // Spawn the lobby-specific player representation
            PlayerObjectController _playerInstance = Instantiate(_playerPrefab);
            _playerInstance.ConnectionId = conn.connectionId;
            _playerInstance.PlayerId = GamePlayers.Count + 1;
            _playerInstance.PlayerSteamId = (ulong)SteamMatchmaking.GetLobbyMemberByIndex((CSteamID)SteamLobby.Instance.CurrentLobbyId, GamePlayers.Count);

            NetworkServer.AddPlayerForConnection(conn, _playerInstance.gameObject);
            Debug.Log($"[MyNetwork] Lobby Player Added for connection {conn.connectionId}");
        }
        else if (sceneName.StartsWith("SteamTest"))
        {
            // Spawn the actual game player (Mecha)
            GameObject gamePlayerInstance = Instantiate(_gamePlayerPrefab);

            // Link the connection ID for voice synchronization
            if (gamePlayerInstance.TryGetComponent(out PlayerController controller))
            {
                controller.ConnectionId = conn.connectionId;
            }

            // Assign the new mecha object as the player's primary object
            NetworkServer.AddPlayerForConnection(conn, gamePlayerInstance);
            Debug.Log($"[MyNetwork] Game Player (Mecha) Added in {sceneName} for connection {conn.connectionId}");
        }
    }

    /// <summary>
    /// Description: Callback when the server starts.
    /// Context: Mirror NetworkManager override.
    /// Justification: Useful for server-side initialization logging.
    /// </summary>
    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("[MyNetwork] Server started!");
    }

    /// <summary>
    /// Description: Callback when the server stops.
    /// Context: Mirror NetworkManager override.
    /// Justification: Useful for server-side cleanup logging.
    /// </summary>
    public override void OnStopServer()
    {
        base.OnStopServer();
        Debug.Log("[MyNetwork] Server Stopped!");
    }

    /// <summary>
    /// Description: Callback when a client connects.
    /// Context: Mirror NetworkManager override.
    /// Justification: Useful for client-side initialization logging.
    /// </summary>
    public override void OnClientConnect()
    {
        base.OnClientConnect();
        Debug.Log("[MyNetwork] Client connected!");
    }

    /// <summary>
    /// Description: Callback when a client disconnects.
    /// Context: Mirror NetworkManager override.
    /// Justification: Useful for client-side cleanup logging.
    /// </summary>
    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();
        Debug.Log("[MyNetwork] Client Disconnected!");
    }
}
