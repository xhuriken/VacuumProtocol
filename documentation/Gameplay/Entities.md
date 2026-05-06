# Entities and Gameplay

This feature defines the core interactions between world objects and the player's perception system.

## Principle
The project uses a standard `IEntity` interface to make any object "detectable". This allows the player's eye-tracking and priority system to interact with disparate objects (other players, items, etc.) in a unified way.

## Related Files
- `Assets/1_Scripts/Core/IEntity.cs`: The base interface for all detectable objects.
- `Assets/1_Scripts/Gameplay/Collectible.cs`: A sample implementation of a detectable item.

---

## File Details

### IEntity.cs
**Context:** Code interface.
**Usage:** Implemented by any script that represents a physical target in the world.

#### Properties
- `Name`: The display name of the object.
- `PriorityLevel`: Used by the `PlayerViewRange` to determine which object is the most "interesting" to look at.
- `gameObject`: Reference to the Unity object.
- `LookAtPoint`: A specific Transform where the eye should point (e.g., the center of a character or the lens of a camera).

### Collectible.cs
**Context:** Attached to world items (cubes, power-ups, etc.).

#### Variables
- `Name`: Set to "Collectible" by default.
- `PriorityLevel`: Set to 2 by default (higher than players which might be 1).
- `LookAtPoint`: Returns its own transform.
