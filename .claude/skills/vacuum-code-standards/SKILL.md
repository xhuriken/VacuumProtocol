---
name: vacuum-code-standards
description: VacuumProtocol C# rules - English XML docs (Description/Context/Justification, Tooltip "Role:"), Mirror authority patterns, fail-loud checks, Unity-safe dead-code removal and tech-debt thresholds. Load before writing, reviewing, documenting or deleting any script in Assets/1_Scripts.
---

# VacuumProtocol code standards

Applies to `Assets/1_Scripts/**`. Third-party folders (`Assets/Mirror`, `Assets/Plugins`, `Assets/Shapes`, `Assets/ParrelSync`, `Networking/Lobby/SteamManager.cs`) are never edited.

## 1. Documentation (English only)

Every type, serialized field and public member is documented. Reference files: `Player/Mechanics/PlayerHealth.cs`, `Player/Visuals/PlayerHealthScreen.cs`.

```csharp
/// <summary>
/// Description: What it is / does, one sentence.
/// Context: Where it lives (prefab, GameObject, server/client side).
/// Justification: Why it is built this way (the non-obvious choice).
/// </summary>
public class Foo : NetworkBehaviour
{
    [Title("Section")]
    [Tooltip("Role: What this value controls, with units.")]
    [SerializeField] private float _speed = 3f;

    /// <summary>One or two lines: what it returns or changes.</summary>
    public void Bar() { }
}
```

- Small nested structs/enums get a short D/C/J too.
- Comments explain WHY (physics hacks, network authority, magic numbers). Delete comments that restate the code.
- Log and exception messages: English, prefixed `[ClassName]`, include `gameObject.name`.
- Writing docs for existing code: describe what the code DOES, never invent intent. If a comment contradicts the code, fix the comment to match the code and flag the mismatch to the user.

## 2. Fail loudly

From `.agents/AGENTS.md`: no silent fallbacks. A missing reference, component or invalid value logs `Debug.LogError(..., this)` (or throws) and disables the component. Never default to `null`, `Vector3.zero` or skip silently.

## 3. Mirror

- Gameplay state changes only on the server: `[Server]` methods, `[Command]` from the owning client. `[SyncVar]` is written only on the server.
- Visual/audio reactions run on clients from SyncVar hooks or events raised by them. Hooks also run on the host; a dedicated server never runs hooks, so a server-side event raised in a `[Server]` method must be guarded with `if (!isClient)` to avoid firing twice on the host (pattern: `PlayerHealth.SetDeadState`).
- Hooks do not fire for the initial spawn value: visual listeners read the current value in `OnEnable`/`OnStartClient`.
- Never send a `[ClientRpc]` or `[Command]` every frame (continuous damage, aiming). Sync a value (SyncVar with threshold) or batch.
- Guard with `isServer` / `isLocalPlayer` / `isOwned` before acting.

## 4. Per-instance visuals

Change one renderer's shader values with a `MaterialPropertyBlock` scoped to the material slot (`SetPropertyBlock(block, index)`), never `sharedMaterial`, and only when the value changed. Continuous animation belongs in the shader (`_Time`).

## 5. Removing code safely (Unity-aware)

A symbol with zero C# references may still be live. Before deleting, check all of:
- Unity messages called by reflection: `Awake`, `Start`, `Update`, `OnEnable`, `OnTrigger*`, `OnCollision*`, `OnValidate`, `OnDrawGizmos`, `Reset`...
- Mirror: SyncVar `hook =` targets, `[Command]`, `[ClientRpc]`, `[TargetRpc]`, `OnStart*`/`OnStop*` overrides.
- Odin: `[Button]`, `[OnValueChanged(nameof(...))]`, `[ShowIf(nameof(...))]`, `[ValidateInput]` targets.
- UnityEvents wired in the Inspector: grep the method name in `Assets/**/*.unity`, `*.prefab`, `*.asset` (`m_MethodName:`).
- Scripts: grep the GUID from the `.cs.meta` in `Assets/**/*.prefab|*.unity|*.asset`.
- Input System callbacks (`OnMove`, `OnLook`...) bound by name through `PlayerInput`.

Delete assets through the Editor (`unity command asset delete ...`) so the `.meta` goes with them. Run `unity recompile` after every removal batch.

## 6. Tech-debt thresholds (flag, do not auto-split)

- File over 500 lines or method over 60 lines: say so in the review and propose a split only if two clear responsibilities exist.
- Same helper written in two files: move it to the shared base (e.g. `UICustomButtonBase`) or a static utility.
- `GetComponent`/`Find*`/LINQ/`new List` inside `Update`/`FixedUpdate`: cache or hoist.
- `TODO`/`FIXME` without an owner or card: list them in the review.

## 7. Before saying "done"

1. `unity recompile --format json` reports 0 errors.
2. Play-mode check for gameplay/network changes when possible (host via `NetworkManager.singleton.StartHost()` in `script eval`).
3. `DEVELOPMENT_LOG.md` entry (French, Goal / Changes / Justification) for features.
4. Commit with plain `git commit -m "type(scope): ..."` (French message, no AI attribution).
