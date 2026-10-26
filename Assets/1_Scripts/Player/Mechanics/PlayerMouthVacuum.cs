using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using Gameplay.Phantoms;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Description: Centralized upgradable parameters for mouth phantom suction, ectoplasm drain, and ghost echo trail visuals.
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

    [Header("Phantom Suction Ghost Echoes (SSOT)")]
    [Tooltip("Interval in seconds between ghost snapshots emitted per captured phantom.")]
    public float EchoSpawnInterval = 0.055f;

    [Tooltip("Maximum lifetime in seconds for an individual ghost echo before despawning.")]
    public float EchoLifetime = 0.45f;

    [Tooltip("Velocity in meters per second pulling ghost echoes towards the mouth nozzle.")]
    public float EchoPullSpeed = 7.5f;

    [Tooltip("Scale ratio when an echo reaches the mouth nozzle (e.g. 0.15 = shrinks down to 15% original scale).")]
    [Range(0.01f, 0.5f)]
    public float EchoMinScaleRatio = 0.15f;

    [Tooltip("Fresnel rim power for the ectoplasm ghost shader.")]
    public float EchoFresnelPower = 2.5f;

    [Tooltip("Base HDR tint color for the ectoplasmic suction echoes.")]
    public Color EchoColor = new Color(0.25f, 0.85f, 1.0f, 0.85f);

    [Tooltip("Maximum number of pre-allocated ghost echo objects in the pool.")]
    public int MaxEchoPoolSize = 48;
}

