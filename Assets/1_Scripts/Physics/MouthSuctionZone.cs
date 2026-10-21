using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Description: Controls the mouth suction zone dedicated to Phantoms, activated exclusively during dual-click suction.
/// Context: Attached to the player's head or mouth hierarchy.
/// Justification: Separates Phantom mouth suction logic, force application, and health drainage from arm-based item suction.
/// </summary>
public class MouthSuctionZone : MonoBehaviour
{
    #region Inspector Fields

    [Header("Cone Settings")]
    [Tooltip("Role: Maximum range of the mouth suction cone.\nUse Case: Overlap detection sphere radius.")]
    [SerializeField]
    private float _suctionRange = 5.0f;

    [Tooltip("Role: Opening half-angle of the cone in degrees.\nUse Case: Angular filter relative to mouth direction.")]
    [SerializeField]
    private float _coneAngle = 40f;

    [Tooltip("Role: Local axis of the nozzle representing the suction direction.")]
    [SerializeField]
    private LocalAxis _suctionAxis = LocalAxis.Forward;

    [Tooltip("Role: Layer mask used to identify Phantom entities in the query.")]
    [SerializeField]
    private LayerMask _phantomLayer;

    [Tooltip("Role: Layer mask used for occlusion raycasts (walls, obstacles).")]
    [SerializeField]
    private LayerMask _occlusionLayer;

    [Tooltip("Role: Origin transform of the suction cone (the player's mouth nozzle).\nUse Case: Cone origin and destination point.")]
    [SerializeField]
    private Transform _nozzleTransform;

    [Header("Suction & Drainage Settings")]
    [Tooltip("Role: Base physical attraction force applied to phantoms.\nUse Case: Pulling phantoms toward the player.")]
    [SerializeField]
    private float _suctionForce = 35f;

    [Tooltip("Role: Lateral centripetal force guiding phantoms toward the center of the cone.")]
    [SerializeField]
    private float _centripetalForce = 15f;

    [Tooltip("Role: Momentum damping applied to phantoms inside the suction stream.")]
    [SerializeField]
    private float _suctionDamping = 8f;

    [Tooltip("Role: Health drained per second from phantoms inside the active suction cone.\nUse Case: Phantoms shrink and get captured as health drops.")]
    [SerializeField]
    private float _healthDrainRatePerSecond = 30f;

    [Tooltip("Role: Distance threshold to perform instant absorption once health is zero.")]
    [SerializeField]
    private float _absorbDistance = 0.8f;

    [Header("Visual Effects & Particles")]
    [Tooltip("Role: Particle system playing when mouth suction is active.")]
    [SerializeField]
    private ParticleSystem _mouthVacuumParticles;

    [Tooltip("Role: The force field GameObjects for the mouth suction particles.\nUse Case: Enabled when active, disabled when inactive.")]
    [SerializeField]
    private GameObject[] _mouthVacuumForceFields;

    [Header("Suction Visual Tether Line")]
    [Tooltip("Role: LineRenderer drawing a visual tether beam between mouth and the targeted phantom.")]
    [SerializeField]
    private LineRenderer _suctionTetherLine;

    [Tooltip("Role: Color of the mouth suction tether beam.")]
    [SerializeField]
    private Color _tetherColor = new Color(0.2f, 0.9f, 1f, 0.9f);

    #endregion

    #region Private Fields

    private bool _isActive = false;
    private PlayerVacuumController _playerVacuum;
    private readonly List<PhantomBase> _currentTickPhantoms = new List<PhantomBase>();

    #endregion

    #region Properties

