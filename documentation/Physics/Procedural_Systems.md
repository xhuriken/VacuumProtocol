# Procedural Systems

This feature handles complex physical structures that are generated or configured at runtime.

## Principle
The system automates the setup of physics-heavy components that would be tedious to configure manually in the editor. Currently, it focuses on creating softbody-like behavior for tubes or arms using a chain of physics constraints.

## Related Files
- `Assets/1_Scripts/Physics/ProceduralTubePhysics.cs`: Automates the creation of a softbody-like chain of joints.

---

## File Details

### ProceduralTubePhysics.cs
**Context:** Attached to the root of a bone/transform hierarchy.
**Usage:** Used in the editor (Odin Inspector buttons) to generate components.

#### Variables
- `segmentMass`: Mass assigned to each Rigidbody in the chain.
- `stiffness`: The spring force (positionSpring) of the joints.
- `tipStiffnessMultiplier`: Increases stiffness at the end of the chain to prevent the tip from being too floppy.
- `angularLimit`: Limits how much each joint can bend.

#### Functions
- `Setup()`: Clears existing physics and recursively adds `Rigidbody`, `CapsuleCollider`, and `ConfigurableJoint` to every child in the hierarchy.
- `Clear()`: Removes all physics-related components from children.
- `SetupRecursive()`: Configures the capsule colliders to automatically align with the distance to the next child and locks linear motion while limiting angular motion.
