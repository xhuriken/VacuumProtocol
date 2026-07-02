using UnityEngine;

/// <summary>
/// Placed on a local custom cursor UI element in any scene.
/// Automatically tracks the global MouseManager's position and visibility state,
/// adapting correctly to the local Canvas scaler, camera, and sorting settings.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class CustomCursorFollower : MonoBehaviour
{
    private RectTransform _rectTransform;
    private Canvas _canvas;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
    }

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
