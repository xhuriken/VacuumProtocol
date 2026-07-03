using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Description: Binds a Slider to the per-peer volume multiplier stored in SettingsData.
/// Context: Placed on each PlayerListItem prefab in the lobby.
/// Justification: Allows the player to individually adjust or mute other players' voice chat.
/// </summary>
public class PlayerVolumeSlider : MonoBehaviour
{
    /// <summary>
    /// Description: Mirror ConnectionId of the remote player.
    /// Context: Passed during initialization.
    /// Justification: Used at runtime to locate the UniVoice AudioSource. Session-local.
    /// </summary>
    [Header("Player Data (set at runtime)")]
    [Tooltip("Role: The network connection ID.\nUse Case: Locating the audio stream.\nJustification: Required to target the specific player's UniVoice output.")]
    public int ConnectionId = -1;

    /// <summary>
    /// Description: Steam64 ID of the remote player.
    /// Context: Passed during initialization.
    /// Justification: Used as the persistent storage key in SettingsData so preferences survive restarts.
    /// </summary>
    [Tooltip("Role: The Steam64 ID.\nUse Case: Saving preferences.\nJustification: Persistent across sessions.")]
    public ulong SteamId = 0;

    [Header("UI Reference")]
    [Tooltip("Role: The Slider UI element.\nUse Case: Adjusting volume.\nJustification: Must be assigned in the Prefab Inspector.")]
    [SerializeField] private Slider _volumeSlider;

    [Header("Debug")]
    [SerializeField] private bool _showDebugLogs = false;

    private bool _isBound = false;

    private void OnEnable()
    {
        if (_volumeSlider != null && _isBound)
        {
            // Restore slider value from settings when the card is re-enabled
            RefreshFromSettings();
        }
    }

    private void OnDisable()
    {
        UnbindSlider();
    }

    private void OnDestroy()
    {
        UnbindSlider();
    }

    /// <summary>
    /// Description: Initializes this volume slider for a specific player.
    /// Context: Call this immediately after instantiating the PlayerListItem prefab.
    /// Justification: Binds the correct peer identifiers before the slider becomes active.
    /// </summary>
    /// <param name="connectionId">Mirror ConnectionId — runtime key for UniVoice audio output.</param>
    /// <param name="steamId">Steam64 ID — persistent storage key in SettingsData.</param>
    /// <param name="isLocalPlayer">If true, the slider is hidden and disabled.</param>
    public void SetPeerIdentity(int connectionId, ulong steamId, bool isLocalPlayer)
    {
        ConnectionId = connectionId;
        SteamId = steamId;

        if (_volumeSlider == null)
        {
            Debug.LogWarning("[PlayerVolumeSlider] Slider reference is null. Assign it in the Prefab Inspector.");
            return;
        }

        if (isLocalPlayer)
        {
            // Adjusting your own outgoing voice for yourself is meaningless — hide the control
            _volumeSlider.gameObject.SetActive(false);
            if (_showDebugLogs) Debug.Log($"[PlayerVolumeSlider] SteamId {steamId} is the local player — slider hidden.");
            return;
        }

        // Restore any previously saved multiplier for this Steam peer
        RefreshFromSettings();

        // Listen for slider changes
        _volumeSlider.onValueChanged.AddListener(OnSliderChanged);
        _isBound = true;

        if (_showDebugLogs) Debug.Log($"[PlayerVolumeSlider] Bound to SteamId {steamId} (ConnectionId {connectionId}), initial volume = {_volumeSlider.value:F2}x");
    }

    /// <summary>
    /// Description: Reads the saved multiplier for this peer from SettingsData (keyed by SteamId) and syncs the slider.
    /// Context: Internal initialization/enable hook.
    /// Justification: Ensures the UI correctly reflects previously saved volumes.
    /// </summary>
    private void RefreshFromSettings()
    {
        if (_volumeSlider == null || !SettingsManager.HasInstance || SteamId == 0) return;

        var multipliers = SettingsManager.Instance.CurrentSettings.PeerVolumeMultipliers;
        float savedValue = multipliers.TryGetValue(SteamId, out float mult) ? mult : 1.0f;

        // Temporarily suppress the listener to avoid a write-back loop on init
        _volumeSlider.onValueChanged.RemoveListener(OnSliderChanged);
        _volumeSlider.value = savedValue;
        if (_isBound) _volumeSlider.onValueChanged.AddListener(OnSliderChanged);

        if (_showDebugLogs) Debug.Log($"[PlayerVolumeSlider] Restored multiplier for SteamId {SteamId}: {savedValue:F2}x");
    }

    /// <summary>
    /// Description: Called whenever the slider is moved.
    /// Context: Bound to the Slider UI event.
    /// Justification: Persists the new multiplier and applies it immediately to the UniVoice stream.
    /// </summary>
    /// <param name="value">New slider value in [0, 2] range.</param>
    private void OnSliderChanged(float value)
    {
        if (SteamId == 0)
        {
            Debug.LogWarning("[PlayerVolumeSlider] SteamId not set yet — slider change ignored.");
            return;
        }

        if (!SettingsManager.HasInstance) return;

        // Save to SettingsData using the persistent SteamId key
        SettingsManager.Instance.UpdateSettings(data =>
        {
            data.PeerVolumeMultipliers[SteamId] = value;
        });

        // Apply immediately at runtime using the session-local ConnectionId to find the UniVoice stream
        if (ConnectionId >= 0)
        {
            VoiceSettingsConsumer.ApplyPeerVolume(ConnectionId, value);
        }

        if (_showDebugLogs)
            Debug.Log($"[PlayerVolumeSlider] SteamId {SteamId} (conn {ConnectionId}) volume -> {value:F2}x ({value * 100f:F0}%)");
    }

    private void UnbindSlider()
    {
        if (_volumeSlider != null)
        {
            _volumeSlider.onValueChanged.RemoveListener(OnSliderChanged);
        }
        _isBound = false;
    }
}
