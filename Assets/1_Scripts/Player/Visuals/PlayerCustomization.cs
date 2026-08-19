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


        [Tooltip("Role: The audio controller for the vacuum sound.\nUse Case: Audio customization.\nJustification: Needs a direct reference to inject the chosen musical root note into the synthesis engine. (Optional on V2 Dummy)")]
        [SerializeField] private VacuumAudioController _vacuumAudio;

        [Header("Material Indices")]
        [Tooltip("Role: The submesh material index for the robot body.\nUse Case: Visual customization.\nJustification: Used to apply skins to the main chassis.")]
        [SerializeField] private int _bodyMaterialIndex = 0;

        [Tooltip("Role: The submesh material index for the robot's eyes (Iris).\nUse Case: Visual customization.\nJustification: Used to apply custom drawn pupil textures.")]
        [SerializeField] private int _eyeMaterialIndex = 2;

        [Header("Preview Settings")]
        [Tooltip("Role: Disables networking hooks for preview mannequins.\nUse Case: Main Menu.\nJustification: Check this ONLY on your Lobby Dummy prefab so it stays completely offline and local, allowing players to preview colors without throwing network errors.")]
        public bool IsLobbyDummy = false;

        [Header("Debug")]
        [Tooltip("Role: Enable verbose logging.\nUse Case: Debugging customization.\nJustification: Used to trace whether colors are failing to load from PlayerPrefs or failing to sync over the network.")]
        public bool EnableDebugLogs = true;

        [System.Serializable]
        public struct PlayerVisualPreset
        {
            public string PresetName;
            public Color BaseColor;
            public Texture BaseMap;
        }

        [Header("Visual Presets")]
        [Tooltip("Role: Predefined visual styles.\nUse Case: Selection from UI.\nJustification: Allows setting both color and texture dynamically.")]
        public PlayerVisualPreset[] VisualPresets;

        // SyncVars automatically sync from the server to all clients. 
        // When they change, they trigger the hook methods.
        [SyncVar(hook = nameof(OnVisualIndexChanged))]
        public int PlayerVisualIndex = 0;

        [SyncVar(hook = nameof(OnNoteChanged))]
        public MusicalNote PlayerRootNote = MusicalNote.C;

        private Material[] _instancedMaterials;

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
                    if (_instancedMaterials != null)
                    {
                        foreach (var mat in _instancedMaterials) 
                            if (mat != null) Destroy(mat);
                    }
                    _instancedMaterials = _modelRenderer.materials;
                    // Apply currently synced visuals immediately if we are initializing late
                    ApplyVisuals(PlayerVisualIndex);
                }
            }
        }

        /// <summary>
        /// Description: Awake callback. Clones the materials.
        /// Context: Lifecycle event.
        /// Justification: We must create instanced materials so changing one player's color or eyes doesn't accidentally tint every player in the match who shares the same base materials.
        /// </summary>
        private void Awake()
        {
            // Create instanced materials by accessing .materials property
            if (_modelRenderer != null && _instancedMaterials == null)
            {
                _instancedMaterials = _modelRenderer.materials;
            }
        }

        /// <summary>
        /// Description: Start callback. Used by the offline dummy to load saved data.
        /// Context: Lifecycle event.
        /// Justification: The Lobby Dummy doesn't run OnStartLocalPlayer, so it must load its saved look locally on startup.
        /// </summary>
        private void Start()
        {
            if (IsLobbyDummy)
            {
                if (EnableDebugLogs) Debug.Log("[PlayerCustomization] Lobby Dummy loading saved customization on Start.");
                
                if (PlayerPrefs.HasKey("PlayerVisualIndex"))
                {
                    PlayerVisualIndex = PlayerPrefs.GetInt("PlayerVisualIndex");
                    ApplyVisuals(PlayerVisualIndex);
                }
                else if (PlayerPrefs.HasKey("PlayerColorHex"))
                {
                    PlayerVisualIndex = 0; // Fallback
                    ApplyVisuals(0);
                }

                if (PlayerPrefs.HasKey("PlayerNoteIndex"))
                {
                    PlayerRootNote = (MusicalNote)PlayerPrefs.GetInt("PlayerNoteIndex");
                    ApplyNote(PlayerRootNote);
                }
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
            ApplyVisuals(PlayerVisualIndex);
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
            if (PlayerPrefs.HasKey("PlayerVisualIndex"))
            {
                int savedIndex = PlayerPrefs.GetInt("PlayerVisualIndex");
                if (EnableDebugLogs) Debug.Log($"[PlayerCustomization] Found saved visual index: {savedIndex}");
                CmdChangeVisual(savedIndex); // If offline, this will throw a warning, which is why we might need to use RequestVisualChange instead
            }
            else if (PlayerPrefs.HasKey("PlayerColorHex")) // Legacy migration fallback
            {
                string hex = PlayerPrefs.GetString("PlayerColorHex");
                if (ColorUtility.TryParseHtmlString(hex, out Color savedColor))
                {
                    // Fallback to visual index 0 as a default if an old color exists
                    CmdChangeVisual(0); 
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
            if (_instancedMaterials != null)
            {
                foreach (var mat in _instancedMaterials)
                {
                    if (mat != null) Destroy(mat);
                }
            }
        }

        #region Hooks (Executed on all clients)

        /// <summary>
        /// Description: SyncVar Hook for visual changes.
        /// Context: Triggered on all clients when the server updates PlayerVisualIndex.
        /// Justification: Automatically applies visual updates to remote avatars when they change their settings.
        /// </summary>
        private void OnVisualIndexChanged(int oldIndex, int newIndex)
        {
            if (IsLobbyDummy) return;
            if (EnableDebugLogs) Debug.Log($"[PlayerCustomization] SyncVar Hook OnVisualIndexChanged triggered. New Index: {newIndex}");
            ApplyVisuals(newIndex);
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
        /// Description: Pushes a color and texture into the material shader based on preset index.
        /// Context: Internal execution.
        /// Justification: Supports both Standard (_Color, _MainTex) and URP (_BaseColor, _BaseMap) shader property naming conventions.
        /// </summary>
        private void ApplyVisuals(int index)
        {
            if (VisualPresets == null || index < 0 || index >= VisualPresets.Length)
            {
                if (EnableDebugLogs) Debug.LogWarning($"[PlayerCustomization] ApplyVisuals failed. Invalid index {index} or no presets defined.");
                return;
            }

            PlayerVisualPreset preset = VisualPresets[index];

            if (EnableDebugLogs) Debug.Log($"[PlayerCustomization] ApplyVisuals called with index: {index} ({preset.PresetName})");
            if (_instancedMaterials != null && _bodyMaterialIndex >= 0 && _bodyMaterialIndex < _instancedMaterials.Length)
            {
                if (EnableDebugLogs) Debug.Log("[PlayerCustomization] Material found, applying texture to body.");
                
                // Textures ONLY (the BaseColor is just for the UI button)
                if (preset.BaseMap != null)
                {
                    _instancedMaterials[_bodyMaterialIndex].SetTexture("_MainTex", preset.BaseMap);
                    _instancedMaterials[_bodyMaterialIndex].SetTexture("_BaseMap", preset.BaseMap); // Safe fallback for URP
                }
            }
            else if (EnableDebugLogs) Debug.LogWarning("[PlayerCustomization] ApplyVisuals failed because _instancedMaterials is null or index out of bounds! Did you assign _modelRenderer?");
        }

        /// <summary>
        /// Description: Applies a texture specifically to the eye material.
        /// Context: Local execution (for now).
        /// Justification: Used by the custom eye painter system to override the Iris material.
        /// </summary>
        public void ApplyLocalEyeTexture(Texture2D eyeTexture)
        {
            if (EnableDebugLogs) Debug.Log("[PlayerCustomization] ApplyLocalEyeTexture called.");
            if (_instancedMaterials != null && _eyeMaterialIndex >= 0 && _eyeMaterialIndex < _instancedMaterials.Length)
            {
                if (eyeTexture != null)
                {
                    _instancedMaterials[_eyeMaterialIndex].SetTexture("_MainTex", eyeTexture);
                    _instancedMaterials[_eyeMaterialIndex].SetTexture("_BaseMap", eyeTexture); // Safe fallback for URP
                }
            }
            else if (EnableDebugLogs) Debug.LogWarning("[PlayerCustomization] ApplyLocalEyeTexture failed. Invalid material array or index.");
        }

        private void ApplyNote(MusicalNote note)
        {
            if (EnableDebugLogs) Debug.Log($"[PlayerCustomization] ApplyNote called with note: {note}");
            if (_vacuumAudio != null)
            {
                if (EnableDebugLogs) Debug.Log("[PlayerCustomization] VacuumAudioController found, applying note.");
                _vacuumAudio.SetRootNote(note);
            }
            // Silent fallback for V2 where vacuum audio might not be fully migrated yet
        }

        #endregion

        #region Public Requests (Safe for both Networked and Offline Dummies)

        /// <summary>
        /// Description: Universal entry point to change player visual preset.
        /// Context: Called by the UI preset picker.
        /// Justification: Safely handles both networked multiplayer avatars (sends a Command) and offline lobby dummies (applies locally without network errors).
        /// </summary>
        public void RequestVisualChange(int presetIndex)
        {
            if (EnableDebugLogs) Debug.Log($"[PlayerCustomization] RequestVisualChange called. NetworkActive={NetworkClient.active}, isOwned={isOwned}, IsLobbyDummy={IsLobbyDummy}");
            if (!IsLobbyDummy && isOwned)
            {
                if (EnableDebugLogs) Debug.Log("[PlayerCustomization] Sending CmdChangeVisual to server.");
                CmdChangeVisual(presetIndex);
            }
            else
            {
                if (EnableDebugLogs) Debug.Log("[PlayerCustomization] Applying visual locally (Preview Mode or No Authority).");
                PlayerVisualIndex = presetIndex;
                ApplyVisuals(presetIndex);
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
            }
        }

        #endregion

        #region Commands (Executed on the Server, requested by the Local Client)

        /// <summary>
        /// Description: Called by the local client's UI to request a visual preset change.
        /// Context: Mirror Command.
        /// Justification: The server must own the SyncVar. Updating it here pushes it to all clients automatically.
        /// </summary>
        [Command]
        private void CmdChangeVisual(int newIndex)
        {
            if (EnableDebugLogs) Debug.Log($"[PlayerCustomization] Server executing CmdChangeVisual: {newIndex}");
            PlayerVisualIndex = newIndex; // This updates the SyncVar on the server, pushing it to all clients
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
        }

        #endregion
    }
}
