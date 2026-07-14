using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using Shapes;
using DG.Tweening;
using Febucci.UI.Core;


/// <summary>
/// Description: A custom vector shapes-based UI dropdown component matching the style of UICustomSimpleButton.
/// Context: Attached to dropdown controls in the canvas settings panel.
/// Justification: Provides a high-fidelity dropdown with custom outline animations, dynamic template item generation, typewriter animator integration, and standard EventSystem handlers.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class UICustomDropdown : UICustomButtonBase
{
    #region Nested Classes & Events

    /// <summary>
    /// Description: Serializable event dispatched when the dropdown value changes.
    /// </summary>
    [System.Serializable]
    public class DropdownEvent : UnityEvent<int> { }

    #endregion

    #region Serialized Fields & References

    [Header("Header Shapes References")]
    [Tooltip("Role: The main outline rectangle shape component for the header.\nUse Case: Visual outline.\nJustification: Animated on hover.")]
    [SerializeField]
    private Rectangle _rect;

    [Tooltip("Role: The main background rectangle shape component for the header.\nUse Case: Visual background.\nJustification: Static plain background color.")]
    [SerializeField]
    private Rectangle _backgroundRect;

    [Header("Header Text References")]
    [Tooltip("Role: The TextMeshPro text component for the selected option.\nUse Case: Header label display.\nJustification: Displays the active selection.")]
    [SerializeField]
    private TextMeshProUGUI _buttonText;

    [Header("List Template References")]
    [Tooltip("Role: The container RectTransform for the dropdown list.\nUse Case: Layout containment.\nJustification: Holds the items list and is animated on open/close.")]
    [SerializeField]
    private RectTransform _templateContainer;

    [Tooltip("Role: The item template component inside the list container.\nUse Case: Item instancing.\nJustification: Cloned dynamically to populate option elements.")]
    [SerializeField]
    private UICustomDropdownItem _itemTemplate;

    [Tooltip("Role: The content RectTransform where items are parented.\nUse Case: Item layout layout.\nJustification: Target container for instantiated options.")]
    [SerializeField]
    private RectTransform _itemParent;

    [Tooltip("Role: The outline rectangle shape component for the unfolded list container.\nUse Case: Visual outline.\nJustification: Animated when the dropdown is open.")]
    [SerializeField]
    private Rectangle _listBorder;

    [Header("Header Animation Settings")]
    [Tooltip("Role: Duration of header hover transitions.")]
    [SerializeField]
    private float _hoverDuration = 0.25f;

    [Tooltip("Role: Size offset added to the header on hover.")]
    [SerializeField]
    private float _hoverSizeOffset = 8f;

    [Tooltip("Role: Thickness multiplier for the header border on hover.")]
    [SerializeField]
    private float _hoverThicknessMultiplier = 1.5f;

    [Tooltip("Role: The size of the dashes when hovered.")]
    [SerializeField]
    private float _dashSize = 4f;

    [Tooltip("Role: The spacing between dashes when hovered.")]
    [SerializeField]
    private float _dashSpacing = 4f;

    [Tooltip("Role: Speed of the infinite rotation animation of the dashed outline.")]
    [SerializeField]
    private float _dashRotationSpeed = 1.5f;

    [Header("List Animation Settings")]
    [Tooltip("Role: Duration of the dropdown opening/closing transitions.")]
    [SerializeField]
    private float _animationDuration = 0.25f;

    [Tooltip("Role: Thickness multiplier for the list border when opened.")]
    [SerializeField]
    private float _listHoverThicknessMultiplier = 1.5f;

    [Tooltip("Role: The size of the list border dashes.")]
    [SerializeField]
    private float _listDashSize = 4f;

    [Tooltip("Role: The spacing between list border dashes.")]
    [SerializeField]
    private float _listDashSpacing = 4f;

    [Tooltip("Role: Speed of the infinite rotation of the list border dashes.")]
    [SerializeField]
    private float _listDashRotationSpeed = 1.5f;

    [Header("Arrow Animation Settings")]
    [Tooltip("Role: The first Line component forming the arrow chevron.\nUse Case: Vector chevron morph.\nJustification: Target of start/end point tweens.")]
    [SerializeField]
    private Line _arrowLine1;

    [Tooltip("Role: The second Line component forming the arrow chevron.\nUse Case: Vector chevron morph.\nJustification: Target of start/end point tweens.")]
    [SerializeField]
    private Line _arrowLine2;

    [Tooltip("Role: The X size parameter for the arrow lines.\nUse Case: Chevron arrow width.\nJustification: Configures arrow width.")]
    [SerializeField]
    private float _arrowLineSizeX = 17f;

    [Tooltip("Role: The Y size parameter for the arrow lines.\nUse Case: Chevron arrow height.\nJustification: Configures arrow height.")]
    [SerializeField]
    private float _arrowLineSizeY = 17f;

    [Tooltip("Role: The parent RectTransform of the arrow lines.\nUse Case: Chevron arrow group containment.\nJustification: Target of vertical layout translation animations.")]
    [SerializeField]
    private RectTransform _arrowParent;

    [Tooltip("Role: Vertical position offset for the arrow parent.\nUse Case: Offset translation between open/closed states.\nJustification: Moves the chevron parent up or down relative to baseline.")]
    [SerializeField]
    private float _arrowParentOffsetY = 5f;

    [Tooltip("Role: Duration of the arrow transition animation.\nUse Case: Open/Close morph animation speed.\nJustification: Configurable speed.")]
    [SerializeField]
    private float _arrowAnimDuration = 0.25f;

    [Tooltip("Role: Animation curve (ease) for the arrow transition.\nUse Case: Open/Close morph animation curve.\nJustification: Configurable curve.")]
    [SerializeField]
    private Ease _arrowAnimEase = Ease.OutCubic;

    [Header("Events")]
    [Tooltip("Role: Event dispatched when the selection changes.")]
    [SerializeField]
    private DropdownEvent _onValueChanged = new DropdownEvent();

    #endregion

    [Header("Dropdown Configuration")]
    [Tooltip("Role: The list of option string values displayed in the dropdown menu.\nUse Case: Inspector populator.\nJustification: Allows designers to define options in Edit Mode.")]
    [SerializeField]
    private List<string> _options = new List<string>();

    [Tooltip("Role: The currently selected option index.\nUse Case: Value tracking.\nJustification: Editable in Inspector, clamped to valid options.")]
    [SerializeField]
    private int _value = 0;

    #region Private Fields

    private bool _isListOpen = false;

    // Original states cache
    private float _originalThickness;
    private Color _originalRectColor;
    private Vector3 _originalRectLocalScale;
    private Vector3 _originalTextScale;

    private float _originalListBorderThickness;
    private Color _originalListBorderColor;
    private float _originalArrowParentY;

    private float _currentSizeOffset = 0f;
    private float _currentDashOffset = 0f;
    private float _currentListDashOffset = 0f;
    private bool _isCached = false;
    private RectTransform _rectTransform;

    private TypewriterCore _textAnimatorPlayer;
    private List<UICustomDropdownItem> _instantiatedItems = new List<UICustomDropdownItem>();
    private bool _isListHovered = false;

    #endregion

    #region Public Properties

    /// <summary>
    /// Description: Gets the header border rectangle component.
    /// </summary>
    public Rectangle Rect => _rect;

    /// <summary>
    /// Description: Gets the list border rectangle component.
    /// </summary>
    public Rectangle ListBorder => _listBorder;

    /// <summary>
    /// Description: Gets or sets the list of options.
    /// </summary>
    public List<string> options
    {
        get => _options;
        set
        {
            _options = value;
            if (_options == null)
            {
                _options = new List<string>();
            }
        }
    }

    /// <summary>
    /// Description: Gets or sets the currently selected option index.
    /// </summary>
    public int value
    {
        get => _value;
        set => SetValue(value, notify: false);
    }

    /// <summary>
    /// Description: Gets the selection value changed event.
    /// </summary>
    public DropdownEvent onValueChanged
    {
        get => _onValueChanged;
        set => _onValueChanged = value;
    }

    /// <summary>
    /// Description: Gets whether the dropdown list is currently open.
    /// </summary>
    public bool IsListOpen => _isListOpen;

    #endregion

    #region Unity Lifecycle Callbacks

    /// <summary>
    /// Description: Unity Awake callback. Fetches standard references.
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        _rectTransform = GetComponent<RectTransform>();

        // Ensure the template container is closed at startup
        if (_templateContainer != null && Application.isPlaying)
        {
            _templateContainer.gameObject.SetActive(false);
            _templateContainer.localScale = new Vector3(1f, 0f, 1f);
        }
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
    /// Description: Unity Update callback. Updates layout dimensions and rotates active dashed borders.
    /// </summary>
    private void Update()
    {
        UpdateDimensions();

        #if UNITY_EDITOR
        if (!Application.isPlaying) return;
        #endif

        // Rotate header dashed border on hover
        if (IsHovered && _rect != null && _rect.Dashed)
        {
            _currentDashOffset += Time.deltaTime * _dashRotationSpeed;
            _currentDashOffset %= 1.0f;
            if (_currentDashOffset < 0f) _currentDashOffset += 1.0f;
            _rect.DashOffset = _currentDashOffset;
        }

        // Rotate list dashed border while open and hovered
        if (_isListOpen && _listBorder != null && _listBorder.Dashed)
        {
            _currentListDashOffset += Time.deltaTime * _listDashRotationSpeed;
            _currentListDashOffset %= 1.0f;
            if (_currentListDashOffset < 0f) _currentListDashOffset += 1.0f;
            _listBorder.DashOffset = _currentListDashOffset;
        }

        // Detect mouse hover over the list template container
        if (_isListOpen && _listBorder != null)
        {
            bool isOver = IsMouseOverTemplate();
            if (isOver != _isListHovered)
            {
                _isListHovered = isOver;
                AnimateListHover(_isListHovered);
            }
        }

        // Close dropdown when clicking outside
        if (_isListOpen && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (!IsMouseOverHeader() && !IsMouseOverTemplate())
            {
                CloseDropdown();
            }
        }
    }

    #if UNITY_EDITOR
    /// <summary>
    /// Description: Unity OnValidate callback. Clamps selection index and updates header text inside the editor.
    /// Context: Editor layout synchronization.
    /// Justification: Ensures immediate visual feedback when modifying fields in the Inspector.
    /// </summary>
    protected virtual void OnValidate()
    {
        if (_options == null || _options.Count == 0)
        {
            _value = -1;
        }
        else
        {
            _value = Mathf.Clamp(_value, 0, _options.Count - 1);
        }

        if (_buttonText != null)
        {
            if (_value >= 0 && _value < _options.Count)
            {
                _buttonText.text = _options[_value];
            }
            else
            {
                _buttonText.text = string.Empty;
            }
        }

        UpdateDimensions();

        // Snap arrow configuration in editor
        AnimateArrow(false);
    }
    #endif

    /// <summary>
    /// Description: Unity OnEnable callback.
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
    /// Description: Unity OnDisable callback. Cleans up active tweens, the blocker, and list instances.
    /// </summary>
    protected override void OnDisable()
    {
        base.OnDisable();

        #if UNITY_EDITOR
        if (!Application.isPlaying) return;
        #endif

        KillActiveTweens();
        ClearInstantiatedItems();
        _isListOpen = false;
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
    /// Description: Handles dropdown opening/toggling click event.
    /// </summary>
    public override void OnPointerClick(PointerEventData eventData)
    {
        if (!Interactable) return;
        base.OnPointerClick(eventData);

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            ToggleDropdown();
        }
    }

    #endregion

    #region Public Operations

    /// <summary>
    /// Description: Clears the list of options and updates the selection index.
    /// </summary>
    public void ClearOptions()
    {
        _options.Clear();
        _value = -1;
        if (_buttonText != null)
        {
            _buttonText.text = string.Empty;
        }
    }

    /// <summary>
    /// Description: Adds new options to the dropdown.
    /// </summary>
    /// <param name="newOptions">List of option strings to append.</param>
    public void AddOptions(List<string> newOptions)
    {
        if (newOptions == null) return;
        _options.AddRange(newOptions);

        if (_options.Count > 0)
        {
            // Select first option by default if nothing is selected yet
            if (_value < 0)
            {
                SetValue(0, notify: false);
            }
            else
            {
                // Refresh existing selection display
                SetValue(_value, notify: false);
            }
        }
    }

    /// <summary>
    /// Description: Explicitly sets the dropdown selected index.
    /// </summary>
    /// <param name="newValue">The index value.</param>
    /// <param name="notify">If true, dispatches the onValueChanged event.</param>
    public void SetValue(int newValue, bool notify = true)
    {
        if (_options == null || _options.Count == 0)
        {
            _value = -1;
            if (_buttonText != null)
            {
                _buttonText.text = string.Empty;
            }
            return;
        }

        _value = Mathf.Clamp(newValue, 0, _options.Count - 1);

        if (_buttonText != null)
        {
            _buttonText.text = _options[_value];
        }

        // Relaunch the typewriter animation on selection update
        if (_textAnimatorPlayer != null && Application.isPlaying)
        {
            _textAnimatorPlayer.StartShowingText(true);
        }

        if (notify)
        {
            _onValueChanged.Invoke(_value);
        }
    }

    /// <summary>
    /// Description: Called by custom dropdown items when clicked.
    /// </summary>
    /// <param name="index">The selected item index.</param>
    public void SelectItem(int index)
    {
        SetValue(index, notify: true);
        CloseDropdown();
    }

    #endregion

    #region Dropdown Open/Close Controller

    /// <summary>
    /// Description: Toggles the open/closed state of the dropdown list container.
    /// </summary>
    public void ToggleDropdown()
    {
        if (_isListOpen)
        {
            CloseDropdown();
        }
        else
        {
            OpenDropdown();
        }
    }

    /// <summary>
    /// Description: Opens the dropdown list container, spawning items dynamically.
    /// </summary>
    public void OpenDropdown()
    {
        if (_isListOpen) return;
        _isListOpen = true;

        if (_templateContainer == null) return;

        // Clean previous runs
        ClearInstantiatedItems();

        // Instantiate item instances based on current options
        if (_itemTemplate != null && _itemParent != null)
        {
            // Ensure the template itself is disabled
            _itemTemplate.gameObject.SetActive(false);

            for (int i = 0; i < _options.Count; i++)
            {
                UICustomDropdownItem clone = Instantiate(_itemTemplate, _itemParent);
                clone.gameObject.SetActive(true);
                clone.Setup(this, i, _options[i]);
                _instantiatedItems.Add(clone);
            }
        }

        // Animate the template opening transition (OutCubic scale Y unfolding)
        _templateContainer.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        UpdateDimensions();
        _templateContainer.DOKill();
        _templateContainer.localScale = new Vector3(1f, 0f, 1f);
        _templateContainer.DOScaleY(1f, _animationDuration).SetEase(Ease.OutCubic);

        // Animate arrow transition to open
        AnimateArrow(true);

        // Initialize list border outline visuals to solid (no dashes) by default
        if (_listBorder != null)
        {
            DOTween.Kill(_listBorder);
            _listBorder.Dashed = true;
            _listBorder.DashType = DashType.Basic;
            _listBorder.DashSize = _listDashSize;
            _listBorder.DashSpacing = 0f;
            _listBorder.Thickness = _originalListBorderThickness;
            _listBorder.Color = _originalListBorderColor;
        }
        _isListHovered = false;

        // Trigger typewriter effects on all opened items for tech aesthetic
        foreach (var item in _instantiatedItems)
        {
            item.PlayTypewriter();
        }
    }

    /// <summary>
    /// Description: Closes the dropdown list container.
    /// </summary>
    public void CloseDropdown()
    {
        if (!_isListOpen) return;
        _isListOpen = false;

        // Animate arrow transition to closed
        AnimateArrow(false);

        if (_templateContainer == null) return;

        // Animate template Y-scale collapse
        _templateContainer.DOKill();
        _templateContainer.DOScaleY(0f, _animationDuration)
            .SetEase(Ease.InCubic)
            .OnComplete(() =>
            {
                _templateContainer.gameObject.SetActive(false);
                ClearInstantiatedItems();
            });

        // Collapse list border outline animations
        if (_listBorder != null)
        {
            DOTween.Kill(_listBorder);
            DOTween.To(() => _listBorder.DashSpacing, x => _listBorder.DashSpacing = x, 0f, _animationDuration)
                .SetEase(Ease.InCubic);
            DOTween.To(() => _listBorder.Thickness, x => _listBorder.Thickness = x, _originalListBorderThickness, _animationDuration)
                .SetEase(Ease.InCubic);
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
            
            // Query typewriter component specifically matching the header text gameobject
            _textAnimatorPlayer = _buttonText.GetComponent<TypewriterCore>();
            if (_textAnimatorPlayer == null)
            {
                _textAnimatorPlayer = _buttonText.GetComponentInChildren<TypewriterCore>(true);
            }
        }

        if (_listBorder != null)
        {
            _originalListBorderThickness = _listBorder.Thickness;
            _originalListBorderColor = _listBorder.Color;
        }

        if (_arrowParent != null)
        {
            _originalArrowParentY = _arrowParent.anchoredPosition.y;
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
        _currentListDashOffset = 0f;

        if (_rect != null)
        {
            _rect.Dashed = true;
            _rect.DashType = DashType.Basic;
            _rect.DashSize = _dashSize;
            _rect.DashSpacing = 0f;
            _rect.Thickness = _originalThickness;
            
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

        if (_listBorder != null)
        {
            _listBorder.Dashed = false;
            _listBorder.Thickness = _originalListBorderThickness;
            _listBorder.Color = _originalListBorderColor;
        }

        // Initialize arrow state to closed
        AnimateArrow(false);
    }

    /// <summary>
    /// Description: Synchronizes the Shapes Rectangle width/height with the parent RectTransform and the template container.
    /// </summary>
    private void UpdateDimensions()
    {
        if (_rectTransform == null)
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        if (_rectTransform != null)
        {
            float baseWidth = _rectTransform.rect.width;
            float baseHeight = _rectTransform.rect.height;

            float thicknessDelta = 0f;
            if (_isCached && Application.isPlaying && _rect != null)
            {
                thicknessDelta = _rect.Thickness - _originalThickness;
            }

            // Sync main border rectangle
            if (_rect != null)
            {
                _rect.Width = baseWidth + thicknessDelta + _currentSizeOffset;
                _rect.Height = baseHeight + thicknessDelta + _currentSizeOffset;
            }

            // Sync static header background rectangle
            if (_backgroundRect != null)
            {
                _backgroundRect.Width = baseWidth;
                _backgroundRect.Height = baseHeight;
            }

            // Sync template list border rectangle
            if (_templateContainer != null && _listBorder != null)
            {
                #if UNITY_EDITOR
                if (!Application.isPlaying && _itemParent != null)
                {
                    Canvas.ForceUpdateCanvases();
                }
                #endif

                // Auto update template container height based on item parent height (which has ContentSizeFitter)
                if (_itemParent != null)
                {
                    float targetHeight = _itemParent.rect.height + 8f;
                    _templateContainer.sizeDelta = new Vector2(_templateContainer.sizeDelta.x, targetHeight);
                }

                float listThicknessDelta = 0f;
                if (_isCached && Application.isPlaying)
                {
                    listThicknessDelta = _listBorder.Thickness - _originalListBorderThickness;
                }

                _listBorder.Width = _templateContainer.rect.width + listThicknessDelta;
                _listBorder.Height = _templateContainer.rect.height + listThicknessDelta;
            }
        }
    }

    /// <summary>
    /// Description: Triggers smooth hover entry animations on the header button area.
    /// </summary>
    private void AnimateHoverEnter()
    {
        KillActiveTweens();

        if (_rect != null)
        {
            _rect.Dashed = true;
            _rect.DashType = DashType.Basic;
            _rect.DashSize = _dashSize;

            DOTween.To(() => _rect.DashSpacing, x => _rect.DashSpacing = x, _dashSpacing, 0.2f)
                .SetEase(Ease.OutCubic);
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

        // Relaunch typewriter on header hover enter
        if (_textAnimatorPlayer != null)
        {
            _textAnimatorPlayer.StartShowingText(true);
        }
    }

    /// <summary>
    /// Description: Triggers smooth hover exit animations on the header button area.
    /// </summary>
    private void AnimateHoverExit()
    {
        KillActiveTweens();

        if (_rect != null)
        {
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
    /// Description: Fades button visuals to greyed out representations when disabled, and restores them when enabled.
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
    /// Description: Transitions interactable color representations.
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
        }
        else
        {
            CloseDropdown();

            _currentSizeOffset = 0f;
            _currentDashOffset = 0f;

            if (_rect != null)
            {
                _rect.DashSpacing = 0f;
                _rect.Thickness = _originalThickness;
                _rect.transform.localScale = _originalRectLocalScale;
                DOTween.To(() => _rect.Color, x => _rect.Color = x, new Color(0.3f, 0.3f, 0.3f, 0.2f), duration).SetEase(Ease.OutQuad);
            }

            if (_buttonText != null)
            {
                _buttonText.transform.localScale = _originalTextScale;
                _buttonText.DOColor(new Color(0.5f, 0.5f, 0.5f, 0.4f), duration).SetEase(Ease.OutQuad);
            }
        }
    }

    /// <summary>

    /// <summary>
    /// Description: Destroys all dynamically created dropdown option item GameObjects.
    /// </summary>
    private void ClearInstantiatedItems()
    {
        foreach (var item in _instantiatedItems)
        {
            if (item != null)
            {
                #if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(item.gameObject);
                    continue;
                }
                #endif
                Destroy(item.gameObject);
            }
        }
        _instantiatedItems.Clear();
    }

    /// <summary>
    /// Description: Stops all active tweens on the header and list border elements.
    /// </summary>
    private void KillActiveTweens()
    {
        if (_rect != null)
        {
            DOTween.Kill(_rect);
            _rect.transform.DOKill();
        }

        if (_listBorder != null)
        {
            DOTween.Kill(_listBorder);
        }

        if (_buttonText != null)
        {
            _buttonText.transform.DOKill();
            _buttonText.DOKill();
        }
    }

    /// <summary>
    /// Description: Checks if the mouse is physically over the dropdown template list container using the MouseManager.
    /// </summary>
    private bool IsMouseOverTemplate()
    {
        if (_templateContainer == null || !_templateContainer.gameObject.activeInHierarchy)
            return false;

        Vector2 mousePos = MouseManager.Instance != null ? MouseManager.Instance.MousePosition : (Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero);
        return RectTransformUtility.RectangleContainsScreenPoint(_templateContainer, mousePos, GetComponentInParent<Canvas>().worldCamera);
    }

    /// <summary>
    /// Description: Checks if the mouse is physically over the dropdown header area using the MouseManager.
    /// </summary>
    private bool IsMouseOverHeader()
    {
        if (_rectTransform == null)
        {
            _rectTransform = GetComponent<RectTransform>();
        }
        if (_rectTransform == null) return false;

        Vector2 mousePos = MouseManager.Instance != null ? MouseManager.Instance.MousePosition : (Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero);
        return RectTransformUtility.RectangleContainsScreenPoint(_rectTransform, mousePos, GetComponentInParent<Canvas>().worldCamera);
    }

    /// <summary>
    /// Description: Smoothly animates the list border dashes spacing when the list is hovered.
    /// </summary>
    private void AnimateListHover(bool hovered)
    {
        if (_listBorder == null) return;

        DOTween.Kill(_listBorder);

        if (hovered)
        {
            _listBorder.Dashed = true;
            _listBorder.DashType = DashType.Basic;
            _listBorder.DashSize = _listDashSize;

            DOTween.To(() => _listBorder.DashSpacing, x => _listBorder.DashSpacing = x, _listDashSpacing, 0.2f)
                .SetEase(Ease.OutCubic);
            DOTween.To(() => _listBorder.Thickness, x => _listBorder.Thickness = x, _originalListBorderThickness * _listHoverThicknessMultiplier, 0.2f)
                .SetEase(Ease.OutCubic);
        }
        else
        {
            DOTween.To(() => _listBorder.DashSpacing, x => _listBorder.DashSpacing = x, 0f, 0.2f)
                .SetEase(Ease.OutCubic);
            DOTween.To(() => _listBorder.Thickness, x => _listBorder.Thickness = x, _originalListBorderThickness, 0.2f)
                .SetEase(Ease.OutCubic);
        }
    }

    /// <summary>
    /// Description: Morph-animates the arrow lines between open and closed chevron state coordinates.
    /// </summary>
    private void AnimateArrow(bool open)
    {
        if (_arrowLine1 == null || _arrowLine2 == null) return;

        DOTween.Kill(_arrowLine1);
        DOTween.Kill(_arrowLine2);

        Vector3 targetLine1Start = open ? new Vector3(0f, 0f, 0f) : new Vector3(-_arrowLineSizeX, _arrowLineSizeY, 0f);
        Vector3 targetLine1End = open ? new Vector3(_arrowLineSizeX, -_arrowLineSizeY, 0f) : new Vector3(0f, 0f, 0f);
        Vector3 targetLine2Start = open ? new Vector3(0f, 0f, 0f) : new Vector3(_arrowLineSizeX, _arrowLineSizeY, 0f);
        Vector3 targetLine2End = open ? new Vector3(-_arrowLineSizeX, -_arrowLineSizeY, 0f) : new Vector3(0f, 0f, 0f);

        if (Application.isPlaying)
        {
            DOTween.To(() => _arrowLine1.Start, x => _arrowLine1.Start = x, targetLine1Start, _arrowAnimDuration)
                .SetEase(_arrowAnimEase);
            DOTween.To(() => _arrowLine1.End, x => _arrowLine1.End = x, targetLine1End, _arrowAnimDuration)
                .SetEase(_arrowAnimEase);

            DOTween.To(() => _arrowLine2.Start, x => _arrowLine2.Start = x, targetLine2Start, _arrowAnimDuration)
                .SetEase(_arrowAnimEase);
            DOTween.To(() => _arrowLine2.End, x => _arrowLine2.End = x, targetLine2End, _arrowAnimDuration)
                .SetEase(_arrowAnimEase);

            if (_arrowParent != null)
            {
                _arrowParent.DOKill();
                float targetY = open ? (_originalArrowParentY + _arrowParentOffsetY) : (_originalArrowParentY - _arrowParentOffsetY);
                _arrowParent.DOAnchorPosY(targetY, _arrowAnimDuration)
                    .SetEase(_arrowAnimEase);
            }
        }
        else
        {
            _arrowLine1.Start = targetLine1Start;
            _arrowLine1.End = targetLine1End;
            _arrowLine2.Start = targetLine2Start;
            _arrowLine2.End = targetLine2End;
        }
    }

    #endregion
}
