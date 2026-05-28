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







