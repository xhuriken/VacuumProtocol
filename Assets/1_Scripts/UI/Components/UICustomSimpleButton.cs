using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using Shapes;
using DG.Tweening;

/// <summary>
/// Description: A simple custom shape-based UI button that matches its RectTransform size automatically.
/// Context: Attached to UI Button objects in the canvas to provide a modular, reusable button style.
/// Justification: Features a dynamic size-sync system, premium DOTween hover/click animations, and infinite dash rotation.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class UICustomSimpleButton : UICustomButtonBase
{
    #region Serialized Fields & References

    [Header("Shapes References")]
    [Tooltip("Role: The main container rectangle shape component.\nUse Case: Visuals.\nJustification: Forms the body and border of the simple button.")]
    [SerializeField]
    private Rectangle _rect;

    [Header("Text References (Optional)")]
    [Tooltip("Role: The TextMeshPro text component.\nUse Case: Displaying the button label.\nJustification: Optional text child for labelling.")]
    [SerializeField]
    private TextMeshProUGUI _buttonText;

    [Header("Animation Settings")]
    [Tooltip("Role: Duration of hover transitions.")]
    [SerializeField]
    private float _hoverDuration = 0.25f;

    [Tooltip("Role: Extra size offset in pixels added on hover to grow the button.")]
    [SerializeField]
    private float _hoverSizeOffset = 8f;

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

    #endregion

    #region Private Fields

    private float _originalThickness;
    private Color _originalRectColor;
    private Vector3 _originalRectLocalScale;
    private Vector3 _originalTextScale;

    private float _currentSizeOffset = 0f;
    private float _currentDashOffset = 0f;
    private bool _isCached = false;
    private RectTransform _rectTransform;
    private Sequence _clickFlashSequence;

    #endregion

    #region Public Properties

    /// <summary>
    /// Description: Gets the main container rectangle shape component.
    /// </summary>
    public Rectangle Rect => _rect;

    /// <summary>
    /// Description: Gets the optional text component.
    /// </summary>
    public TextMeshProUGUI ButtonText => _buttonText;

    #endregion

    #region Unity Lifecycle Callbacks

    /// <summary>
    /// Description: Unity Awake callback.
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        _rectTransform = GetComponent<RectTransform>();
    }

    /// <summary>
    /// Description: Unity Start callback.
    /// </summary>
    protected virtual void Start()
    {
        _rectTransform = GetComponent<RectTransform>();
        
        #if UNITY_EDITOR
        if (!Application.isPlaying) return;
        #endif

        CacheOriginalStates();
        InitializeDefaultVisuals();
    }

    /// <summary>
    /// Description: Unity Update callback. Handles size synchronization in editor/play mode and infinite dash rotation.
    /// </summary>
    private void Update()
    {
        UpdateRectSize();

        #if UNITY_EDITOR
        if (!Application.isPlaying) return;
        #endif

        // Animate the dash offset infinitely on hover if the rectangle is dashed
        if (IsHovered && _rect != null && _rect.Dashed)
        {
            _currentDashOffset += Time.deltaTime * _dashRotationSpeed;
            _currentDashOffset %= 1.0f;
            if (_currentDashOffset < 0f)
            {
                _currentDashOffset += 1.0f;
            }
            _rect.DashOffset = _currentDashOffset;
        }
    }

    /// <summary>
    /// Description: Unity OnEnable callback. Resets visuals when re-enabled.
    /// </summary>
    private void OnEnable()
    {
        #if UNITY_EDITOR
        if (!Application.isPlaying) return;
        #endif

        if (_isCached)
        {
            InitializeDefaultVisuals();
        }
    }

    /// <summary>
    /// Description: Automatically cleans up active tweens when disabled.
    /// </summary>
    protected override void OnDisable()
    {
        base.OnDisable();
        
        #if UNITY_EDITOR
        if (!Application.isPlaying) return;
        #endif

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
    /// Description: Handles press down transitions.
    /// </summary>
    public override void OnPointerDown(PointerEventData eventData)
    {
        if (!Interactable) return;
        base.OnPointerDown(eventData);
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            AnimateClick();
        }
    }

    #endregion

    #region Helpers & Animations

    /// <summary>
    /// Description: Caches original values to ensure accurate resets during animations.
    /// </summary>
    private void CacheOriginalStates()
    {
        if (_rect != null)
        {
            _originalThickness = _rect.Thickness;
            _originalRectColor = _rect.Color;
            _originalRectLocalScale = _rect.transform.localScale;
        }

        if (_buttonText != null)
        {
            _originalTextScale = _buttonText.transform.localScale;
        }

        _isCached = true;
    }

    /// <summary>
    /// Description: Sets the components to their default visual configurations.
    /// </summary>
    private void InitializeDefaultVisuals()
    {
        _currentSizeOffset = 0f;
        _currentDashOffset = 0f;

        if (_rect != null)
        {
            _rect.Dashed = true; // Always dashed in play mode, controlled by DashSpacing
            _rect.DashType = DashType.Basic;
            _rect.DashSize = _dashSize;
            _rect.DashSpacing = 0f; // Start as solid line (no space between dashes)
            _rect.Thickness = _originalThickness;
            
            // Set correct color based on current interactable state
            if (Interactable)
            {
                _rect.Color = _originalRectColor;
            }
            else
            {
                _rect.Color = new Color(0.3f, 0.3f, 0.3f, 0.2f);
            }
            _rect.transform.localScale = _originalRectLocalScale;
        }

        if (_buttonText != null)
        {
            _buttonText.transform.localScale = _originalTextScale;
            if (Interactable)
            {
                _buttonText.color = Color.white;
            }
            else
            {
                _buttonText.color = new Color(0.5f, 0.5f, 0.5f, 0.4f);
            }
        }
    }

    /// <summary>
    /// Description: Synchronizes the Shapes Rectangle width/height with the parent RectTransform, adjusting for hover growth.
    /// </summary>
    private void UpdateRectSize()
    {
        if (_rectTransform == null)
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        if (_rectTransform != null && _rect != null)
        {
            float baseWidth = _rectTransform.rect.width;
            float baseHeight = _rectTransform.rect.height;

            float thicknessDelta = 0f;
            if (_isCached && Application.isPlaying)
            {
                thicknessDelta = _rect.Thickness - _originalThickness;
            }

            _rect.Width = baseWidth + thicknessDelta + _currentSizeOffset;
            _rect.Height = baseHeight + thicknessDelta + _currentSizeOffset;
        }
    }

    /// <summary>
    /// Description: Triggers smooth hover entry animations.
    /// </summary>
    private void AnimateHoverEnter()
    {
        KillActiveTweens();

        if (_rect != null)
        {
            _rect.Dashed = true;
            _rect.DashType = DashType.Basic;
            _rect.DashSize = _dashSize;

            // Animate dash spacing smoothly from 0 (or current) to _dashSpacing in 0.2s
            DOTween.To(() => _rect.DashSpacing, x => _rect.DashSpacing = x, _dashSpacing, 0.2f)
                .SetEase(Ease.OutCubic);

            // Animate thickness and outer growth offset
            DOTween.To(() => _rect.Thickness, x => _rect.Thickness = x, _originalThickness * _hoverThicknessMultiplier, _hoverDuration)
                .SetEase(Ease.OutCubic);
        }

        DOTween.To(() => _currentSizeOffset, x => _currentSizeOffset = x, _hoverSizeOffset, _hoverDuration)
            .SetEase(Ease.OutCubic);

        if (_buttonText != null)
        {
            _buttonText.transform.DOScale(_originalTextScale * 1.05f, _hoverDuration)
                .SetEase(Ease.OutCubic);
        }
    }

    /// <summary>
    /// Description: Triggers smooth hover exit animations.
    /// </summary>
    private void AnimateHoverExit()
    {
        KillActiveTweens();

        if (_rect != null)
        {
            // Animate dash spacing smoothly back to 0 in 0.2s
            DOTween.To(() => _rect.DashSpacing, x => _rect.DashSpacing = x, 0f, 0.2f)
                .SetEase(Ease.OutCubic);

            DOTween.To(() => _rect.Thickness, x => _rect.Thickness = x, _originalThickness, _hoverDuration)
                .SetEase(Ease.OutCubic);
        }

        DOTween.To(() => _currentSizeOffset, x => _currentSizeOffset = x, 0f, _hoverDuration)
            .SetEase(Ease.OutCubic);

        if (_buttonText != null)
        {
            _buttonText.transform.DOScale(_originalTextScale, _hoverDuration)
                .SetEase(Ease.OutCubic);
        }
    }

    /// <summary>
    /// Description: Triggers a stunning flash and scale sequence when clicked.
    /// </summary>
    private void AnimateClick()
    {
        if (_rect != null)
        {
            _rect.transform.DOKill();
            DOTween.Kill(_rect);
        }

        if (_clickFlashSequence != null && _clickFlashSequence.IsActive())
        {
            _clickFlashSequence.Kill();
        }

        if (_rect != null)
        {
            _rect.transform.localScale = _originalRectLocalScale;
            _rect.Color = _originalRectColor;
        }

        _clickFlashSequence = DOTween.Sequence();

        if (_rect != null)
        {
            // Rapid Scale Pulse
            _clickFlashSequence.Append(_rect.transform.DOScale(_originalRectLocalScale * 1.15f, 0.03f).SetEase(Ease.OutQuad));
            _clickFlashSequence.Append(_rect.transform.DOScale(_originalRectLocalScale, 0.12f).SetEase(Ease.OutCubic));

            // Instant bloom peak (Color white)
            _clickFlashSequence.Join(DOTween.To(() => _rect.Color, x => _rect.Color = x, Color.white, 0.02f).SetEase(Ease.OutQuad));

            // Ultra fast blackout
            _clickFlashSequence.Append(DOTween.To(() => _rect.Color, x => _rect.Color = x, new Color(_originalRectColor.r, _originalRectColor.g, _originalRectColor.b, 0f), 0.04f).SetEase(Ease.InQuad));

            // Holographic flicker return
            Color lowFlicker = new Color(_originalRectColor.r, _originalRectColor.g, _originalRectColor.b, 0.2f);
            Color midFlicker = new Color(_originalRectColor.r, _originalRectColor.g, _originalRectColor.b, 0.6f);
            Color highFlicker = new Color(_originalRectColor.r, _originalRectColor.g, _originalRectColor.b, 0.8f);

            _clickFlashSequence.Append(DOTween.To(() => _rect.Color, x => _rect.Color = x, midFlicker, 0.015f));
            _clickFlashSequence.Append(DOTween.To(() => _rect.Color, x => _rect.Color = x, lowFlicker, 0.015f));
            _clickFlashSequence.Append(DOTween.To(() => _rect.Color, x => _rect.Color = x, highFlicker, 0.015f));
            _clickFlashSequence.Append(DOTween.To(() => _rect.Color, x => _rect.Color = x, lowFlicker, 0.015f));

            // Settle back to normal color
            _clickFlashSequence.Append(DOTween.To(() => _rect.Color, x => _rect.Color = x, _originalRectColor, 0.08f).SetEase(Ease.OutQuad));
        }
    }

    /// <summary>
    /// Description: Custom visual transition triggered when the interactable state changes.
    /// </summary>
    protected override void OnInteractableChanged(bool isInteractable)
    {
        base.OnInteractableChanged(isInteractable);
        
        #if UNITY_EDITOR
        if (!Application.isPlaying) return;
        #endif

        KillActiveTweens();
        AnimateInteractableTransition(isInteractable);
    }

    /// <summary>
    /// Description: Fades button visuals to greyed out representations when disabled, and restores them when enabled.
    /// </summary>
    private void AnimateInteractableTransition(bool isInteractable)
    {
        if (!_isCached) return;

        float duration = 0.25f;

        if (isInteractable)
        {
            if (_buttonText != null)
            {
                _buttonText.DOColor(Color.white, duration).SetEase(Ease.OutQuad);
            }

            if (_rect != null)
            {
                DOTween.To(() => _rect.Color, x => _rect.Color = x, _originalRectColor, duration).SetEase(Ease.OutQuad);
            }

            // Sync visual states with actual hover state immediately on re-enable
            if (IsHovered)
            {
                AnimateHoverEnter();
            }
            else
            {
                AnimateHoverExit();
            }
        }
        else
        {
            // Reset hover state variables immediately when disabled to prevent sticking
            _currentSizeOffset = 0f;
            _currentDashOffset = 0f;
            if (_rect != null)
            {
                _rect.DashSpacing = 0f;
                _rect.Thickness = _originalThickness;
                _rect.transform.localScale = _originalRectLocalScale;
            }
            if (_buttonText != null)
            {
                _buttonText.transform.localScale = _originalTextScale;
            }

            Color disabledTextColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);
            Color disabledShapeColor = new Color(0.3f, 0.3f, 0.3f, 0.2f);

            if (_buttonText != null)
            {
                _buttonText.DOColor(disabledTextColor, duration).SetEase(Ease.OutQuad);
            }

            if (_rect != null)
            {
                DOTween.To(() => _rect.Color, x => _rect.Color = x, disabledShapeColor, duration).SetEase(Ease.OutQuad);
            }
        }
    }

    /// <summary>
    /// Description: Safely kills all active tweens.
    /// </summary>
    private void KillActiveTweens()
    {
        if (_rect != null)
        {
            DOTween.Kill(_rect);
            _rect.transform.DOKill();
            if (_isCached)
            {
                _rect.transform.localScale = _originalRectLocalScale;
                
                // Reset to correct color based on current interactable state
                if (Interactable)
                {
                    _rect.Color = _originalRectColor;
                }
                else
                {
                    _rect.Color = new Color(0.3f, 0.3f, 0.3f, 0.2f);
                }
            }
        }

        DOTween.Kill(this);

        if (_buttonText != null)
        {
            _buttonText.transform.DOKill();
        }

        if (_clickFlashSequence != null && _clickFlashSequence.IsActive())
        {
            _clickFlashSequence.Kill();
        }
    }

    #endregion
}
