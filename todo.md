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

## Current Task: Robot Vacuum Schematic Analysis
- [x] Analyze and decompose the "Robot Vacuum" schematic image.
- [x] Document the structural components, degrees of freedom, and look-target constraints.
- [x] Update the DEVELOPMENT_LOG.md and todo.md.

## Current Task: Create Head and Vision Mechanics Documentation
- [x] Create `documentation/Player/Head_and_Vision_Mechanics.md` with detailed explanations of head physical movement (arc of circle, physics reaction, bend factor) and hierarchical vision alignment percentages.
- [x] Update DEVELOPMENT_LOG.md and todo.md.

## Current Task: Refine Head and Vision Specs
- [x] Update `documentation/Player/Head_and_Vision_Mechanics.md` to clarify the mouse-piloted camera, spring-rod head physical model ("boing boing"), and updated hierarchy alignments.
- [x] Update DEVELOPMENT_LOG.md and todo.md.

## Current Task: Clarify Unity Physics Implementation for Head Spring
- [x] Update `documentation/Player/Head_and_Vision_Mechanics.md` to describe the setup using Rigidbody and ConfigurableJoint components, explaining how targetRotation and targetPosition drive the "boing boing" effect and the crouch.
- [x] Update DEVELOPMENT_LOG.md and todo.md.

## Current Task: Create Base PhysicalHeadController Script
- [x] Create `Assets/1_Scripts/Player/Controller/PhysicalHeadController.cs` with explicit visibilities, Allman brackets, private `_camelCase` variables, and complete XML documentation.
- [x] Update DEVELOPMENT_LOG.md and todo.md.

## Current Task: Implement Runtime Unparenting for Head Physics
- [x] Update `PhysicalHeadController.cs` to unparent the head at Start to resolve transform conflicts and handle cleanup when the parent player is destroyed.
- [x] Update DEVELOPMENT_LOG.md and todo.md.

## Current Task: Implement Head and Body Collision Ignoring
- [x] Update `PhysicalHeadController.cs` to dynamically find and ignore all collisions between the head's collider and the body/player colliders at Start.
- [x] Update DEVELOPMENT_LOG.md and todo.md.

## Current Task: Correct Neck Arc Joint Translation
- [x] Update `PhysicalHeadController.cs` to use positive Y offsets and negative Z offsets directly for targetPosition, matching Unity's inverted joint space.
- [x] Update DEVELOPMENT_LOG.md and todo.md.

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

## Current Feature: Simplify Arm Targeting (KISS)
- [x] Remove horizontal spread, lateral separation, and pointing divergence from `PlayerArmsController.cs`.
- [x] Update `ApplyArmReachingForces` to pull the hand Rigidbody directly to the center line of the player's look direction, making aiming and spitting much simpler and more intuitive.
- [x] Verify compilation status.
- [x] Update `DEVELOPMENT_LOG.md` with implementation details.

## Current Feature: Local VAD Filter for Mouth Animator
- [x] Expose local `SimpleVad` instance as a static property in `UniVoiceMirrorSetupSample.cs`.
- [x] Update `MouthAnimator.cs` to check `UniVoiceMirrorSetupSample.LocalVad.IsSpeaking` for the local player's mouth animation.
- [x] Run compilation checks to ensure everything builds without errors.
- [x] Update `DEVELOPMENT_LOG.md` and `todo.md` to mark tasks as completed.

## Current Feature: Settings Manager System
- [x] Create `SettingsData.cs`, `ISettingsConsumer.cs`, and `SettingsManager.cs` under `Assets/1_Scripts/Core/Settings/`.
- [x] Create `VoiceSettingsConsumer.cs` under `Assets/1_Scripts/Audio/` to bridge micro, VAD sensitivity, master volume, and peer-to-peer volumes.
- [x] Create `InputSettingsConsumer.cs` under `Assets/1_Scripts/Player/Controller/` to bridge input rebinding.
- [x] Create `SettingsUIPresenter.cs` under `Assets/1_Scripts/UI/` to manage sliders, dropdowns, and live micro RMS levels.
- [x] Verify compilation status.
- [x] Update `DEVELOPMENT_LOG.md` and `todo.md` to mark tasks as completed.

