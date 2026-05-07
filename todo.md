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
