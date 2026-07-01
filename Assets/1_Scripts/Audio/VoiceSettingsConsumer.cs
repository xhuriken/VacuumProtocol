using System;
using System.Reflection;
using UnityEngine;
using Adrenak.UniMic;
using Adrenak.UniVoice;
using Adrenak.UniVoice.Inputs;
using Adrenak.UniVoice.Outputs;
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

    private FieldInfo _vadConfigField;
    private string _lastAppliedDevice;
    private float _lastAppliedSensitivity = -1f;
    private float _lastAppliedMasterVolume = -1f;
    private float _lastAppliedVoiceVolume = -1f;

    private void Start()
    {
        // Cache reflection info for VAD config access
        _vadConfigField = typeof(SimpleVad).GetField("_config", BindingFlags.NonPublic | BindingFlags.Instance);

        // Register with the central settings manager
        SettingsManager.Instance.RegisterConsumer(this);
    }

    private void OnDestroy()
    {
        if (SettingsManager.Instance != null)
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
        if (UniVoiceMirrorSetupSample.LocalVad == null || _vadConfigField == null) return;

        try
        {
            var config = _vadConfigField.GetValue(UniVoiceMirrorSetupSample.LocalVad) as SimpleVad.Config;
            if (config != null)
            {
                // Map sensitivity (0.0 to 1.0) to decibel thresholds
                // 0.0 = very sensitive (2 dB enter threshold)
                // 1.0 = barely sensitive (32 dB enter threshold)
                float targetDb = 2.0f + (sensitivity * 30.0f);
                config.SnrEnterDb = targetDb;
                config.SnrExitDb = Mathf.Max(1.0f, targetDb - 4.0f); // maintain a hysteresis gap
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
                Debug.Log("[VoiceSettingsConsumer] VoIP ClientSession detected. Applied saved settings successfully.");
            }
        }
    }
}
