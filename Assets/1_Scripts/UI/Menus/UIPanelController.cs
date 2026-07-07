using System;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Description: Controls the showing, hiding, and scaling transition animations of a UI panel using DOTween.
/// Context: Attached to any UGUI Panel intended to be toggled.
/// Justification: Centralizes UI animation logic and CanvasGroup interactability states.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class UIPanelController : MonoBehaviour
{
    [Header("Transition Settings")]
    [Tooltip("Role: Animation duration.\nUse Case: Fading and scaling pacing.\nJustification: Keeps transitions uniform.")]
    [SerializeField] private float _transitionDuration = 0.3f;
    [Tooltip("Role: Flag for scale bounce.\nUse Case: Disabling scale FX.\nJustification: Some flat panels shouldn't bounce.")]
    [SerializeField] private bool _useScaleAnimation = true;
    [Tooltip("Role: Minimum scale.\nUse Case: Starting point of scale bounce.\nJustification: Determines how shrunk the panel is before opening.")]
    [SerializeField] private Vector3 _hiddenScale = new Vector3(0.8f, 0.8f, 0.8f);
    [Tooltip("Role: Flag to open at Start.\nUse Case: Default visibility.\nJustification: Allows a panel to be visibly open immediately.")]
    [SerializeField] private bool _startOpened = false;

    public enum PanelSide { Left, Right }

    [Header("Layout Settings")]
    [Tooltip("Role: The layout side this panel belongs to.\nUse Case: Multi-panel layout navigation.\nJustification: Allows Left and Right panels to remain open simultaneously.")]
    [SerializeField] private PanelSide _side = PanelSide.Left;

    public PanelSide Side => _side;

    private CanvasGroup _canvasGroup;
    private RectTransform _rectTransform;
    private Tween _activeFadeTween;
    private Tween _activeScaleTween;
    private bool _isOpened = false;

    /// <summary>
    /// Description: Event raised when the panel completes its opening animation.
    /// Context: Fired by DOTween sequence end.
    /// Justification: Allows external scripts to know when UI is fully ready.
    /// </summary>
    public event Action OnPanelOpened;

    /// <summary>
    /// Description: Event raised when the panel completes its closing animation.
    /// Context: Fired by DOTween sequence end.
    /// Justification: Allows external scripts to know when UI is fully hidden.
    /// </summary>
    public event Action OnPanelClosed;

    /// <summary>
    /// Description: Gets whether this panel is currently opened.
    /// Context: State flag.
    /// Justification: Checked before triggering open/close logic to prevent redundant tweens.
    /// </summary>
    public bool IsOpened => _isOpened;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _rectTransform = GetComponent<RectTransform>();
        _isOpened = _startOpened;

        // Initialize state
        if (!_isOpened)
        {
            InstantHide();
        }
        else
        {
            InstantShow();
        }
    }

    /// <summary>
    /// Description: Opens the panel smoothly with a fade-in and optional scale animation.
    /// Context: Called by UI Buttons or Controllers.
    /// Justification: Primary way to bring a panel into view.
    /// </summary>
    public void Show()
    {
        if (_isOpened) return;

        _isOpened = true;
        gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.interactable = true;

        // Kill active transitions to avoid overlaps
        _activeFadeTween?.Kill();
        _activeScaleTween?.Kill();

        // Animate opacity (alpha)
        _canvasGroup.alpha = 0f;
        _activeFadeTween = _canvasGroup.DOFade(1f, _transitionDuration).SetUpdate(true);

        // Animate scale
        if (_useScaleAnimation && _rectTransform != null)
        {
            _rectTransform.localScale = _hiddenScale;
            _activeScaleTween = _rectTransform.DOScale(Vector3.one, _transitionDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .OnComplete(() => OnPanelOpened?.Invoke());
        }
        else
        {
            OnPanelOpened?.Invoke();
        }
    }

    /// <summary>
    /// Description: Closes the panel smoothly with a fade-out and optional scale-down animation.
    /// Context: Called by UI Buttons or Controllers.
    /// Justification: Primary way to dismiss a panel.
    /// </summary>
    public void Hide()
    {
        if (!_isOpened) return;

        _isOpened = false;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;

        // Kill active transitions
        _activeFadeTween?.Kill();
        _activeScaleTween?.Kill();

        // Animate opacity
        _activeFadeTween = _canvasGroup.DOFade(0f, _transitionDuration).SetUpdate(true);

        // Animate scale
        if (_useScaleAnimation && _rectTransform != null)
        {
            _activeScaleTween = _rectTransform.DOScale(_hiddenScale, _transitionDuration)
                .SetEase(Ease.InBack)
                .SetUpdate(true)
                .OnComplete(CompleteHide);
        }
        else
        {
            CompleteHide();
        }
    }

    private void CompleteHide()
    {
        gameObject.SetActive(false);
        OnPanelClosed?.Invoke();
    }

    private void InstantHide()
    {
        _isOpened = false;
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
        if (_rectTransform != null)
        {
            _rectTransform.localScale = _hiddenScale;
        }
        gameObject.SetActive(false);
    }

    private void InstantShow()
    {
        _isOpened = true;
        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.interactable = true;
        if (_rectTransform != null)
        {
            _rectTransform.localScale = Vector3.one;
        }
        gameObject.SetActive(true);
    }
}
