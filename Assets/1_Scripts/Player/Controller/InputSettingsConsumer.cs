using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Decoupled settings consumer that applies and manages key rebinding overrides using Unity's New Input System.
/// </summary>
public class InputSettingsConsumer : MonoBehaviour, ISettingsConsumer
{
    [Tooltip("The Input Action Asset containing the bindings to override.")]
    [SerializeField] private InputActionAsset _inputActions;

    private string _lastAppliedBindingsJson;

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

    private void OnDestroy()
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.UnregisterConsumer(this);
        }
    }

    /// <summary>
    /// Reads custom action map override data and rebinds controls.
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
    /// Resets all overrides in the input actions asset back to default.
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
    /// Starts interactive rebind procedure for a specific action.
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
