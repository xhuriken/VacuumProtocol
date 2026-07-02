using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Service locator / Singleton manager for user configuration lifecycle.
/// Coordinates saving/loading from PlayerPrefs and dispatching events to consumers.
/// </summary>
public class SettingsManager : MonoBehaviour
{
    private static SettingsManager _instance;
    private static bool _isQuitting = false;

    /// <summary>
    /// Gets whether a valid active instance of SettingsManager exists.
    /// </summary>
    public static bool HasInstance => _instance != null;

    /// <summary>
    /// Gets the singleton instance of the Settings Manager.
    /// </summary>
    public static SettingsManager Instance
    {
        get
        {
            if (_isQuitting)
            {
                return _instance;
            }

            if (_instance == null)
            {
                var go = GameObject.Find("SettingsManager");
                if (go == null)
                {
                    go = new GameObject("SettingsManager");
                }
                _instance = go.GetComponent<SettingsManager>();
                if (_instance == null)
                {
                    _instance = go.AddComponent<SettingsManager>();
                }
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private SettingsData _currentSettings;
    private readonly List<ISettingsConsumer> _consumers = new List<ISettingsConsumer>();

    /// <summary>
    /// Event raised when settings are changed.
    /// </summary>
    public event Action<SettingsData> OnSettingsChanged;

    /// <summary>
    /// Gets the current active settings data.
    /// </summary>
    public SettingsData CurrentSettings => _currentSettings;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        LoadSettings();
    }

    /// <summary>
    /// Registers a consumer to receive settings updates.
    /// </summary>
    /// <param name="consumer">The consumer instance.</param>
    public void RegisterConsumer(ISettingsConsumer consumer)
    {
        if (consumer == null) return;

        if (!_consumers.Contains(consumer))
        {
            _consumers.Add(consumer);
            consumer.OnSettingsUpdated(_currentSettings);
        }
    }

    /// <summary>
    /// Unregisters a consumer from settings updates.
    /// </summary>
    /// <param name="consumer">The consumer instance.</param>
    public void UnregisterConsumer(ISettingsConsumer consumer)
    {
        if (consumer == null) return;
        _consumers.Remove(consumer);
    }

    /// <summary>
    /// Updates a setting and notifies all registered consumers.
    /// </summary>
    /// <param name="updateAction">Lambda mutating the settings data.</param>
    public void UpdateSettings(Action<SettingsData> updateAction)
    {
        if (updateAction == null) return;

        updateAction(_currentSettings);
        SaveSettings();

        // Notify dynamic event subscribers
        OnSettingsChanged?.Invoke(_currentSettings);

        // Notify registered decoupled consumers
        for (int i = _consumers.Count - 1; i >= 0; i--)
        {
            if (_consumers[i] != null)
            {
                _consumers[i].OnSettingsUpdated(_currentSettings);
            }
            else
            {
                _consumers.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Loads settings data from PlayerPrefs.
    /// </summary>
    public void LoadSettings()
    {
        string json = PlayerPrefs.GetString("UserSettingsData", string.Empty);
        if (string.IsNullOrEmpty(json))
        {
            _currentSettings = new SettingsData();
            
            // Fallback to active microphone if available
            if (Adrenak.UniMic.Mic.AvailableDevices != null && Adrenak.UniMic.Mic.AvailableDevices.Count > 0)
            {
                _currentSettings.ActiveMicrophoneDevice = Adrenak.UniMic.Mic.AvailableDevices[0].Name;
            }
        }
        else
        {
            try
            {
                _currentSettings = JsonUtility.FromJson<SettingsData>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SettingsManager] Failed to deserialize settings: {ex.Message}");
                _currentSettings = new SettingsData();
            }
        }
    }

    /// <summary>
    /// Saves settings data to PlayerPrefs memory.
    /// </summary>
    public void SaveSettings()
    {
        if (_currentSettings == null) return;

        try
        {
            string json = JsonUtility.ToJson(_currentSettings);
            PlayerPrefs.SetString("UserSettingsData", json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SettingsManager] Failed to serialize settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Forces flushing PlayerPrefs changes to the physical disk.
    /// </summary>
    public void SaveToDisk()
    {
        try
        {
            PlayerPrefs.Save();
            Debug.Log("[SettingsManager] Settings successfully flushed to disk.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SettingsManager] Failed to flush settings to disk: {ex.Message}");
        }
    }

    private void OnApplicationQuit()
    {
        _isQuitting = true;
        SaveToDisk();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveToDisk();
        }
    }
}
