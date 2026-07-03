using Mirror;
using Steamworks;
using UnityEngine;

/// <summary>
/// Description: Manages networked player data and actions within the lobby environment.
/// Context: Attached to the LobbyPlayer prefab.
/// Justification: Keeps state (Ready, Name) synchronized between clients using Mirror SyncVars.
/// </summary>
public class PlayerObjectController : NetworkBehaviour
{
    [Header("Networked Player Data")]
    [Tooltip("Role: The Mirror connection ID.\nUse Case: Synchronization.\nJustification: Identifies the player on the server.")]
    [SyncVar] public int ConnectionId;
    
    [Tooltip("Role: The sequential player index.\nUse Case: UI sorting / Host detection.\nJustification: PlayerId == 1 usually means the host.")]
    [SyncVar] public int PlayerId;
    
    [Tooltip("Role: The Steam ID.\nUse Case: Avatar fetching.\nJustification: Shared so all clients can load this player's Steam picture.")]
    [SyncVar] public ulong PlayerSteamId;
    
    [Tooltip("Role: The player's Steam display name.\nUse Case: UI display.\nJustification: Uses a hook to update the UI whenever it changes.")]
    [SyncVar(hook = nameof(PlayerNameUpdate))] public string PlayerName;
    
    [Tooltip("Role: The player's ready state.\nUse Case: Start logic.\nJustification: Uses a hook to update the UI whenever it changes.")]
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
    /// Description: Hook called when the Ready status is synchronized across the network.
    /// Context: SyncVar hook on 'Ready'.
    /// Justification: Automatically updates the Lobby UI without polling.
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
    /// Description: Command to toggle the ready status on the server.
    /// Context: Called by the client with authority.
    /// Justification: Clients cannot modify SyncVars directly; they must ask the server to do it.
    /// </summary>
    [Command]
    private void CmdSetPlayerReady()
    {
        this.PlayerReadyUpdate(this.Ready, !this.Ready);
    }

    /// <summary>
    /// Description: Initiates a ready status change for the local player.
    /// Context: Called by the UI (LobbyController).
    /// Justification: Wrapper to ensure only the local owner sends the Command.
    /// </summary>
    public void ChangeReady()
    {
        if (isOwned)
        {
            CmdSetPlayerReady();
        }
    }

    /// <summary>
    /// Description: Requests the server to start the game.
    /// Context: Called by the Host's UI.
    /// Justification: Safely wraps the CmdStartGame call.
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
    /// Description: Command to trigger a scene change on the server.
    /// Context: Called by the host.
    /// Justification: The server dictates scene changes for all connected clients.
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
    /// Description: Command to set the player name on the server.
    /// Context: Called upon gaining authority.
    /// Justification: Clients retrieve their local Steam name and must push it to the server for distribution.
    /// </summary>
    [Command]
    private void CmdSetPlayerName(string playerName)
    {
        this.PlayerName = playerName;
    }

    /// <summary>
    /// Description: Hook called when the PlayerName is synchronized across the network.
    /// Context: SyncVar hook on 'PlayerName'.
    /// Justification: Automatically updates the Lobby UI list without polling.
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