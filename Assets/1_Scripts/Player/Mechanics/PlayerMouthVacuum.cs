using System.Collections.Generic;
using Mirror;
using UnityEngine;
using Gameplay.Phantoms;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Description: Dedicated mouth vacuum controller for absorbing spectral entities (Phantoms) via dual-click (IsVacuuming).
/// Context: Attached to Player_V2. Activates only when both left and right clicks are held simultaneously.
/// Justification: Decouples phantom capture from arm-based item/dust collection, keeping the mechanics clean, SSOT, and optimized.
/// Uses PhantomBase.ActivePhantoms registry for O(1) query performance without continuous Physics.OverlapSphere allocations.
/// </summary>
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerV2_Controller))]
public class PlayerMouthVacuum : NetworkBehaviour
{
    [Header("Mouth Suction Parameters")]
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

    [Tooltip("Role: Maximum number of simultaneous electric tethers.")]
    [SerializeField] private int _maxLightningTethers = 4;

    [Tooltip("Role: Number of points per lightning arc.")]
    [SerializeField] private int _tetherPoints = 12;

    [Tooltip("Role: Maximum lateral zig-zag offset of lightning arcs in meters.")]
    [SerializeField] private float _lightningJitter = 0.16f;

    [Tooltip("Role: Width of the lightning arc lines.")]
    [SerializeField] private float _lightningLineWidth = 0.045f;

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

    private static readonly int PropVortexSpeed = Shader.PropertyToID("_VortexSpeed");
    private static readonly int PropVacuumTint = Shader.PropertyToID("_VacuumTint");
    private static readonly int PropDistortionStrength = Shader.PropertyToID("_DistortionStrength");

    private LineRenderer[] _lightningLines;
    private readonly List<PhantomBase> _activeCapturedPhantoms = new List<PhantomBase>();

    private void Awake()
    {
        _input = GetComponent<PlayerInputHandler>();
        _controller = GetComponent<PlayerV2_Controller>();

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

        if (!isVacuuming) return;

        ProcessMouthSuction();
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
            UpdateLightningTethers();
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
        // Transition alpha from 0 to initial start alpha
        float currentAlpha = Mathf.Lerp(0f, _initialTint.a, transition);

        // For multiplicative shading (SceneColor * Tint), pure neutral/untinted is White (1,1,1).
        // We lerp RGB from neutral White (1,1,1) to _initialTint.rgb, and Alpha from 0 to _initialTint.a:
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

    private void InitLightningTethers()
    {
        if (_lightningMaterial == null)
        {
            _lightningMaterial = Resources.Load<Material>("Mat_SuctionLightning");
            if (_lightningMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader != null)
                {
                    _lightningMaterial = new Material(shader)
                    {
                        name = "Runtime_LightningMat"
                    };
                    _lightningMaterial.SetFloat("_Surface", 1);
                    _lightningMaterial.SetFloat("_Blend", 1);
                    _lightningMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                    _lightningMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                    _lightningMaterial.SetColor("_BaseColor", new Color(0.25f, 0.85f, 1f, 1f));
                }
            }
        }

        Transform root = _suctionConeRenderer != null ? _suctionConeRenderer.transform : transform;
        _lightningLines = new LineRenderer[_maxLightningTethers];

        for (int i = 0; i < _maxLightningTethers; i++)
        {
            GameObject lineObj = new GameObject($"LightningTether_{i}");
            lineObj.transform.SetParent(root, false);

            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.positionCount = _tetherPoints;
            lr.startWidth = _lightningLineWidth;
            lr.endWidth = _lightningLineWidth * 0.5f;
            lr.material = _lightningMaterial;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.enabled = false;

            _lightningLines[i] = lr;
        }
    }

    /// <summary>
    /// Description: Tests whether a world-space point is inside the largest suction cone mesh (VacuumCone1).
    /// Returns local cone position for centripetal and depth calculations.
    /// </summary>
    public bool IsInsideSuctionCone(Vector3 worldPoint, out Vector3 localConePos)
    {
        localConePos = Vector3.zero;
        if (_suctionConeRenderer == null) return false;

        Transform coneT = _suctionConeRenderer.transform;
        localConePos = coneT.InverseTransformPoint(worldPoint);

        // Distance along cone forward axis Z (from mouth nozzle Z=0 to _coneLength)
        if (localConePos.z < 0f || localConePos.z > _coneLength)
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

        IReadOnlyList<PhantomBase> phantoms = PhantomBase.ActivePhantoms;
        if (phantoms != null && phantoms.Count > 0)
        {
            for (int i = 0; i < phantoms.Count; i++)
            {
                PhantomBase phantom = phantoms[i];
                if (phantom == null || !phantom.gameObject.activeInHierarchy) continue;

                Vector3 phantomTargetPos = phantom.LookAtPoint != null ? phantom.LookAtPoint.position : phantom.transform.position;

                // 1. Precise volumetric containment check inside VacuumCone1
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
                            // Obstructed by a wall!
                            continue;
                        }
                    }
                }

