# Vacuum Aspiration System

The Vacuum system is the core mechanic of the game. It allows players to "inhale" objects and manipulate the environment.

## Logic
- **Trigger**: The aspiration is activated when both **Left Click** and **Right Click** are held simultaneously.
- **Input**: Managed by `PlayerInputHandler` and detected by `PlayerVacuumController`.

## Audio System
The vacuum sound is designed to be highly customizable per player, adding a unique and "fun" auditory identity to each robot.

### Customizable Parameters
These parameters will eventually be set in the Lobby and synchronized across the network:
- **Base Frequency**: The pitch of the vacuum motor.
- **Timbre**: The harmonic richness of the sound.
- **Resonance**: Feedback intensity of the filter.
- **Filter Type**: (Low-pass, Band-pass, etc.) to shape the sound.

### Implementation
- `VacuumAudioController`: Manages the `AudioSource` and applies real-time modulation based on the player's custom settings.
- Uses `OnAudioFilterRead` or dynamic parameter updates to create a synthesizer-like feel.

## Files
- `Assets/1_Scripts/Player/Controller/PlayerVacuumController.cs`
- `Assets/1_Scripts/Audio/VacuumAudioController.cs`
