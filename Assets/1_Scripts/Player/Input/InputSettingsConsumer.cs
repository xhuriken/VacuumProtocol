using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Description: Decoupled settings consumer that applies and manages key rebinding overrides using Unity's New Input System.
/// Context: Attached to a persistent manager or input handler.
/// Justification: Follows the observer pattern to apply control changes automatically when the global settings update, keeping input handling isolated from UI logic.
/// </summary>
public class InputSettingsConsumer : MonoBehaviour, ISettingsConsumer
{
    [Tooltip("Role: The Input Action Asset containing the bindings to override.\nUse Case: Input rebinding.\nJustification: Modifies the in-memory overrides of this asset without permanently altering the source file.")]
    [SerializeField] private InputActionAsset _inputActions;

    [Tooltip("Role: Enable or disable verbose debug log printing.\nUse Case: Debugging.\nJustification: Follows the workspace logging guidelines to reduce console spam.")]
    [SerializeField] private bool _enableDebugLogs = false;

    private string _lastAppliedBindingsJson;

    /// <summary>
    /// Description: Start callback. Validates and registers the consumer.
    /// Context: Lifecycle event.
    /// Justification: Required to start listening to the SettingsManager for updates.
    /// </summary>
    private void Start()
    {
        if (_inputActions == null)
        {
            Debug.LogWarning("[InputSettingsConsumer] InputActionAsset is not assigned. Rebinding overrides cannot be applied.");
            return;
        }

        // Register with the settings manager
        SettingsManager.Instance.RegisterConsumer(this);
    }

    /// <summary>
    /// Description: OnDestroy callback. Unregisters the consumer safely.
    /// Context: Lifecycle event.
    /// Justification: Prevents memory leaks and null reference exceptions if this object is destroyed but the singleton manager persists.
    /// </summary>
    private void OnDestroy()
    {
        if (SettingsManager.HasInstance)
        {
            SettingsManager.Instance.UnregisterConsumer(this);
        }
    }

    /// <summary>
    /// Description: Reads custom action map override data and rebinds controls.
    /// Context: ISettingsConsumer implementation. Called by SettingsManager.
    /// Justification: Safely parses JSON overrides and applies them without freezing the main thread if the strings haven't changed.
    /// </summary>
    /// <param name="settings">The updated settings data.</param>
    public void OnSettingsUpdated(SettingsData settings)
    {
        if (_inputActions == null || settings == null) return;

        // Skip re-binding if the override string hasn't changed (prevents main thread freeze on slider changes)
        if (settings.ControlBindingsOverrideJson == _lastAppliedBindingsJson) return;

        _lastAppliedBindingsJson = settings.ControlBindingsOverrideJson;

        if (string.IsNullOrEmpty(_lastAppliedBindingsJson))
        {
            // If there are no overrides, remove all overrides to restore defaults
            _inputActions.RemoveAllBindingOverrides();
            return;
        }

        try
        {
            _inputActions.LoadBindingOverridesFromJson(_lastAppliedBindingsJson);
            if (_enableDebugLogs)
            {
                Debug.Log("[InputSettingsConsumer] Controls successfully re-bound from overrides.");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[InputSettingsConsumer] Failed to load control overrides: {ex.Message}");
        }
    }

    /// <summary>
    /// Description: Resets all overrides in the input actions asset back to default.
    /// Context: Called via UI button event.
    /// Justification: Wipes the overrides from memory and pushes the empty string back to the SettingsManager for saving.
    /// </summary>
    public void ResetToDefaultBindings()
    {
        if (_inputActions == null) return;

        _inputActions.RemoveAllBindingOverrides();

        SettingsManager.Instance.UpdateSettings(data =>
        {
            data.ControlBindingsOverrideJson = string.Empty;
        });
        
        if (_enableDebugLogs)
        {
            Debug.Log("[InputSettingsConsumer] All bindings reset to default.");
        }
    }

    /// <summary>
    /// Description: Resets a specific action binding override back to default.
    /// Context: Called by RebindRowUI reset button.
    /// Justification: Restores the default configuration for a single key.
    /// </summary>
    /// <param name="actionPathOrName">The name or path of the input action.</param>
    /// <param name="bindingIndex">The index of the binding to reset.</param>
    public void ResetBindingToDefault(string actionPathOrName, int bindingIndex)
    {
        // Search the action object by name
        var action = FindAction(actionPathOrName);
        if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count) return;

        // Strip rebind override back to default key
        action.RemoveBindingOverride(bindingIndex);

        // Serialize updated bindings to JSON format
        string overridesJson = _inputActions.SaveBindingOverridesAsJson();
        
        // Push the new bindings override map to SettingsManager
        SettingsManager.Instance.UpdateSettings(data =>
        {
            data.ControlBindingsOverrideJson = overridesJson;
        });

        // Log operation output
        if (_enableDebugLogs)
        {
            Debug.Log($"[InputSettingsConsumer] Reset action '{action.name}' index {bindingIndex} to default.");
        }
    }

    /// <summary>
    /// Description: Starts interactive rebind procedure for a specific action.
    /// Context: Called by UI rebinding scripts.
    /// Justification: Uses the Unity Input System's built-in interactive rebinding flow, safely blocking mouse clicks from accidentally being mapped as keyboard keys.
    /// </summary>
    /// <param name="actionToRebind">The InputAction to remap.</param>
    /// <param name="bindingIndex">The binding slot index.</param>
    public void RebindAction(InputAction actionToRebind, int bindingIndex)
    {
        if (actionToRebind == null) return;

        actionToRebind.Disable();

        var operation = actionToRebind.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("Mouse") // Avoid accidental mouse clicks remapping keys
            .OnComplete(op =>
            {
                actionToRebind.Enable();
                op.Dispose();

                // Save overrides back into SettingsManager
                string overridesJson = _inputActions.SaveBindingOverridesAsJson();
                SettingsManager.Instance.UpdateSettings(data =>
                {
                    data.ControlBindingsOverrideJson = overridesJson;
                });
                
                Debug.Log($"[InputSettingsConsumer] Successfully remapped action '{actionToRebind.name}' index {bindingIndex}.");
            })
            .Start();
    }

