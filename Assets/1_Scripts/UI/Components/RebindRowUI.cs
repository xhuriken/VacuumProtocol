using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Description: Controls a single key rebinding UI row, displaying the action name and its current keybinding.
/// Context: Placed on a UI Row prefab/object containing a label and a button.
/// Justification: Separates the UI representation of a single row from the parent settings screen manager.
/// </summary>
public class RebindRowUI : MonoBehaviour
{
    [Header("UI Components")]
    [Tooltip("Role: The label showing the action name.\nUse Case: UI display.\nJustification: Identifies the control to the user.")]
    [SerializeField] private TextMeshProUGUI _actionLabelText;

    [Tooltip("Role: The text inside the rebind button showing the current key.\nUse Case: UI display.\nJustification: Shows what key is currently bound.")]
    [SerializeField] private TextMeshProUGUI _bindingButtonText;

    [Tooltip("Role: The button that triggers the rebinding process.\nUse Case: Interaction.\nJustification: Starts listening for inputs when clicked.")]
    [SerializeField] private UICustomButtonBase _rebindButton;

    [Tooltip("Role: The button that resets this specific binding to default.\nUse Case: Control reset.\nJustification: Restores the default key for this action.")]
    [SerializeField] private UICustomButtonBase _rowResetButton;

    [Header("Input Action Configuration")]
    [Tooltip("Role: The name/path of the action in the Input Action Asset.\nUse Case: Binding lookup.\nJustification: Maps this row to a specific action like 'Player/Jump'.")]
    [SerializeField] private string _actionName = "Player/Jump";

    [Tooltip("Role: The index of the binding in the input action.\nUse Case: Rebinding lookup.\nJustification: Identifies which binding in a composite (like WASD) or list to rebind.")]
    [SerializeField] private int _bindingIndex = 0;

    [Tooltip("Role: The injected input settings consumer.\nUse Case: Key mapping lookup.\nJustification: Allows querying active keys and starting interactive rebinding.")]
    private InputSettingsConsumer _consumer;

    [Tooltip("Role: Safety flag for active listening state.\nUse Case: UI locking.\nJustification: Blocks concurrent clicks from starting overlapping rebinding loops.")]
    private bool _isListening = false;

    [Tooltip("Role: Caches the original text color defined in the inspector/prefab.\nUse Case: State reset.\nJustification: Needed to restore visual style after clearing duplicate conflicts.")]
    private Color _originalTextColor = Color.white;

    /// <summary>
    /// Description: Gets the configured action name for this row.
    /// Context: Read access.
    /// Justification: Used by parent presenter to identify the mapped action.
    /// </summary>
    public string ActionName => _actionName;

    /// <summary>
    /// Description: Gets the configured binding index for this row.
    /// Context: Read access.
    /// Justification: Used by parent presenter to identify the target binding slot.
    /// </summary>
    public int BindingIndex => _bindingIndex;

    /// <summary>
    /// Description: Gets whether this row is actively listening for key input during rebinding.
    /// Context: Read access.
    /// Justification: Allows parent presenter and other rows to prevent concurrent rebind inputs.
    /// </summary>
    public bool IsListening => _isListening;

    /// <summary>
    /// Description: Initializes the row with the input settings consumer.
    /// Context: Called by the parent presenter on setup.
    /// Justification: Injection dependency pattern avoids using FindObjectOfType.
    /// </summary>
    /// <param name="consumer">The InputSettingsConsumer reference.</param>
    public void Initialize(InputSettingsConsumer consumer)
    {
        // Inject settings dependency
        _consumer = consumer;

        // Cache original text color for duplicate checks
        if (_bindingButtonText != null)
        {
            _originalTextColor = _bindingButtonText.color;
        }
        
        // Load active display label
        RefreshDisplay();

        // Subscribe UI click events
        if (_rebindButton != null)
        {
            _rebindButton.onClick.AddListener(StartRebindingProcess);
        }

        if (_rowResetButton != null)
        {
            _rowResetButton.onClick.AddListener(ResetRowBinding);
        }
    }

