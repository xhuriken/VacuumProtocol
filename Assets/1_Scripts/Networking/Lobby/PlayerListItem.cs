using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
/// <summary>
/// Description: Represents an individual player entry in the lobby UI list.
/// Context: Instantiated dynamically in the Lobby UI's ScrollView.
/// Justification: Displays the player's Steam avatar, name, and ready status.
/// </summary>
public class PlayerListItem : MonoBehaviour
{
    [Header("Player Data")]
    [Tooltip("Role: The Steam persona name.\nUse Case: UI display.\nJustification: Matches the local network PlayerName.")]
    public string PlayerName;
    
    [Tooltip("Role: The internal Mirror connection ID.\nUse Case: Networking sync.\nJustification: Identifies the player uniquely on the server.")]
    public int ConnectionId;
    
    [Tooltip("Role: The public Steam ID of the player.\nUse Case: Fetching avatars.\nJustification: Required to ask Steam for the user's profile picture.")]
    public ulong PlayerSteamId;
    
    private bool AvatarReceived;

    [Header("UI References")]
    [Tooltip("Role: UI Text component for the name.\nUse Case: Visual update.\nJustification: Required to show who this is.")]
    public TextMeshProUGUI PlayerNameText;
    
    [Tooltip("Role: UI RawImage component for the avatar.\nUse Case: Visual update.\nJustification: Required to show the Steam profile picture.")]
    public RawImage PlayerIcon;
    
    [Tooltip("Role: UI Text component for ready state.\nUse Case: Visual update.\nJustification: Required to show if the player is ready to start.")]
    public TextMeshProUGUI PlayerReadyText;
    
    [Tooltip("Role: Flag indicating if this player is ready.\nUse Case: State tracking.\nJustification: Controls the color and text of PlayerReadyText.")]
    public bool Ready;

    protected Callback<AvatarImageLoaded_t> ImageLoaded;

    private void Start()
    {
        // Subscribe to Steam avatar loading events
        ImageLoaded = Callback<AvatarImageLoaded_t>.Create(OnImageLoaded);
    }

    /// <summary>
    /// Description: Updates the ready status text and color in the UI.
    /// Context: Called during UI synchronization.
    /// Justification: Visually differentiates ready vs not-ready players (green vs red).
    /// </summary>
    public void ChangeReadyStatus()
    {
        PlayerReadyText.text = Ready ? "Charged!" : "Not Charged!";
        PlayerReadyText.color = Ready ? Color.green : Color.red;
    }

    /// <summary>
    /// Description: Synchronizes the UI elements with the player's current data.
    /// Context: Called by LobbyController when the roster updates.
    /// Justification: Centralizes the UI refresh logic for names, status, and avatars.
    /// </summary>
    public void SetPlayerValues()
    {
        PlayerNameText.text = PlayerName;
        ChangeReadyStatus();
        if (!AvatarReceived) GetPlayerIcon();
    }

    /// <summary>
    /// Description: Attempts to retrieve the player's Steam avatar.
    /// Context: Called internally by SetPlayerValues.
    /// Justification: Triggers a Steamworks request to fetch the image data asynchronously.
    /// </summary>
    void GetPlayerIcon()
    {
        int ImageId = SteamFriends.GetLargeFriendAvatar(new CSteamID(PlayerSteamId));
        if (ImageId == -1) { return; }
        PlayerIcon.texture = GetSteamImageAsTexture(ImageId);
    }

    /// <summary>
    /// Description: Callback triggered when a Steam avatar image is loaded.
    /// Context: Invoked by Steam callback system.
    /// Justification: Necessary because Steam avatar loading is asynchronous.
    /// </summary>
    /// <param name="callback">Data containing the loaded image info.</param>
    private void OnImageLoaded(AvatarImageLoaded_t callback)
    {
        if (callback.m_steamID.m_SteamID == PlayerSteamId)
        {
            PlayerIcon.texture = GetSteamImageAsTexture(callback.m_iImage);
        }
    }

    /// <summary>
    /// Description: Converts raw Steam image data into a Unity Texture2D.
    /// Context: Called once the avatar image is successfully loaded in memory.
    /// Justification: Steam provides raw byte arrays; Unity needs a Texture2D for the RawImage component.
    /// </summary>
    /// <param name="iImage">The Steam image handle.</param>
    /// <returns>A Texture2D containing the image data.</returns>
    private Texture2D GetSteamImageAsTexture(int iImage)
    {
        Texture2D texture = null;

        bool isValid = SteamUtils.GetImageSize(iImage, out uint width, out uint height);
        if (isValid)
        {
            byte[] image = new byte[width * height * 4];
            isValid = SteamUtils.GetImageRGBA(iImage, image, (int)(width * height * 4));

            if (isValid)
            {
                texture = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false, true);
                texture.LoadRawTextureData(image);
                texture.Apply();
            }
        }
        AvatarReceived = true;
        return texture;
    }
}
