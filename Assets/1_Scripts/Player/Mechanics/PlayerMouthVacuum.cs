using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using Gameplay.Phantoms;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Description: Centralized upgradable parameters for mouth phantom suction, ectoplasm drain, and metaphysical lightning visuals.
/// Context: Acts as the Single Source of Truth (SSOT) for item upgrades, perks, and gameplay balancing.
/// </summary>
[System.Serializable]
public class MouthVacuumStats
{
    [Header("Suction Cone Upgradable Stats")]
    [Tooltip("Base distance along cone axis Z reached by the mouth vacuum.")]
    public float BaseConeLength = 5.5f;

    [Tooltip("Multiplier applied by items/perks to cone reach and volume.")]
    public float ConeSizeMultiplier = 1.0f;

    [Header("Ectoplasm Drain Stats")]
    [Tooltip("Base ectoplasm health drained per second from captured phantoms.")]
    public float BaseDrainRate = 10.0f;

    [Tooltip("Multiplier applied by items/perks to ectoplasm drain rate.")]
    public float DrainMultiplier = 1.0f;

    [Header("Suction Pull Acceleration")]
    [Tooltip("Multiplier applied to the physical pull velocity dragging phantoms towards the nozzle.")]
    public float SuctionPullMultiplier = 1.0f;

    [Header("Metaphysical Lightning Tethers")]
    [Tooltip("Maximum simultaneous electric tethers.")]
    public int MaxLightningTethers = 4;

    [Tooltip("Number of tessellated points per filament arc.")]
    public int TetherPoints = 24;

    [Tooltip("Base width of the hot central crackling core spine.")]
    public float BaseCoreWidth = 0.012f;

    [Tooltip("Base width of the swirling ethereal aura shroud.")]
    public float BaseAuraWidth = 0.026f;

    [Tooltip("Multiplier applied by items/perks to lightning filament width.")]
    public float LightningWidthMultiplier = 1.0f;

    [Header("Metaphysical Dynamics & Animation")]
    [Tooltip("Duration in seconds for a newly struck tether to stretch from nozzle to ghost.")]
    public float TetherBirthDuration = 0.20f;

    [Tooltip("Duration in seconds for a severed tether to dissolve and fade away.")]
    public float TetherDeathDuration = 0.32f;

    [Tooltip("Lateral micro-displacement jitter for electric arcs.")]
    public float LightningJitter = 0.12f;

    [Tooltip("Frequency of lightning arc restructuring per second.")]
    public float CrackleFrequency = 24.0f;

    [Tooltip("Speed of ethereal plasma waves traveling backward along the tether into the mouth.")]
    public float EnergyFlowSpeed = 4.5f;
}

/// <summary>
/// Description: Dedicated mouth vacuum controller for absorbing spectral entities (Phantoms) via dual-click (IsVacuuming).
/// Context: Attached to Player_V2. Activates only when both left and right clicks are held simultaneously.
/// Justification: Decouples phantom capture from arm-based item/dust collection, keeping the mechanics clean, SSOT, and optimized.
/// Uses PhantomBase.ActivePhantoms registry for O(1) query performance without continuous Physics.OverlapSphere allocations.
/// Features dual-filament metaphysical lightning tethers with progressive birth/death animations and backward energy suction flow.
/// </summary>
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerV2_Controller))]
public class PlayerMouthVacuum : NetworkBehaviour
{
    [Header("Centralized Mouth Vacuum Stats (SSOT)")]
    [SerializeField] private MouthVacuumStats _stats = new MouthVacuumStats();

    [Header("Mouth Suction Parameters (Legacy Serialization Overrides)")]
    [Tooltip("Role: Maximum distance along cone axis Z that the mouth vacuum reaches.")]
    [SerializeField] private float _coneLength = 5.5f;

    [Tooltip("Role: Maximum radius expansion profile along cone length Z matching VacuumCone1.")]
    [SerializeField] private AnimationCurve _coneRadiusProfile;

    [Tooltip("Role: Ectoplasmic health drained per second from phantoms inside the cone.")]
    [SerializeField] private float _drainRatePerSecond = 10f;

    [Tooltip("Role: Layers that block mouth line-of-sight (walls, obstacles, ground).")]
    [SerializeField] private LayerMask _obstacleLayer;

    [Header("Subliminal Lightning Tethers")]
    [Tooltip("Role: Additive electric material for the subliminal vortex lightning arcs.")]
    [SerializeField] private Material _lightningMaterial;

    [Header("Suction Visuals")]
    [Tooltip("Role: Renderer of the 3D suction cone displaying the distortion material.")]
    [SerializeField] private Renderer _suctionConeRenderer;

