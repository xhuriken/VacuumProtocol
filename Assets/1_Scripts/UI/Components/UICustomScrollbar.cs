using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;
using Shapes;

/// <summary>
/// Description: Custom vector graphics scrollbar component using the Shapes library.
/// Context: Placed on a UI GameObject representing a scrollbar.
/// Justification: Provides a premium, vector-based scrollbar mimicking Unity's native Scrollbar logic but with Shapes rendering.
/// </summary>
public class UICustomScrollbar : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerEnterHandler, IPointerExitHandler, IPointerUpHandler
{
    /// <summary>
    /// Description: Orientation enum for the scrollbar.
    /// </summary>
    public enum Direction
    {
        LeftToRight,
        RightToLeft,
        BottomToTop,
        TopToBottom
    }

    /// <summary>
    /// Description: Event dispatched when scrollbar value changes.
    /// </summary>
    [System.Serializable]
    public class ScrollbarEvent : UnityEvent<float> { }

    [Header("Shapes References")]
    [Tooltip("Role: The background track shape.\nUse Case: Displays the track area.\nJustification: Line base of the scrollbar.")]
    [SerializeField] private Line _track;

    [Tooltip("Role: The moving scrollbar handle.\nUse Case: Physical slide representation.\nJustification: Size and position adapt based on content.")]
    [SerializeField] private Line _handle;

    [Header("Scrollbar Settings")]
    [Tooltip("Role: Direction of the scrollbar.\nUse Case: Horizontal or vertical scrolling.")]
    [SerializeField] private Direction _direction = Direction.TopToBottom;

    [Tooltip("Role: Current value of the scrollbar (0 to 1).\nUse Case: Position storage.")]
    [Range(0f, 1f)]
    [SerializeField] private float _value = 0f;

    [Tooltip("Role: Size of the handle as a fraction of the total length (0 to 1).\nUse Case: Represents the proportion of the viewport to the content.")]
    [Range(0f, 1f)]
    [SerializeField] private float _size = 0.2f;

    [Tooltip("Role: Controls whether the scrollbar accepts pointer interactions.")]
    [SerializeField] private bool _interactable = true;

    [Tooltip("Role: Auto-hide the scrollbar when size is 1 (content fits perfectly).")]
    [SerializeField] private bool _autoHideWhenFull = true;

    [Tooltip("Role: Speed of transitions.\nUse Case: Motion timing.")]
    [SerializeField] private float _animationDuration = 0.1f;

    [Header("Visual Configurations")]
    [Tooltip("Role: Safety margins from the track edges in pixels.\nUse Case: Prevents the handle from overlapping rounded corners.")]
    [SerializeField] private float _handleMargin = 0f;

    [Tooltip("Role: Minimum pixel length of the handle.\nUse Case: Prevents the handle from becoming too small to click.")]
    [SerializeField] private float _minHandlePixelSize = 10f;

    [Tooltip("Role: Color multiplier applied to the handle during dragging to simulate HDR bloom/glow.")]
    [SerializeField] private float _handleDragBloomMultiplier = 1.5f;

    [Tooltip("Role: Thickness multiplier applied to the handle when hovered.")]
    [SerializeField] private float _handleHoverThicknessMultiplier = 1.2f;

    [Tooltip("Role: Thickness increment applied to the track on hover.")]
    [SerializeField] private float _trackHoverThicknessOffset = 2f;

    [Header("Disabled Visual Configurations")]
    [SerializeField] private Color _disabledTrackColor = new Color(0.3f, 0.3f, 0.3f, 0.3f);
    [SerializeField] private Color _disabledHandleColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    [Header("Events")]
    [SerializeField] private ScrollbarEvent _onValueChanged = new ScrollbarEvent();

    private float _originalTrackThickness;
    private Color _originalTrackColor;
    private float _originalHandleThickness;
    private Color _originalHandleColor;
    
    // Cached layout variables
    private RectTransform _rectTransform;

    /// <summary>
    /// Description: Public event mapping to the serializable value change callbacks.
    /// </summary>
    public ScrollbarEvent onValueChanged
    {
        get => _onValueChanged;
        set => _onValueChanged = value;
    }

    /// <summary>
    /// Description: Gets or sets the current scrollbar value programmatically. Updates visuals instantly.
    /// </summary>
    public float value
    {
        get => _value;
        set => SetValue(value, notify: false, animate: false);
    }

    /// <summary>
    /// Description: Gets or sets the visual size ratio of the handle.
    /// </summary>
    public float size
    {
        get => _size;
        set
        {
            _size = Mathf.Clamp01(value);
            UpdateVisuals(instant: false);
        }
    }

