using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;
using Shapes;

/// <summary>
/// Controls lobby color selection buttons utilizing custom Freya Holmér Vector Shapes (Rectangle).
/// Handles smooth outline size morphing and magnetic cursor attraction for the plain inner shape.
/// Includes dynamic visual raycast diagnostics to identify hover blockages in the scene.
/// </summary>
public class ColorButtonUI : UICustomButtonBase
{
    [Header("Shapes References")]
    [Tooltip("The outline rectangle shape component.")]
    [SerializeField]
    private Rectangle _outlineShape;

    [Tooltip("The plain inner rectangle shape component.")]
    [SerializeField]
    private Rectangle _plainShape;

    [Header("Base Dimensions")]
    [Tooltip("Base width of the outline rectangle.")]
    [SerializeField]
    private float _baseWidth = 75f;

    [Tooltip("Base height of the outline rectangle.")]
    [SerializeField]
    private float _baseHeight = 75f;

    [Header("Hover Multipliers")]
    [Tooltip("Width scale multiplier on pointer hover.")]
    [SerializeField]
    private float _hoverWidthMultiplier = 1.15f;

    [Tooltip("Height scale multiplier on pointer hover.")]
    [SerializeField]
    private float _hoverHeightMultiplier = 1.15f;

    [Tooltip("Duration of outline resizing and bounce animations.")]
    [SerializeField]
    private float _animationDuration = 0.15f;

    [Header("Magnetic Proximity Options")]
    [Tooltip("Radius in screen pixels within which the magnetic attraction pulls the inner plain shape.")]
    [SerializeField]
    private float _magneticRadius = 150f;

    [Tooltip("Maximum local offset distance the inner plain shape can move towards the mouse.")]
    [SerializeField]
    private float _maxMagneticOffset = 12f;

    [Tooltip("Smoothing factor for inner plain shape movement interpolation.")]
    [SerializeField]
    private float _magneticSmoothSpeed = 12f;

    private Vector3 _originalPlainScale = Vector3.one;
    private Vector3 _originalPlainLocalPos = Vector3.zero;
    
    // Diagnostic tracking variables
    private float _diagnosticTimer = 0f;
    private static bool _hasLoggedDiagnosticHeader = false;

    /// <summary>
    /// Unity Awake callback. Performs validation checks and caches initial visual states.
    /// </summary>
    protected override void Awake()
    {
        base.Awake();

        Debug.Log($"[ColorButtonUI] Awake triggered on '{gameObject.name}'");

        // Validate Shape References to assist editor diagnostics
        if (_plainShape == null)
        {
            Debug.LogError($"[ColorButtonUI] ERROR on '{gameObject.name}': Plain Shape reference is not assigned in the Inspector! Magnetic movement will be disabled.");
        }
        else
        {
            _originalPlainScale = _plainShape.transform.localScale;
            _originalPlainLocalPos = _plainShape.transform.localPosition;
            Debug.Log($"[ColorButtonUI] '{gameObject.name}' cached original Plain scale ({_originalPlainScale}) and local position ({_originalPlainLocalPos})");
        }

        if (_outlineShape == null)
        {
            Debug.LogError($"[ColorButtonUI] ERROR on '{gameObject.name}': Outline Shape reference is not assigned in the Inspector! Resizing animations will be disabled.");
        }
        else
        {
            // Initialize outline to its default width and height immediately
            _outlineShape.Width = _baseWidth;
            _outlineShape.Height = _baseHeight;
            Debug.Log($"[ColorButtonUI] '{gameObject.name}' initialized Outline size to {_baseWidth}x{_baseHeight}");
        }
    }

    /// <summary>
    /// Unity Start callback. Validates presence of critical scene dependencies.
    /// </summary>
    private void Start()
    {
        Debug.Log($"[ColorButtonUI] Start triggered on '{gameObject.name}'");

        // Diagnose if MouseManager is missing
        if (MouseManager.Instance == null)
        {
            Debug.LogWarning($"[ColorButtonUI] WARNING on '{gameObject.name}': MouseManager.Instance is null! Please make sure a MouseManager component is attached to an active GameObject (e.g. your UI Canvas) in the scene.");
        }

        // Diagnose if EventSystem is missing in the scene
        if (EventSystem.current == null)
        {
            Debug.LogError($"[ColorButtonUI] CRITICAL ERROR on '{gameObject.name}': No EventSystem found in the scene! Unity UI cannot process pointer events (hovers/clicks) without an EventSystem.");
        }
        else
        {
            BaseInputModule activeInputModule = EventSystem.current.GetComponent<BaseInputModule>();
            if (activeInputModule != null)
            {
                Debug.Log($"[ColorButtonUI] EventSystem active on '{gameObject.name}' with Input Module: {activeInputModule.GetType().Name}");
            }
        }
    }

    /// <summary>
    /// Smoothly resets scale, positions, and kills active tweens to prevent visual bugs when disabled.
    /// </summary>
    protected override void OnDisable()
    {
        base.OnDisable();
        KillAllTweens();
        ResetToDefaults();
    }

