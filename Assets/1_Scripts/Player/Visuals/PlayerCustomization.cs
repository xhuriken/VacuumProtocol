using Mirror;
using UnityEngine;

namespace VacuumProtocol.Player.Visuals
{
    /// <summary>
    /// Handles syncing player customization (color and root note) across the network.
    /// </summary>
    public class PlayerCustomization : NetworkBehaviour
    {
        [Header("References")]
        [Tooltip("The renderer whose material color will change.")]
        [SerializeField] private Renderer _modelRenderer;


        [Tooltip("The audio controller for the vacuum sound.")]
        [SerializeField] private VacuumAudioController _vacuumAudio;

        [Header("Debug")]
        public bool EnableDebugLogs = true;

        // SyncVars automatically sync from the server to all clients. 
        // When they change, they trigger the hook methods.
        [SyncVar(hook = nameof(OnColorChanged))]
        public Color PlayerColor = Color.white;

        [SyncVar(hook = nameof(OnNoteChanged))]
        public MusicalNote PlayerRootNote = MusicalNote.C;

        private Material _instancedMaterial;

        private void Awake()
        {
            // Create an instanced material so changing color doesn't affect all players
            if (_modelRenderer != null)
            {
                _instancedMaterial = _modelRenderer.material;
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            // Apply the current synced values to this client immediately upon joining
            ApplyColor(PlayerColor);
            ApplyNote(PlayerRootNote);
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            // As soon as our local player object spawns (Lobby OR Game), load from PlayerPrefs
            LoadSavedCustomization();
        }

        private void LoadSavedCustomization()
        {
            if (EnableDebugLogs) Debug.Log("[PlayerCustomization] LoadSavedCustomization called.");
            if (PlayerPrefs.HasKey("PlayerColorHex"))
            {
                string hex = PlayerPrefs.GetString("PlayerColorHex");
                if (EnableDebugLogs) Debug.Log($"[PlayerCustomization] Found saved color hex: {hex}");
                if (ColorUtility.TryParseHtmlString(hex, out Color savedColor))
                {
                    CmdChangeColor(savedColor); // If offline, this will throw a warning, which is why we might need to use RequestColorChange instead
                }
            }

            if (PlayerPrefs.HasKey("PlayerNoteIndex"))
            {
                int noteIndex = PlayerPrefs.GetInt("PlayerNoteIndex");
                if (EnableDebugLogs) Debug.Log($"[PlayerCustomization] Found saved note index: {noteIndex}");
                CmdChangeNote((MusicalNote)noteIndex); // Same here
            }
        }

        private void OnDestroy()
        {
            if (_instancedMaterial != null)
            {
                Destroy(_instancedMaterial);
            }
        }

        #region Hooks (Executed on all clients)

        private void OnColorChanged(Color oldColor, Color newColor)
        {
            if (EnableDebugLogs) Debug.Log($"[PlayerCustomization] SyncVar Hook OnColorChanged triggered. New Color: {newColor}");
            ApplyColor(newColor);
        }

        private void OnNoteChanged(MusicalNote oldNote, MusicalNote newNote)
        {
            if (EnableDebugLogs) Debug.Log($"[PlayerCustomization] SyncVar Hook OnNoteChanged triggered. New Note: {newNote}");
            ApplyNote(newNote);
        }

        private void ApplyColor(Color color)
        {
            if (EnableDebugLogs) Debug.Log($"[PlayerCustomization] ApplyColor called with color: {color}");
            if (_instancedMaterial != null)
            {
                if (EnableDebugLogs) Debug.Log("[PlayerCustomization] Material found, applying color.");
                // Assumes the standard shader color property. Change "_Color" to "_BaseColor" if using URP/HDRP.
                _instancedMaterial.SetColor("_Color", color);

                _instancedMaterial.SetColor("_BaseColor", color); // Safe fallback for URP
            }
            else if (EnableDebugLogs) Debug.LogWarning("[PlayerCustomization] ApplyColor failed because _instancedMaterial is null! Did you assign _modelRenderer?");
        }

        private void ApplyNote(MusicalNote note)
        {
            if (EnableDebugLogs) Debug.Log($"[PlayerCustomization] ApplyNote called with note: {note}");
            if (_vacuumAudio != null)
            {
                if (EnableDebugLogs) Debug.Log("[PlayerCustomization] VacuumAudioController found, applying note.");
                _vacuumAudio.SetRootNote(note);
            }
            else if (EnableDebugLogs) Debug.LogWarning("[PlayerCustomization] ApplyNote failed because _vacuumAudio is null!");
        }

        #endregion

        #region Public Requests (Safe for both Networked and Offline Dummies)

        public void RequestColorChange(Color newColor)
        {
            if (EnableDebugLogs) Debug.Log($"[PlayerCustomization] RequestColorChange called. NetworkActive={NetworkClient.active}, isOwned={isOwned}");
            if (isOwned)
            {
                if (EnableDebugLogs) Debug.Log("[PlayerCustomization] Sending CmdChangeColor to server.");
                CmdChangeColor(newColor);
            }
            else
            {
                if (EnableDebugLogs) Debug.Log("[PlayerCustomization] No network authority. Applying color locally (Preview Mode).");
                PlayerColor = newColor;
                ApplyColor(newColor);
            }
        }

        public void RequestNoteChange(MusicalNote newNote)
        {
            if (EnableDebugLogs) Debug.Log($"[PlayerCustomization] RequestNoteChange called. NetworkActive={NetworkClient.active}, isOwned={isOwned}");
            if (isOwned)
            {
                if (EnableDebugLogs) Debug.Log("[PlayerCustomization] Sending CmdChangeNote to server.");
                CmdChangeNote(newNote);
            }
            else
            {
                if (EnableDebugLogs) Debug.Log("[PlayerCustomization] No network authority. Applying note locally (Preview Mode).");
                PlayerRootNote = newNote;
                ApplyNote(newNote);
            }
        }

        public void RequestVacuumTest(bool isVacuuming)
        {
            if (EnableDebugLogs) Debug.Log($"[PlayerCustomization] RequestVacuumTest called ({isVacuuming}). NetworkActive={NetworkClient.active}, isOwned={isOwned}");
            if (isOwned)
            {
                if (EnableDebugLogs) Debug.Log("[PlayerCustomization] Sending CmdTestVacuum to server.");
                CmdTestVacuum(isVacuuming);
            }
            else
            {
                if (EnableDebugLogs) Debug.Log("[PlayerCustomization] No network authority. Applying vacuum test locally (Preview Mode).");
                if (_vacuumAudio != null)
                {
                    _vacuumAudio.SetVacuumState(isVacuuming);
                }
                else if (EnableDebugLogs) Debug.LogWarning("[PlayerCustomization] Vacuum audio is null!");
            }
        }

        #endregion

        #region Commands (Executed on the Server, requested by the Local Client)

        /// <summary>
        /// Called by the local client's UI to request a color change.
        /// </summary>
        [Command]
        private void CmdChangeColor(Color newColor)
        {
            if (EnableDebugLogs) Debug.Log($"[PlayerCustomization] Server executing CmdChangeColor: {newColor}");
            PlayerColor = newColor; // This updates the SyncVar on the server, pushing it to all clients
        }

        /// <summary>
        /// Called by the local client's UI to request a note change.
        /// </summary>
        [Command]
        private void CmdChangeNote(MusicalNote newNote)
        {
            if (EnableDebugLogs) Debug.Log($"[PlayerCustomization] Server executing CmdChangeNote: {newNote}");
            PlayerRootNote = newNote; // This updates the SyncVar on the server, pushing it to all clients
        }

        /// <summary>
        /// A small debug command to test the vacuum sound from the lobby.
        /// </summary>
        [Command]
        private void CmdTestVacuum(bool isVacuuming)
        {
            if (EnableDebugLogs) Debug.Log($"[PlayerCustomization] Server executing CmdTestVacuum: {isVacuuming}");
            RpcTestVacuum(isVacuuming);
        }

        // Executed on all clients to hear the preview
        [ClientRpc]
        private void RpcTestVacuum(bool isVacuuming)
        {
            if (EnableDebugLogs) Debug.Log($"[PlayerCustomization] Client executing RpcTestVacuum: {isVacuuming}");
            if (_vacuumAudio != null)
            {
                _vacuumAudio.SetVacuumState(isVacuuming);
            }
            else if (EnableDebugLogs) Debug.LogWarning("[PlayerCustomization] RpcTestVacuum failed: _vacuumAudio is null!");
        }

        #endregion
    }
}