    [Tooltip("Role: Optional additional renderers (concentric interior shells) sharing the distortion material.")]
    [SerializeField] private Renderer[] _additionalConeRenderers;

    [Tooltip("Role: Particle system creating 3D volumetric aerodynamic streamlines and swirling dust rushing into the mouth.")]
    [SerializeField] private ParticleSystem _suctionParticles;

    [Tooltip("Role: Duration in seconds to smoothly fade in the suction visuals and vortex speed.")]
    [SerializeField] private float _fadeInDuration = 0.35f;

    [Tooltip("Role: Duration in seconds to smoothly fade out the suction visuals and vortex speed.")]
    [SerializeField] private float _fadeOutDuration = 0.50f;

    [Header("Debug")]
    [SerializeField] private bool _showDebugGizmos = true;

    [SyncVar]
    private bool _isVacuumingNetworked;

    private PlayerInputHandler _input;
    private PlayerV2_Controller _controller;

    private MaterialPropertyBlock _mpb;
    private float _initialVortexSpeed = 3f;
    private float _initialDistortionStrength = 0.025f;
    private Color _initialTint = Color.cyan;
    private float _currentTransition = 0f;

    private Vector3 _initialConeScale = Vector3.one;
    private Vector3[] _initialAdditionalConeScales;

    private static readonly int PropVortexSpeed = Shader.PropertyToID("_VortexSpeed");
    private static readonly int PropVacuumTint = Shader.PropertyToID("_VacuumTint");
    private static readonly int PropDistortionStrength = Shader.PropertyToID("_DistortionStrength");

    /// <summary>
    /// Description: Internal wrapper representing a dual-filament metaphysical lightning tether.
    /// Manages progressive strike (birth) and dissolve (death) animations with zero GC allocations per frame.
    /// </summary>
    private class MetaphysicalLightningTether
    {
        public GameObject RootObject;
        public LineRenderer CoreLine;
        public LineRenderer AuraLine;

        public PhantomBase BoundPhantom;
        public Vector3 LastKnownTargetPos;

        // ManifestProgress: 0f = fully inactive, 1f = fully struck/manifested
        public float ManifestProgress;
        public float PhaseOffset;

        public Vector3[] CoreBuffer;
        public Vector3[] AuraBuffer;

        public Gradient CoreGradient = new Gradient();
        public Gradient AuraGradient = new Gradient();

        public GradientColorKey[] CoreColorKeys;
        public GradientAlphaKey[] CoreAlphaKeys;
        public GradientColorKey[] AuraColorKeys;
        public GradientAlphaKey[] AuraAlphaKeys;
    }

    private MetaphysicalLightningTether[] _tethers;
    private readonly List<PhantomBase> _activeCapturedPhantoms = new List<PhantomBase>();

    #region Public Upgrade & Inspection API

    public MouthVacuumStats Stats => _stats;
    public float EffectiveDrainRate => _stats.BaseDrainRate * _stats.DrainMultiplier;
    public float EffectiveConeLength => _stats.BaseConeLength * _stats.ConeSizeMultiplier;
    public float EffectivePullMultiplier => _stats.SuctionPullMultiplier;

    /// <summary>
    /// Description: Multiplies cone reach and radius. Updates visual 3D cone scale instantly.
    /// Fails loudly if multiplier is negative or zero.
    /// </summary>
    public void SetConeSizeMultiplier(float multiplier)
    {
        if (multiplier <= 0.01f)
        {
            Debug.LogError($"[PlayerMouthVacuum] Invalid ConeSizeMultiplier: {multiplier}. Value must be > 0.01.", this);
            return;
        }

        _stats.ConeSizeMultiplier = multiplier;
        UpdateConeVisualScale();
    }

    /// <summary>
    /// Description: Multiplies ectoplasmic drain rate.
    /// </summary>
    public void SetDrainMultiplier(float multiplier)
    {
        if (multiplier < 0f)
        {
            Debug.LogError($"[PlayerMouthVacuum] Invalid DrainMultiplier: {multiplier}. Value must be >= 0.", this);
            return;
        }

        _stats.DrainMultiplier = multiplier;
    }

    /// <summary>
    /// Description: Multiplies suction physical pull acceleration dragging phantoms towards nozzle.
    /// </summary>
    public void SetSuctionPullMultiplier(float multiplier)
    {
        if (multiplier < 0.1f)
        {
            Debug.LogError($"[PlayerMouthVacuum] Invalid SuctionPullMultiplier: {multiplier}. Value must be >= 0.1.", this);
            return;
        }

        _stats.SuctionPullMultiplier = multiplier;
    }

