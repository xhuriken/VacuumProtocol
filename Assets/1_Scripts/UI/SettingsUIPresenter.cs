using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Adrenak.UniMic;
using Adrenak.UniVoice;

/// <summary>
/// Presenter that bridges UI components with SettingsManager properties.
/// Handles Sliders, Dropdowns, and a live microphone volume indicator (RMS) with thread-safety.
/// </summary>
public class SettingsUIPresenter : MonoBehaviour
{
    [Header("Audio UI Elements")]
    [SerializeField] private Slider _masterVolumeSlider;
    [SerializeField] private Slider _voiceVolumeSlider;
    [SerializeField] private Slider _micSensitivitySlider;
    [SerializeField] private TMP_Dropdown _microphoneDropdown;

    [Header("Microphone Level Indicator")]
    [SerializeField] private Slider _micLevelIndicator;
    [SerializeField] private Image _micLevelFillImage;
    [SerializeField] private Color _silenceColor = Color.green;
    [SerializeField] private Color _talkingColor = Color.cyan;
    [SerializeField] private float _indicatorSmoothSpeed = 10f;
    [SerializeField] private Toggle _micTestToggle;

    // Cached runtime variables
    private float _latestRms = 0f;
    private float _smoothedRms = 0f;
    private bool _isSubscribedToMic = false;

    private void OnEnable()
    {
        InitializeUI();
        BindUIEvents();
        SubscribeToMicrophoneEvents();

        // Listen for microphone hot-swap changes to re-bind the RMS level listener
        VoiceSettingsConsumer.OnMicInputSwapped += HandleMicInputSwapped;
    }

    private void OnDisable()
    {
        UnbindUIEvents();
        UnsubscribeFromMicrophoneEvents();
        VoiceSettingsConsumer.OnMicInputSwapped -= HandleMicInputSwapped;

        // Force disable microphone loopback preview when the UI panel is closed
        VoiceSettingsConsumer.SetLocalLoopback(false);

        // Flush settings to disk when settings panel is closed/disabled
        if (SettingsManager.HasInstance)
        {
            SettingsManager.Instance.SaveToDisk();
        }
    }

    private void HandleMicInputSwapped()
    {
        // Re-subscribe the frame analyzer to the newly instantiated Input device
        UnsubscribeFromMicrophoneEvents();
        SubscribeToMicrophoneEvents();
        Debug.Log("[SettingsUIPresenter] Re-subscribed RMS indicator to new microphone input.");
    }

    private void Update()
    {
        UpdateVolumeIndicator();
    }

    private void InitializeUI()
    {
        if (SettingsManager.Instance == null) return;

        var current = SettingsManager.Instance.CurrentSettings;

        // Initialize sliders
        if (_masterVolumeSlider != null) _masterVolumeSlider.value = current.MasterVolume;
        if (_voiceVolumeSlider != null) _voiceVolumeSlider.value = current.VoiceVolume;
        if (_micSensitivitySlider != null) _micSensitivitySlider.value = current.MicSensitivityLimit;

        // Populate and initialize microphone dropdown
        if (_microphoneDropdown != null)
        {
            _microphoneDropdown.ClearOptions();
            var devices = new List<string>(Mic.AvailableDevices.ConvertAll(d => d.Name));
            _microphoneDropdown.AddOptions(devices);

            int selectedIndex = devices.IndexOf(current.ActiveMicrophoneDevice);
            _microphoneDropdown.value = Mathf.Max(0, selectedIndex);
        }

        // Always reset the mic test toggle to off when opening the UI
        if (_micTestToggle != null)
        {
            _micTestToggle.isOn = false;
        }
    }

    private void BindUIEvents()
    {
        if (_masterVolumeSlider != null) _masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        if (_voiceVolumeSlider != null) _voiceVolumeSlider.onValueChanged.AddListener(OnVoiceVolumeChanged);
        if (_micSensitivitySlider != null) _micSensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        if (_microphoneDropdown != null) _microphoneDropdown.onValueChanged.AddListener(OnMicrophoneSelected);
        if (_micTestToggle != null) _micTestToggle.onValueChanged.AddListener(OnMicTestToggleChanged);
    }

