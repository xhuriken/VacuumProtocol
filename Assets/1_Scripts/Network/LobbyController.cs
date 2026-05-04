using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using NUnit.Framework;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyController : MonoBehaviour
{
    public static LobbyController Instance;

    //Ui Elements
    public TextMeshProUGUI LobbyNameText;

    //Player Data
    public GameObject PlayerListViewContent;
    public GameObject PlayerListItemPrefab;
    public GameObject LocalPlayerObject;

    //Other Data
    public ulong CurrentLobbyId;
    public bool PlayerItemCreated = false;
    private List<PlayerListItem> PlayerListItems = new List<PlayerListItem>();
    public PlayerObjectController LocalPlayerController;

    //Ready
    public Button StartGameButton;
    public TextMeshProUGUI ReadyButtonText;

    //Manger
    private MyNetworkManager _manager;
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

    public void ReadyPlayer()
    {
        LocalPlayerController.ChangeReady();
    }

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

    public void UpdateLobbyName()
    {
        CurrentLobbyId = Manager.GetComponent<SteamLobby>().CurrentLobbyId;
        LobbyNameText.text = SteamMatchmaking.GetLobbyData(new CSteamID(CurrentLobbyId), "name");
    }

    public void UpdatePlayerList()
    {
        if (!PlayerItemCreated) { CreateHostPlayerItem(); }//host
        if (PlayerListItems.Count < Manager.GamePlayers.Count) { CreateClientPlayerItem(); }//client
        if (PlayerListItems.Count > Manager.GamePlayers.Count) { RemovePlayerItem(); }//player left
        if (PlayerListItems.Count == Manager.GamePlayers.Count) { UpdatePlayerItem(); }

    }

    public void FindLocalPlayer()
    {
        LocalPlayerObject = GameObject.Find("LocalGamePlayer");
        LocalPlayerController = LocalPlayerObject.GetComponent<PlayerObjectController>();
    }

    // host only
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

    //client only
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

    public void UpdatePlayerItem()
    {
        foreach (PlayerObjectController player in Manager.GamePlayers)
        {
            foreach (PlayerListItem PlayerListItemScript in PlayerListItems)
            {
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

    public void RemovePlayerItem()
    {
        List<PlayerListItem> playerListItemsToRemove = new List<PlayerListItem>();

        foreach (PlayerListItem playerListItem in PlayerListItems)
        {
            if (!Manager.GamePlayers.Any(b => b.ConnectionId == playerListItem.ConnectionId))
            {
                playerListItemsToRemove.Add(playerListItem);
            }
        }

        if (playerListItemsToRemove.Count > 0)
        {
            foreach (PlayerListItem playerListItem in playerListItemsToRemove)
            {
                GameObject ObjectToRemove = playerListItem.gameObject;
                PlayerListItems.Remove(playerListItem);
                Destroy(ObjectToRemove);
                ObjectToRemove = null;
            }
        }
    }
}
