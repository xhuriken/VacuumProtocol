using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Adrenak.UniMic;
using Adrenak.UniVoice;
using System.Reflection;

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
    [SerializeField] private Color _silenceColor = new Color(0.341f, 0.235f, 0.251f, 1.000f);
    [SerializeField] private Color _talkingColor = new Color(0.000f, 1.000f, 0.251f, 1.000f);
    [SerializeField] private float _indicatorSmoothSpeed = 10f;
    [SerializeField] private Toggle _micTestToggle;

    [Header("Auto VAD Mode")]
    [Tooltip("When enabled, bypasses the manual sensitivity slider and uses UniVoice's default VAD algorithm.")]
    [SerializeField] private Toggle _autoVadToggle;
    [SerializeField] private Slider _autoVadSensitivitySliderRef; // reference to disable slider in Auto mode

    // Cached runtime variables
    private float _latestRms = 0f;
    private float _smoothedRms = 0f;
    private bool _isSubscribedToMic = false;
    private FieldInfo _noiseRmsField;
    private float _lastLoggedSnrDb = float.MinValue;
    private const float SnrLogThresholdDelta = 1.5f; // only log SNR if it changes by more than this amount

    private void OnEnable()
    {
        InitializeUI();
        BindUIEvents();
        SubscribeToMicrophoneEvents();

        // Cache reflection field to query active noise floor from UniVoice VAD
        _noiseRmsField = typeof(SimpleVad).GetField("_noiseRms", BindingFlags.NonPublic | BindingFlags.Instance);

        // Listen for microphone hot-swap changes to re-bind the RMS level listener
        VoiceSettingsConsumer.OnMicInputSwapped += HandleMicInputSwapped;
    }

    private void OnDisable()
    {
        UnbindUIEvents();
        UnsubscribeFromMicrophoneEvents();
        VoiceSettingsConsumer.OnMicInputSwapped -= HandleMicInputSwapped;

        // Force disable microphone loopback preview and auto VAD when the UI panel is closed
        VoiceSettingsConsumer.SetLocalLoopback(false);
        // Note: we do NOT reset AutoVad on panel close — user preference persists until toggled off

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

        // Always reset both toggles to off when opening the UI
        if (_micTestToggle != null) _micTestToggle.isOn = false;
        if (_autoVadToggle != null) _autoVadToggle.isOn = VoiceSettingsConsumer.IsAutoVad;

        // Sync slider interactability with current auto mode state
        UpdateSensitivitySliderInteractability();
    }

    private void BindUIEvents()
    {
        if (_masterVolumeSlider != null) _masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        if (_voiceVolumeSlider != null) _voiceVolumeSlider.onValueChanged.AddListener(OnVoiceVolumeChanged);
        if (_micSensitivitySlider != null) _micSensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        if (_microphoneDropdown != null) _microphoneDropdown.onValueChanged.AddListener(OnMicrophoneSelected);
        if (_micTestToggle != null) _micTestToggle.onValueChanged.AddListener(OnMicTestToggleChanged);
        if (_autoVadToggle != null) _autoVadToggle.onValueChanged.AddListener(OnAutoVadToggleChanged);
    }

    private void UnbindUIEvents()
    {
        if (_masterVolumeSlider != null) _masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
        if (_voiceVolumeSlider != null) _voiceVolumeSlider.onValueChanged.RemoveListener(OnVoiceVolumeChanged);
        if (_micSensitivitySlider != null) _micSensitivitySlider.onValueChanged.RemoveListener(OnSensitivityChanged);
        if (_microphoneDropdown != null) _microphoneDropdown.onValueChanged.RemoveListener(OnMicrophoneSelected);
        if (_micTestToggle != null) _micTestToggle.onValueChanged.RemoveListener(OnMicTestToggleChanged);
        if (_autoVadToggle != null) _autoVadToggle.onValueChanged.RemoveListener(OnAutoVadToggleChanged);
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

        // Peak-hold (Instant-Attack, Slow-Decay) meter logic.
        float rawTarget = _latestRms;
        if (rawTarget > _smoothedRms)
        {
            _smoothedRms = rawTarget;
        }
        else
        {
            _smoothedRms = Mathf.Lerp(_smoothedRms, rawTarget, Time.deltaTime * _indicatorSmoothSpeed);
        }

        // Get VAD noise floor dynamically via reflection to compute actual SNR
        float noiseRms = 0.002f; // default fallback if VAD is not active yet
        if (UniVoiceMirrorSetupSample.LocalVad != null && _noiseRmsField != null)
        {
            try
            {
                noiseRms = (float)_noiseRmsField.GetValue(UniVoiceMirrorSetupSample.LocalVad);
            }
            catch
            {
                noiseRms = 0.002f;
            }
        }

        // Calculate live SNR in dB
        float snrDb = 20f * Mathf.Log10((_smoothedRms + 1e-6f) / (noiseRms + 1e-6f));

        // Log the SNR when it crosses a meaningful threshold — useful for comparing against sensitivity threshold logs
        if (Mathf.Abs(snrDb - _lastLoggedSnrDb) >= SnrLogThresholdDelta)
        {
            _lastLoggedSnrDb = snrDb;
            Debug.Log($"[SettingsUIPresenter] Audio peak SNR = {snrDb:F2} dB  (noise floor = {noiseRms:F6}  signal RMS = {_smoothedRms:F6})");
        }

        // In Auto mode: indicator still shows level but calibration is based on defaults (8 dB enter / 4 dB exit)
        float snrMin, snrRange;
        if (VoiceSettingsConsumer.IsAutoVad)
        {
            snrMin = 4f;  // SnrExitDb default
            snrRange = 12f; // 4..16 dB covers the visible range when using defaults
        }
        else
        {
            snrMin = 2.0f;   // manual mode minimum
            snrRange = 16.0f; // 2..18 dB maps to 0..1
        }

        // Map SNR to the linear [0..1] range of the indicator
        float normalizedVal = (snrDb - snrMin) / snrRange;
        _micLevelIndicator.value = Mathf.Clamp01(normalizedVal);

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
                // Fallback check (e.g. offline menu): compare normalized live SNR against slider threshold
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
        if (VoiceSettingsConsumer.IsAutoVad)
        {
            Debug.Log("[SettingsUIPresenter] Sensitivity slider moved but AUTO mode is active — slider ignored.");
            return;
        }

        // Log the threshold equivalent in dB so you can compare to SNR peak logs
        float targetDb = 2.0f + (value * 16.0f);
        Debug.Log($"[SettingsUIPresenter] Sensitivity slider -> {value:F3}  |  VAD will trigger when SNR > {targetDb:F2} dB");

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

    private void OnAutoVadToggleChanged(bool value)
    {
        VoiceSettingsConsumer.SetAutoVad(value);

        // When toggling AUTO off, immediately force re-apply the saved sensitivity to restore manual config
        if (!value && SettingsManager.HasInstance)
        {
            SettingsManager.Instance.UpdateSettings(data =>
            {
                // Invalidate cached value to force VoiceSettingsConsumer to re-apply the threshold
                data.MicSensitivityLimit = data.MicSensitivityLimit;
            });
        }

        UpdateSensitivitySliderInteractability();
        Debug.Log($"[SettingsUIPresenter] Auto VAD toggled: {value}");
    }

    /// <summary>
    /// Greys out the sensitivity slider and its reference when Auto VAD is enabled.
    /// </summary>
    private void UpdateSensitivitySliderInteractability()
    {
        bool manual = !VoiceSettingsConsumer.IsAutoVad;
        if (_micSensitivitySlider != null) _micSensitivitySlider.interactable = manual;
        if (_autoVadSensitivitySliderRef != null) _autoVadSensitivitySliderRef.interactable = manual;
    }
}
