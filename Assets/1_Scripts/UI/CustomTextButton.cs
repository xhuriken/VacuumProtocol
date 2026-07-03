using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using Shapes;
using DG.Tweening;
using Febucci.UI.Core;

/// <summary>
/// Description: A custom shape-based UI button that supports text and line/dot shape visual structures.
/// Context: Attached to standard UI Button objects in the canvas.
/// Justification: Features high-fidelity DOTween animations, anti-spam execution guards, and Febucci typewriter triggers.
/// </summary>
public class CustomTextButton : UICustomButtonBase
{
    #region Serialized Fields & References

    [Header("Shapes References")]
    [Tooltip("Role: The left decorative line shape component.\nUse Case: Visuals.\nJustification: Animated on hover.")]
    [SerializeField]
    private Line _leftLine;

    [Tooltip("Role: The main container rectangle shape component.\nUse Case: Visuals.\nJustification: Forms the core button body.")]
    [SerializeField]
    private Rectangle _rect;

    [Tooltip("Role: The decorative dots shape component/parent.\nUse Case: Visuals.\nJustification: Contains animated child discs for tech aesthetic.")]
    [SerializeField]
    private GameObject _dots;

    [Header("Text References")]
    [Tooltip("Role: The TextMeshPro text component.\nUse Case: Displaying the button label.\nJustification: Required to show what the button does.")]
    [SerializeField]
    private TextMeshProUGUI _buttonText;

    [Header("Animation Durations")]
    [Tooltip("Role: Duration of hover transitions.\nUse Case: Animation pacing.\nJustification: Standardizes how fast the elements react to mouse over.")]
    [SerializeField]
    private float _hoverDuration = 0.3f;

    [Tooltip("Role: Duration of click flash/shimmer transitions.\nUse Case: Animation pacing.\nJustification: Standardizes how fast the elements react to click.")]
    [SerializeField]
    private float _clickDuration = 0.25f;

    #endregion

    #region Private Fields (State Caching & Tween Control)

    private TypewriterCore _textAnimatorPlayer;

    // Cached states for precise animation resets and spam prevention
    private Color _originalLineColor;
    private Vector3 _originalLineEnd;
    
    private Color _originalRectColor;
    private Vector3 _originalRectLocalPos;
    private Vector3 _originalRectLocalScale;
    private float _originalDashOffset;
    
    private Vector3 _originalDotsLocalPos;
    private Vector3 _originalTextLocalPos;

    // Discs cache for Dots micro-animations
    private Disc _mainDisc;
    private Disc[] _childDiscs;
    private Color _originalMainDiscColor;
    private float _originalMainDiscRadius;
    private Color[] _originalChildColors;
    private float[] _originalChildRadii;
    private Vector3[] _originalChildPositions;

    private Sequence _clickFlashSequence;

    #endregion

    #region Public Properties

    /// <summary>
    /// Description: Gets the left line shape component.
    /// Context: Property accessor.
    /// Justification: Allows external scripts to read the Line object.
    /// </summary>
    public Line LeftLine => _leftLine;

    /// <summary>
    /// Description: Gets the main container rectangle shape component.
    /// Context: Property accessor.
    /// Justification: Allows external scripts to read the Rect object.
    /// </summary>
    public Rectangle Rect => _rect;

    /// <summary>
    /// Description: Gets the decorative dots shape component/parent.
    /// Context: Property accessor.
    /// Justification: Allows external scripts to read the Dots GameObject.
    /// </summary>
    public GameObject Dots => _dots;

    /// <summary>
    /// Description: Gets the TextMeshPro text component of the button.
    /// Context: Property accessor.
    /// Justification: Allows external scripts to read the Text object.
    /// </summary>
    public TextMeshProUGUI ButtonText => _buttonText;

    #endregion

    #region Unity Lifecycle Callbacks

    /// <summary>
    /// Description: Unity Awake callback. Performs base validation and fetches the Febucci typewriter player.
    /// Context: Initialization.
    /// Justification: Required to prepare the animated text and child shapes.
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        _textAnimatorPlayer = GetComponentInChildren<TypewriterCore>();

