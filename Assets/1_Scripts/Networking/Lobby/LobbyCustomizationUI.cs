using Mirror;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using VacuumProtocol.Player.Visuals;

namespace VacuumProtocol.Networking.Lobby
{
    /// <summary>
    /// Description: UI Script to be attached to the Lobby Canvas. Handles sending UI events to the local player's PlayerCustomization script.
    /// Context: Runs in the Lobby scene.
    /// Justification: Bridges standard UGUI events (buttons, text fields) to network commands, allowing offline preview dummies.
    /// </summary>
    public class LobbyCustomizationUI : MonoBehaviour
    {
        [Header("Scene References")]
        [Tooltip("Role: The local dummy player in the scene.\nUse Case: Offline previews.\nJustification: Allows customizing without needing an active network connection.")]
        public PlayerCustomization PreviewPlayer;

        [Header("Debug")]
        [Tooltip("Role: Toggle for debug logs.\nUse Case: Troubleshooting.\nJustification: Customization UI can spam console during rapid color switching.")]
        public bool EnableDebugLogs = true;

        // ----------------------------------------------------
        // PUBLIC METHODS TO LINK IN THE UNITY INSPECTOR (On Click Events)
        // ----------------------------------------------------

        /// <summary>
        /// Description: Call this from a UI Button (On Click) and pass the Color you want.
        /// Context: Unity UI Event.
        /// Justification: Standard way to apply a pure Unity Color to the customization system.
        /// </summary>
        public void SetPlayerColor(Color newColor)
        {
            if (EnableDebugLogs) Debug.Log($"[LobbyUI] SetPlayerColor called with color {newColor}");
            var targetPlayer = GetTargetPlayer();
            if (targetPlayer != null)
            {
                if (EnableDebugLogs) Debug.Log($"[LobbyUI] Target player found. Requesting color change...");
                targetPlayer.RequestColorChange(newColor);
            }
            else if (EnableDebugLogs) Debug.LogWarning("[LobbyUI] Target player is null! Color change aborted.");
        }

        /// <summary>
        /// Description: Unity's Inspector Button OnClick doesn't support the Color type. Use this method instead and pass a string.
        /// Context: Unity UI Event (InputField or string-based Button).
        /// Justification: Allows players to paste raw Hex codes directly into the UI.
        /// </summary>
        public void SetPlayerColorHex(string hexColor)
        {
            if (EnableDebugLogs) Debug.Log($"[LobbyUI] SetPlayerColorHex called with string '{hexColor}'");
            // Auto-add '#' if the user forgot it and typed a 6-character hex code
            if (!hexColor.StartsWith("#") && hexColor.Length == 6)
            {
                hexColor = "#" + hexColor;
                if (EnableDebugLogs) Debug.Log($"[LobbyUI] Auto-added '#' -> new string is '{hexColor}'");
            }

            if (ColorUtility.TryParseHtmlString(hexColor, out Color parsedColor))
            {
                if (EnableDebugLogs) Debug.Log($"[LobbyUI] Successfully parsed hex '{hexColor}' into Color {parsedColor}");
                // SAVE IT LOCALLY!
                PlayerPrefs.SetString("PlayerColorHex", hexColor);
                PlayerPrefs.Save();


                SetPlayerColor(parsedColor);
            }
            else
            {
                Debug.LogError($"[LobbyUI] Invalid color string: {hexColor}. Please use a hex format like #FF0000 or names like red, blue.");
            }
        }