    private void UnbindUIEvents()
    {
        if (_masterVolumeSlider != null) _masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
        if (_voiceVolumeSlider != null) _voiceVolumeSlider.onValueChanged.RemoveListener(OnVoiceVolumeChanged);
        if (_micSensitivitySlider != null) _micSensitivitySlider.onValueChanged.RemoveListener(OnSensitivityChanged);
        if (_microphoneDropdown != null) _microphoneDropdown.onValueChanged.RemoveListener(OnMicrophoneSelected);
        if (_micTestToggle != null) _micTestToggle.onValueChanged.RemoveListener(OnMicTestToggleChanged);
    }

    private void SubscribeToMicrophoneEvents()
    {
        if (_isSubscribedToMic) return;

        // Wait for UniVoice ClientSession to initialize
        StartCoroutine(WaitForClientSessionAndSubscribe());
    }

    private System.Collections.IEnumerator WaitForClientSessionAndSubscribe()
    {
        while (UniVoiceMirrorSetupSample.ClientSession == null || UniVoiceMirrorSetupSample.ClientSession.Input == null)
        {
            yield return null;
        }

        UniVoiceMirrorSetupSample.ClientSession.Input.OnFrameReady += OnLocalMicFrameReady;
        _isSubscribedToMic = true;
    }

    private void UnsubscribeFromMicrophoneEvents()
    {
        if (!_isSubscribedToMic) return;

        if (UniVoiceMirrorSetupSample.ClientSession != null && UniVoiceMirrorSetupSample.ClientSession.Input != null)
        {
            UniVoiceMirrorSetupSample.ClientSession.Input.OnFrameReady -= OnLocalMicFrameReady;
        }
        _isSubscribedToMic = false;
    }

    private void OnLocalMicFrameReady(AudioFrame frame)
    {
        if (frame.samples == null) return;

        // Calculate RMS (Root-Mean-Square) of the frame samples in-place
        double sumSq = 0;
        int count = 0;
        for (int i = 0; i < frame.samples.Length; i += 4)
        {
            if (i + 3 >= frame.samples.Length) break;
            float sample = BitConverter.ToSingle(frame.samples, i);
            sumSq += (double)sample * sample;
            count++;
        }

        float rms = count > 0 ? (float)Math.Sqrt(sumSq / count) : 0f;

        // Save the value to be processed on the main thread in Update()
        _latestRms = rms;
    }

    private void UpdateVolumeIndicator()
    {
        if (_micLevelIndicator == null) return;

        // Interpolate the value on the main thread for smooth transitions
        _smoothedRms = Mathf.Lerp(_smoothedRms, _latestRms, Time.deltaTime * _indicatorSmoothSpeed);
        _micLevelIndicator.value = Mathf.Clamp01(_smoothedRms * 10f); // Multiply to boost visualization scale

        // Visual feedback matching Discord: change indicator color when speaking vs silent
        if (_micLevelFillImage != null)
        {
            bool isSpeaking = false;
            if (UniVoiceMirrorSetupSample.LocalVad != null)
            {
                isSpeaking = UniVoiceMirrorSetupSample.LocalVad.IsSpeaking;
            }
            else if (_micSensitivitySlider != null)
            {
                // Fallback check (e.g. offline menu): compare normalized live RMS against slider threshold
                isSpeaking = _micLevelIndicator.value > _micSensitivitySlider.value;
            }

            _micLevelFillImage.color = isSpeaking ? _talkingColor : _silenceColor;
        }
    }

    private void OnMasterVolumeChanged(float value)
    {
        SettingsManager.Instance.UpdateSettings(data =>
        {
            data.MasterVolume = value;
        });
    }

    private void OnVoiceVolumeChanged(float value)
    {
        SettingsManager.Instance.UpdateSettings(data =>
        {
            data.VoiceVolume = value;
        });
    }

    private void OnSensitivityChanged(float value)
    {
        SettingsManager.Instance.UpdateSettings(data =>
        {
            data.MicSensitivityLimit = value;
        });
    }

    private void OnMicrophoneSelected(int index)
    {
        var devices = Mic.AvailableDevices;
        if (index < 0 || index >= devices.Count) return;

        string selectedDevice = devices[index].Name;
        SettingsManager.Instance.UpdateSettings(data =>
        {
            data.ActiveMicrophoneDevice = selectedDevice;
        });
    }

    private void OnMicTestToggleChanged(bool value)
    {
        VoiceSettingsConsumer.SetLocalLoopback(value);
    }
}
