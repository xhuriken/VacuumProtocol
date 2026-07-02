using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Placed on each PlayerListItem prefab in the lobby.
/// Binds a Slider to the per-peer volume multiplier stored in SettingsData.
///
/// Storage key: Steam64 ID (ulong) — persistent across sessions.
/// Runtime key:  Mirror ConnectionId (int)  — used only to locate the UniVoice AudioSource at runtime.
///
/// The slider operates on a [0, 2] range: 0 = muted, 1.0 = 100% (default), 2.0 = 200%.
/// The slider is hidden/disabled for the local player's own card.
/// </summary>
public class PlayerVolumeSlider : MonoBehaviour
{
    /// <summary>
    /// Mirror ConnectionId of the remote player: used at runtime to locate the UniVoice AudioSource.
    /// Session-local — NOT used as the storage key.
    /// </summary>
    [Header("Player Data (set at runtime)")]
    [Tooltip("Mirror ConnectionId — session-local, used only to find the UniVoice audio output at runtime.")]
    public int ConnectionId = -1;

    /// <summary>
    /// Steam64 ID of the remote player: used as the persistent storage key in SettingsData.
    /// </summary>
    [Tooltip("Steam64 ID — persistent across sessions, used as the storage key for saved volume preferences.")]
    public ulong SteamId = 0;

    [Header("UI Reference")]
    [Tooltip("The Slider UI element (range: 0 to 2). Must be assigned in the Prefab Inspector.")]
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
    /// Initializes this volume slider for a specific player.
    /// Call this immediately after instantiating the PlayerListItem prefab.
    /// </summary>
    /// <param name="connectionId">Mirror ConnectionId — runtime key for UniVoice audio output.</param>
    /// <param name="steamId">Steam64 ID — persistent storage key in SettingsData.</param>
    /// <param name="isLocalPlayer">If true, the slider is hidden and disabled (no point adjusting yourself).</param>
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
    /// Reads the saved multiplier for this peer from SettingsData (keyed by SteamId) and syncs the slider.
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
    /// Called whenever the slider is moved.
    /// Persists the new multiplier (by SteamId) and applies it immediately to the UniVoice stream (by ConnectionId).
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
