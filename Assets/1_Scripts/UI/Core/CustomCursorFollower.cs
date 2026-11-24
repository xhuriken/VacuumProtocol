using UnityEngine;
using Shapes;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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

    private CanvasGroup _canvasGroup;

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
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;

        // Désactiver le raycastTarget sur tous les graphismes du curseur pour qu'il ne bloque jamais les boutons en dessous
        UnityEngine.UI.Graphic[] graphics = GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
            {
                graphics[i].raycastTarget = false;
            }
        }

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
        bool isCursorLocked = Cursor.lockState == CursorLockMode.Locked;
        bool shouldBeVisible = !isCursorLocked;

        if (MouseManager.Instance != null)
        {
            shouldBeVisible = MouseManager.Instance.ShouldShowCursor;
        }

        // Contrôler la visibilité des visuels sans désactiver le GameObject racine pour que LateUpdate continue de tourner
        if (_defaultCursorVisual != null)
        {
            _defaultCursorVisual.SetActive(shouldBeVisible && !_isInBrushMode);
        }
        if (_brushCursorVisual != null)
        {
            _brushCursorVisual.SetActive(shouldBeVisible && _isInBrushMode);
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = shouldBeVisible ? 1f : 0f;
        }

        // Pour les Shapes et enfants directs si les conteneurs visuels ne sont pas assignés
        if (_defaultCursorVisual == null && _brushCursorVisual == null)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                transform.GetChild(i).gameObject.SetActive(shouldBeVisible);
            }
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
            _brushInnerDisc.Color = activeColor;
            _brushInnerDisc.Radius = Mathf.Max(2f, radiusPixels * 0.25f);
        }

        if (_brushOutlineRing != null)
        {
            _brushOutlineRing.Radius = Mathf.Max(4f, radiusPixels);
            _brushOutlineRing.Thickness = 2f;
            _brushOutlineRing.Color = isEraser ? new Color(1f, 0.4f, 0.4f, 0.9f) : Color.white;
        }
    }

    /// <summary>
    /// Description: Resets cursor mode back to the standard UI pointer.
    /// Context: Called on Awake or canvas exit.
    /// Justification: Cleans up brush ring states.
    /// </summary>
    public void ResetCursorMode()
    {
        SetBrushCursorMode(false);
    }

    private Vector2 GetMouseScreenPosition()
    {
        if (MouseManager.Instance != null)
        {
            return MouseManager.Instance.MousePosition;
        }

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return Mouse.current.position.ReadValue();
        }
#endif
        return Input.mousePosition;
    }

    /// <summary>
    /// Description: Calculates the correct screen/world position for the cursor based on Canvas render mode.
    /// Context: Called every frame while visible.
    /// Justification: Solves the discrepancy between ScreenSpaceOverlay and ScreenSpaceCamera positioning.
    /// </summary>
    private void PositionCursor()
    {
        Vector2 mousePos = GetMouseScreenPosition();

        if (_canvas == null)
        {
            _canvas = GetComponentInParent<Canvas>();
        }

        if (_canvas == null)
        {
            transform.position = mousePos;
            return;
        }

        Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : (_canvas.worldCamera != null ? _canvas.worldCamera : Camera.main);

        RectTransform parentRect = transform.parent as RectTransform;
        if (parentRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, mousePos, cam, out Vector2 localPoint))
        {
            _rectTransform.anchoredPosition = localPoint;
        }
        else if (RectTransformUtility.ScreenPointToWorldPointInRectangle(_rectTransform, mousePos, cam, out Vector3 worldPoint))
        {
            transform.position = worldPoint;
        }
        else
        {
            transform.position = mousePos;
        }
    }
}
