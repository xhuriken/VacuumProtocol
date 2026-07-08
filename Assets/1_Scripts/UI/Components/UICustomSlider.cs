using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;
using Shapes;

/// <summary>
/// Description: Custom vector graphics slider component using the Shapes library.
/// Context: Placed on a UI GameObject representing a slider control, overlayed with a transparent graphic for raycasting.
/// Justification: Implements the hybrid uGUI-Shapes pattern to create premium vector-based sliders supporting modular configurations.
/// </summary>
public class UICustomSlider : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerEnterHandler, IPointerExitHandler, IPointerUpHandler
{
    /// <summary>
    /// Description: Nested class wrapping slider value change notifications.
    /// Context: Event dispatching.
    /// Justification: Follows uGUI design patterns for event binding in the Inspector.
    /// </summary>
    [System.Serializable]
    public class SliderEvent : UnityEvent<float> { }

    [Header("Shapes References")]
    [Tooltip("Role: The background track shape.\nUse Case: Displays the slider track.\nJustification: Line base of the slider.")]
    [SerializeField] private Line _track;

    [Tooltip("Role: The fill overlay shape (optional).\nUse Case: Displays current fill percentage.\nJustification: Extends horizontally from the left side of the track.")]
    [SerializeField] private Line _fill;

    [Tooltip("Role: The moving toggle handle/knob (optional).\nUse Case: Physical slide representation.\nJustification: Relocated horizontally relative to value changes.")]
    [SerializeField] private Disc _handle;

    [Header("Slider Settings")]
    [Tooltip("Role: Minimum value of the slider range.\nUse Case: Range scaling.\nJustification: Standard slider property.")]
    [SerializeField] private float _minValue = 0f;

    [Tooltip("Role: Maximum value of the slider range.\nUse Case: Range scaling.\nJustification: Standard slider property.")]
    [SerializeField] private float _maxValue = 1f;

    [Tooltip("Role: Current value of the slider.\nUse Case: Value storage.\nJustification: Standard slider value.")]
    [SerializeField] private float _value = 0f;

    [Tooltip("Role: Controls whether the slider accepts pointer interactions.\nUse Case: Component locking.\nJustification: Matches the standard uGUI interactable state.")]
    [SerializeField] private bool _interactable = true;

    [Tooltip("Role: Speed of the slide fill and handle transitions.\nUse Case: Motion timing.\nJustification: Visual smoothness parameter.")]
    [SerializeField] private float _animationDuration = 0.1f;

    [Header("Visual Configurations")]
    [Tooltip("Role: Safety margins from the track edges for handle movement in pixels.\nUse Case: Handle layout constraint.\nJustification: Prevents the handle from sliding outside the rounded borders of the track.")]
    [SerializeField] private float _handleMargin = 0f;

    [Tooltip("Role: Color multiplier applied to the handle during dragging to simulate HDR bloom/glow.\nUse Case: Visual feedback during click/drag.\nJustification: Standard premium juice effect.")]
    [SerializeField] private float _handleDragBloomMultiplier = 1.5f;

    [Tooltip("Role: Radius multiplier applied to the handle when hovered.\nUse Case: Hover feedback.\nJustification: Enlarges the knob to indicate it is interactive.")]
    [SerializeField] private float _handleHoverRadiusMultiplier = 1.2f;

    [Tooltip("Role: Height increment applied to the background track on hover.\nUse Case: Hover feedback.\nJustification: Enlarges the track Y-axis on pointer enter for rich interactive polish.")]
    [SerializeField] private float _trackHoverHeightOffset = 5f;

    [Header("Disabled Visual Configurations")]
    [Tooltip("Role: Color applied to the track when the slider is disabled/non-interactable.\nUse Case: Disabled visual state.")]
    [SerializeField] private Color _disabledTrackColor = new Color(0.3f, 0.3f, 0.3f, 0.3f);

