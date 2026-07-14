using Mirror;
using UnityEngine;

namespace VacuumProtocol.Player.Visuals
{
    /// <summary>
    /// Description: Handles syncing player customization (color and root note) across the network.
    /// Context: Attached to the player prefab.
    /// Justification: Centralizes visual and audio customization data and uses Mirror SyncVars to ensure late-joiners see the correct colors and hear the correct notes.
    /// </summary>
    public class PlayerCustomization : NetworkBehaviour
    {
        [Header("References")]
        [Tooltip("Role: The renderer whose material color will change.\nUse Case: Visual customization.\nJustification: Allows targeting a specific sub-mesh (like the robot chassis) without tinting everything.")]
        [SerializeField] private Renderer _modelRenderer;


        [Tooltip("Role: The audio controller for the vacuum sound.\nUse Case: Audio customization.\nJustification: Needs a direct reference to inject the chosen musical root note into the synthesis engine.")]
        [SerializeField] private VacuumAudioController _vacuumAudio;

        [Header("Preview Settings")]
        [Tooltip("Role: Disables networking hooks for preview mannequins.\nUse Case: Main Menu.\nJustification: Check this ONLY on your Lobby Dummy prefab so it stays completely offline and local, allowing players to preview colors without throwing network errors.")]
        public bool IsLobbyDummy = false;

        [Header("Debug")]
        [Tooltip("Role: Enable verbose logging.\nUse Case: Debugging customization.\nJustification: Used to trace whether colors are failing to load from PlayerPrefs or failing to sync over the network.")]
        public bool EnableDebugLogs = true;

        // SyncVars automatically sync from the server to all clients. 
        // When they change, they trigger the hook methods.
        [SyncVar(hook = nameof(OnColorChanged))]
        public Color PlayerColor = Color.white;

        [SyncVar(hook = nameof(OnNoteChanged))]
        public MusicalNote PlayerRootNote = MusicalNote.C;

    private Material _instancedMaterial;

        /// <summary>
        /// Description: Dynamic setter/getter for model renderer, used by PlayerBoneBridge at startup.
        /// </summary>
        public Renderer ModelRenderer
        {
            get => _modelRenderer;
            set
            {
                _modelRenderer = value;
                if (_modelRenderer != null)
                {
                    if (_instancedMaterial != null)
                    {
                        Destroy(_instancedMaterial);
                    }
                    _instancedMaterial = _modelRenderer.material;
                    // Apply currently synced color immediately if we are initializing late
                    ApplyColor(PlayerColor);
                }
            }
        }

        /// <summary>
        /// Description: Awake callback. Clones the material.
        /// Context: Lifecycle event.
        /// Justification: We must create an instanced material so changing one player's color doesn't accidentally tint every player in the match who shares the same base material.
        /// </summary>
        private void Awake()
        {
            // Create an instanced material so changing color doesn't affect all players
            if (_modelRenderer != null && _instancedMaterial == null)
            {
                _instancedMaterial = _modelRenderer.material;
            }
        }

        /// <summary>
        /// Description: Applies synced data on client start.
        /// Context: Mirror NetworkBehaviour callback.
        /// Justification: Ensures that when a new client connects, they immediately apply the current synced values to already-spawned players before the first frame renders.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsLobbyDummy) return;

            // Apply the current synced values to this client immediately upon joining
            ApplyColor(PlayerColor);
            ApplyNote(PlayerRootNote);
        }

        /// <summary>
        /// Description: Triggers loading of saved customization data.
        /// Context: Mirror NetworkBehaviour callback for the local player.
        /// Justification: As soon as our local player object spawns (in Lobby OR Game), it becomes the authoritative source to load from PlayerPrefs and push to the server.
        /// </summary>
        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            if (IsLobbyDummy) return;

