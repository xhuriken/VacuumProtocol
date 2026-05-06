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
/// Controls the lobby UI, manages the player list, and handles the game start logic.
/// </summary>
public class LobbyController : MonoBehaviour
{
    /// <summary>
    /// Singleton instance of the LobbyController.
    /// </summary>
    public static LobbyController Instance;

    [Header("UI Elements")]
    public TextMeshProUGUI LobbyNameText;

    [Header("Player Data")]
    public GameObject PlayerListViewContent;
    public GameObject PlayerListItemPrefab;
    public GameObject LocalPlayerObject;

    [Header("Lobby State")]
    public ulong CurrentLobbyId;
    public bool PlayerItemCreated = false;
    private List<PlayerListItem> PlayerListItems = new List<PlayerListItem>();
    public PlayerObjectController LocalPlayerController;

    [Header("Ready System")]
    public Button StartGameButton;
    public TextMeshProUGUI ReadyButtonText;

    private MyNetworkManager _manager;
    /// <summary>
    /// Accessor for the custom Network Manager.
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
    /// Toggles the ready state of the local player.
    /// </summary>
    public void ReadyPlayer()
    {
        LocalPlayerController.ChangeReady();
    }

    /// <summary>
    /// Starts the game for all connected players.
    /// </summary>
    public void StartGame()
    {
        LocalPlayerController.CanStartGame("SteamTest");
    }

    /// <summary>
    /// Updates the visual state of the ready button based on the local player's status.
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
    /// Checks if all players are ready. Enables the start button if the host.
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
                StartGameButton.interactable = true;
            }
            else
            {
                StartGameButton.interactable = false;
            }
        }
        else
        {
            StartGameButton.interactable = false;
        }
    }

    /// <summary>
    /// Fetches and updates the lobby name from Steam data.
    /// </summary>
    public void UpdateLobbyName()
    {
        CurrentLobbyId = Manager.GetComponent<SteamLobby>().CurrentLobbyId;
        LobbyNameText.text = SteamMatchmaking.GetLobbyData(new CSteamID(CurrentLobbyId), "name");
    }

    /// <summary>
    /// Orchestrates the synchronization of the player list UI.
    /// </summary>
    public void UpdatePlayerList()
    {
        if (!PlayerItemCreated) { CreateHostPlayerItem(); } // Initial creation for host
        if (PlayerListItems.Count < Manager.GamePlayers.Count) { CreateClientPlayerItem(); } // Add new clients
        if (PlayerListItems.Count > Manager.GamePlayers.Count) { RemovePlayerItem(); } // Handle player disconnection
        if (PlayerListItems.Count == Manager.GamePlayers.Count) { UpdatePlayerItem(); } // Update existing items
    }

    /// <summary>
    /// Finds and assigns the local player object in the scene.
    /// </summary>
    public void FindLocalPlayer()
    {
        LocalPlayerObject = GameObject.Find("LocalGamePlayer");
        LocalPlayerController = LocalPlayerObject.GetComponent<PlayerObjectController>();
    }

    /// <summary>
    /// Creates UI items for all players currently in the manager (Host logic).
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

            newPlayerItem.transform.SetParent(PlayerListViewContent.transform);
            newPlayerItem.transform.localScale = Vector3.one;

            PlayerListItems.Add(NewPlayerItemScript);
        }
        PlayerItemCreated = true;
    }

    /// <summary>
    /// Creates UI items for players not yet represented in the UI (Client logic).
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

                newPlayerItem.transform.SetParent(PlayerListViewContent.transform);
                newPlayerItem.transform.localScale = Vector3.one;

                PlayerListItems.Add(NewPlayerItemScript);
            }
        }
    }

    /// <summary>
    /// Updates the data displayed in existing player list UI items.
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
    /// Removes UI items for players who have left the lobby.
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
