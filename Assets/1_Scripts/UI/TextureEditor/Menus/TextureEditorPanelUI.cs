using System;
using UnityEngine;
using UnityEngine.UI;

namespace VacuumProtocol.UI.TextureEditor
{
    /// <summary>
    /// Description: Lobby Texture Editor panel controller managing UI tools, color palettes, brush size slider, Undo/Redo buttons, and texture export callbacks.
    /// Context: Main panel component for the Lobby Texture Studio feature.
    /// Justification: Orchestrates drawing UI controls in accordance with project UI design standards (Shapes vectors, custom buttons, DOTween).
    /// </summary>
    public class TextureEditorPanelUI : MonoBehaviour
    {
        [Header("Canvas Presenter Reference")]
        [Tooltip("Role: The active canvas presenter component.\nUse Case: Painting input.\nJustification: Receives tool commands and draws pixels.")]
        [SerializeField] private TexturePainterUI _painterUI;

        [Header("Color Palette Controls")]
        [Tooltip("Role: Optional palette manager component.\nUse Case: Color choices.\nJustification: Integrates project UI color palettes.")]
        [SerializeField] private UIColorsPalettes _colorPaletteManager;

        [Tooltip("Role: Array of color selection buttons.\nUse Case: Preset colors.\nJustification: Allows picking colors from custom ColorButtonUI instances.")]
        [SerializeField] private ColorButtonUI[] _colorButtons;

        [Header("Brush Size Controls")]
        [Tooltip("Role: Custom shapes slider for adjusting brush size.\nUse Case: Size selection.\nJustification: Integrates project UICustomSlider.")]
        [SerializeField] private UICustomSlider _brushSizeSlider;

        [Header("Brush Opacity Controls")]
        [Tooltip("Role: Custom shapes slider for adjusting brush opacity.\nUse Case: Opacity selection.\nJustification: Integrates project UICustomSlider.")]
        [SerializeField] private UICustomSlider _brushOpacitySlider;

        [Header("Brush Density Controls")]
        [Tooltip("Role: Custom shapes slider for adjusting airbrush spray density.\nUse Case: Density selection.\nJustification: Integrates project UICustomSlider.")]
        [SerializeField] private UICustomSlider _brushDensitySlider;

        [System.Serializable]
        private class SavedToolSettings
        {
            public int Radius = 5;
            public float Opacity = 1f;
            public float Density = 0.3f;
        }

        private System.Collections.Generic.Dictionary<PainterTool, SavedToolSettings> _savedToolSettings = 
            new System.Collections.Generic.Dictionary<PainterTool, SavedToolSettings>();

        [Header("Tool Buttons")]
        [Tooltip("Role: Pencil tool selection button.\nUse Case: Hard brush mode.")]
        [SerializeField] private UICustomButtonBase _pencilButton;

        [Tooltip("Role: Soft brush selection button.\nUse Case: Soft falloff mode.")]
        [SerializeField] private UICustomButtonBase _softBrushButton;

        [Tooltip("Role: Airbrush selection button.\nUse Case: Spray mode.")]
        [SerializeField] private UICustomButtonBase _airbrushButton;

        [Tooltip("Role: Eraser selection button.\nUse Case: Erase mode.")]
        [SerializeField] private UICustomButtonBase _eraserButton;

        [Tooltip("Role: Flood fill selection button.\nUse Case: Bucket mode.")]
        [SerializeField] private UICustomButtonBase _floodFillButton;

        [Tooltip("Role: Eyedropper selection button.\nUse Case: Color picker mode.")]
        [SerializeField] private UICustomButtonBase _eyedropperButton;

        [Header("Action Buttons")]
        [Tooltip("Role: Undo action button.\nUse Case: Step revert.")]
        [SerializeField] private UICustomButtonBase _undoButton;

        [Tooltip("Role: Redo action button.\nUse Case: Step re-apply.")]
        [SerializeField] private UICustomButtonBase _redoButton;

        [Tooltip("Role: Clear canvas action button.\nUse Case: Canvas wipe.")]
        [SerializeField] private UICustomButtonBase _clearButton;

        [Tooltip("Role: Apply / Save texture button.\nUse Case: Texture export.")]
        [SerializeField] private UICustomButtonBase _saveButton;

        /// <summary>
        /// Description: Event emitted when player saves/applies their drawn texture.
        /// Context: Subscribed to by Lobby Customization manager to assign texture to player avatar/pupils/skin.
        /// Justification: Decouples texture creation from destination application.
        /// </summary>
        public event Action<Texture2D> OnTextureSaved;

        private void Start()
        {
            InitializeEditor();
        }

