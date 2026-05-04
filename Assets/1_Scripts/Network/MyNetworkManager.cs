using System.Collections.Generic;
using Mirror;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MyNetworkManager : NetworkManager
{

    [SerializeField] private PlayerObjectController _playerPrefab;
    public List<PlayerObjectController> GamePlayers { get; } = new List<PlayerObjectController>();

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        //base.OnServerAddPlayer(conn);
        if (SceneManager.GetActiveScene().name == "Lobby")
        {
            PlayerObjectController _playerInstance = Instantiate(_playerPrefab);
            _playerInstance.ConnectionId = conn.connectionId;
            _playerInstance.PlayerId = GamePlayers.Count + 1;
            _playerInstance.PlayerSteamId = (ulong)SteamMatchmaking.GetLobbyMemberByIndex((CSteamID)SteamLobby.Instance.CurrentLobbyId, GamePlayers.Count);

            NetworkServer.AddPlayerForConnection(conn, _playerInstance.gameObject);
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
