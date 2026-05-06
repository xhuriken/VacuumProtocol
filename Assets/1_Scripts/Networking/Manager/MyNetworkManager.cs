using System.Collections.Generic;
using Mirror;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Custom Network Manager that handles player spawning and connection lifecycle.
/// </summary>
public class MyNetworkManager : NetworkManager
{
    [SerializeField] private PlayerObjectController _playerPrefab;
    [SerializeField] private GameObject _gamePlayerPrefab;

    /// <summary>
    /// List of all player controllers currently connected to the game.
    /// </summary>
    public List<PlayerObjectController> GamePlayers { get; } = new List<PlayerObjectController>();

    /// <summary>
    /// Called on the server when a new player joins. Spawns the appropriate prefab based on the current scene.
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
            if (gamePlayerInstance.TryGetComponent(out PlayerPhysicsMovement movement))
            {
                movement.ConnectionId = conn.connectionId;
            }

            // Assign the new mecha object as the player's primary object
            NetworkServer.AddPlayerForConnection(conn, gamePlayerInstance);
            Debug.Log($"[MyNetwork] Game Player (Mecha) Added in {sceneName} for connection {conn.connectionId}");
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("[MyNetwork] Server started!");
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        Debug.Log("[MyNetwork] Server Stopped!");
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();
        Debug.Log("[MyNetwork] Client connected!");
    }

    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();
        Debug.Log("[MyNetwork] Client Disconnected!");
    }
}
