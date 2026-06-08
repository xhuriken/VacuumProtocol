using UnityEngine;

/// <summary>
/// Represents a generic item in the world that can be detected by entities like the player.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Collectible : MonoBehaviour, IEntity
{
    [Header("Entity Settings")]
    [Tooltip("The display name of the collectible.")]
    [SerializeField]
    private string _name = "Collectible";

    [Tooltip("Priority level for entity detection systems.")]
    [SerializeField]
    private int _priorityLevel = 2;

    [Header("Vacuum Settings")]
    [Tooltip("Resistance factor to suction forces. Higher values make the object heavier/harder to pull.")]
    [SerializeField]
    private float _pullResistance = 1.0f;

    private Vector3 _originalScale;
    private Rigidbody _rb;

    /// <summary>
    /// Gets or sets the display name of the entity.
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
    /// Gets or sets the priority level for detection (higher means more important).
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
    /// Gets the specific point where other entities should look when focusing on this one.
    /// </summary>
    public Transform LookAtPoint
    {
        get
        {
            return transform;
        }
    }

    /// <summary>
    /// Gets the cached original local scale of the object.
    /// </summary>
    public Vector3 OriginalScale
    {
        get
        {
            return _originalScale;
        }
    }

    /// <summary>
    /// Gets the proportional resistance of the object to suction forces.
    /// </summary>
    public float PullResistance
    {
        get
        {
            return _pullResistance;
        }
    }

    /// <summary>
    /// Gets the Rigidbody component cached on Awake.
    /// </summary>
    public Rigidbody Rb
    {
        get
        {
            return _rb;
        }
    }

    /// <summary>
    /// Awake callback. Caches references and stores the initial local scale.
    /// </summary>
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _originalScale = transform.localScale;
    }

    /// <summary>
    /// Resets the object's local scale back to its cached original scale.
    /// </summary>
    public void ResetScale()
    {
        transform.localScale = _originalScale;
    }
}
