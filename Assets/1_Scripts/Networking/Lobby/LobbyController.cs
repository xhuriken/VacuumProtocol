using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using NUnit.Framework;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Description: Controls the lobby UI, manages the player list, and handles the game start logic.
/// Context: Attached to a persistent GameObject in the Lobby scene.
/// Justification: Coordinates between the SteamLobby backend and the visible UI frontend.
/// </summary>
public class LobbyController : MonoBehaviour
{
    /// <summary>
    /// Description: Singleton instance of the LobbyController.
    /// Context: Set on Awake.
    /// Justification: Allows external UI scripts and network callbacks to easily trigger UI refreshes without heavy FindObjectOfType calls.
    /// </summary>
    public static LobbyController Instance;

    [Header("UI Elements")]
    [Tooltip("Role: Displays the Steam lobby's name.\nUse Case: UI Update.\nJustification: Confirms to the user which lobby they joined.")]
    public TextMeshProUGUI LobbyNameText;

    [Header("Player Data")]
    [Tooltip("Role: Container for all player UI items.\nUse Case: Instantiation parent.\nJustification: A VerticalLayoutGroup usually manages this to list players.")]
    public GameObject PlayerListViewContent;
    
    [Tooltip("Role: The prefab for a single player in the list.\nUse Case: Instantiation.\nJustification: Contains the Steam avatar, name, and ready status.")]
    public GameObject PlayerListItemPrefab;
    
    [Tooltip("Role: Reference to the local player's network object.\nUse Case: Local state checks.\nJustification: Used to read the local Ready state for the button.")]
    public GameObject LocalPlayerObject;

    [Header("Lobby State")]
    [Tooltip("Role: Tracks the Steam Lobby ID.\nUse Case: State tracking.\nJustification: Exposed for inspector debugging.")]
    public ulong CurrentLobbyId;
    
    [Tooltip("Role: Tracks if the host's player item has been created.\nUse Case: Initialization flag.\nJustification: Ensures we don't duplicate the host in the list.")]
    public bool PlayerItemCreated = false;
    
    private List<PlayerListItem> PlayerListItems = new List<PlayerListItem>();
    
    [Tooltip("Role: Reference to the local player's controller script.\nUse Case: Network commands.\nJustification: Required to send CmdStartGame or CmdSetReady.")]
    public PlayerObjectController LocalPlayerController;

    [Header("Ready System")]
    [Tooltip("Role: Custom button component for starting the game.\nUse Case: Disabling/enabling the start button.\nJustification: Only interactable for the host when everyone is ready.")]
    public UICustomButtonBase StartGameButton;
    
    [Tooltip("Role: Text showing the Ready button status.\nUse Case: UI feedback.\nJustification: Switches between 'Plug' (Ready) and 'Unplug' (Not Ready).")]
    public TextMeshProUGUI ReadyButtonText;

