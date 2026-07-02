using System;
using System.Reflection;
using UnityEngine;
using Adrenak.UniMic;
using Adrenak.UniVoice;
using Adrenak.UniVoice.Inputs;
using Adrenak.UniVoice.Outputs;
using Adrenak.UniVoice.Filters;
using Adrenak.UniVoice.Samples;

/// <summary>
/// Decoupled consumer responsible for bridging SettingsManager changes into UniVoice.
/// Handles microphone hot-swapping, VAD sensitivity gating, and volume adjustments.
/// </summary>
public class VoiceSettingsConsumer : MonoBehaviour, ISettingsConsumer
{
    /// <summary>
    /// Event raised when the microphone device is swapped at runtime.
    /// </summary>
    public static event Action OnMicInputSwapped;

    /// <summary>
    /// When true, the manual sensitivity slider is ignored and UniVoice's default VAD config is used.
    /// </summary>
    public static bool IsAutoVad { get; private set; } = false;

    private FieldInfo _vadConfigField;
    private string _lastAppliedDevice;
    private float _lastAppliedSensitivity = -1f;
    private float _lastAppliedMasterVolume = -1f;
    private float _lastAppliedVoiceVolume = -1f;

    private void Start()
    {
        // Cache reflection info for VAD config access
        _vadConfigField = typeof(SimpleVad).GetField("_config", BindingFlags.NonPublic | BindingFlags.Instance);

        // Register instance callback for static Auto VAD toggle hot-reload
        _onAutoVadChanged += OnAutoVadChangedCallback;

        // Register with the central settings manager
        SettingsManager.Instance.RegisterConsumer(this);
    }

    private void OnDestroy()
    {
        // Unregister from static callback to avoid stale instance references after destroy
        _onAutoVadChanged -= OnAutoVadChangedCallback;

        TeardownLoopbackFilter();

        if (SettingsManager.HasInstance)
        {
            SettingsManager.Instance.UnregisterConsumer(this);
        }
    }

    /// <summary>
    /// Implementation of ISettingsConsumer. Invoked whenever settings are updated.
    /// </summary>
    /// <param name="settings">The updated settings data.</param>
    public void OnSettingsUpdated(SettingsData settings)
    {
        if (settings == null) return;

        // Only swap microphone device if the selection has actually changed
        if (settings.ActiveMicrophoneDevice != _lastAppliedDevice)
        {
            _lastAppliedDevice = settings.ActiveMicrophoneDevice;
            ApplyMicrophoneDevice(_lastAppliedDevice);
        }

        // Only update VAD noise gate settings if sensitivity threshold changes
        if (!Mathf.Approximately(settings.MicSensitivityLimit, _lastAppliedSensitivity))
        {
            _lastAppliedSensitivity = settings.MicSensitivityLimit;
            ApplyGateSensitivity(_lastAppliedSensitivity);
        }

        // Only update audio sources volume if master volume or voice volume levels change
        if (!Mathf.Approximately(settings.MasterVolume, _lastAppliedMasterVolume) || 
            !Mathf.Approximately(settings.VoiceVolume, _lastAppliedVoiceVolume))
        {
            _lastAppliedMasterVolume = settings.MasterVolume;
            _lastAppliedVoiceVolume = settings.VoiceVolume;
            ApplyVoiceVolumes(settings);
        }
    }

    // Internal callback allowing the static SetAutoVad to trigger instance-level config changes
    private static Action _onAutoVadChanged;

    /// <summary>
    /// Toggles the Auto VAD mode. When enabled, restores UniVoice default config immediately.
    /// When disabled, re-applies the saved sensitivity from SettingsManager.
    /// </summary>
    /// <param name="enabled">True to use UniVoice default VAD; false to use manual slider value.</param>
    public static void SetAutoVad(bool enabled)
    {
        IsAutoVad = enabled;
        _onAutoVadChanged?.Invoke();
        Debug.Log($"[VoiceSettingsConsumer] Auto VAD mode set to: {enabled}");
    }

    private void OnAutoVadChangedCallback()
    {
        if (IsAutoVad)
        {
            RestoreDefaultVadConfig();
        }
        else
        {
            // Invalidate cache and immediately re-apply the saved manual threshold
            _lastAppliedSensitivity = -1f;
            if (SettingsManager.HasInstance)
            {
                OnSettingsUpdated(SettingsManager.Instance.CurrentSettings);
            }
        }
    }

