# Development Log

## [2026-07-21] - Lobby Texture Editor Feature (Tomodachi Style)

### Feature Added
- **Core Texture Painting Engine (`TexturePainter.cs`)**: Non-UI drawing engine operating directly on raw `Color32[]` pixel buffers with dynamic texture dimensions ($W \times H$). Supports:
  - **Bresenham Interpolation**: Connects consecutive drag points to ensure smooth, gap-free strokes during fast mouse movements.
  - **Brush Tools**: Hard Pencil, Soft Brush (radial falloff gradient), Airbrush (stochastic spray), Eraser (background restore/clear), Flood Fill (BFS queue bucket fill), and Eyedropper (color sampler).
- **Snapshot History Engine (`TextureUndoSystem.cs`)**: Memory-efficient Undo/Redo stack manager storing pixel array snapshots with configurable step bounds.
- **SSOT Custom Cursor Integration (`CustomCursorFollower.cs`)**: Extended existing single-source-of-truth custom cursor follower with `SetBrushCursorMode()`. Canvas hover dynamically switches standard UI cursor graphics into a *Shapes* vector brush ring matching the active tool diameter, color, or eraser indicator.
- **UI Presenter (`TexturePainterUI.cs`)**: Receives Unity UGUI pointer events on canvas `RawImage`, transforms local RectTransform screen points to exact UV pixel coordinates, and updates SSOT cursor visual dimensions.
- **Lobby Studio Control Panel (`TextureEditorPanelUI.cs`)**: Integrates custom tool selection, project `ColorButtonUI` color buttons, `UICustomSlider` for brush size control, and Undo/Redo/Clear/Save action buttons.

### Code Modified/Added
- **Created `Assets/1_Scripts/UI/TextureEditor/Core/BrushData.cs`**: Defines `PainterTool` enum and `BrushSettings` container class.
- **Created `Assets/1_Scripts/UI/TextureEditor/Core/TextureUndoSystem.cs`**: Implements snapshot stacks for memory-friendly Undo/Redo operations.
- **Created `Assets/1_Scripts/UI/TextureEditor/Core/TexturePainter.cs`**: Implements core pixel drawing algorithms, Bresenham line rendering, and flood fill.
- **Implemented Opacity Blending on Flood Fill (`TexturePainter.cs`)**: Refactored `PerformFloodFill()` to support opacity blending using `Color32.Lerp(targetColor, fillColor, opacity)`. Now, bucket filling regions blends the new color cleanly with the target background color based on active opacity slider settings.
- **Dynamic Tool-Specific Slider Visibility (`TextureEditorPanelUI.cs`)**: Refactored `SetTool` to show/hide sliders contextually:
  - Pencil/SoftBrush/Eraser: Shows Size & Opacity.
  - Airbrush: Shows Size, Opacity & Density.
  - FloodFill: Shows Opacity (hides Size as it has no radius).
  - Eyedropper: Hides all sliders (locked to default single-pixel size).
- **Added Airbrush Spray Density Slider (`TextureEditorPanelUI.cs`)**: Created a dedicated `_brushDensitySlider` for the Airbrush tool. The slider is dynamically shown (`SetActive(true)`) only when the Airbrush tool is selected.
- **Implemented Tool-Specific Persistent Settings (`TextureEditorPanelUI.cs`, `UICustomSlider.cs`)**: Designed brush-specific memory and storage. Every brush now maintains independent settings for Size, Opacity, and Spray Density. When switching tools, the sliders automatically transition to their saved settings. To prevent UI lag, PlayerPrefs persistence is triggered only on pointer release (`onPointerUp` event added to `UICustomSlider.cs`).
- **Fixed Collapsed Slider Track Layout Timing Bug (`UICustomSlider.cs`)**: Added fallback handling to `UpdateVisuals()` in `UICustomSlider.cs`. If the UGUI layout pass hasn't completed on initialization, it forces canvas layout update or reads `sizeDelta.x` to prevent the track geometry from collapsing to zero width.
- **Added Brush Opacity Slider (`BrushData.cs`, `TextureEditorPanelUI.cs`)**: Expanded brush settings with a dynamic `Opacity` property (`0.0` to `1.0`) and mapped it to a new `_brushOpacitySlider` control in the editor panel.
- **Implemented Non-Accumulating Blending Mask (`TexturePainter.cs`)**: Introduced `_strokeStartBuffer` (Color32 texture snapshot) and `_strokeAlphaBuffer` (opacity coverage tracking layer) initialized at `BeginStroke`. During a single mouse drag stroke, stamp alphas are combined using `Mathf.Max` rather than additive accumulation. Prevents paint buildup on slow mouse speeds and ensures perfectly uniform opacity coverage regardless of drag speed.
- **Modified `Assets/1_Scripts/UI/Core/UICustomButtonBase.cs`**: Refactored `Interactable` setter to perform an instant physical hover check using `RectTransformUtility.RectangleContainsScreenPoint` and `MouseManager.Instance.MousePosition` when the button is re-enabled. Resolves EventSystem limitation where disabled buttons did not receive exit events.
- **Modified `Assets/1_Scripts/UI/Components/UICustomSimpleButton.cs`**: Removed `_buttonText.DOKill()` from `KillActiveTweens()`. Prevents hover exit transitions from instantly killing text color fade tweens, correcting the bug where button text remained greyed out.
- **Modified `Assets/1_Scripts/UI/Components/UICustomSimpleButton.cs` and `CustomTextButton.cs`**: Implemented hover state synchronization inside `AnimateInteractableTransition(true)`. When a button is re-enabled, it immediately evaluates the physical `IsHovered` check and transitions visual states to `AnimateHoverEnter` or `AnimateHoverExit` automatically.
- **Fixed Stroke Drawing Interruptions (`TexturePainterUI.cs`)**: Refactored `OnDrag` to check `!_painter.IsStrokeActive` and dynamically resume drawing via `BeginStroke` upon mouse drag re-entry into the canvas, resolving the issue where drawing broke if the mouse temporarily exited the canvas.
- **Modified `Assets/1_Scripts/UI/Components/UIColorsPalettes.cs`**: Added generic `OnColorSelected` and `OnHexColorSelected` UnityEvents to decouple color palette selection, allowing the same `UIColorsPalettes` component to drive TextureEditor, Player Customization, or any menu cleanly via Observer pattern.
- **Fixed Brush Cursor Precision Math (`TexturePainterUI.cs`)**: Calculated exact sub-pixel screen radius `uiRadius = (brush.Radius + 0.5f) * uiPixelSize`. Ensures 100.0% exact alignment between the custom cursor ring visual and painted pixels regardless of RawImage UI scaling or texture resolution.

### Technical Justification & Details
- **Responsive Layout Architecture**: Replaced arbitrary static pixel offsets with normalized anchor ranges and UGUI layout groups (`HorizontalLayoutGroup`, `VerticalLayoutGroup`, `GridLayoutGroup`). The drawing canvas enforces a 1:1 ratio using `AspectRatioFitter` so the painting surface stays square regardless of screen size.
- **TextMeshPro Integration**: All section headers and button labels utilize `TextMeshProUGUI` for ultra-crisp vector typography matching project design standards.
- **Auto-Aligning Button Containers**: Tools and color buttons are placed inside auto-wrapping grid containers with `GridLayoutGroup` and `ContentSizeFitter`, allowing endless tool and color button additions without breaking panel alignment.
- **Dynamic Resolution Flexibility**: Canvas resolution is specified dynamically at initialization (`InitializeCanvas(width, height)`) or on texture load (`LoadTexture(Texture2D)`). This allows painting small 64x64 pupil textures, 128x128 player avatar icons, or large player body UV maps with the exact same codebase.
- **SSOT Cursor Consistency**: Reuses the existing `CustomCursorFollower.cs` component rather than creating a secondary mouse follower script, ensuring single source of truth for mouse tracking and screen-space project alignment.
- **Non-Recursive BFS Flood Fill**: Avoids stack overflow exceptions on large texture fills by utilizing a `Queue<Vector2Int>` breadth-first algorithm.

### Accessibility/Visibility Signature Checks
- Microsoft CoreFX naming convention applied: explicit visibilities, Allman brackets, private `_camelCase` members.
- XML `/// <summary>` documentation added on all public classes, methods, and serialized fields.



### Feature Added
- **Unity Color Array Inspector Support**: Replaced hardcoded string colors with an editable `Color[]` array, making it extremely easy to tweak and preview colors directly within the Unity Inspector.
- **Odin Inspector `[Button]` Generator**: Added a customized Odin Inspector action button `GenerateQuantizedPalette()` that programmatically calculates a gorgeous 16-color quantized gradient.
- **Quantized Gradient Calculation (16-bit like)**: The generator calculates:
  - 3 Grayscale tones: Black, Medium Grey, and White.
  - 13 Rainbow hues: Evenly stepping through the HSV spectrum (Red, Orange, Yellow, Green, Cyan, Blue, Violet, Magenta, Pink).
- **Runtime Hex Syncing**: Dynamically converts chosen Unity Colors to HTML Hex values at runtime using `ColorUtility.ToHtmlStringRGB(buttonColor)` to maintain full synchronization compatibility with the multiplayer backend without altering any networking code.
- **Smooth DOTween UI Animations**: Attaches a custom micro-animation controller `UIColorsPaletteButtonAnimator` to each button, handling dynamic hovering, click scaling, and snappy bouncy pop feedback.

### Code Modified/Added

#### `Assets/1_Scripts/UI/UIColorsPalettes.cs`
- **Class `UIColorsPalettes`**: Implements dynamic Unity Color processing, Sirenix Odin Inspector integration, automated HSV-based color quantization, loop-variable capture safety, and runtime event generation.
- **Class `UIColorsPaletteButtonAnimator`**: Handles `IPointerEnterHandler`, `IPointerExitHandler`, `IPointerDownHandler`, `IPointerUpHandler`. Animates button scaling with `.SetUpdate(true)` to support responsive rendering even when `Time.timeScale` is paused in menus.

### Technical Justification & Details
- **Safety from loop variable capture**: Capturing the current loop index or item inside a delegate/lambda expression in C# leads to closure bugs if not assigned to a local variable within the scope of the iteration (`string capturedHex = hexColor;`).
- **Ease of Setup (KISS)**: Developers do not need to manually configure DOTween or animation components on 16 individual buttons in the Unity Inspector. The main controller automatically scans and applies script attachments dynamically in `Start()`.
- **Modern Unity APIs**: Uses `FindAnyObjectByType` instead of the deprecated `FindObjectOfType` to achieve optimal scene query performance.
- **Responsive Menu Rendering**: Configured all DOTween scaling with `.SetUpdate(true)` to guarantee smooth UI feedback regardless of lobby game state pause scales.

### Accessibility/Visibility Signature Checks
- All private/public member access levels are explicitly declared (`private string[] _hexColors`, `private void Start()`, etc.) to prevent compilation errors and comply with style standards.
- Script namespaces properly import `UnityEngine.UI`, `UnityEngine.EventSystems`, `DG.Tweening`, and `VacuumProtocol.Networking.Lobby`.

## [2026-05-26] - Architecture Guidance: Custom Shape Buttons in Unity UI

### Feature Added
- **Invisible Raycast Target Pattern**: Described the industry-standard architecture for interactive UI components containing non-standard graphics (e.g., custom Vector Shapes).
- **EventSystem Custom Handlers**: Outlined pointer interface implementations (`IPointerEnterHandler`, `IPointerExitHandler`, `IPointerDownHandler`, `IPointerUpHandler`, `IPointerClickHandler`) to drive sub-children animations without relying on standard `Button` visual transitions.

### Technical Justification & Details
  - Decoupled Visuals and Interactions: Standard `Button` components require a single `Image` as `targetGraphic` for transitions. By setting the `Button` transition to `None` and placing an invisible `Image` (alpha = 0) with `raycastTarget = true` on the parent, we decouple the collision/interaction area from the complex visual shapes underneath.
  - Custom Event Handlers: Implementing standard Unity UGUI event interfaces on custom scripts allows driving complex multi-child animations (scale, sub-positions, colors of nested custom shapes) using DOTween directly from pointer lifecycle callbacks.

## [2026-05-26] - Custom Vector Shape UI Toolkit (Freya Holmér Shapes)

### Feature Added
- **Base UI Pointer Toolkit (`UICustomButtonBase`)**: Extends standard MonoBehaviour and UGUI pointer interfaces (`IPointerEnterHandler`, `IPointerExitHandler`, `IPointerDownHandler`, `IPointerUpHandler`, `IPointerClickHandler`) to expose lifecycle hooks and Unity Events.
- **Global Magnetic Mouse Proximity Solver (`MouseManager`)**: A Canvas-level singleton helper script that computes screen-space distance from interactive UI elements to the mouse pointer, providing Snappy Quadratic Attenuation for magnetic attraction.
- **Lobby Color Button Custom Vector Controller (`ColorButtonUI`)**: Exposes dual `Shapes.Rectangle` properties (`Outline`, `Plain`) for Freya Holmér vector components.
- **Responsive Width/Height Morph Animations**: Performs dynamic DOTween property tweens (`DOTween.To`) targeting `Rectangle.Width` and `Rectangle.Height` on pointer enter, exit, down, and up states.
- **Dynamic Magnetic Attraction Offset**: Interpolates the local position of the inner plain shape relative to its cached original coordinate based on real-time mouse direction and proximity.

### Code Modified/Added
- **Created `Assets/1_Scripts/UI/UICustomButtonBase.cs`**: Handles fundamental pointer events and maps them to reusable `ButtonClickedEvent` UnityEvents.
- **Created `Assets/1_Scripts/UI/MouseManager.cs`**: Tracks mouse screen coordinates and offers robust vector proximity formulas.
- **Created `Assets/1_Scripts/UI/ColorButtonUI.cs`**: Subclasses `UICustomButtonBase` to animate outline bounds and plain shape offset translation.
- **Modified `Assets/1_Scripts/UI/UIColorsPalettes.cs`**: Swapped deprecated `UICustomShapeButton` arrays for unified `ColorButtonUI` references.

### Technical Justification & Details
- **Non-UGUI Graphic Compatibilities**: Custom Vector Shape tools like Freya Holmér's Shapes asset render via custom MeshRenderers and do not inherit from standard UGUI `Graphic`. This prevents standard UGUI buttons from controlling their properties directly. Custom scripts driving these properties are mandatory.
- **Property Tweening (`DOTween.To`)**: Because standard extension methods like `DOScale` target Transform parameters, custom vector properties like `Rectangle.Width` and `Rectangle.Height` must be tweened using explicit property setters to avoid stretching or pixelating shape bounds.
- **Magnetic Proximity Attenuation**: Calculated in screen space using `RectTransformUtility.WorldToScreenPoint` to ensure responsiveness across different canvas resolutions, aspect ratios, and scaling modes.
- **Spring Damped Interpolation**: Employs `Vector3.Lerp` with `Time.unscaledDeltaTime` to achieve visual spring dampening that works flawlessly during active pauses.

## [2026-05-26] - Fix Input System Compatibility & KISS Cleanups

### Feature Refactored
- **Unity New Input System Support**: Replaced references to deprecated legacy `UnityEngine.Input.mousePosition` with direct queries to the modern `UnityEngine.InputSystem.Mouse.current.position.ReadValue()` API. This resolves dynamic runtime `InvalidOperationException` errors when running under active Input System Package configurations.
- **Strict Separation of Concerns (KISS)**: Stripped procedural mathematical calculations (`CalculateMagneticPull`) out of the `MouseManager`. The global manager is now exclusively a simple, high-performance mouse coordinate reporter.
- **Localized Attraction Logic**: Moved all magnetic vector proximity queries and spring dampening logic directly inside the `ColorButtonUI` script's `Update()` loop. This encapsulates interactive visual mathematics locally on the buttons that consume them, simplifying architecture and avoiding global pollution.

### Code Modified/Added
- **Modified `Assets/1_Scripts/UI/MouseManager.cs`**: Simplified coordinates polling to leverage the `UnityEngine.InputSystem` assembly.
- **Modified `Assets/1_Scripts/UI/ColorButtonUI.cs`**: Handled screen distance calculations and spring offsets internally using the local component's parameters.

## [2026-05-27] - Inject Validation & Interactive Safety Nets

### Feature Added
- **Automated Raycast Safety Net**: UICustomButtonBase now checks for an active UGUI `Graphic` component on the GameObject. If missing or if `raycastTarget` is disabled, it dynamically adds an invisible transparent `Image` (alpha = 0, `raycastTarget = true`), ensuring UGUI pointer raycasts are correctly processed.
- **Critical Diagnostics Logging**: Added strategic `Debug.Log` statements inside pointer events (`OnPointerEnter`, `OnPointerExit`, `OnPointerDown`, `OnPointerUp`, `OnPointerClick`), Awake, and Start cycles to track exactly when and where a collision blockage occurs.
- **Scene Dependency Diagnostics**: Added explicit warning/error reporters in `Start()` to verify if `MouseManager` exists and if an `EventSystem` is missing in the scene hierarchy.
- **World Space Camera Projection Fix**: Replaced overlay-space `RectTransformUtility.WorldToScreenPoint(null, ...)` coordinate mapping with a dynamic camera-projected `Camera.main.WorldToScreenPoint(...)` conversion. This resolves coordinates-projection misalignment when buttons are rendered in the 3D/2D world space (using colliders and physics raycasters) rather than basic screen overlay.

## [2026-05-28] - Smooth Hover Snapping & Reset Integration

