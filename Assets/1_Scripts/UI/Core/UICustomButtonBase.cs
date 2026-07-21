using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Description: Reusable base toolkit class for custom interactive UI elements.
/// Context: Inherited by ColorButtonUI and CustomTextButton.
/// Justification: Centralizes UGUI EventSystem callbacks (enter, exit, down, up, click) so child classes only need to override what they need.
/// </summary>
public class UICustomButtonBase : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    /// <summary>
    /// Description: Event triggered when the custom button is successfully clicked.
    /// Context: Invoked on PointerClick.
    /// Justification: Allows binding actions via the Unity Inspector.
    /// </summary>
    public Button.ButtonClickedEvent onClick = new Button.ButtonClickedEvent();

    /// <summary>
    /// Description: Event triggered when the pointer enters the button bounds.
    /// Context: Invoked on PointerEnter.
    /// Justification: Allows binding actions via the Unity Inspector.
    /// </summary>
    public Button.ButtonClickedEvent onPointerEnter = new Button.ButtonClickedEvent();

    /// <summary>
    /// Description: Event triggered when the pointer leaves the button bounds.
    /// Context: Invoked on PointerExit.
    /// Justification: Allows binding actions via the Unity Inspector.
    /// </summary>
    public Button.ButtonClickedEvent onPointerExit = new Button.ButtonClickedEvent();

    /// <summary>
    /// Description: Event triggered when the pointer is pressed down.
    /// Context: Invoked on PointerDown.
    /// Justification: Allows binding actions via the Unity Inspector.
    /// </summary>
    public Button.ButtonClickedEvent onPointerDown = new Button.ButtonClickedEvent();

    /// <summary>
    /// Description: Event triggered when the pointer is released.
    /// Context: Invoked on PointerUp.
    /// Justification: Allows binding actions via the Unity Inspector.
    /// </summary>
    public Button.ButtonClickedEvent onPointerUp = new Button.ButtonClickedEvent();

    [Header("Interactable State")]
    [Tooltip("Role: Controls button interactivity.\nUse Case: Disabling the button.\nJustification: Used when a button should be visible but not clickable.")]
    [SerializeField]
    private bool _interactable = true;

    [Header("Debug")]
    [Tooltip("Role: Toggles debug logs for UI interactions.\nUse Case: Testing.\nJustification: Useful for checking if EventSystem raycasts are hitting the button.")]
    [SerializeField]
    protected bool _enableDebugLogs = false;

    private bool _isHovered = false;

    /// <summary>
    /// Description: Gets whether the pointer is currently hovering over the button bounds.
    /// Context: State variable updated by enter/exit handlers.
    /// Justification: Required to know if hover animations should be playing.
    /// </summary>
    public bool IsHovered => _isHovered;

    /// <summary>
    /// Description: Gets or sets whether this button is interactable. Modifying this triggers the virtual OnInteractableChanged hook.
    /// Context: Public accessor.
    /// Justification: Ensures state transitions happen automatically when the property is changed via code.
    /// </summary>
    public bool Interactable
    {
        get => _interactable;
        set
        {
            if (_interactable != value)
            {
                _interactable = value;
                if (_interactable)
                {
                    // Check if mouse is physically hovering the RectTransform right now (using SSOT MouseManager)
                    Canvas canvas = GetComponentInParent<Canvas>();
                    Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
                    _isHovered = RectTransformUtility.RectangleContainsScreenPoint(GetComponent<RectTransform>(), MouseManager.Instance.MousePosition, cam);
                }
                else
                {
                    _isHovered = false; // Reset hover state when becoming disabled
                }
                OnInteractableChanged(value);
            }
        }
    }

    /// <summary>
    /// Description: Virtual lifecycle hook triggered when the button's interactability changes.
    /// Context: Called by the Interactable setter.
    /// Justification: Allows child classes to define custom visual transitions for disabled states.
    /// </summary>
    /// <param name="isInteractable">The new interactability state.</param>
    protected virtual void OnInteractableChanged(bool isInteractable)
    {
        // Custom transitions can be defined by subclasses
    }

    [Header("Awake Setup")]
    [Tooltip("Role: Safety image raycast setup.")]
    private bool _unusedPlaceholder;

    /// <summary>
    /// Description: Unity Awake callback. Performs safety validation to ensure pointer raycasts are configured.
    /// Context: Initialization.
    /// Justification: Prevents developer error by automatically adding a raycast target if missing.
    /// </summary>
    protected virtual void Awake()
    {
        // Validation check: UGUI requires a Graphic component with raycastTarget = true to receive pointer events
        Graphic graphic = GetComponent<Graphic>();
        if (graphic == null)
        {
            if (_enableDebugLogs) Debug.LogWarning($"[UICustomButtonBase] WARNING on '{gameObject.name}': Raycast target missing! A Graphic component (e.g. Image) is required for EventSystem raycasts. Adding a transparent Image dynamically to resolve this.");
            
            Image dynamicImage = gameObject.AddComponent<Image>();
            dynamicImage.color = new Color(0f, 0f, 0f, 0f);
            dynamicImage.raycastTarget = true;
        }
        else if (!graphic.raycastTarget)
        {
            if (_enableDebugLogs) Debug.LogWarning($"[UICustomButtonBase] WARNING on '{gameObject.name}': The {graphic.GetType().Name} component has raycastTarget set to FALSE. Enabling raycastTarget dynamically so events can fire.");
            graphic.raycastTarget = true;
        }
    }

    /// <summary>
    /// Description: Automatically resets the hover state when the component is disabled.
    /// Context: Unity OnDisable callback.
    /// Justification: Prevents the button from getting stuck in a hovered state if deactivated while hovered.
    /// </summary>
    protected virtual void OnDisable()
    {
        _isHovered = false;
    }

    /// <summary>
    /// Description: Handles pointer enter event, invoking callbacks.
    /// Context: EventSystem callback.
    /// Justification: Base functionality for hover logic.
    /// </summary>
    /// <param name="eventData">Pointer event data.</param>
    public virtual void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
        if (!_interactable) return;
        if (_enableDebugLogs) Debug.Log($"[UICustomButtonBase] OnPointerEnter triggered on '{gameObject.name}'");
        onPointerEnter.Invoke();
    }

    /// <summary>
    /// Description: Handles pointer exit event, invoking callbacks.
    /// Context: EventSystem callback.
    /// Justification: Base functionality for hover exit logic.
    /// </summary>
    /// <param name="eventData">Pointer event data.</param>
    public virtual void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        if (!_interactable) return;
        if (_enableDebugLogs) Debug.Log($"[UICustomButtonBase] OnPointerExit triggered on '{gameObject.name}'");
        onPointerExit.Invoke();
    }

    /// <summary>
    /// Description: Handles pointer down event, invoking callbacks.
    /// Context: EventSystem callback.
    /// Justification: Base functionality for press down logic.
    /// </summary>
    /// <param name="eventData">Pointer event data.</param>
    public virtual void OnPointerDown(PointerEventData eventData)
    {
        if (!_interactable) return;
        if (_enableDebugLogs) Debug.Log($"[UICustomButtonBase] OnPointerDown triggered on '{gameObject.name}'");
        onPointerDown.Invoke();
    }

    /// <summary>
    /// Description: Handles pointer up event, invoking callbacks.
    /// Context: EventSystem callback.
    /// Justification: Base functionality for release logic.
    /// </summary>
    /// <param name="eventData">Pointer event data.</param>
    public virtual void OnPointerUp(PointerEventData eventData)
    {
        if (!_interactable) return;
        if (_enableDebugLogs) Debug.Log($"[UICustomButtonBase] OnPointerUp triggered on '{gameObject.name}'");
        onPointerUp.Invoke();
    }

    /// <summary>
    /// Description: Handles pointer click event, invoking the onClick event if left clicked.
    /// Context: EventSystem callback.
    /// Justification: Base functionality for standard button clicks.
    /// </summary>
    /// <param name="eventData">Pointer event data.</param>
    public virtual void OnPointerClick(PointerEventData eventData)
    {
        if (!_interactable) return;
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (_enableDebugLogs) Debug.Log($"[UICustomButtonBase] OnPointerClick (Left-Click) triggered on '{gameObject.name}'");
            onClick.Invoke();
        }
    }
}
