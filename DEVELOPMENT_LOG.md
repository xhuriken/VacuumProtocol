# Development Log

## [2026-05-26] - Lobby Color Selection Palette Implementation

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