### Feature Added
- **Dynamic Proximity Hover Disabling**: Modified the magnetic pull calculation to automatically bypass vector tracking when `IsHovered == true`. The inner `Plain` shape smoothly slides back and snaps perfectly into its original parent local coordinate center using spring-dampened `Vector3.Lerp` interpolation when hovered, resuming interactive drift once the cursor leaves the element collision bounds.
- **KISS Math & Condition Cleanups**: Stripped redundant safety variables (`_magneticRadius > 0.001f`) since magnitude is naturally `>= 0` and standard comparisons organically bypass computation. Consolidated local variables to maximize readable, production-grade flow.

### Bug Fixes
- **Blit Script Compile Restores**: Resolved blocking compile errors in `Blit.cs` by commenting out non-standard decorative attributes (`[ShowIf]`, `[Indent]`). Removing these un-imported attributes immediately satisfies the C# compiler, restoring asset compilation and allowing `Blit` to register correctly as a ScriptableRendererFeature under the URP Forward+ settings inspector.

## [2026-05-28] - Custom Text Button Toolkit Expansion

### Feature Added
- **CustomTextButton Subclassing**: Created `CustomTextButton.cs` inheriting directly from the foundational `UICustomButtonBase` class. Exposed serialized fields for `LeftLine` (Shapes.Line), `Rect` (Shapes.Rectangle), `Dots` (GameObject), and the button text (`TextMeshProUGUI`) with robust XML documentation. Implemented clean override event hooks (`OnPointerEnter`, `OnPointerExit`, `OnPointerDown`, `OnPointerUp`, `OnPointerClick`) to provide clear visual animation placeholders for future DOTween sequences.
- **Holographic DOTween Animations**: Fully implemented premium, smooth custom vector animations inside `CustomTextButton.cs`. Designed an initial state where the Rect is invisible, LeftLine is visible, and Text is visible. Added hover tweens collapsing the LeftLine to zero and fading it out while the Rect fades in and slides 8 units to the right, and the Dots shift 20 units to the left. Relaunches the Febucci Text Animator typewriter dynamically on hover. Integrated a crisp punch scale and bright white scintillation shimmer sequence for pointer clicks. Integrated thorough `.DOKill()` active tween cancellation and automatic cleanups inside `OnDisable` to completely prevent execution overlaps or spam anomalies.

### Bug Fixes
- **SetKeepAlive Compilation Repair**: Resolved C# compilation error `CS1061` in `CustomTextButton.cs` by removing the invalid `.SetKeepAlive(true)` method call from the DOTween animation chain of `_dots.transform.DOLocalMove`. Active cancellation is fully covered by standard `.DOKill()` methods.
- **Visual Upgrades & Holographic Flicker Click**: 
  - **Leftward Rect Shift**: Corrected the translation of the `Rectangle` shape to slide to the LEFT (`-8f` units offset) instead of right on hover enter.
  - **TextMeshPro Leftward Translation**: Added caching and animation of the `TextMeshProUGUI` transform, moving it 20 units to the left matching the `Dots` translation seamlessly.
  - **Holographic Scintillation Flicker**: Redesigned the click scintillation visual effect from a boring fade into a gorgeous high-fidelity projection simulation: features a super-fast bright white bloom peak (0.04s), an instantaneous drop to complete transparency (0.07s), followed by a high-frequency flickering back-and-forth pattern settling smoothly into the normal hover color. Safe under spam-clicking due to instant sequence termination.
- **Visual Fine-tuning & Press Action Triggers**:
  - **Leftward Rect Translation (20 units)**: Enlarged the `Rectangle` offset to EXACTLY **-20 units** (`-20f`) on the X axis, ensuring a perfectly aligned translation along with the Text and the Dots.
  - **Rectangle DashOffset Morphing**: Added high-fidelity tweening to the `Rectangle.DashOffset` (dashed offset), morphing it from **0.3 to 0.2** on hover enter and restoring it to **0.3** on hover exit, creating a premium sliding effect inside the dashed line.
  - **Immediate Press Click (PointerDown)**: Mapped the visual holographic scintillation flicker to trigger immediately on **PointerDown** (press action) instead of PointerClick (release action) to deliver razor-sharp, instant tactile feedback.
  - **Spam Click Scale Stabilization**: Replaced `DOPunchScale` entirely with an explicit `DOScale` sequence integrated inside `_clickFlashSequence`. `DOPunchScale` has internal caching bugs in DOTween when interrupted/killed repeatedly, which caused compounding scaling bugs. Using explicit `DOScale` targets (`_originalRectLocalScale * 1.08f` then returning to `_originalRectLocalScale`) completely resolves any potential scale aggregation and guarantees the rectangle returns to its exact scale even under extreme click spam.
- **Dynamic Dots & Shockwave Animation (Discs)**:
  - **Dynamic Setup**: Automatically queries the main `Disc` on the `Dots` GameObject, and any child `Disc` components under it. Caches their default sizes, colors, and positions.
  - **Hover Enter (Playful Spin & Breathing)**: The parent `Dots` transform immediately starts a **continuous 360° rotation loop** (incremental Linear orbit) while the two child discs expand and start a **playful continuous breathing yoyo scale pulse (radii breathing between 1.35x and 1.6x)**. This creates a lively, high-tech orbital loader feel.
  - **Hover Exit**: Smoothly interpolates radii, positions, and colors back to their exact cached default states, while gently rotating the parent `Dots` transform back to 0° alignment.
  - **PointerDown Press (Snappy Shockwave Scintillation)**: Overhauled all durations for an ultra-fast, snappy impact:
    - **Rectangle scale explosion** spikes to **1.15x** in just **0.03s**, snapping back in **0.12s**.
    - **Color flash/bloom peak** triggers in **0.02s**, followed by a **0.04s** blackout and high-speed **0.015s** holographic flickers.
    - **Dots shockwave** expands child discs to a dramatic **2.2x** size and **0.22 units outward burst** in **0.03s**, snapping back to home in **0.12s**.
    - Complete, watertight protection against execution overlaps or scale aggregation.
- **Structural Code Cleanup & Organization**:
  - Organized the entirety of `CustomTextButton.cs` into clear, easy-to-read, standard `#region` blocks (`Serialized Fields`, `Private Fields`, `Properties`, `Unity Lifecycle Callbacks`, `EventSystem Overrides`, `Caching Helpers`, `Core Tween Animations`, `Cleanup & Safety Guards`). 
  - Retained every single feature, duration, transition, and security precaution with 100% functional parity.
- **Custom Button Interactable & Disabled Visual States**:
  - **Base Integration (`UICustomButtonBase.cs`)**: Added an `Interactable` property (backing field `_interactable`) and a virtual `OnInteractableChanged(bool isInteractable)` callback hook. Blocked all EventSystem inputs (PointerEnter, PointerExit, PointerDown, PointerUp, PointerClick) dynamically when `Interactable` is false.
  - **High-Fidelity Transitions (`CustomTextButton.cs`)**: Implemented `OnInteractableChanged` override. When disabled (`Interactable = false`), all active pointer tweens are killed and elements fade smoothly (in `0.25s`) to a gorgeous translucent grey look (text, dashed border, main/child discs). When re-enabled, they return dynamically to their respective cached idle configurations.
  - **LobbyController Migration (`LobbyController.cs`)**: Refactored the `StartGameButton` field from `UnityEngine.UI.Button` to `UICustomButtonBase`, adapting its ready check states to utilize the premium custom transition system via the `.Interactable` property.

## [2026-06-01] - One-Click URL Button Redirect

### Feature Added
- **One-Click URL Button Script (`OpenURLButton.cs`)**: Created a clean, robust, and highly reliable script designed to reside on a standard UGUI Button. It automatically binds to the button click event at Awake and runs the system browser redirect when clicked.
- **Auto-Registration & Dynamic Setup**: Requires a `Button` component, automatically caching and linking listeners at runtime. This avoids manual Inspector click binding, providing a foolproof "one-click" configuration experience.
- **Sanitized Redirects**: Performs robust string trimming (`_url.Trim()`) to remove leading/trailing carriage returns or spaces that frequently trigger system browser failure exceptions.
- **Manual Hook Support**: Exposes a clean public method `OpenConfiguredURL()` so the component can still be manually bound to standard Unity events or custom script sequences if needed.

### Code Modified/Added

#### `Assets/1_Scripts/UI/OpenURLButton.cs`
- **Class `OpenURLButton`**: Implements the automatic registration of listeners on a local standard `Button` component, sanitizes target URL values, triggers standard system browser execution, and implements strict memory logging / safety hooks.