        /// <summary>
        /// Description: Call this from a UI Button (On Click) and pass the integer value of the Enum.
        /// Context: Unity UI Event.
        /// Justification: Binds the UI audio pitch buttons to the MusicalNote enum.
        /// </summary>
        public void SetPlayerNote(int noteIndex)
        {
            if (EnableDebugLogs) Debug.Log($"[LobbyUI] SetPlayerNote called with index {noteIndex}");
            // SAVE IT LOCALLY!
            PlayerPrefs.SetInt("PlayerNoteIndex", noteIndex);
            PlayerPrefs.Save();

            var targetPlayer = GetTargetPlayer();
            if (targetPlayer != null)
            {
                MusicalNote newNote = (MusicalNote)noteIndex;
                if (EnableDebugLogs) Debug.Log($"[LobbyUI] Target player found. Requesting note change to {newNote}...");
                targetPlayer.RequestNoteChange(newNote);
            }
            else if (EnableDebugLogs) Debug.LogWarning("[LobbyUI] Target player is null! Note change aborted.");
        }

        /// <summary>
        /// Description: Call this from an EventTrigger (Pointer Down) on a UI Button to preview the vacuum sound.
        /// Context: Unity UI Event.
        /// Justification: Tests the chosen audio settings dynamically before entering a game.
        /// </summary>
        public void StartPreviewVacuum()
        {
            if (EnableDebugLogs) Debug.Log("[LobbyUI] StartPreviewVacuum called (Pointer Down)");
            var targetPlayer = GetTargetPlayer();
            if (targetPlayer != null)
            {
                targetPlayer.RequestVacuumTest(true);
            }
            else if (EnableDebugLogs) Debug.LogWarning("[LobbyUI] Target player is null! Vacuum preview aborted.");
        }

        /// <summary>
        /// Description: Call this from an EventTrigger (Pointer Up) on the same UI Button.
        /// Context: Unity UI Event.
        /// Justification: Stops the preview sound once the button is released.
        /// </summary>
        public void StopPreviewVacuum()
        {
            if (EnableDebugLogs) Debug.Log("[LobbyUI] StopPreviewVacuum called (Pointer Up)");
            var targetPlayer = GetTargetPlayer();
            if (targetPlayer != null)
            {
                targetPlayer.RequestVacuumTest(false);
            }
            else if (EnableDebugLogs) Debug.LogWarning("[LobbyUI] Target player is null! Vacuum stop aborted.");
        }

        // ----------------------------------------------------
        // INTERNAL LOGIC
        // ----------------------------------------------------

        private bool _isHoldingBothClicks = false;

        private void Update()
        {
            // Allow the user to test the vacuum simply by holding left & right click in the lobby using the New Input System
            if (Mouse.current != null && Mouse.current.leftButton.isPressed && Mouse.current.rightButton.isPressed)
            {
                if (!_isHoldingBothClicks)
                {
                    _isHoldingBothClicks = true;
                    StartPreviewVacuum();
                }
            }
            else
            {
                if (_isHoldingBothClicks)
                {
                    _isHoldingBothClicks = false;
                    StopPreviewVacuum();
                }
            }
        }

        private PlayerCustomization GetTargetPlayer()
        {
            // 1. If you manually linked the dummy player in the scene, use it!
            if (PreviewPlayer != null) 
            {
                if (EnableDebugLogs) Debug.Log("[LobbyUI] GetTargetPlayer returned the manually linked PreviewPlayer.");
                return PreviewPlayer;
            }

            // 2. Otherwise, look for the networked local player
            if (NetworkClient.localPlayer != null)
            {
                if (EnableDebugLogs) Debug.Log("[LobbyUI] GetTargetPlayer returned NetworkClient.localPlayer.");
                return NetworkClient.localPlayer.GetComponent<PlayerCustomization>();
            }

            // 3. Fallback: just find any player in the scene
            Debug.LogWarning("[LobbyUI] Local player not found yet. Make sure you are spawned in the lobby, biatch");
            var fallback = FindObjectOfType<PlayerCustomization>();
            if (fallback != null && EnableDebugLogs) Debug.Log("[LobbyUI] GetTargetPlayer returned a fallback player found in the scene.");
            else if (fallback == null && EnableDebugLogs) Debug.LogWarning("[LobbyUI] FindObjectOfType failed. No PlayerCustomization found in the scene.");
            
            return fallback;
        }
    }
}
