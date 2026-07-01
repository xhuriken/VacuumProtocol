using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Automatically detects a UGUI Button component on the same GameObject and configures
/// it to open a serialized web link in the default system browser upon being clicked.
/// Supports a safe visual fallback method for manual hookups in the Inspector.
/// </summary>
[RequireComponent(typeof(Button))]
public class OpenURLButton : MonoBehaviour
{
    [Header("URL Settings")]
    [Tooltip("The destination URL address to open in the system browser.")]
    [SerializeField]
    private string _url = "https://";

    /// <summary>
    /// Cached UGUI Button component on this GameObject.
    /// </summary>
    private Button _button;

    /// <summary>
    /// Unity Awake callback. Caches the Button component and registers the click event.
    /// </summary>
    private void Awake()
    {
        // Cache the Button component attached to this GameObject
        _button = GetComponent<Button>();

        if (_button != null)
        {
            // Wire up the click handler automatically to achieve the "one-click" setup experience
            _button.onClick.AddListener(HandleButtonClick);
            // Debug.Log($"[OpenURLButton] Automatically registered click event on '{gameObject.name}' for URL: {_url}");
        }
        else
        {
            Debug.LogError($"[OpenURLButton] CRITICAL: Button component is missing on '{gameObject.name}' despite RequireComponent constraint!");
        }
    }

    /// <summary>
    /// Unity OnDestroy callback. Unregisters listeners to prevent potential memory leaks.
    /// </summary>
    private void OnDestroy()
    {
        if (_button != null)
        {
            // Clean up the listener dynamically
            _button.onClick.RemoveListener(HandleButtonClick);
        }
    }

    /// <summary>
    /// Opens the configured URL in the default web browser.
    /// Exposes a public endpoint that can be bound manually via Inspector onClick events.
    /// </summary>
    public void OpenConfiguredURL()
    {
        if (string.IsNullOrEmpty(_url) || _url == "https://")
        {
            Debug.LogWarning($"[OpenURLButton] Attempted to open URL on '{gameObject.name}', but the link is empty or default!");
            return;
        }

        // Clean up leading/trailing whitespaces that might cause browser failures
        string sanitizedUrl = _url.Trim();

        Debug.Log($"[OpenURLButton] Redirecting user to browser. URL: '{sanitizedUrl}'");
        Application.OpenURL(sanitizedUrl);
    }

    /// <summary>
    /// Internal delegate listener fired when the cached Button is clicked.
    /// </summary>
    private void HandleButtonClick()
    {
        // Delegate execution to the main public redirect routine
        OpenConfiguredURL();
    }
}
