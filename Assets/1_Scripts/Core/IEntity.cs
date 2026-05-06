using UnityEngine;

/// <summary>
/// Interface for objects that can be detected and focused on by the player's vision system.
/// </summary>
public interface IEntity
{
    /// <summary>
    /// Display name of the entity.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Priority level for detection (higher means more important).
    /// </summary>
    public int PriorityLevel { get; set; }

    /// <summary>
    /// Reference to the Unity GameObject.
    /// </summary>
    GameObject gameObject { get; }

    /// <summary>
    /// The specific point where other entities should look when focusing on this one.
    /// </summary>
    Transform LookAtPoint { get; }
}
