using Mirror;
using Steamworks;
using UnityEngine;

/// <summary>
/// Manages networked player data and actions within the lobby environment.
/// </summary>
public class PlayerObjectController : NetworkBehaviour
{
    [Header("Networked Player Data")]
    [SyncVar] public int ConnectionId;
    [SyncVar] public int PlayerId;
    [SyncVar] public ulong PlayerSteamId;
    [SyncVar(hook = nameof(PlayerNameUpdate))] public string PlayerName;
    [SyncVar(hook = nameof(PlayerReadyUpdate))] public bool Ready;

    private MyNetworkManager _manager;
    private MyNetworkManager Manager
    {
        get
        {
            if (_manager != null) { return _manager; }
            return _manager = MyNetworkManager.singleton as MyNetworkManager;
        }
    }

    /// <summary>
    /// Hook called when the Ready status is synchronized across the network.
    /// </summary>
    public void PlayerReadyUpdate(bool OldValue, bool NewValue)
    {
        if (isServer)
        {
            this.Ready = NewValue;
        }
        if (isClient)
        {
            LobbyController.Instance.UpdatePlayerList();
        }
    }

    /// <summary>
    /// Command to toggle the ready status on the server.
    /// </summary>
    [Command]
    private void CmdSetPlayerReady()
    {
        this.PlayerReadyUpdate(this.Ready, !this.Ready);
    }

    /// <summary>
    /// Initiates a ready status change for the local player.
    /// </summary>
    public void ChangeReady()
    {
        if (isOwned)
        {
            CmdSetPlayerReady();
        }
    }

    /// <summary>
    /// Requests the server to start the game.
    /// </summary>
    /// <param name="sceneName">The name of the scene to load.</param>
    public void CanStartGame(string sceneName)
    {
        if (isOwned)
        {
            CmdStartGame(sceneName);
        }
    }

    /// <summary>
    /// Command to trigger a scene change on the server.
    /// </summary>
    [Command]
    public void CmdStartGame(string sceneName)
    {
        Manager.ServerChangeScene(sceneName);
    }

    public override void OnStartAuthority()
    {
        CmdSetPlayerName(SteamFriends.GetPersonaName());
        gameObject.name = "LocalGamePlayer";
        LobbyController.Instance.FindLocalPlayer();
        LobbyController.Instance.UpdateLobbyName();
    }

    public override void OnStartClient()
    {
        Manager.GamePlayers.Add(this);
        LobbyController.Instance.UpdateLobbyName();
        LobbyController.Instance.UpdatePlayerList();
    }

    public override void OnStopClient()
    {
        Manager.GamePlayers.Remove(this);
        LobbyController.Instance.UpdatePlayerList();
    }

    /// <summary>
    /// Command to set the player name on the server.
    /// </summary>
    [Command]
    private void CmdSetPlayerName(string playerName)
    {
        this.PlayerName = playerName;
    }

    /// <summary>
    /// Hook called when the PlayerName is synchronized across the network.
    /// </summary>
    public void PlayerNameUpdate(string OldValue, string NewValue)
    {
        if (isServer)
        {
            this.PlayerName = NewValue;
        }
        if (isClient)
        {
            LobbyController.Instance.UpdatePlayerList();
        }
    }
}