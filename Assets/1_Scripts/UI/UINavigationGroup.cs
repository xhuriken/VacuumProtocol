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

    private readonly Stack<UIPanelController> _history = new Stack<UIPanelController>();
    private UIPanelController _currentPanel;

    private void Start()
    {
        // Enforce instant hiding on all panels except the default one on start
        foreach (var panel in _panels)
        {
            if (panel != null && panel != _defaultPanel)
            {
                panel.Hide();
            }
        }

        if (_defaultPanel != null)
        {
            OpenPanel(_defaultPanel, false);
        }
    }

    /// <summary>
    /// Description: Deactivates the active panel in the group and activates the target panel.
    /// Context: Called by Navigation UI Buttons.
    /// Justification: Saves the current panel to navigation history for the 'GoBack' function.
    /// </summary>
    /// <param name="targetPanel">The panel to display.</param>
    public void OpenPanel(UIPanelController targetPanel)
    {
        OpenPanel(targetPanel, true);
    }

    private void OpenPanel(UIPanelController targetPanel, bool recordHistory)
    {
        if (targetPanel == null || targetPanel == _currentPanel) return;

        if (_currentPanel != null)
        {
            if (recordHistory)
            {
                _history.Push(_currentPanel);
            }
            _currentPanel.Hide();
        }

        _currentPanel = targetPanel;
        _currentPanel.Show();
    }

    /// <summary>
    /// Description: Hides the active panel and displays the previous panel in the history stack.
    /// Context: Called by 'Back' UI Buttons.
    /// Justification: Allows nesting UI screens (like Settings within Settings) cleanly.
    /// </summary>
    public void GoBack()
    {
        if (_history.Count == 0) return;

        var previousPanel = _history.Pop();
        if (_currentPanel != null)
        {
            _currentPanel.Hide();
        }

        _currentPanel = previousPanel;
        _currentPanel.Show();
    }
}
