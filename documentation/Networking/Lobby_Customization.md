# Lobby Customization System

## Overview
The Lobby Customization System allows players to select their robot's color and vacuum root note in the lobby before the game starts. It utilizes Mirror for networking and `PlayerPrefs` for persistence, ensuring choices carry over gracefully between the offline lobby dummy and the actual networked gameplay prefab.

## Core Scripts

### 1. `LobbyCustomizationUI.cs`
- **Purpose**: Acts as the UI bridge attached to the Lobby Canvas.
- **Features**:
  - Handles UI inputs for Color (Hex String) and Root Note (Enum int).
  - Uses the new Unity `InputSystem` to detect simultaneous Left and Right mouse clicks for live-testing the vacuum sound in the lobby.
  - Automatically saves all selected options to the local user's `PlayerPrefs`.
  - Supports linking directly to an offline Dummy Preview Player in the scene.

### 2. `PlayerCustomization.cs`
- **Purpose**: Attached to the Player Prefab (both Lobby Dummy and Gameplay).
- **Features**:
  - Contains `[SyncVar]` properties for `PlayerColor` and `PlayerRootNote`.
  - Uses `OnStartLocalPlayer()` to automatically read `PlayerPrefs` upon spawning and syncs those values with the server using `[Command]`s.
  - **Offline Fallback**: Contains safe wrapper methods (`RequestColorChange`, `RequestNoteChange`) that will instantly apply the visual/audio updates locally if the script detects it does not have network authority (e.g., when acting as an offline Dummy in the main menu).

## Integrations

- **`VacuumAudioController.cs`**: Updated to use `.SetUpdate(true)` for DOTween to bypass `Time.timeScale` pauses in the lobby menu. Moved `_audioSource.Play()` to `Start()` for improved reliability when switching scenes.
- **`MouthAnimator.cs`**: Added an `IsLobbyPreviewDummy` bool flag. When set to true on the dummy prefab, it explicitly listens to the local microphone input without waiting for Mirror initialization or `isLocalPlayer` confirmation.
