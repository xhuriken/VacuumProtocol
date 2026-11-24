using Player.Mechanics;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Player.Visuals
{
    /// <summary>
    /// Description: Drives the robot's CRT health screen (Shader_PlayerHealthScreen) from PlayerHealth.
    /// Context: On the Player_V2 root, next to PlayerHealth. Visual only and runs on every client: the PlayerHealth
    /// SyncVar hook raises OnHealthChanged everywhere, so each player sees the other robots' screens.
    /// Justification: PlayerHealth stays pure server logic. This component pushes only 3 values to the shader
    /// (_Health, _Impact, _HealFlash) through a MaterialPropertyBlock scoped to the screen slot: no material
    /// instance, and other robots are unaffected. Every continuous animation lives in the shader.
    /// </summary>
    public class PlayerHealthScreen : MonoBehaviour
    {
        #region Inspector Settings
        [Title("References")]
        [Tooltip("Role: Health source (SyncVar). Required.")]
        [SerializeField, Required] private PlayerHealth _playerHealth;

        [Tooltip("Role: Renderer that carries the screen (SKM_Robot).")]
        [SerializeField, Required] private Renderer _screenRenderer;

        [Tooltip("Role: Material slot index of the screen on the renderer (M_HP = 3 on SKM_Robot).")]
        [SerializeField, MinValue(0)] private int _screenMaterialIndex = 3;

        [Title("Contamination Front")]
        [Tooltip("Role: How fast the ectoplasm front catches up with the real health. High = sharp bite, low = slow creep.")]
        [SerializeField, Range(1f, 40f)] private float _frontFollowSharpness = 14f;

        [Title("Impact Energy")]
        [Tooltip("Role: Impact energy added per fraction of health lost. A 20 % hit (0.2 x 5) saturates it; continuous drain stays low.")]
        [SerializeField, Range(0f, 20f)] private float _impactPerHealthLost = 5f;

        [Tooltip("Role: Minimum energy added on each loss so even a tiny drain tick makes the front quiver.")]
        [SerializeField, Range(0f, 0.5f)] private float _impactMinPerLoss = 0.04f;

        [Tooltip("Role: Impact energy decay speed (per second).")]
        [SerializeField, Range(0.1f, 10f)] private float _impactDecay = 2.2f;

        [Title("Heal Feedback")]
        [Tooltip("Role: Heal flash decay speed (per second).")]
        [SerializeField, Range(0.1f, 10f)] private float _healFlashDecay = 1.8f;
        #endregion

        #region Runtime State
        [Title("Runtime (Read Only)")]
        [ShowInInspector, ReadOnly] private float _targetHealth = 1f;
        [ShowInInspector, ReadOnly] private float _displayedHealth = 1f;
        [ShowInInspector, ReadOnly] private float _impact;
        [ShowInInspector, ReadOnly] private float _healFlash;

        private MaterialPropertyBlock _block;
        private bool _isValid;
        private float _pushedHealth = -1f;
        private float _pushedImpact = -1f;
        private float _pushedHealFlash = -1f;

        private static readonly int HealthId = Shader.PropertyToID("_Health");
        private static readonly int ImpactId = Shader.PropertyToID("_Impact");
        private static readonly int HealFlashId = Shader.PropertyToID("_HealFlash");
        private const float PushEpsilon = 0.0005f;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            _isValid = ValidateSetup();
            if (!_isValid)
            {
                enabled = false;
                return;
            }
            _block = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (!_isValid) return;
            _playerHealth.OnHealthChanged += HandleHealthChanged;

            // The SyncVar hook does not fire for the initial spawn state: start from the current value.
            _targetHealth = _playerHealth.HealthNormalized;
            _displayedHealth = _targetHealth;
            _impact = 0f;
            _healFlash = 0f;
            PushToShader(force: true);
        }

        private void OnDisable()
        {
            if (_playerHealth != null)
            {
                _playerHealth.OnHealthChanged -= HandleHealthChanged;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            _displayedHealth = Mathf.Lerp(_displayedHealth, _targetHealth, 1f - Mathf.Exp(-_frontFollowSharpness * dt));
            if (Mathf.Abs(_displayedHealth - _targetHealth) < 0.0005f) _displayedHealth = _targetHealth;

            _impact = Mathf.MoveTowards(_impact, 0f, _impactDecay * dt);
            _healFlash = Mathf.MoveTowards(_healFlash, 0f, _healFlashDecay * dt);

            PushToShader(force: false);
        }
        #endregion

        #region Health Events
        private void HandleHealthChanged(float current, float max)
        {
            if (max <= 0f)
            {
                Debug.LogError($"[PlayerHealthScreen] '{name}' : MaxHealth <= 0 ({max}), cannot normalize health.", this);
                return;
            }

            float newHealth = Mathf.Clamp01(current / max);
            float delta = newHealth - _targetHealth;

            if (delta < 0f)
            {
                _impact = Mathf.Clamp01(_impact + _impactMinPerLoss + (-delta) * _impactPerHealthLost);
            }
            else if (delta > 0f)
            {
                _healFlash = 1f;
            }

            _targetHealth = newHealth;
        }
        #endregion

        #region Shader Push
        private void PushToShader(bool force)
        {
            bool changed = force
                || Mathf.Abs(_displayedHealth - _pushedHealth) > PushEpsilon
                || Mathf.Abs(_impact - _pushedImpact) > PushEpsilon
                || Mathf.Abs(_healFlash - _pushedHealFlash) > PushEpsilon;
            if (!changed) return;

            _screenRenderer.GetPropertyBlock(_block, _screenMaterialIndex);
            _block.SetFloat(HealthId, _displayedHealth);
            _block.SetFloat(ImpactId, _impact);
            _block.SetFloat(HealFlashId, _healFlash);
            _screenRenderer.SetPropertyBlock(_block, _screenMaterialIndex);

            _pushedHealth = _displayedHealth;
            _pushedImpact = _impact;
            _pushedHealFlash = _healFlash;
        }

        private bool ValidateSetup()
        {
            if (_playerHealth == null)
            {
                Debug.LogError($"[PlayerHealthScreen] '{name}' : missing PlayerHealth reference.", this);
                return false;
            }
            if (_screenRenderer == null)
            {
                Debug.LogError($"[PlayerHealthScreen] '{name}' : missing Screen Renderer reference.", this);
                return false;
            }

            Material[] mats = _screenRenderer.sharedMaterials;
            if (_screenMaterialIndex >= mats.Length)
            {
                Debug.LogError($"[PlayerHealthScreen] '{name}' : slot {_screenMaterialIndex} out of range, '{_screenRenderer.name}' only has {mats.Length} materials.", this);
                return false;
            }

            Material screenMat = mats[_screenMaterialIndex];
            if (screenMat == null || !screenMat.HasFloat(HealthId) || !screenMat.HasFloat(ImpactId) || !screenMat.HasFloat(HealFlashId))
            {
                string matName = screenMat != null ? $"{screenMat.name} ({screenMat.shader.name})" : "null";
                Debug.LogError($"[PlayerHealthScreen] '{name}' : material in slot {_screenMaterialIndex} ({matName}) does not use VacuumProtocol/PlayerHealthScreen.", this);
                return false;
            }
            return true;
        }
        #endregion
    }
}
