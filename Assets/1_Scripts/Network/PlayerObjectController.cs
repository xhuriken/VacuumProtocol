using UnityEngine;
using Mirror;
using Steamworks;

public class PlayerObjectController : NetworkBehaviour
{
    //Player Data
    [SyncVar] public int ConnecionId;
    [SyncVar] public int PlayerId;
    [SyncVar] public ulong PlayerSteamId;
    [SyncVar(hook = nameof(PlayerNameUpdate))] public string PlayerName;

    private MyNetworkManager _manager;

    private MyNetworkManager Manager
    {
        get
        {
            if(_manager != null) { return _manager; }
            return _manager = MyNetworkManager.singleton as MyNetworkManager;
        }

    }

    public override void OnStartAuthority()
    {
        //base.OnStartAuthority();
        CmdSetPlayerName(SteamFriends.GetPersonaName());
        gameObject.name = "LocalGamePlayer";
        LobbyController.Intance.FindLocalPlayer();
        LobbyController.Intance.UpdateLobbyName();
    }

    public override void OnStartClient()
    {
        //base.OnStartClient();
        Manager.GamePlayers.Add(this);
        LobbyController.Intance.UpdateLobbyName();
        LobbyController.Intance.UpdatePlayerList();
    }

    public override void OnStopClient()
    {
        //base.OnStopClient();
        Manager.GamePlayers.Remove(this);
        LobbyController.Intance.UpdatePlayerList();
    }

    [Command]
    private void CmdSetPlayerName(string playerName)
    {
        this.PlayerNameUpdate(this.PlayerName, playerName);
    }

    public void PlayerNameUpdate(string OldValue, string NewValue)
    {
        if(isServer)
        {
            this.PlayerName = NewValue;
        }
        if(isClient)
        {
            LobbyController.Intance.UpdatePlayerList();
        }
    }

}
