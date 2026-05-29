using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Reusable base toolkit class for custom interactive UI elements.
/// Retrieves standard pointer events from Unity EventSystem and exposes virtual lifecycle hooks.
/// </summary>
public class UICustomButtonBase : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    /// <summary>
    /// Event triggered when the custom button is successfully clicked.
    /// </summary>
    public Button.ButtonClickedEvent onClick = new Button.ButtonClickedEvent();

    /// <summary>
    /// Event triggered when the pointer enters the button bounds.
    /// </summary>
    public Button.ButtonClickedEvent onPointerEnter = new Button.ButtonClickedEvent();

    /// <summary>
    /// Event triggered when the pointer leaves the button bounds.
    /// </summary>
    public Button.ButtonClickedEvent onPointerExit = new Button.ButtonClickedEvent();

    /// <summary>
    /// Event triggered when the pointer is pressed down.
    /// </summary>
    public Button.ButtonClickedEvent onPointerDown = new Button.ButtonClickedEvent();

    /// <summary>
    /// Event triggered when the pointer is released.
    /// </summary>
    public Button.ButtonClickedEvent onPointerUp = new Button.ButtonClickedEvent();

    [Header("Interactable State")]
    [SerializeField]
    private bool _interactable = true;

    private bool _isHovered = false;

    /// <summary>
    /// Gets whether the pointer is currently hovering over the button bounds.
    /// </summary>
    public bool IsHovered => _isHovered;

    /// <summary>
    /// Gets or sets whether this button is interactable. Modifying this triggers the virtual OnInteractableChanged hook.
    /// </summary>
    public bool Interactable
    {
        get => _interactable;
        set
        {
            if (_interactable != value)
            {
                _interactable = value;
                OnInteractableChanged(value);
            }
        }
    }

    /// <summary>
    /// Virtual lifecycle hook triggered when the button's interactability changes.
    /// </summary>
    /// <param name="isInteractable">The new interactability state.</param>
    protected virtual void OnInteractableChanged(bool isInteractable)
    {
        // Custom transitions can be defined by subclasses
    }

    /// <summary>
    /// Unity Awake callback. Performs safety validation to ensure pointer raycasts are configured.
    /// </summary>
    protected virtual void Awake()
    {
        // Validation check: UGUI requires a Graphic component with raycastTarget = true to receive pointer events
        Graphic graphic = GetComponent<Graphic>();
        if (graphic == null)
        {
            Debug.LogWarning($"[UICustomButtonBase] WARNING on '{gameObject.name}': Raycast target missing! A Graphic component (e.g. Image) is required for EventSystem raycasts. Adding a transparent Image dynamically to resolve this.");
            
            Image dynamicImage = gameObject.AddComponent<Image>();
            dynamicImage.color = new Color(0f, 0f, 0f, 0f);
            dynamicImage.raycastTarget = true;
        }
        else if (!graphic.raycastTarget)
        {
            Debug.LogWarning($"[UICustomButtonBase] WARNING on '{gameObject.name}': The {graphic.GetType().Name} component has raycastTarget set to FALSE. Enabling raycastTarget dynamically so events can fire.");
            graphic.raycastTarget = true;
        }
    }

    /// <summary>
    /// Automatically resets the hover state when the component is disabled.
    /// </summary>
    protected virtual void OnDisable()
    {
        _isHovered = false;
    }

    /// <summary>
    /// Handles pointer enter event, invoking callbacks.
    /// </summary>
    /// <param name="eventData">Pointer event data.</param>
    public virtual void OnPointerEnter(PointerEventData eventData)
    {
        if (!_interactable) return;
        Debug.Log($"[UICustomButtonBase] OnPointerEnter triggered on '{gameObject.name}'");
        _isHovered = true;
        onPointerEnter.Invoke();
    }

    /// <summary>
    /// Handles pointer exit event, invoking callbacks.
    /// </summary>
    /// <param name="eventData">Pointer event data.</param>
    public virtual void OnPointerExit(PointerEventData eventData)
    {
        if (!_interactable) return;
        Debug.Log($"[UICustomButtonBase] OnPointerExit triggered on '{gameObject.name}'");
        _isHovered = false;
        onPointerExit.Invoke();
    }

    /// <summary>
    /// Handles pointer down event, invoking callbacks.
    /// </summary>
    /// <param name="eventData">Pointer event data.</param>
    public virtual void OnPointerDown(PointerEventData eventData)
    {
        if (!_interactable) return;
        Debug.Log($"[UICustomButtonBase] OnPointerDown triggered on '{gameObject.name}'");
        onPointerDown.Invoke();
    }

    /// <summary>
    /// Handles pointer up event, invoking callbacks.
    /// </summary>
    /// <param name="eventData">Pointer event data.</param>
    public virtual void OnPointerUp(PointerEventData eventData)
    {
        if (!_interactable) return;
        Debug.Log($"[UICustomButtonBase] OnPointerUp triggered on '{gameObject.name}'");
        onPointerUp.Invoke();
    }

    /// <summary>
    /// Handles pointer click event, invoking the onClick event if left clicked.
    /// </summary>
    /// <param name="eventData">Pointer event data.</param>
    public virtual void OnPointerClick(PointerEventData eventData)
    {
        if (!_interactable) return;
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            Debug.Log($"[UICustomButtonBase] OnPointerClick (Left-Click) triggered on '{gameObject.name}'");
            onClick.Invoke();
        }
    }
}
