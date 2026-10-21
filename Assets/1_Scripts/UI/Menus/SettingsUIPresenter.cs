using System;
using System.Collections.Generic;
using System.Reflection;
using Adrenak.UniMic;
using Adrenak.UniVoice;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Description: Presenter that bridges UI components with SettingsManager properties.
/// Context: Attached to the Settings Panel UI object.
/// Justification: Handles Sliders, Dropdowns, and a live microphone volume indicator (RMS) with thread-safety.
/// </summary>
public class SettingsUIPresenter : MonoBehaviour
{
    [Header("Audio UI Elements")]
    [SerializeField] private UICustomSlider _masterVolumeSlider;
    [SerializeField] private UICustomSlider _voiceVolumeSlider;
    [SerializeField] private UICustomSlider _micSensitivitySlider;
    [SerializeField] private UICustomDropdown _microphoneDropdown;

    [Header("Microphone Level Indicator")]
    [SerializeField] private UICustomSlider _micLevelIndicator;
    [SerializeField] private Color _silenceColor = new Color(0.341f, 0.235f, 0.251f, 1.000f);
    [SerializeField] private Color _talkingColor = new Color(0.000f, 1.000f, 0.251f, 1.000f);
    [SerializeField] private float _indicatorSmoothSpeed = 10f;
    [SerializeField] private UICustomToggle _micTestToggle;

    [Header("Auto VAD Mode")]
    [Tooltip("Role: Toggles auto VAD mode.\nUse Case: Settings configuration.\nJustification: When enabled, bypasses the manual sensitivity slider and uses UniVoice's default algorithm.")]
    [SerializeField] private UICustomToggle _autoVadToggle;
    [SerializeField] private UICustomSlider _autoVadSensitivitySliderRef; // reference to disable slider in Auto mode

    [Header("Gameplay UI Elements")]
    [SerializeField] private UICustomSlider _fovSlider;
    [SerializeField] private UICustomToggle _debugGizmosToggle;

    [Header("Graphics UI Elements")]
    [SerializeField] private UICustomSlider _motionBlurSlider;
    [SerializeField] private UICustomDropdown _qualityDropdown;
    [SerializeField] private UICustomToggle _fullscreenToggle;

    [Header("Debug")]
    [SerializeField] private bool _enableDebugLogs = true;

    // Cached runtime variables
    private float _latestRms = 0f;
    private float _smoothedRms = 0f;
    private bool _isSubscribedToMic = false;
    private FieldInfo _noiseRmsField;
    private float _lastLoggedSnrDb = float.MinValue;
    private const float SnrLogThresholdDelta = 1.5f; // only log SNR if it changes by more than this amount

    private void Awake()
    {
        AutoWireElements();
    }

    private void OnEnable()
    {
        AutoWireElements();
        InitializeUI();
        BindUIEvents();
        SubscribeToMicrophoneEvents();

        // Cache reflection field to query active noise floor from UniVoice VAD
        _noiseRmsField = typeof(SimpleVad).GetField("_noiseRms", BindingFlags.NonPublic | BindingFlags.Instance);

        // Listen for microphone hot-swap changes to re-bind the RMS level listener
        VoiceSettingsConsumer.OnMicInputSwapped += HandleMicInputSwapped;
    }