    [Tooltip("Role: Color applied to the fill when the slider is disabled/non-interactable.\nUse Case: Disabled visual state.")]
    [SerializeField] private Color _disabledFillColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);

    [Tooltip("Role: Color applied to the handle when the slider is disabled/non-interactable.\nUse Case: Disabled visual state.")]
    [SerializeField] private Color _disabledHandleColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    [Header("Events")]
    [Tooltip("Role: Event dispatched when the slider value changes.\nUse Case: Observer pattern notification.\nJustification: Exposes the value changes to presenters and controllers.")]
    [SerializeField] private SliderEvent _onValueChanged = new SliderEvent();

    private float _originalTrackThickness;
    private Color _originalTrackColor;
    private Color _originalFillColor;
    private Color _originalHandleColor;
    private float _originalHandleRadius;
    private float _initialHandleX;

    /// <summary>
    /// Description: Public event mapping to the serializable value change callbacks.
    /// Context: External event registration.
    /// Justification: Mirrors the uGUI Slider API for drop-in replacement capability.
    /// </summary>
    public SliderEvent onValueChanged
    {
        get => _onValueChanged;
        set => _onValueChanged = value;
    }

    /// <summary>
    /// Description: Gets or sets the minimum value range. Re-clamps the current value accordingly.
    /// </summary>
    public float minValue
    {
        get => _minValue;
        set
        {
            _minValue = value;
            SetValue(_value, notify: false, animate: false);
        }
    }

    /// <summary>
    /// Description: Gets or sets the maximum value range. Re-clamps the current value accordingly.
    /// </summary>
    public float maxValue
    {
        get => _maxValue;
        set
        {
            _maxValue = value;
            SetValue(_value, notify: false, animate: false);
        }
    }

    /// <summary>
    /// Description: Gets or sets the current slider value programmatically. Updates visuals instantly.
    /// </summary>
    public float value
    {
        get => _value;
        set => SetValue(value, notify: false, animate: false);
    }

    /// <summary>
    /// Description: Gets or sets the interactable state of the slider. Updates visuals accordingly.
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
    /// Description: Gets or sets the color of the fill Line shape.
    /// </summary>
    public Color fillColor
    {
        get => _fill != null ? _fill.Color : Color.white;
        set
        {
            if (_fill != null)
            {
                _originalFillColor = value;
                if (_interactable)
                {
                    _fill.Color = value;
                }
            }
        }
    }


    /// <summary>
    /// Description: Unity Awake lifecycle event. Checks for a Graphic component and dynamically adds a transparent Image if missing.
    /// Context: Initialization.
    /// Justification: Unity's EventSystem requires a Graphic component (like Image) with raycastTarget enabled to detect pointer clicks and hovers. Dynamically adding it saves setup steps in the Inspector.
    /// </summary>
    private void Awake()
    {
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
    /// Description: Unity Start lifecycle event. Caches original size, handle center, and initializes the visual state instantly.
    /// Context: Initialization.
    /// Justification: Caches default geometry values and syncs the visual layout with the serialized state value on startup.
    /// </summary>
    private void Start()
    {
        if (_track != null)
        {
            _originalTrackThickness = _track.Thickness;
            _originalTrackColor = _track.Color;
        }
        if (_fill != null)
        {
            _originalFillColor = _fill.Color;
        }
        if (_handle != null)
        {
            _initialHandleX = _handle.transform.localPosition.x;
            _originalHandleColor = _handle.Color;
            _originalHandleRadius = _handle.Radius;
        }
        UpdateVisuals(instant: true);
        UpdateInteractableVisuals(animate: false);
    }

    /// <summary>
    /// Description: Implementation of IPointerDownHandler. Updates values when clicking within the slider bounds and increases bloom.
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
    /// Description: Implementation of IPointerEnterHandler. Animates the track thickness and handle radius to hover sizes.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_interactable) return;
        if (_track != null)
        {
            DOTween.To(() => _track.Thickness, x => _track.Thickness = x, _originalTrackThickness + _trackHoverHeightOffset, 0.15f);
        }
        if (_handle != null)
        {
            DOTween.To(() => _handle.Radius, x => _handle.Radius = x, _originalHandleRadius * _handleHoverRadiusMultiplier, 0.15f);
        }
    }

    /// <summary>
    /// Description: Implementation of IPointerExitHandler. Restores original track thickness and handle radius.
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
            DOTween.To(() => _handle.Radius, x => _handle.Radius = x, _originalHandleRadius, 0.15f);
        }
    }

    /// <summary>
    /// Description: Helper that converts a screen point to local coordinates and applies values mapped to the slider range.
    /// </summary>
    private void UpdateValueFromPointer(PointerEventData eventData)
    {
        RectTransform rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
        {
            float width = rectTransform.rect.width;
            if (width <= 0f) return;

            float minX = rectTransform.rect.xMin;
            float pct = Mathf.Clamp01((localPoint.x - minX) / width);

            float newValue = Mathf.Lerp(_minValue, _maxValue, pct);
            SetValue(newValue, notify: true, animate: true);
        }
    }

    /// <summary>
    /// Description: Standard setter to modify values with granular notification and animation control.
    /// </summary>
    public void SetValue(float newValue, bool notify = true, bool animate = true)
    {
        newValue = Mathf.Clamp(newValue, _minValue, _maxValue);
        if (Mathf.Approximately(_value, newValue)) return;

        _value = newValue;

        UpdateVisuals(!animate);

        if (notify)
        {
            _onValueChanged?.Invoke(_value);
        }
    }

    /// <summary>
    /// Description: Aligns the Shapes visual elements (fill width and handle X position) to the current slider percentage.
    /// </summary>
    private void UpdateVisuals(bool instant)
    {
        float range = _maxValue - _minValue;
        float pct = range > 0f ? (_value - _minValue) / range : 0f;

        RectTransform rectTransform = GetComponent<RectTransform>();
        float width = rectTransform != null ? rectTransform.rect.width : 100f;
        float minX = rectTransform != null ? rectTransform.rect.xMin : -width / 2f;
        float maxX = rectTransform != null ? rectTransform.rect.xMax : width / 2f;

        // 1. Update track points if present
        if (_track != null)
        {
            _track.Start = new Vector3(minX, 0f, 0f);
            _track.End = new Vector3(maxX, 0f, 0f);
        }

        // 2. Update fill width/endpoints if present
        if (_fill != null)
        {
            float targetX = Mathf.Lerp(minX, maxX, pct);
            _fill.Start = new Vector3(minX, 0f, 0f);
            if (instant)
            {
                _fill.End = new Vector3(targetX, 0f, 0f);
            }
            else
            {
                DOTween.To(() => _fill.End, x => _fill.End = x, new Vector3(targetX, 0f, 0f), _animationDuration);
            }
        }

        // 3. Update handle position if present
        if (_handle != null && rectTransform != null)
        {
            float handleMinX = minX + _handleMargin;
            float handleMaxX = maxX - _handleMargin;
            float targetX = Mathf.Lerp(handleMinX, handleMaxX, pct);

            if (instant)
            {
                Vector3 localPos = _handle.transform.localPosition;
                _handle.transform.localPosition = new Vector3(targetX, localPos.y, localPos.z);
            }
            else
            {
                _handle.transform.DOLocalMoveX(targetX, _animationDuration).SetEase(Ease.OutQuad);
            }
        }
    }

    /// <summary>
    /// Description: Updates the visual colors of track, fill, and handle to represent active or disabled states.
    /// Justification: Ensures disabled/non-interactable sliders are visually greyed out.
    /// </summary>
    private void UpdateInteractableVisuals(bool animate)
    {
        Color targetTrackColor = _interactable ? _originalTrackColor : _disabledTrackColor;
        Color targetFillColor = _interactable ? _originalFillColor : _disabledFillColor;
        Color targetHandleColor = _interactable ? _originalHandleColor : _disabledHandleColor;

        float duration = animate ? _animationDuration : 0f;

        if (duration <= 0f)
        {
            if (_track != null) _track.Color = targetTrackColor;
            if (_fill != null) _fill.Color = targetFillColor;
            if (_handle != null) _handle.Color = targetHandleColor;
        }
        else
        {
            if (_track != null) DOTween.To(() => _track.Color, x => _track.Color = x, targetTrackColor, duration).SetEase(Ease.OutQuad);
            if (_fill != null) DOTween.To(() => _fill.Color, x => _fill.Color = x, targetFillColor, duration).SetEase(Ease.OutQuad);
            if (_handle != null) DOTween.To(() => _handle.Color, x => _handle.Color = x, targetHandleColor, duration).SetEase(Ease.OutQuad);
        }
    }
}