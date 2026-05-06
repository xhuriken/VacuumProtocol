# Movement and Physics

This feature handles the physical movement of the robot character, designed for a "heavy yet responsive" feel.

## Principle
Movement is entirely **Rigidbody-based** to allow for realistic interactions with the environment. It uses high acceleration forces and custom gravity multipliers to create a snappy movement style while maintaining the feeling of mass.

## Related Files
- `Assets/1_Scripts/Player/Controller/PlayerMovement.cs`: The core physics-based FPS controller.

---

## File Details

### PlayerPhysicsMovement.cs
**Context:** Attached to the main Gameplay Player prefab (Mecha).
**Usage:** Only active for the local player. Remote clones have their physics disabled or set to kinematic.

#### Variables
- `_accelerationForce`: The force applied when moving. High value (150+) for weight.
- `_maxSpeed`: Horizontal speed limit.
- `_gravityMultiplier`: Increases gravity during descent (snappy jumping).
- `_decelerationDamping`: Linear damping for quick stops.
- `ConnectionId`: Used to link this object to networking/audio systems.

#### Functions
- `OnStartLocalPlayer()`: Enables the local camera, input system, and physics interpolation.
- `FixedUpdate()`: Applies movement forces and custom gravity.
- `ApplyMovementPhysics()`: Calculates the direction based on input and applies `AddForce`. Clamps velocity to `_maxSpeed`.
- `ApplyCustomGravity()`: Adds extra downward force if the robot is falling.
- `OnMove()` / `OnLook()` / `OnJump()`: Callbacks from the Unity Input System.
