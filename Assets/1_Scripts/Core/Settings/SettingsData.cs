using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Description: Holds serializable data representing all user preferences.
/// Context: Used by the SettingsManager to persist data to PlayerPrefs.
/// Justification: Acts as the Single Source of Truth (SSOT). Implements ISerializationCallbackReceiver to handle dictionary serialization via Unity's JsonUtility, which natively fails to serialize dictionaries.
/// </summary>
[Serializable]
public class SettingsData : ISerializationCallbackReceiver
{
    // --- Audio Settings ---
    [SerializeField] 
    [Tooltip("Role: Stores the name of the microphone device.\nUse Case: Audio input selection.\nJustification: Used to re-bind the microphone in UniVoice upon startup.")]
    private string _activeMicrophoneDevice = string.Empty;

    [SerializeField] 
    [Tooltip("Role: The microphone activation threshold.\nUse Case: VAD (Voice Activity Detection).\nJustification: Prevents background noise from broadcasting to other players.")]
    private float _micSensitivityLimit = 0.1f;

    [SerializeField] 
    [Tooltip("Role: Toggles auto VAD mode.\nUse Case: Settings configuration.\nJustification: When enabled, ignores manual sensitivity slider.")]
    private bool _isAutoVad = false;

    [SerializeField] 
    [Tooltip("Role: Overall game volume.\nUse Case: Audio mixer parameter.\nJustification: User control over the entire game sound output.")]
    private float _masterVolume = 1.0f;

    [SerializeField] 
    [Tooltip("Role: Voice chat volume modifier.\nUse Case: Voice system gain.\nJustification: Allows users to balance in-game voice chat relative to game sounds.")]
    private float _voiceVolume = 1.0f;

    // --- Controls Settings ---
    [SerializeField] 
    [Tooltip("Role: Serialized JSON of custom input bindings.\nUse Case: Control mapping.\nJustification: Allows overriding the default Unity Input System bindings at runtime.")]
    private string _controlBindingsOverrideJson = string.Empty;

    // --- Graphics Settings ---
    [SerializeField] 
    [Tooltip("Role: Quality settings index.\nUse Case: Graphics fidelity.\nJustification: Maps directly to Unity's QualitySettings.SetQualityLevel index.")]
    private int _qualityLevelIndex = 2;

    [SerializeField] 
    [Tooltip("Role: Fullscreen toggle state.\nUse Case: Screen mode.\nJustification: Maps directly to Screen.fullScreen.")]
    private bool _isFullscreen = true;

    // --- Peer Volume Dictionary (Non-serialized directly) ---
    // Key: Steam64 ID (ulong) — persistent across sessions, unlike Mirror ConnectionId which is session-local.
    private Dictionary<ulong, float> _peerVolumeMultipliers = new Dictionary<ulong, float>();

    // Lists used by ISerializationCallbackReceiver for Dictionary serialization
    // ulong is stored as string to avoid JSON precision loss (ulong exceeds float/int JSON range)
    [SerializeField] 
    [Tooltip("Role: Serialized keys for peer volumes.\nUse Case: Player-specific muting.\nJustification: JsonUtility doesn't serialize dictionaries, so we use parallel lists.")]
    private List<string> _peerVolumeKeys = new List<string>();

    [SerializeField] 
    [Tooltip("Role: Serialized values for peer volumes.\nUse Case: Player-specific muting.\nJustification: Corresponds to _peerVolumeKeys to reconstruct the dictionary.")]
    private List<float> _peerVolumeValues = new List<float>();

    /// <summary>
    /// Description: Gets or sets the active microphone device name.
    /// Context: UI dropdowns for mic selection.
    /// Justification: Direct mapping to Unity's Microphone.devices string array.
    /// </summary>
    public string ActiveMicrophoneDevice
    {
        get => _activeMicrophoneDevice;
        set => _activeMicrophoneDevice = value;
    }

    /// <summary>
    /// Description: Gets or sets the microphone sensitivity threshold (0.0 to 1.0).
    /// Context: Settings slider.
    /// Justification: Clamped to 0-1 to prevent invalid values breaking the VAD math.
    /// </summary>
    public float MicSensitivityLimit
    {
        get => _micSensitivityLimit;
        set => _micSensitivityLimit = Mathf.Clamp01(value);
    }

