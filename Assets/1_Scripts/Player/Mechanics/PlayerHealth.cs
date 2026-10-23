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
        /// Fired on damage feedback (amount, source).
        /// </summary>
        public event System.Action<float, string> OnDamageReceived;

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

            RpcOnDamageFeedback(amount, damageSource);

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

            _isDead = false;
            _currentHealth = Mathf.Clamp(_maxHealth * healthPercent, 1f, _maxHealth);

            PlayerV2_Controller controller = GetComponent<PlayerV2_Controller>();
            OnPlayerRevived?.Invoke(controller);
            RpcOnRevived();

            Debug.Log($"<color=green>[PlayerHealth]</color> Player '{gameObject.name}' was revived with {_currentHealth:F1} PV!");
        }

        [Server]
        private void Die(string killerSource)
        {
            _isDead = true;
            _currentHealth = 0f;

            PlayerV2_Controller controller = GetComponent<PlayerV2_Controller>();
            Debug.LogWarning($"<color=red>[PlayerHealth]</color> Player '{gameObject.name}' DIED! Killed by: {killerSource}");

            OnPlayerDied?.Invoke(controller);
            RpcOnDied();
        }
        #endregion

        #region Client Synchronization Hooks & RPCs
        private void OnCurrentHealthChanged(float oldVal, float newVal)
        {
            OnHealthChanged?.Invoke(newVal, _maxHealth);
        }

        private void OnDeathStateChanged(bool oldDead, bool newDead)
        {
            if (newDead)
            {
                PlayerV2_Controller controller = GetComponent<PlayerV2_Controller>();
                OnPlayerDied?.Invoke(controller);
            }
        }

        [ClientRpc]
        private void RpcOnDamageFeedback(float amount, string source)
        {
            OnDamageReceived?.Invoke(amount, source);
        }

        [ClientRpc]
        private void RpcOnDied()
        {
            // Local client audio/visual trigger hook
            if (isLocalPlayer)
            {
                Debug.Log("<color=red>[PlayerHealth]</color> Local player succumbed to the entities!");
            }
        }

        [ClientRpc]
        private void RpcOnRevived()
        {
            if (isLocalPlayer)
            {
                Debug.Log("<color=green>[PlayerHealth]</color> Local player revived!");
            }
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
