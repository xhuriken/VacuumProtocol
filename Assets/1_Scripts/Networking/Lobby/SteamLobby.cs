using Mirror;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
//https://www.youtube.com/watch?v=7Eoc8U8TWa8&list=PLfFBezYu5hogMS3QeJkM1FQfl3s1sCzwV&index=6
/// <summary>
/// Description: Manages Steam lobby creation, joining, and integration with Mirror network manager.
/// Context: Attached to the NetworkManager. Runs on Start.
/// Justification: Bridges the gap between Steamworks.NET matchmaking callbacks and Mirror's hosting/joining functionality.
/// </summary>
public class SteamLobby : MonoBehaviour
{
    /// <summary>
    /// Description: Singleton instance of the SteamLobby.
    /// Context: Used by UI scripts to access the active lobby data.
    /// Justification: We only ever have one lobby active at a time per game instance.
    /// </summary>
    public static SteamLobby Instance;

    // Steam Callbacks
    protected Callback<LobbyCreated_t> LobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> JoinRequest;
    protected Callback<LobbyEnter_t> LobbyEntered;

    /// <summary>
    /// Description: The unique ID of the currently joined Steam lobby.
    /// Context: Used by UI scripts to fetch lobby metadata.
    /// Justification: Required for all subsequent SteamMatchmaking API calls regarding the current lobby.
    /// </summary>
    [Tooltip("Role: The active Steam lobby ID.\nUse Case: API interactions.\nJustification: Exposed for inspector debugging.")]
    public ulong CurrentLobbyId;
    
    private const string HostAddressKey = "HostAddress";
    private MyNetworkManager manager;

    /// <summary>
    /// Description: Initializes Steam callbacks and references.
    /// Context: Unity Start lifecycle event.
    /// Justification: We must bind Steamworks delegates immediately to ensure we don't miss incoming lobby invites or creation responses.
    /// </summary>
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
    /// Description: Initiates the creation of a Steam lobby.
    /// Context: Called by the "Host Game" UI button.
    /// Justification: Instructs Steam servers to allocate a new lobby for friends to join.
    /// </summary>
    public void HostLobby()
    {
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, manager.maxConnections);
    }

    /// <summary>
    /// Description: Handler for when a Steam lobby is successfully created.
    /// Context: Invoked by Steam callback.
    /// Justification: This is where we safely start the Mirror Host, now that Steam has confirmed the lobby exists.
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
    /// Description: Handler for when a join request is received (e.g., through Steam friends list).
    /// Context: Invoked by Steam callback when accepting an invite.
    /// Justification: Tells the Steam client to attempt joining the target lobby ID.
    /// </summary>
    /// <param name="callback">Data containing the lobby ID to join.</param>
    private void OnJoinRequest(GameLobbyJoinRequested_t callback)
    {
        Debug.Log("Join Request Received");
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    /// <summary>
    /// Description: Handler for when the player successfully enters a Steam lobby.
    /// Context: Invoked by Steam callback after a successful join attempt.
    /// Justification: We extract the host's Steam ID from the lobby data and tell Mirror to connect to it.
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
