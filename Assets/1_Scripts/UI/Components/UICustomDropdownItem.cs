using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using Shapes;
using DG.Tweening;
using Febucci.UI.Core;

/// <summary>
/// Description: A custom shape-based UI dropdown item.
/// Context: Attached to dropdown option item elements inside the unfolded dropdown list template.
/// Justification: Handles background hover color blending, Febucci typewriter triggers on hover, and selection communication to the parent dropdown.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UICustomDropdownItem : UICustomButtonBase
{
    #region Serialized Fields & References

    [Header("Shapes References")]
    [Tooltip("Role: The background rectangle shape component.\nUse Case: Visual representation of the item body.\nJustification: Changes color dynamically on hover.")]
    [SerializeField]
    private Rectangle _backgroundRect;

    [Tooltip("Role: The outline rectangle shape component.\nUse Case: Visual outline.\nJustification: Animated on hover.")]
    [SerializeField]
    private Rectangle _rect;

    [Header("Text References")]
    [Tooltip("Role: The TextMeshPro text component.\nUse Case: Displaying the item label.\nJustification: Standard option text field.")]
    [SerializeField]
    private TextMeshProUGUI _itemText;

    [Header("Animation Settings")]
    [Tooltip("Role: Duration of the outline transition on hover.")]
    [SerializeField]
    private float _hoverDuration = 0.25f;

    [Tooltip("Role: Extra size offset in pixels added on hover to grow the item outline.")]
    [SerializeField]
    private float _hoverSizeOffset = 6f;

    [Tooltip("Role: Thickness multiplier applied to the rectangle border on hover.")]
    [SerializeField]
    private float _hoverThicknessMultiplier = 1.5f;

    [Header("Dash Settings (Hover)")]
    [Tooltip("Role: The size of the dashes when hovered.")]
    [SerializeField]
    private float _dashSize = 4f;

    [Tooltip("Role: The spacing between dashes when hovered.")]
    [SerializeField]
    private float _dashSpacing = 4f;

    [Tooltip("Role: Speed of the infinite rotation animation of the dashed outline.")]
    [SerializeField]
    private float _dashRotationSpeed = 1.5f;

    [Header("Background Settings")]
    [Tooltip("Role: Normal background color of the item.")]
    [SerializeField]
    private Color _normalColor = new Color(0f, 0f, 0f, 0f);

    [Tooltip("Role: Click background color of the item.")]
    [SerializeField]
    private Color _hoverColor = new Color(1f, 1f, 1f, 0.1f);

    #endregion

    #region Private Fields

    private UICustomDropdown _parentDropdown;
    private int _itemIndex = -1;
    private TypewriterCore _textAnimatorPlayer;
    private bool _isCached = false;

    // Cache original states
    private float _originalThickness;
    private Color _originalRectColor;
    private Vector3 _originalRectLocalScale;
    private float _currentSizeOffset = 0f;
    private float _currentDashOffset = 0f;

    #endregion

    #region Public Properties

    /// <summary>
    /// Description: Gets the background rectangle shape component.
    /// </summary>
    public Rectangle BackgroundRect => _backgroundRect;

    /// <summary>
    /// Description: Gets the item text component.
    /// </summary>
    public TextMeshProUGUI ItemText => _itemText;

    #endregion

    #region Unity Lifecycle Callbacks

    /// <summary>
    /// Description: Unity Start callback. Caches the initial typewriter component.
    /// </summary>
    protected virtual void Start()
    {
        CacheComponents();
        ResetVisuals();
    }

    /// <summary>
    /// Description: Unity OnEnable callback. Ensures visual consistency when re-enabled.
    /// </summary>
    private void OnEnable()
    {
        if (_isCached)
        {
            ResetVisuals();
        }
    }

    /// <summary>
    /// Description: Unity OnDisable callback. Cleans up active tweens.
    /// </summary>
    protected override void OnDisable()
    {
        base.OnDisable();
        KillActiveTweens();
    }

    #endregion

    #region EventSystem Interface Overrides

    /// <summary>
    /// Description: Handles hover entry transitions.
    /// </summary>
    public override void OnPointerEnter(PointerEventData eventData)
    {
        if (!Interactable) return;
        base.OnPointerEnter(eventData);
        AnimateHoverEnter();
    }

    /// <summary>
    /// Description: Handles hover exit transitions.
    /// </summary>
    public override void OnPointerExit(PointerEventData eventData)
    {
        if (!Interactable) return;
        base.OnPointerExit(eventData);
        AnimateHoverExit();
    }

    /// <summary>
    /// Description: Handles item selection click event.
    /// </summary>
    public override void OnPointerClick(PointerEventData eventData)
    {
        if (!Interactable) return;
        base.OnPointerClick(eventData);
        
        if (_parentDropdown != null && eventData.button == PointerEventData.InputButton.Left)
        {
            AnimateHoverExit();

            if (_backgroundRect != null)
            {
                DOTween.Kill(_backgroundRect);
                DOTween.To(() => _backgroundRect.Color, x => _backgroundRect.Color = x, _hoverColor, _hoverDuration)
                    .SetEase(Ease.OutCubic);
            }

            _parentDropdown.SelectItem(_itemIndex);
        }
    }

    #endregion

    #region Unity Update Callback

    /// <summary>
    /// Description: Unity Update callback. Updates layout dimensions and rotates dashed outline borders.
    /// </summary>
    private void Update()
    {
        UpdateRectSize();

        if (IsHovered && _rect != null && _rect.Dashed)
        {
            _currentDashOffset += Time.deltaTime * _dashRotationSpeed;
            _currentDashOffset %= 1.0f;
            if (_currentDashOffset < 0f) _currentDashOffset += 1.0f;
            _rect.DashOffset = _currentDashOffset;
        }
    }

    /// <summary>
    /// Description: Synchronizes outline dimensions with the parent RectTransform.
    /// </summary>
    private void UpdateRectSize()
    {
        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null && _rect != null)
        {
            float baseWidth = rt.rect.width;
            float baseHeight = rt.rect.height;

            float thicknessDelta = 0f;
            if (_isCached)
            {
                thicknessDelta = _rect.Thickness - _originalThickness;
            }

            _rect.Width = baseWidth + thicknessDelta + _currentSizeOffset;
            _rect.Height = baseHeight + thicknessDelta + _currentSizeOffset;
        }
    }

    #endregion

    #region Public Setup & Operations

    /// <summary>
    /// Description: Configures the dropdown item parameters.
    /// </summary>
    /// <param name="parent">The parent custom dropdown component.</param>
    /// <param name="index">The index of the option this item represents.</param>
    /// <param name="text">The label text of the option.</param>
    public void Setup(UICustomDropdown parent, int index, string text)
    {
        _parentDropdown = parent;
        _itemIndex = index;

        if (_itemText != null)
        {
            _itemText.text = text;
        }

        CacheComponents();
        ResetVisuals();

        // Highlight active item background
        if (_parentDropdown != null && _itemIndex == _parentDropdown.value)
        {
            if (_backgroundRect != null)
            {
                _backgroundRect.Color = _hoverColor;
            }
        }
    }

    /// <summary>
    /// Description: Relaunches the Febucci typewriter animation if the animator player is configured.
    /// </summary>
    public void PlayTypewriter()
    {
        if (_textAnimatorPlayer != null)
        {
            _textAnimatorPlayer.StartShowingText(true);
        }
    }

    #endregion

    #region Helpers & Animations

    /// <summary>
    /// Description: Caches components dynamically.
    /// </summary>
    private void CacheComponents()
    {
        if (_isCached) return;

        _textAnimatorPlayer = GetComponentInChildren<TypewriterCore>();

        if (_rect != null)
        {
            _originalThickness = _rect.Thickness;
            _originalRectColor = _rect.Color;
            _originalRectLocalScale = _rect.transform.localScale;
        }

        _isCached = true;
    }

    /// <summary>
    /// Description: Resets background colors to default state.
    /// </summary>
    private void ResetVisuals()
    {
        KillActiveTweens();

        if (_backgroundRect != null)
        {
            _backgroundRect.Color = _normalColor;
        }

        if (_rect != null)
        {
            _rect.Dashed = true;
            _rect.DashType = DashType.Basic;
            _rect.DashSize = _dashSize;
            _rect.DashSpacing = 0f;
            _rect.Thickness = _originalThickness;
            _rect.transform.localScale = _originalRectLocalScale;
            // Dotted border is invisible (alpha 0) when not hovered
            _rect.Color = new Color(_originalRectColor.r, _originalRectColor.g, _originalRectColor.b, 0f);
        }
        _currentSizeOffset = 0f;
        _currentDashOffset = 0f;
    }

    /// <summary>
    /// Description: Kills active DOTween animations.
    /// </summary>
    private void KillActiveTweens()
    {
        if (_backgroundRect != null)
        {
            DOTween.Kill(_backgroundRect);
        }

        if (_rect != null)
        {
            DOTween.Kill(_rect);
            _rect.transform.DOKill();
        }
    }

    /// <summary>
    /// Description: Transitions color to hover state and triggers the typewriter effect.
    /// </summary>
    private void AnimateHoverEnter()
    {
        KillActiveTweens();

        if (_rect != null)
        {
            _rect.Dashed = true;
            _rect.DashType = DashType.Basic;
            _rect.DashSize = _dashSize;

            DOTween.To(() => _rect.Color, x => _rect.Color = x, _originalRectColor, _hoverDuration)
                .SetEase(Ease.OutCubic);
            DOTween.To(() => _rect.DashSpacing, x => _rect.DashSpacing = x, _dashSpacing, 0.2f)
                .SetEase(Ease.OutCubic);
            DOTween.To(() => _rect.Thickness, x => _rect.Thickness = x, _originalThickness * _hoverThicknessMultiplier, _hoverDuration)
                .SetEase(Ease.OutCubic);
        }

        DOTween.To(() => _currentSizeOffset, x => _currentSizeOffset = x, _hoverSizeOffset, _hoverDuration)
            .SetEase(Ease.OutCubic);

        PlayTypewriter();
    }

    /// <summary>
    /// Description: Transitions background color back to normal.
    /// </summary>
    private void AnimateHoverExit()
    {
        KillActiveTweens();

        if (_rect != null)
        {
            Color targetColor = new Color(_originalRectColor.r, _originalRectColor.g, _originalRectColor.b, 0f);
            DOTween.To(() => _rect.Color, x => _rect.Color = x, targetColor, _hoverDuration)
                .SetEase(Ease.OutCubic);
            DOTween.To(() => _rect.DashSpacing, x => _rect.DashSpacing = x, 0f, 0.2f)
                .SetEase(Ease.OutCubic);
            DOTween.To(() => _rect.Thickness, x => _rect.Thickness = x, _originalThickness, _hoverDuration)
                .SetEase(Ease.OutCubic);
        }

        DOTween.To(() => _currentSizeOffset, x => _currentSizeOffset = x, 0f, _hoverDuration)
            .SetEase(Ease.OutCubic);
    }

    #endregion
}
