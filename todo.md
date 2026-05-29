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
- [x] Solve compilation blocks in `Blit.cs` to allow registration of the ScriptableRendererFeature under URP.
- [x] Create subclassed `CustomTextButton.cs` exposing LeftLine, Rect, Dots, and text components with complete override animation hooks.
- [x] Implement smooth DOTween animations inside CustomTextButton for Line collapses, Rect fades, Dots translation, Febucci relaunching, click punches, white scintillation shimmers, and full anti-spam cancellation.
- [x] Redesign click sequence to feature ultra-fast white bloom (0.04s), instant blackout (0.07s), and high-frequency holographic flickering return with total spam-clicking protection.
- [x] Map leftward shift to Rectangle (-20f) and shift TextMeshPro -20f to the left alongside Dots.
- [x] Animate Rectangle DashOffset morphing from 0.3 to 0.2 on hover enter, and back to 0.3 on hover exit.
- [x] Reposition click scintillation effect to execute on PointerDown (instant press action) rather than release click.
- [x] Prevent scale aggregation and stuck issues on spam-clicking by replacing `DOPunchScale` with an explicit `DOScale` target sequence inside the unified `_clickFlashSequence`.
- [x] Query, cache, and animate parent `Disc` and child `Disc` components in the `Dots` hierarchy for a premium hover expansion and pointer down shockwave/scintillation burst.
- [x] Implement a continuous orbital 360° rotation loop on `Dots` and yoyo breathing sizes on children on hover enter, with smooth return to 0° alignment on exit.
- [x] Hyper-accelerate click animations for extreme snappy feedback: rectangle scales in **0.03s** (to 1.15x), color blooms in **0.02s**, and child discs burst outward to **2.2x** in **0.03s**, snapping back in **0.12s**.
- [x] Restructure and organize the entire `CustomTextButton.cs` script using standard `#region` blocks with 100% functional parity.
- [x] Build robust `Interactable` property state management inside `UICustomButtonBase.cs` to dynamically control and block pointer event invocations when disabled.
- [x] Implement smooth 0.25s translucent grey deactivated fade animations inside `CustomTextButton.cs` for text, rectangles, and disc shapes, with seamless re-enabling transition logic.
- [x] Refactor `LobbyController.cs` to declare `StartGameButton` as a `UICustomButtonBase` instead of standard UGUI `Button`, integrating the custom deactivated state system.


