## Current Feature: Modular UI Page Navigation System
- [x] Create `UIPanelController.cs`, `UINavigationGroup.cs`, and `InGameMenuController.cs` under `Assets/1_Scripts/UI/` to manage page transition logic.
- [x] Set up DOTween animations for panels with SetUpdate(true) safety for paused states.
- [x] Document the UI Navigation System layout, component roles, and installation guide in `UI_Navigation_System.md`.
- [x] Add note to `Voice_System.md` and `Settings_System.md` justifying why `UniVoiceMirrorSetupSample` is locally copied.
- [x] Run compilation checks to ensure everything builds successfully.
- [x] Update `DEVELOPMENT_LOG.md` and `todo.md` to mark tasks as completed.

## Current Task: IDE Configuration & Autocomplete Repair
- [x] Fix self-reference DLL reference bug in `ProjectGeneration.cs` project generator.
- [x] Clean up conflicting C# / Unity extensions (`csharp`, `vstuc`, `clover-unity`) to remove LSP and utility overlaps.
- [x] Update global `settings.json` for Visual Studio C# aesthetics (Visual Studio 2022 Dark theme, Cascadia Code font with ligatures, Ctrl + molette zoom, Quick Suggestions, Enter suggestion commit, Tab completion).
- [x] Run local compilation verification to ensure all project assemblies build without CS0121 errors.
- [x] Upgrade dotnet 10 SDK to 10.0.301 to check if runtime mismatch was resolved (MSBuild still had incompatibility in preview assemblies).
- [x] Install stable dotnet 9 SDK (9.0.315) and update workspace `settings.json` to load it. This resolves the `Microsoft.Build.Shared.XMakeElements` TypeInitializationException completely.
- [x] Dynamically name the external script editor entries in `AntigravityScriptEditor.cs` to show both "Antigravity" and "Antigravity IDE" in Unity preferences.
- [x] Re-install the Clover extension (`november.clover-unity`) to restore the "1 meta reference", "Unity Script", "Unity Serialized Field" annotations above classes/fields.
- [x] Create a `global.json` file in the workspace root to pin the SDK to stable `9.0.315` and prevent preview .NET 10 compilation blocks.
- [x] Configure the runtimeconfig.json of DotRush (version 26.6.179) to target stable .NET 9.0 (tfm: net9.0, version: 9.0.17), bypassing .NET 10 preview runtime MSBuild crash bugs.
- [x] Remove the wildcard `'*'` activation warning in the Unity integration package.json.

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

## Current Task: Robot Vacuum Schematic Analysis
- [x] Analyze and decompose the "Robot Vacuum" schematic image.
- [x] Document the structural components, degrees of freedom, and look-target constraints.
- [x] Update the DEVELOPMENT_LOG.md and todo.md.

## Current Task: Create Head and Vision Mechanics Documentation
- [x] Create `documentation/Player/Head_and_Vision_Mechanics.md` with detailed explanations of head physical movement (arc of circle, physics reaction, bend factor) and hierarchical vision alignment percentages.
- [x] Update DEVELOPMENT_LOG.md and todo.md.

## Current Task: Refine Head and Vision Specs
- [x] Update `documentation/Player/Head_and_Vision_Mechanics.md` to clarify the mouse-piloted camera, spring-rod head physical model ("boing boing"), and updated hierarchy alignments.
- [x] Update DEVELOPMENT_LOG.md and todo.md.

## Current Task: Clarify Unity Physics Implementation for Head Spring
- [x] Update `documentation/Player/Head_and_Vision_Mechanics.md` to describe the setup using Rigidbody and ConfigurableJoint components, explaining how targetRotation and targetPosition drive the "boing boing" effect and the crouch.
- [x] Update DEVELOPMENT_LOG.md and todo.md.

