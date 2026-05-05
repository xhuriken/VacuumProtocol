using System.Collections.Generic;
using Mirror;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MyNetworkManager : NetworkManager
{

    [SerializeField] private PlayerObjectController _playerPrefab;
    [SerializeField] private GameObject _gamePlayerPrefab;
    public List<PlayerObjectController> GamePlayers { get; } = new List<PlayerObjectController>();

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        string sceneName = SceneManager.GetActiveScene().name;

        if (sceneName == "Lobby")
        {
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
            
            // Set the connection ID for voice mapping
            if (gamePlayerInstance.TryGetComponent(out PlayerPhysicsMovement movement))
            {
                movement.ConnectionId = conn.connectionId;
            }

            // This replaces the old player object with the new mecha for this connection
            NetworkServer.AddPlayerForConnection(conn, gamePlayerInstance);
            Debug.Log($"[MyNetwork] Game Player (Mecha) Added in {sceneName} for connection {conn.connectionId}");
        }
    }
    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("[MyNetwork] Server started !");
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        Debug.Log("[MyNetwork] Server Stopped !");
    }
    public override void OnClientConnect()
    {
        base.OnClientConnect();
        Debug.Log("[MyNetwork] Client connected !");
    }

    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();
        Debug.Log("[MyNetwork] Client Disconnected !");
    }
}