    /// <summary>
    /// Description: Gets or sets the interactable state of the scrollbar. Updates visuals accordingly.
    /// </summary>
    public bool interactable
    {
        get => _interactable;
        set
        {
            if (_interactable == value) return;
            _interactable = value;
            UpdateInteractableVisuals(animate: true);
        }
    }

    /// <summary>
    /// Description: Gets or sets the orientation of the scrollbar.
    /// </summary>
    public Direction direction
    {
        get => _direction;
        set
        {
            _direction = value;
            UpdateVisuals(instant: true);
        }
    }

    /// <summary>
    /// Description: Unity Awake lifecycle event. Checks for a Graphic component and dynamically adds a transparent Image if missing.
    /// </summary>
    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        Graphic graphic = GetComponent<Graphic>();
        if (graphic == null)
        {
            Image dynamicImage = gameObject.AddComponent<Image>();
            dynamicImage.color = new Color(0f, 0f, 0f, 0f);
            dynamicImage.raycastTarget = true;
        }
        else if (!graphic.raycastTarget)
        {
            graphic.raycastTarget = true;
        }
    }

    /// <summary>
    /// Description: Unity Start lifecycle event. Caches original sizes and initializes the visual state instantly.
    /// </summary>
    private void Start()
    {
        if (_track != null)
        {
            _originalTrackThickness = _track.Thickness;
            _originalTrackColor = _track.Color;
        }
        if (_handle != null)
        {
            _originalHandleThickness = _handle.Thickness;
            _originalHandleColor = _handle.Color;
        }
        UpdateVisuals(instant: true);
        UpdateInteractableVisuals(animate: false);
    }

    /// <summary>
    /// Description: Implementation of IPointerDownHandler. Updates values when clicking and adds bloom.
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!_interactable) return;
        UpdateValueFromPointer(eventData);
        if (_handle != null)
        {
            DOTween.To(() => _handle.Color, x => _handle.Color = x, _originalHandleColor * _handleDragBloomMultiplier, 0.1f);
        }
    }

    /// <summary>
    /// Description: Implementation of IPointerUpHandler. Resets the handle bloom/color on release.
    /// </summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_interactable) return;
        if (_handle != null)
        {
            DOTween.To(() => _handle.Color, x => _handle.Color = x, _originalHandleColor, 0.1f);
        }
    }

    /// <summary>
    /// Description: Implementation of IDragHandler. Updates values dynamically while dragging.
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (!_interactable) return;
        UpdateValueFromPointer(eventData);
    }

    /// <summary>
    /// Description: Implementation of IPointerEnterHandler. Animates the track and handle thickness.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_interactable) return;
        if (_track != null)
        {
            DOTween.To(() => _track.Thickness, x => _track.Thickness = x, _originalTrackThickness + _trackHoverThicknessOffset, 0.15f);
        }
        if (_handle != null)
        {
            DOTween.To(() => _handle.Thickness, x => _handle.Thickness = x, _originalHandleThickness * _handleHoverThicknessMultiplier, 0.15f);
        }
    }

    /// <summary>
    /// Description: Implementation of IPointerExitHandler. Restores original thickness.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        if (!_interactable) return;
        if (_track != null)
        {
            DOTween.To(() => _track.Thickness, x => _track.Thickness = x, _originalTrackThickness, 0.15f);
        }
        if (_handle != null)
        {
            DOTween.To(() => _handle.Thickness, x => _handle.Thickness = x, _originalHandleThickness, 0.15f);
        }
    }

    /// <summary>
    /// Description: Helper that converts a screen point to local coordinates and applies values mapped to the slider range.
    /// </summary>
    private void UpdateValueFromPointer(PointerEventData eventData)
    {
        if (_rectTransform == null) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
        {
            Vector2 rectSize = _rectTransform.rect.size;
            float pct = 0f;

            switch (_direction)
            {
                case Direction.LeftToRight:
                    if (rectSize.x > 0f) pct = Mathf.Clamp01((localPoint.x - _rectTransform.rect.xMin) / rectSize.x);
                    break;
                case Direction.RightToLeft:
                    if (rectSize.x > 0f) pct = 1f - Mathf.Clamp01((localPoint.x - _rectTransform.rect.xMin) / rectSize.x);
                    break;
                case Direction.BottomToTop:
                    if (rectSize.y > 0f) pct = Mathf.Clamp01((localPoint.y - _rectTransform.rect.yMin) / rectSize.y);
                    break;
                case Direction.TopToBottom:
                    if (rectSize.y > 0f) pct = 1f - Mathf.Clamp01((localPoint.y - _rectTransform.rect.yMin) / rectSize.y);
                    break;
            }

            SetValue(pct, notify: true, animate: true);
        }
    }

    /// <summary>
    /// Description: Standard setter to modify values with granular notification and animation control.
    /// </summary>
    public void SetValue(float newValue, bool notify = true, bool animate = true)
    {
        newValue = Mathf.Clamp01(newValue);
        if (Mathf.Approximately(_value, newValue)) return;

        _value = newValue;

        UpdateVisuals(!animate);

        if (notify)
        {
            _onValueChanged?.Invoke(_value);
        }
    }

    /// <summary>
    /// Description: Aligns the track and handle Line vectors based on orientation and value.
    /// </summary>
    private void UpdateVisuals(bool instant)
    {
        if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
        if (_rectTransform == null) return;

        // Auto Hide Logic
        bool shouldShow = !(_autoHideWhenFull && _size >= 0.999f);
        if (_track != null) _track.enabled = shouldShow;
        if (_handle != null) _handle.enabled = shouldShow;

        if (!shouldShow) return;

        float width = _rectTransform.rect.width;
        float height = _rectTransform.rect.height;
        float minX = _rectTransform.rect.xMin;
        float maxX = _rectTransform.rect.xMax;
        float minY = _rectTransform.rect.yMin;
        float maxY = _rectTransform.rect.yMax;

        Vector3 trackStart = Vector3.zero;
        Vector3 trackEnd = Vector3.zero;
        Vector3 handleStart = Vector3.zero;
        Vector3 handleEnd = Vector3.zero;

        bool isHorizontal = _direction == Direction.LeftToRight || _direction == Direction.RightToLeft;
        float totalLength = isHorizontal ? width : height;
        float safeLength = Mathf.Max(0f, totalLength - (_handleMargin * 2f));
        
        // Calculate the physical size of the handle
        float handlePixelSize = Mathf.Max(safeLength * _size, _minHandlePixelSize);
        
        // The space available for the handle to slide within
        float slidableLength = safeLength - handlePixelSize;
        float startOffset = _handleMargin + (slidableLength * _value);

        if (isHorizontal)
        {
            trackStart = new Vector3(minX, 0f, 0f);
            trackEnd = new Vector3(maxX, 0f, 0f);

            float handleXStart = _direction == Direction.LeftToRight ? minX + startOffset : maxX - startOffset - handlePixelSize;
            float handleXEnd = handleXStart + handlePixelSize;

            handleStart = new Vector3(handleXStart, 0f, 0f);
            handleEnd = new Vector3(handleXEnd, 0f, 0f);
        }
        else
        {
            trackStart = new Vector3(0f, minY, 0f);
            trackEnd = new Vector3(0f, maxY, 0f);

            float handleYStart = _direction == Direction.BottomToTop ? minY + startOffset : maxY - startOffset - handlePixelSize;
            float handleYEnd = handleYStart + handlePixelSize;

            handleStart = new Vector3(0f, handleYStart, 0f);
            handleEnd = new Vector3(0f, handleYEnd, 0f);
        }

        // Apply to Track
        if (_track != null)
        {
            _track.Start = trackStart;
            _track.End = trackEnd;
        }

        // Apply to Handle
        if (_handle != null)
        {
            if (instant)
            {
                _handle.Start = handleStart;
                _handle.End = handleEnd;
            }
            else
            {
                DOTween.To(() => _handle.Start, x => _handle.Start = x, handleStart, _animationDuration);
                DOTween.To(() => _handle.End, x => _handle.End = x, handleEnd, _animationDuration);
            }
        }
    }

    /// <summary>
    /// Description: Updates colors based on interactable state.
    /// </summary>
    private void UpdateInteractableVisuals(bool animate)
    {
        Color targetTrackColor = _interactable ? _originalTrackColor : _disabledTrackColor;
        Color targetHandleColor = _interactable ? _originalHandleColor : _disabledHandleColor;

        float duration = animate ? _animationDuration : 0f;

        if (duration <= 0f)
        {
            if (_track != null) _track.Color = targetTrackColor;
            if (_handle != null) _handle.Color = targetHandleColor;
        }
        else
        {
            if (_track != null) DOTween.To(() => _track.Color, x => _track.Color = x, targetTrackColor, duration).SetEase(Ease.OutQuad);
            if (_handle != null) DOTween.To(() => _handle.Color, x => _handle.Color = x, targetHandleColor, duration).SetEase(Ease.OutQuad);
        }
    }
}
