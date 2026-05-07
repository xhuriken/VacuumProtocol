using Mirror;
using UnityEngine;

/// <summary>
    /// Handles the Vacuum Aspiration logic, detecting dual-arm input and synchronizing the state over the network.
    /// </summary>
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerVacuumController : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private VacuumAudioController _audioController;
        
        [SyncVar(hook = nameof(OnVacuumStateChanged))]
        private bool _isVacuuming;

        /// <summary>
        /// Public access to the vacuuming state (synced over network).
        /// </summary>
        public bool IsVacuuming => _isVacuuming;

        private PlayerInputHandler _input;

        private void Awake()
        {
            _input = GetComponent<PlayerInputHandler>();
        }

        private void Update()
        {
            // Only the local player can trigger the vacuum
            if (!isLocalPlayer) return;

            bool currentInput = _input.IsVacuuming;
            
            // If the state changed, notify the server
            if (currentInput != _isVacuuming)
            {
                CmdSetVacuumState(currentInput);
            }
        }

        [Command]
        private void CmdSetVacuumState(bool state)
        {
            _isVacuuming = state;
        }

        private void OnVacuumStateChanged(bool oldState, bool newState)
        {
            if (_audioController != null)
            {
                _audioController.SetVacuumState(newState);
            }
        }

        /// <summary>
        /// Ensure the audio state is correct when a player joins or the object is spawned.
        /// </summary>
        public override void OnStartClient()
        {
            if (_audioController != null)
            {
                _audioController.SetVacuumState(_isVacuuming);
            }
        }
    }
