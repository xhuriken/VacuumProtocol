using Mirror;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
//https://www.youtube.com/watch?v=7Eoc8U8TWa8&list=PLfFBezYu5hogMS3QeJkM1FQfl3s1sCzwV&index=6
public class SteamLobby : MonoBehaviour
{
    public static SteamLobby Instance;

    //CallBacks
    protected Callback<LobbyCreated_t> LobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> JoinRequest;
    protected Callback<LobbyEnter_t> LobbyEntered;

    public ulong CurrentLobbyId;
    private const string HostAddressKey = "HostAddress";
    private MyNetworkManager manager;

    //public GameObject HostButton;
    //public TextMeshProUGUI LobbyNameText;


    private void Start()
    {
        if(!SteamManager.Initialized) { return; }
        if(Instance == null) Instance = this;

        manager = GetComponent<MyNetworkManager>();

        LobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        JoinRequest = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequest);
        LobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
    }

    public void HostLobby()
    {
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, manager.maxConnections);
    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if(callback.m_eResult != EResult.k_EResultOK) { Debug.LogError("LobbyCreated Error"); return; }
        Debug.Log("Lobby Created Succesfully");

        manager.StartHost();

        //6min30 https://www.youtube.com/watch?v=7Eoc8U8TWa8
        SteamMatchmaking.SetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), HostAddressKey, SteamUser.GetSteamID().ToString());
        SteamMatchmaking.SetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), "name", SteamFriends.GetPersonaName().ToString() + "'s Unit");
    }

    private void OnJoinRequest(GameLobbyJoinRequested_t callback)
    {
        Debug.Log("Join Request Receive");
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        Debug.Log("On Lobby Entered callback");
        // Everyone
        CurrentLobbyId = callback.m_ulSteamIDLobby;
        //LobbyNameText.gameObject.SetActive(true);
        //LobbyNameText.text = SteamMatchmaking.GetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), "name");

        //Client
        if (NetworkServer.active) { return; }

        manager.networkAddress = SteamMatchmaking.GetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), HostAddressKey);
        manager.StartClient();
    }

}
