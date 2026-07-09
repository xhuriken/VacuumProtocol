using UnityEngine;
using UnityEngine.UI;
using Shapes;
using DG.Tweening;

/// <summary>
/// Description: Custom vector graphics ScrollView/ListView component using the Shapes library.
/// Context: Placed on a UI GameObject that also contains a standard Unity ScrollRect.
/// Justification: Wraps Unity's robust ScrollRect logic with premium custom Shapes visuals for background and scrollbars.
/// </summary>
[RequireComponent(typeof(ScrollRect))]
public class UICustomScrollView : MonoBehaviour
{
    [Header("Core References")]
    [Tooltip("Role: The standard Unity ScrollRect.\nUse Case: Physics, masking, and dragging logic.\nJustification: Leveraging built-in uGUI stability.")]
    [SerializeField] private ScrollRect _scrollRect;

    [Tooltip("Role: The background shape.\nUse Case: Premium vector background for the list.")]
    [SerializeField] private Rectangle _background;

    [Header("Scrollbars")]
    [Tooltip("Role: Custom vertical scrollbar using Shapes.\nUse Case: Replaces Unity's native vertical scrollbar.")]
    [SerializeField] private UICustomScrollbar _verticalScrollbar;

    [Tooltip("Role: Custom horizontal scrollbar using Shapes.\nUse Case: Replaces Unity's native horizontal scrollbar.")]
    [SerializeField] private UICustomScrollbar _horizontalScrollbar;

    // Cache to prevent unnecessary size updates
    private float _lastVerticalSize = -1f;
    private float _lastHorizontalSize = -1f;

    /// <summary>
    /// Description: Validates and finds missing references.
    /// </summary>
    private void Awake()
    {
        if (_scrollRect == null) _scrollRect = GetComponent<ScrollRect>();
        
        // Remove native scrollbars if they are assigned, to avoid conflicts
        if (_scrollRect != null)
        {
            _scrollRect.verticalScrollbar = null;
            _scrollRect.horizontalScrollbar = null;
            
            _scrollRect.onValueChanged.AddListener(OnScrollRectValueChanged);
        }

        if (_verticalScrollbar != null)
        {
            _verticalScrollbar.onValueChanged.AddListener(OnVerticalScrollbarValueChanged);
        }

        if (_horizontalScrollbar != null)
        {
            _horizontalScrollbar.onValueChanged.AddListener(OnHorizontalScrollbarValueChanged);
        }
    }

    /// <summary>
    /// Description: Ensures scrollbars are initialized correctly at start.
    /// </summary>
    private void Start()
    {
        UpdateScrollbarSizes();
    }

    /// <summary>
    /// Description: Unsubscribes events on destroy to prevent memory leaks.
    /// </summary>
    private void OnDestroy()
    {
        if (_scrollRect != null) _scrollRect.onValueChanged.RemoveListener(OnScrollRectValueChanged);
        if (_verticalScrollbar != null) _verticalScrollbar.onValueChanged.RemoveListener(OnVerticalScrollbarValueChanged);
        if (_horizontalScrollbar != null) _horizontalScrollbar.onValueChanged.RemoveListener(OnHorizontalScrollbarValueChanged);
    }

    /// <summary>
    /// Description: Unity LateUpdate event. Continually checks for content/viewport size changes to update scrollbar handle size.
    /// Justification: Content layout in uGUI happens dynamically, making LateUpdate the safest place to calculate accurate sizes without complex event tracking.
    /// </summary>
    private void LateUpdate()
    {
        UpdateScrollbarSizes();
    }

    /// <summary>
    /// Description: Syncs the Unity ScrollRect normalized position with our custom scrollbars.
    /// </summary>
    private void OnScrollRectValueChanged(Vector2 normalizedPos)
    {
        if (_verticalScrollbar != null && _verticalScrollbar.interactable)
        {
            _verticalScrollbar.value = normalizedPos.y;
        }

        if (_horizontalScrollbar != null && _horizontalScrollbar.interactable)
        {
            _horizontalScrollbar.value = normalizedPos.x;
        }
    }

    /// <summary>
    /// Description: Pushes the custom vertical scrollbar value back into the Unity ScrollRect.
    /// </summary>
    private void OnVerticalScrollbarValueChanged(float val)
    {
        if (_scrollRect != null)
        {
            Vector2 pos = _scrollRect.normalizedPosition;
            pos.y = val;
            _scrollRect.normalizedPosition = pos;
        }
    }

    /// <summary>
    /// Description: Pushes the custom horizontal scrollbar value back into the Unity ScrollRect.
    /// </summary>
    private void OnHorizontalScrollbarValueChanged(float val)
    {
        if (_scrollRect != null)
        {
            Vector2 pos = _scrollRect.normalizedPosition;
            pos.x = val;
            _scrollRect.normalizedPosition = pos;
        }
    }

    /// <summary>
    /// Description: Calculates the ratio between viewport size and content size to size the custom scrollbar handles appropriately.
    /// </summary>
    private void UpdateScrollbarSizes()
    {
        if (_scrollRect == null || _scrollRect.content == null || _scrollRect.viewport == null) return;

        float viewportHeight = _scrollRect.viewport.rect.height;
        float contentHeight = _scrollRect.content.rect.height;
        
        float viewportWidth = _scrollRect.viewport.rect.width;
        float contentWidth = _scrollRect.content.rect.width;

        if (_verticalScrollbar != null)
        {
            float newVerticalSize = contentHeight > 0 ? Mathf.Clamp01(viewportHeight / contentHeight) : 1f;
            if (!Mathf.Approximately(_lastVerticalSize, newVerticalSize))
            {
                _lastVerticalSize = newVerticalSize;
                _verticalScrollbar.size = newVerticalSize;
            }
        }

        if (_horizontalScrollbar != null)
        {
            float newHorizontalSize = contentWidth > 0 ? Mathf.Clamp01(viewportWidth / contentWidth) : 1f;
            if (!Mathf.Approximately(_lastHorizontalSize, newHorizontalSize))
            {
                _lastHorizontalSize = newHorizontalSize;
                _horizontalScrollbar.size = newHorizontalSize;
            }
        }
    }
}
