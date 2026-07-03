using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Description: Service locator / Singleton manager for user configuration lifecycle.
/// Context: Globally accessible via SettingsManager.Instance to fetch or update settings.
/// Justification: Coordinates saving/loading from PlayerPrefs and dispatching events to decoupled consumers. A Singleton is used here because settings must be universally accessible and persistent across scenes.
/// </summary>
public class SettingsManager : MonoBehaviour
{
    private static SettingsManager _instance;
    private static bool _isQuitting = false;

    /// <summary>
    /// Description: Gets whether a valid active instance of SettingsManager exists.
    /// Context: Used during teardown (OnDestroy / OnApplicationQuit) by other scripts.
    /// Justification: Prevents accidental Singleton recreation during application exit, which causes ghost objects and memory leaks in the editor.
    /// </summary>
    public static bool HasInstance => _instance != null;

    /// <summary>
    /// Description: Gets the singleton instance of the Settings Manager.
    /// Context: Accessed by any script needing to read or write settings.
    /// Justification: Lazy instantiation ensures the manager exists when called, while DontDestroyOnLoad keeps it alive across scene transitions.
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
    /// Description: Event raised when settings are changed.
    /// Context: Subscribed to by UI elements or dynamic systems that don't implement ISettingsConsumer.
    /// Justification: Provides a standard C# event pattern for lightweight listeners.
    /// </summary>
    public event Action<SettingsData> OnSettingsChanged;

    /// <summary>
    /// Description: Gets the current active settings data.
    /// Context: Used by consumers to read the current state of settings.
    /// Justification: Exposes the SSOT (Single Source of Truth) read-only so external scripts cannot replace the instance, only mutate it through UpdateSettings.
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
    /// Description: Registers a consumer to receive settings updates.
    /// Context: Called by consumers in their Start() or OnEnable() methods.
    /// Justification: Keeps a list of active consumers to push updates to, avoiding expensive FindObjectsOfType calls. Also immediately pushes the current settings to the new consumer.
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
    /// Description: Unregisters a consumer from settings updates.
    /// Context: Called by consumers in their OnDestroy() or OnDisable() methods.
    /// Justification: Prevents memory leaks and null reference exceptions when consumers are destroyed.
    /// </summary>
    /// <param name="consumer">The consumer instance.</param>
    public void UnregisterConsumer(ISettingsConsumer consumer)
    {
        if (consumer == null) return;
        _consumers.Remove(consumer);
    }

    /// <summary>
    /// Description: Updates a setting and notifies all registered consumers.
    /// Context: Called by UI presenters when a user changes a setting via slider/toggle.
    /// Justification: Takes a lambda to mutate data, ensuring that every modification triggers a save and notifies all listeners synchronously.
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
    /// Description: Loads settings data from PlayerPrefs.
    /// Context: Called automatically during Awake.
    /// Justification: Deserializes the JSON from disk. If missing or corrupted, creates a new default instance to ensure stability.
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
    /// Description: Saves settings data to PlayerPrefs memory.
    /// Context: Called automatically by UpdateSettings.
    /// Justification: Serializes the current state into JSON and stores it in Unity's PlayerPrefs. Note: Does not force disk write immediately to save performance.
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
    /// Description: Forces flushing PlayerPrefs changes to the physical disk.
    /// Context: Called during ApplicationQuit or ApplicationPause.
    /// Justification: Ensures that changes held in memory are physically written to the OS storage before the application is terminated.
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
