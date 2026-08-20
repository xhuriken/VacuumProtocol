using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Description: UI Script to be attached to the Lobby Canvas. Handles sending UI events to the local player's PlayerCustomization script.
/// Context: Runs in the Lobby scene.
/// Justification: Bridges standard UGUI events (buttons, text fields) to network commands, allowing offline preview dummies.
/// </summary>
public class LobbyCustomizationUI : MonoBehaviour
{
    [Header("Scene References")]
    [Tooltip("Role: The local dummy player in the scene.\nUse Case: Offline previews.\nJustification: Allows customizing without needing an active network connection.")]
    public PlayerCustomization PreviewPlayer;

    [Header("Debug")]
    [Tooltip("Role: Toggle for debug logs.\nUse Case: Troubleshooting.\nJustification: Customization UI can spam console during rapid color switching.")]
    public bool EnableDebugLogs = true;

    [Header("UI Tabs & Editor Integration")]
    public CustomizationMenuTabs MenuTabs;
    public TextureEditorPanelUI TextureEditor;
    public int AppearanceTabIndex = 0;

    [Tooltip("Role: Elements to hide when the Texture Editor is open.\nUse Case: Fullscreen drawing.")]
    public GameObject[] ElementsToHideWhenDrawing;

    [Header("Custom Eyes UI")]
    public GameObject CustomEyeButtonPrefab;
    public Transform CustomEyeListContainer;
    [Tooltip("Role: Base textures always available.\nUse Case: Default eyes like the Circle.")]
    public Texture2D[] DefaultEyeTextures;
    private List<GameObject> _spawnedEyeButtons = new List<GameObject>();

    private void Start()
    {
        if (TextureEditor != null)
        {
            TextureEditor.OnTextureSaved += HandleTextureSaved;
            TextureEditor.OnEditorClosed += CloseTextureEditor;
        }
        RefreshCustomEyeButtons();
    }

    private void OnDestroy()
    {
        if (TextureEditor != null)
        {
            TextureEditor.OnTextureSaved -= HandleTextureSaved;
            TextureEditor.OnEditorClosed -= CloseTextureEditor;
        }
    }

    private void HandleTextureSaved(Texture2D newTexture)
    {
        CustomEyeTextureManager.SaveCustomEyeTexture(newTexture);
        RefreshCustomEyeButtons();

        CloseTextureEditor();

        SetLocalEyeTexture(newTexture);
    }

    public void OpenTextureEditor()
    {
        if (ElementsToHideWhenDrawing != null)
        {
            foreach (var element in ElementsToHideWhenDrawing)
            {
                if (element != null)
                {
                    if (EnableDebugLogs) Debug.Log($"[LobbyUI] Désactivation de : {element.name}");
                    element.SetActive(false);
                }
            }
        }
        if (TextureEditor != null) TextureEditor.gameObject.SetActive(true);
    }

    public void CloseTextureEditor()
    {
        if (ElementsToHideWhenDrawing != null)
        {
            foreach (var element in ElementsToHideWhenDrawing)
            {
                if (element != null)
                {
                    if (EnableDebugLogs) Debug.Log($"[LobbyUI] Réactivation de : {element.name}");
                    element.SetActive(true);
                }
            }
        }
        if (TextureEditor != null) TextureEditor.gameObject.SetActive(false);

        if (MenuTabs != null)
        {
            MenuTabs.OpenTab(AppearanceTabIndex);
        }
    }

    public void RefreshCustomEyeButtons()
    {
        if (CustomEyeButtonPrefab == null || CustomEyeListContainer == null) return;

        // Clear old buttons
        foreach (var btn in _spawnedEyeButtons) Destroy(btn);
        _spawnedEyeButtons.Clear();

        // 1. Instantiate Default Textures first
        if (DefaultEyeTextures != null)
        {
            foreach (var tex in DefaultEyeTextures)
            {
                if (tex != null) SpawnEyeButton(tex);
            }
        }

        // 2. Instantiate Custom Saved Textures
        List<Texture2D> savedTextures = CustomEyeTextureManager.LoadAllCustomEyeTextures();
        foreach (var tex in savedTextures)
        {
            if (tex != null) SpawnEyeButton(tex);
        }
    }