    /// <summary>
    /// Description: Gets or sets whether Auto VAD mode is enabled.
    /// Context: Voice chat settings.
    /// Justification: Saves whether the VAD uses automatic thresholds or the manual slider.
    /// </summary>
    public bool IsAutoVad
    {
        get => _isAutoVad;
        set => _isAutoVad = value;
    }

    /// <summary>
    /// Description: Gets or sets the master audio volume (0.0 to 1.0).
    /// Context: General audio mixing.
    /// Justification: Clamped to 0-1.
    /// </summary>
    public float MasterVolume
    {
        get => _masterVolume;
        set => _masterVolume = Mathf.Clamp01(value);
    }

    /// <summary>
    /// Description: Gets or sets the global voice chat volume (0.0 to 1.0).
    /// Context: Voice system volume application.
    /// Justification: Clamped to 0-1.
    /// </summary>
    public float VoiceVolume
    {
        get => _voiceVolume;
        set => _voiceVolume = Mathf.Clamp01(value);
    }

    /// <summary>
    /// Description: Gets or sets the input system bindings override JSON string.
    /// Context: New Input System rebinding UI.
    /// Justification: The easiest way to save Rebind overrides is serializing the InputActionAsset overrides to JSON.
    /// </summary>
    public string ControlBindingsOverrideJson
    {
        get => _controlBindingsOverrideJson;
        set => _controlBindingsOverrideJson = value;
    }

    /// <summary>
    /// Description: Gets or sets the graphics quality level index.
    /// Context: Video settings menu.
    /// Justification: Standard Unity approach for scalable graphics settings.
    /// </summary>
    public int QualityLevelIndex
    {
        get => _qualityLevelIndex;
        set => _qualityLevelIndex = value;
    }

    /// <summary>
    /// Description: Gets or sets whether the game runs in fullscreen.
    /// Context: Video settings menu.
    /// Justification: Standard toggle for Windowed/Fullscreen mode.
    /// </summary>
    public bool IsFullscreen
    {
        get => _isFullscreen;
        set => _isFullscreen = value;
    }

    /// <summary>
    /// Description: Gets the dictionary of peer volume multipliers.
    /// Context: Used to mute or adjust the volume of specific players.
    /// Justification: Keyed by SteamId (ulong) for persistence across sessions, since Mirror ConnectionId is ephemeral and changes every lobby.
    /// </summary>
    public Dictionary<ulong, float> PeerVolumeMultipliers => _peerVolumeMultipliers;

    /// <summary>
    /// Description: Saves dictionary state to lists before serialization.
    /// Context: Called automatically by Unity before JsonUtility.ToJson.
    /// Justification: ulong keys are serialized as strings to avoid JSON 64-bit integer precision loss, which is a common JS/JSON limitation.
    /// </summary>
    public void OnBeforeSerialize()
    {
        _peerVolumeKeys.Clear();
        _peerVolumeValues.Clear();

        foreach (var kvp in _peerVolumeMultipliers)
        {
            // Store SteamId as string to survive JsonUtility serialization without precision loss
            _peerVolumeKeys.Add(kvp.Key.ToString());
            _peerVolumeValues.Add(kvp.Value);
        }
    }

    /// <summary>
    /// Description: Restores dictionary state from lists after deserialization.
    /// Context: Called automatically by Unity after JsonUtility.FromJson.
    /// Justification: Reconstructs the workable C# Dictionary from the parallel lists stored in JSON.
    /// </summary>
    public void OnAfterDeserialize()
    {
        _peerVolumeMultipliers.Clear();

        int count = Mathf.Min(_peerVolumeKeys.Count, _peerVolumeValues.Count);
        for (int i = 0; i < count; i++)
        {
            // Parse the string key back to ulong
            if (ulong.TryParse(_peerVolumeKeys[i], out ulong steamId))
            {
                _peerVolumeMultipliers[steamId] = _peerVolumeValues[i];
            }
        }
    }
}