        private void OnDestroy()
        {
            UnbindEvents();
        }

        /// <summary>
        /// Description: Initializes the editor controls and default canvas state.
        /// Context: Panel start or setup.
        /// Justification: Hooks UI callbacks and sets initial brush parameters.
        /// </summary>
        public void InitializeEditor(int defaultWidth = 128, int defaultHeight = 128)
        {
            if (_painterUI == null)
            {
                _painterUI = GetComponentInChildren<TexturePainterUI>();
            }

            // Load all settings first from PlayerPrefs
            LoadAllBrushSettings();

            if (_painterUI != null)
            {
                _painterUI.InitializeCanvas(defaultWidth, defaultHeight, Color.white);
                _painterUI.Painter.OnColorPicked += HandleColorPickedFromCanvas;
                _painterUI.Painter.OnCanvasUpdated += RefreshActionButtonsState;
            }

            BindEvents();

            // Set initial tool to Pencil, which will automatically configure initial brush settings and sliders
            SetTool(PainterTool.Pencil);

            RefreshActionButtonsState();
        }

        /// <summary>
        /// Description: Opens editor with a specified existing texture asset to edit dynamically.
        /// Context: Editing player eyes, flag, or skin textures.
        /// Justification: Supports loading arbitrary size textures into painter.
        /// </summary>
        public void OpenWithTexture(Texture2D existingTexture)
        {
            if (_painterUI != null && existingTexture != null)
            {
                _painterUI.LoadTexture(existingTexture);
            }
            RefreshActionButtonsState();
        }

        private void BindEvents()
        {
            // Tool Buttons
            if (_pencilButton != null) _pencilButton.onClick.AddListener(() => SetTool(PainterTool.Pencil));
            if (_softBrushButton != null) _softBrushButton.onClick.AddListener(() => SetTool(PainterTool.SoftBrush));
            if (_airbrushButton != null) _airbrushButton.onClick.AddListener(() => SetTool(PainterTool.Airbrush));
            if (_eraserButton != null) _eraserButton.onClick.AddListener(() => SetTool(PainterTool.Eraser));
            if (_floodFillButton != null) _floodFillButton.onClick.AddListener(() => SetTool(PainterTool.FloodFill));
            if (_eyedropperButton != null) _eyedropperButton.onClick.AddListener(() => SetTool(PainterTool.Eyedropper));

            // Action Buttons
            if (_undoButton != null) _undoButton.onClick.AddListener(OnUndoClicked);
            if (_redoButton != null) _redoButton.onClick.AddListener(OnRedoClicked);
            if (_clearButton != null) _clearButton.onClick.AddListener(OnClearClicked);
            if (_saveButton != null) _saveButton.onClick.AddListener(OnSaveClicked);

            // Sliders Value Changed & Save triggers (PointerUp)
            if (_brushSizeSlider != null)
            {
                _brushSizeSlider.onValueChanged.AddListener(OnBrushSizeSliderChanged);
                _brushSizeSlider.onPointerUp.AddListener(SaveActiveBrushSettings);
            }

            if (_brushOpacitySlider != null)
            {
                _brushOpacitySlider.onValueChanged.AddListener(OnBrushOpacitySliderChanged);
                _brushOpacitySlider.onPointerUp.AddListener(SaveActiveBrushSettings);
            }

            if (_brushDensitySlider != null)
            {
                _brushDensitySlider.onValueChanged.AddListener(OnBrushDensitySliderChanged);
                _brushDensitySlider.onPointerUp.AddListener(SaveActiveBrushSettings);
            }

            // Color Palette Manager Event Listener (Decoupled Observer Pattern)
            if (_colorPaletteManager != null)
            {
                _colorPaletteManager.OnColorSelected.AddListener(SetColor);
            }

            // Color Palette Buttons
            if (_colorButtons != null)
            {
                foreach (ColorButtonUI btn in _colorButtons)
                {
                    if (btn != null)
                    {
                        Color capturedColor = btn.ButtonColor;
                        btn.onClick.AddListener(() => SetColor(capturedColor));
                    }
                }
            }
        }