## Current Task: Create Base PhysicalHeadController Script
- [x] Create `Assets/1_Scripts/Player/Controller/PhysicalHeadController.cs` with explicit visibilities, Allman brackets, private `_camelCase` variables, and complete XML documentation.
- [x] Update DEVELOPMENT_LOG.md and todo.md.

## Current Task: Implement Runtime Unparenting for Head Physics
- [x] Update `PhysicalHeadController.cs` to unparent the head at Start to resolve transform conflicts and handle cleanup when the parent player is destroyed.
- [x] Update DEVELOPMENT_LOG.md and todo.md.

## Current Task: Implement Head and Body Collision Ignoring
- [x] Update `PhysicalHeadController.cs` to dynamically find and ignore all collisions between the head's collider and the body/player colliders at Start.
- [x] Update DEVELOPMENT_LOG.md and todo.md.

## Current Task: Correct Neck Arc Joint Translation
- [x] Update `PhysicalHeadController.cs` to use positive Y offsets and negative Z offsets directly for targetPosition, matching Unity's inverted joint space.
- [x] Update DEVELOPMENT_LOG.md and todo.md.

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

## Current Feature: Simplify Arm Targeting (KISS)
- [x] Remove horizontal spread, lateral separation, and pointing divergence from `PlayerArmsController.cs`.
- [x] Update `ApplyArmReachingForces` to pull the hand Rigidbody directly to the center line of the player's look direction, making aiming and spitting much simpler and more intuitive.
- [x] Verify compilation status.
- [x] Update `DEVELOPMENT_LOG.md` with implementation details.

## Current Feature: Local VAD Filter for Mouth Animator
- [x] Expose local `SimpleVad` instance as a static property in `UniVoiceMirrorSetupSample.cs`.
- [x] Update `MouthAnimator.cs` to check `UniVoiceMirrorSetupSample.LocalVad.IsSpeaking` for the local player's mouth animation.
- [x] Run compilation checks to ensure everything builds without errors.
- [x] Update `DEVELOPMENT_LOG.md` and `todo.md` to mark tasks as completed.

## Current Feature: Settings Manager System
- [x] Create `SettingsData.cs`, `ISettingsConsumer.cs`, and `SettingsManager.cs` under `Assets/1_Scripts/Core/Settings/`.
- [x] Create `VoiceSettingsConsumer.cs` under `Assets/1_Scripts/Audio/` to bridge micro, VAD sensitivity, master volume, and peer-to-peer volumes.
- [x] Create `InputSettingsConsumer.cs` under `Assets/1_Scripts/Player/Controller/` to bridge input rebinding.
- [x] Create `SettingsUIPresenter.cs` under `Assets/1_Scripts/UI/` to manage sliders, dropdowns, and live micro RMS levels.
- [x] Verify compilation status.
- [x] Update `DEVELOPMENT_LOG.md` and `todo.md` to mark tasks as completed.

## Current Feature: Modular UI Page Navigation System
- [x] Create `UIPanelController.cs`, `UINavigationGroup.cs`, and `InGameMenuController.cs` under `Assets/1_Scripts/UI/` to manage page transition logic.
- [x] Set up DOTween animations for panels with SetUpdate(true) safety for paused states.
- [x] Document the UI Navigation System layout, component roles, and installation guide in `UI_Navigation_System.md`.
- [x] Add note to `Voice_System.md` and `Settings_System.md` justifying why `UniVoiceMirrorSetupSample` is locally copied.
- [x] Run compilation checks to ensure everything builds successfully.
- [x] Update `DEVELOPMENT_LOG.md` and `todo.md` to mark tasks as completed.

