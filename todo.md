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

## Current Feature: One-Click URL Button Redirect
- [x] Create `OpenURLButton.cs` in `Assets/1_Scripts/UI/` to handle opening browser URLs.
- [x] Implement automatic caching and binding to standard UGUI `Button` components.
- [x] Adhere to Microsoft CoreFX styling, explicit member visibilities, and full XML comments.
- [x] Insert strategic `Debug.Log` for visual observability inside Unity.
- [x] Update `DEVELOPMENT_LOG.md` with implementation details.
- [x] Perform validation signature and compilation check on properties.

## Current Feature: Physical Multiplayer Arm Reaching (Procedural Joints)
- [x] Create `PlayerArmsController.cs` in `Assets/1_Scripts/Player/Controller/` to manage arm physics.
- [x] Implement Network SyncVars and Commands to replicate arm extension state across clients.
- [x] Implement automatic hierarchy traversal to find the last segment (hand/nozzle) of each arm.
- [x] Calculate total arm length dynamically to define accurate maximum reaching distance.
- [x] Add PD/Spring forces and alignment torque to target hand towards head/look direction.
- [x] Ensure natural fallback (gravity/joints rest state) when arms are released.
- [x] Update `DEVELOPMENT_LOG.md` with architectural implementation notes.
- [x] Perform compilation checks and validation signature checks.

## Current Feature: Real-time Reaching Tweaking parameters for PlayerArms
- [x] Add serialized real-time tweakable parameters (spread, height, angle offset, forward reach factor) in `PlayerArmsController.cs`.
- [x] Update `ApplyArmReachingForces` implementation to calculate target positions and target orientations using these properties in FixedUpdate.
- [x] Adhere to Microsoft CoreFX styling, explicit member visibilities, and full XML comments.
- [x] Update `DEVELOPMENT_LOG.md` with implementation details.
- [x] Run verification compile check.

## Current Feature: Vacuum Physics Suction, Object Shrinking, and Player Inventory
- [x] Create `VacuumableObject.cs` in `Assets/1_Scripts/Physics/` to tag and cache original scale on vacuumable objects.
- [x] Create `PlayerInventory.cs` in `Assets/1_Scripts/Player/Controller/` to manage storage, LIFO collection, and spawning/spitting over Mirror.
- [x] Create `VacuumSuctionZone.cs` in `Assets/1_Scripts/Physics/` to apply pull forces, process distance-based shrinking, and trigger local absorption.
- [x] Rewrite `PlayerVacuumController.cs` to orchestrate input states, trigger zone activation, and host Network Command handlers for absorption and spitting.
- [x] Update `PlayerArmsController.cs` to expose public read-only hand and extension state properties.
- [x] Verify compilation status and explicit member visibility signatures.
- [x] Update `DEVELOPMENT_LOG.md` with implementation logs.

## Current Feature: Merge Collectible and VacuumableObject
- [x] Merge the fields, properties, and methods of `VacuumableObject` into `Collectible`.
- [x] Update `Collectible` to implement required components (like `Rigidbody`).
- [x] Update references in `PlayerInventory.cs` and `VacuumSuctionZone.cs` to use `Collectible` instead of `VacuumableObject`.
- [x] Delete `VacuumableObject.cs`.
- [x] Run compile checks and verify all references compile correctly.
- [x] Update `DEVELOPMENT_LOG.md`.

## Current Feature: Fix Right Arm Extension and Mouth Vacuum Input Logic
- [x] Fix `FindLastChild` in `PlayerArmsController.cs` to return the deepest node containing a `Rigidbody` component, resolving the issue where children without Rigidbodies (like `VacuumSuctionZone`) broke arm extension physics.
- [x] Restore `_isVacuuming` audio and animation states in `PlayerVacuumController.cs` to trigger only when both arms are active (`_input.IsVacuuming`), separating it from right click individual arm extension.
- [x] Verify compilation status.
- [x] Update `DEVELOPMENT_LOG.md` with detailed description of the fixes.

## Current Feature: Spit and Mouth Vacuum Constraints
- [x] Prevent arm extensions from functioning/reaching when both left and right clicks are pressed (`IsVacuuming` state), making it only trigger the mouth vacuum visuals and audio.
- [x] Implement waiting mechanics for spitting: wait until the left arm is physically extended (reaches 80% target distance) before launching the item, with a 0.25-second timeout fallback.
- [x] Lower default spit force in `PlayerInventory.cs` to 15f for gentler, more natural shooting.
- [x] Run compilation checks.
- [x] Update `DEVELOPMENT_LOG.md` with implementation details.
