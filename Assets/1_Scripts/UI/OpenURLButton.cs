using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Description: Automatically configures a UGUI Button to open a serialized web link.
/// Context: Attached to a Button component.
/// Justification: Supports a safe visual fallback method for manual hookups in the Inspector without hard-coding URLs.
/// </summary>
[RequireComponent(typeof(Button))]
public class OpenURLButton : MonoBehaviour
{
    [Header("URL Settings")]
    [Tooltip("Role: The destination URL address.\nUse Case: External link.\nJustification: Opens in the system browser.")]
    [SerializeField]
    private string _url = "https://";

    [Header("Debug")]
    [SerializeField]
    private bool _enableDebugLogs = false;

    /// <summary>
    /// Description: Cached UGUI Button component on this GameObject.
    /// Context: Retrieved in Awake.
    /// Justification: Used to hook up the click listener programmatically.
    /// </summary>
    private Button _button;

    /// <summary>
    /// Description: Unity Awake callback. Caches the Button component and registers the click event.
    /// Context: Initialization.
    /// Justification: One-click setup experience.
    /// </summary>
    private void Awake()
    {
        // Cache the Button component attached to this GameObject
        _button = GetComponent<Button>();

        if (_button != null)
        {
            // Wire up the click handler automatically to achieve the "one-click" setup experience
            _button.onClick.AddListener(HandleButtonClick);
        }
        else
        {
            if (_enableDebugLogs) Debug.LogError($"[OpenURLButton] CRITICAL: Button component is missing on '{gameObject.name}' despite RequireComponent constraint!");
        }
    }

    /// <summary>
    /// Description: Unity OnDestroy callback. Unregisters listeners to prevent potential memory leaks.
    /// Context: Object destruction.
    /// Justification: Standard Unity Event cleanup practice.
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
    /// Description: Opens the configured URL in the default web browser.
    /// Context: Public function endpoint.
    /// Justification: Can be bound manually via Inspector onClick events if auto-setup fails.
    /// </summary>
    public void OpenConfiguredURL()
    {
        if (string.IsNullOrEmpty(_url) || _url == "https://")
        {
            if (_enableDebugLogs) Debug.LogWarning($"[OpenURLButton] Attempted to open URL on '{gameObject.name}', but the link is empty or default!");
            return;
        }

        // Clean up leading/trailing whitespaces that might cause browser failures
        string sanitizedUrl = _url.Trim();

        if (_enableDebugLogs) Debug.Log($"[OpenURLButton] Redirecting user to browser. URL: '{sanitizedUrl}'");
        Application.OpenURL(sanitizedUrl);
    }

    /// <summary>
    /// Description: Internal delegate listener fired when the cached Button is clicked.
    /// Context: Programmatic callback.
    /// Justification: Wrapper for OpenConfiguredURL.
    /// </summary>
    private void HandleButtonClick()
    {
        // Delegate execution to the main public redirect routine
        OpenConfiguredURL();
    }
}
