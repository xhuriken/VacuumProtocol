using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Description: Listens for the Escape key to toggle the in-game Pause Panel.
/// Context: Attached to an invisible Input Manager object in gameplay scenes.
/// Justification: Integrates sub-panel hiding (like the Settings Panel) to ensure correct closing order.
/// </summary>
public class InGameMenuController : MonoBehaviour
{
    [Tooltip("Role: The main pause menu panel.\nUse Case: Displaying options when paused.\nJustification: Required to show resume/quit buttons.")]
    [SerializeField] private UIPanelController _pausePanel;

    [Tooltip("Role: The settings sub-panel.\nUse Case: Handling nested UI closing.\nJustification: Closes it if the player hits Escape while it is active.")]
    [SerializeField] private UIPanelController _settingsPanel;

    private void Update()
    {
        bool escapePressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            escapePressed = true;
        }
#else
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            escapePressed = true;
        }
#endif

        if (escapePressed)
        {
            TogglePauseMenu();
        }
    }

    /// <summary>
    /// Description: Toggles the pause menu visibility.
    /// Context: Triggered by Escape key or UI buttons.
    /// Justification: If the settings sub-panel is currently active, it correctly closes it first.
    /// </summary>
    public void TogglePauseMenu()
    {
        if (_pausePanel == null) return;

        if (_pausePanel.IsOpened)
        {
            // Close settings if it was opened on top of the pause panel
            if (_settingsPanel != null && _settingsPanel.IsOpened)
            {
                _settingsPanel.Hide();
                return; // Consume escape to only close settings first
            }
            _pausePanel.Hide();
        }
        else
        {
            _pausePanel.Show();
        }
    }
}