    /// <summary>
    /// Unity Update callback. Calculates screen proximity to mouse and applies magnetic pull to the inner shape.
    /// Runs dynamic diagnostics to help developer uncover UGUI graphic raycast blocks.
    /// </summary>
    private void Update()
    {
        if (_plainShape == null)
        {
            return;
        }

        Vector3 targetLocalPos = Vector3.zero;

        // Apply magnetic pull toward cursor if active, within radius, and not hovered
        if (MouseManager.Instance != null && !IsHovered)
        {
            Vector2 mousePos = MouseManager.Instance.MousePosition;
            Camera uiCamera = Camera.main;
            
            Vector2 buttonScreenPos = uiCamera != null 
                ? (Vector2)uiCamera.WorldToScreenPoint(transform.position) 
                : (Vector2)transform.position;

            Vector2 direction = mousePos - buttonScreenPos;
            float distance = direction.magnitude;

            if (distance < _magneticRadius)
            {
                float proximity = 1f - (distance / _magneticRadius);
                float pullStrength = proximity * proximity * _maxMagneticOffset;

                Vector2 magneticPull = direction.normalized * pullStrength;
                targetLocalPos = new Vector3(magneticPull.x, magneticPull.y, 0f);
            }
        }

        // Smoothly interpolate the local position of the plain inner shape
        _plainShape.transform.localPosition = Vector3.Lerp(
            _plainShape.transform.localPosition,
            _originalPlainLocalPos + targetLocalPos,
            Time.unscaledDeltaTime * _magneticSmoothSpeed
        );

        // Run UGUI Raycast diagnostic scanner every 2 seconds
        RunDiagnosticTracker();
    }

    /// <summary>
    /// Runs a recursive raycast test from current mouse coordinates to detect graphic blockages.
    /// </summary>
    private void RunDiagnosticTracker()
    {
        _diagnosticTimer += Time.unscaledDeltaTime;
        if (_diagnosticTimer < 2.0f)
        {
            return;
        }
        _diagnosticTimer = 0f;

        if (EventSystem.current == null)
        {
            Debug.LogError("[UI Diagnostic Raycaster] EventSystem.current is NULL! Dynamic UI hover captures are completely offline.");
            return;
        }

        Vector2 mousePos = MouseManager.Instance != null ? MouseManager.Instance.MousePosition : Vector2.zero;
        
        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = mousePos
        };

        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        if (results.Count > 0)
        {
            // Only log the breakdown from the first active button in the hierarchy to avoid spamming 8 duplicate logs
            if (!_hasLoggedDiagnosticHeader)
            {
                _hasLoggedDiagnosticHeader = true;
                string logBreakdown = $"[UI Diagnostic] Cursor Screen Position: {mousePos}. Raycast hit {results.Count} active GameObjects underneath:\n";
                for (int i = 0; i < results.Count; i++)
                {
                    GameObject hitObj = results[i].gameObject;
                    Graphic hitGraphic = hitObj.GetComponent<Graphic>();
                    RectTransform hitRect = hitObj.GetComponent<RectTransform>();
                    
                    string raycastState = hitGraphic != null ? $"Graphic ({hitGraphic.GetType().Name}), RaycastTarget = {hitGraphic.raycastTarget}" : "No Graphic component";
                    string rectSize = hitRect != null ? $"{hitRect.rect.width}x{hitRect.rect.height}" : "No RectTransform";

                    logBreakdown += $"   -> [{i}] Name: '{hitObj.name}' | Layer: {LayerMask.LayerToName(hitObj.layer)} | {raycastState} | Rect Size: {rectSize}\n";
                }
                Debug.Log(logBreakdown);
            }
        }
        else
        {
            if (!_hasLoggedDiagnosticHeader)
            {
                _hasLoggedDiagnosticHeader = true;
            //     Debug.LogWarning($"[UI Diagnostic] Cursor Screen Position: {mousePos}. Raycast hit NOTHING! \n" +
            //                      "Check list:\n" +
            //                      "1. Does your Canvas contain a 'Graphic Raycaster' component?\n" +
            //                      "2. If Canvas is World Space, is the Main Camera assigned to its 'Event Camera' field?\n" +
            //                      "3. Are your buttons' RectTransform dimensions set correctly (width/height > 0)? Currently transparent hits are generated dynamically.\n" +
            //                      "4. If New Input System is active, has the EventSystem been upgraded to 'InputSystemUIInputModule'?");
            // 
            }
        }