## Current Task: IDE Configuration & Autocomplete Repair
- [x] Fix self-reference DLL reference bug in `ProjectGeneration.cs` project generator.
- [x] Clean up conflicting C# / Unity extensions (`csharp`, `vstuc`, `clover-unity`) to remove LSP and utility overlaps.
- [x] Update global `settings.json` for Visual Studio C# aesthetics (Visual Studio 2022 Dark theme, Cascadia Code font with ligatures, Ctrl + molette zoom, Quick Suggestions, Enter suggestion commit, Tab completion).
- [x] Run local compilation verification to ensure all project assemblies build without CS0121 errors.
- [x] Upgrade dotnet 10 SDK to 10.0.301 to check if runtime mismatch was resolved (MSBuild still had incompatibility in preview assemblies).
- [x] Install stable dotnet 9 SDK (9.0.315) and update workspace `settings.json` to load it. This resolves the `Microsoft.Build.Shared.XMakeElements` TypeInitializationException completely.
- [x] Dynamically name the external script editor entries in `AntigravityScriptEditor.cs` to show both "Antigravity" and "Antigravity IDE" in Unity preferences.
- [x] Re-install the Clover extension (`november.clover-unity`) to restore the "1 meta reference", "Unity Script", "Unity Serialized Field" annotations above classes/fields.
- [x] Create a `global.json` file in the workspace root to pin the SDK to stable `9.0.315` and prevent preview .NET 10 compilation blocks.
- [x] Configure the runtimeconfig.json of DotRush (version 26.6.179) to target stable .NET 9.0 (tfm: net9.0, version: 9.0.17), bypassing .NET 10 preview runtime MSBuild crash bugs.
- [x] Remove the wildcard `'*'` activation warning in the Unity integration package.json.

## Current Feature: Local VAD Loopback & Teardown Cleanup
- [x] Expose `SettingsManager.HasInstance` and add `_isQuitting` safety flag to block GameObject spawning on teardown.
- [x] Update all settings consumers (`VoiceSettingsConsumer`, `InputSettingsConsumer`, `SettingsUIPresenter`) to check `HasInstance` before unregistering or saving on destruction.
- [x] Implement `LocalLoopbackFilter` to preview microphone audio after VAD processing but before Concentus Opus encoding.
- [x] Expose static toggling API `VoiceSettingsConsumer.SetLocalLoopback(enabled)` to control local preview playback.
- [x] Add UI Toggle `_micTestToggle` in `SettingsUIPresenter` and link it to trigger loopback preview dynamically.
- [x] Verify compilation status.
- [x] Update `DEVELOPMENT_LOG.md` and `todo.md` to mark tasks as completed.

## Current Feature: VAD Precision & Responsiveness Optimization
- [x] Reduce the VAD Release hangover timer (`ReleaseMs`) to 300ms and `NoDropWindowMs` to 200ms inside `VoiceSettingsConsumer.cs` to ensure voice cuts are fast and snappy.
- [x] Adjust the sensitivity mapping range from wide 2..32 dB SNR to a highly precise 2..18 dB SNR to spread the settings evenly across the slider.
- [x] Implement Peak-Hold (Instant-Attack, Slow-Decay) meter logic in `SettingsUIPresenter.cs` to eliminate visual lag/smoothing on transient peaks.
- [x] Map the visual level bar logarithmically to the live SNR (Signal-to-Noise Ratio) in dB, utilizing dynamic reflection to match the exact 2..18 dB SNR range of the sensitivity slider.
- [x] Run compilation checks.
- [x] Log modifications in `DEVELOPMENT_LOG.md`.


## Current Task: Complete Codebase Audit & Documentation (Phase 1)
- [x] Audit Core Systems (`IEntity.cs`, `ISettingsConsumer.cs`, `SettingsData.cs`, `SettingsManager.cs`)
- [x] Audit Audio Systems (`MouthAnimator.cs`, `UniVoiceMirrorSetupSample.cs`, `UniVoicePlayerAudio.cs`, `VacuumAudioController.cs`, `VoiceSettingsConsumer.cs`)
- [x] Update `DEVELOPMENT_LOG.md` for Phase 1
- [x] Update documentation (`Voice_System.md`, `Settings_System.md`)

## Current Task: Complete Codebase Audit & Documentation (Phase 2)
- [x] Audit Gameplay Systems (`Collectible.cs`)
- [x] Audit Physics Systems (`ProceduralTubePhysics.cs`, `VacuumSuctionZone.cs`)
- [x] Update `DEVELOPMENT_LOG.md` for Phase 2
- [x] Update documentation (`Entities.md`, `Procedural_Systems.md`)