        private void UnbindEvents()
        {
            if (_painterUI != null && _painterUI.Painter != null)
            {
                _painterUI.Painter.OnColorPicked -= HandleColorPickedFromCanvas;
                _painterUI.Painter.OnCanvasUpdated -= RefreshActionButtonsState;
            }

            if (_brushSizeSlider != null)
            {
                _brushSizeSlider.onValueChanged.RemoveListener(OnBrushSizeSliderChanged);
                _brushSizeSlider.onPointerUp.RemoveListener(SaveActiveBrushSettings);
            }

            if (_brushOpacitySlider != null)
            {
                _brushOpacitySlider.onValueChanged.RemoveListener(OnBrushOpacitySliderChanged);
                _brushOpacitySlider.onPointerUp.RemoveListener(SaveActiveBrushSettings);
            }

            if (_brushDensitySlider != null)
            {
                _brushDensitySlider.onValueChanged.RemoveListener(OnBrushDensitySliderChanged);
                _brushDensitySlider.onPointerUp.RemoveListener(SaveActiveBrushSettings);
            }

            if (_colorPaletteManager != null)
            {
                _colorPaletteManager.OnColorSelected.RemoveListener(SetColor);
            }
        }

        #region UI Handlers

        public void SetTool(PainterTool tool)
        {
            if (_painterUI != null && _painterUI.Painter != null)
            {
                _painterUI.Painter.BrushSettings.Tool = tool;
                UpdateToolButtonsState(tool);

                // Toggle visibility of sliders based on active tool requirements
                if (_brushSizeSlider != null)
                {
                    // Size is only relevant for Pencil, SoftBrush, Airbrush, and Eraser
                    bool needsSize = tool == PainterTool.Pencil || 
                                     tool == PainterTool.SoftBrush || 
                                     tool == PainterTool.Airbrush || 
                                     tool == PainterTool.Eraser;
                    _brushSizeSlider.gameObject.SetActive(needsSize);
                }

                if (_brushOpacitySlider != null)
                {
                    // Opacity is relevant for drawing tools and flood fill, but NOT eyedropper
                    bool needsOpacity = tool != PainterTool.Eyedropper;
                    _brushOpacitySlider.gameObject.SetActive(needsOpacity);
                }

                if (_brushDensitySlider != null)
                {
                    // Density is only relevant for Airbrush
                    _brushDensitySlider.gameObject.SetActive(tool == PainterTool.Airbrush);
                }

                // Apply saved settings to brush and sliders
                if (_savedToolSettings.TryGetValue(tool, out SavedToolSettings settings))
                {
                    _painterUI.Painter.BrushSettings.Radius = settings.Radius;
                    _painterUI.Painter.BrushSettings.Opacity = settings.Opacity;
                    _painterUI.Painter.BrushSettings.SprayDensity = settings.Density;

                    // Set slider values without triggering valueChanged event loops
                    if (_brushSizeSlider != null)
                    {
                        float sizePct = Mathf.Clamp01(settings.Radius / 32f);
                        _brushSizeSlider.SetValue(sizePct, notify: false, animate: false);
                    }
                    if (_brushOpacitySlider != null)
                    {
                        _brushOpacitySlider.SetValue(settings.Opacity, notify: false, animate: false);
                    }
                    if (_brushDensitySlider != null)
                    {
                        _brushDensitySlider.SetValue(settings.Density, notify: false, animate: false);
                    }
                }

                Debug.Log($"[TextureEditorPanelUI] Active tool switched to: {tool}");
            }
        }

        private void UpdateToolButtonsState(PainterTool activeTool)
        {
            if (_pencilButton != null) _pencilButton.Interactable = (activeTool != PainterTool.Pencil);
            if (_softBrushButton != null) _softBrushButton.Interactable = (activeTool != PainterTool.SoftBrush);
            if (_airbrushButton != null) _airbrushButton.Interactable = (activeTool != PainterTool.Airbrush);
            if (_eraserButton != null) _eraserButton.Interactable = (activeTool != PainterTool.Eraser);
            if (_floodFillButton != null) _floodFillButton.Interactable = (activeTool != PainterTool.FloodFill);
            if (_eyedropperButton != null) _eyedropperButton.Interactable = (activeTool != PainterTool.Eyedropper);
        }

        public void SetColor(Color color)
        {
            if (_painterUI != null && _painterUI.Painter != null)
            {
                _painterUI.Painter.BrushSettings.Color = color;
                
                // If in eraser or eyedropper, switch back to pencil on color pick
                if (_painterUI.Painter.BrushSettings.Tool == PainterTool.Eraser || _painterUI.Painter.BrushSettings.Tool == PainterTool.Eyedropper)
                {
                    SetTool(PainterTool.Pencil);
                }
            }
        }

        private void OnBrushSizeSliderChanged(float value)
        {
            if (_painterUI != null && _painterUI.Painter != null)
            {
                // Map 0..1 slider value to 0..32 pixel radius (0 = 1x1 pixel stroke)
                int radius = Mathf.RoundToInt(Mathf.Lerp(0f, 32f, value));
                _painterUI.Painter.BrushSettings.Radius = radius;
            }
        }