                _activeCapturedPhantoms.Add(phantom);

                // Apply instant client-side prediction and authoritative server command
                phantom.OnMouthSuctionApplied(nozzleOrigin, coneForward, coneUp, coneRight, _drainRatePerSecond);
                CmdApplyMouthSuction(phantom.gameObject, nozzleOrigin, coneForward, coneUp, coneRight, _drainRatePerSecond);
            }
        }

        UpdateLightningTethers();
    }

    [Command]
    private void CmdApplyMouthSuction(GameObject phantomObj, Vector3 nozzlePos, Vector3 coneForward, Vector3 coneUp, Vector3 coneRight, float drainRate)
    {
        if (phantomObj == null) return;
        PhantomBase phantom = phantomObj.GetComponent<PhantomBase>();
        if (phantom != null)
        {
            phantom.OnMouthSuctionApplied(nozzlePos, coneForward, coneUp, coneRight, drainRate);
        }
    }

    /// <summary>
    /// Description: Updates subliminal lightning arcs connecting the mouth nozzle to each captured phantom.
    /// </summary>
    private void UpdateLightningTethers()
    {
        if (_lightningLines == null || _lightningLines.Length == 0) return;

        bool isVacuuming = isLocalPlayer
            ? (_input != null && _input.IsVacuuming)
            : _isVacuumingNetworked;

        if (!isVacuuming || _activeCapturedPhantoms.Count == 0 || _suctionConeRenderer == null)
        {
            for (int i = 0; i < _lightningLines.Length; i++)
            {
                if (_lightningLines[i] != null && _lightningLines[i].enabled)
                {
                    _lightningLines[i].enabled = false;
                }
            }
            return;
        }

        Vector3 nozzlePos = _suctionConeRenderer.transform.position;
        Vector3 coneForward = _suctionConeRenderer.transform.forward;

        for (int i = 0; i < _lightningLines.Length; i++)
        {
            LineRenderer lr = _lightningLines[i];
            if (lr == null) continue;

            if (i < _activeCapturedPhantoms.Count)
            {
                PhantomBase phantom = _activeCapturedPhantoms[i];
                if (phantom == null || !phantom.gameObject.activeInHierarchy)
                {
                    lr.enabled = false;
                    continue;
                }

                // Subliminal strobe: rapid flicker for intense electric energy
                bool subliminalFlicker = (Time.frameCount % 2 == 0) || (Random.value > 0.15f);
                lr.enabled = subliminalFlicker;

                if (!subliminalFlicker) continue;

                Vector3 targetPos = phantom.LookAtPoint != null ? phantom.LookAtPoint.position : phantom.transform.position;

                // Animate dynamic line width with high-voltage pulse
                float widthPulse = _lightningLineWidth * Random.Range(0.8f, 1.4f);
                lr.startWidth = widthPulse;
                lr.endWidth = widthPulse * 0.6f;

                // Calculate curved fractal arc points from nozzle to target
                for (int p = 0; p < _tetherPoints; p++)
                {
                    float t = (float)p / (_tetherPoints - 1);
                    Vector3 basePt = Vector3.Lerp(nozzlePos, targetPos, t);

                    // Pull slightly towards the vortex central axis along the way
                    float axialDist = Vector3.Dot(basePt - nozzlePos, coneForward);
                    Vector3 axisPt = nozzlePos + coneForward * axialDist;
                    basePt = Vector3.Lerp(axisPt, basePt, Mathf.SmoothStep(0f, 1f, t));

                    // Add erratic high-voltage zig-zag noise on intermediate points
                    if (p > 0 && p < _tetherPoints - 1)
                    {
                        float jitterScale = Mathf.Sin(t * Mathf.PI) * _lightningJitter;
                        Vector3 randJitter = Random.insideUnitSphere * jitterScale;
                        basePt += randJitter;
                    }

                    lr.SetPosition(p, basePt);
                }
            }
            else
            {
                if (lr.enabled) lr.enabled = false;
            }
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

        // Draw cross-section rings along VacuumCone1 length
        float[] ringDistances = new float[] { 0.1f, 1.0f, 2.5f, 4.0f, _coneLength };
        Vector3[] prevPoints = new Vector3[16];

        for (int r = 0; r < ringDistances.Length; r++)
        {
            float z = ringDistances[r];
            float radius = _coneRadiusProfile != null ? _coneRadiusProfile.Evaluate(z) : 2.5f;
            Vector3 ringCenter = coneT.TransformPoint(new Vector3(0, 0, z));

            Handles.DrawWireDisc(ringCenter, coneT.forward, radius);

            // Connect longitudinal hull lines
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI * 2f / 16f;
                Vector3 localHullPoint = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, z);
                Vector3 worldHullPoint = coneT.TransformPoint(localHullPoint);

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