    private MyNetworkManager _manager;
    /// <summary>
    /// Description: Accessor for the custom Network Manager.
    /// Context: Lazy initialization.
    /// Justification: Safely retrieves the manager without requiring manual assignment in the inspector.
    /// </summary>
    private MyNetworkManager Manager
    {
        get
        {
            if (_manager != null) { return _manager; }
            return _manager = MyNetworkManager.singleton as MyNetworkManager;
        }
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    /// <summary>
    /// Description: Toggles the ready state of the local player.
    /// Context: Called by the UI Ready button OnClick event.
    /// Justification: Only the local player can dictate their own ready state to the server.
    /// </summary>
    public void ReadyPlayer()
    {
        LocalPlayerController.ChangeReady();
    }

    /// <summary>
    /// Description: Starts the game for all connected players.
    /// Context: Called by the Host's UI Start button OnClick event.
    /// Justification: Initiates the scene transition via the NetworkManager.
    /// </summary>
    public void StartGame()
    {
        LocalPlayerController.CanStartGame("SteamTest");
    }

    /// <summary>
    /// Description: Updates the visual state of the ready button based on the local player's status.
    /// Context: Called when local ready state changes.
    /// Justification: Provides immediate visual feedback to the player.
    /// </summary>
    public void UpdateButton()
    {
        if (LocalPlayerController.Ready)
        {
            ReadyButtonText.text = "Unplug";
            ReadyButtonText.color = Color.red;
        }
        else
        {
            ReadyButtonText.text = "Plug";
            ReadyButtonText.color = Color.green;
        }
    }

    /// <summary>
    /// Description: Checks if all players are ready. Enables the start button if the host.
    /// Context: Called after any player's ready state is updated.
    /// Justification: Prevents the host from starting the game prematurely.
    /// </summary>
    public void CheckIfAllReady()
    {
        bool allReady = false;
        foreach (PlayerObjectController player in Manager.GamePlayers)
        {
            if (player.Ready)
            {
                allReady = true;
            }
            else
            {
                allReady = false;
                break;
            }
        }

        if (allReady)
        {
            // Only the host (Player 1) can start the game
            if (LocalPlayerController.PlayerId == 1)
            {
                StartGameButton.Interactable = true;
            }
            else
            {
                StartGameButton.Interactable = false;
            }
        }
        else
        {
            StartGameButton.Interactable = false;
        }
    }

    /// <summary>
    /// Description: Fetches and updates the lobby name from Steam data.
    /// Context: Called when the local player gains authority in the lobby.
    /// Justification: Ensures the lobby displays the correct Steam persona name of the host.
    /// </summary>
    public void UpdateLobbyName()
    {
        CurrentLobbyId = Manager.GetComponent<SteamLobby>().CurrentLobbyId;
        LobbyNameText.text = SteamMatchmaking.GetLobbyData(new CSteamID(CurrentLobbyId), "name");
    }

    /// <summary>
    /// Description: Orchestrates the synchronization of the player list UI.
    /// Context: Called anytime a player joins, leaves, or changes ready status.
    /// Justification: Acts as the main update loop for the visual lobby roster.
    /// </summary>
    public void UpdatePlayerList()
    {
        if (!PlayerItemCreated) { CreateHostPlayerItem(); } // Initial creation for host
        if (PlayerListItems.Count < Manager.GamePlayers.Count) { CreateClientPlayerItem(); } // Add new clients
        if (PlayerListItems.Count > Manager.GamePlayers.Count) { RemovePlayerItem(); } // Handle player disconnection
        if (PlayerListItems.Count == Manager.GamePlayers.Count) { UpdatePlayerItem(); } // Update existing items
    }

    /// <summary>
    /// Description: Finds and assigns the local player object in the scene.
    /// Context: Called during local player authorization.
    /// Justification: We need a direct reference to the local controller to read its Ready state and issue commands.
    /// </summary>
    public void FindLocalPlayer()
    {
        LocalPlayerObject = GameObject.Find("LocalGamePlayer");
        LocalPlayerController = LocalPlayerObject.GetComponent<PlayerObjectController>();
    }

    /// <summary>
    /// Description: Creates UI items for all players currently in the manager (Host logic).
    /// Context: Called during initial list population.
    /// Justification: The host needs to generate the UI for themselves and any immediate peers upon lobby creation.
    /// </summary>
    public void CreateHostPlayerItem()
    {
        foreach (PlayerObjectController player in Manager.GamePlayers)
        {
            GameObject newPlayerItem = Instantiate(PlayerListItemPrefab) as GameObject;
            PlayerListItem NewPlayerItemScript = newPlayerItem.GetComponent<PlayerListItem>();

            NewPlayerItemScript.PlayerName = player.PlayerName;
            NewPlayerItemScript.ConnectionId = player.ConnectionId;
            NewPlayerItemScript.PlayerSteamId = player.PlayerSteamId;
            NewPlayerItemScript.Ready = player.Ready;
            NewPlayerItemScript.SetPlayerValues();

            // Initialize per-peer volume slider: pass both IDs.
            // SteamId is the persistent storage key; ConnectionId is the runtime UniVoice key.
            var volumeSlider = newPlayerItem.GetComponent<PlayerVolumeSlider>();
            if (volumeSlider != null)
            {
                bool isLocal = player.isLocalPlayer;
                volumeSlider.SetPeerIdentity(player.ConnectionId, player.PlayerSteamId, isLocal);
            }

            newPlayerItem.transform.SetParent(PlayerListViewContent.transform);
            newPlayerItem.transform.localPosition = new Vector3(0, 0, 0);
            newPlayerItem.transform.localScale = Vector3.one;

            PlayerListItems.Add(NewPlayerItemScript);
        }
        PlayerItemCreated = true;
    }

    /// <summary>
    /// Description: Creates UI items for players not yet represented in the UI (Client logic).
    /// Context: Called when syncing the list and finding a mismatch.
    /// Justification: Dynamically adds late-joiners to the visual roster.
    /// </summary>
    public void CreateClientPlayerItem()
    {
        foreach (PlayerObjectController player in Manager.GamePlayers)
        {
            if (!PlayerListItems.Any(b => b.ConnectionId == player.ConnectionId))
            {
                GameObject newPlayerItem = Instantiate(PlayerListItemPrefab) as GameObject;
                PlayerListItem NewPlayerItemScript = newPlayerItem.GetComponent<PlayerListItem>();

                NewPlayerItemScript.PlayerName = player.PlayerName;
                NewPlayerItemScript.ConnectionId = player.ConnectionId;
                NewPlayerItemScript.PlayerSteamId = player.PlayerSteamId;
                NewPlayerItemScript.Ready = player.Ready;
                NewPlayerItemScript.SetPlayerValues();

                // Initialize per-peer volume slider: pass both IDs.
                // SteamId is the persistent storage key; ConnectionId is the runtime UniVoice key.
                var volumeSlider = newPlayerItem.GetComponent<PlayerVolumeSlider>();
                if (volumeSlider != null)
                {
                    bool isLocal = player.isLocalPlayer;
                    volumeSlider.SetPeerIdentity(player.ConnectionId, player.PlayerSteamId, isLocal);
                }

                newPlayerItem.transform.SetParent(PlayerListViewContent.transform);
                newPlayerItem.transform.localScale = Vector3.one;

                PlayerListItems.Add(NewPlayerItemScript);
            }
        }
    }

    /// <summary>
    /// Description: Updates the data displayed in existing player list UI items.
    /// Context: Called when a player's name or ready status changes.
    /// Justification: Ensures the UI matches the network state without destroying and recreating the GameObjects.
    /// </summary>
    public void UpdatePlayerItem()
    {
        foreach (PlayerObjectController player in Manager.GamePlayers)
        {
            if (player == null) continue;
            foreach (PlayerListItem PlayerListItemScript in PlayerListItems)
            {
                if (PlayerListItemScript == null) continue;
                if (PlayerListItemScript.ConnectionId == player.ConnectionId)
                {
                    PlayerListItemScript.PlayerName = player.PlayerName;
                    PlayerListItemScript.Ready = player.Ready;
                    PlayerListItemScript.SetPlayerValues();
                    if (player == LocalPlayerController)
                    {
                        UpdateButton();
                    }
                }
            }
        }
        CheckIfAllReady();
    }

    /// <summary>
    /// Description: Removes UI items for players who have left the lobby.
    /// Context: Called when the UI list size exceeds the network manager list size.
    /// Justification: Cleans up the roster when someone disconnects.
    /// </summary>
    public void RemovePlayerItem()
    {
        List<PlayerListItem> playerListItemsToRemove = new List<PlayerListItem>();

        foreach (PlayerListItem playerListItem in PlayerListItems)
        {
            if (playerListItem == null) continue;
            if (!Manager.GamePlayers.Any(b => b != null && b.ConnectionId == playerListItem.ConnectionId))
            {
                playerListItemsToRemove.Add(playerListItem);
            }
        }

        if (playerListItemsToRemove.Count > 0)
        {
            foreach (PlayerListItem playerListItem in playerListItemsToRemove)
            {
                if (playerListItem == null) continue;
                GameObject ObjectToRemove = playerListItem.gameObject;
                PlayerListItems.Remove(playerListItem);
                if (ObjectToRemove != null)
                {
                    Destroy(ObjectToRemove);
                }
            }
        }
    }
}
