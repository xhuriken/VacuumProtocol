using UnityEngine;

/// <summary>
/// Description: Interface for objects that can be detected and focused on by the player's vision system.
/// Context: Used by the player's head and eye scripts to determine what dynamic objects to look at in the world.
/// Justification: Abstracting this to an interface ensures decoupling, allowing any GameObject (collectibles, other players, enemies) to become a visual target without strict inheritance.
/// </summary>
public interface IEntity
{
    /// <summary>
    /// Description: Display name of the entity.
    /// Context: Used by UI systems to show what the player is looking at.
    /// Justification: Essential for user feedback so they know exactly which object they are focusing on.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Description: Priority level for detection (higher means more important).
    /// Context: When multiple entities are in view, the vision system targets the one with the highest priority.
    /// Justification: Necessary to prevent the player's head from glitching between multiple overlapping targets.
    /// </summary>
    public int PriorityLevel { get; set; }

    /// <summary>
    /// Description: Reference to the Unity GameObject.
    /// Context: Needed to get spatial coordinates and attach visual effects.
    /// Justification: Since this is an interface, we must explicitly expose the underlying GameObject reference for Unity's transform operations.
    /// </summary>
    GameObject gameObject { get; }

    /// <summary>
    /// Description: The specific point where other entities should look when focusing on this one.
    /// Context: Used by LookAt algorithms (like IK or procedural animation).
    /// Justification: An entity's origin is often at its feet, so this transform allows targeting a specific point (e.g., the face or the center of an item).
    /// </summary>
    Transform LookAtPoint { get; }
}
