# Visual Polish

This feature adds interactive visual behaviors to the player model, such as eye-tracking and wheel orientation.

## Principle
The system enhances immersion by making the robot model react to the environment. The "Eye" tracks nearby entities, the "ViewRange" detects those entities, and the "Wheels" rotate to match the movement direction.

## Related Files
- `Assets/1_Scripts/Player/Visuals/Eye.cs`: Controls the eye's rotation towards targets.
- `Assets/1_Scripts/Player/Visuals/PlayerViewRange.cs`: Detects entities in a vision cone.
- `Assets/1_Scripts/Player/Visuals/Wheels.cs`: Orients wheels based on velocity.
- `Assets/1_Scripts/Player/Utilities/ModelMigrator.cs`: Editor tool for switching 3D models.

---

## File Details

### Eye.cs
**Context:** Attached to the "Eye" bone or object of the robot model.
**Usage:** Follows the target detected by `PlayerViewRange`.

#### Variables
- `_rotationSpeed`: Speed of the smooth rotation (Slerp).
- `_initialLocalRotation`: Caches the "forward" rotation at Start.
- `_targetLocalRotation`: The rotation calculated to face a target.

#### Functions
- `CalculateTargetRotation()`: Determines the rotation needed to face the highest priority entity, relative to the parent transform.
- `ApplyRotation()`: Smoothly interpolates the current rotation towards the target.

### PlayerViewRange.cs
**Context:** Attached to the player's head or camera reference.

#### Variables
- `_viewDistance`: Maximum detection range.
- `_viewAngle`: Field of view (FOV) cone.
- `_entityLayer`: Layer for objects implementing `IEntity`.
- `_highestPriorityEntity`: The "best" target currently in view.

#### Functions
- `DetectEntities()`: Uses `Physics.OverlapSphere` followed by angle and line-of-sight checks to find targets.
- `UpdatePriority()`: Sorts detected targets by their `PriorityLevel`.

### WheelSteering.cs (Wheels.cs)
**Context:** Attached to the robot root or a wheel controller object.

#### Variables
- `_wheels`: List of transforms (bones) to rotate.
- `_steeringSpeed`: How fast wheels align with movement.
- `_minVelocityThreshold`: Minimum speed required to update orientation.

#### Functions
- `UpdateWheelOrientation()`: Uses the parent's Rigidbody velocity to determine movement direction and rotates wheels on their local Y-axis to match it.

### ModelMigrator.cs
**Context:** Editor-only utility script.
**Usage:** Button-triggered migration from an old hierarchy to a new one.

#### Functions
- `PerfectMigration()`: Instantiates the new model, copies non-mesh components, moves manual objects (lights/cameras), and updates script references by name mapping.