            // As soon as our local player object spawns (Lobby OR Game), load from PlayerPrefs
            LoadSavedCustomization();
        }

        /// <summary>
        /// Description: Reads local PlayerPrefs and commands the server to adopt them.
        /// Context: Called by OnStartLocalPlayer.
        /// Justification: Customization is persistent between sessions. The client must tell the server what they look/sound like.
        /// </summary>
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

        /// <summary>
        /// Description: Cleans up instanced materials.
        /// Context: Lifecycle event.
        /// Justification: Prevents Unity memory leaks by explicitly destroying dynamically created material instances when the player object is destroyed.
        /// </summary>
        private void OnDestroy()
        {
            if (_instancedMaterial != null)
            {
                Destroy(_instancedMaterial);
            }
        }

        #region Hooks (Executed on all clients)

        /// <summary>
        /// Description: SyncVar Hook for color changes.
        /// Context: Triggered on all clients when the server updates PlayerColor.
        /// Justification: Automatically applies visual updates to remote avatars when they change their settings.
        /// </summary>
        private void OnColorChanged(Color oldColor, Color newColor)
        {
            if (IsLobbyDummy) return;
            if (EnableDebugLogs) Debug.Log($"[PlayerCustomization] SyncVar Hook OnColorChanged triggered. New Color: {newColor}");
            ApplyColor(newColor);
        }

        /// <summary>
        /// Description: SyncVar Hook for musical note changes.
        /// Context: Triggered on all clients when the server updates PlayerRootNote.
        /// Justification: Automatically applies audio updates to remote avatars when they change their settings.
        /// </summary>
        private void OnNoteChanged(MusicalNote oldNote, MusicalNote newNote)
        {
            if (IsLobbyDummy) return;
            if (EnableDebugLogs) Debug.Log($"[PlayerCustomization] SyncVar Hook OnNoteChanged triggered. New Note: {newNote}");
            ApplyNote(newNote);
        }

        /// <summary>
        /// Description: Pushes a color into the material shader.
        /// Context: Internal execution.
        /// Justification: Supports both Standard (_Color) and URP (_BaseColor) shader property naming conventions.
        /// </summary>
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

        /// <summary>
        /// Description: Updates the root note in the audio controller.
        /// Context: Internal execution.
        /// Justification: Passes the enum value to the audio synthesis system.
        /// </summary>
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

        /// <summary>
        /// Description: Universal entry point to change player color.
        /// Context: Called by the UI color picker.
        /// Justification: Safely handles both networked multiplayer avatars (sends a Command) and offline lobby dummies (applies locally without network errors).
        /// </summary>
        public void RequestColorChange(Color newColor)
        {
            if (EnableDebugLogs) Debug.Log($"[PlayerCustomization] RequestColorChange called. NetworkActive={NetworkClient.active}, isOwned={isOwned}, IsLobbyDummy={IsLobbyDummy}");
            if (!IsLobbyDummy && isOwned)
            {
                if (EnableDebugLogs) Debug.Log("[PlayerCustomization] Sending CmdChangeColor to server.");
                CmdChangeColor(newColor);
            }
            else
            {
                if (EnableDebugLogs) Debug.Log("[PlayerCustomization] Applying color locally (Preview Mode or No Authority).");
                PlayerColor = newColor;
                ApplyColor(newColor);
            }
        }

        /// <summary>
        /// Description: Universal entry point to change the musical note.
        /// Context: Called by the UI note picker.
        /// Justification: Safely handles both networked multiplayer avatars (sends a Command) and offline lobby dummies (applies locally without network errors).
        /// </summary>
        public void RequestNoteChange(MusicalNote newNote)
        {
            if (EnableDebugLogs) Debug.Log($"[PlayerCustomization] RequestNoteChange called. NetworkActive={NetworkClient.active}, isOwned={isOwned}, IsLobbyDummy={IsLobbyDummy}");
            if (!IsLobbyDummy && isOwned)
            {
                if (EnableDebugLogs) Debug.Log("[PlayerCustomization] Sending CmdChangeNote to server.");
                CmdChangeNote(newNote);
            }
            else
            {
                if (EnableDebugLogs) Debug.Log("[PlayerCustomization] Applying note locally (Preview Mode or No Authority).");
                PlayerRootNote = newNote;
                ApplyNote(newNote);
            }
        }

        /// <summary>
        /// Description: Universal entry point to test the vacuum sound.
        /// Context: Called by UI testing buttons.
        /// Justification: Safely routes the test command either through the network (for other players to hear) or locally for offline dummies.
        /// </summary>
        public void RequestVacuumTest(bool isVacuuming)
        {
            if (EnableDebugLogs) Debug.Log($"[PlayerCustomization] RequestVacuumTest called ({isVacuuming}). NetworkActive={NetworkClient.active}, isOwned={isOwned}, IsLobbyDummy={IsLobbyDummy}");
            if (!IsLobbyDummy && isOwned)
            {
                if (EnableDebugLogs) Debug.Log("[PlayerCustomization] Sending CmdTestVacuum to server.");
                CmdTestVacuum(isVacuuming);
            }
            else
            {
                if (EnableDebugLogs) Debug.Log("[PlayerCustomization] Applying vacuum test locally (Preview Mode or No Authority).");
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
        /// Description: Called by the local client's UI to request a color change.
        /// Context: Mirror Command.
        /// Justification: The server must own the SyncVar. Updating it here pushes it to all clients automatically.
        /// </summary>
        [Command]
        private void CmdChangeColor(Color newColor)
        {
            if (EnableDebugLogs) Debug.Log($"[PlayerCustomization] Server executing CmdChangeColor: {newColor}");
            PlayerColor = newColor; // This updates the SyncVar on the server, pushing it to all clients
        }

        /// <summary>
        /// Description: Called by the local client's UI to request a note change.
        /// Context: Mirror Command.
        /// Justification: The server must own the SyncVar. Updating it here pushes it to all clients automatically.
        /// </summary>
        [Command]
        private void CmdChangeNote(MusicalNote newNote)
        {
            if (EnableDebugLogs) Debug.Log($"[PlayerCustomization] Server executing CmdChangeNote: {newNote}");
            PlayerRootNote = newNote; // This updates the SyncVar on the server, pushing it to all clients
        }

        /// <summary>
        /// Description: A small debug command to test the vacuum sound from the lobby.
        /// Context: Mirror Command.
        /// Justification: Routes a temporary audio test to all clients via an RPC rather than using a SyncVar, since it's a momentary action.
        /// </summary>
        [Command]
        private void CmdTestVacuum(bool isVacuuming)
        {
            if (EnableDebugLogs) Debug.Log($"[PlayerCustomization] Server executing CmdTestVacuum: {isVacuuming}");
            RpcTestVacuum(isVacuuming);
        }

        /// <summary>
        /// Description: Executed on all clients to hear the preview.
        /// Context: Mirror ClientRpc.
        /// Justification: Forces every connected client to momentarily play the vacuum sound so the customizing player knows others can hear their new note.
        /// </summary>
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