    private void RestoreDefaultVadConfig()
    {
        if (UniVoiceMirrorSetupSample.LocalVad == null || _vadConfigField == null) return;

        try
        {
            var config = _vadConfigField.GetValue(UniVoiceMirrorSetupSample.LocalVad) as SimpleVad.Config;
            if (config != null)
            {
                // Restore UniVoice SimpleVad defaults (from SimpleVad.Config class definition)
                config.SnrEnterDb = 8f;
                config.SnrExitDb = 4f;
                config.ReleaseMs = 1000;
                config.NoDropWindowMs = 400;
                config.AttackMs = 20;
                Debug.Log("[VoiceSettingsConsumer] Restored default UniVoice VAD config.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[VoiceSettingsConsumer] Failed to restore default VAD config: {ex.Message}");
        }
    }

    private void ApplyMicrophoneDevice(string deviceName)
    {
        if (string.IsNullOrEmpty(deviceName) || UniVoiceMirrorSetupSample.ClientSession == null) return;

        // Find the device in UniMic available devices
        var devices = Mic.AvailableDevices;
        Mic.Device targetDevice = null;
        foreach (var dev in devices)
        {
            if (dev.Name == deviceName)
            {
                targetDevice = dev;
                break;
            }
        }

        if (targetDevice == null)
        {
            Debug.LogWarning($"[VoiceSettingsConsumer] Requested microphone device not found: {deviceName}");
            return;
        }

        // If it's already recording and is the current input, do nothing
        var currentInput = UniVoiceMirrorSetupSample.ClientSession.Input as UniMicInput;
        if (targetDevice.IsRecording && currentInput != null)
        {
            // Verify if it wraps the same device
            var deviceField = typeof(UniMicInput).GetField("device", BindingFlags.NonPublic | BindingFlags.Instance);
            if (deviceField != null)
            {
                var wrappedDevice = deviceField.GetValue(currentInput) as Mic.Device;
                if (wrappedDevice != null && wrappedDevice.Name == deviceName)
                {
                    return; // Already using this device
                }
            }
        }

        // Stop recording on all currently recording devices to prevent resource locking
        foreach (var dev in devices)
        {
            if (dev.IsRecording)
            {
                dev.StopRecording();
            }
        }

        // Hot-swap routine:
        try
        {
            targetDevice.StartRecording(60); // 60fps frame rate
            var newInput = new UniMicInput(targetDevice);
            UniVoiceMirrorSetupSample.ClientSession.Input = newInput;
            
            // Raise swap event so that UI presenters can re-subscribe to the new mic input's events
            OnMicInputSwapped?.Invoke();
            Debug.Log($"[VoiceSettingsConsumer] Hot-swapped microphone input to: {deviceName}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[VoiceSettingsConsumer] Failed to hot-swap microphone: {ex.Message}");
        }
    }

    private void ApplyGateSensitivity(float sensitivity)
    {
        // In Auto mode, skip manual config and restore defaults instead
        if (IsAutoVad)
        {
            RestoreDefaultVadConfig();
            return;
        }

        if (UniVoiceMirrorSetupSample.LocalVad == null || _vadConfigField == null) return;

        try
        {
            var config = _vadConfigField.GetValue(UniVoiceMirrorSetupSample.LocalVad) as SimpleVad.Config;
            if (config != null)
            {
                // Map sensitivity (0.0 to 1.0) to decibel thresholds
                // 0.0 = very sensitive (2 dB SNR enter threshold)
                // 1.0 = barely sensitive (18 dB SNR enter threshold)
                // Using 18 dB instead of 32 dB makes the slider much more precise (useful zone spread across the full width)
                float targetDb = 2.0f + (sensitivity * 16.0f);
                config.SnrEnterDb = targetDb;
                config.SnrExitDb = Mathf.Max(1.0f, targetDb - 3.0f); // maintain a small hysteresis gap (3 dB)

                // Speed up release time (hangover) from default 1000ms (1s) to 300ms for snappier cutoffs
                config.ReleaseMs = 300;
                config.NoDropWindowMs = 200;

                // Log the resolved threshold so the user can compare against audio peak logs
                Debug.Log($"[VoiceSettingsConsumer] Sensitivity slider = {sensitivity:F3} -> SNR enter threshold = {targetDb:F2} dB  (exit = {config.SnrExitDb:F2} dB)");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[VoiceSettingsConsumer] Failed to apply VAD sensitivity threshold: {ex.Message}");
        }
    }

    private void ApplyVoiceVolumes(SettingsData settings)
    {
        if (UniVoiceMirrorSetupSample.ClientSession == null) return;

        float baseVolume = settings.MasterVolume * settings.VoiceVolume;

        foreach (var kp in UniVoiceMirrorSetupSample.ClientSession.PeerOutputs)
        {
            int peerId = kp.Key;
            IAudioOutput output = kp.Value;

            if (output is StreamedAudioSourceOutput streamedOutput)
            {
                // Retrieve peer volume multiplier (default to 1.0 if not defined)
                float peerMultiplier = 1.0f;
                if (settings.PeerVolumeMultipliers.TryGetValue(peerId, out float mult))
                {
                    peerMultiplier = mult;
                }

                // Combined volume formula
                streamedOutput.Stream.UnityAudioSource.volume = baseVolume * peerMultiplier;
            }
        }
    }

    private static LocalLoopbackFilter _loopbackFilter;
    private static bool _loopbackRequested = false;

    /// <summary>
    /// Configures the local loopback audio preview so the player can listen to their own voice (gated by VAD settings).
    /// </summary>
    /// <param name="enabled">True to start playing, false to stop.</param>
    public static void SetLocalLoopback(bool enabled)
    {
        _loopbackRequested = enabled;
        if (_loopbackFilter != null)
        {
            _loopbackFilter.Enabled = enabled;
            Debug.Log($"[VoiceSettingsConsumer] Local microphone loopback preview toggled to: {enabled}");
        }
    }

    private bool _sessionWasActive = false;

    private void Update()
    {
        bool sessionIsActive = UniVoiceMirrorSetupSample.ClientSession != null;
        if (sessionIsActive != _sessionWasActive)
        {
            _sessionWasActive = sessionIsActive;
            if (sessionIsActive)
            {
                // Invalidate cache to force re-applying settings to the newly started session
                _lastAppliedDevice = null;
                _lastAppliedSensitivity = -1f;
                _lastAppliedMasterVolume = -1f;
                _lastAppliedVoiceVolume = -1f;

                OnSettingsUpdated(SettingsManager.Instance.CurrentSettings);

                // Initialize loopback filter after settings have been updated
                SetupLoopbackFilter();

                Debug.Log("[VoiceSettingsConsumer] VoIP ClientSession detected. Applied saved settings successfully.");
            }
            else
            {
                TeardownLoopbackFilter();
            }
        }
    }

    private void SetupLoopbackFilter()
    {
        if (UniVoiceMirrorSetupSample.ClientSession == null) return;

        _loopbackFilter = new LocalLoopbackFilter();
        _loopbackFilter.Enabled = _loopbackRequested;

        // Insert local loopback preview filter right after SimpleVadFilter to ensure we preview gated sound
        int insertIndex = 0;
        var filters = UniVoiceMirrorSetupSample.ClientSession.InputFilters;
        for (int i = 0; i < filters.Count; i++)
        {
            if (filters[i] is SimpleVadFilter)
            {
                insertIndex = i + 1;
                break;
            }
        }
        filters.Insert(insertIndex, _loopbackFilter);
        Debug.Log("[VoiceSettingsConsumer] Hooked LocalLoopbackFilter into UniVoice InputFilters chain.");
    }

    private void TeardownLoopbackFilter()
    {
        if (_loopbackFilter != null)
        {
            if (UniVoiceMirrorSetupSample.ClientSession != null && UniVoiceMirrorSetupSample.ClientSession.InputFilters != null)
            {
                UniVoiceMirrorSetupSample.ClientSession.InputFilters.Remove(_loopbackFilter);
            }
            _loopbackFilter.Dispose();
            _loopbackFilter = null;
            Debug.Log("[VoiceSettingsConsumer] Disposed and removed LocalLoopbackFilter.");
        }
    }
}

/// <summary>
/// Custom UniVoice filter that intercepts processed microphone PCM float frames and plays them back in real time.
/// </summary>
public class LocalLoopbackFilter : IAudioFilter
{
    private StreamedAudioSourceOutput _previewOutput;
    private bool _enabled = false;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled != value)
            {
                _enabled = value;
                if (_enabled)
                {
                    if (_previewOutput == null)
                    {
                        _previewOutput = StreamedAudioSourceOutput.New();
                        var audioSource = _previewOutput.Stream.UnityAudioSource;
                        if (audioSource != null)
                        {
                            audioSource.spatialBlend = 0f; // Force 2D sound so the user hears it clearly in both ears
                            audioSource.volume = 1f;
                        }
                    }
                }
                else
                {
                    if (_previewOutput != null)
                    {
                        _previewOutput.Dispose();
                        _previewOutput = null;
                    }
                }
            }
        }
    }

    public AudioFrame Run(AudioFrame frame)
    {
        if (_enabled && _previewOutput != null && frame.samples != null && frame.samples.Length > 0)
        {
            _previewOutput.Feed(frame);
        }
        return frame;
    }

    public void Dispose()
    {
        if (_previewOutput != null)
        {
            _previewOutput.Dispose();
            _previewOutput = null;
        }
    }
}