## Current Task: Complete Codebase Audit & Documentation (Phase 3: Player Systems)
- [x] Audit Controller scripts (`InputSettingsConsumer.cs`, `PhysicalHeadController.cs`, `PlayerArmsController.cs`, `PlayerController.cs`, `PlayerInputHandler.cs`, `PlayerInventory.cs`, `PlayerJumpComponent.cs`, `PlayerLookComponent.cs`, `PlayerMovementComponent.cs`, `PlayerVacuumController.cs`)
- [x] Audit Visual scripts (`Eye.cs`, `PlayerCustomization.cs`, `PlayerViewRange.cs`, `Wheels.cs`)
- [x] Update `DEVELOPMENT_LOG.md` for Phase 3
- [x] Update documentation (`Movement_and_Physics.md`, `Head_and_Vision_Mechanics.md`, `Vacuum_System.md`, `Visual_Polish.md`)

## Current Task: Complete Codebase Audit & Documentation (Phase 4: Networking)
- [x] Audit Lobby scripts (LobbyController.cs, LobbyCustomizationUI.cs, PlayerListItem.cs, PlayerObjectController.cs, SteamLobby.cs, SteamManager.cs)
- [x] Audit Manager scripts (MyNetworkManager.cs)
- [x] Update DEVELOPMENT_LOG.md for Phase 4
- [x] Update documentation (Network_and_Lobby.md)

## Current Task: Complete Codebase Audit & Documentation (Phase 5: UI Systems)
- [x] Audit Vector UI scripts (ColorButtonUI.cs, CustomTextButton.cs, UICustomButtonBase.cs, UIColorsPalettes.cs)
- [x] Audit Menu/Navigation scripts (InGameMenuController.cs, UIPanelController.cs, UINavigationGroup.cs, OpenURLButton.cs)
- [x] Audit Utility/Mouse scripts (CustomCursorFollower.cs, MouseManager.cs, PlayerVolumeSlider.cs, SettingsUIPresenter.cs)
- [x] Update DEVELOPMENT_LOG.md for Phase 5
- [x] Update documentation (UI_System.md)

## Current Feature: Key Rebinding System
- [x] Create implementation plan and obtain user approval.
- [x] Implement interactive rebinding callbacks and cancel options in `InputSettingsConsumer.cs`.
- [x] Create `RebindRowUI.cs` to handle individual key rows in UI.
- [x] Create `ControlRebindUIPresenter.cs` to orchestrate keybinding UI.
- [x] Implement Escape key back navigation support inside `UINavigationGroup.cs`.
- [x] Fix NullReferenceException on CustomTextButton when deactivated on start.
- [x] Verify functionality (key mapping, escaping, resetting to defaults).
- [x] Update `DEVELOPMENT_LOG.md` and `todo.md` tasks.

## Current Feature: Codebase Folder Reorganization
- [x] Create folder structure for scripts, physics, and input.
- [x] Move physic materials to Assets/6_Physics/ and input settings to Assets/Input/.
- [x] Move script files and their associated .meta files to their respective target folders.
- [x] Perform compilation checks and clean up empty folder structures.
- [x] Update DEVELOPMENT_LOG.md and todo.md to mark tasks as completed.

## Current Feature: Key Rebinding Mouse Input & Concurrent Lock Fix
- [x] Implement single-row lock in ControlRebindUIPresenter to prevent concurrent rebinding clicks.
- [x] Expose IsListening state in RebindRowUI and integrate presenter lock checks on rebind click.
- [x] Refactor InputSettingsConsumer to allow mouse button rebinds (Left/Right Click) while excluding position/delta/scroll.
- [x] Add one-frame delay in InputSettingsConsumer using a Coroutine to prevent the initiating mouse click from auto-triggering the rebind.
- [x] Verify build compiles and functions without errors.
- [x] Update DEVELOPMENT_LOG.md and todo.md to mark tasks as completed.

