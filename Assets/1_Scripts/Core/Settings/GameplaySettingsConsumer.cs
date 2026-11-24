using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Description: Consumes gameplay and graphics settings like FOV and Motion Blur.
/// Context: Attached to a persistent manager or instantiated globally.
/// Justification: Implements ISettingsConsumer to dynamically apply graphics settings without polling or expensive FindObjectOfType calls every frame.
/// </summary>
public class GameplaySettingsConsumer : MonoBehaviour, ISettingsConsumer
{
    private Volume _globalVolume;
    private MotionBlur _motionBlur;
    private Camera _mainCamera;

    private void Start()
    {
        // Look up the global Volume only once at startup to avoid wasting performance
        _globalVolume = FindFirstObjectByType<Volume>();
        if (_globalVolume == null || _globalVolume.profile == null)
        {
            Debug.LogError($"[GameplaySettingsConsumer] '{gameObject.name}': no Volume with a profile in the scene, motion blur settings cannot be applied.", this);
        }
        else if (!_globalVolume.profile.TryGet(out _motionBlur))
        {
            Debug.LogWarning($"[GameplaySettingsConsumer] '{gameObject.name}': Volume profile '{_globalVolume.profile.name}' has no MotionBlur override, motion blur settings are ignored.", this);
        }

        if (!SettingsManager.HasInstance)
        {
            Debug.LogError($"[GameplaySettingsConsumer] '{gameObject.name}': no SettingsManager instance, gameplay settings will never be applied.", this);
            return;
        }
        SettingsManager.Instance.RegisterConsumer(this);
    }

    private void OnDestroy()
    {
        if (SettingsManager.HasInstance)
        {
            SettingsManager.Instance.UnregisterConsumer(this);
        }
    }

    /// <summary>
    /// Description: Callback invoked automatically when a value changes in the UI or on load.
    /// Applies FOV, motion blur, quality level and fullscreen mode.
    /// </summary>
    public void OnSettingsUpdated(SettingsData settings)
    {
        ApplyFieldOfView(settings.FieldOfView);
        ApplyMotionBlur(settings.MotionBlurIntensity);
        ApplyQuality(settings.QualityLevelIndex);
        ApplyFullscreen(settings.IsFullscreen);
    }

    private void ApplyFieldOfView(float fov)
    {
        bool applied = false;

        // Apply to the local player's camera first
        if (PlayerV2_Controller.LocalInstance != null && PlayerV2_Controller.LocalInstance.CameraTransform != null)
        {
            Camera playerCam = PlayerV2_Controller.LocalInstance.CameraTransform.GetComponentInChildren<Camera>();
            if (playerCam != null)
            {
                playerCam.fieldOfView = fov;
                applied = true;
            }
        }

        // Also apply to Camera.main
        _mainCamera = Camera.main;
        if (_mainCamera != null)
        {
            _mainCamera.fieldOfView = fov;
            applied = true;
        }

        if (!applied)
        {
            Debug.LogWarning($"[GameplaySettingsConsumer] '{gameObject.name}': no local player camera and no Camera.main, FOV {fov} not applied.", this);
        }
    }

    private void ApplyMotionBlur(float intensity)
    {
        // Missing Volume / override was already reported in Start.
        if (_motionBlur != null)
        {
            _motionBlur.intensity.value = intensity;
        }
    }

    private void ApplyQuality(int qualityIndex)
    {
        if (qualityIndex >= 0 && qualityIndex < QualitySettings.names.Length && QualitySettings.GetQualityLevel() != qualityIndex)
        {
            QualitySettings.SetQualityLevel(qualityIndex, true);
        }
    }

    private void ApplyFullscreen(bool isFullscreen)
    {
        FullScreenMode targetMode = isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        if (Screen.fullScreenMode != targetMode)
        {
            Screen.fullScreenMode = targetMode;
        }
        if (Screen.fullScreen != isFullscreen)
        {
            Screen.fullScreen = isFullscreen;
        }
    }
}
