using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using Sirenix.OdinInspector;
using VacuumProtocol.Networking.Lobby;

/// <summary>
/// Manages the lobby robot color selection palette by coloring 16 buttons
/// and attaching smooth DOTween scale animations for hover and click states.
/// </summary>
public class UIColorsPalettes : MonoBehaviour
{

    [Header("Color Palette Options")]
    [Tooltip("The list of 16 editable Unity Colors available in the lobby palette.")]
    [SerializeField]
    private Color[] _colors = new Color[16];

    [Header("Debug")]
    [SerializeField]
    private bool _enableDebugLogs = false;

    [Header("Lobby Integration")]
    [Tooltip("Reference to the Lobby Customization UI script in the scene.")]
    [SerializeField]
    private LobbyCustomizationUI _lobbyCustomizationUI;

    /// <summary>
    /// Unity Start callback. Triggers the initialization of the color palette buttons.
    /// </summary>
    private void Start()
    {
        InitializePalette();
    }

    /// <summary>
    /// Unity Update callback. Kept for legacy compatibility if needed.
    /// </summary>
    private void Update()
    {
        // Reserved for future update logic if necessary.
    }
    

    /// <summary>
    /// Programmatically calculates a quantized gradient of 16 colors
    /// consisting of grayscale tones and vibrant HSV rainbow hues.
    /// </summary>
    [Button("Generate Quantized 16-Color Palette")]
    private void GenerateQuantizedPalette()
    {
        _colors = new Color[16];

        // 1. Grayscale tones (Black, Grey, White)
        _colors[0] = Color.black;
        _colors[1] = new Color(0.5f, 0.5f, 0.5f, 1f); // Medium Grey
        _colors[2] = Color.white;

        // 2. 13 Rainbow hues covering the HSL/HSV spectrum (Yellow, Orange, Red, Blue, Violet, Green, etc.)
        int hueCount = 13;
        for (int i = 0; i < hueCount; i++)
        {
            // Calculate hue stepping from 0.0 to 1.0
            float hue = (float)i / hueCount;
            // Convert HSV to RGB with full saturation and value for maximum vibrancy
            Color rainbowColor = Color.HSVToRGB(hue, 1f, 1f);
            
            _colors[3 + i] = rainbowColor;
        }

        if (_enableDebugLogs) Debug.Log("[UIColorsPalettes] Generated 16 quantized colors successfully.");
    }

    /// <summary>
    /// Automatically retrieves all child custom shape buttons, assigns them their respective colors,
    /// sets up click handlers to communicate with LobbyCustomizationUI, and drives custom shape animations.
    /// </summary>
    [Button("Initialize Palette")]
    private void InitializePalette()
    {
        // Try to automatically find LobbyCustomizationUI if not assigned in the Inspector
        if (_lobbyCustomizationUI == null)
        {
            _lobbyCustomizationUI = FindAnyObjectByType<LobbyCustomizationUI>();
            if (_lobbyCustomizationUI != null)
            {
                if (_enableDebugLogs) Debug.Log("[UIColorsPalettes] Automatically located LobbyCustomizationUI in the scene.");
            }
            else
            {
                if (_enableDebugLogs) Debug.LogWarning("[UIColorsPalettes] LobbyCustomizationUI could not be found in the scene. Click actions may fail until it is present.");
            }
        }

        // Fetch all custom color buttons in the children of this GameObject
        ColorButtonUI[] buttons = GetComponentsInChildren<ColorButtonUI>(true);
        if (_enableDebugLogs) Debug.Log($"[UIColorsPalettes] Found {buttons.Length} child custom color buttons to initialize.");

        // Loop through all custom buttons and configure them based on our color palette
        for (int i = 0; i < buttons.Length; i++)
        {
            // Stop configuring if we exceed the color palette list size
            if (i >= _colors.Length)
            {
                if (_enableDebugLogs) Debug.LogWarning($"[UIColorsPalettes] More child buttons exist ({buttons.Length}) than colors defined ({_colors.Length}). Remaining buttons will be left unconfigured.");
                break;
            }

            ColorButtonUI currentButton = buttons[i];
            Color buttonColor = _colors[i];

            // Assign the palette color to the custom shape button, which handles coloring nested shapes
            currentButton.SetButtonColor(buttonColor);

            // Convert the Unity Color into an HTML Hex string for multiplayer sync compatibility
            string hexColor = "#" + ColorUtility.ToHtmlStringRGB(buttonColor);

            // Clean previous listeners and add a new hex assignment action
            string capturedHex = hexColor; // Prevent closure variable capture issue in C# loops
            currentButton.onClick.RemoveAllListeners();
            currentButton.onClick.AddListener(() =>
            {
                if (_lobbyCustomizationUI != null)
                {
                    _lobbyCustomizationUI.SetPlayerColorHex(capturedHex);
                }
                else
                {
                    // Fallback to find active lobby UI dynamically if original was destroyed or re-loaded
                    LobbyCustomizationUI dynamicLobbyUI = FindAnyObjectByType<LobbyCustomizationUI>();
                    if (dynamicLobbyUI != null)
                    {
                        dynamicLobbyUI.SetPlayerColorHex(capturedHex);
                    }
                    else
                    {
                        if (_enableDebugLogs) Debug.LogError("[UIColorsPalettes] Color button clicked, but no LobbyCustomizationUI was found in the scene.");
                    }
                }
            });
        }

        if (_enableDebugLogs) Debug.Log("[UIColorsPalettes] Color palette successfully initialized.");
    }
}

