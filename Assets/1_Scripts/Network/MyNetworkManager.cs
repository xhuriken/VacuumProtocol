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

    public override void OnServerSceneChanged(string sceneName)
    {
        if (sceneName.StartsWith("SteamTest")) // Using StartsWith in case of variations
        {
            // We need a temporary list because ReplacePlayerForConnection might affect the original list
            List<PlayerObjectController> playersToReplace = new List<PlayerObjectController>(GamePlayers);

            foreach (var lobbyPlayer in playersToReplace)
            {
                GameObject gamePlayerInstance = Instantiate(_gamePlayerPrefab);
                
                // Mirror logic: Replace the Lobby Player object with the Game Player object
                // for this specific connection. The old object is destroyed automatically.
                NetworkServer.ReplacePlayerForConnection(lobbyPlayer.connectionToClient, gamePlayerInstance);
            }
        }
    }

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
