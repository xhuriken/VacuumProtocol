using System;
using Mirror;
using UnityEngine;

/// <summary>
/// Description: Manages the networked health system for the player character, handling damage, health drain from phantoms, healing, and death states.
/// Context: Attached to the player prefab root (PlayerV2).
/// Justification: Centralizes authoritative health logic on the server while synchronizing state and visual/audio events to all clients.
/// </summary>
public class PlayerHealth : NetworkBehaviour
{
    [Header("Health Settings")]
    [Tooltip("Role: Maximum health value of the player.\nUse Case: Health capacity.\nJustification: Upper bound for health normalization and healing limits.")]
    [SerializeField]
    private float _maxHealth = 100f;

    [Tooltip("Role: Toggles invulnerability for testing or specific gameplay states.\nUse Case: God mode / debug.\nJustification: Prevents health reduction when active.")]
    [SerializeField]
    private bool _isInvulnerable = false;

    [Header("Debug")]
    [Tooltip("Role: Enables detailed health change logs in the console.\nUse Case: Debugging.")]
    [SerializeField]
    private bool _enableDebugLogs = false;

    [Tooltip("Role: Current real-time health value of the player (synced across network).\nUse Case: Inspector observation / runtime health tracking.")]
    [SyncVar(hook = nameof(OnHealthChangedHook))]
    [SerializeField]
    private float _currentHealth;

    [SyncVar(hook = nameof(OnDeadStateChangedHook))]
    private bool _isDead = false;

    /// <summary>
    /// Description: Triggered when health changes. Parameters: (currentHealth, maxHealth).
    /// </summary>
    public event Action<float, float> OnHealthChanged;

    /// <summary>
    /// Description: Triggered when the player's health drops to 0.
    /// </summary>
    public event Action OnDeath;

    /// <summary>
    /// Description: Triggered when the player is revived.
    /// </summary>
    public event Action OnRevived;

    /// <summary>
    /// Description: Gets the maximum health of the player.
    /// </summary>
    public float MaxHealth => _maxHealth;

    /// <summary>
    /// Description: Gets the current health of the player.
    /// </summary>
    public float CurrentHealth => _currentHealth;

    /// <summary>
    /// Description: Gets whether the player is currently dead.
    /// </summary>
    public bool IsDead => _isDead;

    /// <summary>
    /// Description: Gets the normalized health percentage (0.0 to 1.0).
    /// </summary>
    public float HealthPercent => _maxHealth > 0f ? Mathf.Clamp01(_currentHealth / _maxHealth) : 0f;

    /// <summary>
    /// Description: Gets or sets whether the player is invulnerable.
    /// </summary>
    public bool IsInvulnerable
    {
        get => _isInvulnerable;
        set => _isInvulnerable = value;
    }

    /// <summary>
    /// Description: Awake lifecycle callback. Validates configuration.
    /// </summary>
    private void Awake()
    {
        if (_maxHealth <= 0f)
        {
            Debug.LogError($"[PlayerHealth] Invalid MaxHealth ({_maxHealth}) configured on {gameObject.name}. Resetting to 100.");
            _maxHealth = 100f;
        }

        _currentHealth = _maxHealth;
    }

    /// <summary>
    /// Description: Server-side initialization hook.
    /// </summary>
    public override void OnStartServer()
    {
        base.OnStartServer();
        _currentHealth = _maxHealth;
        _isDead = false;
    }

    /// <summary>
    /// Description: Authoritative method to apply instant damage to the player.
    /// Context: Called on the server (e.g. by hazards or direct attacks).
    /// </summary>
    /// <param name="amount">Amount of damage to inflict.</param>
    [Server]
    public void TakeDamage(float amount)
    {
        if (_isDead || _isInvulnerable || amount <= 0f)
        {
            return;
        }

        float oldHealth = _currentHealth;
        _currentHealth = Mathf.Max(0f, _currentHealth - amount);

        if (_enableDebugLogs)
        {
            Debug.Log($"[PlayerHealth] {gameObject.name} took {amount:F1} damage. Health: {oldHealth:F1} -> {_currentHealth:F1}");
        }

        if (_currentHealth <= 0f && !_isDead)
        {
            Die();
        }
    }

    /// <summary>
    /// Description: Authoritative method to continuously drain health over time.
    /// Context: Called by Phantoms during proximity attack or aura drain.
    /// </summary>
    /// <param name="drainRate">Health drained per second.</param>
    /// <param name="deltaTime">Time elapsed during this tick.</param>
    [Server]
    public void DrainHealth(float drainRate, float deltaTime)
    {
        if (_isDead || _isInvulnerable || drainRate <= 0f || deltaTime <= 0f)
        {
            return;
        }

        float amountToDrain = drainRate * deltaTime;
        TakeDamage(amountToDrain);
    }

    /// <summary>
    /// Description: Authoritative method to restore player health.
    /// Context: Called on the server by healing items or regeneration.
    /// </summary>
    /// <param name="amount">Amount of health to restore.</param>
    [Server]
    public void Heal(float amount)
    {
        if (_isDead || amount <= 0f)
        {
            return;
        }

        float oldHealth = _currentHealth;
        _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);

        if (_enableDebugLogs)
        {
            Debug.Log($"[PlayerHealth] {gameObject.name} healed {amount:F1}. Health: {oldHealth:F1} -> {_currentHealth:F1}");
        }
    }

    /// <summary>
    /// Description: Authoritative method to revive a dead player.
    /// </summary>
    /// <param name="healthAmount">Initial health to grant upon revival.</param>
    [Server]
    public void Revive(float healthAmount)
    {
        if (!_isDead)
        {
            return;
        }

        _isDead = false;
        _currentHealth = Mathf.Clamp(healthAmount, 1f, _maxHealth);

        if (_enableDebugLogs)
        {
            Debug.Log($"[PlayerHealth] {gameObject.name} revived with {_currentHealth:F1} health.");
        }

        RpcOnRevived();
    }

    /// <summary>
    /// Description: Internal server handler for player death.
    /// </summary>
    [Server]
    private void Die()
    {
        _isDead = true;
        _currentHealth = 0f;

        Debug.Log($"[PlayerHealth] Player {gameObject.name} died.");
        RpcOnDeath();
    }

    #region Client RPCs and Hooks

    /// <summary>
    /// Description: RPC dispatched to all clients when the player dies.
    /// </summary>
    [ClientRpc]
    private void RpcOnDeath()
    {
        OnDeath?.Invoke();
    }

    /// <summary>
    /// Description: RPC dispatched to all clients when the player is revived.
    /// </summary>
    [ClientRpc]
    private void RpcOnRevived()
    {
        OnRevived?.Invoke();
    }

    /// <summary>
    /// Description: SyncVar hook triggered when health changes across the network.
    /// </summary>
    private void OnHealthChangedHook(float oldHealth, float newHealth)
    {
        OnHealthChanged?.Invoke(newHealth, _maxHealth);
    }

    /// <summary>
    /// Description: SyncVar hook triggered when death state changes across the network.
    /// </summary>
    private void OnDeadStateChangedHook(bool oldDead, bool newDead)
    {
        if (newDead && !oldDead)
        {
            OnDeath?.Invoke();
        }
        else if (!newDead && oldDead)
        {
            OnRevived?.Invoke();
        }
    }

    #endregion
}
