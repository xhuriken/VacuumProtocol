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
        // On cherche le Volume global une seule fois au démarrage pour éviter de consommer des perfs
        _globalVolume = FindObjectOfType<Volume>();

        if (_globalVolume != null && _globalVolume.profile != null)
        {
            _globalVolume.profile.TryGet(out _motionBlur);
        }

        // On s'enregistre auprès du SettingsManager
        if (SettingsManager.HasInstance)
        {
            SettingsManager.Instance.RegisterConsumer(this);
        }
    }

    private void OnDestroy()
    {
        if (SettingsManager.HasInstance)
        {
            SettingsManager.Instance.UnregisterConsumer(this);
        }
    }

    /// <summary>
    /// Description: Callback appelé automatiquement quand une valeur change dans l'UI ou au chargement.
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
        // Appliquer à la caméra du joueur local en priorité
        if (PlayerV2_Controller.LocalInstance != null && PlayerV2_Controller.LocalInstance.CameraTransform != null)
        {
            Camera playerCam = PlayerV2_Controller.LocalInstance.CameraTransform.GetComponentInChildren<Camera>();
            if (playerCam != null)
            {
                playerCam.fieldOfView = fov;
            }
        }

        // Appliquer aussi à Camera.main
        _mainCamera = Camera.main;
        if (_mainCamera != null)
        {
            _mainCamera.fieldOfView = fov;
        }
    }

    private void ApplyMotionBlur(float intensity)
    {
        if (_globalVolume == null)
        {
            _globalVolume = FindFirstObjectByType<Volume>();
        }

        if (_globalVolume != null && _globalVolume.profile != null)
        {
            if (_motionBlur == null)
            {
                _globalVolume.profile.TryGet(out _motionBlur);
            }
            if (_motionBlur != null)
            {
                _motionBlur.intensity.value = intensity;
            }
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