    private void SpawnEyeButton(Texture2D tex)
    {
        Texture2D capturedTex = tex;
        GameObject newBtn = Instantiate(CustomEyeButtonPrefab, CustomEyeListContainer);
        _spawnedEyeButtons.Add(newBtn);

        // Set preview image
        RawImage rawImg = newBtn.GetComponentInChildren<RawImage>();
        if (rawImg != null) rawImg.texture = capturedTex;

        // If the user uses a standard Image with sprite, it requires conversion, so RawImage is preferred for dynamic textures.

        Button btnComp = newBtn.GetComponent<Button>();
        if (btnComp != null)
        {
            btnComp.onClick.RemoveAllListeners();
            btnComp.onClick.AddListener(() => SetLocalEyeTexture(capturedTex));
        }
        else
        {
            UICustomButtonBase customBtnComp = newBtn.GetComponent<UICustomButtonBase>();
            if (customBtnComp != null)
            {
                customBtnComp.onClick.RemoveAllListeners();
                customBtnComp.onClick.AddListener(() => SetLocalEyeTexture(capturedTex));
            }
        }
    }

    public void SetLocalEyeTexture(Texture2D tex)
    {
        if (EnableDebugLogs) Debug.Log($"[LobbyUI] Setting local eye texture: {(tex != null ? tex.name : "null")}");
        
        // Save the path for the multiplayer NetworkTextureTransfer script
        if (tex != null && !string.IsNullOrEmpty(tex.name))
        {
            string path = System.IO.Path.Combine(CustomEyeTextureManager.GetFolderPath(), tex.name + ".png");
            if (System.IO.File.Exists(path))
            {
                PlayerPrefs.SetString("SelectedEyeTexture", path);
            }
            else
            {
                PlayerPrefs.SetString("SelectedEyeTexture", ""); // It's a default Unity texture, handled differently
            }
            PlayerPrefs.Save();
        }

        // Applied locally only (network sync of custom image bytes to be implemented later)
        var targetPlayer = GetTargetPlayer();
        if (targetPlayer != null)
        {
            targetPlayer.ApplyLocalEyeTexture(tex);
        }
    }

    // ----------------------------------------------------
    // PUBLIC METHODS TO LINK IN THE UNITY INSPECTOR (On Click Events)
    // ----------------------------------------------------

    /// <summary>
    /// Description: Call this from a UI Button (On Click) and pass the preset index you want.
    /// Context: Unity UI Event.
    /// Justification: Standard way to apply a visual preset (Color + Texture) to the customization system.
    /// </summary>
    public void SetPlayerVisualPreset(int presetIndex)
    {
        if (EnableDebugLogs) Debug.Log($"[LobbyUI] SetPlayerVisualPreset called with index {presetIndex}");

        // SAVE IT LOCALLY!
        PlayerPrefs.SetInt("PlayerVisualIndex", presetIndex);
        PlayerPrefs.Save();

        var targetPlayer = GetTargetPlayer();
        if (targetPlayer != null)
        {
            if (EnableDebugLogs) Debug.Log($"[LobbyUI] Target player found. Requesting visual preset change...");
            targetPlayer.RequestVisualChange(presetIndex);
        }
        else if (EnableDebugLogs) Debug.LogWarning("[LobbyUI] Target player is null! Visual change aborted.");
    }

    // (Legacy) hex parsing removed because the new system uses Preset Indices instead.

