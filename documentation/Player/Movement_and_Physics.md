# Movement and Physics

The player movement system has been refactored into modular components to improve maintainability and allow for more complex interactions.

## Architecture
The system follows a **Component-Based** design:
1. **PlayerInputHandler**: The single point of entry for Unity Input System callbacks. It exposes input state (Move, Look, Jump, Sprint, Arms) to other components.
2. **PlayerMovementComponent**: Handles horizontal Rigidbody physics, acceleration, and speed clamping.
3. **PlayerLookComponent**: Handles mouse-look for both the camera (pitch) and the robot body (yaw).
4. **PlayerJumpComponent**: Handles vertical impulses and applies custom gravity multipliers for a snappy feel.
5. **PlayerController**: The core component that manages networking lifecycle, camera activation, and shared player state (like `ConnectionId`).

## Vacuum Aspiration
A new "Aspiration" feature is triggered by holding both **Left Arm** (Mouse Left) and **Right Arm** (Mouse Right) buttons.
- **Logic**: Managed by `PlayerVacuumController`.
- **Audio**: Controlled by `VacuumAudioController`, featuring customizable parameters for frequency and filtering to give each player a unique sound.

## Related Files
- `Assets/1_Scripts/Player/Controller/PlayerController.cs`
- `Assets/1_Scripts/Player/Controller/PlayerInputHandler.cs`
- `Assets/1_Scripts/Player/Controller/PlayerMovementComponent.cs`
- `Assets/1_Scripts/Player/Controller/PlayerLookComponent.cs`
- `Assets/1_Scripts/Player/Controller/PlayerJumpComponent.cs`
- `Assets/1_Scripts/Player/Controller/PlayerVacuumController.cs`
- `Assets/1_Scripts/Audio/VacuumAudioController.cs`
