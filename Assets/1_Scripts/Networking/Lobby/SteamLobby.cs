using Mirror;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
//https://www.youtube.com/watch?v=7Eoc8U8TWa8&list=PLfFBezYu5hogMS3QeJkM1FQfl3s1sCzwV&index=6
/// <summary>
/// Manages Steam lobby creation, joining, and integration with Mirror network manager.
/// </summary>
public class SteamLobby : MonoBehaviour
{
    /// <summary>
    /// Singleton instance of the SteamLobby.
    /// </summary>
    public static SteamLobby Instance;

    // Steam Callbacks
    protected Callback<LobbyCreated_t> LobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> JoinRequest;
    protected Callback<LobbyEnter_t> LobbyEntered;

    /// <summary>
    /// The unique ID of the currently joined Steam lobby.
    /// </summary>
    public ulong CurrentLobbyId;
    
    private const string HostAddressKey = "HostAddress";
    private MyNetworkManager manager;

    private void Start()
    {
        if(!SteamManager.Initialized) { return; }
        if(Instance == null) Instance = this;

        manager = GetComponent<MyNetworkManager>();

        // Link Steam callbacks to their respective handler functions
        LobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        JoinRequest = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequest);
        LobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
    }

    /// <summary>
    /// Initiates the creation of a Steam lobby.
    /// </summary>
    public void HostLobby()
    {
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, manager.maxConnections);
    }

    /// <summary>
    /// Handler for when a Steam lobby is successfully created.
    /// </summary>
    /// <param name="callback">Data containing the lobby creation result.</param>
    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if(callback.m_eResult != EResult.k_EResultOK) 
        { 
            Debug.LogError("LobbyCreated Error"); 
            return; 
        }
        
        Debug.Log("Lobby Created Successfully");

        // Start the Mirror host
        manager.StartHost();

        // Store the host's Steam ID in the lobby data so clients know where to connect
        SteamMatchmaking.SetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), HostAddressKey, SteamUser.GetSteamID().ToString());
        SteamMatchmaking.SetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), "name", SteamFriends.GetPersonaName().ToString() + "'s Unit");
    }

    /// <summary>
    /// Handler for when a join request is received (e.g., through Steam friends list).
    /// </summary>
    /// <param name="callback">Data containing the lobby ID to join.</param>
    private void OnJoinRequest(GameLobbyJoinRequested_t callback)
    {
        Debug.Log("Join Request Received");
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    /// <summary>
    /// Handler for when the player successfully enters a Steam lobby.
    /// </summary>
    /// <param name="callback">Data containing the lobby entry result.</param>
    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        Debug.Log("On Lobby Entered callback");
        
        // Update the current lobby ID for all players
        CurrentLobbyId = callback.m_ulSteamIDLobby;

        // If we are already the host, we don't need to connect as a client
        if (NetworkServer.active) { return; }

        // Get the host address from lobby data and connect the Mirror client
        manager.networkAddress = SteamMatchmaking.GetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), HostAddressKey);
        manager.StartClient();
    }
}
