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

    [Header("Debug")]
    [SerializeField] private bool _showDebugGizmos = true;

    private PlayerInputHandler _input;
    private PlayerV2_Controller _controller;

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
    }

    private void Update()
    {
        if (!isLocalPlayer || _input == null || !_input.IsVacuuming) return;

        ProcessMouthSuction();
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