    /// <summary>
    /// Description: Finds an InputAction within the asset by name or path.
    /// Context: Internal helper.
    /// Justification: Centralizes action retrieval and logging for safety.
    /// </summary>
    /// <param name="actionPathOrName">The name or path of the input action.</param>
    /// <returns>The found InputAction, or null.</returns>
    public InputAction FindAction(string actionPathOrName)
    {
        if (_inputActions == null) return null;
        return _inputActions.FindAction(actionPathOrName);
    }

    /// <summary>
    /// Description: Gets the human-readable display string of a binding.
    /// Context: Called by UI to refresh button labels.
    /// Justification: Integrates Unity Input System's localization/naming.
    /// </summary>
    /// <param name="actionPathOrName">The name or path of the action.</param>
    /// <param name="bindingIndex">The index of the binding.</param>
    /// <returns>The human-readable display string, or empty string.</returns>
    public string GetBindingDisplayString(string actionPathOrName, int bindingIndex)
    {
        var action = FindAction(actionPathOrName);
        if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
        {
            return string.Empty;
        }
        return action.GetBindingDisplayString(bindingIndex);
    }

    /// <summary>
    /// Description: Starts interactive rebind procedure with callbacks for completion/cancellation and custom options.
    /// Context: Internal helper called by the delayed coroutine.
    /// Justification: Provides granular UX state control to show/hide "listening" status in the UI, and supports canceling via Escape.
    /// </summary>
    /// <param name="actionToRebind">The InputAction to remap.</param>
    /// <param name="bindingIndex">The binding slot index.</param>
    /// <param name="onComplete">Callback triggered when rebinding succeeds.</param>
    /// <param name="onCancel">Callback triggered when rebinding is canceled.</param>
    private void RebindActionInteractiveImmediate(InputAction actionToRebind, int bindingIndex, System.Action onComplete, System.Action onCancel)
    {
        if (actionToRebind == null) return;

        actionToRebind.Disable();

        var operation = actionToRebind.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("<Mouse>/position") // Allow mouse clicks but exclude cursor position axis rebinds
            .WithControlsExcluding("<Mouse>/delta")    // Exclude mouse drag/movement axis
            .WithControlsExcluding("<Mouse>/scroll")   // Exclude mouse scroll wheel
            .WithCancelingThrough("<Keyboard>/escape") // Exclude escape from being bound, use it to cancel instead
            .OnComplete(op =>
            {
                actionToRebind.Enable();
                op.Dispose();

                // Save overrides back into SettingsManager
                string overridesJson = _inputActions.SaveBindingOverridesAsJson();
                SettingsManager.Instance.UpdateSettings(data =>
                {
                    data.ControlBindingsOverrideJson = overridesJson;
                });
                
                if (_enableDebugLogs)
                {
                    Debug.Log($"[InputSettingsConsumer] Successfully remapped action '{actionToRebind.name}' index {bindingIndex}.");
                }
                onComplete?.Invoke();
            })
            .OnCancel(op =>
            {
                actionToRebind.Enable();
                op.Dispose();
                
                if (_enableDebugLogs)
                {
                    Debug.Log($"[InputSettingsConsumer] Rebind canceled for action '{actionToRebind.name}' index {bindingIndex}.");
                }
                onCancel?.Invoke();
            })
            .Start();
    }

    /// <summary>
    /// Description: Starts interactive rebind procedure using action string path with a one-frame safety delay.
    /// Context: Called by UI rebinding scripts.
    /// Justification: Exposes the string-based interface for UI rows. Waiting one frame ensures that the mouse click used to press the UI button is cleared from the input event queue before the rebind operation starts listening.
    /// </summary>
    /// <param name="actionPathOrName">The name or path of the action.</param>
    /// <param name="bindingIndex">The index of the binding.</param>
    /// <param name="onComplete">Callback triggered when rebinding succeeds.</param>
    /// <param name="onCancel">Callback triggered when rebinding is canceled.</param>
    public void RebindActionInteractive(string actionPathOrName, int bindingIndex, System.Action onComplete, System.Action onCancel)
    {
        var action = FindAction(actionPathOrName);
        if (action == null)
        {
            Debug.LogError($"[InputSettingsConsumer] Action '{actionPathOrName}' not found.");
            onCancel?.Invoke();
            return;
        }

        // Start rebind with a coroutine to wait 1 frame for the click to complete
        StartCoroutine(RebindActionCoroutine(action, bindingIndex, onComplete, onCancel));
    }

    /// <summary>
    /// Description: Coroutine that delays interactive rebinding by one frame.
    /// Context: Rebinding lifecycle.
    /// Justification: Clears the click event from the input event queue to prevent immediate registration of the initiating click.
    /// </summary>
    private System.Collections.IEnumerator RebindActionCoroutine(InputAction actionToRebind, int bindingIndex, System.Action onComplete, System.Action onCancel)
    {
        // Wait one frame to clear the pointer/mouse click event that initiated the rebinding process
        yield return null;

        RebindActionInteractiveImmediate(actionToRebind, bindingIndex, onComplete, onCancel);
    }
}
