using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using Sirenix.OdinInspector;
using VacuumProtocol.Networking.Lobby;

/// <summary>
/// Description: Manages the lobby robot color selection palette by coloring 16 buttons and attaching smooth DOTween scale animations.
/// Context: Attached to a persistent UI element in the lobby.
/// Justification: Generates and handles a uniform color palette for players to pick from.
/// </summary>
public class UIColorsPalettes : MonoBehaviour
{

    [Header("Color Palette Options")]
    [Tooltip("Role: Array of colors.\nUse Case: Assigning colors to buttons.\nJustification: Provides exactly 16 color choices.")]
    [SerializeField]
    private Color[] _colors = new Color[16];

    [Header("Debug")]
    [Tooltip("Role: Flag to output logs.\nUse Case: Tracking initialization.\nJustification: Helps diagnose missing connections.")]
    [SerializeField]
    private bool _enableDebugLogs = false;

    [Header("Lobby Integration")]
    [Tooltip("Role: Optional Lobby Customization UI script.\nUse Case: Sending chosen player color.\nJustification: Fallback intermediary for local player customization.")]
    [SerializeField]
    private LobbyCustomizationUI _lobbyCustomizationUI;

    [Header("Events (Generic Reusable Pattern)")]
    [Tooltip("Role: Dispatched when a color is picked.\nUse Case: Observer pattern.\nJustification: Allows TextureEditor, Lobby, or any system to listen to color selection.")]
    [SerializeField]
    private UnityEngine.Events.UnityEvent<Color> _onColorSelected = new UnityEngine.Events.UnityEvent<Color>();

    [Tooltip("Role: Dispatched with Hex string when a color is picked.\nUse Case: Network sync.")]
    [SerializeField]
    private UnityEngine.Events.UnityEvent<string> _onHexColorSelected = new UnityEngine.Events.UnityEvent<string>();

    /// <summary>
    /// Description: UnityEvent invoked when any color button in the palette is clicked (emits Color).
    /// Context: External subscription API.
    /// Justification: Allows TextureEditorPanelUI to bind cleanly.
    /// </summary>
    public UnityEngine.Events.UnityEvent<Color> OnColorSelected => _onColorSelected;

    /// <summary>
    /// Description: UnityEvent invoked when any color button in the palette is clicked (emits Hex string).
    /// Context: External subscription API.
    /// Justification: Allows network customization to bind cleanly.
    /// </summary>
    public UnityEngine.Events.UnityEvent<string> OnHexColorSelected => _onHexColorSelected;

    /// <summary>
    /// Description: Unity Start callback. Triggers the initialization of the color palette buttons.
    /// Context: Initialization.
    /// Justification: Must wait for Awake to finish on child buttons.
    /// </summary>
    private void Start()
    {
        InitializePalette();
    }

    /// <summary>
    /// Description: Unity Update callback. Kept for legacy compatibility if needed.
    /// Context: Frame loop.
    /// Justification: Reserved for future update logic.
    /// </summary>
    private void Update()
    {
        // Reserved for future update logic if necessary.
    }
    

    /// <summary>
    /// Description: Programmatically calculates a quantized gradient of 16 colors consisting of grayscale tones and vibrant HSV rainbow hues.
    /// Context: Can be run from Inspector button.
    /// Justification: Generates a mathematically perfect spectrum for the user.
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
    /// Description: Automatically retrieves all child custom shape buttons and assigns them their respective colors.
    /// Context: Invoked by Start.
    /// Justification: Sets up click handlers to communicate with LobbyCustomizationUI.
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

            // Clean previous listeners and add dynamic event dispatching
            string capturedHex = hexColor; // Prevent closure variable capture issue in C# loops
            Color capturedColor = buttonColor;
            currentButton.onClick.RemoveAllListeners();
            currentButton.onClick.AddListener(() =>
            {
                // 1. Dispatch generic observer events for decoupled listeners (TextureEditor, etc.)
                _onColorSelected?.Invoke(capturedColor);
                _onHexColorSelected?.Invoke(capturedHex);

                // 2. Fallback to Lobby Customization if present
                if (_lobbyCustomizationUI != null)
                {
                    _lobbyCustomizationUI.SetPlayerColorHex(capturedHex);
                }
                else
                {
                    LobbyCustomizationUI dynamicLobbyUI = FindAnyObjectByType<LobbyCustomizationUI>();
                    if (dynamicLobbyUI != null)
                    {
                        dynamicLobbyUI.SetPlayerColorHex(capturedHex);
                    }
                }
            });
        }

        if (_enableDebugLogs) Debug.Log("[UIColorsPalettes] Color palette successfully initialized.");
    }
}