## Current Feature: Custom Shapes-Based Toggle Component & Settings UI Integration
- [x] Create UICustomToggle.cs script using Shapes and DOTween.
- [x] Modify SettingsUIPresenter.cs to use UICustomToggle.
- [x] Run compilation checks to ensure 0 build errors.
- [x] Update DEVELOPMENT_LOG.md and todo.md to mark tasks as completed.

## Current Feature: Custom Shapes-Based Toggle Raycast Target Fix
- [x] Add Awake validation to UICustomToggle.cs to automatically attach transparent Image for click raycasts.
- [x] Verify build compiles and functions without errors.
- [x] Update DEVELOPMENT_LOG.md and todo.md to mark tasks as completed.

## Current Feature: VoiceSettingsConsumer Log Gating & Image Raycast Clarification
- [x] Add _enableDebugLogs field, static Instance property, and log gating inside VoiceSettingsConsumer.cs.
- [x] Verify build compiles cleanly with zero compile errors.
- [x] Update DEVELOPMENT_LOG.md and todo.md to mark tasks as completed.

## Current Feature: Custom Shapes-Based Toggle Hover & Color Customizations
- [x] Add _handleOnColor, _handleOffColor, and _trackHoverHeightOffset parameters in UICustomToggle.cs.
- [x] Store original track height and animate track height on hover enter/exit.
- [x] Shift color animations to the handle/disc rather than the track background.
- [x] Fix default offset value to be suited for uGUI pixels (default to 25f).
- [x] Verify build compiles and functions without errors.
- [x] Update DEVELOPMENT_LOG.md and todo.md to mark tasks as completed.

## Current Feature: Custom Shapes-Based Toggle Handle Center Alignment Fix
- [x] Cache initial handle local X position on Start as the center point.
- [x] Calculate ON/OFF positions relative to initial handle X position.
- [x] Verify build compiles and functions without errors.
- [x] Update DEVELOPMENT_LOG.md and todo.md to mark tasks as completed.

## Current Feature: Custom Shapes-Based Slider Integration (Menu Paramètres)
- [x] Create UICustomSlider.cs implementing mouse dragging and support for modular handle-only or fill-only overlays.
- [x] Modify SettingsUIPresenter.cs to use UICustomSlider for volume, sensitivity, and mic indicators.
- [x] Add UICustomSlider.cs compile target to Assembly-CSharp.csproj.
- [x] Verify build compiles cleanly with zero compilation errors.
- [x] Update DEVELOPMENT_LOG.md and todo.md to mark tasks as completed.
- [x] Change track and fill shape components from Rectangle to Line in UICustomSlider.cs.
- [x] Adapt UICustomSlider.cs logic to use Line.Thickness and compute Line.Start/Line.End dynamically based on RectTransform.rect.
- [x] Synchronize handle position calculation with the exact Line/RectTransform coordinate space to resolve offset issues.
- [x] Verify build compiles cleanly with zero compilation errors.
- [x] Update DEVELOPMENT_LOG.md and todo.md to mark tasks as completed.

## Current Feature: Custom Shapes-Based Toggle Track Background Rect
- [x] Add _trackBackground Rectangle field and original height caching in UICustomToggle.cs.
- [x] Update hover height animations in UICustomToggle.cs to affect both _track and _trackBackground.
- [x] Verify build compiles cleanly with zero compilation errors.
- [x] Update DEVELOPMENT_LOG.md and todo.md to mark tasks as completed.

## Current Feature: Custom Shapes-Based Slider Hover & Drag Polish (Resolving TODOs)
- [x] Add _handleDragBloomMultiplier and _handleHoverRadiusMultiplier fields in UICustomSlider.cs.
- [x] Implement IPointerUpHandler and handle caching/resetting original color and radius.
- [x] Animate handle radius on pointer enter/exit, and handle bloom (color factor) on pointer down/up.
- [x] Verify build compiles cleanly with zero compilation errors.
- [x] Update DEVELOPMENT_LOG.md and todo.md to mark tasks as completed.