    /// <summary>
    /// Description: OnDestroy lifecycle event. Unregisters UI click listeners.
    /// Context: Cleanup.
    /// Justification: Prevents memory leaks when loading different menus.
    /// </summary>
    private void OnDestroy()
    {
        // Unbind event listeners safely
        if (_rebindButton != null)
        {
            _rebindButton.onClick.RemoveListener(StartRebindingProcess);
        }

        if (_rowResetButton != null)
        {
            _rowResetButton.onClick.RemoveListener(ResetRowBinding);
        }
    }

    /// <summary>
    /// Description: Refreshes the button label with the latest binding display string.
    /// Context: Called on startup, settings updates, or after a rebind completes/cancels.
    /// Justification: Displays up-to-date binding labels directly from the Input System.
    /// </summary>
    public void RefreshDisplay()
    {
        if (_consumer == null || string.IsNullOrEmpty(_actionName)) return;

        // Only update text if we are not actively listening for keyboard inputs
        if (!_isListening && _bindingButtonText != null)
        {
            _bindingButtonText.text = _consumer.GetBindingDisplayString(_actionName, _bindingIndex);
        }
    }

    /// <summary>
    /// Description: Initiates the interactive rebinding process.
    /// Context: Triggered by the rebind button onClick event.
    /// Justification: Puts the row in a 'listening' state and calls the settings consumer's rebind method.
    /// </summary>
    public void StartRebindingProcess()
    {
        // Prevent launching rebinding twice on the same row
        if (_consumer == null || _isListening) return;

        // Prevent launching rebinding if another row is already actively listening
        var presenter = GetComponentInParent<ControlRebindUIPresenter>();
        if (presenter != null && presenter.IsAnyRowRebinding()) return;

        _isListening = true;

        // Prompt user to press any key
        if (_bindingButtonText != null)
        {
            _bindingButtonText.text = "...Press Key...";
        }

        // Lock button during interaction
        if (_rebindButton != null)
        {
            _rebindButton.Interactable = false;
        }

        // Trigger interactive rebind via input consumer
        _consumer.RebindActionInteractive(
            _actionName,
            _bindingIndex,
            onComplete: () => HandleRebindCompleted(true),
            onCancel: () => HandleRebindCompleted(false)
        );
    }

    /// <summary>
    /// Description: Handles the conclusion of the interactive rebinding process.
    /// Context: Rebinding callbacks.
    /// Justification: Restores row interaction state.
    /// </summary>
    /// <param name="success">True if binding succeeded, false if canceled.</param>
    private void HandleRebindCompleted(bool success)
    {
        _isListening = false;

        // Restore rebind button interactivity
        if (_rebindButton != null)
        {
            _rebindButton.Interactable = true;
        }

        // Refresh label text
        RefreshDisplay();
    }

    /// <summary>
    /// Description: Resets this specific binding back to default.
    /// Context: Triggered by the row-specific reset button.
    /// Justification: Provides granular reset control per key.
    /// </summary>
    private void ResetRowBinding()
    {
        if (_consumer == null || string.IsNullOrEmpty(_actionName)) return;

        // Command consumer to remove override for this index
        _consumer.ResetBindingToDefault(_actionName, _bindingIndex);

        // Force visual update
        RefreshDisplay();
    }

    /// <summary>
    /// Description: Gets the current binding display string.
    /// Context: Read access.
    /// Justification: Used by parent presenter to check duplicates.
    /// </summary>
    /// <returns>The binding display string.</returns>
    public string GetCurrentBindingString()
    {
        if (_consumer == null || string.IsNullOrEmpty(_actionName)) return string.Empty;
        return _consumer.GetBindingDisplayString(_actionName, _bindingIndex);
    }

    /// <summary>
    /// Description: Sets the text color of the binding label.
    /// Context: Called by presenter to highlight duplicates.
    /// Justification: Provides visual alert for conflicting bindings.
    /// </summary>
    /// <param name="color">The highlight color (e.g. red).</param>
    public void SetBindingTextColor(Color color)
    {
        if (_bindingButtonText != null)
        {
            _bindingButtonText.color = color;
        }
    }

    /// <summary>
    /// Description: Resets the text color of the binding label to its original color.
    /// Context: Called by presenter when conflict is resolved.
    /// Justification: Reverts visual state.
    /// </summary>
    public void ResetBindingTextColor()
    {
        if (_bindingButtonText != null)
        {
            _bindingButtonText.color = _originalTextColor;
        }
    }
}