        // Reset the header lock at the end of the frame so next diagnostic cycle runs cleanly
        Invoke(nameof(ResetDiagnosticHeaderLock), 0.05f);
    }

    private void ResetDiagnosticHeaderLock()
    {
        _hasLoggedDiagnosticHeader = false;
    }

    /// <summary>
    /// Sets the color of both the outline and plain vector shapes.
    /// </summary>
    /// <param name="color">The color to apply.</param>
    public void SetButtonColor(Color color)
    {
        Debug.Log($"[ColorButtonUI] SetButtonColor triggered on '{gameObject.name}' with color: {color}");

        if (_plainShape != null)
        {
            _plainShape.Color = color;
        }

        if (_outlineShape != null)
        {
            _outlineShape.Color = color;
        }
    }

    /// <summary>
    /// Animates the outline shape size and plain scale when pointer enters button bounds.
    /// </summary>
    /// <param name="eventData">Pointer event data.</param>
    public override void OnPointerEnter(PointerEventData eventData)
    {
        base.OnPointerEnter(eventData);

        Debug.Log($"[ColorButtonUI] OnPointerEnter callback executing on '{gameObject.name}'");

        float targetWidth = _baseWidth * _hoverWidthMultiplier;
        float targetHeight = _baseHeight * _hoverHeightMultiplier;
        AnimateOutlineSize(targetWidth, targetHeight, _animationDuration);

        if (_plainShape != null)
        {
            _plainShape.transform.DOKill();
            _plainShape.transform.DOScale(_originalPlainScale * 1.1f, _animationDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }
    }

    /// <summary>
    /// Resets outline shape size and plain scale when pointer leaves button bounds.
    /// </summary>
    /// <param name="eventData">Pointer event data.</param>
    public override void OnPointerExit(PointerEventData eventData)
    {
        base.OnPointerExit(eventData);

        Debug.Log($"[ColorButtonUI] OnPointerExit callback executing on '{gameObject.name}'");

        AnimateOutlineSize(_baseWidth, _baseHeight, _animationDuration);

        if (_plainShape != null)
        {
            _plainShape.transform.DOKill();
            _plainShape.transform.DOScale(_originalPlainScale, _animationDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }
    }

    /// <summary>
    /// Shrinks visual elements on pointer down to provide premium tactile press feedback.
    /// </summary>
    /// <param name="eventData">Pointer event data.</param>
    public override void OnPointerDown(PointerEventData eventData)
    {
        base.OnPointerDown(eventData);

        Debug.Log($"[ColorButtonUI] OnPointerDown callback executing on '{gameObject.name}'");

        float pressWidth = _baseWidth * 0.9f;
        float pressHeight = _baseHeight * 0.9f;
        AnimateOutlineSize(pressWidth, pressHeight, _animationDuration * 0.6f);

        if (_plainShape != null)
        {
            _plainShape.transform.DOKill();
            _plainShape.transform.DOScale(_originalPlainScale * 0.9f, _animationDuration * 0.6f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }
    }

    /// <summary>
    /// Springs visual elements back to their appropriate hover or idle scales on pointer release.
    /// </summary>
    /// <param name="eventData">Pointer event data.</param>
    public override void OnPointerUp(PointerEventData eventData)
    {
        base.OnPointerUp(eventData);

        Debug.Log($"[ColorButtonUI] OnPointerUp callback executing on '{gameObject.name}'");

        float targetMultiplier = IsHovered ? _hoverWidthMultiplier : 1.0f;
        float targetWidth = _baseWidth * targetMultiplier;
        float targetHeight = _baseHeight * targetMultiplier;
        AnimateOutlineSize(targetWidth, targetHeight, _animationDuration);

        if (_plainShape != null)
        {
            _plainShape.transform.DOKill();
            float plainMultiplier = IsHovered ? 1.1f : 1.0f;
            _plainShape.transform.DOScale(_originalPlainScale * plainMultiplier, _animationDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
        }
    }

    /// <summary>
    /// Smoothly tweens the outline rectangle's Width and Height properties using DOTween.
    /// </summary>
    /// <param name="targetWidth">Target width to tween to.</param>
    /// <param name="targetHeight">Target height to tween to.</param>
    /// <param name="duration">Duration of the transition.</param>
    private void AnimateOutlineSize(float targetWidth, float targetHeight, float duration)
    {
        if (_outlineShape == null)
        {
            return;
        }

        Debug.Log($"[ColorButtonUI] '{gameObject.name}' animating Outline size to: {targetWidth}x{targetHeight}");

        // Kill active size tweens targeting this specific shapes outline to avoid conflicts
        DOTween.Kill(_outlineShape);

        DOTween.To(() => _outlineShape.Width, w => _outlineShape.Width = w, targetWidth, duration)
            .SetTarget(_outlineShape)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);

        DOTween.To(() => _outlineShape.Height, h => _outlineShape.Height = h, targetHeight, duration)
            .SetTarget(_outlineShape)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }

    /// <summary>
    /// Halts all active DOTween animations targeting shapes.
    /// </summary>
    private void KillAllTweens()
    {
        if (_outlineShape != null)
        {
            DOTween.Kill(_outlineShape);
        }

        if (_plainShape != null)
        {
            _plainShape.transform.DOKill();
        }
    }

    /// <summary>
    /// Resets all visual shapes to their initial inspector dimensions and positions.
    /// </summary>
    private void ResetToDefaults()
    {
        if (_outlineShape != null)
        {
            _outlineShape.Width = _baseWidth;
            _outlineShape.Height = _baseHeight;
        }

        if (_plainShape != null)
        {
            _plainShape.transform.localScale = _originalPlainScale;
            _plainShape.transform.localPosition = _originalPlainLocalPos;
        }
    }
}
