using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;
using Shapes;

/// <summary>
/// Description: Custom vector graphics toggle button component using the Shapes library.
/// Context: Placed on a UI GameObject representing a toggle control, overlayed with a transparent graphic for raycasting.
/// Justification: Implements the hybrid uGUI-Shapes pattern to create premium vector-based checkboxes without layout integration issues.
/// </summary>
public class UICustomToggle : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    /// <summary>
    /// Description: Nested class or delegate wrapping state change event notifications.
    /// Context: Event dispatching.
    /// Justification: Follows uGUI design patterns for event binding in the Inspector.
    /// </summary>
    [System.Serializable]
    public class ToggleEvent : UnityEvent<bool> { }

    [Header("Shapes References")]
    [Tooltip("Role: The background track shape.\nUse Case: Displays the toggle state background.\nJustification: Rectangular base of the toggle.")]
    [SerializeField] private Rectangle _track;

    [Tooltip("Role: The background fill shape behind the track (optional).\nUse Case: Displays the toggle track background.\nJustification: Allows separating track border and track background visual layers.")]
    [SerializeField] private Rectangle _trackBackground;

    [Tooltip("Role: The moving toggle handle/knob.\nUse Case: Physical slide representation.\nJustification: Relocated horizontally and colored dynamically to show state.")]
    [SerializeField] private Disc _handle;

    [Header("Toggle Settings")]
    [Tooltip("Role: The current state of the toggle.\nUse Case: State verification.\nJustification: Tracks active vs inactive state.")]
    [SerializeField] private bool _isOn = false;

    [Tooltip("Role: Total duration of the toggle transition animations.\nUse Case: Motion timing.\nJustification: Controls the speed of color shifts and handle translation.")]
    [SerializeField] private float _animationDuration = 0.2f;

    [Header("Visual Configurations")]
    [Tooltip("Role: Color of the handle knob when toggle is active (ON).\nUse Case: Color lookup.\nJustification: Standard active visual style.")]
    [SerializeField] private Color _handleOnColor = Color.green;

    [Tooltip("Role: Color of the handle knob when toggle is inactive (OFF).\nUse Case: Color lookup.\nJustification: Standard inactive visual style.")]
    [SerializeField] private Color _handleOffColor = Color.white;

    [Tooltip("Role: Local horizontal offset distance in pixels for the handle position in active state.\nUse Case: Movement math.\nJustification: Dictates the boundaries in uGUI coordinates (-offset for OFF, +offset for ON).")]
    [SerializeField] private float _handleLocalXOffset = 25f;

    [Tooltip("Role: Height increment applied to the background track on hover.\nUse Case: Hover feedback.\nJustification: Enlarges the track Y-axis on pointer enter for rich interactive polish.")]
    [SerializeField] private float _trackHoverHeightOffset = 5f;

    [Tooltip("Role: Color multiplier applied to the handle during transition to simulate a brief HDR bloom/glow.\nUse Case: State transition visual feedback.\nJustification: Standard premium juice effect during movement.")]
    [SerializeField] private float _handleTransitionBloomMultiplier = 1.5f;

    [Tooltip("Role: Radius multiplier applied to the handle when hovered.\nUse Case: Hover feedback.\nJustification: Enlarges the toggle handle to indicate interactivity.")]
    [SerializeField] private float _handleHoverRadiusMultiplier = 1.2f;

    [Header("Events")]
    [Tooltip("Role: Event dispatched when the toggle state changes.\nUse Case: Observer pattern notification.\nJustification: Exposes the state change value to presenters and controllers.")]
    [SerializeField] private ToggleEvent _onValueChanged = new ToggleEvent();

    private float _originalTrackHeight;
    private float _originalTrackBackgroundHeight;
    private float _originalHandleRadius;
    private float _initialHandleX;
    private bool _hasCachedOriginals = false;

    /// <summary>
    /// Description: Public event mapping to the serializable state change callbacks.
    /// Context: External event registration.
    /// Justification: Mirrors the uGUI Toggle API for drop-in replacement capability.
    /// </summary>
    public ToggleEvent onValueChanged
    {
        get => _onValueChanged;
        set => _onValueChanged = value;
    }

    /// <summary>
    /// Description: Gets or sets the toggle state programmatically.
    /// Context: Read/write access.
    /// Justification: Matches the standard uGUI Toggle property API. Updates visuals instantly on programmatic assignment.
    /// </summary>
    public bool isOn
    {
        get => _isOn;
        set => SetIsOn(value, notify: true, animate: false);
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
    /// Description: Caches the original geometric dimensions and positions of the track, background, and handle.
    /// Context: Component initialization.
    /// Justification: Ensures that baseline values are read exactly once from the initial prefab state, even if external calls modify state properties before Start.
    /// </summary>
    private void CacheOriginals()
    {
        if (_hasCachedOriginals) return;
        _hasCachedOriginals = true;

        if (_track != null)
        {
            _originalTrackHeight = _track.Height;
        }
        if (_trackBackground != null)
        {
            _originalTrackBackgroundHeight = _trackBackground.Height;
        }
        if (_handle != null)
        {
            _initialHandleX = _handle.transform.localPosition.x;
            _originalHandleRadius = _handle.Radius;
        }
    }

    /// <summary>
    /// Description: Unity Start lifecycle event. Caches original size, handle center, and initializes the visual state instantly.
    /// Context: Initialization.
    /// Justification: Caches default geometry values and syncs the visual layout with the serialized state value on startup.
    /// </summary>
    private void Start()
    {
        CacheOriginals();
        UpdateVisuals(instant: true);
    }

    /// <summary>
    /// Description: Implementation of IPointerClickHandler. Handles click events on the toggle area.
    /// Context: User interaction.
    /// Justification: Standard way to catch mouse/touch presses on uGUI transparent raycast targets.
    /// </summary>
    /// <param name="eventData">Pointer event data.</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        SetIsOn(!_isOn, notify: true, animate: true);
    }

    /// <summary>
    /// Description: Implementation of IPointerEnterHandler. Animates the track height to hover size.
    /// Context: User feedback.
    /// Justification: Provides visual feedback that the component is hoverable and interactive.
    /// </summary>
    /// <param name="eventData">Pointer event data.</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_track != null)
        {
            DOTween.To(() => _track.Height, x => _track.Height = x, _originalTrackHeight + _trackHoverHeightOffset, 0.15f);
        }
        if (_trackBackground != null)
        {
            DOTween.To(() => _trackBackground.Height, x => _trackBackground.Height = x, _originalTrackBackgroundHeight + _trackHoverHeightOffset, 0.15f);
        }
        if (_handle != null)
        {
            DOTween.To(() => _handle.Radius, x => _handle.Radius = x, _originalHandleRadius * _handleHoverRadiusMultiplier, 0.15f);
        }
    }

    /// <summary>
    /// Description: Implementation of IPointerExitHandler. Restores original track height.
    /// Context: User feedback.
    /// Justification: Reverts hover visual modifications.
    /// </summary>
    /// <param name="eventData">Pointer event data.</param>
    public void OnPointerExit(PointerEventData eventData)
    {
        if (_track != null)
        {
            DOTween.To(() => _track.Height, x => _track.Height = x, _originalTrackHeight, 0.15f);
        }
        if (_trackBackground != null)
        {
            DOTween.To(() => _trackBackground.Height, x => _trackBackground.Height = x, _originalTrackBackgroundHeight, 0.15f);
        }
        if (_handle != null)
        {
            DOTween.To(() => _handle.Radius, x => _handle.Radius = x, _originalHandleRadius, 0.15f);
        }
    }

    /// <summary>
    /// Description: Updates the toggle state with custom notification and animation options.
    /// Context: State modification.
    /// Justification: Provides granular control to distinguish user clicks (animated) from startup/programmatic setups (instant).
    /// </summary>
    /// <param name="value">The target boolean state.</param>
    /// <param name="notify">If true, dispatches onValueChanged event.</param>
    /// <param name="animate">If true, uses DOTween animations; otherwise, updates position and colors instantly.</param>
    public void SetIsOn(bool value, bool notify = true, bool animate = true)
    {
        if (_isOn == value) return;
        _isOn = value;

        UpdateVisuals(!animate);

        if (notify)
        {
            _onValueChanged?.Invoke(_isOn);
        }
    }

    /// <summary>
    /// Description: Synchronizes the Shapes visual properties with the current toggle state.
    /// Context: Visual update.
    /// Justification: Ensures track color and handle positions are aligned with the state value.
    /// </summary>
    /// <param name="instant">If true, applies values immediately; otherwise, plays smooth DOTween animations.</param>
    private void UpdateVisuals(bool instant)
    {
        CacheOriginals();

        float targetX = _isOn ? (_initialHandleX + _handleLocalXOffset) : (_initialHandleX - _handleLocalXOffset);
        Color targetColor = _isOn ? _handleOnColor : _handleOffColor;

        if (instant)
        {
            if (_handle != null)
            {
                Vector3 handlePos = _handle.transform.localPosition;
                _handle.transform.localPosition = new Vector3(targetX, handlePos.y, handlePos.z);
                _handle.Color = targetColor;
            }
        }
        else
        {
            if (_handle != null)
            {
                _handle.transform.DOLocalMoveX(targetX, _animationDuration).SetEase(Ease.OutBack);
                
                // Create a brief bloom flash during translation
                DOTween.Sequence()
                    .Append(DOTween.To(() => _handle.Color, x => _handle.Color = x, targetColor * _handleTransitionBloomMultiplier, _animationDuration * 0.4f).SetEase(Ease.OutQuad))
                    .Append(DOTween.To(() => _handle.Color, x => _handle.Color = x, targetColor, _animationDuration * 0.6f).SetEase(Ease.InOutCubic));
            }
        }
    }
}