    /// <summary>
    /// Description: Multiplies width of metaphysical lightning filaments.
    /// </summary>
    public void SetLightningWidthMultiplier(float multiplier)
    {
        if (multiplier < 0.1f)
        {
            Debug.LogError($"[PlayerMouthVacuum] Invalid LightningWidthMultiplier: {multiplier}. Value must be >= 0.1.", this);
            return;
        }

        _stats.LightningWidthMultiplier = multiplier;
    }

    #endregion

    private void Awake()
    {
        _input = GetComponent<PlayerInputHandler>();
        _controller = GetComponent<PlayerV2_Controller>();

        // Synchronize legacy inspector values into centralized stats
        if (_coneLength > 0f) _stats.BaseConeLength = _coneLength;
        if (_drainRatePerSecond > 0f) _stats.BaseDrainRate = _drainRatePerSecond;

        if (_coneRadiusProfile == null || _coneRadiusProfile.length == 0)
        {
            _coneRadiusProfile = new AnimationCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(1f, 1.20f),
                new Keyframe(2.5f, 2.10f),
                new Keyframe(4f, 2.75f),
                new Keyframe(5.5f, 2.50f)
            );
        }

        if (_obstacleLayer.value == 0)
        {
            _obstacleLayer = LayerMask.GetMask("Default", "Ground", "Obstacle", "Obstable");
            if (_obstacleLayer.value == 0)
            {
                _obstacleLayer = (1 << 0) | (1 << 3) | (1 << 8);
            }
        }

