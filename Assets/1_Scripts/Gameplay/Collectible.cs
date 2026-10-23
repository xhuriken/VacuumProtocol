using UnityEngine;
using VacuumProtocol.Core;

/// <summary>
/// Description: Represents a generic item in the world that can be detected by entities like the player.
/// Context: Attached to prefabs that the player can look at, pick up, or vacuum.
/// Justification: Implements IEntity to integrate with the player's vision system and serves as a base container for physics properties (mass/suction resistance).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Collectible : MonoBehaviour, IEntity, IVacuumable
{
    [Header("Entity Settings")]
    [Tooltip("Role: The display name of the collectible.\nUse Case: UI Label rendering.\nJustification: Gives the player readable text when looking at the object.")]
    [SerializeField]
    private string _name = "Collectible";

    [Tooltip("Role: Priority level for entity detection systems.\nUse Case: Vision targeting.\nJustification: Allows important items to take focus over generic clutter.")]
    [SerializeField]
    private int _priorityLevel = 2;

    [Header("Vacuum Settings")]
    [Tooltip("Role: Resistance factor to suction forces.\nUse Case: Physics calculations.\nJustification: Higher values simulate heavier objects that are harder to pull, adding gameplay variety.")]
    [SerializeField]
    private float _pullResistance = 1.0f;

    private Vector3 _originalScale;
    private Rigidbody _rb;

    /// <summary>
    /// Description: Gets or sets the display name of the entity.
    /// Context: Satisfies IEntity interface. Used by UI to render text.
    /// Justification: Wrapped in a property to allow potential future localization hooks.
    /// </summary>
    public string Name
    {
        get
        {
            return _name;
        }
        set
        {
            _name = value;
        }
    }

    /// <summary>
    /// Description: Gets or sets the priority level for detection (higher means more important).
    /// Context: Satisfies IEntity interface. Used by PlayerV2_Look.
    /// Justification: Resolves conflicts when multiple objects are within the player's field of view.
    /// </summary>
    public int PriorityLevel
    {
        get
        {
            return _priorityLevel;
        }
        set
        {
            _priorityLevel = value;
        }
    }

    /// <summary>
    /// Description: Gets the specific point where other entities should look when focusing on this one.
    /// Context: Satisfies IEntity interface.
    /// Justification: Currently defaults to the root transform, but allows future override to target a specific child (like a label or handle).
    /// </summary>
    public Transform LookAtPoint
    {
        get
        {
            return transform;
        }
    }

    /// <summary>
    /// Description: Gets the cached original local scale of the object.
    /// Context: Used by the VacuumSuctionZone to shrink objects and then restore them if they escape.
    /// Justification: Collectibles can have any arbitrary initial scale set by level designers. Caching this prevents permanent deformation.
    /// </summary>
    public Vector3 OriginalScale
    {
        get
        {
            return _originalScale;
        }
    }

    /// <summary>
    /// Description: Gets the proportional resistance of the object to suction forces.
    /// Context: Used by VacuumSuctionZone force multiplier math.
    /// Justification: Decouples suction resistance from Rigidbody mass, allowing a heavy object to be easily sucked up, or a light object to stick to the ground.
    /// </summary>
    public float PullResistance
    {
        get
        {
            return _pullResistance;
        }
    }

    /// <summary>
    /// Description: Gets the Rigidbody component cached on Awake.
    /// Context: Used by physics effectors to apply forces.
    /// Justification: Caching avoids expensive GetComponent calls in FixedUpdate loops.
    /// </summary>
    public Rigidbody Rb
    {
        get
        {
            return _rb;
        }
    }

    /// <summary>
    /// Description: Awake callback. Caches references and stores the initial local scale.
    /// Context: Lifecycle event triggered before Start.
    /// Justification: Ensures that OriginalScale is perfectly accurate to the scene's starting state before any physics act on it.
    /// </summary>
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _originalScale = transform.localScale;
    }

    /// <summary>
    /// Description: Resets the object's local scale back to its cached original scale.
    /// Context: Called when the item escapes the vacuum zone or is dropped from inventory.
    /// Justification: Reverts the shrinking effect applied dynamically by the vacuum nozzle.
    /// </summary>
    public void ResetScale()
    {
        transform.localScale = _originalScale;
    }

    #region IVacuumable Implementation
    public Rigidbody Rigidbody => _rb;

    public void OnSuctionPulled(Vector3 nozzlePosition, Vector3 suctionDirection, float pullForce, float visibilityFactor)
    {
        if (_rb == null) return;

        Vector3 toItem = transform.position - nozzlePosition;
        float distance = toItem.magnitude;
        if (distance < 0.01f) return;

        Vector3 direction = toItem.normalized;
        float resistance = Mathf.Max(0.05f, _pullResistance);
        float forceAmount = (pullForce / resistance) * visibilityFactor;
        float distanceScale = Mathf.Clamp(2.0f - (distance / 4f), 0.5f, 2.0f);
        Vector3 attractionForce = -direction * (forceAmount * distanceScale);

        _rb.AddForce(attractionForce, ForceMode.Force);
    }

    public void OnSuctionDrained(float drainAmount, Mirror.NetworkConnectionToClient playerConnection)
    {
        // Physical items don't have drainable fluid/dirt
    }

    public bool CanBeAbsorbed(float distanceToNozzle, float absorbThreshold)
    {
        return distanceToNozzle <= absorbThreshold;
    }

    public void OnAbsorbed(PlayerVacuumController playerVacuum)
    {
        if (playerVacuum != null && playerVacuum.isLocalPlayer)
        {
            playerVacuum.AbsorbObject(gameObject);
        }
    }
    #endregion
}
