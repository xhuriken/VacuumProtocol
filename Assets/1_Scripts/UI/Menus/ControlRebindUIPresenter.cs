using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Description: Presenter that coordinates the key rebinding UI panel.
/// Context: Attached to the Controls Panel UI GameObject in the canvas.
/// Justification: Implements the MVP/Presenter pattern for control settings, decoupling the individual row logic from the global settings manager.
/// </summary>
public class ControlRebindUIPresenter : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Role: The input settings consumer that handles actual rebinding logic.\nUse Case: Key mapping.\nJustification: Decouples UI buttons from direct Input System interaction.")]
    [SerializeField] private InputSettingsConsumer _inputSettingsConsumer;

    [Header("UI Elements")]
    [Tooltip("Role: List of rebind rows in this panel.\nUse Case: Bulk refresh.\nJustification: Allows iterating and initializing each key row dynamically.")]
    [SerializeField] private List<RebindRowUI> _rebindRows = new List<RebindRowUI>();

    [Tooltip("Role: Button that resets all custom bindings back to defaults.\nUse Case: Control reset.\nJustification: Clears overrides in one click for player convenience.")]
    [SerializeField] private UICustomButtonBase _resetButton;

    /// <summary>
    /// Description: Unity OnEnable callback. Initializes UI rows and registers listeners.
    /// Context: Lifecycle event.
    /// Justification: Begins listening to settings adjustments and click commands.
    /// </summary>
    private void OnEnable()
    {
        // Setup row links
        InitializeRows();

        // Bind reset button click
        if (_resetButton != null)
        {
            _resetButton.onClick.AddListener(ResetAllBindings);
        }

        // Listen for global settings changes (e.g. if another script resets settings)
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.OnSettingsChanged += HandleSettingsChanged;
        }
    }

    /// <summary>
    /// Description: Unity OnDisable callback. Unregisters settings and click listeners.
    /// Context: Lifecycle event.
    /// Justification: Cleanup avoids memory leaks.
    /// </summary>
    private void OnDisable()
    {
        // Unbind reset button safely
        if (_resetButton != null)
        {
            _resetButton.onClick.RemoveListener(ResetAllBindings);
        }

        // Unbind global settings listener safely
        if (SettingsManager.HasInstance)
        {
            SettingsManager.Instance.OnSettingsChanged -= HandleSettingsChanged;
        }
    }

    /// <summary>
    /// Description: Initializes each rebind row UI element with the settings consumer.
    /// Context: Internal setup.
    /// Justification: Distributes dependencies to row sub-components.
    /// </summary>
    private void InitializeRows()
    {
        // Auto-locate settings consumer if not manually assigned
        if (_inputSettingsConsumer == null)
        {
            _inputSettingsConsumer = FindFirstObjectByType<InputSettingsConsumer>();
            if (_inputSettingsConsumer == null)
            {
                Debug.LogWarning("[ControlRebindUIPresenter] InputSettingsConsumer not found. Rows cannot be initialized.");
                return;
            }
        }

        // Setup every row reference
        foreach (var row in _rebindRows)
        {
            if (row != null)
            {
                row.Initialize(_inputSettingsConsumer);
            }
        }

        // Run initial conflict check
        CheckForDuplicateBindings();
    }

    /// <summary>
    /// Description: Resets all controls back to default mappings.
    /// Context: UI reset button click callback.
    /// Justification: Simplifies control restorations.
    /// </summary>
    private void ResetAllBindings()
    {
        if (_inputSettingsConsumer == null) return;

        // Command global settings reset
        _inputSettingsConsumer.ResetToDefaultBindings();
        
        // Redraw rows
        RefreshAllRows();
    }

    /// <summary>
    /// Description: Event listener callback when settings update.
    /// Context: Settings observer.
    /// Justification: Dynamically refreshes UI layout.
    /// </summary>
    /// <param name="settings">The updated settings data.</param>
    private void HandleSettingsChanged(SettingsData settings)
    {
        // Redraw all keys
        RefreshAllRows();
    }

    /// <summary>
    /// Description: Forces all rebind rows to update their display values.
    /// Context: Called when settings change or defaults are restored.
    /// Justification: Keeps UI elements synchronized with active binding configurations.
    /// </summary>
    public void RefreshAllRows()
    {
        // Refresh text representation for each row
        foreach (var row in _rebindRows)
        {
            if (row != null)
            {
                row.RefreshDisplay();
            }
        }

        // Refresh color configurations based on duplication conflicts
        CheckForDuplicateBindings();
    }

    /// <summary>
    /// Description: Scans all rebind rows for duplicate key assignments and colors conflicts in red.
    /// Context: Called after refreshing all rows.
    /// Justification: Provides visual feedback to prevent key conflicts.
    /// </summary>
    private void CheckForDuplicateBindings()
    {
        // Store visual string mappings to check conflicts
        Dictionary<string, int> bindingCounts = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);

        // Count duplicates
        foreach (var row in _rebindRows)
        {
            if (row == null) continue;
            string bindStr = row.GetCurrentBindingString();
            if (string.IsNullOrEmpty(bindStr)) continue;

            if (bindingCounts.ContainsKey(bindStr))
            {
                bindingCounts[bindStr]++;
            }
            else
            {
                bindingCounts[bindStr] = 1;
            }
        }

        // Apply visual highlights based on duplication count
        foreach (var row in _rebindRows)
        {
            if (row == null) continue;
            string bindStr = row.GetCurrentBindingString();

            if (!string.IsNullOrEmpty(bindStr) && bindingCounts.TryGetValue(bindStr, out int count) && count > 1)
            {
                // Conflict found: set button text to red
                row.SetBindingTextColor(Color.red);
            }
            else
            {
                // No conflict: restore original visual color
                row.ResetBindingTextColor();
            }
        }
    }

    /// <summary>
    /// Description: Checks if any row is currently listening for key input during rebinding.
    /// Context: Interaction lock.
    /// Justification: Prevents overlapping rebinding loops from different rows.
    /// </summary>
    /// <returns>True if a row is rebinding, false otherwise.</returns>
    public bool IsAnyRowRebinding()
    {
        foreach (var row in _rebindRows)
        {
            if (row != null && row.IsListening)
            {
                return true;
            }
        }
        return false;
    }
}