## Current Feature: Custom Shapes-Based Toggle Hover & Transition Bloom
- [x] Add _handleHoverRadiusMultiplier and _handleTransitionBloomMultiplier fields to UICustomToggle.cs.
- [x] Cache original handle radius on Start in UICustomToggle.cs.
- [x] Implement handle radius animations on hover (PointerEnter/Exit) and handle bloom flash sequence on toggle value transition.
- [x] Verify build compiles cleanly with zero compilation errors.
- [x] Update DEVELOPMENT_LOG.md and todo.md to mark tasks as completed.

## Current Feature: Custom Shapes-Based Slider Disabled State (Grey out)
- [x] Fix compile typo "e sois grisé." at the end of UICustomSlider.cs.
- [x] Add _disabledTrackColor, _disabledFillColor, and _disabledHandleColor fields to UICustomSlider.cs.
- [x] Cache original track and fill colors in Start(), and update interactable property setter to trigger UpdateInteractableVisuals().
- [x] Verify build compiles cleanly with zero compilation errors.
- [x] Update DEVELOPMENT_LOG.md and todo.md to mark tasks as completed.

## Current Feature: Custom Shapes-Based ListView / ScrollView
- [x] Create implementation plan and wait for user approval.
- [x] Create `UICustomScrollbar.cs` supporting variable track sizes and directional setups (horizontal/vertical).
- [x] Create `UICustomScrollView.cs` to wrap Unity `ScrollRect` and coordinate custom scrollbars via `LateUpdate`.
- [x] Write XML Comments and conform to Microsoft CoreFX C# Standards.
- [x] Update DEVELOPMENT_LOG.md and TODO.md to log tasks as completed.

## Current Feature: Custom Shapes-Based Simple Button
- [x] Create implementation plan and wait for user approval.
- [x] Create `UICustomSimpleButton.cs` script supporting auto size-sync, hover dash rotation, outward thickness growth, and click pulse/bloom.
- [x] Run compilation checks.
- [x] Update DEVELOPMENT_LOG.md and todo.md to log tasks as completed.

## Current Feature: Rebind Row UI Custom Button Integration
- [x] Refactor `RebindRowUI.cs` to accept custom button components (`UICustomButtonBase`) instead of standard UGUI buttons.
- [x] Run compilation checks.
- [x] Update DEVELOPMENT_LOG.md and todo.md to log tasks as completed.

## Current Feature: UI Shape Size Synchronization Helper
- [x] Create `UIShapeSizeSync.cs` generic helper component.
- [x] Run compilation checks.
- [x] Update DEVELOPMENT_LOG.md and todo.md to log tasks as completed.

## Current Feature: Auto VAD Settings Serialization
- [x] Add `_isAutoVad` serialized field and public `IsAutoVad` property to `SettingsData.cs`.
- [x] Modify `VoiceSettingsConsumer.cs` to synchronize `IsAutoVad` on update and write changes to `SettingsManager`.
- [x] Verify project compilation and successful build.
- [x] Update DEVELOPMENT_LOG.md and todo.md to log tasks as completed.

## Current Feature: Control Rebind UI Custom Reset Button & Simple Button Polish
- [x] Refactor `ControlRebindUIPresenter.cs` to use `UICustomButtonBase` instead of standard UGUI `Button` for the reset button.
- [x] Polish `UICustomSimpleButton.cs` to fix color and dotted outline stuck bugs when spamming clicks/hovers.
- [x] Implement smooth 0.2s DOTween transition animation for the dash spacing of the dotted border (from 0 to current spacing, and vice versa).
- [x] Add `OnEnable()` visual reset lifecycle wrapper in `UICustomSimpleButton.cs` to ensure fresh state initialization.
- [x] Fix stuck hover bug when buttons are disabled by resetting `_isHovered` in `UICustomButtonBase.Interactable` and clearing hover visual variables in `UICustomSimpleButton.AnimateInteractableTransition`.
- [x] Allow click animations to play fully on rebind rows by removing the `Interactable = false` lock on the active button in `RebindRowUI.cs`.
- [x] Optimize `RebindRowUI.RefreshDisplay()` using a local text string cache to only set text when it changes, preventing typewriter animations from re-triggering on all rows.
- [x] Fix hover-to-white color reset bug on active button text by removing the text color overwrite from `KillActiveTweens()` in `UICustomSimpleButton.cs`.
- [x] Verify project compilation and successful build.
- [x] Update DEVELOPMENT_LOG.md and todo.md to mark tasks as completed.

