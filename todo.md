# Project TODO List

## Current Status: Restructuring & Documentation Setup
- [x] Translate all French comments to English (B1).
- [x] Add XML `<summary>` tags to all scripts.
- [x] Reorganize `Assets/1_Scripts` folder structure.
- [x] Create initial `documentation/` folder and feature-specific `.md` files.
- [x] Update global developer rules in Antigravity.

## Next Feature: Control Refactor & Vacuum Aspiration
- [x] Refactor `PlayerPhysicsMovement` into modular components (`PlayerInputHandler`, `PlayerMovement`, `PlayerLook`, `PlayerJump`).
- [x] Implement `PlayerVacuumController` triggered by Dual Click.
- [x] Implement `VacuumAudioController` with modulated parameters (Freq, Resonance, etc.).
- [x] Update documentation to reflect new component architecture.

## Feature: Voice Visuals & Audio Enhancements
- [x] Create Tomodachi "Babble" mode for VacuumAudioController.
- [x] Refactor `MicVolumeLogger` to animate local player mouth scale.
- [x] Implement vacuum bypass to force max mouth scale during aspiration.
