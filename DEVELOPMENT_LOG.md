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
