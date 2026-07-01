using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Listens for the Escape key to toggle the in-game Pause Panel.
/// Integrates sub-panel hiding (like the Settings Panel) to ensure correct closing order.
/// </summary>
public class InGameMenuController : MonoBehaviour
{
    [Tooltip("The main pause menu panel that displays options when the game is paused.")]
    [SerializeField] private UIPanelController _pausePanel;

    [Tooltip("Optional reference to the settings panel to close it if the player hits Escape while it is active.")]
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
    /// Toggles the pause menu visibility.
    /// If the settings sub-panel is currently active, closes it first.
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
