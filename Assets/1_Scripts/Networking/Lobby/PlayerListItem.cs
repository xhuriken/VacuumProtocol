using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
/// <summary>
/// Represents an individual player entry in the lobby UI list.
/// </summary>
public class PlayerListItem : MonoBehaviour
{
    [Header("Player Data")]
    public string PlayerName;
    public int ConnectionId;
    public ulong PlayerSteamId;
    private bool AvatarReceived;

    [Header("UI References")]
    public TextMeshProUGUI PlayerNameText;
    public RawImage PlayerIcon;
    public TextMeshProUGUI PlayerReadyText;
    public bool Ready;

    protected Callback<AvatarImageLoaded_t> ImageLoaded;

    private void Start()
    {
        // Subscribe to Steam avatar loading events
        ImageLoaded = Callback<AvatarImageLoaded_t>.Create(OnImageLoaded);
    }

    /// <summary>
    /// Updates the ready status text and color in the UI.
    /// </summary>
    public void ChangeReadyStatus()
    {
        PlayerReadyText.text = Ready ? "Charged!" : "Not Charged!";
        PlayerReadyText.color = Ready ? Color.green : Color.red;
    }

    /// <summary>
    /// Synchronizes the UI elements with the player's current data.
    /// </summary>
    public void SetPlayerValues()
    {
        PlayerNameText.text = PlayerName;
        ChangeReadyStatus();
        if (!AvatarReceived) GetPlayerIcon();
    }

    /// <summary>
    /// Attempts to retrieve the player's Steam avatar.
    /// </summary>
    void GetPlayerIcon()
    {
        int ImageId = SteamFriends.GetLargeFriendAvatar(new CSteamID(PlayerSteamId));
        if (ImageId == -1) { return; }
        PlayerIcon.texture = GetSteamImageAsTexture(ImageId);
    }

    /// <summary>
    /// Callback triggered when a Steam avatar image is loaded.
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
    /// Converts raw Steam image data into a Unity Texture2D.
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
