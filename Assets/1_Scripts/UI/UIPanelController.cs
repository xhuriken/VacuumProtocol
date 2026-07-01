using System;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Controls the showing, hiding, and scaling transition animations of a UI panel using DOTween.
/// Requires a CanvasGroup component to manage fading and input block states.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class UIPanelController : MonoBehaviour
{
    [Header("Transition Settings")]
    [SerializeField] private float _transitionDuration = 0.3f;
    [SerializeField] private bool _useScaleAnimation = true;
    [SerializeField] private Vector3 _hiddenScale = new Vector3(0.8f, 0.8f, 0.8f);
    [SerializeField] private bool _startOpened = false;

    private CanvasGroup _canvasGroup;
    private RectTransform _rectTransform;
    private Tween _activeFadeTween;
    private Tween _activeScaleTween;
    private bool _isOpened = false;

    /// <summary>
    /// Event raised when the panel completes its opening animation.
    /// </summary>
    public event Action OnPanelOpened;

    /// <summary>
    /// Event raised when the panel completes its closing animation.
    /// </summary>
    public event Action OnPanelClosed;

    /// <summary>
    /// Gets whether this panel is currently opened.
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
    /// Opens the panel smoothly with a fade-in and optional scale animation.
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
    /// Closes the panel smoothly with a fade-out and optional scale-down animation.
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