    /// <summary>
    /// Description: Call this from a UI Button (On Click) and pass the integer value of the Enum.
    /// Context: Unity UI Event.
    /// Justification: Binds the UI audio pitch buttons to the MusicalNote enum.
    /// </summary>
    public void SetPlayerNote(int noteIndex)
    {
        if (EnableDebugLogs) Debug.Log($"[LobbyUI] SetPlayerNote called with index {noteIndex}");
        // SAVE IT LOCALLY!
        PlayerPrefs.SetInt("PlayerNoteIndex", noteIndex);
        PlayerPrefs.Save();

        var targetPlayer = GetTargetPlayer();
        if (targetPlayer != null)
        {
            MusicalNote newNote = (MusicalNote)noteIndex;
            if (EnableDebugLogs) Debug.Log($"[LobbyUI] Target player found. Requesting note change to {newNote}...");
            targetPlayer.RequestNoteChange(newNote);
        }
        else if (EnableDebugLogs) Debug.LogWarning("[LobbyUI] Target player is null! Note change aborted.");
    }

    /// <summary>
    /// Description: Call this from an EventTrigger (Pointer Down) on a UI Button to preview the vacuum sound.
    /// Context: Unity UI Event.
    /// Justification: Tests the chosen audio settings dynamically before entering a game.
    /// </summary>
    public void StartPreviewVacuum()
    {
        if (EnableDebugLogs) Debug.Log("[LobbyUI] StartPreviewVacuum called (Pointer Down)");
        var targetPlayer = GetTargetPlayer();
        if (targetPlayer != null)
        {
            targetPlayer.RequestVacuumTest(true);
        }
        else if (EnableDebugLogs) Debug.LogWarning("[LobbyUI] Target player is null! Vacuum preview aborted.");
    }

    /// <summary>
    /// Description: Call this from an EventTrigger (Pointer Up) on the same UI Button.
    /// Context: Unity UI Event.
    /// Justification: Stops the preview sound once the button is released.
    /// </summary>
    public void StopPreviewVacuum()
    {
        if (EnableDebugLogs) Debug.Log("[LobbyUI] StopPreviewVacuum called (Pointer Up)");
        var targetPlayer = GetTargetPlayer();
        if (targetPlayer != null)
        {
            targetPlayer.RequestVacuumTest(false);
        }
        else if (EnableDebugLogs) Debug.LogWarning("[LobbyUI] Target player is null! Vacuum stop aborted.");
    }

    // ----------------------------------------------------
    // INTERNAL LOGIC
    // ----------------------------------------------------

    private bool _isHoldingBothClicks = false;

    private void Update()
    {
        // Allow the user to test the vacuum simply by holding left & right click in the lobby using the New Input System
        if (Mouse.current != null && Mouse.current.leftButton.isPressed && Mouse.current.rightButton.isPressed)
        {
            if (!_isHoldingBothClicks)
            {
                _isHoldingBothClicks = true;
                StartPreviewVacuum();
            }
        }
        else
        {
            if (_isHoldingBothClicks)
            {
                _isHoldingBothClicks = false;
                StopPreviewVacuum();
            }
        }
    }

    private PlayerCustomization GetTargetPlayer()
    {
        // 1. If you manually linked the dummy player in the scene, use it!
        if (PreviewPlayer != null)
        {
            if (EnableDebugLogs) Debug.Log("[LobbyUI] GetTargetPlayer returned the manually linked PreviewPlayer.");
            return PreviewPlayer;
        }

        // 2. Otherwise, look for the networked local player
        if (NetworkClient.localPlayer != null)
        {
            if (EnableDebugLogs) Debug.Log("[LobbyUI] GetTargetPlayer returned NetworkClient.localPlayer.");
            return NetworkClient.localPlayer.GetComponent<PlayerCustomization>();
        }

        // 3. Fallback: just find any player in the scene
        Debug.LogWarning("[LobbyUI] Local player not found yet. Make sure you are spawned in the lobby, biatch");
        var fallback = FindObjectOfType<PlayerCustomization>();
        if (fallback != null && EnableDebugLogs) Debug.Log("[LobbyUI] GetTargetPlayer returned a fallback player found in the scene.");
        else if (fallback == null && EnableDebugLogs) Debug.LogWarning("[LobbyUI] FindObjectOfType failed. No PlayerCustomization found in the scene.");

        return fallback;
    }
}

