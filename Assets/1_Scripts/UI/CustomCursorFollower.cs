using UnityEngine;

/// <summary>
/// Description: Placed on a local custom cursor UI element in any scene. Automatically tracks the global MouseManager's position and visibility state.
/// Context: Attached to a UI image/rect transform that acts as a cursor.
/// Justification: Adapts correctly to the local Canvas scaler, camera, and sorting settings, which a global cursor might fail at.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class CustomCursorFollower : MonoBehaviour
{
    private RectTransform _rectTransform;
    private Canvas _canvas;

    private RectTransform _rectTransform;
    private Canvas _canvas;

    /// <summary>
    /// Description: Unity Awake callback. Caches necessary components.
    /// Context: Initialization.
    /// Justification: Required to correctly position the cursor inside the canvas hierarchy.
    /// </summary>
    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
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