### Technical Justification & Details
- **Foolproof Implementation (KISS)**: Designed for minimal configuration. Dropping the script onto a standard Button GameObject completely wires up the click handler with zero developer interaction needed.
- **Garbage Collection & Memory Safety**: Implements standard listener registration in `Awake` and automated un-registration inside the `OnDestroy` callback to guarantee no lingering listener reference leaks when scenes are reloaded or objects are destroyed.
- **Official API Redirects**: Utilizes `Application.OpenURL` to trigger system browser invocation. Detailed official references are available at [Unity Application.OpenURL Documentation](https://docs.unity3d.com/ScriptReference/Application.OpenURL.html).

### Accessibility/Visibility Signature Checks
- All property members and callbacks (`_url`, `_button`, `Awake()`, `OnDestroy()`, `OpenConfiguredURL()`, `HandleButtonClick()`) are explicitly declared with exact access visibility levels to satisfy compiler requirements and enforce strict standards.

## [2026-06-01] - Physical Multiplayer Arm Reaching (Procedural Joints)

### Feature Added
- **Multiplayer Arm Physical Reaching Component (`PlayerArmsController.cs`)**: Created a high-quality player controller script designed to manage physical joint-based arm movements. When the left or right click is held, the corresponding arm reaches out in the look direction of the player's head, reverting organically back to a relaxed/gravity state upon release.
- **Dynamic Hierarchy Traversal**: Automatically traverses child hierarchies of the Left and Right arm roots to locate the exact terminal node (hand or nozzle) of the physical chain.
- **Auto-Calculated Max Reach**: Measures the cumulative length of each arm segment dynamically, ensuring perfect world-space reach mapping that matches any robotic appendage structure without manual adjustments.
- **Physics Attraction (Forces & Torque)**: Computes dynamic spring-damping vector attraction forces (`AddForce`) and angular look-alignment torques (`AddTorque`) targeting the hand Rigidbody, achieving snappy, responsive, and organically stable reaching animations.
- **Mirror Multiplayer Synchronization**: Syncs inputs via `[SyncVar]` properties and client-to-server `[Command]` methods. Physics forces are simulated locally on every client for all players, providing seamless lag-free animations on remote clones.

### Code Modified/Added

#### `Assets/1_Scripts/Player/Controller/PlayerArmsController.cs`
- **Class `PlayerArmsController`**: Integrates with `PlayerInputHandler`, resolves target reach directions using localized looking components, dynamically traverses limbs to apply physics attractions to terminal nodes, and synchronizes status over Mirror.

### Technical Justification & Details
- **Procedural Joints Coexistence**: Leveraging Unity joints' native spring systems (`ProceduralTubePhysics`) allows remote/local clones to handle physical reactions (collisions, bending) automatically, making the rest-state collapse completely free and organic.
- **Mass-Relative Forces**: Multiplies computed forces and torques by target Rigidbody mass (`handRb.mass`) to guarantee identical, scale-invariant reaching responsiveness regardless of player avatar sizing.
- **Oscillation Mitigation (Damping)**: Implements precise damping coefficients for both linear and angular velocity curves to avoid high-frequency jitter during collision contact.

### Accessibility/Visibility Signature Checks
- Fully declared all access modifier levels (`private`, `public`, `protected`) across properties and methods to prevent compile blockages or reflection ambiguities in Mirror.

## [2026-06-03] - Vacuum Suction Physics, Object Shrinking, and Player Inventory

### Feature Added
- **Vacuum Physics Suction Field (`VacuumSuctionZone.cs`)**: Created a trigger volume component placed on the Right Hand that applies progressive target attraction forces to any physics-enabled Rigidbody marked with the new component.
- **Dynamic Proportional Shrinking**: Shrinks items in scale dynamically as they get closer to the nozzle tip using distance interpolation ratios. Restores the object's scale automatically if it escapes the field or if the vacuum is deactivated.
- **Networked Player Inventory (`PlayerInventory.cs`)**: Added storage capacity tracking for absorbed items on the server. Deactivates GameObjects upon absorption and keeps the item count synchronized to clients via `[SyncVar]`.
- **LIFO Spit/Launch Mechanics**: Allows spitting stored inventory items forward from the Left Hand tip nozzle (initial left click press). Restores their original scale and applies a strong physical impulse force to the object's Rigidbody.
- **Unified Controller Orchestration (`PlayerVacuumController.cs`)**: Integrates inventory, arm extensions, and trigger zone activation under Mirror networking, with instant local-client deactivation for latency-free item pickup.

### Code Modified/Added

#### `Assets/1_Scripts/Player/Controller/PlayerVacuumController.cs`
- **Class `PlayerVacuumController`**: Rewritten to manage the Right-Hand trigger zone activation, monitor left click for projectile spits, and host `CmdAbsorbObject` / `CmdSpitItem` Commands.

#### `Assets/1_Scripts/Player/Controller/PlayerInventory.cs`
- **Class `PlayerInventory`**: New class maintaining LIFO list of GameObjects on the server and synchronizing item count to clients.

#### `Assets/1_Scripts/Physics/VacuumSuctionZone.cs`
- **Class `VacuumSuctionZone`**: New class processing trigger overlaps, pull forces, visual shrinking, and notifying the controller of local absorption.

#### `Assets/1_Scripts/Physics/VacuumableObject.cs`
- **Class `VacuumableObject`**: New marker class containing original local scale and customizable resistance factors to suction force.

#### `Assets/1_Scripts/Player/Controller/PlayerArmsController.cs`
- Exposed public read-only properties for hands (`LeftHand`, `RightHand`) and extension states (`IsLeftArmExtended`, `IsRightArmExtended`).

### Technical Justification & Details
- **Trigger Stay Physics**: Leverages `OnTriggerStay` inside Unity's physics loop to apply forces and calculate relative distance scaling factors dynamically.
- **Visual Scale Restorations**: Prevents objects from permanently shrinking by tracking scale states in an active Dictionary and resetting them inside `OnTriggerExit` and `Update` (if vacuum is deactivated).
- **LIFO Stack Mechanics**: Re-spits the most recently vacuumed item, allowing natural gameplay shooting feedback.

### Accessibility/Visibility Signature Checks
- Fully verified explicit access modifiers and XML comments for all new methods and fields.

## [2026-06-08] - Merging VacuumableObject with Collectible

### Technical Justification & Details
- **Redundancy Reduction**: Rather than maintaining a separate `VacuumableObject` marker component alongside the `Collectible` component, all suction and physical parameters are merged directly into `Collectible`.
- **Inheritance and Requirements**: `Collectible` now implements `IEntity` and requires a `Rigidbody` component, which aligns with both physical simulation and player vision focus targeting systems.
- **Reference Updates**: References to `VacuumableObject` in `PlayerInventory` and `VacuumSuctionZone` have been migrated to `Collectible` to maintain full compilation consistency.

### Code Modified/Added

#### `Assets/1_Scripts/Gameplay/Collectible.cs`
- **Class `Collectible`**: Merged the physics caching, original local scale, pull resistance settings, and `ResetScale()` methods into the existing class structure. Added XML summaries to both properties and methods.

#### `Assets/1_Scripts/Player/Controller/PlayerInventory.cs`
- Updated the collection scale reset callback to query for the `Collectible` component.

#### `Assets/1_Scripts/Physics/VacuumSuctionZone.cs`
- Updated trigger stay lists, cache queries, and dictionary types to map `Collectible` components.

### File Deletions
- Deleted `Assets/1_Scripts/Physics/VacuumableObject.cs` and `Assets/1_Scripts/Physics/VacuumableObject.cs.meta`.

### Accessibility/Visibility Signature Checks
- Verified explicit access modifiers (`private`, `public`) and complete XML summaries on all newly introduced members within `Collectible`.

## [2026-06-08] - Fixing Arm Extension and Mouth Vacuum Input Logic

### Technical Justification & Details
- **Hierarchy Search Fix**: The procedural arm reaching physics searches for the hand GameObject using a recursive child traversal (`FindLastChild`). When a child without a Rigidbody (such as `VacuumSuctionZone`'s trigger collider) was added under the hand, the search returned the child collider rather than the parent hand itself. Since the child has no `Rigidbody`, joint forces could not be applied, causing the arm to remain static. Updated `FindLastChild` to stop and return the deepest node that contains a `Rigidbody` component, falling back to the deepest child only if no Rigidbody is found in descendants.
- **Mouth Vacuum Input Decoupling**: Restored the mouth animation/audio vacuum state to evaluate `_input.IsVacuuming` (which checks if both left and right mouse click inputs are active). This separates individual right-arm suction zone activations from mouth vacuum triggering.

### Code Modified/Added

#### `Assets/1_Scripts/Player/Controller/PlayerArmsController.cs`
- **`FindLastChild`**: Updated recursive algorithm to track and return the deepest child node containing a `Rigidbody` component.

#### `Assets/1_Scripts/Player/Controller/PlayerVacuumController.cs`
- **`Update`**: Decoupled vacuum state check from `_armsController.IsRightArmExtended` and restored it to check for `_input.IsVacuuming` (both keys pressed).

## [2026-06-08] - Spit and Mouth Vacuum Constraints

### Technical Justification & Details
- **Mouth Vacuum Override**: When both clicks are pressed (`IsVacuuming`), only the mouth should perform vacuuming audio/visuals. The arms should not extend. To achieve this, the local input checks in `PlayerArmsController.Update` force the target arm extensions (`leftInput` and `rightInput`) to `false` when `_input.IsVacuuming` is true, ensuring both arms remain at rest during the mouth vacuum.
- **Physical Extension Check for Spitting**: To improve shooting game feel, the projectile spit action must wait until the left arm is physically extended. Added `IsLeftHandExtendedPhysically` property to `PlayerArmsController` which calculates the distance from the left hand to the left arm root, requiring it to reach at least 80% of target extension.
- **Blocked State Timeout Fallback**: If the player is standing close to a wall, physical constraints might prevent the arm from straight-extending. Added a 0.25-second timeout fallback since left-click press: if the arm does not reach 80% physical extension within 0.25s, it spits anyway to prevent lockup.
- **Spit Force Reduction**: Lowered default `_spitForce` from 400f to 15f in `PlayerInventory.cs` to avoid extreme physics impulse launch velocities.

### Code Modified/Added

#### `Assets/1_Scripts/Player/Controller/PlayerArmsController.cs`
- **`IsLeftHandExtendedPhysically`**: Public property returning true if the left hand has physically reached at least 80% of its target extension length.
- **`Update`**: Forced arm extension inputs to false if `_input.IsVacuuming` is active.

#### `Assets/1_Scripts/Player/Controller/PlayerVacuumController.cs`
- **`Update`**: Re-implemented spitting to check for physical extension (`_armsController.IsLeftHandExtendedPhysically`) or a 0.25s timeout after press, and disabled spitting when mouth vacuum is active.

#### `Assets/1_Scripts/Player/Controller/PlayerInventory.cs`
- Lowered default `_spitForce` to 15f.

## [2026-06-10] - Simplifying Arm Targeting (KISS)

### Technical Justification & Details
- **Aim Simplification**: Reduced layout and reach complexity by removing lateral spread, divergence angles, and horizontal offsets when calculating target arm positions. Since the character only extends one arm at a time during gameplay actions, aiming is much more natural and intuitive when the extending arm targets the exact center line of where the player is looking.
- **KISS Philosophy**: Deleted `_horizontalSpread` and `_angleSpread` fields to keep inspector interfaces cleaner and reduce unnecessary math.

### Code Modified/Added

#### `Assets/1_Scripts/Player/Controller/PlayerArmsController.cs`
- **Fields**: Removed `_horizontalSpread` and `_angleSpread`.
- **`ApplyArmReachingForces`**: Simplified the target position and target rotation calculations to pull the hand directly to the center line of vision.

## [2026-06-11] - Spécifications Techniques de la Tête et de la Vision du Robot Vacuum

### Justification Technique & Détails
- **Analyse du Schéma de Fonctionnement** : Traduction et enrichissement des spécifications de mouvement et de vision du Robot Vacuum depuis les schémas manuscrits.
- **Intégration Physique via ConfigurableJoint d'Unity** :
  - Utilisation d'un **Rigidbody** (tête) relié au buste par un **ConfigurableJoint** pour gérer nativement les collisions, chocs et forces d'inertie.
  - Configuration du `Slerp Drive` (ressorts de rotation) avec un faible amortissement (`Position Damper`) pour générer le balancement élastique ("boing boing") naturel lors des mouvements ou impacts physiques.
  - Configuration du `Y Drive` pour le déplacement vertical (Crouch) gérant l'affaissement élastique.
- **Formulation Mathématique de Guidage (targetRotation / targetPosition)** :
  - La souris pilote directement la **Caméra Verte** (100% de la direction visée).
  - La rotation de la tête suit à amplitude réduite (70%) via l'assignation de la `targetRotation` du Joint.
  - La position de la tête suit un arc de cercle et un affaissement via l'assignation de la `targetPosition` du Joint (ajoutant l'offset d'accroupissement).
- **Vision Hiérarchique Ciblée (Œil, Pupille)** :
  - L'Œil Bleu s'aligne à **75%** vers la cible et la Pupille Mauve s'aligne à **100%**, produisant un effet visuel de regard en coin très expressif.

### Code Modifié/Added
- Création du fichier de spécifications : `documentation/Player/Head_and_Vision_Mechanics.md` avec description détaillée de la configuration du Joint et script C# d'implémentation.
- Création de `Assets/1_Scripts/Player/Controller/PhysicalHeadController.cs` : classe de gestion physique de la tête avec calcul d'arc de cercle, crouch, et liaison ConfigurableJoint.
- Mise à jour de `PhysicalHeadController.cs` : implémentation du détachement hiérarchique au `Start()` via `transform.SetParent(null)` pour éliminer les conflits de double contrainte avec les animations de l'armature parent, calcul de la rotation relative par rapport au parent d'origine, et destruction automatique lors de la destruction du joueur. Ajustement avec inversion (`Quaternion.Inverse` et `-desiredOffset`) pour `targetRotation` et `targetPosition` du ConfigurableJoint suite aux spécifications internes d'Unity. Ajout de l'ignorance dynamique des collisions via `Physics.IgnoreCollision` au `Start()` entre le collider de la tête et tous les colliders du corps/bras du joueur pour éviter tout blocage physique. Correction des signes de `targetPosition` (+arcY et -arcZ) pour appliquer la translation de l'arc de cercle dans le bon sens physique.

## [2026-07-01] - Local VAD Filter for Mouth Animator

### Technical Justification & Details
- **Raw Mic Noise Jitter Issue**: The local player's mouth animation previously subscribed directly to the raw microphone stream (`IAudioInput.OnFrameReady`), which triggered *before* any filters were run. Consequently, background white noise and breathing caused the local player's mouth to jitter even when they were silent. Conversely, remote players' mouths appeared perfectly clean because remote volumes are derived from the networked stream, which is gated by the client-side Voice Activity Detector (VAD) filter.
- **Adaptive VAD Synchronization**: Exposing the local `SimpleVad` instance as a static public property in `UniVoiceMirrorSetupSample` allows other scripts to access the local client's VAD state.
- **Local Volume Gating**: Updated `MouthAnimator.cs` to check `UniVoiceMirrorSetupSample.LocalVad` and force `_lastPeak = 0f` if `LocalVad.IsSpeaking` is false. This mirrors the remote network behavior on the local client, resulting in a clean local mouth animation that only activates when actual speech is detected.

### Code Modified/Added

#### `Assets/1_Scripts/Audio/UniVoiceMirrorSetupSample.cs`
- **`LocalVad`**: Added a public static property to hold the active local `SimpleVad` instance.
- **`SetupClientSession`**: Assigned `LocalVad` during VAD initialization before adding it to `ClientSession.InputFilters`.

#### `Assets/1_Scripts/Audio/MouthAnimator.cs`
- **`SetupLocalMicLogging`**: Updated the local microphone `OnFrameReady` handler to check if `LocalVad` is active, forcing the local peak volume to `0f` when the user is not speaking.

#### `documentation/Audio/Voice_System.md`
- **Documentation Update**: Completely rewrote the document to explain how the VAD (Voice Activity Detection) algorithm works (RMS energy, EMA background noise floor estimation, SNR thresholds, attack/release timers). Explicitly differentiated between what is built-in in Mirror/UniVoice (network messaging, mic capture, base VAD/Opus filters) and what we custom-coded as a bridge/pont (dynamic 3D spatialization, mouth volume animation, local VAD gating, Steamworks lobby hosts fix).

## [2026-07-01] - Settings Manager System

### Technical Justification & Details
- **Settings State Isolation**: Designed and implemented a robust, modular, and extensible settings manager pattern. Created `SettingsData` as a POCO class acting as the Single Source of Truth (SSOT). Handled dictionary serialization inside `SettingsData` by implementing `ISerializationCallbackReceiver` to bypass Unity `JsonUtility` serialization limitations.
- **Decoupled Consumer Pipeline**: Added `ISettingsConsumer` interface allowing game components (Voice, Input, Graphics) to dynamically observe Settings changes. `SettingsManager` manages loading/saving JSON configurations from/to PlayerPrefs and routes updates to all registered consumers.
- **Audio Hot-swapping & Sensibility Bridge**: Implemented `VoiceSettingsConsumer.cs` to capture settings changes. Resolves hot-swapping by stopping previous recording devices, starting recording on the new target device, and updating `ClientSession.Input` dynamically. Uses reflection to access `SimpleVad._config` and update SNR thresholds dynamically based on user sensitivity preferences. Calculates combined peer volumes (Master * Voice * PeerMultiplier) dynamically.
- **Input System Rebinding**: Implemented `InputSettingsConsumer.cs` to apply bindings overrides onto `InputActionAsset` at load, and provides methods to trigger interactive rebinding.
- **UI Presenter and Thread-Safe Indicator**: Implemented `SettingsUIPresenter.cs` to bind UI components (Sliders, Dropdowns) to the manager. Calculates frame RMS in-place on the microphone capture thread and uses a cached float field to safely apply level changes to the UI on Unity's main thread inside `Update()`.
- **Namespace Clean-Up**: Removed namespaces from all settings scripts (`SettingsData`, `ISettingsConsumer`, `SettingsManager`, `VoiceSettingsConsumer`, `InputSettingsConsumer`, and `SettingsUIPresenter`) to prevent compilation blocks, match the project conventions, and allow global references.


### Code Modified/Added

#### `Assets/1_Scripts/Core/Settings/SettingsData.cs` [NEW]
- Holds all volume, sensitivity, input overrides, and graphics quality index. Implements `ISerializationCallbackReceiver`.

#### `Assets/1_Scripts/Core/Settings/ISettingsConsumer.cs` [NEW]
- Interface declaring `OnSettingsUpdated(SettingsData)` method.

#### `Assets/1_Scripts/Core/Settings/SettingsManager.cs` [NEW]
- Central Singleton manager for lifecycle, persistence, and event dispatching. Fixed type conversion compiler error by querying `.Name` property on target `Mic.Device`.

#### `Assets/1_Scripts/Audio/VoiceSettingsConsumer.cs` [NEW]
- Bridges volume, microphone hot-swapping, and VAD sensitivity levels dynamically.

#### `Assets/1_Scripts/Player/Controller/InputSettingsConsumer.cs` [NEW]
- Handles control rebinding overrides with the new Unity Input System.

#### `Assets/1_Scripts/UI/SettingsUIPresenter.cs` [NEW]
- Presenter script to bind UI sliders/dropdowns and render live micro RMS levels with thread-safety.

#### `Assets/1_Scripts/Audio/UniVoiceMirrorSetupSample.cs`
- Removed namespace wrapper to put the class in the global namespace. Also added `using Adrenak.UniVoice;` to restore visibility of `IAudioServer`, `ClientSession`, and `SimpleVad` which are no longer automatically visible from parent namespace nesting after removing the namespace wrapper.

#### `Assets/1_Scripts/Audio/MouthAnimator.cs`
- Removed `using Adrenak.UniVoice.Samples;` reference.

#### `Assets/1_Scripts/Audio/UniVoicePlayerAudio.cs`
- Removed `using Adrenak.UniVoice.Samples;` reference.

#### `documentation/Gameplay/Settings_System.md` [NEW]
- Technical documentation detailing the modular, extensible Settings Manager system, core component responsibilities, VAD details, and Unity Editor setup steps. Added note explaining why `UniVoiceMirrorSetupSample` is locally copied.

## [2026-07-01] - Modular UI Page Navigation System

### Technical Justification & Details
- **Reusable Prefab Architecture**: Designed a decoupled UI workflow where individual menu panels (e.g. Settings Panel, Main Menu, Pause Menu) handle their own show/hide/animation logic and remain independent. This allows the Settings Panel to be turned into a Prefab and dropped into both Main Menu and In-Game Pause canvas hierarchies without duplication.
- **Visual Panel Transitions**: Implemented `UIPanelController.cs` which manages opacity fading (`DOFade`) and scale scaling (`DOScale` using `Ease.OutBack`/`Ease.InBack`) dynamically using DOTween. Sets `SetUpdate(true)` to guarantee animations play when the game is paused (timeScale = 0).
- **Navigation Groups**: Implemented `UINavigationGroup.cs` to orchestrate mutually exclusive panels and maintain history stack tracking for back button traversal.
- **Pause Menu Key Gating**: Implemented `InGameMenuController.cs` supporting both legacy `Input.GetKeyDown` and New Input System `Keyboard.current` to capture Escape key and toggle the pause panel. If settings are open, it closes them first.

### Code Modified/Added

#### `Assets/1_Scripts/UI/UIPanelController.cs` [NEW]
- Manages show/hide lifecycle, raycast blocking, and DOTween transitions for individual canvas group panels.

#### `Assets/1_Scripts/UI/UINavigationGroup.cs` [NEW]
- Coordinates active panel swapping in a group and handles back operations.

#### `Assets/1_Scripts/UI/InGameMenuController.cs` [NEW]
- Listens for Escape key to toggle pause menus and manage nested panel visibility.

#### `documentation/Gameplay/UI_Navigation_System.md` [NEW]
- Comprehensive design document detailing the modular UI layout, component roles, code structure, and guide for editor setup.

#### `Assets/1_Scripts/UI/SettingsUIPresenter.cs`
- Added offline fallback check to `UpdateVolumeIndicator()` comparing live RMS values directly against the threshold slider value when `LocalVad` is null (e.g. in the Main Menu), allowing test/preview of color changing thresholds offline.
- Added `SettingsManager.Instance.SaveToDisk()` call inside `OnDisable()` to safely persist changes when the UI panel is closed.
- Subscribed to `VoiceSettingsConsumer.OnMicInputSwapped` to automatically re-subscribe the RMS local mic frame analyzer callback (`OnLocalMicFrameReady`) onto the newly instantiated `ClientSession.Input` device, preventing the dynamic volume indicator level from locking or stopping after a hot-swap.

#### `Assets/1_Scripts/Core/Settings/SettingsManager.cs`
- Removed synchronous `PlayerPrefs.Save()` disk flush from the hot update path (`SaveSettings()`) to prevent I/O blocking lag during active UI slider dragging.
- Added `SaveToDisk()` method and hooked it to `OnApplicationQuit()` and `OnApplicationPause()` to safely write changes to disk on application cycle events.

#### `Assets/1_Scripts/Audio/VoiceSettingsConsumer.cs`
- Added caching system (`_lastAppliedDevice`, `_lastAppliedSensitivity`, `_lastAppliedMasterVolume`, `_lastAppliedVoiceVolume`) in `OnSettingsUpdated` to completely avoid invoking costly OS audio driver list checks (`Mic.AvailableDevices`) and VAD reflection changes during slider drag ticks.
- Added `Update()` monitoring loop to automatically detect when `UniVoiceMirrorSetupSample.ClientSession` is initialized, invalidating the local cache and applying the player's saved parameters to the active VoIP session dynamically.
- Declared and triggered public static event `OnMicInputSwapped` when hot-swapping `ClientSession.Input` with a new `UniMicInput` instance.

#### `Assets/1_Scripts/Player/Controller/InputSettingsConsumer.cs`
- Added caching field (`_lastAppliedBindingsJson`) to skip redundant JSON parsing and binding overrides instantiation during slider drag updates.

#### `Assets/1_Scripts/Audio/UniVoiceMirrorSetupSample.cs`
- Updated `SetupClientSession()` to initialize using the saved `ActiveMicrophoneDevice` name from `SettingsManager.Instance.CurrentSettings` instead of defaulting statically to the first microphone index in the system.

## [2026-07-02] - IDE Configuration & Autocomplete Repair

### Technical Justification & Details
- **Self-Reference DLL Bug in Project Generator**: Fixed a compiler-blocking bug in `ProjectGeneration.cs` where assemblies (such as `Mirror.Components.csproj`) were being configured to reference their own pre-compiled DLL binaries located under `Library/ScriptAssemblies/`. This self-reference caused duplicate type definitions at compilation, generating CS0121 ambiguity errors (e.g. `'PredictedSyncDataReadWrite.ReadPredictedSyncData' is ambiguous between ...`). These compilation failures broke the Roslyn Analyzer/LSP, blocking autocomplete for Unity-specific APIs globally. Fix applied by adding `assembly.name` to `referencedNames` immediately, ensuring the generator skips adding the assembly's own compiled output as a dependency.
- **Extension Conflict Resolution**: Cleaned up the IDE's extensions directory and updated `extensions.json` and `.obsolete` list. Removed `muhammad-sammy.csharp` (conflicts with DotRush), `zlorn.vstuc` (redundant debugger bridge), and `november.clover-unity` (redundant Unity integration), leaving `nromanov.dotrush` and `antigravity-unity` as the single unified C# and Unity support stack to prevent language server conflicts and performance issues.
- **Visual Studio Aesthetics Match**: Configured global user settings in `settings.json` to match the exact aesthetics of Visual Studio C#, setting the theme to `Visual Studio Dark` (`vs-dark`), font to `Consolas`, and enabling autocomplete, parameter hints, and enter-to-commit preferences.
- **Unity External Script Editor Distinction**: Modified [AntigravityScriptEditor.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Packages/com.antigravity.ide/Editor/AntigravityScriptEditor.cs) to dynamically differentiate between "Antigravity" and "Antigravity IDE" depending on their executable paths. This allows selecting the correct executable in the Unity Preferences dropdown list.
- **Clover Extension Restoration**: Re-installed `november.clover-unity` v1.0.5 in the IDE via CLI to restore the "1 meta reference", "Unity Script", and "Unity Serialized Field" CodeLens annotations.
- **Workspace-wide SDK Pinning**: Added a [global.json](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/global.json) file at the root of the workspace to force the use of stable `.NET 9` SDK (`9.0.315`), resolving MSBuild incompatibilities on preview .NET 10 systems.
- **DotRush .NET 9 Runtime Override**: Configured the `DotRush.runtimeconfig.json` of the re-installed DotRush version `26.6.179` to target `.NET 9` (TFM `net9.0`, runtime `9.0.17`), and registered it in `extensions.json` to bypass .NET 10 preview runtime MSBuild crash bugs.
- **Package.json Activation Cleanup**: Removed the wildcard `*` from the activationEvents array in the Unity extension package.json to eliminate performance warnings in the IDE problems list.

### Code Modified/Added

#### [MODIFY] [AntigravityScriptEditor.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Packages/com.antigravity.ide/Editor/AntigravityScriptEditor.cs)
- Replaced hardcoded `EditorName` references with path checks to dynamically return `"Antigravity IDE"` or `"Antigravity"`.

#### [MODIFY] [package.json (extension)](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Packages/com.antigravity.ide/antigravity-unity-extension~/package.json)
- Removed wildcard `*` activation entry.

#### [MODIFY] [DotRush.runtimeconfig.json](file:///c:/Users/celestin/.antigravity-ide/extensions/nromanov.dotrush-26.6.179-win32-x64/extension/bin/LanguageServer/DotRush.runtimeconfig.json)
- Override `tfm` to `"net9.0"` and `version` to `"9.0.17"`.

#### [NEW] [global.json](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/global.json)
- Configured to pin .NET SDK version to `9.0.315`.

### Environment Changes
- Patched DotRush 26.6.179, forcing its execution environment to the stable .NET 9 CLR runtime, and pinned the workspace to .NET 9 using `global.json` to resolve the MSBuild loading crash.

## [2026-07-02] - Local VAD Loopback & Teardown Cleanup

### Technical Justification & Details
- **Singleton Teardown Leak Fix**: Resolved Unity warning `Some objects were not cleaned up when closing the scene. (Did you spawn new GameObjects from OnDestroy?)`. In Unity, singletons accessed inside `OnDestroy()` or `OnDisable()` during scene teardown can inadvertently instantiate a new singleton GameObject if the singleton has already been destroyed. Added a `HasInstance` property to `SettingsManager.cs` and an `_isQuitting` safety flag in the `Instance` getter. Updated all settings consumers (`VoiceSettingsConsumer.cs`, `InputSettingsConsumer.cs`, `SettingsUIPresenter.cs`) to check `HasInstance` before trying to unregister or flush settings on destruction.
- **Local Microphone Loopback (Gated Preview)**: Implemented a Discord-style local microphone test toggle. Added `LocalLoopbackFilter` implementing `IAudioFilter` to intercept PCM frames directly from the microphone after VAD processing but before Concentus Opus compression. This allows players to hear their own voice gated by the threshold value. Added `_micTestToggle` in `SettingsUIPresenter.cs` to trigger the local loopback preview dynamically.

### Code Modified/Added

#### `Assets/1_Scripts/Core/Settings/SettingsManager.cs`
- Added `HasInstance` static property and `_isQuitting` check to the `Instance` getter to block GameObject spawning during teardown.

#### `Assets/1_Scripts/Audio/VoiceSettingsConsumer.cs`
- Changed `OnDestroy()` to check `SettingsManager.HasInstance`. Implemented the nested class `LocalLoopbackFilter` and the static methods `SetLocalLoopback`, `SetupLoopbackFilter`, and `TeardownLoopbackFilter` to inject loopback preview after VAD.
- Optimized VAD sensitivity mappings in `ApplyGateSensitivity()` from a wide 2..32 dB SNR range to a more precise 2..18 dB SNR range.
- Reduced the VAD release hangover timer (`ReleaseMs`) from 1000ms to 300ms, and `NoDropWindowMs` to 200ms to ensure highly responsive, snappy voice cuts.

#### `Assets/1_Scripts/Player/Controller/InputSettingsConsumer.cs`
- Changed `OnDestroy()` to check `SettingsManager.HasInstance` before unregistering.

#### `Assets/1_Scripts/UI/SettingsUIPresenter.cs`
- Added `_micTestToggle` field and bound it to toggle the local preview audio loopback. Changed `OnDisable()` to check `SettingsManager.HasInstance` and automatically disable local preview when closing.
- Implemented **Peak-Hold (Instant-Attack, Slow-Decay)** visual meter logic in `UpdateVolumeIndicator()`. If a new audio frame peak is higher than the smoothed value, the jauge jumps to it instantly instead of being slowed down by interpolation (Lerp). The meter then decays slowly.
- Aligned visual level indicator logarithmically to display the **live Signal-to-Noise Ratio (SNR) in dB** instead of raw linear RMS. It queries the active noise floor `_noiseRms` dynamically via reflection from `SimpleVad`, computes `20 * log10(signal / noise)`, and maps the result to the precise 2..18 dB SNR range of the sensitivity slider. This ensures the voice indicator crosses the slider handle to the exact pixel whenever the noise gate opens.

## [2026-07-02] - Per-Peer Volume Slider (Lobby)

### Technical Justification & Details
- **Feature Request**: Each player in the lobby should be able to independently adjust the volume of each other player's voice (from 0% to 200%).
- **Architecture**: `SettingsData` already held a `Dictionary<int, float> PeerVolumeMultipliers` (key = Mirror `ConnectionId`) that was already serialized and already read by `VoiceSettingsConsumer.ApplyVoiceVolumes()`. However, no UI script existed to write into this dictionary.
- **Slider Range**: The slider uses `[0, 2]` (not `[0, 1]`). The float value is used directly as a multiplier in `baseVolume * peerMultiplier`. `1.0 = 100%`, `2.0 = 200%`, `0 = muted`.
- **Immediate Application**: Instead of waiting for the `SettingsManager` → `ISettingsConsumer` propagation loop (which only fires when MasterVolume or VoiceVolume changes), a new static method `VoiceSettingsConsumer.ApplyPeerVolume(int peerId, float multiplier)` directly touches the target peer's `UnityAudioSource.volume` for zero-latency feedback while the user is dragging the slider.
- **Local Player Card Handling**: When `LobbyController` instantiates a `PlayerListItem`, it checks if the card belongs to the local player and if so, disables and hides the volume slider (`volumeSlider.SetConnectionId(id, isLocalPlayer: true)`). Adjusting your own outgoing voice for yourself is meaningless.
- **Persistence**: On slider change, `SettingsManager.UpdateSettings()` writes to `PeerVolumeMultipliers[connectionId]`. On startup/reconnect, `PlayerVolumeSlider.RefreshFromSettings()` restores the slider position from saved data.

### Code Modified/Added

#### [NEW] `Assets/1_Scripts/UI/PlayerVolumeSlider.cs`
- MonoBehaviour placed on each `PlayerListItem` prefab.
- Exposes `SetConnectionId(int, bool)` to bind itself to a Mirror peer by ConnectionId.
- Listens to `onValueChanged`, persists via `SettingsManager.UpdateSettings`, and calls `VoiceSettingsConsumer.ApplyPeerVolume` for real-time volume control.
- Hides the slider entirely when `isLocalPlayer = true`.

#### [MODIFY] `Assets/1_Scripts/Audio/VoiceSettingsConsumer.cs`
- Added `public static void ApplyPeerVolume(int peerId, float multiplier)` — directly applies `baseVolume * multiplier` to the peer's `UnityAudioSource` for immediate feedback without waiting for `OnSettingsUpdated`.

#### [MODIFY] `Assets/1_Scripts/Networking/Lobby/LobbyController.cs`
- Both `CreateHostPlayerItem()` and `CreateClientPlayerItem()` now call `volumeSlider.SetConnectionId(player.ConnectionId, isLocal)` after each `PlayerListItem` instantiation.

## [2026-07-02] - Custom UI Cursor (Follower Architecture)

### Technical Justification & Details
- **Feature Request**: Hide the default system cursor and display a custom circle/disc shape cursor. The setup must avoid multi-scene canvas registration issues (e.g. cameras going missing during DontDestroyOnLoad transitions) and work cleanly in all rendering modes (Overlay and Screen Space Camera).
- **Implementation**: 
  - **Decoupled Architecture**: Upgraded `MouseManager.cs` to serve solely as a persistent global coordinator (`DontDestroyOnLoad`). It hides the default hardware cursor and exposes `ShouldShowCursor` (based on `Cursor.lockState`) and `MousePosition`.
  - **Local Follower Component**: Created `CustomCursorFollower.cs`. You can place the custom cursor prefab inside any local Canvas of any scene. The local follower reads the global `MouseManager` values, automatically adapts to the local Canvas scaler, handles camera lookups locally on the active Canvas, and handles visibility automatically.

### Code Modified/Added

#### [NEW] `Assets/1_Scripts/UI/CustomCursorFollower.cs`
- Local follower script to place on local custom cursor UI prefabs.
- Handles ScreenSpace camera-relative point translation locally, adapting instantly to any screen resolution, scale factor, or rendering mode.

#### [MODIFY] `Assets/1_Scripts/UI/MouseManager.cs`
- Stripped UI Canvas positioning logic.
- Serves as persistent, clean global mouse coordinate provider and hardware cursor suppression system.




## Codebase Audit - Phase 1: Core & Audio Systems (July 2026)
- **Modified Files**: IEntity.cs, ISettingsConsumer.cs, SettingsData.cs, SettingsManager.cs, MouthAnimator.cs, UniVoiceMirrorSetupSample.cs, UniVoicePlayerAudio.cs, VacuumAudioController.cs, VoiceSettingsConsumer.cs.
- **Why**: Enforcement of strict code standards. Missing architectural context made future maintenance risky.
- **Problem solved**: Added exhaustive XML <summary> tags detailing Description, Context, and Justification for all methods and properties. Added explicit [Tooltip] attributes with Role, Use Case, and Justification to all serialized variables to ensure clear intent directly in the Unity Inspector.


## Codebase Audit - Phase 2: Gameplay & Physics Systems (July 2026)
- **Modified Files**: Collectible.cs, ProceduralTubePhysics.cs, VacuumSuctionZone.cs.
- **Why**: Enforcement of strict code standards and improved observability for level designers.
- **Problem solved**: Added exhaustive XML <summary> tags detailing Description, Context, and Justification for all methods and properties. Added explicit [Tooltip] attributes with Role, Use Case, and Justification to all serialized variables, which is particularly critical for the heavily math-based ProceduralTubePhysics and VacuumSuctionZone scripts.


## Codebase Audit - Phase 3: Player Systems (July 2026)
- **Modified Files**: InputSettingsConsumer.cs, PhysicalHeadController.cs, PlayerArmsController.cs, PlayerController.cs, PlayerInputHandler.cs, PlayerInventory.cs, PlayerJumpComponent.cs, PlayerLookComponent.cs, PlayerMovementComponent.cs, PlayerVacuumController.cs, Eye.cs, PlayerCustomization.cs, PlayerViewRange.cs, Wheels.cs, ModelMigrator.cs.
- **Why**: Ensure standard practices across the heavily-populated player control domain. Complex networked input handlers require thorough explanation.
- **Problem solved**: Added XML summaries (Description, Context, Justification) and Tooltips (Role, Use Case, Justification) to demystify complex procedural physics (Arms/Head) and network sync logic (Vacuum/Customization). This reduces onboarding time for developers modifying player mechanics.


## Codebase Audit - Phase 4: Networking (July 2026)
- **Modified Files**: MyNetworkManager.cs, SteamLobby.cs, LobbyController.cs, LobbyCustomizationUI.cs, PlayerListItem.cs, PlayerObjectController.cs.
- **Why**: Solidify the networking core. Multiplayer synchronization is the most fragile part of the project.
- **Problem solved**: Added XML summaries and Tooltips to map out the Steamworks-to-Mirror handshake, Lobby UI refresh cycles, and [SyncVar] hooks. This ensures future modifications to the lobby don't accidentally break Steam integration or client-host state mismatch.
## Codebase Audit - Phase 5: UI Systems (July 2026)
- **Modified Files**: ColorButtonUI.cs, CustomTextButton.cs, UICustomButtonBase.cs, UIColorsPalettes.cs, InGameMenuController.cs, UIPanelController.cs, UINavigationGroup.cs, OpenURLButton.cs, CustomCursorFollower.cs, MouseManager.cs, PlayerVolumeSlider.cs, SettingsUIPresenter.cs.
- **Why**: Finalize the strict code standards implementation on the last remaining subsystem: the User Interface. The custom vector graphics UI and menu navigation controllers are highly customized and require thorough explanation for future maintainability.
- **Problem solved**: Added strict XML summaries (Description, Context, Justification) and Tooltips (Role, Use Case, Justification) across all UI scripts. Created `documentation/UI_System.md` to provide a high-level architectural overview of the vector UI integrations (Shapes), menu navigation, and input helpers, thereby successfully concluding the complete codebase audit.

## [2026-07-03] - Key Rebinding System & Multi-Menu Navigation

### Technical Justification & Details
- **Feature Request**: Implement key binding settings menu to let players customize their keyboard and mouse inputs, and support multi-category settings sub-menus.
- **Architecture**:
  - Designed `ControlRebindUIPresenter.cs` to coordinate a list of `RebindRowUI.cs` row entries.
  - Enhanced `InputSettingsConsumer.cs` to support interactive rebinding callbacks (onComplete, onCancel) and Escape-key cancellation flow.
  - Upgraded `UINavigationGroup.cs` to natively listen to the Escape key to close active sub-panels dynamically (allowing Escape to act as "Back").
  - Fixed duplicate field declarations in `CustomCursorFollower.cs` to restore compiling project state.
- **Persistence**: Rebinding overrides continue to be serialized to JSON and saved in `SettingsData` via `SettingsManager.UpdateSettings`.

### Code Modified/Added
- [NEW] [RebindRowUI.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/RebindRowUI.cs)
- [NEW] [ControlRebindUIPresenter.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/ControlRebindUIPresenter.cs)
- [MODIFY] [InputSettingsConsumer.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Controller/InputSettingsConsumer.cs) (Interactive overloads)
- [MODIFY] [UINavigationGroup.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/UINavigationGroup.cs) (Escape key back navigation)
- [MODIFY] [CustomCursorFollower.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/CustomCursorFollower.cs) (Fixed duplicate fields compilation error)
- [NEW] [walkthrough.md](file:///C:/Users/celestin/.gemini/antigravity-ide/brain/d23a534e-7777-4755-8e9f-c4cada1843ed/walkthrough.md) (Editor setup walkthrough)

## [2026-07-06] - Key Rebinding Upgrades, Left/Right Menu Sides & Duplicate Conflict Warnings

### Technical Justification & Details
- **Feature Request**: Resolve NullReferenceException on CustomTextButton, implement individual key reset, implement duplicate key conflict highlighting in Red, and support Left/Right concurrent panels in the UINavigationGroup.
- **NullReference Fix**: 
  - `OnDisable` triggers `KillActiveTweens()`. If the GameObject starts deactivated or gets deactivated before `Start()`, original states haven't been cached yet (`CacheOriginalStates()` runs in `Start()`). Thus, `_originalChildColors` is null, causing a NullReferenceException when indexing it in `KillActiveTweens()`.
  - Added an `_isCached` boolean flag to guard all original state restorations in `KillActiveTweens()` and `AnimateInteractableTransition()`.
- **Left / Right Multi-Panel Navigation**:
  - Added `PanelSide` Side setting to `UIPanelController` (categories: `Left` and `Right`).
  - Redesigned `UINavigationGroup` history tracking to split left and right panel groups, allowing a Left sub-menu panel (e.g. Settings Category) and a Right content panel (e.g. Controls or Audio) to stay visible simultaneously.
  - Automatically closes any open Right panel when returning back to the default Left panel (e.g. Main Menu).
  - Refactored history navigation to only push/pop Left panels, ensuring the Escape key/Back action always operates on Left panels.
- **Specific Key Reset**:
  - Added `ResetBindingToDefault(string actionName, int bindingIndex)` to `InputSettingsConsumer.cs` to remove single-binding overrides.
  - Hooked up `_rowResetButton` onClick listener in `RebindRowUI.cs` to trigger specific binding resets.
- **Conflict Highlighting**:
  - Implemented `CheckForDuplicateBindings()` in `ControlRebindUIPresenter.cs` that scans for active key conflicts across all rows.
  - Changes the key text label color to **Red** for duplicate/conflicting key assignments.
- **Strict Code Auditing & Comments**:
  - Added XML documentation summaries to all private/internal variables across modified UI scripts.
  - Added detailed code comments inside method bodies in B1 English to clarify algorithms.
- **Logging Gating**:
  - Added `_enableDebugLogs` serialized boolean field (defaulting to `false`) in `InputSettingsConsumer.cs` and guarded all standard `Debug.Log` calls to comply with the project's log reduction standard.

### Code Modified/Added
- [MODIFY] [CustomTextButton.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/CustomTextButton.cs) (Added `_isCached` state safety guards)
- [MODIFY] [UIPanelController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/UIPanelController.cs) (Added PanelSide enum and Side property)
- [MODIFY] [UINavigationGroup.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/UINavigationGroup.cs) (Implemented concurrent Left/Right panel and Left-only history stack)
- [MODIFY] [InputSettingsConsumer.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Controller/InputSettingsConsumer.cs) (Added ResetBindingToDefault method, _enableDebugLogs field, debug guards & B1 comments)
- [MODIFY] [RebindRowUI.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/RebindRowUI.cs) (Added reset button, duplicate coloring, B1 comments and XML variable summaries)
- [MODIFY] [ControlRebindUIPresenter.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/ControlRebindUIPresenter.cs) (Added duplicate scanning logic, B1 comments and XML summaries)
- [MODIFY] [walkthrough.md](file:///C:/Users/celestin/.gemini/antigravity-ide/brain/d23a534e-7777-4755-8e9f-c4cada1843ed/walkthrough.md) (Updated walkthrough)

## [2026-07-07] - Codebase Folder Reorganization & Assets Root Cleanup

### Technical Justification & Details
- **Feature Request**: Reorganize the project's folder structure, especially scripts, and clean up the root `Assets/` directory.
- **Organization & Cleanliness**:
  - Moved generic utility scripts to `Assets/1_Scripts/Utils/`.
  - Subdivided player controllers and visuals into `Assets/1_Scripts/Player/Movement/`, `Assets/1_Scripts/Player/Input/`, and `Assets/1_Scripts/Player/Mechanics/`.
  - Relocated editor utility scripts (such as `ModelMigrator.cs`) into an `Editor/` subdirectory under `Assets/1_Scripts/Player/Editor/`. This ensures that they compile only in editor contexts and prevents build packaging errors.
  - Subdivided UI scripts into `Assets/1_Scripts/UI/Core/`, `Assets/1_Scripts/UI/Components/`, and `Assets/1_Scripts/UI/Menus/` to separate core vector graphics math from actual buttons and high-level menu controllers.
  - Separated networking voice scripts from general audio scripts, relocating voice scripts to `Assets/1_Scripts/Audio/Voice/` and general game audio script and animator script to `Assets/1_Scripts/Audio/Controllers/`.
  - Moved loose Physic Materials (`ArmPart`, `NoFrixion`) from the `Assets/` root to a new dedicated `Assets/6_Physics/` directory.
  - Relocated the Input Actions asset from `Assets/` root to a clean configuration folder under `Assets/Input/`.
- **References Preservation**:
  - Moved script files and their associated `.meta` files simultaneously to keep Unity GUID references intact across all scenes and prefabs.
  - Updated the compile references inside `Assembly-CSharp.csproj` and `Assembly-CSharp-Editor.csproj` to match the new file locations on disk, allowing successful compiler build verification. Added exclusions for first-pass plugin scripts to prevent duplicate compilation.

### Code Modified/Added
- [NEW] [6_Physics](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/6_Physics) (New folder for physics materials)
- [NEW] [Input](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/Input) (New folder for input actions configuration)
- [MODIFY] [ArmPart.physicMaterial](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/6_Physics/ArmPart.physicMaterial) (Moved from Assets/ root)
- [MODIFY] [NoFrixion.physicMaterial](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/6_Physics/NoFrixion.physicMaterial) (Moved from Assets/ root)
- [MODIFY] [InputSystem_Actions.inputactions](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/Input/InputSystem_Actions.inputactions) (Moved from Assets/ root)
- [MODIFY] [InfiniteRotate.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Utils/InfiniteRotate.cs) (Moved)
- [MODIFY] [PlayerController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerController.cs) (Moved)
- [MODIFY] [PlayerMovementComponent.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerMovementComponent.cs) (Moved)
- [MODIFY] [PlayerJumpComponent.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerJumpComponent.cs) (Moved)
- [MODIFY] [PlayerLookComponent.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerLookComponent.cs) (Moved)
- [MODIFY] [PhysicalHeadController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PhysicalHeadController.cs) (Moved)
- [MODIFY] [PlayerInputHandler.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Input/PlayerInputHandler.cs) (Moved)
- [MODIFY] [InputSettingsConsumer.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Input/InputSettingsConsumer.cs) (Moved)
- [MODIFY] [PlayerArmsController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Mechanics/PlayerArmsController.cs) (Moved)
- [MODIFY] [PlayerVacuumController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Mechanics/PlayerVacuumController.cs) (Moved)
- [MODIFY] [PlayerInventory.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Mechanics/PlayerInventory.cs) (Moved)
- [MODIFY] [ModelMigrator.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Editor/ModelMigrator.cs) (Moved to Editor folder)
- [MODIFY] [UICustomButtonBase.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Core/UICustomButtonBase.cs) (Moved)
- [MODIFY] [MouseManager.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Core/MouseManager.cs) (Moved)
- [MODIFY] [CustomCursorFollower.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Core/CustomCursorFollower.cs) (Moved)
- [MODIFY] [ColorButtonUI.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/ColorButtonUI.cs) (Moved)
- [MODIFY] [CustomTextButton.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/CustomTextButton.cs) (Moved)
- [MODIFY] [OpenURLButton.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/OpenURLButton.cs) (Moved)
- [MODIFY] [RebindRowUI.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/RebindRowUI.cs) (Moved)
- [MODIFY] [UIColorsPalettes.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UIColorsPalettes.cs) (Moved)
- [MODIFY] [InGameMenuController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Menus/InGameMenuController.cs) (Moved)
- [MODIFY] [SettingsUIPresenter.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Menus/SettingsUIPresenter.cs) (Moved)
- [MODIFY] [ControlRebindUIPresenter.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Menus/ControlRebindUIPresenter.cs) (Moved)
- [MODIFY] [UIPanelController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Menus/UIPanelController.cs) (Moved)
- [MODIFY] [UINavigationGroup.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Menus/UINavigationGroup.cs) (Moved)
- [MODIFY] [PlayerVolumeSlider.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Menus/PlayerVolumeSlider.cs) (Moved)
- [MODIFY] [UniVoiceMirrorSetupSample.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Audio/Voice/UniVoiceMirrorSetupSample.cs) (Moved)
- [MODIFY] [UniVoicePlayerAudio.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Audio/Voice/UniVoicePlayerAudio.cs) (Moved)
- [MODIFY] [VoiceSettingsConsumer.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Audio/Voice/VoiceSettingsConsumer.cs) (Moved)
- [MODIFY] [VacuumAudioController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Audio/Controllers/VacuumAudioController.cs) (Moved)
- [MODIFY] [MouthAnimator.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Audio/Controllers/MouthAnimator.cs) (Moved)
- [MODIFY] [Assembly-CSharp.csproj](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assembly-CSharp.csproj) (Updated script paths & filtered plugin duplicates)
- [MODIFY] [Assembly-CSharp-Editor.csproj](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assembly-CSharp-Editor.csproj) (Updated script paths)

## [2026-07-07] - Rebind Mouse Buttons & Concurrent Rebind Row Lock Fix

### Technical Justification & Details
- **Bug Fix (Mouse Rebinding)**:
  - Allowed Left Click and Right Click to be bound during rebinding by removing the coarse `.WithControlsExcluding("Mouse")` filter in `InputSettingsConsumer.cs`.
  - Excluded only mouse axes that could accidentally trigger a rebind upon simple cursor movement: `.WithControlsExcluding("<Mouse>/position")`, `.WithControlsExcluding("<Mouse>/delta")`, and `.WithControlsExcluding("<Mouse>/scroll")`.
  - Introduced a one-frame safety delay using a Coroutine in `InputSettingsConsumer.cs` before starting the `PerformInteractiveRebinding` operation. This ensures that the mouse click event which triggered the "Rebind" button is completely processed and cleared from the Input System event queue, preventing it from immediately registering and auto-completing the rebind.
- **Bug Fix (Concurrent Row Lock)**:
  - Exposed the public `IsListening` state on `RebindRowUI.cs`.
  - Added `IsAnyRowRebinding()` in `ControlRebindUIPresenter.cs` to check if any managed row is currently waiting for input.
  - Guarded `StartRebindingProcess` in `RebindRowUI.cs` so that if another row is already actively waiting for input, the click is ignored, preventing concurrent overlapping "...Press Key..." states.

### Code Modified/Added
- [MODIFY] [InputSettingsConsumer.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Input/InputSettingsConsumer.cs) (Allowed mouse button inputs, filtered mouse movements/scroll, and added one-frame coroutine delay)
- [MODIFY] [RebindRowUI.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/RebindRowUI.cs) (Exposed IsListening property and added concurrent rebind lock check)
- [MODIFY] [ControlRebindUIPresenter.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Menus/ControlRebindUIPresenter.cs) (Implemented IsAnyRowRebinding check across all active rows)

## [2026-07-07] - Custom Shapes-Based Toggle Component & Settings UI Integration

### Technical Justification & Details
- **Feature Request**: Remplacer les boutons bascule (Toggle) d'Unity classiques par des composants de type Shapes (méthode hybride) dans le menu Audio.
- **UICustomToggle implementation**:
  - Created `UICustomToggle.cs` which inherits from `MonoBehaviour` and implements pointer interaction interfaces (`IPointerClickHandler`, `IPointerEnterHandler`, `IPointerExitHandler`).
  - Utilizes `Shapes.Rectangle` for the track background and `Shapes.Disc` for the slider knob/handle.
  - Applies smooth horizontal local movement to the handle using DOTween's `DOLocalMoveX` and morphs the track color using a generic `DOTween.To` tween to avoid direct extension method dependency conflicts.
  - Supports instant snapping (`animate = false`) for programmatic updates (e.g. menu setup on initialization) to prevent visual sliding artifacts when first opening the settings panel.
- **Presenter Integration**:
  - Replaced native `Toggle` fields (`_micTestToggle`, `_autoVadToggle`) in `SettingsUIPresenter.cs` with `UICustomToggle`.
  - The API calls (`isOn` and event listener bindings) map perfectly to our custom class, ensuring minimal refactoring overhead and zero behavioral differences.

### Code Modified/Added
- [NEW] [UICustomToggle.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomToggle.cs) (New custom vector Shapes-based toggle component)
- [MODIFY] [SettingsUIPresenter.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Menus/SettingsUIPresenter.cs) (Changed toggle fields to UICustomToggle)
- [MODIFY] [Assembly-CSharp.csproj](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assembly-CSharp.csproj) (Added UICustomToggle compile target reference)

## [2026-07-07] - Custom Toggle Raycast Target Fix

### Technical Justification & Details
- **Bug Fix**:
  - uGUI's `EventSystem` requires a component inheriting from `UnityEngine.UI.Graphic` (like `Image`) with `raycastTarget` set to `true` to detect mouse hovers and click inputs. Because the custom shapes are drawn via the Shapes package rather than standard uGUI meshes, pointer events were not being triggered.
  - Added an `Awake()` validation check in `UICustomToggle.cs` that automatically checks for a `Graphic` component on the Toggle's GameObject. If missing, it dynamically attaches a transparent `Image` (`Color(0,0,0,0)`) with `raycastTarget = true`. This mirrors the behavior of `UICustomButtonBase.cs` and guarantees that mouse click inputs are captured immediately without requiring colliders or manual inspector configuration.

### Code Modified/Added
- [MODIFY] [UICustomToggle.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomToggle.cs) (Added UnityEngine.UI import and automatic transparent Image generation on Awake)

## [2026-07-07] - Custom Toggle Visual & Animation Updates

### Technical Justification & Details
- **Toggle Customizations**:
  - Re-mapped the toggle state colors to morph the **handle disc** (`_handle.Color`) instead of the track background.
  - Replaced the handle scale animation on hover with a **track height expansion animation** (`_track.Height`). The script caches the original track height on `Start()` and tweens it to `_originalTrackHeight + _trackHoverHeightOffset` using DOTween.
  - Adjusted the default `_handleLocalXOffset` from `0.4f` (suited for meter-scale world objects) to `25.0f` (pixels) to work beautifully inside uGUI coordinates.

### Code Modified/Added
- [MODIFY] [UICustomToggle.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomToggle.cs) (Re-engineered hover height animations, handle-based color changes, and uGUI-adapted offset values)

## [2026-07-07] - Custom Toggle Handle Center Alignment Fix

### Technical Justification & Details
- **Bug Fix**:
  - Overwriting the handle's absolute horizontal coordinate with `_handleLocalXOffset` caused alignment issues if the handle's pivot or design center in the editor was not exactly `X = 0`.
  - Updated `UICustomToggle.cs` to cache the initial local X coordinate of the handle (`_initialHandleX`) during `Start()`.
  - Transition offsets are now computed relative to this cached design center: `_initialHandleX + _handleLocalXOffset` for the active state and `_initialHandleX - _handleLocalXOffset` for the inactive state. This ensures that the handle slides symmetrically relative to its editor layout design.

### Code Modified/Added
- [MODIFY] [UICustomToggle.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomToggle.cs) (Cached initial handle local position and made target slider X calculations relative to it)

## [2026-07-07] - Custom Shapes-Based Slider Integration

### Technical Justification & Details
- **Feature Request**: Replace standard Unity UI Sliders in the settings menu with a custom vector Shapes-based slider (`UICustomSlider.cs`).
- **UICustomSlider implementation**:
  - Implements `IPointerDownHandler`, `IDragHandler`, `IPointerEnterHandler`, `IPointerExitHandler`.
  - Converts pointer screen points to local RectTransform coordinates using `RectTransformUtility.ScreenPointToLocalPointInRectangle`.
  - Supports modular configurations: handles cases where `_fill` (Rectangle) is null (handle-only, like sensitivity threshold) and where `_handle` (Disc) is null (fill-only, like live mic volume indicator).
  - Exposes `fillColor` property to allow dynamic scripting changes to the fill Rectangle's color.
- **Presenter Integration**:
  - Replaced the five native `Slider` fields (`_masterVolumeSlider`, `_voiceVolumeSlider`, `_micSensitivitySlider`, `_micLevelIndicator`, `_autoVadSensitivitySliderRef`) with `UICustomSlider`.
  - Removed `_micLevelFillImage` from the fields and updated the live voice indicator code to set `fillColor` on `_micLevelIndicator` directly, simplifying the inspector layout.

### Code Modified/Added
- [NEW] [UICustomSlider.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomSlider.cs) (New custom vector Shapes-based slider component)
- [MODIFY] [SettingsUIPresenter.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Menus/SettingsUIPresenter.cs) (Changed slider fields to UICustomSlider and simplified level fill color mapping)
- [MODIFY] [Assembly-CSharp.csproj](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assembly-CSharp.csproj) (Added UICustomSlider compile target reference)
## [2026-07-08] - Custom Shapes-Based Slider Line Transition & Handle Realignment

### Technical Justification & Details
- **Slider Track & Fill Migration**:
  - Replaced the Shapes `Rectangle` components for `_track` and `_fill` with `Shapes.Line` components in `UICustomSlider.cs`.
  - Rectangle elements draw relative to their center/pivot which caused them to scale outwards in both directions when their width changed. By switching to `Line`, we define explicit `Start` and `End` local points, allowing the fill to grow cleanly from left-to-right.
  - Adjusted hover state animations to manipulate `Line.Thickness` instead of `Rectangle.Height`.
- **Coordinate System Alignment & Handle Correction**:
  - Realigned track, fill, and handle positioning to compute coordinates relative to the same source: the `RectTransform` local bounding box (`rectTransform.rect`).
  - The track line now stretches from `xMin` to `xMax`.
  - The fill line starts at `xMin` and ends at `Mathf.Lerp(xMin, maxX, pct)`.
  - The handle sits at the same `Mathf.Lerp(xMin, maxX, pct)` coordinate (with custom handle margins applied).
  - This solves the issue where the handle was misaligned relative to the track bounds and appeared in the middle of the slider when the value was at maximum.

### Code Modified/Added
- [MODIFY] [UICustomSlider.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomSlider.cs) (Re-engineered track and fill using Shapes Line, adapted hover thickness tweens, and realigned coordinate math to match uGUI boundaries)

## [2026-07-08] - Custom Shapes-Based Toggle Track Background Rect

### Technical Justification & Details
- **Toggle Customization**:
  - Added a new `Rectangle` reference `_trackBackground` in `UICustomToggle.cs` to act as the fill/background inside the toggle's border/track.
  - Caches the initial height of the track background (`_originalTrackBackgroundHeight`) on `Start()`.
  - Animates the height of `_trackBackground` symmetrically with the main `_track` component during hover enter and hover exit transitions (leveraging DOTween).

### Code Modified/Added
- [MODIFY] [UICustomToggle.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomToggle.cs) (Added background track shape and synchronized its hover animations with the outer track)

## [2026-07-08] - Custom Shapes-Based Slider Hover & Drag Polish

### Technical Justification & Details
- **Slider Handle Polish**:
  - Implemented the `IPointerUpHandler` interface in `UICustomSlider.cs` to accurately detect release events.
  - Caches the initial handle `Color` (`_originalHandleColor`) and `Radius` (`_originalHandleRadius`) on `Start()`.
  - Added visual configuration fields `_handleDragBloomMultiplier` (defaults to 1.5f) and `_handleHoverRadiusMultiplier` (defaults to 1.2f).
  - Configured `OnPointerEnter` and `OnPointerExit` to animate `_handle.Radius` using DOTween to simulate hover expansion.
  - Configured `OnPointerDown` and `OnPointerUp` to animate `_handle.Color` by applying/resetting the drag bloom multiplier, creating a premium glowing juice effect.

### Code Modified/Added
- [MODIFY] [UICustomSlider.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomSlider.cs) (Resolved slider TODO comments: implemented hover radius scaling and drag bloom multiplier animations using DOTween)

## [2026-07-08] - Custom Shapes-Based Toggle Hover & Transition Bloom

### Technical Justification & Details
- **Toggle Handle Polish**:
  - Caches the initial handle `Radius` (`_originalHandleRadius`) on `Start()`.
  - Added visual configuration fields `_handleHoverRadiusMultiplier` (defaults to 1.2f) and `_handleTransitionBloomMultiplier` (defaults to 1.5f).
  - Configured `OnPointerEnter` and `OnPointerExit` to animate `_handle.Radius` using DOTween to simulate hover expansion.
  - Configured `UpdateVisuals` (when animating transitions) to briefly flash the handle's `Color` with the HDR bloom multiplier during the horizontal slide translation, settling back down to the target ON/OFF color at the end.

### Code Modified/Added
- [MODIFY] [UICustomToggle.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomToggle.cs) (Added hover handle radius scaling and a DOTween sequence to flash HDR bloom on the handle color during state transitions)

## [2026-07-08] - Custom Shapes-Based Slider Disabled State (Grey out)

### Technical Justification & Details
- **Slider Typo Fix**:
  - Removed the compile-breaking typo `"e sois grisé."` that was accidentally appended to the end of `UICustomSlider.cs`.
- **Disabled State Visual Transition**:
  - Added visual configuration fields `_disabledTrackColor`, `_disabledFillColor`, and `_disabledHandleColor` in `UICustomSlider.cs` to allow full inspector styling for non-interactable states.
  - Caches the initial/active track and fill colors (`_originalTrackColor`, `_originalFillColor`) on `Start()`.
  - Refactored `fillColor` property so setting fill color dynamically while disabled preserves the configured value in cache and only applies it visually upon slider re-activation.
  - Implemented `UpdateInteractableVisuals` which animates (with DOTween) or instantly sets the components' colors to their respective disabled or active values when the `interactable` property is toggled.

### Code Modified/Added
- [MODIFY] [UICustomSlider.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomSlider.cs) (Cleaned up trailing typo, cached original track/fill colors, added disabled state color configurations, and hooked up DOTween visual transitions on interactable state changes)

## [2026-07-08] - Custom Shapes-Based ListView / ScrollView

### Technical Justification & Details
- **ScrollRect & Shapes Integration (KISS)**:
  - Created a hybrid UI architecture leveraging Unity's native `ScrollRect` for stable physics (inertia, masking, touch dragging) alongside Freya Holmér's Shapes library for premium vector visuals.
- **Custom Scrollbar Component (`UICustomScrollbar.cs`)**:
  - Independent vector graphic scrollbar supporting horizontal and vertical directions.
  - Dynamically calculates the handle size relative to the scrollable content proportion (`size` property).
  - Includes hover states (thickness expansion) and active dragging states (HDR color bloom on click/drag) mapped via `IPointerDownHandler`, `IDragHandler`, etc.
  - Automatically hides the track and handle when `size >= 1f` (content fits perfectly).
- **Master ListView Component (`UICustomScrollView.cs`)**:
  - Automatically links to the sibling `ScrollRect` and overrides default `verticalScrollbar` and `horizontalScrollbar` mapping to prevent standard Unity graphic conflicts.
  - Uses `LateUpdate` to continually measure content vs viewport sizes, synchronizing the dynamic handle sizes directly to the custom scrollbars safely within Unity's layout pass flow.
  - Two-way binding for normalized scroll values between the `ScrollRect` logic and our `UICustomScrollbar` UI scripts.

### Code Modified/Added
- [NEW] [UICustomScrollbar.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomScrollbar.cs) (Custom vector scrollbar rendering logic and event handlers).
- [NEW] [UICustomScrollView.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomScrollView.cs) (Coordinator script linking standard ScrollRect to custom Shapes scrollbars).

## [2026-07-09] - Custom Shapes-Based Simple Button
### Technical Justification & Details
- **Dynamic Size Synchronization**:
  - Implemented the script with the `[ExecuteAlways]` attribute so it automatically updates the Shapes `Rectangle` components' width and height to match the `RectTransform` bounds, providing real-time UI feedback inside the Unity Editor without playing the scene.
- **Outward Growth Calculation**:
  - Developed a mathematical size compensation formula during hover expansion: when the rectangle border thickness increases, the width and height are padded by the exact thickness delta. This keeps the inner bounds of the button perfectly locked in place while the border expands purely outwards.
- **Infinite Dash Rotation**:
  - Configured the button to transition into a dashed border style (`Dashed = true`) on hover.
  - Implemented seamless frame-rate independent rotation of the dash offset within the `Update()` lifecycle using standard modulo `1.0f` math.
- **Tactile Click Feedback**:
  - Transferred the high-fidelity DOTween click sequence from `CustomTextButton.cs`, implementing a snappy transform scale pulse (1.15x) paired with a high-intensity white bloom flash, fast blackout, and holographic flickering return sequence.
- **Disabled State Handling**:
  - Implemented the `OnInteractableChanged` event handler override to grey out the button text and rectangle shape components when `Interactable` is toggled.

### Code Modified/Added
- [NEW] [UICustomSimpleButton.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomSimpleButton.cs) (New modular shape-based button supporting size-sync, hover dash rotation, outward growth, and snappy click sequence).

## [2026-07-09] - Rebind Row UI Custom Button Integration
### Technical Justification & Details
- **Polymorphic Custom Button Support**:
  - Refactored `RebindRowUI.cs` serialization fields `_rebindButton` and `_rowResetButton` to use the parent base class `UICustomButtonBase` instead of the standard UGUI `Button`. This allows assigning either `CustomTextButton` or `UICustomSimpleButton` modularly in the inspector.
- **Interactable API Alignment**:
  - Updated button interactivity state changes inside `RebindRowUI.cs` to invoke the public `Interactable` property (capital I) rather than the standard UGUI `interactable` field, ensuring correct DOTween transitions and disabling sequences run dynamically.

### Code Modified/Added
- [MODIFY] [RebindRowUI.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/RebindRowUI.cs) (Refactored serialization to UICustomButtonBase and aligned interactivity calls to use the capital Interactable property).

## [2026-07-09] - UI Shape Size Synchronization Helper
### Technical Justification & Details
- **Reusable Size Sync Component**:
  - Created `UIShapeSizeSync.cs` as a generic helper component for Shapes `Rectangle` components.
  - Implements the `[ExecuteAlways]` attribute to automatically capture the parent `RectTransform` dimensions and apply them to the `Rectangle` shape's width and height.
  - Avoids code repetition (SSOT) across custom UI components and simplifies layout designs without manual sizing configurations inside the Unity Editor.

### Code Modified/Added
- [NEW] [UIShapeSizeSync.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UIShapeSizeSync.cs) (Utility component for automatic synchronization of Shapes Rectangle bounds with local RectTransform dimensions).

## [2026-07-09] - Auto VAD Settings Serialization
### Technical Justification & Details
- **Settings Serialization Support**:
  - Added `_isAutoVad` serialized boolean field and a public `IsAutoVad` property to `SettingsData.cs`. This allows persisting the Auto VAD toggle state to disk (via JSON PlayerPrefs serialization) along with the rest of the game settings.
- **Consumer State Synchronization**:
  - Modified `VoiceSettingsConsumer.cs`'s `OnSettingsUpdated` method to check for changes to `IsAutoVad`. If the setting has changed, it updates the local state and triggers the `_onAutoVadChanged` action callback to restore default or manual VAD configurations.
- **UI & Manager Propagation**:
  - Refactored `VoiceSettingsConsumer.SetAutoVad` to update and save the settings state via `SettingsManager.Instance.UpdateSettings` when the SettingsManager is available, ensuring immediate disk flushing and synchronized consumer propagation.

### Code Modified/Added
- [MODIFY] [SettingsData.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Core/Settings/SettingsData.cs) (Added serialized field and property for IsAutoVad settings).
- [MODIFY] [VoiceSettingsConsumer.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Audio/Voice/VoiceSettingsConsumer.cs) (Synchronized IsAutoVad loading and saving through SettingsManager).

## [2026-07-09] - Control Rebind UI Custom Reset Button & Simple Button Polish
### Technical Justification & Details
- **Polymorphic Reset Button Support**:
  - Refactored `ControlRebindUIPresenter.cs`'s field `_resetButton` from standard UGUI `Button` to the base class `UICustomButtonBase`. This allows assigning any custom vector button component (e.g. `UICustomSimpleButton`) modularly to reset bindings in the Controls UI.
- **Visual State and Color Resets**:
  - Fixed color stuck bugs in `UICustomSimpleButton.cs` when spam clicking or hover exiting. Updated `KillActiveTweens()` to safely reset the rectangle and text color back to their active baseline (`_originalRectColor`, `Color.white`) or disabled baseline colors depending on the `Interactable` state.
- **Smooth Dotted Transition Animation**:
  - Replaced the immediate basic boolean toggle of the dashed border on hover with a smooth, 0.2s duration float tween of `_rect.DashSpacing`. The border is now kept dashed by default at runtime with `DashSpacing` initialized to `0f` (rendering as a continuous line), and is animated to `_dashSpacing` on hover enter and back to `0f` on hover exit.
- **OnEnable Visual Caching Reset**:
  - Implemented the `OnEnable` callback in `UICustomSimpleButton.cs` to call `InitializeDefaultVisuals()`, ensuring that any disabled buttons (such as when the Settings panel is closed) reset cleanly to their base visual states when reopened.

### Code Modified/Added
- [MODIFY] [ControlRebindUIPresenter.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Menus/ControlRebindUIPresenter.cs) (Refactored reset button serialization to UICustomButtonBase).
- [MODIFY] [UICustomSimpleButton.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomSimpleButton.cs) (Polished visual state caching, implemented OnEnable resets, fixed stuck colors on tween cancels, and implemented smooth 0.2s dash spacing transitions).

## [2026-07-09] - Rebind Row Interactivity & Typewriter Optimizations
### Technical Justification & Details
- **Micro-Animation Click Preservation**:
  - Removed the `Interactable = false` lock on the active rebind button in `RebindRowUI.cs` when starting a key rebinding sequence. Disabling the button instantly cut off the click animation sequence mid-run. Since double-clicking or clicking other rows is already prevented programmatically via input state variables, removing the UGUI interactivity toggle allows the snappy scale and bloom click animation to execute fully.
- **Duplicate Typewriter Animation Prevention**:
  - Refactored `RebindRowUI.RefreshDisplay()` to perform a string comparison (`_bindingButtonText.text != newText`) before assigning the key label. Setting the text string on a TextMeshPro component automatically re-triggers any attached Febucci TextAnimator typewriter. Checking for changes prevents the typewriter animation from playing on all rows when resetting defaults or editing a single key.
- **Stuck Hover Visual state Fix**:
  - Modified the `Interactable` property setter in `UICustomButtonBase.cs` to instantly reset `_isHovered` to `false` when the button is disabled. This prevents the button from remembering a stale hover state if it is disabled while hovered.
  - Modified `UICustomSimpleButton.cs`'s `AnimateInteractableTransition()` to reset hover variables (`_rect.DashSpacing = 0f`, `_rect.Thickness`, etc.) when `isInteractable` is `false`, ensuring visual parameters return to normal.

### Code Modified/Added
- [MODIFY] [RebindRowUI.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/RebindRowUI.cs) (Optimized text refreshed comparison and bypassed button disabling during listening).
- [MODIFY] [UICustomButtonBase.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Core/UICustomButtonBase.cs) (Cleared hover state flag on interactability change).
- [MODIFY] [UICustomSimpleButton.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomSimpleButton.cs) (Reset visual thickness and dash spacing parameters when disabled).

## [2026-07-09] - Typewriter Caching & Hover Color Fixes
### Technical Justification & Details
- **Local Text Caching (`RebindRowUI.cs`)**:
  - Implemented a private `_lastAssignedBindingText` string variable. Rather than checking TMPro's raw text (which can return formatting tags or be altered by TextAnimator), we compare the retrieved binding string with this local cache.
  - TextMeshPro only receives text assignments when the binding text actually changes, preventing redundant TextAnimator typewriter triggers across all other rows.
  - Cleared this cache inside `StartRebindingProcess` to allow immediate redraws if the user rebinds the same key or cancels the rebind.
- **Conflict Text Hover Color Fix (`UICustomSimpleButton.cs`)**:
  - Removed the `_buttonText.color = Color.white` overwrite from `KillActiveTweens()`. Setting text color to white on every pointer enter/exit overrode the duplicate key conflict highlight (red color). Visual color switches between active and disabled states are now correctly isolated inside `AnimateInteractableTransition()`.

### Code Modified/Added
- [MODIFY] [RebindRowUI.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/RebindRowUI.cs) (Added local string caching to block redundant typewriter triggers and cleared it on rebind start).
- [MODIFY] [UICustomSimpleButton.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomSimpleButton.cs) (Removed text color overrides from the KillActiveTweens cleanup routine).

## [2026-07-10] - Custom Shapes-Based Dropdown

### Technical Justification & Details
- **Vector Shapes Dropdown Integration**:
  - Created `UICustomDropdown.cs` and `UICustomDropdownItem.cs` utilizing the Freya Holmér Shapes library to render premium vector outlines, backgrounds, and drop-down containers.
- **Header Button Visual Mirroring**:
  - Implemented border outline hover animations on the header `Rectangle` to match `UICustomSimpleButton.cs` exactly (outward thickness growth, dash spacing scaling, and infinite dash rotation).
  - Maintained a static, non-animated background shape for the header area.
  - Fetches the Febucci typewriter player from children to animate header selection updates.
- **Dropdown List Unfolding & Border Animations**:
  - Configured the list template container `_templateContainer` to unfold smoothly using a DOTween scale Y transition (0 to 1) with an `OutCubic` ease.
  - Attached a dedicated border outline `Rectangle` `_listBorder` that mimics the simple button's hover border animation while the dropdown is open (dashes rotate, thickness expands, and dash spacing increases).
- **Interactive Option Elements**:
  - Created option items that inherit from `UICustomButtonBase`, animating the background rectangle color on hover and triggering their child Febucci typewriter player.
  - Spawns option instances dynamically from the item template, populates labels, binds click events, and automatically closes the dropdown upon selection.
- **Click-Outside Blocker**:
  - Implemented an automatic blocker generator: when opened, it creates an invisible, fullscreen raycast blocker in the root Canvas to dismiss the dropdown when the player clicks outside the list container.
- **Settings Presenter Support**:
  - Swapped standard `TMP_Dropdown` with `UICustomDropdown` in `SettingsUIPresenter.cs`. The custom dropdown implements the exact same API signature (`ClearOptions()`, `AddOptions(List<string>)`, `value`, and `onValueChanged` event), enabling a seamless transition.

### Code Modified/Added
- [NEW] [UICustomDropdown.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomDropdown.cs) (Custom vector shapes dropdown header, panel blocker, item populator, and opening transitions).
- [NEW] [UICustomDropdownItem.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomDropdownItem.cs) (Dropdown option element controller handling background hover color transitions and typewriter relaunching).
- [MODIFY] [SettingsUIPresenter.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Menus/SettingsUIPresenter.cs) (Replaced standard TMP_Dropdown with UICustomDropdown for active microphone settings).
- [MODIFY] [Assembly-CSharp.csproj](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assembly-CSharp.csproj) (Added compile references for UICustomDropdown.cs and UICustomDropdownItem.cs).
- [x] Update DEVELOPMENT_LOG.md and todo.md to mark tasks as completed.

## [2026-07-10] - Custom Shapes-Based Toggle Handle Loading Fix
### Technical Justification & Details
- **Toggle State Loading Race Condition**:
  - Solved a startup visual initialization bug where setting `isOn` programmatically (e.g. from disk saves loading during early lifecycle cycles) before the toggle's `Start()` runs would result in the toggle handle shifting incorrectly.
  - The setter `isOn` triggered `UpdateVisuals()`, translating the handle using the uninitialized `_initialHandleX` (which is `0f`). Subsequently, when Unity's `Start()` hook fired, it cached the already offset coordinate (`35f` or `-35f`) as `_initialHandleX`, skewing all future target X calculations.
- **Lazy Caching System**:
  - Implemented `CacheOriginals()` and a private `_hasCachedOriginals` boolean state flag in `UICustomToggle.cs`.
  - The new method reads the initial coordinate `localPosition.x` of the handle exactly once, either from `Start()` or from `UpdateVisuals()` (whichever is executed first).
  - This guarantees that the correct reference center is captured regardless of early load orders.
- **Accessibility & Signature Validation**:
  - All modified fields and new helper methods are strictly private, preventing external visibility pollution.
  - Checked properties and types, assuring perfect backward compatibility and zero API surface changes.

### Code Modified/Added
- [MODIFY] [UICustomToggle.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomToggle.cs) (Added lazy caching mechanism to safely retrieve initial geometry configurations).

## [2026-07-12] - Custom Dropdown Editor Support
### Technical Justification & Details
- **Dropdown Configuration Serialization**:
  - Exposed options `_options` and active value `_value` fields in `UICustomDropdown.cs` via `[SerializeField]` and customized properties. This allows game designers to customize standard options lists and defaults inside Unity's Inspector.
- **Edit-Mode Visual Synchronization**:
  - Implemented `OnValidate()` in `UICustomDropdown.cs` to handle property modifications in the editor. It clamps index selection variables and updates TextMeshPro text references so the active value string updates instantly in the Scene view.
- **Unified Custom Inspector**:
  - Created `UICustomDropdownEditor.cs` under `Assets/1_Scripts/UI/Editor/` to organize complex dropdown visual properties into tidy collapsible Foldout groups (Header visual components, Template settings, Animation variables, Options configuration).
- **Hierarchy Validation Warning Checks**:
  - Added real-time error/warning check boxes inside the Custom Editor GUI. Displays explicit suggestions when essential components (outline rectangle shapes, text label components, template list bodies) are left empty.
- **Fast-Spawn Menu Integration**:
  - Implemented a hierarchy menu item `GameObject -> UI -> Shapes-Based Dropdown`. Generates, scales, nests, and pre-wires all necessary shapes, content layout groups, text elements, and template components under the current Canvas in one click.
- **Nested Foldout Warning Resolution**:
  - Replaced `EditorGUILayout.BeginFoldoutHeaderGroup` with standard `EditorGUILayout.Foldout` in `UICustomDropdownEditor.cs`. This prevents GUI layout warnings when displaying list/array properties (which have internal foldouts) inside visual groups.
- **Dynamic Template Auto-Sizing Layout**:
  - Configured `UpdateDimensions()` in `UICustomDropdown.cs` to dynamically adjust `_templateContainer`'s height based on `_itemParent.rect.height` at runtime and edit time.
  - Corrected `Content` layout parent anchoring (anchorMin: top-left, anchorMax: top-right, pivot: top-center) in `UICustomDropdownEditor.cs` menu helper to isolate vertical layout calculations and prevent circular size loops.

## [2026-07-12] - Premium Visuals & Dotted Hover Outline Animations
### Technical Justification & Details
- **Invisible-to-Visible Dotted Hover Outlines**:
  - Refactored both the dropdown header and the item template to keep their dotted outlines completely invisible (alpha 0) when not hovered.
  - On hover enter, they transition smoothly using DOTween to full opacity and animate their `DashSpacing` from 0f to target space value in 0.2s, mimicking `UICustomSimpleButton.cs`.
- **Item-Specific Outline & Caching**:
  - Extended `UICustomDropdownItem.cs` to support an outline `_rect` component. Updates dimensions inside `Update()` and animates dash spacing, thickness, and size offset on pointer hover.
- **Click-Only Background Transitions**:
  - Disabled item background color changes on pointer hover. The background now transitions to the selection color `_hoverColor` only when clicked, providing instant click feedback.
- **Hierarchy Creator Wiring**:
  - Modified `UICustomDropdownEditor.cs`'s GameObject menu builder to automatically instantiate, position, and bind the new item outline rectangle for the template.

## [2026-07-12] - Blockerless Custom Dropdown & New Input System Fixes
### Technical Justification & Details
- **New Input System Compatibility**:
  - Replaced legacy `Input.GetMouseButtonDown(0)` and `Input.mousePosition` references with `Mouse.current.leftButton.wasPressedThisFrame` and `Mouse.current.position.ReadValue()` to fix the `InvalidOperationException` crash when clicking or moving the mouse.
- **Independent Header Hover Behavior**:
  - Removed the `_isListOpen` lock inside `AnimateHoverExit()` so the header outline transition (solid vs dotted) matches exactly when the mouse pointer leaves or enters its physical boundaries, even while the dropdown list is open.
- **Blockerless outside-click detection**:
  - Removed the `Dropdown Blocker` GameObject entirely. Used coordinate checks on click (via `Mouse.current`) to close the list if clicked outside the header and template container bounds.

### Code Modified/Added
- [MODIFY] [UICustomDropdown.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomDropdown.cs) (Added UnityEngine.InputSystem imports, replaced legacy Input calls with Mouse.current checks, and removed list open checks on hover exit).

## [2026-07-13] - Dropdown Arrow Morph Animation
### Technical Justification & Details
- **Chevron vector morphing**:
  - Implemented a vector morphing animation for the dropdown chevron arrow, which is composed of two `Shapes.Line` components.
  - Closed chevron points down: Line 1 goes from (-17, 17) to (0, 0); Line 2 goes from (17, 17) to (0, 0).
  - Open chevron points up: Line 1 goes from (0, 0) to (17, -17); Line 2 goes from (0, 0) to (-17, -17).
  - Transition speed and ease curves are fully configurable in the Unity Inspector using exposed variables `_arrowAnimDuration` and `_arrowAnimEase`.
  - Morph is processed using DOTween for smooth runtime playback and snaps instantly during editor-time `OnValidate()` updates or initializations.

### Code Modified/Added
- [MODIFY] [UICustomDropdown.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomDropdown.cs) (Added serialized fields for arrow lines, duration, and ease; added AnimateArrow helper method; hooked morph triggers into Open, Close, OnValidate, and InitializeDefaultVisuals).

## [2026-07-13] - Dropdown Custom Inspector & KISS Cleanup
### Technical Justification & Details
- **Inspector Field Drawing (Editor Serialization)**:
  - Custom editor classes override the standard Inspector drawing. I added the arrow properties (`_arrowLine1`, `_arrowLine2`, `_arrowAnimDuration`, and `_arrowAnimEase`) to `UICustomDropdownEditor.cs`, exposing and drawing them under the "Animation Settings" Foldout group.
- **KISS Menu Creator Removal**:
  - Removed the complex hierarchy menu item creator shortcut method `CreateShapesBasedDropdown` (and associated child setups) from `UICustomDropdownEditor.cs` entirely to eliminate boilerplate code and simplify future asset maintenance.

### Code Modified/Added
- [MODIFY] [UICustomDropdownEditor.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Editor/UICustomDropdownEditor.cs) (Added serialized properties and drawn layouts for arrow animations inside OnInspectorGUI; removed the GameObject creation shortcut method).

## [2026-07-13] - Dropdown Arrow Line Size X & Y Parameterization
### Technical Justification & Details
- **Chevron Line Size X & Y Parameterization**:
  - Replaced the hardcoded coordinate values (17f / -17f) inside `AnimateArrow()` with two independent variables: `_arrowLineSizeX` and `_arrowLineSizeY` (both defaulting to 17f).
  - Exposed and drew `_arrowLineSizeX` and `_arrowLineSizeY` inside the custom editor `UICustomDropdownEditor.cs` under the "Arrow Animations" foldout group.

### Code Modified/Added
- [MODIFY] [UICustomDropdownEditor.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Editor/UICustomDropdownEditor.cs) (Added _arrowLineSizeXProp and _arrowLineSizeYProp serialization and drew them under the Arrow Animations group).

## [2026-07-13] - Dropdown Arrow Parent Offset Translation
### Technical Justification & Details
- **Chevron Parent Translation**:
  - Implemented vertical Y offset translation on open/close for the chevron parent RectTransform `_arrowParent`.
  - Closed dropdown: moves parent down by `_arrowParentOffsetY` relative to its baseline.
  - Open dropdown: moves parent up by `_arrowParentOffsetY` relative to its baseline.
  - Base Y coordinate `_originalArrowParentY` is cached on startup inside `CacheOriginalStates()`.
  - Exposed and serialized `_arrowParent` and `_arrowParentOffsetY` variables in the inspector using `UICustomDropdownEditor.cs`.

### Code Modified/Added
- [MODIFY] [UICustomDropdown.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomDropdown.cs) (Added _arrowParent and _arrowParentOffsetY fields, cached original position on start, and animated translation inside AnimateArrow).
- [MODIFY] [UICustomDropdownEditor.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Editor/UICustomDropdownEditor.cs) (Added properties serialization and drew fields inside Arrow Animations section of OnInspectorGUI).

## [2026-07-13] - Player Bone Bridge Architecture
### Technical Justification & Details
- **Decoupling Skeletal Mesh**:
  - Implemented the `PlayerBoneBridge` architecture to serve as a single source of truth (SSOT) for the player's skeletal bones. Control scripts (like `PlayerArmsController`, `PhysicalHeadController`) and physics joint systems now bind to static Bone Bridge transforms rather than model-specific bones.
  - At Awake, `PlayerBoneBridge` scans the child visual mesh for any `SkinnedMeshRenderer` and rebinds their `.bones` array and `rootBone` to the Bone Bridge transforms by name match, deforming the visual mesh using physics/bones animation output.
  - Added a follower script mechanism `RuntimeFollower` for non-skinned objects (e.g., wheels) to copy positions/rotations of Bone Bridge bones at runtime.
- **Dynamic Controls Wiring**:
  - Modified `PlayerCustomization.cs` to expose `ModelRenderer` as a public property.
  - `PlayerBoneBridge` detects the main renderer on the imported mesh at startup and assigns it to `PlayerCustomization.ModelRenderer`, which automatically reinstalls and instances the customized materials.
- **Editor Validation & Automation**:
  - Developed a custom editor `PlayerBoneBridgeEditor.cs` with validation checkers that report matching bone counts by name, and an auto-detector utility that populates custom followers (e.g. wheels) matching specific keywords.

### Code Modified/Added
- [NEW] [PlayerBoneBridge.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/PlayerBoneBridge.cs) (Manages runtime re-binding of skinned/non-skinned bones by name, and wires ModelRenderer to PlayerCustomization).
- [NEW] [PlayerBoneBridgeEditor.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Editor/PlayerBoneBridgeEditor.cs) (Custom inspector featuring bone name matching validators and keyword-based follower configuration tools).
- [MODIFY] [PlayerCustomization.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Visuals/PlayerCustomization.cs) (Exposed ModelRenderer property and added material instancing hooks on re-assignment).

## [2026-07-13] - Mouth Animator 3-Bone Thickness Preserving Scaling
### Technical Justification & Details
- **3-Bone Scaling Mechanics**:
  - Implemented 3-bone scaling support inside `MouthAnimator.cs` to match the modeler's armature layout designed to preserve thickness during mouth size changes.
  - When the target scale factor $S$ changes, the scale change vector is calculated as `change = targetScale - Vector3.one`. Each bone scales using a baseline scale of `Vector3.one` plus the scale change vector scaled by the bone's independent scale multiplier:
    - Bone 1 Scale = `1 + change * _bone1Multiplier` (scales with 100% of the mouth scale change, e.g. goes from 1 to 2)
    - Bone 2 Scale = `1 + change * _bone2Multiplier` (scales with 75% of the mouth scale change, e.g. goes from 1 to 1.75)
    - Bone 3 Scale = `1 + change * _bone3Multiplier` (scales with 50% of the mouth scale change, e.g. goes from 1 to 1.50)
  - Exposes 3 separate `Transform` fields (`_mouthBone1`, `_mouthBone2`, `_mouthBone3`) and their respective multipliers in the inspector to allow the user to easily configure the scaling ratios.
  - Implemented a backward-compatible check: if `_mouthBone1` is not set, it cleanly falls back to scaling the single `_mouthTransform` object, preventing inspector setup errors from breaking existing assets.

### Code Modified/Added
- [MODIFY] [MouthAnimator.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Audio/Controllers/MouthAnimator.cs) (Added bone fields and scale multipliers, updated scale application math in Update, and added backward compatibility fallback).

## [2026-07-13] - Remote Player Multiplayer Look & Wheels Sync
### Technical Justification & Details
- **Kinematic Wheel Speed Estimation**:
  - Since remote player clones have `Rigidbody.isKinematic = true` (to allow smooth Mirror `NetworkTransform` positioning without physics collisions dragging them), they have a default velocity of zero. This caused remote players' wheels to never rotate or steer.
  - Modified `WheelSteering` in `Wheels.cs` to dynamically compute estimated velocity using position changes over time (`(transform.position - _lastPosition) / Time.deltaTime`) when the Rigidbody is kinematic. This ensures wheels rotate and pivot realistically for all remote players.
- **Camera Look Pitch Synchronization**:
  - Yaw (turning left/right) is naturally synchronized because the root GameObject rotates, which is synced by the root's `NetworkTransform`.
  - Pitch (looking up/down) only updated the local camera localRotation on `isLocalPlayer`. Since remote players had their camera gameobjects deactivated, their pitch remained static at 0 degrees, meaning their heads never nodded/tilted and their procedural arms aimed straight ahead on other clients.
  - Added a `_syncedCameraPitch` `[SyncVar]` and a `CmdSyncCameraPitch` `[Command]` in `PlayerLookComponent.cs`. Local players stream their look pitch to the server when it changes by > 0.5 degrees, and other clients apply this synced pitch to the remote player's camera transform, allowing `PhysicalHeadController` to nod/aim physically.

### Code Modified/Added
- [MODIFY] [PlayerLookComponent.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerLookComponent.cs) (Synced camera pitch look direction on the network via Command/SyncVar).
- [MODIFY] [Wheels.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Visuals/Wheels.cs) (Added kinematic estimated velocity fallback to steer wheels of remote players).

## [2026-07-14] - Snappy Shoulder Rotation Animation
### Technical Justification & Details
- **Snappy Shoulder Rotation**:
  - Implemented smooth, snappy shoulder rotation animations triggered automatically when individual arms are extended.
  - When the Left Arm is extended, the Left Shoulder rotates by +90 degrees on the Y-axis.
  - When the Right Arm is extended, the Right Shoulder rotates by -90 degrees on the Y-axis.
  - The rotation returns to 0 degrees when the respective arm is retracted.
  - Uses DOTween to animate local rotation. The transition utilizes `Ease.OutBack` by default (configurable in the inspector) to exceed/overshoot the target rotation slightly before settling, providing a snappy, responsive feel.
  - Pre-kills existing tweens (`shoulder.DOKill()`) to support rapid input spamming safely.
  - On startup (`Start()`), initial positions snap instantly without playing animations to match the starting extension states.
  - Triggered in the Mirror SyncVar hooks `OnLeftArmStateChanged` and `OnRightArmStateChanged`, ensuring the visual shoulder animations play synchronously across all clients in multiplayer.

### Code Modified/Added
- [MODIFY] [PlayerArmsController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Mechanics/PlayerArmsController.cs) (Added shoulder fields, snap initialization, and DOTween rotation animation triggers inside SyncVar hooks).

## [2026-07-14] - Player Arm Retraction Rest Float Physics & Joint Optimization
### Technical Justification & Details
- **Arm Colliders Separation (Self/Body Collision Bypass)**:
  - Dynamically disabled physics collisions on startup (`Start()`) between left/right arm structures and the player's main chassis, head, wheels, and other arm.
  - Ignored self-collisions between segments of the same arm to prevent mechanical lockup.
  - Decoupled arm physics from the player's core movement, completely fixing linear velocity bottlenecks (blocks/lags) and massive FPS drops under quick movement.
- **Dynamic Joint Stiffening & Locking**:
  - Automatically configured all `ConfigurableJoint` and `Rigidbody` components on start to enforce a high-stiffness, zero-stretch preset (`_jointSpringForce = 1500f`, `_jointDamping = 100f`, projection distance/angle bounds, and `angularXMotion` locked to prevent twisting on itself).
  - Configured Rigidbody `angularDamping` (Unity 6 drag replacement) dynamically to dampen rapid oscillations.
- **T-Pose Rest Targeting & Decaying Retraction Forces**:
  - Cached design-time local rest positions/rotations of the hands on startup relative to the player root.
  - Modified physics simulation to continuously attract hands back to their local T-pose coordinates when retracted, preventing them from sagging to the floor.
  - Implemented dynamic force/torque scaling based on elapsed time since release (`_retractTransitionDuration = 0.5s`):
    - **Transient Phase (Strong)**: Immediately after release, applies a strong force (`_releaseTransientForce`) to quickly pull the arm back to the T-pose.
    - **Resting Phase (Weak)**: Gradually decays to a weaker resting force (`_releaseRestForce`) and torque (`_releaseRestTorque`), keeping the arm suspended above the ground loosely without making it look rigidly frozen.
- **Distance-Based Fade-Out (Anti-Vibration & Anti-Wrist-Curve)**:
  - Added a `_restFadeDistance` parameter (default `0.35m`).
  - Inside `ApplyArmPhysicsForces`, if the arm is retracted, we calculate a `distanceFactor` which scales down to `0` linearly as the hand reaches the target rest position.
  - This eliminates jitter/vibrations at the equilibrium rest state since attraction forces drop to zero, and prevents the hand/wrist from being artificially torque-forced to align horizontally, letting the arm hang naturally aligned with the preceding joints without curving up.
- **Physics Solver & Deadzone Stabilizers (Continuous Hand Jitter Fix)**:
  - Added a strict `_restDeadzone` radius (default `0.05m`) to cut all external manual forces/torques to exactly `0` when the hand reaches rest.
  - Automatically configured `solverIterations = 25` and `solverVelocityIterations = 15` on all arm Rigidbody components dynamically on startup. This increases joint simulation precision, preventing the native joint spring calculations from oscillating/vibrating at high spring coefficients.

### Code Modified/Added
- [MODIFY] [PlayerArmsController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Mechanics/PlayerArmsController.cs) (Implemented collision ignoring, runtime joint tuning, rest pose caching, transient-to-rest force interpolation, distance-based fade-out with a strict deadzone, and high solver iterations on all arm rigidbodies).

## [2026-07-15] - Progressive Neck Curvature & Look Centralization
### Technical Justification & Details
- **Centralized Look State & Component-like Decoupling**:
  - Decoupled view pitch and yaw logic from physical movements by establishing `PlayerLookComponent.cs` as the Single Source of Truth (SSOT).
  - Exposed `CurrentPitch` and `CurrentYaw` public properties on `PlayerLookComponent.cs` to serve both local input and synced remote client states.
  - Refactored `PhysicalHeadController.cs` to read pitch and yaw directly from `PlayerLookComponent` rather than recalculating them independently, simplifying the component logic.
- **4-Bone Neck Chain Procedural Curvature**:
  - Implemented progressive relative local rotation for a 4-bone neck chain (`_neckBones`) in `PhysicalHeadController.cs`.
  - Distributes the centralized look rotation using configurable local rotation multipliers (`_neckRotationWeights`).
- **Progressive Backward Translation (Local -Z Receding)**:
  - Bending or turning the neck now drives a progressive backward translation of the neck bones along their own rotated local Z axis (`_neckBones[i].localRotation * Vector3.back`).
  - The recede distance scales dynamically based on local pitch and yaw magnitude and configurable factors (`_neckBackwardFactors`), which prevents mesh stretching and clipping against the torso.
- **Physical Head Target Tracking & Rotation Unlock**:
  - Detaches the physical head bone on startup and tracks the end of the procedurally bent neck chain.
  - Caches the starting relative offset of the head relative to the body root (`_headStartLocalPosInOriginalParent`) on `Start()`.
  - Calculates the ConfigurableJoint targetPosition offset dynamically relative to this initial position (`targetPosition = -offset`), preventing the head from collapsing downwards on startup.
  - Re-injects the crouch height offset (`_crouchYOffset`) directly on top of the target joint translation.
  - Dynamically configures the joint's rotation limits and drives on `Start()` to allow target rotation to rotate the head freely: sets `rotationDriveMode = RotationDriveMode.Slerp` and unlocks angular motions by setting `angularXMotion`, `angularYMotion`, and `angularZMotion` to `Free`.
- **Robust Automatic Inspector Fallback**:
  - Implemented automatic upward hierarchy traversal in `Start` starting from the head's parent to auto-populate the 4 neck bones in order (`Neck_01` to `Neck_04`) if they are left unassigned in the inspector.
- **Enabled Head Collision Physics & Weight Stabilization**:
  - Removed the `Physics.IgnoreCollision` setup between the head collider and body/arm colliders to let the head physically contact and collide with the body instead of passing through it.
  - Added serialized properties `_headMass` (default `0f`), `_positionSpring`, `_positionDamping`, `_rotationSpring`, and `_rotationDamping` to `PhysicalHeadController.cs` to dynamically configure physical weight resistance and spring stiffness. Setting mass to 0f allows Unity to use the minimum positive mass value, bypassing movement drag and body self-collision issues.
- **Torso Bone Rotation Control (Yaw decoupling)**:
  - Added a serialized `_torsoBone` field to `PlayerLookComponent.cs` along with network synchronization variables (`_syncedTorsoYaw` SyncVar and `CmdSyncTorsoYaw` command).
  - When a torso bone is assigned, yaw input rotates the torso bone (`_torsoBone.localRotation`) instead of rotating the whole player root transform.
  - Added support for camera nesting: if the camera is a child of the torso bone, it automatically inherits the torso's yaw; otherwise, yaw is applied to the camera transform directly. Remote players smoothly interpolate this torso yaw.
- **Vision Range Auto-Discovery Fallback**:
  - Refactored `PlayerViewRange.cs` to automatically auto-discover the player's main camera on startup and assign it to `_viewReference` if it was left unassigned in the inspector, restoring the vision cone orientation dynamically.

### Code Modified/Added
- [MODIFY] [PhysicalHeadController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PhysicalHeadController.cs) (Added neck configuration fields, cached initial transforms, added automatic fallback discovery, refactored ApplyJointTargetState to drive neck bones and joint targets using Atan2 projections and relative offsets, removed collision ignore logic, added serialized physics settings to override head mass/joint drives, and unlocked angular motion limits dynamically on Start).
- [MODIFY] [PlayerLookComponent.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerLookComponent.cs) (Added _torsoBone serialized field and network sync variables, exposed CurrentPitch and CurrentYaw properties, refactored HandleRotation and Update to apply yaw to the torso bone and camera nesting, and cached original torso rotation offset).
- [MODIFY] [PlayerViewRange.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Visuals/PlayerViewRange.cs) (Implemented auto-discovery camera fallback for _viewReference on start local player).

## [2026-07-15] - Torso Pivoting, Decoupled Movement & Clean Physics Setup
### Technical Justification & Details
- **Auto-Discovery of Torso Bone**:
  - Implemented automatic child hierarchy search in `PlayerLookComponent.cs` on startup. If `_torsoBone` is left unassigned, it automatically scans for transforms matching names "torso", "chest", "spine", or the parent of "Neck_01", making the inspector setup plug-and-play.
- **Torso-Relative Movement Direction**:
  - Refactored `PlayerMovementComponent.cs` to calculate horizontal movement vectors relative to `PlayerLookComponent.CurrentYaw` rather than the player root's forward/right vectors.
  - This allows the wheelchair-like wheels base to remain horizontally static while the player still moves forward/sideways in the direction their torso is looking.
- **Coordinate Space Correction for Head Joint**:
  - Fixed a critical coordinate space mismatch in `PhysicalHeadController.cs`. The ConfigurableJoint's target position and target rotation are relative to the connected body Rigidbody.
  - Caches `_bodyRoot` (the joint's `connectedBody` Transform, or falls back to the player parent Rigidbody/root).
  - Converts all desired world coordinates of the neck tip into `_bodyRoot` space rather than the neck parent space (`_originalParent`).
  - This allows the head to pivot cleanly and follows the torso's yaw rotations without collapsing or collapsing downwards.
- **Simplification of Procedural Neck Bending**:
  - Simplified the procedural neck bone bending to distribute pitch (nodding) only.
  - Removed local yaw bending since the torso already rotates on yaw, allowing the neck and head to stay perfectly aligned as a single solid horizontal unit when looking around.

### Code Modified/Added
- [MODIFY] [PlayerLookComponent.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerLookComponent.cs) (Implemented automatic fallback detection of the torso bone in child hierarchy on startup).
- [MODIFY] [PlayerMovementComponent.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerMovementComponent.cs) (Modified movement vector calculations to align with look yaw instead of the root transform).
- [MODIFY] [PhysicalHeadController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PhysicalHeadController.cs) (Cached the connectedBody/player root as `_bodyRoot`, converted all joint target calculations to `_bodyRoot` space, and simplified progressive neck bending to only distribute look pitch).

## [2026-07-15] - KISS Clean Setup & Reference Simplification
### Technical Justification & Details
- **Removed Auto-Discovery Fallback Guess-Work**:
  - Removed torso bone auto-discovery from `PlayerLookComponent.cs`.
  - Removed camera auto-discovery from `PlayerViewRange.cs`.
  - Removed neck bone auto-discovery from `PhysicalHeadController.cs`.
  - T- **Direct ConnectedBody Coordinate Space & Torso Joint Rigging**:
  - Simplified `_bodyRoot` resolution in `PhysicalHeadController.cs`. It now uses `_joint.connectedBody.transform` directly if assigned in the editor, and falls back to `_originalParent` otherwise.
  - This completely solves joint yaw snapping when looking past 180 degrees. Because the joint rotates w- **Unified Agnostic Physical Coordinates**:
  - Removed all dynamic Rigidbody attachments, visual counter-rotation band-aids, and custom look conversions.
  - Simplified `PlayerLookComponent.cs` to handle mouse inputs and rotate the `_torsoBone` transform. If a Rigidbody is attached to the torso bone in the Unity Editor (kinematic or physical), the script automatically uses `MoveRotation` in `FixedUpdate` to drive it smoothly, avoiding out-of-sync physics solver jitters.
  - Refactored `PhysicalHeadController.cs` to be completely reference-space agnostic. It resolves the joint's target coordinates in the space of `_joint.connectedBody` directly, meaning it adapts automatically to whichever Rigidbody/bone the user assigns in the Editor.
  - Removed joint motion limits overriding from the script, allowing the user to configure limits (`angularXMotion`, `angularYMotion`, etc.) directly in the Unity Inspector.

### Code Modified/Added
- [MODIFY] [PlayerLookComponent.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerLookComponent.cs) (Cleaned torso yaw rotation to support standard transform or Rigidbody MoveRotation in FixedUpdate).
- [MODIFY] [PlayerMovementComponent.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerMovementComponent.cs) (Used Look Component yaw relative vectors for movement calculations).
- [MODIFY] [PhysicalHeadController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PhysicalHeadController.cs) (Simplified body root resolving to use connectedBody directly, relative target calculations, and removed hardcoded joint angular limit overrides).

## 2026-07-15: Reconstruction V5 (Camera Alignment & Signed Bending offsets)

### Fix / Feature
- Rebuilt from scratch the look, head, movement direction, and eye tracking scripts to support exact world camera looking, lagged physics head tilting, locked horizontal yaw, signed neck recoil translations, and instant pupil tracking.

### Rationale
- Decoupled physical wiggling from camera look targeting by overriding the camera's world rotation in `LateUpdate` (100% precision), while allowing its position to follow the head Rigidbody.
- Locked the head's horizontal yaw joint (`Angular Y Motion` = Locked) to prevent horizontal twisting/lag, while leaving Pitch and Roll wobbly.
- Replaced the absolute recoil factor with signed recoil (`bonePitch * _neckBackwardFactors[i]`), ensuring correct recoil (backward in `-Z` when looking down, forward in `+Z` when looking up).
- Replaced soft fallback validation checks with loud assertions (throwing explicit exceptions) to make setup issues obvious in the Unity Editor console.

### Code Modified/Added
- [MODIFY] [PlayerLookComponent.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerLookComponent.cs) (World camera rotation override, kinematic assertions).
- [MODIFY] [PlayerMovementComponent.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerMovementComponent.cs) (Torso-look relative movement forces).
- [MODIFY] [PhysicalHeadController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PhysicalHeadController.cs) (Signed neck translations, joint look targeting, kinematic torso check asserts).
- [MODIFY] [Eye.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Visuals/Eye.cs) (Added instant world pupil rotation and 70% slerped eye rotation).

## [2026-07-15] - Reconstruction V5 (Step 0: Scrap Logic)

### Feature / Refactoring
- Emptied all logical method bodies in `PhysicalHeadController.cs` and `PlayerLookComponent.cs` as part of Step 0 of the implementation plan, while preserving all existing comments. Eye tracking scripts (`Eye.cs`) were left untouched.

### Rationale
- Allows rebuilding look, camera world alignment, physical neck bending, and physical head joint features step-by-step from clean files to ensure maximum stability and zero legacy interference.

### Code Modified/Added
- [MODIFY] [PhysicalHeadController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PhysicalHeadController.cs) (Scrapped execution logic in method bodies).
- [MODIFY] [PlayerLookComponent.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerLookComponent.cs) (Scrapped execution logic in method bodies).

## [2026-07-15] - Reconstruction V5 (Step 1: Torso Look & Relative Movement)

### Feature Added
- Reimplemented mouse look logic in `PlayerLookComponent.cs`.
- Kinematic torso bone Rigidbody rotation via `MoveRotation` in `FixedUpdate` (Y-axis Yaw).
- Camera world rotation forced to 100% look accuracy in `LateUpdate` (X-axis Pitch and Y-axis Yaw).
- Strict runtime validations/asserts for torso bone assignment and Rigidbody kinematic setting on startup.

### Rationale
- Decouples torso rotation from movement root and prevents double camera sensitivity by enforcing absolute world rotation on camera.
- Relies on kinematic Rigidbody `MoveRotation` for smooth physics-accurate updates rather than direct transform manipulation, preventing visual jitters.

### Code Modified/Added
- [MODIFY] [PlayerLookComponent.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerLookComponent.cs) (Reimplemented Look methods, cursor locking, and kinematic Rigidbody updates).

## [2026-07-15] - Reconstruction V5 (Step 2: Neck Bending & Signed Offset)

### Feature Added
- Reimplemented neck bending logic in `PhysicalHeadController.cs`.
- Iterates over all cached neck bone transforms, rotating them on the vertical Pitch axis relative to their initial local orientation based on the camera's Pitch.
- Applies a signed translation offset in the local Z axis for bones with a translation factor > 0 (recedes in -Z when looking down, advances in +Z when looking up).

### Rationale
- Creates a smooth visual curvature for the robot's neck that mirrors the vertical look direction.
- Dynamic signed offsets prevent the head mesh from clipping/colliding with the main robot body when looking at extreme angles.

### Code Modified/Added
- [MODIFY] [PhysicalHeadController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PhysicalHeadController.cs) (Reimplemented Start, FixedUpdate, and ApplyJointTargetState for neck bending and translation offsets).

## [2026-07-16] - Reconstruction V5 (Step 1 Update: Decoupled Visuals & Root Player Rotation)

### Feature Added
- Reverted head joint and neck bending logic to Step 0 (empty method bodies in `PhysicalHeadController.cs`).
- Refactored `PlayerLookComponent.cs` to rotate the entire player root Rigidbody horizontally (Yaw) in `FixedUpdate` instead of only rotating the Torso bone.
- Added serialized `_wheelsChassisVisual` field in `PlayerLookComponent.cs` to handle counter-rotation of the wheels chassis.
- In `LateUpdate`, if `_wheelsChassisVisual` is assigned, applies a local rotation of `-targetYaw` to keep the wheels visual stationary in world space.
- Cleaned up and removed all unused `_torsoBone`, `_torsoRb`, and `_originalTorsoLocalRot` fields and checks from `PlayerLookComponent.cs`.

### Rationale
- Rotating the entire player root keeps all child joints and transforms aligned inside a single rotating reference frame, avoiding joint reference frame torsion issues.
- Visual counter-rotation of the wheels visual chassis maintains the aesthetic decoupling of the wheels visual relative to the player's look direction, preserving the original design.
- Cleaning up the torso fields keeps the inspector and code clean according to the KISS principle since the torso bone is no longer rotated independently.

### Code Modified/Added
- [MODIFY] [PhysicalHeadController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PhysicalHeadController.cs) (Reverted method bodies to empty).
- [MODIFY] [PlayerLookComponent.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerLookComponent.cs) (Switched to player root Rigidbody rotation, added wheels chassis visual counter-rotation, and removed torso bone references/validations).

## [2026-07-16] - Active Ragdoll Head Pitch & Neck Bending Control

### Feature Added
- **Active Ragdoll Pitch Control**: Replaced old procedural neck bone bending logic in `PhysicalHeadController.cs` with physical pitch-based active ragdoll joint targeting using Slerp drives.
- **Organic Softbody Reactions**: Intermediate neck bones that are not actively driven (e.g. Neck base, Neck 1, Neck 3) now flex and bend organically in response to the physical motion of the driven bones (Neck 2 and Head).
- **Automatic Physics Configuration**: Dynamically configures all ConfigurableJoint and Rigidbody parameters under an optional neck root transform at Start (stiffening drives, enabling projection, setting solver iterations and high angular drag to prevent jitter/stretch).
- **Collision Separation**: Programmatically configures all neck and head colliders to ignore collisions with the rest of the player's body to avoid physics glitches.

### Code Modified/Added

#### [MODIFY] [PhysicalHeadController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PhysicalHeadController.cs)
- **Class `PhysicalHeadController`**: Completely rewritten. Features a configurable `_controlledJoints` list mapping bones to pitch multipliers, dynamic active ragdoll setup, collision ignoring, and mathematically precise joint-space target rotation updates in `FixedUpdate`.

### Technical Justification & Details
- **Joint Space Target Rotation Offset**: Standard Unity ConfigurableJoint `targetRotation` operates in joint-space. Transformed the desired pitch rotation offset relative to the starting local rotation into the joint's local axes to guarantee exact alignment.
- **KISS Philosophy**: Completely removed procedural translation curves and camera-lag ratios, relying instead on pure PhysX joint dynamics.
- **Explicit Visibility & Standard compliance**: Followed Allman styling, explicit visibilities, and private `_camelCase` member naming.

## [2026-07-17] - Centralized Player Collision ignoring Manager (SSOT)

### Feature Added
- **Centralized Player Collision Manager**: Created `PlayerCollisionManager.cs` to serve as the Single Source of Truth (SSOT) for all player-internal physics collision ignoring rules.
- **Custom Torso and Arm Collision Interactivity**: 
  - Allows two custom torso colliders (A and B) to ignore collisions with the wheels.
  - Torso Collider A ignores collisions with the arms.
  - Torso Collider B **does not ignore** collisions with the arms, allowing arms to physically collide and interact with this specific torso collider.
  - Centralizes other standard player collision rules (head/neck vs torso/wheels, arms self-collisions, arm vs arm) in one location.

### Code Modified/Added

#### [NEW] [PlayerCollisionManager.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerCollisionManager.cs)
- **Class `PlayerCollisionManager`**: Centralizes classification and configuration of physics collision exemptions using `Physics.IgnoreCollision` at startup.

#### [MODIFY] [PlayerArmsController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Mechanics/PlayerArmsController.cs)
- Removed local `IgnorePlayerCollisions()` method and call, delegating arm collision management entirely to the centralized `PlayerCollisionManager`.

#### [MODIFY] [PhysicalHeadController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PhysicalHeadController.cs)
- Removed local `IgnorePlayerCollisions()` method and call, delegating head/neck collision management entirely to the centralized `PlayerCollisionManager`.

### Technical Justification & Details
- **Selective Physics Blocking**: Employs lists and explicit references to handle different interaction parameters on specific torso colliders.

## [2026-07-17] - Dynamic Joint Stiffness & Rest Vibration Damping

### Feature Added
- **State-Dependent Joint Stiffness**: Introduced dynamic spring force adjustments for arm joints. Left and right arm joints now transition spring/damping forces based on extension state.
- **Separate Shoulder and Elbow/Wrist Tuning**: Exposed independent properties for the shoulder joint (stiffer at rest to prevent sagging) and the elbow/wrist joints (softer at rest for loose/relaxed arms).
- **Vibration Damping**: Enabled fine-tuned damping variables for rest states to eliminate high-frequency jitter and trembling in limp limbs.

### Code Modified

#### [MODIFY] [PlayerArmsController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Mechanics/PlayerArmsController.cs)
- Added caching structures and collections for left/right shoulder joints and left/right elbow/wrist joints at initialization.
- Modified `ConfigureArmJointsPhysics` to skip setting hardcoded slerp drives.
- Added `UpdateJointDrives` method to dynamically update ConfigurableJoint spring/damping drives.
- Updated `FixedUpdate` to refresh joint drives when state changes.




