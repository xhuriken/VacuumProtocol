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
            Debug.Log("[InputSettingsConsumer] Controls successfully re-bound from overrides.");
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
        
        Debug.Log("[InputSettingsConsumer] All bindings reset to default.");
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
}
