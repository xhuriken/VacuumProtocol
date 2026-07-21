using UnityEngine;

namespace VacuumProtocol.UI.TextureEditor
{
    /// <summary>
    /// Description: Enumeration of all available painting tools in the texture editor.
    /// Context: Used by TexturePainter and TextureEditorPanelUI to switch current tool state.
    /// Justification: Centralizes tool types to maintain a clean KISS architecture.
    /// </summary>
    public enum PainterTool
    {
        Pencil,
        SoftBrush,
        Airbrush,
        Eraser,
        FloodFill,
        Eyedropper
    }

    /// <summary>
    /// Description: Container holding active brush attributes (tool type, color, radius, hardness, spray density).
    /// Context: Shared between the UI panel and the core TexturePainter engine.
    /// Justification: Grouping brush properties simplifies state passing across components.
    /// </summary>
    [System.Serializable]
    public class BrushSettings
    {
        [Tooltip("Role: The active tool type.\nUse Case: Painting logic.\nJustification: Determines pixel manipulation algorithm.")]
        [SerializeField] private PainterTool _tool = PainterTool.Pencil;

        [Tooltip("Role: Active painting color.\nUse Case: Color application.\nJustification: Defines RGBA applied to canvas.")]
        [SerializeField] private Color _color = Color.black;

        [Tooltip("Role: Brush radius in pixel units.\nUse Case: Stamp sizing.\nJustification: Controls brush diameter on canvas.")]
        [SerializeField] private int _radius = 5;

        [Tooltip("Role: Hardness factor from 0.0 (soft falloff) to 1.0 (hard edge).\nUse Case: Soft brush blending.\nJustification: Controls alpha falloff curve.")]
        [SerializeField] private float _hardness = 0.8f;

        [Tooltip("Role: Spray density for airbrush tool (0.0 to 1.0).\nUse Case: Airbrush randomness.\nJustification: Controls particle density per stamp.")]
        [SerializeField] private float _sprayDensity = 0.3f;

        [Tooltip("Role: Brush opacity from 0.0 (fully transparent) to 1.0 (fully opaque).\nUse Case: Paint blending.\nJustification: Controls blending strength of painted stamps.")]
        [SerializeField] private float _opacity = 1f;

        /// <summary>
        /// Description: Brush opacity from 0.0 to 1.0.
        /// Context: Property read by painter.
        /// Justification: Exposes opacity control safely.
        /// </summary>
        public float Opacity
        {
            get => _opacity;
            set => _opacity = Mathf.Clamp01(value);
        }

        /// <summary>
        /// Description: Active tool mode.
        /// Context: Property read by painter.
        /// Justification: Exposes tool selection safely.
        /// </summary>
        public PainterTool Tool
        {
            get => _tool;
            set => _tool = value;
        }

        /// <summary>
        /// Description: Active painting color.
        /// Context: Property read by painter and UI.
        /// Justification: Exposes color selection safely.
        /// </summary>
        public Color Color
        {
            get => _color;
            set => _color = value;
        }

        /// <summary>
        /// Description: Active radius in canvas pixels.
        /// Context: Property read by painter and cursor visualizer.
        /// Justification: Exposes brush size safely.
        /// </summary>
        public int Radius
        {
            get => _radius;
            set => _radius = Mathf.Clamp(value, 0, 64);
        }

        /// <summary>
        /// Description: Active hardness factor (0 to 1).
        /// Context: Property read by painter.
        /// Justification: Exposes edge softness safely.
        /// </summary>
        public float Hardness
        {
            get => _hardness;
            set => _hardness = Mathf.Clamp01(value);
        }

        /// <summary>
        /// Description: Spray density factor (0 to 1).
        /// Context: Property read by airbrush tool.
        /// Justification: Exposes spray particle probability safely.
        /// </summary>
        public float SprayDensity
        {
            get => _sprayDensity;
            set => _sprayDensity = Mathf.Clamp01(value);
        }
    }
}