        InitSuctionVisuals();
        InitLightningTethers();
    }

    /// <summary>
    /// Description: Initializes the suction cone material property block, particle system, and caches starting values.
    /// Fails loudly if renderer or particle system is missing.
    /// </summary>
    private void InitSuctionVisuals()
    {
        _mpb = new MaterialPropertyBlock();

        if (_suctionConeRenderer == null)
        {
            // Auto-search in children for ease of setup
            _suctionConeRenderer = GetComponentInChildren<MeshRenderer>();
        }

        if (_suctionConeRenderer == null)
        {
            Debug.LogError($"[PlayerMouthVacuum] '_suctionConeRenderer' is missing on '{gameObject.name}'. Please assign the 3D suction cone renderer.", this);
            return;
        }

        _initialConeScale = _suctionConeRenderer.transform.localScale;

        if (_additionalConeRenderers != null && _additionalConeRenderers.Length > 0)
        {
            _initialAdditionalConeScales = new Vector3[_additionalConeRenderers.Length];
            for (int i = 0; i < _additionalConeRenderers.Length; i++)
            {
                if (_additionalConeRenderers[i] != null)
                {
                    _initialAdditionalConeScales[i] = _additionalConeRenderers[i].transform.localScale;
                }
            }
        }

        UpdateConeVisualScale();

        Material mat = _suctionConeRenderer.sharedMaterial;
        if (mat != null)
        {
            if (mat.HasProperty(PropVortexSpeed)) _initialVortexSpeed = mat.GetFloat(PropVortexSpeed);
            if (mat.HasProperty(PropDistortionStrength)) _initialDistortionStrength = mat.GetFloat(PropDistortionStrength);
            if (mat.HasProperty(PropVacuumTint))
            {
                _initialTint = mat.GetColor(PropVacuumTint);
                if (_initialTint.a <= 0.0001f)
                {
                    _initialTint.a = 0.3f;
                }
            }
        }

        if (_suctionParticles == null && _suctionConeRenderer != null)
        {
            _suctionParticles = _suctionConeRenderer.GetComponentInChildren<ParticleSystem>();
            if (_suctionParticles == null && _suctionConeRenderer.transform.parent != null)
            {
                Transform pChild = _suctionConeRenderer.transform.parent.Find("MouthSuctionParticles");
                if (pChild != null)
                {
                    _suctionParticles = pChild.GetComponent<ParticleSystem>();
                }
            }
        }

        if (_suctionParticles == null)
        {
            Debug.LogError($"[PlayerMouthVacuum] '_suctionParticles' is missing on '{gameObject.name}'. Please assign the dedicated mouth suction ParticleSystem in the inspector.", this);
        }
        else
        {
            _suctionParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // Start fully hidden and idle
        SetConeRenderersEnabled(false);
        ApplyVisualProperties(0f);
    }

    private void UpdateConeVisualScale()
    {
        if (_suctionConeRenderer != null)
        {
            _suctionConeRenderer.transform.localScale = _initialConeScale * _stats.ConeSizeMultiplier;
        }

        if (_additionalConeRenderers != null && _initialAdditionalConeScales != null)
        {
            for (int i = 0; i < _additionalConeRenderers.Length; i++)
            {
                if (_additionalConeRenderers[i] != null && i < _initialAdditionalConeScales.Length)
                {
                    _additionalConeRenderers[i].transform.localScale = _initialAdditionalConeScales[i] * _stats.ConeSizeMultiplier;
                }
            }
        }
    }

    private void SetConeRenderersEnabled(bool isEnabled)
    {
        if (_suctionConeRenderer != null)
        {
            _suctionConeRenderer.enabled = isEnabled;
        }

        if (_additionalConeRenderers != null)
        {
            for (int i = 0; i < _additionalConeRenderers.Length; i++)
            {
                if (_additionalConeRenderers[i] != null)
                {
                    _additionalConeRenderers[i].enabled = isEnabled;
                }
            }
        }
    }

    private void Update()
    {
        UpdateVisuals();

        if (!isLocalPlayer || _input == null) return;

        bool isVacuuming = _input.IsVacuuming;

        // Synchronize visual state across Mirror network
        if (isVacuuming != _isVacuumingNetworked)
        {
            CmdSetVacuumingVisual(isVacuuming);
        }

        if (isVacuuming)
        {
            ProcessMouthSuction();
        }
        else
        {
            _activeCapturedPhantoms.Clear();
            UpdateLightningTethers();
        }
    }

    /// <summary>
    /// Description: Smoothly animates vortex speed, distortion strength, tint alpha, and controls the particle system.
    /// Anti-spam protected via Mathf.MoveTowards without popping or clipping.
    /// </summary>
    private void UpdateVisuals()
    {
        if (_suctionConeRenderer == null) return;

        bool active = isLocalPlayer
            ? (_input != null && _input.IsVacuuming)
            : _isVacuumingNetworked;

        // Particle system playback synchronization
        if (_suctionParticles != null)
        {
            if (active && !_suctionParticles.isEmitting)
            {
                _suctionParticles.Play(true);
            }
            else if (!active && _suctionParticles.isEmitting)
            {
                _suctionParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        float target = active ? 1f : 0f;
        float duration = active ? _fadeInDuration : _fadeOutDuration;
        float step = Time.deltaTime / Mathf.Max(0.001f, duration);

        _currentTransition = Mathf.MoveTowards(_currentTransition, target, step);

        if (_currentTransition <= 0.0001f)
        {
            if (_suctionConeRenderer.enabled)
            {
                SetConeRenderersEnabled(false);
            }
            return;
        }

        if (!_suctionConeRenderer.enabled)
        {
            SetConeRenderersEnabled(true);
        }

        ApplyVisualProperties(_currentTransition);
    }

    /// <summary>
    /// Description: Updates shader properties via MaterialPropertyBlock for zero-allocation, per-player visual state.
    /// </summary>
    private void ApplyVisualProperties(float transition)
    {
        if (_suctionConeRenderer == null) return;

        _suctionConeRenderer.GetPropertyBlock(_mpb);

        // Smooth speed ramp (0 at rest -> initial speed when active)
        _mpb.SetFloat(PropVortexSpeed, Mathf.Lerp(0f, _initialVortexSpeed, transition));

        // Smooth distortion strength ramp (no heatwave at rest -> full heatwave when active)
        _mpb.SetFloat(PropDistortionStrength, Mathf.Lerp(0f, _initialDistortionStrength, transition));

        // Smooth tint color ramp:
        float currentAlpha = Mathf.Lerp(0f, _initialTint.a, transition);
        Color neutralTint = new Color(1f, 1f, 1f, 0f);
        Color currentTint = Color.Lerp(neutralTint, _initialTint, transition);
        currentTint.a = currentAlpha;

        _mpb.SetColor(PropVacuumTint, currentTint);

        _suctionConeRenderer.SetPropertyBlock(_mpb);

        if (_additionalConeRenderers != null)
        {
            for (int i = 0; i < _additionalConeRenderers.Length; i++)
            {
                if (_additionalConeRenderers[i] != null)
                {
                    _additionalConeRenderers[i].SetPropertyBlock(_mpb);
                }
            }
        }
    }

    [Command]
    private void CmdSetVacuumingVisual(bool active)
    {
        _isVacuumingNetworked = active;
    }

    /// <summary>
    /// Description: Initializes dual-filament metaphysical lightning tether units.
    /// Each unit contains a hot-crackling Core spine and a soft-swirling Ethereal Aura shroud.
    /// Fails loudly if electric material cannot be loaded.
    /// </summary>
    private void InitLightningTethers()
    {
        if (_lightningMaterial == null)
        {
            _lightningMaterial = Resources.Load<Material>("Mat_SuctionLightning");
#if UNITY_EDITOR
            if (_lightningMaterial == null)
            {
                _lightningMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/4_Graphics/Shader/Player/Mat_SuctionLightning.mat");
            }
#endif
        }

        if (_lightningMaterial == null)
        {
            Debug.LogError($"[PlayerMouthVacuum] '_lightningMaterial' could not be found or loaded! Assign Mat_SuctionLightning in the inspector on '{gameObject.name}'.", this);
            return;
        }

        Transform root = _suctionConeRenderer != null ? _suctionConeRenderer.transform : transform;
        int count = Mathf.Max(1, _stats.MaxLightningTethers);
        int points = Mathf.Max(4, _stats.TetherPoints);

        _tethers = new MetaphysicalLightningTether[count];

        AnimationCurve coreWidthCurve = new AnimationCurve(
            new Keyframe(0.0f, 0.45f, 0f, 1.5f),
            new Keyframe(0.5f, 1.0f, 0f, 0f),
            new Keyframe(1.0f, 0.60f, -1.2f, 0f)
        );

        AnimationCurve auraWidthCurve = new AnimationCurve(
            new Keyframe(0.0f, 0.35f, 0f, 1.8f),
            new Keyframe(0.45f, 1.0f, 0f, 0f),
            new Keyframe(1.0f, 0.50f, -1.5f, 0f)
        );

        for (int i = 0; i < count; i++)
        {
            GameObject tetherRoot = new GameObject($"MetaphysicalTether_{i}");
            tetherRoot.transform.SetParent(root, false);

            // 1. Hot crackling core spine
            GameObject coreObj = new GameObject("CoreSpine");
            coreObj.transform.SetParent(tetherRoot.transform, false);
            LineRenderer coreLr = coreObj.AddComponent<LineRenderer>();
            coreLr.positionCount = points;
            coreLr.widthCurve = coreWidthCurve;
            coreLr.widthMultiplier = _stats.BaseCoreWidth;
            coreLr.material = _lightningMaterial;
            coreLr.useWorldSpace = true;
            coreLr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            coreLr.receiveShadows = false;
            coreLr.enabled = false;

            // 2. Soft ethereal swirling aura shroud
            GameObject auraObj = new GameObject("EtherealAura");
            auraObj.transform.SetParent(tetherRoot.transform, false);
            LineRenderer auraLr = auraObj.AddComponent<LineRenderer>();
            auraLr.positionCount = points;
            auraLr.widthCurve = auraWidthCurve;
            auraLr.widthMultiplier = _stats.BaseAuraWidth;
            auraLr.material = _lightningMaterial;
            auraLr.useWorldSpace = true;
            auraLr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            auraLr.receiveShadows = false;
            auraLr.enabled = false;

            MetaphysicalLightningTether tether = new MetaphysicalLightningTether
            {
                RootObject = tetherRoot,
                CoreLine = coreLr,
                AuraLine = auraLr,
                CoreBuffer = new Vector3[points],
                AuraBuffer = new Vector3[points],
                PhaseOffset = i * (Mathf.PI * 2f / count),
                ManifestProgress = 0f,
                BoundPhantom = null,
                LastKnownTargetPos = Vector3.zero,
                CoreColorKeys = new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.4f, 0.9f, 1f), 0.0f),
                    new GradientColorKey(Color.white, 0.25f),
                    new GradientColorKey(Color.white, 0.75f),
                    new GradientColorKey(new Color(0.35f, 0.85f, 1f), 1.0f)
                },
                CoreAlphaKeys = new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 0.08f),
                    new GradientAlphaKey(1.0f, 0.92f),
                    new GradientAlphaKey(0.5f, 1.0f)
                },
                AuraColorKeys = new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.1f, 0.65f, 1f), 0.0f),
                    new GradientColorKey(new Color(0.2f, 0.85f, 1f), 0.5f),
                    new GradientColorKey(new Color(0.5f, 0.3f, 1f), 1.0f)
                },
                AuraAlphaKeys = new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.40f, 0.20f),
                    new GradientAlphaKey(0.40f, 0.80f),
                    new GradientAlphaKey(0.15f, 1.0f)
                }
            };

            tether.CoreGradient.SetKeys(tether.CoreColorKeys, tether.CoreAlphaKeys);
            tether.AuraGradient.SetKeys(tether.AuraColorKeys, tether.AuraAlphaKeys);

            coreLr.colorGradient = tether.CoreGradient;
            auraLr.colorGradient = tether.AuraGradient;

            _tethers[i] = tether;
        }
    }

    /// <summary>
    /// Description: Tests whether a world-space point is inside the suction cone mesh.
    /// Accounts for upgradable ConeSizeMultiplier.
    /// </summary>
    public bool IsInsideSuctionCone(Vector3 worldPoint, out Vector3 localConePos)
    {
        localConePos = Vector3.zero;
        if (_suctionConeRenderer == null) return false;

        Transform coneT = _suctionConeRenderer.transform;
        localConePos = coneT.InverseTransformPoint(worldPoint);

        // Distance along cone forward axis Z (from mouth nozzle Z=0 to BaseConeLength)
        // Since coneT localScale is scaled by ConeSizeMultiplier, localConePos is in base unscaled space.
        if (localConePos.z < 0f || localConePos.z > _stats.BaseConeLength)
        {
            return false;
        }

        // Radial distance from central axis in local XY plane
        float radiusAtZ = _coneRadiusProfile != null ? _coneRadiusProfile.Evaluate(localConePos.z) : 2.5f;
        float radialDistance = Mathf.Sqrt(localConePos.x * localConePos.x + localConePos.y * localConePos.y);

        return radialDistance <= radiusAtZ;
    }

    /// <summary>
    /// Description: Scans all active phantoms and applies suction and drain to those within the mouth cone.
    /// </summary>
    private void ProcessMouthSuction()
    {
        _activeCapturedPhantoms.Clear();

        if (_suctionConeRenderer == null) return;

        Transform coneT = _suctionConeRenderer.transform;
        Vector3 nozzleOrigin = coneT.position;
        Vector3 coneForward = coneT.forward;
        Vector3 coneUp = coneT.up;
        Vector3 coneRight = coneT.right;

        float effectiveDrain = _stats.BaseDrainRate * _stats.DrainMultiplier;
        float effectivePull = _stats.SuctionPullMultiplier;

        IReadOnlyList<PhantomBase> phantoms = PhantomBase.ActivePhantoms;
        if (phantoms != null && phantoms.Count > 0)
        {
            for (int i = 0; i < phantoms.Count; i++)
            {
                PhantomBase phantom = phantoms[i];
                if (phantom == null || !phantom.gameObject.activeInHierarchy) continue;

                Vector3 phantomTargetPos = phantom.LookAtPoint != null ? phantom.LookAtPoint.position : phantom.transform.position;

                // 1. Volumetric containment check inside suction cone
                if (!IsInsideSuctionCone(phantomTargetPos, out _))
                {
                    continue;
                }

                // 2. Obstacle line-of-sight check against walls/doors
                Vector3 toPhantom = phantomTargetPos - nozzleOrigin;
                float distance = toPhantom.magnitude;
                if (distance > 0.05f)
                {
                    if (Physics.Raycast(nozzleOrigin, toPhantom.normalized, out RaycastHit hit, distance, _obstacleLayer, QueryTriggerInteraction.Ignore))
                    {
                        if (!hit.transform.IsChildOf(phantom.transform))
                        {
                            // Obstructed by a wall
                            continue;
                        }
                    }
                }

                _activeCapturedPhantoms.Add(phantom);

                // Apply instant client-side prediction and authoritative server command
                phantom.OnMouthSuctionApplied(netId, nozzleOrigin, coneForward, coneUp, coneRight, effectiveDrain, effectivePull);
                CmdApplyMouthSuction(phantom.gameObject, nozzleOrigin, coneForward, coneUp, coneRight, effectiveDrain, effectivePull);
            }
        }

        UpdateLightningTethers();
    }

    [Command]
    private void CmdApplyMouthSuction(GameObject phantomObj, Vector3 nozzlePos, Vector3 coneForward, Vector3 coneUp, Vector3 coneRight, float drainRate, float pullMultiplier)
    {
        if (phantomObj == null) return;
        PhantomBase phantom = phantomObj.GetComponent<PhantomBase>();
        if (phantom != null)
        {
            phantom.OnMouthSuctionApplied(netId, nozzlePos, coneForward, coneUp, coneRight, drainRate, pullMultiplier);
        }
    }

    /// <summary>
    /// Description: Updates subliminal lightning arcs connecting the mouth nozzle to captured phantoms.
    /// Manages progressive strike (birth) and dissolve (death) animations with zero GC allocations.
    /// Features discrete stepped crackle for the core spine and continuous backward helical flow for the ethereal aura.
    /// </summary>
    private void UpdateLightningTethers()
    {
        if (_tethers == null || _tethers.Length == 0 || _suctionConeRenderer == null) return;

        Transform coneT = _suctionConeRenderer.transform;
        Vector3 nozzlePos = coneT.position;
        Vector3 coneForward = coneT.forward;
        Vector3 coneUp = coneT.up;
        Vector3 coneRight = coneT.right;

        // 1. Tether assignment & tracking
        // Mark currently active targets
        for (int i = 0; i < _tethers.Length; i++)
        {
            MetaphysicalLightningTether tether = _tethers[i];
            if (tether == null) continue;

            if (tether.BoundPhantom != null)
            {
                if (!_activeCapturedPhantoms.Contains(tether.BoundPhantom) || !tether.BoundPhantom.gameObject.activeInHierarchy)
                {
                    // Target lost or escaped: begin progressive dissolution
                    tether.BoundPhantom = null;
                }
            }
        }

        // Assign unassigned captured phantoms to available tether slots
        for (int p = 0; p < _activeCapturedPhantoms.Count; p++)
        {
            PhantomBase phantom = _activeCapturedPhantoms[p];
            if (phantom == null) continue;

            bool alreadyBound = false;
            for (int i = 0; i < _tethers.Length; i++)
            {
                if (_tethers[i] != null && _tethers[i].BoundPhantom == phantom)
                {
                    alreadyBound = true;
                    break;
                }
            }

            if (!alreadyBound)
            {
                // Find best slot: first unassigned slot, prioritizing lowest ManifestProgress
                MetaphysicalLightningTether bestSlot = null;
                float lowestProgress = float.MaxValue;
                for (int i = 0; i < _tethers.Length; i++)
                {
                    MetaphysicalLightningTether t = _tethers[i];
                    if (t != null && t.BoundPhantom == null && t.ManifestProgress < lowestProgress)
                    {
                        lowestProgress = t.ManifestProgress;
                        bestSlot = t;
                    }
                }

                if (bestSlot != null)
                {
                    bestSlot.BoundPhantom = phantom;
                    Vector3 targetPos = phantom.LookAtPoint != null ? phantom.LookAtPoint.position : phantom.transform.position;
                    bestSlot.LastKnownTargetPos = targetPos;
                }
            }
        }

        // 2. Animate and tessellate each tether
        float steppedTime = Mathf.Floor(Time.time * _stats.CrackleFrequency);
        float timeFlow = Time.time * _stats.EnergyFlowSpeed;

        for (int i = 0; i < _tethers.Length; i++)
        {
            MetaphysicalLightningTether tether = _tethers[i];
            if (tether == null) continue;

            // Progressive Birth or Dissolve animation step
            if (tether.BoundPhantom != null)
            {
                float birthStep = Time.deltaTime / Mathf.Max(0.01f, _stats.TetherBirthDuration);
                tether.ManifestProgress = Mathf.MoveTowards(tether.ManifestProgress, 1f, birthStep);

                Vector3 targetPos = tether.BoundPhantom.LookAtPoint != null
                    ? tether.BoundPhantom.LookAtPoint.position
                    : tether.BoundPhantom.transform.position;
                tether.LastKnownTargetPos = targetPos;
            }
            else
            {
                float deathStep = Time.deltaTime / Mathf.Max(0.01f, _stats.TetherDeathDuration);
                tether.ManifestProgress = Mathf.MoveTowards(tether.ManifestProgress, 0f, deathStep);
            }

            if (tether.ManifestProgress <= 0.0001f)
            {
                if (tether.CoreLine.enabled) tether.CoreLine.enabled = false;
                if (tether.AuraLine.enabled) tether.AuraLine.enabled = false;
                continue;
            }

            if (!tether.CoreLine.enabled) tether.CoreLine.enabled = true;
            if (!tether.AuraLine.enabled) tether.AuraLine.enabled = true;

            // Progressive strike reach (bolt extends from nozzle towards ghost on birth)
            float strikeReach = Mathf.Clamp01(tether.ManifestProgress * 1.5f);
            Vector3 currentHeadPos = Vector3.Lerp(nozzlePos, tether.LastKnownTargetPos, strikeReach);

            // Dynamic width scaling with manifest progress and item multiplier
            float effectiveCoreWidth = _stats.BaseCoreWidth * _stats.LightningWidthMultiplier * Mathf.SmoothStep(0.2f, 1f, tether.ManifestProgress);
            float effectiveAuraWidth = _stats.BaseAuraWidth * _stats.LightningWidthMultiplier * Mathf.SmoothStep(0.1f, 1f, tether.ManifestProgress);

            tether.CoreLine.widthMultiplier = effectiveCoreWidth;
            tether.AuraLine.widthMultiplier = effectiveAuraWidth;

            // Update alpha gradient keys
            tether.CoreAlphaKeys[1].alpha = 1.0f * tether.ManifestProgress;
            tether.CoreAlphaKeys[2].alpha = 1.0f * tether.ManifestProgress;
            tether.CoreAlphaKeys[3].alpha = 0.5f * tether.ManifestProgress;
            tether.CoreGradient.SetKeys(tether.CoreColorKeys, tether.CoreAlphaKeys);
            tether.CoreLine.colorGradient = tether.CoreGradient;

            tether.AuraAlphaKeys[1].alpha = 0.40f * tether.ManifestProgress;
            tether.AuraAlphaKeys[2].alpha = 0.40f * tether.ManifestProgress;
            tether.AuraAlphaKeys[3].alpha = 0.15f * tether.ManifestProgress;
            tether.AuraGradient.SetKeys(tether.AuraColorKeys, tether.AuraAlphaKeys);
            tether.AuraLine.colorGradient = tether.AuraGradient;

            int points = tether.CoreBuffer.Length;
            for (int p = 0; p < points; p++)
            {
                float t = (float)p / (points - 1);
                Vector3 basePt = Vector3.Lerp(nozzlePos, currentHeadPos, t);

                // Vortex centripetal pull towards central cone axis
                float axialDist = Vector3.Dot(basePt - nozzlePos, coneForward);
                Vector3 axisPt = nozzlePos + coneForward * axialDist;
                basePt = Vector3.Lerp(axisPt, basePt, Mathf.SmoothStep(0.15f, 1f, t));

                // Hot core stepped high-voltage crackle
                float envelope = Mathf.Sin(t * Mathf.PI);
                float crackleSeed = steppedTime * 19.37f + p * 7.19f + i * 41.53f;
                Vector3 crackleOffset = (coneRight * Mathf.Sin(crackleSeed) + coneUp * Mathf.Cos(crackleSeed * 1.37f)) * (_stats.LightningJitter * envelope);
                Vector3 corePt = basePt + crackleOffset;
                tether.CoreBuffer[p] = corePt;

                // Ethereal aura continuous backward helical suction flow
                // Minus sign (- timeFlow) causes energy waves to travel backwards towards the mouth nozzle
                float auraPhase = t * 6.0f * Mathf.PI - timeFlow + tether.PhaseOffset;
                float auraRadius = 0.065f * envelope * _stats.LightningWidthMultiplier;
                Vector3 auraOffset = (coneRight * Mathf.Cos(auraPhase) + coneUp * Mathf.Sin(auraPhase)) * auraRadius;
                tether.AuraBuffer[p] = corePt + auraOffset;
            }

            tether.CoreLine.SetPositions(tether.CoreBuffer);
            tether.AuraLine.SetPositions(tether.AuraBuffer);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!_showDebugGizmos || _suctionConeRenderer == null) return;

        Transform coneT = _suctionConeRenderer.transform;
        bool isActive = _input != null && _input.IsVacuuming;

        Color wireColor = isActive
            ? new Color(0.9f, 0.2f, 1f, 0.85f)
            : new Color(0.2f, 0.8f, 1f, 0.35f);

        Handles.color = wireColor;
        Gizmos.color = wireColor;

        float effectiveLength = _stats.BaseConeLength * _stats.ConeSizeMultiplier;
        float[] ringDistances = new float[] { 0.1f, 1.0f * _stats.ConeSizeMultiplier, 2.5f * _stats.ConeSizeMultiplier, 4.0f * _stats.ConeSizeMultiplier, effectiveLength };
        Vector3[] prevPoints = new Vector3[16];

        for (int r = 0; r < ringDistances.Length; r++)
        {
            float z = ringDistances[r];
            float unscaledZ = _stats.ConeSizeMultiplier > 0.001f ? (z / _stats.ConeSizeMultiplier) : z;
            float baseRadius = _coneRadiusProfile != null ? _coneRadiusProfile.Evaluate(unscaledZ) : 2.5f;
            float radius = baseRadius * _stats.ConeSizeMultiplier;
            Vector3 ringCenter = coneT.position + coneT.forward * z;

            Handles.DrawWireDisc(ringCenter, coneT.forward, radius);

            // Connect longitudinal hull lines
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI * 2f / 16f;
                Vector3 worldHullPoint = ringCenter + (coneT.right * Mathf.Cos(angle) + coneT.up * Mathf.Sin(angle)) * radius;

                if (r > 0)
                {
                    Gizmos.DrawLine(prevPoints[i], worldHullPoint);
                }
                prevPoints[i] = worldHullPoint;
            }
        }
    }
#endif
}