## Current Feature: Custom Shapes-Based Dropdown
- [x] Create implementation plan and wait for user approval.
- [x] Create `UICustomDropdownItem.cs` with hover background color animation and typewriter support.
- [x] Create `UICustomDropdown.cs` with simple button matching hover animations, open/close animations, option population, and item binding.
- [x] Modify `SettingsUIPresenter.cs` to declare `_microphoneDropdown` as a `UICustomDropdown` instead of standard `TMP_Dropdown`.
- [x] Update `Assembly-CSharp.csproj` to include the new script compilation targets.
- [x] Verify project compilation and successful build.
- [x] Update DEVELOPMENT_LOG.md and todo.md to mark tasks as completed.

## Current Feature: Custom Shapes-Based Toggle Handle Loading Fix
- [x] Create implementation plan and wait for user approval.
- [x] Implement lazy initialization in `UICustomToggle.cs` to cache original handle offset safely.
- [x] Run compilation checks.
- [x] Update DEVELOPMENT_LOG.md and todo.md to mark tasks as completed.

## Current Feature: Custom Dropdown Editor Support
- [x] Create implementation plan and wait for user approval.
- [x] Serialize `_options` and `_value` in `UICustomDropdown.cs`.
- [x] Implement `OnValidate()` visual synchronization in `UICustomDropdown.cs`.
- [x] Protect runtime-only dependencies under editor execution context.
- [x] Create custom inspector script `UICustomDropdownEditor.cs` with validation check messages.
- [x] Add editor menu creation item `GameObject/UI/Shapes-Based Dropdown`.
- [x] Verify project compilation and successful build.
- [x] Update DEVELOPMENT_LOG.md and todo.md to mark tasks as completed.

## Current Feature: Custom Dropdown Premium Visuals & Dotted Hover Outlines Fix
- [x] Refactor header outline border and list outline border to be visible by default as solid borders.
- [x] Implement invisible-to-visible dotted outlines on dropdown items upon mouse hover.
- [x] Isolate item background color changes to trigger only when clicked, using a smooth DOTween transition.
- [x] Implement setup-time background highlight of the active selected item.
- [x] Remove fullscreen blocker and implement lightweight Update click-outside detection to prevent UGUI layering bugs.
- [x] Revert runtime dynamic safety-net from item code to follow KISS principles, and document layout config requirements for the user.
- [x] Update menu builder to instantiate, format, and assign the new item outline vector.
- [x] Implement configurable dropdown chevron arrow morph animation between closed and open vector coordinates with independent X and Y size parameters.
- [x] Add arrow parent position Y translation based on a configurable offset.
- [x] Add arrow parent, offset, and independent X/Y size fields to custom editor class and draw them inside "Animation Settings" Foldout.
- [x] Remove obsolete GameObject menu creator utility from UICustomDropdownEditor to keep editor code clean (KISS).
- [x] Verify project compilation and successful build.
- [x] Expose ModelRenderer property in PlayerCustomization.cs.
- [x] Create PlayerBoneBridge.cs runtime dynamic bone mapping component.
- [x] Create PlayerBoneBridgeEditor.cs custom inspector with validators and auto-detect follower configs.
- [x] Implement 3-bone thickness preserving scaling mechanism in MouthAnimator.cs with configurable multipliers.
- [x] Implement remote player look pitch SyncVar/Command replication in PlayerLookComponent.cs.
- [x] Implement kinematic wheel estimated velocity calculations in Wheels.cs.
- [x] Update DEVELOPMENT_LOG.md and todo.md to mark tasks as completed.

