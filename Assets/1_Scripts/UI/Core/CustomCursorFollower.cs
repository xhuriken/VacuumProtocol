using UnityEngine;
using Shapes;

/// <summary>
/// Description: Placed on a local custom cursor UI element in any scene. Automatically tracks the global MouseManager's position and visibility state.
/// Context: Attached to a UI image/rect transform that acts as a cursor.
/// Justification: Adapts correctly to the local Canvas scaler, camera, and sorting settings. Serves as SSOT for custom cursor & brush ring visuals.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class CustomCursorFollower : MonoBehaviour
{
    /// <summary>
    /// Description: Static singleton instance of the active CustomCursorFollower.
    /// Context: Global access point for UI systems.
    /// Justification: Allows TexturePainterUI to register brush cursor modes cleanly without duplicate mouse followers.
    /// </summary>
    public static CustomCursorFollower Instance { get; private set; }

    [Header("Visual Container References")]
    [Tooltip("Role: The standard UI cursor graphics container.\nUse Case: Default menu navigation.\nJustification: Displayed when not in brush painting mode.")]
    [SerializeField] private GameObject _defaultCursorVisual;

    [Tooltip("Role: The canvas brush ring indicator container.\nUse Case: Texture editing.\nJustification: Displayed when hovering active drawing canvas.")]
    [SerializeField] private GameObject _brushCursorVisual;

    [Header("Shapes Vector References")]
    [Tooltip("Role: Inner preview disc for active brush color.\nUse Case: Visual feedback.\nJustification: Color matched to current paint palette selection.")]
    [SerializeField] private Disc _brushInnerDisc;

    [Tooltip("Role: Outer ring outline for brush diameter bounds.\nUse Case: Size feedback.\nJustification: Scales dynamically to match brush radius.")]
    [SerializeField] private Disc _brushOutlineRing;

    private RectTransform _rectTransform;
    private Canvas _canvas;
    private bool _isInBrushMode;

    /// <summary>
    /// Description: Unity Awake callback. Establishes singleton and caches necessary components.
    /// Context: Initialization.
    /// Justification: Required to correctly position the cursor inside the canvas hierarchy.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _rectTransform = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();

        ResetCursorMode();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Description: Unity LateUpdate callback. Updates cursor position and visibility.
    /// Context: Frame loop, runs after all standard Updates.
    /// Justification: Ensures the cursor perfectly matches the final mouse position without jitter.
    /// </summary>
    private void LateUpdate()
    {
        if (MouseManager.Instance == null)
        {
            return;
        }

        // 1. Sync visibility with global MouseManager context state (locked/gameplay vs UI)
        bool shouldBeVisible = MouseManager.Instance.ShouldShowCursor;
        if (gameObject.activeSelf != shouldBeVisible)
        {
            gameObject.SetActive(shouldBeVisible);
        }

        if (!shouldBeVisible)
        {
            return;
        }

        // 2. Position the custom cursor accurately within its local Canvas space
        PositionCursor();
    }

    /// <summary>
    /// Description: Enables or updates the dynamic brush ring cursor mode (SSOT).
    /// Context: Called by TexturePainterUI on canvas enter/drag.
    /// Justification: Adapts cursor visuals to show active brush diameter and color.
    /// </summary>
    /// <param name="radiusPixels">Brush radius in UI screen space pixels.</param>
    /// <param name="brushColor">Active brush color.</param>
    /// <param name="isEraser">True if eraser mode is active.</param>
    public void SetBrushCursorMode(bool enabled, float radiusPixels = 10f, Color? brushColor = null, bool isEraser = false)
    {
        _isInBrushMode = enabled;

        if (_defaultCursorVisual != null)
        {
            _defaultCursorVisual.SetActive(!enabled);
        }

        if (_brushCursorVisual != null)
        {
            _brushCursorVisual.SetActive(enabled);
        }

        if (!enabled)
        {
            return;
        }

        Color activeColor = brushColor ?? Color.black;

        if (_brushInnerDisc != null)
        {
            _brushInnerDisc.Radius = radiusPixels;
            _brushInnerDisc.Color = isEraser ? new Color(1f, 1f, 1f, 0.2f) : activeColor;
        }

        if (_brushOutlineRing != null)
        {
            _brushOutlineRing.Radius = radiusPixels + 1.5f;
            _brushOutlineRing.Color = isEraser ? Color.red : Color.white;
        }
    }

    /// <summary>
    /// Description: Resets cursor visual state to standard default UI cursor.
    /// Context: Called on Canvas exit or disable.
    /// Justification: Restores default mouse cursor visual cleanly.
    /// </summary>
    public void ResetCursorMode()
    {
        SetBrushCursorMode(false);
    }

    /// <summary>
    /// Description: Calculates the correct screen/world position for the cursor based on Canvas render mode.
    /// Context: Called every frame while visible.
    /// Justification: Solves the discrepancy between ScreenSpaceOverlay and ScreenSpaceCamera positioning.
    /// </summary>
    private void PositionCursor()
    {
        if (_canvas == null)
        {
            transform.position = MouseManager.Instance.MousePosition;
            return;
        }

        if (_canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // In Overlay mode, screen coordinates map directly to world coordinates
            transform.position = MouseManager.Instance.MousePosition;
        }
        else if (_canvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            Camera cam = _canvas.worldCamera != null ? _canvas.worldCamera : Camera.main;
            if (cam != null)
            {
                // Position directly on the camera plane distance to avoid perspective offsets
                Vector3 screenPos = new Vector3(MouseManager.Instance.MousePosition.x, MouseManager.Instance.MousePosition.y, _canvas.planeDistance);
                transform.position = cam.ScreenToWorldPoint(screenPos);
            }
            else
            {
                transform.position = MouseManager.Instance.MousePosition;
            }
        }
    }
}