/// <summary>
/// Description: Dedicated mouth vacuum controller for absorbing spectral entities (Phantoms) via dual-click (IsVacuuming).
/// Context: Attached to Player_V2. Activates only when both left and right clicks are held simultaneously.
/// Justification: Decouples phantom capture from arm-based item/dust collection, keeping mechanics clean, SSOT, and optimized.
/// Uses PhantomBase.ActivePhantoms registry for O(1) query performance without continuous Physics.OverlapSphere allocations.
/// Features high-performance pooled ghost afterimages (Fresnel echoes) that stream into the mouth nozzle while shrinking and dissolving.
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

    [Header("Phantom Ghost Echoes Visuals")]
    [Tooltip("Role: Additive Fresnel material used for phantom ghost snapshots.")]
    [SerializeField] private Material _echoMaterial;

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

    private static readonly int PropEchoAlpha = Shader.PropertyToID("_Alpha");
    private static readonly int PropEchoColor = Shader.PropertyToID("_Color");
    private static readonly int PropEchoFresnel = Shader.PropertyToID("_FresnelPower");

    /// <summary>
    /// Description: Internal pooled entity representing a phantom snapshot afterimage streaming towards the nozzle.
    /// Operates with zero per-frame GC allocations through shared MeshFilter reassignment and MaterialPropertyBlocks.
    /// </summary>
    private class PhantomSuctionEcho
    {
        public GameObject GameObject;
        public Transform Transform;
        public MeshFilter MeshFilter;
        public MeshRenderer Renderer;
        public bool IsActive;
        public float Age;
        public float MaxLifetime;
        public Vector3 InitialScale;
        public float InitialDistanceToNozzle;
    }

    private GameObject _echoPoolRoot;
    private PhantomSuctionEcho[] _echoPool;
    private int _nextEchoPoolIndex = 0;
    private MaterialPropertyBlock _echoMpb;
    private readonly Dictionary<PhantomBase, float> _phantomEchoTimers = new Dictionary<PhantomBase, float>();
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
    /// Description: Updates snapshot spawn frequency per captured phantom.
    /// </summary>
    public void SetEchoSpawnInterval(float interval)
    {
        if (interval < 0.01f)
        {
            Debug.LogError($"[PlayerMouthVacuum] Invalid EchoSpawnInterval: {interval}. Value must be >= 0.01.", this);
            return;
        }

        _stats.EchoSpawnInterval = interval;
    }

    /// <summary>
    /// Description: Updates velocity dragging ghost snapshots into the mouth nozzle.
    /// </summary>
    public void SetEchoPullSpeed(float speed)
    {
        if (speed < 0.1f)
        {
            Debug.LogError($"[PlayerMouthVacuum] Invalid EchoPullSpeed: {speed}. Value must be >= 0.1.", this);
            return;
        }

        _stats.EchoPullSpeed = speed;
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
        InitEchoPool();
    }

    private void OnDestroy()
    {
        if (_echoPoolRoot != null)
        {
            Destroy(_echoPoolRoot);
            _echoPoolRoot = null;
        }
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

    /// <summary>
    /// Description: Pre-allocates and initializes the pool of ghost echo entities with zero runtime allocations.
    /// Fails loudly if ghost echo material cannot be resolved.
    /// </summary>
    private void InitEchoPool()
    {
        if (_echoMaterial == null)
        {
            _echoMaterial = Resources.Load<Material>("Mat_PhantomSuctionEcho");
#if UNITY_EDITOR
            if (_echoMaterial == null)
            {
                _echoMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/4_Graphics/Shader/Player/Mat_PhantomSuctionEcho.mat");
            }
#endif
        }

        if (_echoMaterial == null)
        {
            Debug.LogError($"[PlayerMouthVacuum] '_echoMaterial' could not be found or loaded! Assign Mat_PhantomSuctionEcho on '{gameObject.name}'. Fail loudly per workspace rules.", this);
            return;
        }

        _echoMpb = new MaterialPropertyBlock();
        int poolSize = Mathf.Max(8, _stats.MaxEchoPoolSize);
        _echoPool = new PhantomSuctionEcho[poolSize];

        // Root kept in world space so snapshots maintain accurate spatial orientations independent of camera/player rotation
        _echoPoolRoot = new GameObject($"SuctionEchoPool_{gameObject.name}");

        for (int i = 0; i < poolSize; i++)
        {
            GameObject echoObj = new GameObject($"GhostEcho_{i}");
            echoObj.transform.SetParent(_echoPoolRoot.transform, false);

            MeshFilter mf = echoObj.AddComponent<MeshFilter>();
            MeshRenderer mr = echoObj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _echoMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            echoObj.SetActive(false);

            _echoPool[i] = new PhantomSuctionEcho
            {
                GameObject = echoObj,
                Transform = echoObj.transform,
                MeshFilter = mf,
                Renderer = mr,
                IsActive = false,
                Age = 0f,
                MaxLifetime = _stats.EchoLifetime,
                InitialScale = Vector3.one,
                InitialDistanceToNozzle = 1f
            };
        }
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
        UpdateSuctionEchoes();

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
            if (_activeCapturedPhantoms.Count > 0)
            {
                _activeCapturedPhantoms.Clear();
            }
            if (_phantomEchoTimers.Count > 0)
            {
                _phantomEchoTimers.Clear();
            }
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
    /// Emits ghost snapshots for captured phantoms based on EchoSpawnInterval.
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

                // 3. Emit phantom suction ghost snapshots (Fresnel echo trail)
                if (!_phantomEchoTimers.TryGetValue(phantom, out float nextEchoTime) || Time.time >= nextEchoTime)
                {
                    SpawnEchoSnapshot(phantom, nozzleOrigin);
                    _phantomEchoTimers[phantom] = Time.time + _stats.EchoSpawnInterval;
                }
            }
        }
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
    /// Description: Spawns an animated snapshot clone of the captured phantom using its exact mesh and transform.
    /// Reuses pooled items with zero runtime instantiate/destroy calls.
    /// </summary>
    private void SpawnEchoSnapshot(PhantomBase phantom, Vector3 nozzlePos)
    {
        if (_echoPool == null || _echoPool.Length == 0 || phantom == null) return;

        MeshFilter pMeshFilter = phantom.VisualMeshFilter;
        if (pMeshFilter == null || pMeshFilter.sharedMesh == null)
        {
            Debug.LogError($"[PlayerMouthVacuum] Cannot spawn ghost echo: Phantom '{phantom.name}' has no valid VisualMeshFilter or sharedMesh! Fail loudly per workspace rules.", phantom);
            return;
        }

        // Find next available slot or recycle oldest via circular index
        PhantomSuctionEcho echo = null;
        int poolSize = _echoPool.Length;
        for (int i = 0; i < poolSize; i++)
        {
            int index = (_nextEchoPoolIndex + i) % poolSize;
            if (!_echoPool[index].IsActive)
            {
                echo = _echoPool[index];
                _nextEchoPoolIndex = (index + 1) % poolSize;
                break;
            }
        }

        if (echo == null)
        {
            echo = _echoPool[_nextEchoPoolIndex];
            _nextEchoPoolIndex = (_nextEchoPoolIndex + 1) % poolSize;
        }

        Transform pT = pMeshFilter.transform;
        echo.MeshFilter.sharedMesh = pMeshFilter.sharedMesh;
        echo.Transform.position = pT.position;
        echo.Transform.rotation = pT.rotation;
        echo.Transform.localScale = pT.lossyScale;

        echo.InitialScale = pT.lossyScale;
        echo.InitialDistanceToNozzle = Mathf.Max(0.2f, Vector3.Distance(pT.position, nozzlePos));
        echo.Age = 0f;
        echo.MaxLifetime = _stats.EchoLifetime;
        echo.IsActive = true;

        // Initialize MaterialPropertyBlock
        echo.Renderer.GetPropertyBlock(_echoMpb);
        _echoMpb.SetFloat(PropEchoAlpha, 1.0f);
        _echoMpb.SetColor(PropEchoColor, _stats.EchoColor);
        _echoMpb.SetFloat(PropEchoFresnel, _stats.EchoFresnelPower);
        echo.Renderer.SetPropertyBlock(_echoMpb);

        if (!echo.GameObject.activeSelf)
        {
            echo.GameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Description: Updates all active ghost afterimages, animating their movement towards the mouth nozzle,
    /// progressively shrinking their scale and dissolving their alpha.
    /// </summary>
    private void UpdateSuctionEchoes()
    {
        if (_echoPool == null || _echoPool.Length == 0 || _suctionConeRenderer == null) return;

        Vector3 nozzlePos = _suctionConeRenderer.transform.position;
        float dt = Time.deltaTime;

        for (int i = 0; i < _echoPool.Length; i++)
        {
            PhantomSuctionEcho echo = _echoPool[i];
            if (echo == null || !echo.IsActive) continue;

            echo.Age += dt;
            float lifeProgress = Mathf.Clamp01(echo.Age / Mathf.Max(0.01f, echo.MaxLifetime));

            // Pull towards the mouth nozzle
            echo.Transform.position = Vector3.MoveTowards(echo.Transform.position, nozzlePos, _stats.EchoPullSpeed * dt);
            float currentDist = Vector3.Distance(echo.Transform.position, nozzlePos);

            // Reached nozzle or lifetime expired -> recycle to pool
            if (currentDist <= 0.15f || echo.Age >= echo.MaxLifetime)
            {
                DespawnEcho(echo);
                continue;
            }

            // Progression along the suction path into the mouth
            float distProgress = 1f - Mathf.Clamp01(currentDist / Mathf.Max(0.01f, echo.InitialDistanceToNozzle));
            float shrinkProgress = Mathf.Max(distProgress, lifeProgress);

            // Animate scale shrinking as it gets closer to the nozzle
            echo.Transform.localScale = Vector3.Lerp(echo.InitialScale, echo.InitialScale * _stats.EchoMinScaleRatio, shrinkProgress);

            // Animate progressive alpha dissolve closer to the mouth nozzle
            float alpha = (1f - distProgress) * (1f - lifeProgress);

            echo.Renderer.GetPropertyBlock(_echoMpb);
            _echoMpb.SetFloat(PropEchoAlpha, alpha);
            _echoMpb.SetColor(PropEchoColor, _stats.EchoColor);
            _echoMpb.SetFloat(PropEchoFresnel, _stats.EchoFresnelPower);
            echo.Renderer.SetPropertyBlock(_echoMpb);
        }
    }

    private void DespawnEcho(PhantomSuctionEcho echo)
    {
        if (echo == null) return;
        echo.IsActive = false;
        if (echo.GameObject.activeSelf)
        {
            echo.GameObject.SetActive(false);
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