    /// <summary>
    /// Description: Gets or sets whether the mouth suction cone is actively pulling and draining phantoms.
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                UpdateParticleState();
                UpdateForceFields();
            }
        }
    }

    /// <summary>
    /// Description: Gets the configured suction range.
    /// </summary>
    public float SuctionRange => _suctionRange;

    /// <summary>
    /// Description: Gets the configured cone angle.
    /// </summary>
    public float ConeAngle => _coneAngle;

    /// <summary>
    /// Description: Gets the absorb distance threshold for capturing phantoms.
    /// </summary>
    public float AbsorbDistance => _absorbDistance;

    /// <summary>
    /// Description: Gets the nozzle transform representing the mouth suction point.
    /// </summary>
    public Transform NozzleTransform => _nozzleTransform != null ? _nozzleTransform : transform;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Description: Awake callback. Caches references and validates configuration.
    /// </summary>
    private void Awake()
    {
        if (_nozzleTransform == null)
        {
            _nozzleTransform = transform;
        }

        _playerVacuum = GetComponentInParent<PlayerVacuumController>();
        if (_playerVacuum == null)
        {
            Debug.LogError($"[MouthSuctionZone] PlayerVacuumController not found in parents of {gameObject.name}!");
        }

        // Auto-configure suction tether LineRenderer if not explicitly assigned
        if (_suctionTetherLine == null)
        {
            _suctionTetherLine = GetComponent<LineRenderer>();
            if (_suctionTetherLine == null)
            {
                _suctionTetherLine = gameObject.AddComponent<LineRenderer>();
            }

            _suctionTetherLine.useWorldSpace = true;
            _suctionTetherLine.startWidth = 0.04f;
            _suctionTetherLine.endWidth = 0.12f;
            _suctionTetherLine.material = new Material(Shader.Find("Sprites/Default"));
            _suctionTetherLine.startColor = _tetherColor;
            _suctionTetherLine.endColor = new Color(1f, 1f, 1f, 0.9f);
            _suctionTetherLine.positionCount = 2;
            _suctionTetherLine.enabled = false;
        }
    }

    /// <summary>
    /// Description: FixedUpdate callback. Processes suction physics and health drainage when active.
    /// </summary>
    private void FixedUpdate()
    {
        if (!_isActive || _nozzleTransform == null)
        {
            return;
        }

        ProcessSuctionPhysics();
    }

    /// <summary>
    /// Description: LateUpdate callback. Updates visual suction tether LineRenderer between mouth and active phantom.
    /// </summary>
    private void LateUpdate()
    {
        if (_suctionTetherLine == null)
        {
            return;
        }

        if (_isActive && _currentTickPhantoms.Count > 0 && _currentTickPhantoms[0] != null && !_currentTickPhantoms[0].IsAbsorbed)
        {
            PhantomBase targetPhantom = _currentTickPhantoms[0];
            Vector3 startPos = _nozzleTransform != null ? _nozzleTransform.position : transform.position;
            Vector3 endPos = targetPhantom.LookAtPoint != null ? targetPhantom.LookAtPoint.position : targetPhantom.transform.position;

            _suctionTetherLine.enabled = true;
            _suctionTetherLine.SetPosition(0, startPos);
            _suctionTetherLine.SetPosition(1, endPos);
        }
        else
        {
            if (_suctionTetherLine.enabled)
            {
                _suctionTetherLine.enabled = false;
            }
        }
    }

    #endregion

    #region Suction & Drain Processing

    /// <summary>
    /// Description: Queries for phantoms within the suction cone, checks visibility, and applies physical forces and health drain.
    /// </summary>
    private void ProcessSuctionPhysics()
    {
        _currentTickPhantoms.Clear();

        Vector3 nozzlePos = _nozzleTransform.position;
        Vector3 suctionDir = GetSuctionDirection();

        // Support triggers so ethereal phantoms can be vacuumed, and fallback if LayerMask is unassigned
        LayerMask detectionMask = _phantomLayer.value != 0 ? _phantomLayer : ~0;
        Collider[] hits = Physics.OverlapSphere(nozzlePos, _suctionRange, detectionMask, QueryTriggerInteraction.Collide);

        for (int i = 0; i < hits.Length; i++)
        {
            PhantomBase phantom = hits[i].GetComponentInParent<PhantomBase>();
            if (phantom == null || phantom.IsAbsorbed || _currentTickPhantoms.Contains(phantom))
            {
                continue;
            }

            Vector3 toPhantom = phantom.transform.position - nozzlePos;
            float distance = toPhantom.magnitude;

            if (distance <= 0.001f)
            {
                continue;
            }

            Vector3 dirToPhantom = toPhantom / distance;
            float angle = Vector3.Angle(suctionDir, dirToPhantom);

            if (angle <= _coneAngle)
            {
                // Check occlusion between nozzle and phantom
                if (!IsOccluded(nozzlePos, phantom.transform.position, phantom.gameObject))
                {
                    _currentTickPhantoms.Add(phantom);
                }
            }
        }

        // Apply forces and drain for all detected phantoms
        float dt = Time.fixedDeltaTime;
        for (int i = 0; i < _currentTickPhantoms.Count; i++)
        {
            PhantomBase phantom = _currentTickPhantoms[i];
            ApplySuctionToPhantom(phantom, nozzlePos, suctionDir, dt);
        }
    }

    /// <summary>
    /// Description: Applies physics pull, centripetal alignment, drag, and triggers health drainage on a targeted phantom.
    /// </summary>
    private void ApplySuctionToPhantom(PhantomBase phantom, Vector3 nozzlePos, Vector3 suctionDir, float dt)
    {
        if (phantom == null || phantom.Rb == null || phantom.IsAbsorbed)
        {
            return;
        }

        phantom.SetVacuumedState(true);

        Vector3 toNozzle = nozzlePos - phantom.transform.position;
        float distance = toNozzle.magnitude;
        Vector3 pullDir = distance > 0.001f ? toNozzle / distance : -suctionDir;

        float resistance = Mathf.Max(0.1f, phantom.PullResistance);

        // 1. Pull Force towards nozzle
        float pullForceMagnitude = (_suctionForce / resistance);
        phantom.Rb.AddForce(pullDir * pullForceMagnitude, ForceMode.Force);

        // 2. Smooth Centripetal Alignment (Spring-Damper along perpendicular axis)
        Vector3 projectionOnAxis = Vector3.Project(phantom.transform.position - nozzlePos, suctionDir);
        Vector3 closestPointOnAxis = nozzlePos + projectionOnAxis;
        Vector3 toCenterAxis = closestPointOnAxis - phantom.transform.position;
        float lateralOffset = toCenterAxis.magnitude;
        if (lateralOffset > 0.01f)
        {
            Vector3 centripetalDir = toCenterAxis / lateralOffset;
            float alignmentForce = Mathf.Min(lateralOffset * _centripetalForce, _centripetalForce * 1.5f);
            phantom.Rb.AddForce(centripetalDir * alignmentForce, ForceMode.Force);
        }

        // 3. Momentum Damping / Drag
        if (_suctionDamping > 0f)
        {
            Vector3 dragForce = -phantom.Rb.linearVelocity * (_suctionDamping / resistance);
            phantom.Rb.AddForce(dragForce, ForceMode.Force);
        }

        // 4. Health Drain Request
        float drainAmount = _healthDrainRatePerSecond * dt;
        if (_playerVacuum != null)
        {
            _playerVacuum.DrainPhantom(phantom, drainAmount);
        }
    }

    /// <summary>
    /// Description: Checks if an obstacle blocks the line of sight between the nozzle and the phantom.
    /// </summary>
    private bool IsOccluded(Vector3 origin, Vector3 target, GameObject phantomObj)
    {
        Vector3 direction = target - origin;
        float dist = direction.magnitude;

        if (dist <= 0.01f)
        {
            return false;
        }

        if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit, dist, _occlusionLayer, QueryTriggerInteraction.Ignore))
        {
            if (!hit.transform.IsChildOf(phantomObj.transform) && hit.transform.gameObject != phantomObj)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Description: Computes the world-space suction vector based on the configured LocalAxis.
    /// </summary>
    public Vector3 GetSuctionDirection()
    {
        Transform t = _nozzleTransform != null ? _nozzleTransform : transform;
        switch (_suctionAxis)
        {
            case LocalAxis.Forward: return t.forward;
            case LocalAxis.Backward: return -t.forward;
            case LocalAxis.Right: return t.right;
            case LocalAxis.Left: return -t.right;
            case LocalAxis.Up: return t.up;
            case LocalAxis.Down: return -t.up;
            default: return t.forward;
        }
    }

    /// <summary>
    /// Description: Updates mouth vacuum particle system state when suction toggles.
    /// Uses explicit emission toggle and Play/Stop without isPlaying checks to support fast activation spamming.
    /// </summary>
    private void UpdateParticleState()
    {
        if (_mouthVacuumParticles == null)
        {
            return;
        }

        var emission = _mouthVacuumParticles.emission;
        emission.enabled = _isActive;

        if (_isActive)
        {
            _mouthVacuumParticles.Play(true);
        }
        else
        {
            _mouthVacuumParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    /// <summary>
    /// Description: Enables or disables the force fields attached to the mouth suction zone.
    /// </summary>
    private void UpdateForceFields()
    {
        if (_mouthVacuumForceFields != null)
        {
            for (int i = 0; i < _mouthVacuumForceFields.Length; i++)
            {
                if (_mouthVacuumForceFields[i] != null)
                {
                    _mouthVacuumForceFields[i].SetActive(_isActive);
                }
            }
        }
    }

    #endregion

    #region Selection Gizmos

    /// <summary>
    /// Description: Draws suction cone when MouthSuctionZone is selected in the Editor.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Transform t = _nozzleTransform != null ? _nozzleTransform : transform;
        Vector3 pos = t.position;
        Vector3 dir = GetSuctionDirection().normalized;

        float radAngle = _coneAngle * Mathf.Deg2Rad;
        float baseRadius = _suctionRange * Mathf.Tan(radAngle);
        Vector3 baseCenter = pos + dir * _suctionRange;

        Vector3 right = Vector3.Cross(dir, t.up).normalized;
        if (right.sqrMagnitude < 0.001f)
        {
            right = Vector3.Cross(dir, t.forward).normalized;
        }
        Vector3 up = Vector3.Cross(right, dir).normalized;

        Gizmos.color = _isActive ? Color.green : new Color(0.2f, 0.8f, 1f, 0.8f);

        int segments = 24;
        Vector3 prevPoint = baseCenter + right * baseRadius;
        for (int i = 1; i <= segments; i++)
        {
            float angle = (i * 360f / segments) * Mathf.Deg2Rad;
            Vector3 nextPoint = baseCenter + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * baseRadius;
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }

        Gizmos.DrawLine(pos, baseCenter + right * baseRadius);
        Gizmos.DrawLine(pos, baseCenter - right * baseRadius);
        Gizmos.DrawLine(pos, baseCenter + up * baseRadius);
        Gizmos.DrawLine(pos, baseCenter - up * baseRadius);
        Gizmos.DrawRay(pos, dir * _suctionRange);

        if (_absorbDistance > 0f)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(pos, _absorbDistance);
        }
    }

    #endregion
}
