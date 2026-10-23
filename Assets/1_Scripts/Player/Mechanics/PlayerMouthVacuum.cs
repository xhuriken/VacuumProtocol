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
    [Tooltip("Role: Maximum distance the mouth vacuum can reach to pull and drain phantoms.")]
    [SerializeField] private float _suctionRange = 7.0f;

    [Tooltip("Role: Opening angle of the mouth suction cone in degrees.")]
    [SerializeField] private float _coneAngle = 45f;

    [Tooltip("Role: Ectoplasmic health drained per second from phantoms inside the cone.")]
    [SerializeField] private float _drainRatePerSecond = 10f;

    [Tooltip("Role: Layers that block mouth line-of-sight (walls, obstacles, ground).")]
    [SerializeField] private LayerMask _obstacleLayer;

    [Header("Suction Visuals")]
    [Tooltip("Role: Renderer of the 3D suction cone displaying the distortion material.")]
    [SerializeField] private Renderer _suctionConeRenderer;

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

    private void Awake()
    {
        _input = GetComponent<PlayerInputHandler>();
        _controller = GetComponent<PlayerV2_Controller>();

        if (_obstacleLayer.value == 0)
        {
            _obstacleLayer = LayerMask.GetMask("Default", "Ground", "Obstacle", "Obstable");
            if (_obstacleLayer.value == 0)
            {
                _obstacleLayer = (1 << 0) | (1 << 3) | (1 << 8);
            }
        }

        InitSuctionVisuals();
    }

    /// <summary>
    /// Description: Initializes the suction cone material property block and caches starting values.
    /// Fails loudly if renderer is missing.
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

        // Start fully hidden and idle
        _suctionConeRenderer.enabled = false;
        ApplyVisualProperties(0f);
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
    /// Description: Smoothly animates vortex speed, distortion strength, and tint alpha.
    /// Anti-spam protected via Mathf.MoveTowards without popping or clipping.
    /// </summary>
    private void UpdateVisuals()
    {
        if (_suctionConeRenderer == null) return;

        bool active = isLocalPlayer

            ? (_input != null && _input.IsVacuuming)

            : _isVacuumingNetworked;

        float target = active ? 1f : 0f;
        float duration = active ? _fadeInDuration : _fadeOutDuration;
        float step = Time.deltaTime / Mathf.Max(0.001f, duration);

        _currentTransition = Mathf.MoveTowards(_currentTransition, target, step);

        if (_currentTransition <= 0.0001f)
        {
            if (_suctionConeRenderer.enabled)
            {
                _suctionConeRenderer.enabled = false;
            }
            return;
        }

        if (!_suctionConeRenderer.enabled)
        {
            _suctionConeRenderer.enabled = true;
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
    }

    [Command]
    private void CmdSetVacuumingVisual(bool active)
    {
        _isVacuumingNetworked = active;
    }

    /// <summary>
    /// Description: Scans all active phantoms and applies suction and drain to those within the mouth cone.
    /// </summary>
    private void ProcessMouthSuction()
    {
        Transform viewRef = _controller.CameraTransform != null ? _controller.CameraTransform : transform;
        Vector3 mouthOrigin = viewRef.position;
        Vector3 mouthForward = viewRef.forward;

        IReadOnlyList<PhantomBase> phantoms = PhantomBase.ActivePhantoms;
        if (phantoms == null || phantoms.Count == 0) return;

        for (int i = 0; i < phantoms.Count; i++)
        {
            PhantomBase phantom = phantoms[i];
            if (phantom == null || !phantom.gameObject.activeInHierarchy) continue;

            Vector3 toPhantom = phantom.LookAtPoint.position - mouthOrigin;
            float distance = toPhantom.magnitude;

            if (distance > _suctionRange || distance < 0.01f) continue;

            // Angular check (Cone containment)
            float angle = Vector3.Angle(mouthForward, toPhantom.normalized);
            if (angle > _coneAngle / 2f) continue;

            // Obstacle Line-of-sight check
            if (Physics.Raycast(mouthOrigin, toPhantom.normalized, out RaycastHit hit, distance, _obstacleLayer, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(phantom.transform))
                {
                    // Blocked by a wall!
                    continue;
                }
            }

            // Target valid: apply instant client feedback and authoritative server command
            phantom.OnMouthSuctionApplied(mouthOrigin, _drainRatePerSecond);
            CmdApplyMouthSuction(phantom.gameObject, mouthOrigin, _drainRatePerSecond);
        }
    }

    [Command]
    private void CmdApplyMouthSuction(GameObject phantomObj, Vector3 nozzlePos, float drainRate)
    {
        if (phantomObj == null) return;
        PhantomBase phantom = phantomObj.GetComponent<PhantomBase>();
        if (phantom != null)
        {
            phantom.OnMouthSuctionApplied(nozzlePos, drainRate);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!_showDebugGizmos || _controller == null) return;

        Transform viewRef = _controller.CameraTransform != null ? _controller.CameraTransform : transform;
        Vector3 origin = viewRef.position;
        Vector3 forward = viewRef.forward;

        bool isActive = _input != null && _input.IsVacuuming;

        // Draw mouth suction cone
        Vector3 leftBoundary = Quaternion.AngleAxis(-_coneAngle / 2f, viewRef.up) * forward;
        Vector3 rightBoundary = Quaternion.AngleAxis(_coneAngle / 2f, viewRef.up) * forward;

        Color coneColor = isActive

            ? new Color(0.6f, 0.1f, 1f, 0.25f)

            : new Color(0.2f, 0.8f, 1f, 0.08f);

        Color wireColor = isActive

            ? new Color(0.8f, 0.2f, 1f, 0.9f)

            : new Color(0.2f, 0.8f, 1f, 0.3f);

        Handles.color = coneColor;
        Handles.DrawSolidArc(origin, viewRef.up, leftBoundary, _coneAngle, _suctionRange);

        Handles.color = wireColor;
        Handles.DrawWireArc(origin, viewRef.up, leftBoundary, _coneAngle, _suctionRange);
        Gizmos.color = wireColor;
        Gizmos.DrawRay(origin, leftBoundary * _suctionRange);
        Gizmos.DrawRay(origin, rightBoundary * _suctionRange);
    }
#endif
}
