using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages a group of mutually exclusive panels (e.g., swapping between Main, Settings, and Credits).
/// Tracks historical navigation stack to allow simple backing operations.
/// </summary>
public class UINavigationGroup : MonoBehaviour
{
    [Tooltip("The panel that should be active at startup.")]
    [SerializeField] private UIPanelController _defaultPanel;

    [Tooltip("All panels managed under this mutually exclusive group.")]
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
    /// Deactivates the active panel in the group and activates the target panel.
    /// Saves the current panel to navigation history.
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
    /// Hides the active panel and displays the previous panel in the history stack.
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
