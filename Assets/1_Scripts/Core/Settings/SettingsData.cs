using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds serializable data representing all user preferences.
/// Implements ISerializationCallbackReceiver to handle dictionary serialization via Unity's JsonUtility.
/// This acts as the Single Source of Truth (SSOT).
/// </summary>
[Serializable]
public class SettingsData : ISerializationCallbackReceiver
{
    // --- Audio Settings ---
    [SerializeField] private string _activeMicrophoneDevice = string.Empty;
    [SerializeField] private float _micSensitivityLimit = 0.1f;
    [SerializeField] private float _masterVolume = 1.0f;
    [SerializeField] private float _voiceVolume = 1.0f;

    // --- Controls Settings ---
    [SerializeField] private string _controlBindingsOverrideJson = string.Empty;

    // --- Graphics Settings ---
    [SerializeField] private int _qualityLevelIndex = 2;
    [SerializeField] private bool _isFullscreen = true;

    // --- Peer Volume Dictionary (Non-serialized directly) ---
    // Key: Steam64 ID (ulong) — persistent across sessions, unlike Mirror ConnectionId which is session-local.
    private Dictionary<ulong, float> _peerVolumeMultipliers = new Dictionary<ulong, float>();

    // Lists used by ISerializationCallbackReceiver for Dictionary serialization
    // ulong is stored as string to avoid JSON precision loss (ulong exceeds float/int JSON range)
    [SerializeField] private List<string> _peerVolumeKeys = new List<string>();
    [SerializeField] private List<float> _peerVolumeValues = new List<float>();

    /// <summary>
    /// Gets or sets the active microphone device name.
    /// </summary>
    public string ActiveMicrophoneDevice
    {
        get => _activeMicrophoneDevice;
        set => _activeMicrophoneDevice = value;
    }

    /// <summary>
    /// Gets or sets the microphone sensitivity threshold (0.0 to 1.0).
    /// </summary>
    public float MicSensitivityLimit
    {
        get => _micSensitivityLimit;
        set => _micSensitivityLimit = Mathf.Clamp01(value);
    }

    /// <summary>
    /// Gets or sets the master audio volume (0.0 to 1.0).
    /// </summary>
    public float MasterVolume
    {
        get => _masterVolume;
        set => _masterVolume = Mathf.Clamp01(value);
    }

    /// <summary>
    /// Gets or sets the global voice chat volume (0.0 to 1.0).
    /// </summary>
    public float VoiceVolume
    {
        get => _voiceVolume;
        set => _voiceVolume = Mathf.Clamp01(value);
    }

    /// <summary>
    /// Gets or sets the input system bindings override JSON string.
    /// </summary>
    public string ControlBindingsOverrideJson
    {
        get => _controlBindingsOverrideJson;
        set => _controlBindingsOverrideJson = value;
    }

    /// <summary>
    /// Gets or sets the graphics quality level index.
    /// </summary>
    public int QualityLevelIndex
    {
        get => _qualityLevelIndex;
        set => _qualityLevelIndex = value;
    }

    /// <summary>
    /// Gets or sets whether the game runs in fullscreen.
    /// </summary>
    public bool IsFullscreen
    {
        get => _isFullscreen;
        set => _isFullscreen = value;
    }

    /// <summary>
    /// Gets the dictionary of peer volume multipliers (Key: Steam64 ID, Value: Volume multiplier [0..2]).
    /// Keyed by SteamId for persistence across sessions. ConnectionId is session-ephemeral and must not be used here.
    /// </summary>
    public Dictionary<ulong, float> PeerVolumeMultipliers => _peerVolumeMultipliers;

    /// <summary>
    /// Saves dictionary state to lists before serialization.
    /// ulong keys are serialized as strings to avoid JSON 64-bit integer precision loss.
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
    /// Restores dictionary state from lists after deserialization.
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
