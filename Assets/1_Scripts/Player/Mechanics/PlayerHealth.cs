using Mirror;
using Sirenix.OdinInspector;
using UnityEngine;

// Disambiguate attributes shared between Mirror and Sirenix.OdinInspector
using ShowInInspector = Sirenix.OdinInspector.ShowInInspectorAttribute;
using ReadOnly = Sirenix.OdinInspector.ReadOnlyAttribute;

namespace Player.Mechanics
{
    /// <summary>
    /// Description: Network-synchronized player health management component.
    /// Context: Attached to Player_V2 root GameObject. Runs server-authoritative in Mirror multiplayer.
    /// Justification: Handles damage reception (e.g. proximity terror drain, phantom attacks), healing,
    /// and death triggers, dispatching client events for UI healthbars and audio/visual feedback.
    /// </summary>
    [SelectionBase]
    public class PlayerHealth : NetworkBehaviour
    {
        #region Inspector Settings
        [Title("Health Configuration")]
        [Tooltip("Role: Maximum health capacity of this player.")]
        [SerializeField] private float _maxHealth = 100f;

        [Title("Runtime State (Network Synchronized)")]
        [SyncVar(hook = nameof(OnCurrentHealthChanged))]
        [ShowInInspector, ReadOnly]
        private float _currentHealth = 100f;

        [SyncVar(hook = nameof(OnDeathStateChanged))]
        [ShowInInspector, ReadOnly]
        private bool _isDead = false;

        [Title("Debug Settings")]
        [SerializeField] private bool _enableDebugLogs = true;
        #endregion

        #region Public Properties
        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _maxHealth;
        public float HealthNormalized => _maxHealth > 0f ? Mathf.Clamp01(_currentHealth / _maxHealth) : 0f;
        public bool IsDead => _isDead;
        #endregion

        #region Events
        /// <summary>
        /// Fired whenever current health changes (current, max).
        /// </summary>
        public event System.Action<float, float> OnHealthChanged;

        /// <summary>
        /// Fired when this player reaches 0 health.
        /// </summary>
        public event System.Action<PlayerV2_Controller> OnPlayerDied;

        /// <summary>
        /// Fired when this player is revived.
        /// </summary>
        public event System.Action<PlayerV2_Controller> OnPlayerRevived;
        #endregion

        #region Unity & Network Lifecycle
        public override void OnStartServer()
        {
            base.OnStartServer();
            _currentHealth = _maxHealth;
            _isDead = false;
        }

        private void Reset()
        {
            _maxHealth = 100f;
            _currentHealth = 100f;
        }
        #endregion

        #region Server Health Modification
        /// <summary>
        /// Description: Authoritative method to inflict damage on this player.
        /// </summary>
        [Server]
        public void TakeDamage(float amount, string damageSource = "Unknown")
        {
            if (_isDead || amount <= 0f) return;

            _currentHealth = Mathf.Max(0f, _currentHealth - amount);

            if (_currentHealth <= 0f && !_isDead)
            {
                Die(damageSource);
            }
        }

        /// <summary>
        /// Description: Authoritative method to heal the player.
        /// </summary>
        [Server]
        public void Heal(float amount)
        {
            if (_isDead || amount <= 0f) return;

            _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
            if (_enableDebugLogs)
            {
                Debug.Log($"<color=green>[PlayerHealth]</color> Player '{gameObject.name}' healed {amount:F1} PV. Current: {_currentHealth:F1}/{_maxHealth:F1}");
            }
        }

        /// <summary>
        /// Description: Authoritative method to revive a dead player.
        /// </summary>
        [Server]
        public void Revive(float healthPercent = 1.0f)
        {
            if (!_isDead) return;

            // Health first so listeners of OnPlayerRevived already read the restored value.
            _currentHealth = Mathf.Clamp(_maxHealth * healthPercent, 1f, _maxHealth);
            SetDeadState(false, "Revive");
        }

        [Server]
        private void Die(string killerSource)
        {
            _currentHealth = 0f;
            SetDeadState(true, killerSource);
        }

        /// <summary>
        /// Description: Single server entry point for death/revive state changes.
        /// Justification: The SyncVar hook raises OnPlayerDied / OnPlayerRevived on every client (host included).
        /// A dedicated server never runs hooks, so it raises the event itself; this keeps exactly one event per peer.
        /// </summary>
        [Server]
        private void SetDeadState(bool dead, string reason)
        {
            _isDead = dead;
            if (!isClient)
            {
                RaiseDeathStateEvent(dead);
            }

            if (dead)
            {
                Debug.LogWarning($"<color=red>[PlayerHealth]</color> Player '{gameObject.name}' DIED! Killed by: {reason}");
            }
            else
            {
                Debug.Log($"<color=green>[PlayerHealth]</color> Player '{gameObject.name}' was revived with {_currentHealth:F1} PV!");
            }
        }
        #endregion

        #region Client Synchronization Hooks & RPCs
        private void OnCurrentHealthChanged(float oldVal, float newVal)
        {
            OnHealthChanged?.Invoke(newVal, _maxHealth);
        }

        private void OnDeathStateChanged(bool oldDead, bool newDead)
        {
            if (oldDead == newDead) return;
            RaiseDeathStateEvent(newDead);
        }

        private void RaiseDeathStateEvent(bool dead)
        {
            PlayerV2_Controller controller = GetComponent<PlayerV2_Controller>();
            if (controller == null)
            {
                Debug.LogError($"[PlayerHealth] '{gameObject.name}' has no PlayerV2_Controller: death/revive listeners receive null.", this);
            }

            if (dead) OnPlayerDied?.Invoke(controller);
            else OnPlayerRevived?.Invoke(controller);
        }
        #endregion

        #region Odin Inspector Test Actions
        [Title("Play Mode Debugging Tools")]
        [Button("Damage 20 PV", ButtonSizes.Medium)]
        [GUIColor(1f, 0.5f, 0.5f)]
        public void DebugDamage20()
        {
            if (isServer)
            {
                TakeDamage(20f, "Debug Tool");
            }
            else
            {
                Debug.LogWarning("[PlayerHealth] Debug damage can only be executed on the Server/Host.");
            }
        }

        [Button("Heal 20 PV", ButtonSizes.Medium)]
        [GUIColor(0.5f, 1f, 0.5f)]
        public void DebugHeal20()
        {
            if (isServer)
            {
                Heal(20f);
            }
            else
            {
                Debug.LogWarning("[PlayerHealth] Debug heal can only be executed on the Server/Host.");
            }
        }

        [Button("Revive (Full Health)", ButtonSizes.Medium)]
        [GUIColor(0.4f, 0.9f, 1f)]
        public void DebugRevive()
        {
            if (!isServer)
            {
                Debug.LogWarning("[PlayerHealth] Debug revive can only be executed on the Server/Host.");
                return;
            }
            if (!_isDead)
            {
                Debug.LogWarning($"[PlayerHealth] Debug revive ignored: '{gameObject.name}' is not dead.");
                return;
            }
            Revive(1f);
        }

        [Button("Kill Immediately", ButtonSizes.Small)]
        [GUIColor(1f, 0.2f, 0.2f)]
        public void DebugKill()
        {
            if (isServer)
            {
                TakeDamage(_maxHealth + 10f, "Debug Command");
            }
            else
            {
                Debug.LogWarning("[PlayerHealth] Debug kill can only be executed on the Server/Host.");
            }
        }
        #endregion
    }
}