        // Query Disc components inside Dots hierarchy
        if (_dots != null)
        {
            _mainDisc = _dots.GetComponent<Disc>();
            
            Disc[] allDiscs = _dots.GetComponentsInChildren<Disc>(true);
            System.Collections.Generic.List<Disc> childList = new System.Collections.Generic.List<Disc>();
            foreach (Disc d in allDiscs)
            {
                if (d != _mainDisc)
                {
                    childList.Add(d);
                }
            }
            _childDiscs = childList.ToArray();
        }
    }

    /// <summary>
    /// Description: Unity Start callback. Caches default states and initializes the visual element positions/colors.
    /// Context: Initialization.
    /// Justification: Ensures all tweens return to their correct starting values.
    /// </summary>
    protected virtual void Start()
    {
        CacheOriginalStates();
        InitializeDefaultVisuals();
    }

    /// <summary>
    /// Description: Automatically cleans up any residual tweens when disabled.
    /// Context: Unity lifecycle event.
    /// Justification: Prevents execution leaks and visual bugs when the UI is toggled.
    /// </summary>
    protected override void OnDisable()
    {
        base.OnDisable();
        KillActiveTweens();
    }

    #endregion

    #region EventSystem Interface Overrides

    /// <summary>
    /// Description: Handles pointer enter event, triggering hover entry visual animations.
    /// Context: EventSystem callback.
    /// Justification: Fires the complex UI hover effect.
    /// </summary>
    /// <param name="eventData">Pointer event data from UGUI EventSystem.</param>
    public override void OnPointerEnter(PointerEventData eventData)
    {
        if (!Interactable) return;
        base.OnPointerEnter(eventData);
        AnimateHoverEnter();
    }

    /// <summary>
    /// Description: Handles pointer exit event, triggering hover exit visual animations.
    /// Context: EventSystem callback.
    /// Justification: Reverts the complex UI hover effect.
    /// </summary>
    /// <param name="eventData">Pointer event data from UGUI EventSystem.</param>
    public override void OnPointerExit(PointerEventData eventData)
    {
        if (!Interactable) return;
        base.OnPointerExit(eventData);
        AnimateHoverExit();
    }

    /// <summary>
    /// Description: Handles pointer down event, triggering click animations at the exact moment of pressing down.
    /// Context: EventSystem callback.
    /// Justification: Provides immediate visual feedback for the press.
    /// </summary>
    /// <param name="eventData">Pointer event data from UGUI EventSystem.</param>
    public override void OnPointerDown(PointerEventData eventData)
    {
        if (!Interactable) return;
        base.OnPointerDown(eventData);
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            AnimateClick();
        }
    }

    /// <summary>
    /// Description: Handles pointer up event, triggering release visual animations.
    /// Context: EventSystem callback.
    /// Justification: Handles the return state after a press.
    /// </summary>
    /// <param name="eventData">Pointer event data from UGUI EventSystem.</param>
    public override void OnPointerUp(PointerEventData eventData)
    {
        if (!Interactable) return;
        base.OnPointerUp(eventData);
        AnimateRelease();
    }

    /// <summary>
    /// Description: Handles pointer click event. Custom click effects are fired on PointerDown instead for instant tactile feedback.
    /// Context: EventSystem callback.
    /// Justification: Base class handles the actual onClick invocation.
    /// </summary>
    /// <param name="eventData">Pointer event data from UGUI EventSystem.</param>
    public override void OnPointerClick(PointerEventData eventData)
    {
        if (!Interactable) return;
        base.OnPointerClick(eventData);
    }

    #endregion

    #region Caching & Initialization Helpers

    /// <summary>
    /// Description: Caches all default coordinates, offsets, and colors to guarantee stable mathematical transformations.
    /// Context: Called during Start.
    /// Justification: Animations need a baseline to reset to.
    /// </summary>
    private void CacheOriginalStates()
    {
        if (_leftLine != null)
        {
            _originalLineColor = _leftLine.Color;
            _originalLineEnd = _leftLine.End;
        }

        if (_rect != null)
        {
            _originalRectColor = _rect.Color;
            _originalRectLocalPos = _rect.transform.localPosition;
            _originalRectLocalScale = _rect.transform.localScale;
            _originalDashOffset = _rect.DashOffset;
        }

        if (_dots != null)
        {
            _originalDotsLocalPos = _dots.transform.localPosition;
        }

        if (_buttonText != null)
        {
            _originalTextLocalPos = _buttonText.transform.localPosition;
        }

        // Cache Disc components states
        if (_mainDisc != null)
        {
            _originalMainDiscColor = _mainDisc.Color;
            _originalMainDiscRadius = _mainDisc.Radius;
        }

        if (_childDiscs != null && _childDiscs.Length > 0)
        {
            _originalChildColors = new Color[_childDiscs.Length];
            _originalChildRadii = new float[_childDiscs.Length];
            _originalChildPositions = new Vector3[_childDiscs.Length];

            for (int i = 0; i < _childDiscs.Length; i++)
            {
                if (_childDiscs[i] != null)
                {
                    _originalChildColors[i] = _childDiscs[i].Color;
                    _originalChildRadii[i] = _childDiscs[i].Radius;
                    _originalChildPositions[i] = _childDiscs[i].transform.localPosition;
                }
            }
        }
    }

    /// <summary>
    /// Description: Configures the initial visibility states: Rect is invisible, Line is visible, Text is visible.
    /// Context: Called during Start.
    /// Justification: Prepares the button for its default idle look.
    /// </summary>
    private void InitializeDefaultVisuals()
    {
        if (_rect != null)
        {
            // Set Rectangle to fully transparent and set dash offset to its default 0.3f
            _rect.Color = new Color(_originalRectColor.r, _originalRectColor.g, _originalRectColor.b, 0f);
            _rect.DashOffset = 0.3f;
            _rect.transform.localScale = _originalRectLocalScale;
        }

        if (_leftLine != null)
        {
            _leftLine.Color = _originalLineColor;
            _leftLine.End = _originalLineEnd;
        }

        if (_buttonText != null)
        {
            _buttonText.transform.localPosition = _originalTextLocalPos;
            _buttonText.gameObject.SetActive(true);
        }

        // Initialize Discs defaults
        if (_mainDisc != null)
        {
            _mainDisc.Color = _originalMainDiscColor;
            _mainDisc.Radius = _originalMainDiscRadius;
        }

        if (_childDiscs != null)
        {
            for (int i = 0; i < _childDiscs.Length; i++)
            {
                if (_childDiscs[i] != null)
                {
                    _childDiscs[i].Color = _originalChildColors[i];
                    _childDiscs[i].Radius = _originalChildRadii[i];
                    _childDiscs[i].transform.localPosition = _originalChildPositions[i];
                }
            }
        }
    }

    #endregion

    #region Core Tween Animations

    /// <summary>
    /// Description: Animates elements smoothly on hover enter, with full spam tween cancellation.
    /// Context: Called by OnPointerEnter.
    /// Justification: Houses the core DOTween sequence for hover.
    /// </summary>
    private void AnimateHoverEnter()
    {
        // 1. Kill any active tweens to prevent overlapping and memory leaks
        KillActiveTweens();

        // 2. Animate the Left Line: right point collapses to start/left, then fades out
        if (_leftLine != null)
        {
            DOTween.To(() => _leftLine.End, x => _leftLine.End = x, _leftLine.Start, _hoverDuration * 0.8f)
                .SetEase(Ease.OutQuad);

            DOTween.To(() => _leftLine.Color, x => _leftLine.Color = x, new Color(_originalLineColor.r, _originalLineColor.g, _originalLineColor.b, 0f), _hoverDuration * 0.7f)
                .SetEase(Ease.OutQuad);
        }

        // 3. Animate the Rectangle: fade in, shift left by 20 units, and morph DashOffset from 0.3 to 0.2
        if (_rect != null)
        {
            DOTween.To(() => _rect.Color, x => _rect.Color = x, _originalRectColor, _hoverDuration)
                .SetEase(Ease.OutCubic);

            // Shift the rectangle to the LEFT by exactly 20 units
            _rect.transform.DOLocalMove(_originalRectLocalPos + new Vector3(-20f, 0f, 0f), _hoverDuration)
                .SetEase(Ease.OutCubic);

            // Animate rectangle DashOffset to 0.2f
            DOTween.To(() => _rect.DashOffset, x => _rect.DashOffset = x, 0.2f, _hoverDuration)
                .SetEase(Ease.OutCubic);
        }

        // 4. Animate the Dots: shift left by 20 units and start a playful continuous sci-fi orbit spin!
        if (_dots != null)
        {
            _dots.transform.DOLocalMove(_originalDotsLocalPos + new Vector3(-20f, 0f, 0f), _hoverDuration)
                .SetEase(Ease.OutBack);

            // Playful spinning of the dots parent continuously while hovered!
            _dots.transform.DORotate(new Vector3(0f, 0f, 360f), 1.2f, RotateMode.FastBeyond360)
                .SetLoops(-1, LoopType.Incremental)
                .SetEase(Ease.Linear);
        }

        // 5. Animate the Text: shift left by 20 units smoothly
        if (_buttonText != null)
        {
            _buttonText.transform.DOLocalMove(_originalTextLocalPos + new Vector3(-20f, 0f, 0f), _hoverDuration)
                .SetEase(Ease.OutBack);
        }

        // 6. Animate Dots Discs expansion + continuous playful breathing yoyo!
        if (_mainDisc != null)
        {
            DOTween.To(() => _mainDisc.Radius, x => _mainDisc.Radius = x, _originalMainDiscRadius * 1.25f, _hoverDuration)
                .SetEase(Ease.OutCubic);

            DOTween.To(() => _mainDisc.Color, x => _mainDisc.Color = x, _originalMainDiscColor * 1.2f, _hoverDuration)
                .SetEase(Ease.OutCubic);
        }

        if (_childDiscs != null)
        {
            for (int i = 0; i < _childDiscs.Length; i++)
            {
                if (_childDiscs[i] != null)
                {
                    Disc child = _childDiscs[i];
                    Vector3 origPos = _originalChildPositions[i];
                    float origRadius = _originalChildRadii[i];
                    Color origColor = _originalChildColors[i];

                    // Push outward relative to origin
                    Vector3 direction = origPos.normalized;
                    if (direction == Vector3.zero) direction = Vector3.right;

                    child.transform.DOLocalMove(origPos + direction * 0.08f, _hoverDuration)
                        .SetEase(Ease.OutBack);

                    DOTween.To(() => child.Radius, x => child.Radius = x, origRadius * 1.35f, _hoverDuration)
                        .SetEase(Ease.OutCubic)
                        .OnComplete(() =>
                        {
                            // Playful continuous size breathing pulse!
                            DOTween.To(() => child.Radius, x => child.Radius = x, origRadius * 1.6f, 0.35f)
                                .SetEase(Ease.InOutSine)
                                .SetLoops(-1, LoopType.Yoyo);
                        });

                    DOTween.To(() => child.Color, x => child.Color = x, origColor * 1.3f, _hoverDuration)
                        .SetEase(Ease.OutCubic);
                }
            }
        }

        // 7. Relauch the Febucci Text Animator typewriter sequence
        if (_textAnimatorPlayer != null)
        {
            _textAnimatorPlayer.StartShowingText(true);
        }
    }

    /// <summary>
    /// Description: Restores default states smoothly when the hover exits, preventing glitches.
    /// Context: Called by OnPointerExit.
    /// Justification: Houses the core DOTween sequence for hover exit.
    /// </summary>
    private void AnimateHoverExit()
    {
        // 1. Kill active tweens to ensure clean reverse transitions
        KillActiveTweens();

        // 2. Restore the Left Line: restore end point and fade color back to visible
        if (_leftLine != null)
        {
            DOTween.To(() => _leftLine.End, x => _leftLine.End = x, _originalLineEnd, _hoverDuration)
                .SetEase(Ease.OutCubic);

            DOTween.To(() => _leftLine.Color, x => _leftLine.Color = x, _originalLineColor, _hoverDuration)
                .SetEase(Ease.OutQuad);
        }

        // 3. Restore the Rectangle: fade out, return to default position, and restore DashOffset to 0.3
        if (_rect != null)
        {
            DOTween.To(() => _rect.Color, x => _rect.Color = x, new Color(_originalRectColor.r, _originalRectColor.g, _originalRectColor.b, 0f), _hoverDuration)
                .SetEase(Ease.OutQuad);

            _rect.transform.DOLocalMove(_originalRectLocalPos, _hoverDuration)
                .SetEase(Ease.OutCubic);

            // Restore DashOffset back to 0.3f
            DOTween.To(() => _rect.DashOffset, x => _rect.DashOffset = x, 0.3f, _hoverDuration)
                .SetEase(Ease.OutCubic);
        }

        // 4. Restore the Dots: slide back to their starting local position and smoothly realign rotation
        if (_dots != null)
        {
            _dots.transform.DOLocalMove(_originalDotsLocalPos, _hoverDuration)
                .SetEase(Ease.OutQuad);

            // Smoothly rotate back to 0 degrees alignment
            _dots.transform.DORotate(Vector3.zero, _hoverDuration)
                .SetEase(Ease.OutQuad);
        }

        // 5. Restore the Text: slide back to its starting local position
        if (_buttonText != null)
        {
            _buttonText.transform.DOLocalMove(_originalTextLocalPos, _hoverDuration)
                .SetEase(Ease.OutQuad);
        }

        // 6. Restore Dots Discs
        if (_mainDisc != null)
        {
            DOTween.To(() => _mainDisc.Radius, x => _mainDisc.Radius = x, _originalMainDiscRadius, _hoverDuration)
                .SetEase(Ease.OutCubic);

            DOTween.To(() => _mainDisc.Color, x => _mainDisc.Color = x, _originalMainDiscColor, _hoverDuration)
                .SetEase(Ease.OutCubic);
        }

        if (_childDiscs != null)
        {
            for (int i = 0; i < _childDiscs.Length; i++)
            {
                if (_childDiscs[i] != null)
                {
                    Disc child = _childDiscs[i];
                    child.transform.DOLocalMove(_originalChildPositions[i], _hoverDuration)
                        .SetEase(Ease.OutCubic);

                    DOTween.To(() => child.Radius, x => child.Radius = x, _originalChildRadii[i], _hoverDuration)
                        .SetEase(Ease.OutCubic);

                    DOTween.To(() => child.Color, x => child.Color = x, _originalChildColors[i], _hoverDuration)
                        .SetEase(Ease.OutCubic);
                }
            }
        }
    }

    /// <summary>
    /// Description: Handles pointer release transitions.
    /// Context: Called by OnPointerUp.
    /// Justification: Return animation handled gracefully by hover transitions.
    /// </summary>
    private void AnimateRelease()
    {
        // Return animation handled gracefully by hover transitions
    }

    /// <summary>
    /// Description: Executes a stunning scintillation/flash effect on the Rectangle component upon click.
    /// Context: Called by OnPointerDown.
    /// Justification: Houses the core DOTween sequence for clicks.
    /// </summary>
    private void AnimateClick()
    {
        // Kill existing tweens on components to prevent state blending
        if (_rect != null)
        {
            _rect.transform.DOKill();
            DOTween.Kill(_rect);
        }

        if (_mainDisc != null)
        {
            DOTween.Kill(_mainDisc);
        }

        if (_childDiscs != null)
        {
            for (int i = 0; i < _childDiscs.Length; i++)
            {
                if (_childDiscs[i] != null)
                {
                    _childDiscs[i].transform.DOKill();
                    DOTween.Kill(_childDiscs[i]);
                }
            }
        }

        if (_clickFlashSequence != null && _clickFlashSequence.IsActive())
        {
            _clickFlashSequence.Kill();
        }

        // Instantly restore exact original local scales, positions and colors
        if (_rect != null)
        {
            _rect.transform.localScale = _originalRectLocalScale;
            _rect.Color = _originalRectColor;
        }

        if (_mainDisc != null)
        {
            _mainDisc.Color = _originalMainDiscColor;
            _mainDisc.Radius = _originalMainDiscRadius;
        }

        if (_childDiscs != null)
        {
            for (int i = 0; i < _childDiscs.Length; i++)
            {
                if (_childDiscs[i] != null)
                {
                    _childDiscs[i].transform.localPosition = _originalChildPositions[i];
                    _childDiscs[i].Color = _originalChildColors[i];
                    _childDiscs[i].Radius = _originalChildRadii[i];
                }
            }
        }

        // Create a single unified click sequence
        _clickFlashSequence = DOTween.Sequence();

        // ==================== RECTANGLE ANIMATION ====================
        if (_rect != null)
        {
            // Rapid Scale Pulse (extremely snappy!)
            _clickFlashSequence.Append(_rect.transform.DOScale(_originalRectLocalScale * 1.15f, 0.03f).SetEase(Ease.OutQuad));
            _clickFlashSequence.Append(_rect.transform.DOScale(_originalRectLocalScale, 0.12f).SetEase(Ease.OutCubic));

            // Instant bloom peak
            _clickFlashSequence.Join(DOTween.To(() => _rect.Color, x => _rect.Color = x, Color.white, 0.02f)
                .SetEase(Ease.OutQuad));
            
            // Ultra fast blackout
            _clickFlashSequence.Append(DOTween.To(() => _rect.Color, x => _rect.Color = x, new Color(_originalRectColor.r, _originalRectColor.g, _originalRectColor.b, 0f), 0.04f)
                .SetEase(Ease.InQuad));
            
            // Fast high-frequency holographic flicker return
            Color lowFlicker = new Color(_originalRectColor.r, _originalRectColor.g, _originalRectColor.b, 0.2f);
            Color midFlicker = new Color(_originalRectColor.r, _originalRectColor.g, _originalRectColor.b, 0.6f);
            Color highFlicker = new Color(_originalRectColor.r, _originalRectColor.g, _originalRectColor.b, 0.8f);

            _clickFlashSequence.Append(DOTween.To(() => _rect.Color, x => _rect.Color = x, midFlicker, 0.015f));
            _clickFlashSequence.Append(DOTween.To(() => _rect.Color, x => _rect.Color = x, lowFlicker, 0.015f));
            _clickFlashSequence.Append(DOTween.To(() => _rect.Color, x => _rect.Color = x, highFlicker, 0.015f));
            _clickFlashSequence.Append(DOTween.To(() => _rect.Color, x => _rect.Color = x, lowFlicker, 0.015f));
            
            // Settle back to standard hover color
            _clickFlashSequence.Append(DOTween.To(() => _rect.Color, x => _rect.Color = x, _originalRectColor, 0.08f)
                .SetEase(Ease.OutQuad));
        }

        // ==================== MAIN DISC ANIMATION ====================
        if (_mainDisc != null)
        {
            // Rapid high-intensity white flash
            _clickFlashSequence.Join(DOTween.To(() => _mainDisc.Color, x => _mainDisc.Color = x, Color.white, 0.02f).SetEase(Ease.OutQuad));
            _clickFlashSequence.Append(DOTween.To(() => _mainDisc.Color, x => _mainDisc.Color = x, new Color(_originalMainDiscColor.r, _originalMainDiscColor.g, _originalMainDiscColor.b, 0.1f), 0.04f));
            _clickFlashSequence.Append(DOTween.To(() => _mainDisc.Color, x => _mainDisc.Color = x, _originalMainDiscColor, 0.10f).SetEase(Ease.OutQuad));

            // Radius pulse expand and contract
            _clickFlashSequence.Join(DOTween.To(() => _mainDisc.Radius, x => _mainDisc.Radius = x, _originalMainDiscRadius * 1.6f, 0.03f).SetEase(Ease.OutQuad));
            _clickFlashSequence.Append(DOTween.To(() => _mainDisc.Radius, x => _mainDisc.Radius = x, _originalMainDiscRadius, 0.12f).SetEase(Ease.OutCubic));
        }

        // ==================== CHILD DISCS ANIMATION ====================
        if (_childDiscs != null)
        {
            for (int i = 0; i < _childDiscs.Length; i++)
            {
                if (_childDiscs[i] != null)
                {
                    Disc child = _childDiscs[i];
                    float origRadius = _originalChildRadii[i];
                    Vector3 origPos = _originalChildPositions[i];
                    Color origColor = _originalChildColors[i];

                    Vector3 direction = origPos.normalized;
                    if (direction == Vector3.zero) direction = Vector3.right;

                    // Creative local Shockwave motion outwards, then snaps back
                    _clickFlashSequence.Join(child.transform.DOLocalMove(origPos + direction * 0.22f, 0.03f).SetEase(Ease.OutQuad));
                    _clickFlashSequence.Append(child.transform.DOLocalMove(origPos, 0.12f).SetEase(Ease.OutCubic));

                    // Radius shockwave scale pulse
                    _clickFlashSequence.Join(DOTween.To(() => child.Radius, x => child.Radius = x, origRadius * 2.2f, 0.03f).SetEase(Ease.OutQuad));
                    _clickFlashSequence.Append(DOTween.To(() => child.Radius, x => child.Radius = x, origRadius, 0.12f).SetEase(Ease.OutCubic));

                    // Electric color pulse
                    _clickFlashSequence.Join(DOTween.To(() => child.Color, x => child.Color = x, Color.white, 0.02f).SetEase(Ease.OutQuad));
                    _clickFlashSequence.Append(DOTween.To(() => child.Color, x => child.Color = x, origColor, 0.10f).SetEase(Ease.OutCubic));
                }
            }
        }
    }

    #endregion

    #region Interactable State Transitions

    /// <summary>
    /// Description: Overrides the interactability change callback to trigger custom fade-out and fade-in transitions.
    /// Context: Invoked when Interactable property changes.
    /// Justification: visually reflects the disabled state.
    /// </summary>
    /// <param name="isInteractable">True if the button is now interactable, false otherwise.</param>
    protected override void OnInteractableChanged(bool isInteractable)
    {
        base.OnInteractableChanged(isInteractable);
        
        // Terminate any active pointer motion tweens to prevent state mixing
        KillActiveTweens();

        // Animate elements to represent active or deactivated visuals
        AnimateInteractableTransition(isInteractable);
    }

    /// <summary>
    /// Description: Animates the text, rect, and dots colors to greyed-out/semi-transparent values when disabled, and smoothly restores original colors when re-enabled.
    /// Context: Called by OnInteractableChanged.
    /// Justification: Provides clear feedback that the button cannot be pressed.
    /// </summary>
    /// <param name="isInteractable">True if active, false if disabled.</param>
    private void AnimateInteractableTransition(bool isInteractable)
    {
        float duration = 0.25f;

        if (isInteractable)
        {
            // 1. Smoothly fade text back to its original white state
            if (_buttonText != null)
            {
                _buttonText.DOColor(Color.white, duration).SetEase(Ease.OutQuad);
            }

            // 2. Smoothly restore Left Line color
            if (_leftLine != null)
            {
                DOTween.To(() => _leftLine.Color, x => _leftLine.Color = x, _originalLineColor, duration)
                    .SetEase(Ease.OutQuad);
            }

            // 3. Fade Rectangle back to its transparent default state
            if (_rect != null)
            {
                Color targetRectColor = new Color(_originalRectColor.r, _originalRectColor.g, _originalRectColor.b, 0f);
                DOTween.To(() => _rect.Color, x => _rect.Color = x, targetRectColor, duration)
                    .SetEase(Ease.OutQuad);
            }

            // 4. Restore Main Disc color
            if (_mainDisc != null)
            {
                DOTween.To(() => _mainDisc.Color, x => _mainDisc.Color = x, _originalMainDiscColor, duration)
                    .SetEase(Ease.OutQuad);
            }

            // 5. Restore Child Discs colors
            if (_childDiscs != null)
            {
                for (int i = 0; i < _childDiscs.Length; i++)
                {
                    if (_childDiscs[i] != null)
                    {
                        Disc child = _childDiscs[i];
                        Color origColor = _originalChildColors[i];
                        DOTween.To(() => child.Color, x => child.Color = x, origColor, duration)
                            .SetEase(Ease.OutQuad);
                    }
                }
            }
        }
        else
        {
            // Deactivated states: aesthetic translucent grey styles
            Color disabledTextColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);
            Color disabledShapeColor = new Color(0.3f, 0.3f, 0.3f, 0.2f);

            // 1. Fade out text
            if (_buttonText != null)
            {
                _buttonText.DOColor(disabledTextColor, duration).SetEase(Ease.OutQuad);
            }

            // 2. Fade out Left Line
            if (_leftLine != null)
            {
                DOTween.To(() => _leftLine.Color, x => _leftLine.Color = x, disabledShapeColor, duration)
                    .SetEase(Ease.OutQuad);
            }

            // 3. Fade/Grey out Rectangle
            if (_rect != null)
            {
                DOTween.To(() => _rect.Color, x => _rect.Color = x, disabledShapeColor, duration)
                    .SetEase(Ease.OutQuad);
            }

            // 4. Fade out Main Disc
            if (_mainDisc != null)
            {
                DOTween.To(() => _mainDisc.Color, x => _mainDisc.Color = x, disabledShapeColor, duration)
                    .SetEase(Ease.OutQuad);
            }

            // 5. Fade out Child Discs
            if (_childDiscs != null)
            {
                for (int i = 0; i < _childDiscs.Length; i++)
                {
                    if (_childDiscs[i] != null)
                    {
                        Disc child = _childDiscs[i];
                        DOTween.To(() => child.Color, x => child.Color = x, disabledShapeColor, duration)
                            .SetEase(Ease.OutQuad);
                    }
                }
            }
        }
    }

    #endregion

    #region Cleanup & Safety Guards

    /// <summary>
    /// Description: Safely terminates all running tweens on shapes and transform components to prevent state pollution.
    /// Context: Utility method.
    /// Justification: Required before starting new tweens to prevent overlapping animations.
    /// </summary>
    private void KillActiveTweens()
    {
        if (_leftLine != null)
        {
            DOTween.Kill(_leftLine);
        }

        if (_rect != null)
        {
            DOTween.Kill(_rect);
            _rect.transform.DOKill();
            // Restore scale state cleanly when resetting
            _rect.transform.localScale = _originalRectLocalScale;
        }

        if (_dots != null)
        {
            _dots.transform.DOKill();
        }

        if (_buttonText != null)
        {
            _buttonText.transform.DOKill();
        }

        if (_clickFlashSequence != null && _clickFlashSequence.IsActive())
        {
            _clickFlashSequence.Kill();
        }

        // Clean kill Discs
        if (_mainDisc != null)
        {
            DOTween.Kill(_mainDisc);
            _mainDisc.Color = _originalMainDiscColor;
            _mainDisc.Radius = _originalMainDiscRadius;
        }

        if (_childDiscs != null)
        {
            for (int i = 0; i < _childDiscs.Length; i++)
            {
                if (_childDiscs[i] != null)
                {
                    Disc child = _childDiscs[i];
                    DOTween.Kill(child);
                    child.transform.DOKill();
                    child.Color = _originalChildColors[i];
                    child.Radius = _originalChildRadii[i];
                    child.transform.localPosition = _originalChildPositions[i];
                }
            }
        }
    }

    #endregion
}
