using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Description: Manages a group of mutually exclusive panels.
/// Context: Attached to a parent canvas object.
/// Justification: Tracks historical navigation stack to allow simple backing operations (e.g. Settings -> Main).
/// </summary>
public class UINavigationGroup : MonoBehaviour
{
    [Tooltip("Role: The panel that should be active at startup.\nUse Case: Default view.\nJustification: Guarantees the player sees something initially.")]
    [SerializeField] private UIPanelController _defaultPanel;

    [Tooltip("Role: All panels managed under this mutually exclusive group.\nUse Case: Bulk tracking.\nJustification: Needed to instant-hide panels that aren't the default.")]
    [SerializeField] private List<UIPanelController> _panels = new List<UIPanelController>();

    [Tooltip("Role: If enabled, pressing the Escape key will automatically invoke GoBack.\nUse Case: Hardware back navigation.\nJustification: Allows Escape to function as a universal back button in menus.")]
    [SerializeField] private bool _goBackOnEscape = true;

    [Tooltip("Role: The stack tracking Left panel navigation history.\nUse Case: History stack.\nJustification: Allows going back to previous Left panels dynamically.")]
    private readonly Stack<UIPanelController> _history = new Stack<UIPanelController>();

    [Tooltip("Role: The currently active Left panel.\nUse Case: State tracking.\nJustification: Helps manage mutual exclusivity for Left layout panels.")]
    private UIPanelController _currentLeftPanel;

    [Tooltip("Role: The currently active Right panel.\nUse Case: State tracking.\nJustification: Helps manage mutual exclusivity for Right layout panels.")]
    private UIPanelController _currentRightPanel;

    /// <summary>
    /// Description: Unity Start callback. Hides all non-default panels instantly and opens the default Left panel.
    /// Context: Lifecycle event.
    /// Justification: Sets up the initial clean state of the main menu.
    /// </summary>
    private void Start()
    {
        // Enforce instant hiding on all panels except the default one on start
        foreach (var panel in _panels)
        {
            if (panel != null && panel != _defaultPanel)
            {
                // Instantly deactivate non-default panels to prevent visual overlays
                panel.Hide();
            }
        }

        if (_defaultPanel != null)
        {
            // Open the main menu panel without adding it to history
            OpenPanel(_defaultPanel, false);
        }
    }

    /// <summary>
    /// Description: Unity Update callback. Listens to the hardware Escape key to trigger back navigation.
    /// Context: Input polling loop.
    /// Justification: Standardizes user navigation by mapping Escape to back buttons.
    /// </summary>
    private void Update()
    {
        if (_goBackOnEscape)
        {
#if ENABLE_INPUT_SYSTEM
            // Query the new Input System keyboard state
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                GoBack();
            }
#else
            // Fallback to legacy Input Manager
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                GoBack();
            }
#endif
        }
    }

    /// <summary>
    /// Description: Deactivates the active panel on the corresponding side and activates the target panel.
    /// Context: Called by Navigation UI Buttons.
    /// Justification: Separates Left and Right panel lifecycles so settings menus don't cover sub-options.
    /// </summary>
    /// <param name="targetPanel">The panel to display.</param>
    public void OpenPanel(UIPanelController targetPanel)
    {
        // Call the private overload recording history by default
        OpenPanel(targetPanel, true);
    }

    /// <summary>
    /// Description: Handles the split Left/Right show/hide panel logic.
    /// Context: Called internally by the public OpenPanel.
    /// Justification: Implements side-aware UI switching without destroying canvas structures.
    /// </summary>
    /// <param name="targetPanel">The panel to display.</param>
    /// <param name="recordHistory">If true, saves the current panel to history before transition.</param>
    private void OpenPanel(UIPanelController targetPanel, bool recordHistory)
    {
        if (targetPanel == null) return;

        // Process Left panels (e.g. Main Menu, Settings Category list)
        if (targetPanel.Side == UIPanelController.PanelSide.Left)
        {
            // Prevent redundant animations if clicking already active left panel
            if (targetPanel == _currentLeftPanel) return;

            if (_currentLeftPanel != null)
            {
                if (recordHistory)
                {
                    // Push the current Left panel to history so we can return to it
                    _history.Push(_currentLeftPanel);
                }
                _currentLeftPanel.Hide();
            }

            _currentLeftPanel = targetPanel;
            _currentLeftPanel.Show();

            // When returning to the default Left panel (MainMenu), automatically close any active Right panel
            if (targetPanel == _defaultPanel && _currentRightPanel != null)
            {
                _currentRightPanel.Hide();
                _currentRightPanel = null;
            }
        }
        else // Process Right panels (e.g. Controls, Audio details)
        {
            // Prevent redundant animations if clicking already active right panel
            if (targetPanel == _currentRightPanel) return;

            // Hide the active Right panel to show the new one in its place
            if (_currentRightPanel != null)
            {
                _currentRightPanel.Hide();
            }

            _currentRightPanel = targetPanel;
            _currentRightPanel.Show();
        }
    }

    /// <summary>
    /// Description: Hides the active panel and displays the previous panel in the history stack.
    /// Context: Called by 'Back' UI Buttons or Escape.
    /// Justification: Supports nested menus by popping the last navigation state and restoring Left/Right panels accordingly.
    /// </summary>
    public void GoBack()
    {
        // If history is empty but we have a Right panel open, close the Right panel as fallback
        if (_history.Count == 0)
        {
            if (_currentRightPanel != null)
            {
                _currentRightPanel.Hide();
                _currentRightPanel = null;
            }
            return;
        }

        // Pop the previous Left panel from history
        var previousPanel = _history.Pop();

        if (previousPanel != null && previousPanel.Side == UIPanelController.PanelSide.Left)
        {
            if (_currentLeftPanel != null)
            {
                _currentLeftPanel.Hide();
            }

            _currentLeftPanel = previousPanel;
            _currentLeftPanel.Show();

            // If we are back to the default Left panel (MainMenu), hide the active Right panel
            if (previousPanel == _defaultPanel && _currentRightPanel != null)
            {
                _currentRightPanel.Hide();
                _currentRightPanel = null;
            }
        }
    }
}
