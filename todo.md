# Project TODO List

## Previous Feature: Voice Visuals & Audio Enhancements
- [x] Create Tomodachi "Babble" mode for VacuumAudioController.
- [x] Refactor `MicVolumeLogger` to animate local player mouth scale.
- [x] Implement vacuum bypass to force max mouth scale during aspiration.

## Current Feature: Multiplayer Voice Mouth Animation (Unified)
- [x] Refactor `MicVolumeLogger.cs` into a unified `MouthAnimator.cs`.
- [x] Implement `isLocalPlayer` logic to switch between Microphone Input and AudioSource Output.
- [x] Use `AudioSource.GetOutputData()` for remote players to sync mouth with heard audio.
- [x] Expose `IsVacuuming` in `PlayerVacuumController` and use it for the bypass on all clients.
- [x] Update the Player prefab with the new `MouthAnimator` script.
- [x] Document the script in the `documentation/` folder.

## Current Feature: Multiplayer Lobby Customization
- [x] Create `PlayerCustomization` for syncing color and root note via Mirror `[SyncVar]`.
- [x] Create `LobbyCustomizationUI` to handle Color Hex and Enum Note inputs.
- [x] Use `PlayerPrefs` to seamlessly transfer customization data from Lobby Prefab to Gameplay Prefab.
- [x] Support offline local previews (no network authority) in the main menu for immediate visual feedback.
- [x] Update `MouthAnimator` to support Lobby Preview dummies reading local mic instantly.
- [x] Update `VacuumAudioController` to use `.SetUpdate(true)` for DOTween to bypass lobby paused time scales.
