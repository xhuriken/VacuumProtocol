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
- [x] Implement `UIColorsPalettes.cs` for 16-button layout with DOTween animations and client-side Hex color selection.
- [x] Refactor `UIColorsPalettes.cs` to use editable Unity `Color` array, implement Odin `[Button]` generator for a 16-color quantized gradient, and convert color to Hex at runtime.

## Architecture & Guidance: Custom Shape Buttons
- [x] Analyze pointer events (hover, leave, click) for non-standard UI GameObjects with custom Shapes.
- [x] Provide a comprehensive, high-quality, production-ready solution utilizing an invisible raycast target and EventSystem handlers.
- [x] Document the technical architecture in the development log and explain how to wire the custom Shape properties (like scale, position, and colors) using DOTween.

## Current Feature: Custom Vector Shape UI Toolkit (Freya Holmér Shapes)
- [x] Create `UICustomButtonBase.cs` to retrieve and expose standard UGUI pointer events as a reusable toolkit.
- [x] Create `MouseManager.cs` using the Unity New Input System to safely track mouse screen coordinates.
- [x] Create `ColorButtonUI.cs` inheriting from `UICustomButtonBase` with dual `Shapes.Rectangle` properties (Outline, Plain).
- [x] Implement DOTween-driven outline width/height responsive morph animations (base 75, multiplier scales).
- [x] Move magnetic proximity vector mathematics directly into `ColorButtonUI.cs` for pristine KISS design.
- [x] Smoothly translate the local offset of the plain inner shape based on mouse proximity.
- [x] Refactor `UIColorsPalettes.cs` to color and bind to custom `ColorButtonUI` instances.
- [x] Add automated transparent Graphic/Image safety-net generators and diagnostic logging systems to guarantee EventSystem operation.
- [x] Support Camera.main projection inside proximity checks to ensure perfect world-space coordinates alignment.
- [x] Implement smooth local auto-centering snap translation of plain inner shape on hover enter, and resume drift on leave.
- [x] Apply comprehensive KISS refactoring to simplify equations and drop redundant epsilon bounds comparisons.








