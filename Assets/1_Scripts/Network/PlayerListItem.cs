using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class PlayerListItem : MonoBehaviour
{
    public string PlayerName;
    public int ConnectionId;
    public ulong PlayerSteamId;
    private bool AvatarReceived;

    public TextMeshProUGUI PlayerNameText;
    public RawImage PlayerIcon;
    public TextMeshProUGUI PlayerReadyText;
    public bool Ready;

    protected Callback<AvatarImageLoaded_t> ImageLoaded;

    private void Start()
    {
        ImageLoaded = Callback<AvatarImageLoaded_t>.Create(OnImageLoaded);
    }

    public void ChangeReadyStatus()
    {
        //Visual
        PlayerReadyText.text = Ready ? "Charged !" : "Not Charged !";
        PlayerReadyText.color = Ready ? Color.green : Color.red;
    }

    public void SetPlayerValues()
    {
        PlayerNameText.text = PlayerName;
        ChangeReadyStatus();
        if (!AvatarReceived) GetPlayerIcon();

    }

    void GetPlayerIcon()
    {
        int ImageId = SteamFriends.GetLargeFriendAvatar(new CSteamID(PlayerSteamId));
        if (ImageId == -1) { return; }
        PlayerIcon.texture = GetSteamImageAsTexture(ImageId);

    }

    private void OnImageLoaded(AvatarImageLoaded_t callback)
    {
        if (callback.m_steamID.m_SteamID == PlayerSteamId)
        {
            PlayerIcon.texture = GetSteamImageAsTexture(callback.m_iImage);
        }
    }

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