    private void AutoWireElements()
    {
        UICustomSlider[] sliders = GetComponentsInChildren<UICustomSlider>(true);
        UICustomToggle[] toggles = GetComponentsInChildren<UICustomToggle>(true);
        UICustomDropdown[] dropdowns = GetComponentsInChildren<UICustomDropdown>(true);

        for (int i = 0; i < sliders.Length; i++)
        {
            string lowerName = sliders[i].name.ToLower();
            if (_fovSlider == null && (lowerName.Contains("fov") || lowerName.Contains("field"))) _fovSlider = sliders[i];
            if (_motionBlurSlider == null && (lowerName.Contains("blur") || lowerName.Contains("motion"))) _motionBlurSlider = sliders[i];
            if (_masterVolumeSlider == null && lowerName.Contains("master")) _masterVolumeSlider = sliders[i];
            if (_voiceVolumeSlider == null && lowerName.Contains("voice")) _voiceVolumeSlider = sliders[i];
            if (_micSensitivitySlider == null && (lowerName.Contains("sens") || lowerName.Contains("mic"))) _micSensitivitySlider = sliders[i];
        }

        for (int i = 0; i < toggles.Length; i++)
        {
            string lowerName = toggles[i].name.ToLower();
            if (_debugGizmosToggle == null && (lowerName.Contains("gizmo") || lowerName.Contains("guizmo") || lowerName.Contains("debug"))) _debugGizmosToggle = toggles[i];
            if (_fullscreenToggle == null && (lowerName.Contains("screen") || lowerName.Contains("full"))) _fullscreenToggle = toggles[i];
            if (_autoVadToggle == null && lowerName.Contains("auto")) _autoVadToggle = toggles[i];
            if (_micTestToggle == null && lowerName.Contains("test")) _micTestToggle = toggles[i];
        }

        for (int i = 0; i < dropdowns.Length; i++)
        {
            string lowerName = dropdowns[i].name.ToLower();
            if (_qualityDropdown == null && lowerName.Contains("quality")) _qualityDropdown = dropdowns[i];
            if (_microphoneDropdown == null && (lowerName.Contains("mic") || lowerName.Contains("device"))) _microphoneDropdown = dropdowns[i];
        }
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
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SaveSettings();
        }
    }

    private void HandleMicInputSwapped()
    {
        // Re-subscribe the frame analyzer to the newly instantiated Input device
        UnsubscribeFromMicrophoneEvents();
        SubscribeToMicrophoneEvents();
        if (_enableDebugLogs) Debug.Log("[SettingsUIPresenter] Re-subscribed RMS indicator to new microphone input.");
    }

    private void Update()
    {
        UpdateVolumeIndicator();
    }

    private void InitializeUI()
    {
        if (SettingsManager.Instance == null) return;

        var current = SettingsManager.Instance.CurrentSettings;
        if (current == null)
        {
            Debug.LogWarning("[SettingsUIPresenter] InitializeUI -> CurrentSettings est encore null !");
            return;
        }

        Debug.Log($"[SettingsUIPresenter] InitializeUI -> Données actuelles chargées: FOV={current.FieldOfView} | Gizmos={current.ShowDebugGizmos}");

        // Initialize sliders
        if (_masterVolumeSlider != null) _masterVolumeSlider.value = current.MasterVolume;
        if (_voiceVolumeSlider != null) _voiceVolumeSlider.value = current.VoiceVolume;
        if (_micSensitivitySlider != null) _micSensitivitySlider.value = current.MicSensitivityLimit;

        // Populate and initialize microphone dropdown
        if (_microphoneDropdown != null)
        {
            _microphoneDropdown.ClearOptions();
            var devices = new List<string>(UnityEngine.Microphone.devices);
            _microphoneDropdown.AddOptions(devices);

            int selectedIndex = devices.IndexOf(current.ActiveMicrophoneDevice);
            _microphoneDropdown.value = Mathf.Max(0, selectedIndex);
        }

        // Gameplay
        if (_fovSlider != null)
        {
            _fovSlider.minValue = 60f;
            _fovSlider.maxValue = 120f;
            _fovSlider.value = current.FieldOfView;
            Debug.Log($"[SettingsUIPresenter] _fovSlider ({_fovSlider.name}) configuré: Min=60, Max=120, Value={current.FieldOfView}");
        }
        else
        {
            Debug.LogWarning("[SettingsUIPresenter] Attention : _fovSlider n'est pas assigné !");
        }

        if (_debugGizmosToggle != null)
        {
            _debugGizmosToggle.isOn = current.ShowDebugGizmos;
            Debug.Log($"[SettingsUIPresenter] _debugGizmosToggle ({_debugGizmosToggle.name}) configuré: IsOn={current.ShowDebugGizmos}");
        }
        else
        {
            Debug.LogWarning("[SettingsUIPresenter] Attention : _debugGizmosToggle n'est pas assigné !");
        }

        // Graphics
        if (_motionBlurSlider != null) _motionBlurSlider.value = current.MotionBlurIntensity;
        if (_qualityDropdown != null)
        {
            _qualityDropdown.ClearOptions();
            var qualityNames = new List<string>(QualitySettings.names);
            _qualityDropdown.AddOptions(qualityNames);
            _qualityDropdown.value = Mathf.Clamp(current.QualityLevelIndex, 0, Mathf.Max(0, qualityNames.Count - 1));
        }
        if (_fullscreenToggle != null) _fullscreenToggle.isOn = current.IsFullscreen;

        // Always reset both toggles to off when opening the UI
        if (_micTestToggle != null) _micTestToggle.isOn = false;
        if (_autoVadToggle != null) _autoVadToggle.isOn = VoiceSettingsConsumer.IsAutoVad;

        // Sync slider interactability with current auto mode state
        UpdateSensitivitySliderInteractability();
    }

    private void BindUIEvents()
    {
        if (_masterVolumeSlider != null)
        {
            _masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            _masterVolumeSlider.onPointerUp.AddListener(OnSliderReleased);
        }
        if (_voiceVolumeSlider != null)
        {
            _voiceVolumeSlider.onValueChanged.AddListener(OnVoiceVolumeChanged);
            _voiceVolumeSlider.onPointerUp.AddListener(OnSliderReleased);
        }
        if (_micSensitivitySlider != null)
        {
            _micSensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
            _micSensitivitySlider.onPointerUp.AddListener(OnSliderReleased);
        }
        if (_microphoneDropdown != null) _microphoneDropdown.onValueChanged.AddListener(OnMicrophoneSelected);
        if (_micTestToggle != null) _micTestToggle.onValueChanged.AddListener(OnMicTestToggleChanged);
        if (_autoVadToggle != null) _autoVadToggle.onValueChanged.AddListener(OnAutoVadToggleChanged);

        // Gameplay

        if (_fovSlider != null)
        {
            _fovSlider.onValueChanged.AddListener(OnFovChanged);
            _fovSlider.onPointerUp.AddListener(OnSliderReleased);
        }
        if (_debugGizmosToggle != null) _debugGizmosToggle.onValueChanged.AddListener(OnDebugGizmosChanged);

        // Graphics
        if (_motionBlurSlider != null)
        {
            _motionBlurSlider.onValueChanged.AddListener(OnMotionBlurChanged);
            _motionBlurSlider.onPointerUp.AddListener(OnSliderReleased);
        }
        if (_qualityDropdown != null) _qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
        if (_fullscreenToggle != null) _fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
    }

    private void UnbindUIEvents()
    {
        if (_masterVolumeSlider != null)
        {
            _masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
            _masterVolumeSlider.onPointerUp.RemoveListener(OnSliderReleased);
        }
        if (_voiceVolumeSlider != null)
        {
            _voiceVolumeSlider.onValueChanged.RemoveListener(OnVoiceVolumeChanged);
            _voiceVolumeSlider.onPointerUp.RemoveListener(OnSliderReleased);
        }
        if (_micSensitivitySlider != null)
        {
            _micSensitivitySlider.onValueChanged.RemoveListener(OnSensitivityChanged);
            _micSensitivitySlider.onPointerUp.RemoveListener(OnSliderReleased);
        }
        if (_microphoneDropdown != null) _microphoneDropdown.onValueChanged.RemoveListener(OnMicrophoneSelected);
        if (_micTestToggle != null) _micTestToggle.onValueChanged.RemoveListener(OnMicTestToggleChanged);
        if (_autoVadToggle != null) _autoVadToggle.onValueChanged.RemoveListener(OnAutoVadToggleChanged);

        // Gameplay
        if (_fovSlider != null)
        {
            _fovSlider.onValueChanged.RemoveListener(OnFovChanged);
            _fovSlider.onPointerUp.RemoveListener(OnSliderReleased);
        }
        if (_debugGizmosToggle != null) _debugGizmosToggle.onValueChanged.RemoveListener(OnDebugGizmosChanged);

        // Graphics
        if (_motionBlurSlider != null)
        {
            _motionBlurSlider.onValueChanged.RemoveListener(OnMotionBlurChanged);
            _motionBlurSlider.onPointerUp.RemoveListener(OnSliderReleased);
        }
        if (_qualityDropdown != null) _qualityDropdown.onValueChanged.RemoveListener(OnQualityChanged);
        if (_fullscreenToggle != null) _fullscreenToggle.onValueChanged.RemoveListener(OnFullscreenChanged);
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
            if (_enableDebugLogs) Debug.Log($"[SettingsUIPresenter] Audio peak SNR = {snrDb:F2} dB  (noise floor = {noiseRms:F6}  signal RMS = {_smoothedRms:F6})");
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
        if (_micLevelIndicator != null)
        {
            _micLevelIndicator.value = Mathf.Clamp01(normalizedVal);

            // Visual feedback matching Discord: change indicator color when speaking vs silent
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

            _micLevelIndicator.fillColor = isSpeaking ? _talkingColor : _silenceColor;
        }
    }

    private void OnMasterVolumeChanged(float value)
    {
        SettingsManager.Instance.UpdateSettings(data =>
        {
            data.MasterVolume = value;
        }, saveToDisk: false);
    }

    private void OnVoiceVolumeChanged(float value)
    {
        SettingsManager.Instance.UpdateSettings(data =>
        {
            data.VoiceVolume = value;
        }, saveToDisk: false);
    }

    private void OnSensitivityChanged(float value)
    {
        if (VoiceSettingsConsumer.IsAutoVad)
        {
            if (_enableDebugLogs) Debug.Log("[SettingsUIPresenter] Sensitivity slider moved but AUTO mode is active — slider ignored.");
            return;
        }

        // Log the threshold equivalent in dB so you can compare to SNR peak logs
        float targetDb = 2.0f + (value * 16.0f);
        if (_enableDebugLogs) Debug.Log($"[SettingsUIPresenter] Sensitivity slider -> {value:F3}  |  VAD will trigger when SNR > {targetDb:F2} dB");

        SettingsManager.Instance.UpdateSettings(data =>
        {
            data.MicSensitivityLimit = value;
        }, saveToDisk: false);
    }

    private void OnMicrophoneSelected(int index)
    {
        var devices = UnityEngine.Microphone.devices;
        if (index < 0 || index >= devices.Length) return;

        string selectedDevice = devices[index];
        SettingsManager.Instance.UpdateSettings(data =>
        {
            data.ActiveMicrophoneDevice = selectedDevice;
        });
    }

    private void OnFovChanged(float value)
    {
        SettingsManager.Instance.UpdateSettings(data =>
        {
            data.FieldOfView = value;
            if (_enableDebugLogs) Debug.Log($"[SettingsUIPresenter] FOV modifié -> {data.FieldOfView}°");
        }, saveToDisk: true);
    }

    private void OnMotionBlurChanged(float value)
    {
        SettingsManager.Instance.UpdateSettings(data =>
        {
            data.MotionBlurIntensity = value;
            if (_enableDebugLogs) Debug.Log($"[SettingsUIPresenter] Motion Blur modifié -> {data.MotionBlurIntensity}");
        }, saveToDisk: true);
    }

    /// <summary>
    /// Description: Callback appelé quand un slider est relâché. Enregistre physiquement les données sur disque.
    /// </summary>
    private void OnSliderReleased()
    {
        SettingsManager.Instance.SaveSettings();
        if (_enableDebugLogs) Debug.Log("[SettingsUIPresenter] Slider relâché -> Sauvegarde sur disque exécutée (PlayerPrefs.Save).");
    }

    private void OnQualityChanged(int index)
    {
        QualitySettings.SetQualityLevel(index, true);
        SettingsManager.Instance.UpdateSettings(data =>
        {
            data.QualityLevelIndex = index;
        });
    }

    private void OnFullscreenChanged(bool value)
    {
        Screen.fullScreen = value;
        SettingsManager.Instance.UpdateSettings(data =>
        {
            data.IsFullscreen = value;
        });
    }

    private void OnDebugGizmosChanged(bool value)
    {
        SettingsManager.Instance.UpdateSettings(data =>
        {
            data.ShowDebugGizmos = value;
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
        if (_enableDebugLogs) Debug.Log($"[SettingsUIPresenter] Auto VAD toggled: {value}");
    }

    /// <summary>
    /// Description: Greys out the sensitivity slider and its reference when Auto VAD is enabled.
    /// Context: State synchronization.
    /// Justification: Prevents the user from trying to change a slider that has no effect.
    /// </summary>
    private void UpdateSensitivitySliderInteractability()
    {
        bool manual = !VoiceSettingsConsumer.IsAutoVad;
        if (_micSensitivitySlider != null) _micSensitivitySlider.interactable = manual;
        if (_autoVadSensitivitySliderRef != null) _autoVadSensitivitySliderRef.interactable = manual;
    }
}
