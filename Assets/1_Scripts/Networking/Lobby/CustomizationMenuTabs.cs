using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Description: Simple UI tab controller for the customization menu.
/// Context: Lobby customization UI.
/// Justification: Keeps the UI clean by separating sections (Appearance, Drawing, Audio) without cluttering LobbyController.
/// </summary>
public class CustomizationMenuTabs : MonoBehaviour
{
    /// <summary>
    /// Description: Pairs a tab button with the panel it opens.
    /// Context: Configured in the Inspector through the Tabs array.
    /// Justification: Keeps each button and its panel together so indices can never drift apart.
    /// </summary>
    [System.Serializable]
    public struct Tab
    {
        [Tooltip("Role: Button that opens this tab.")]
        public UICustomButtonBase TabButton;
        [Tooltip("Role: Panel shown while this tab is active.")]
        public GameObject TabPanel;
    }

    [Tooltip("Role: List of all tabs in the menu.\nUse Case: UI Navigation.\nJustification: Used to toggle active panels based on button clicks.")]
    public Tab[] Tabs;

    [Tooltip("Role: The index of the tab to open by default.\nUse Case: Initialization.")]
    public int DefaultTabIndex = 0;

    private void Start()
    {
        // Bind buttons
        for (int i = 0; i < Tabs.Length; i++)
        {
            int index = i; // Capture index for closure
            if (Tabs[i].TabButton != null)
            {
                Tabs[i].TabButton.onClick.AddListener(() => OpenTab(index));
            }
        }

        // Open default tab
        if (Tabs.Length > 0)
        {
            OpenTab(DefaultTabIndex);
        }
    }

    /// <summary>
    /// Shows the panel at the given index, hides the others and disables the active tab's button.
    /// </summary>
    public void OpenTab(int index)
    {
        for (int i = 0; i < Tabs.Length; i++)
        {
            if (Tabs[i].TabPanel != null)
            {
                Tabs[i].TabPanel.SetActive(i == index);
            }
            
            // Optionally highlight the active button if UICustomButtonBase supports it
            if (Tabs[i].TabButton != null)
            {
                Tabs[i].TabButton.Interactable = (i != index);
            }
        }
    }
}