        private void OnBrushOpacitySliderChanged(float value)
        {
            if (_painterUI != null && _painterUI.Painter != null)
            {
                _painterUI.Painter.BrushSettings.Opacity = value;
            }
        }

        private void OnBrushDensitySliderChanged(float value)
        {
            if (_painterUI != null && _painterUI.Painter != null)
            {
                _painterUI.Painter.BrushSettings.SprayDensity = value;
            }
        }

        private void OnUndoClicked()
        {
            if (_painterUI != null && _painterUI.Painter != null)
            {
                _painterUI.Painter.Undo();
            }
        }

        private void OnRedoClicked()
        {
            if (_painterUI != null && _painterUI.Painter != null)
            {
                _painterUI.Painter.Redo();
            }
        }

        private void OnClearClicked()
        {
            if (_painterUI != null && _painterUI.Painter != null)
            {
                _painterUI.Painter.ClearCanvas(Color.white);
            }
        }

        private void OnSaveClicked()
        {
            if (_painterUI != null && _painterUI.Painter != null)
            {
                Texture2D result = _painterUI.Painter.TargetTexture;
                Debug.Log($"[TextureEditorPanelUI] Texture saved ({result.width}x{result.height})!");
                OnTextureSaved?.Invoke(result);
            }
        }

        private void HandleColorPickedFromCanvas(Color color)
        {
            Debug.Log($"[TextureEditorPanelUI] Eyedropper picked color: {color}");
        }

        private void RefreshActionButtonsState()
        {
            if (_painterUI == null || _painterUI.Painter == null)
            {
                return;
            }

            TextureUndoSystem undoSystem = _painterUI.Painter.UndoSystem;
            if (_undoButton != null)
            {
                _undoButton.Interactable = undoSystem.CanUndo;
            }
            if (_redoButton != null)
            {
                _redoButton.Interactable = undoSystem.CanRedo;
            }
        }

        private void LoadAllBrushSettings()
        {
            foreach (PainterTool tool in Enum.GetValues(typeof(PainterTool)))
            {
                SavedToolSettings settings = new SavedToolSettings();

                string radiusKey = $"TextureEditor_Brush_Radius_{(int)tool}";
                string opacityKey = $"TextureEditor_Brush_Opacity_{(int)tool}";
                string densityKey = $"TextureEditor_Brush_Density_{(int)tool}";

                int defaultRadius = 5;
                float defaultOpacity = 1f;
                float defaultDensity = 0.3f;

                if (tool == PainterTool.Eraser)
                {
                    defaultRadius = 8;
                }
                else if (tool == PainterTool.SoftBrush)
                {
                    defaultRadius = 12;
                    defaultOpacity = 0.5f;
                }
                else if (tool == PainterTool.FloodFill || tool == PainterTool.Eyedropper)
                {
                    defaultRadius = 0; // fixed single-pixel size for UI ring
                }

                settings.Radius = PlayerPrefs.GetInt(radiusKey, defaultRadius);
                settings.Opacity = PlayerPrefs.GetFloat(opacityKey, defaultOpacity);
                settings.Density = PlayerPrefs.GetFloat(densityKey, defaultDensity);

                _savedToolSettings[tool] = settings;
            }
        }

        private void SaveActiveBrushSettings()
        {
            if (_painterUI == null || _painterUI.Painter == null) return;

            PainterTool activeTool = _painterUI.Painter.BrushSettings.Tool;
            if (!_savedToolSettings.TryGetValue(activeTool, out SavedToolSettings settings))
            {
                settings = new SavedToolSettings();
                _savedToolSettings[activeTool] = settings;
            }

            settings.Radius = _painterUI.Painter.BrushSettings.Radius;
            settings.Opacity = _painterUI.Painter.BrushSettings.Opacity;
            settings.Density = _painterUI.Painter.BrushSettings.SprayDensity;

            string radiusKey = $"TextureEditor_Brush_Radius_{(int)activeTool}";
            string opacityKey = $"TextureEditor_Brush_Opacity_{(int)activeTool}";
            string densityKey = $"TextureEditor_Brush_Density_{(int)activeTool}";

            PlayerPrefs.SetInt(radiusKey, settings.Radius);
            PlayerPrefs.SetFloat(opacityKey, settings.Opacity);
            PlayerPrefs.SetFloat(densityKey, settings.Density);
            PlayerPrefs.Save();

            Debug.Log($"[TextureEditorPanelUI] Saved active settings for {activeTool}: Radius={settings.Radius}, Opacity={settings.Opacity}, Density={settings.Density}");
        }

        #endregion
    }
}
