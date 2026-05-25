---
paths:
  - "Main/**/Hooks/**"
  - "Main/**/Patches/**"
  - "Main/**/*Patch.cs"
---

# Harmony Patch Rules

## Research First (MANDATORY)
ALWAYS decompile the target method with `ilspycmd` before writing a patch. Verify:
- Exact method signature (parameters, return types, access modifiers)
- Whether the method is virtual, sealed, or static
- Correct namespace and class hierarchy
- Method existence in Bannerlord v1.4.5

## Patch Types
- **Prefix** — Runs before original method. Return `false` to skip original.
- **Postfix** — Runs after original method. Can modify `__result`.
- **Transpiler** — Modifies IL instructions. Most fragile — use sparingly.

## Architecture Requirements
- Patches are **thin entry points** — delegate ALL logic to services via `IHookInterface`
- Entry point files MUST be <150 lines (ADR-002)
- Resolve services from IoC container, never instantiate directly
- Use thread-local state pattern for multi-patch coordination

## Patch Organization
- Place in `Main/Features/{FeatureName}/Hooks/` directory
- Name: `{TargetClass}{TargetMethod}Patch.cs`
- Register in `SubModule.cs` patch categories (Patch0 through Patch6)

## Common Pitfalls
- Collection modification during iteration — use `.ToList()` copy
- Null handling — TaleWorlds often expects `TextObject.Empty` not `null`
- Event timing — verify when events fire vs when state changes
- Static state — avoid unless using thread-local pattern
- **Reflection in hot paths** — `AccessTools.Method` / `AccessTools.Field` lookups MUST be cached in a static field during `Initialize()`, never resolved inside `Prefix()`/`Postfix()`. Guard spawning calls the patch ~20x per settlement visit; uncached reflection means ~20 redundant lookups per entry.

## Static State Machines: Sentinel-Collision Check (MANDATORY)

When a patch holds static state across frames AND drives that state from polling external values (engine counts, file sizes, MBObjectManager queries, vanilla VM properties), enumerate the four boundary states BEFORE writing the change-detection logic:

| # | State | Typical value |
|---|-------|---------------|
| 1 | Sentinel / uninitialized (set by `Reset...()` / `Initialize()`) | `-1`, `null`, `default(T)`, empty |
| 2 | First real observation (poll returns this BEFORE work has begun) | `0`, `false`, empty collection |
| 3 | In-progress values | the range during normal operation |
| 4 | Terminal value (completion) | often the same encoding as state 2 |

**The trap:** state 2 and state 4 frequently share the same encoding (e.g. `0`). The change-detection comparison sees `_lastValue = -1`, observes `0`, and concludes "value changed, terminal state reached" — even though the polled subsystem simply hadn't started yet.

**The rule:** if your patch acts on a "sentinel → terminal" transition (cleanup, latch reset, `EndGame()` call, anything irreversible-for-this-cycle), require an additional `_hasObservedWork` boolean flag set the first time you observe a state-3 value. Only fire the terminal-state action when `current == terminal && _hasObservedWork`.

**Why this rule exists:** RCA `docs/reviews/rca-shader-precompilation-initial-zero-latch-2026-05-04.md`. The shader-precompilation patch's `_lastShaderCount = -1` collided with `Utilities.GetNumberOfShaderCompilationsInProgress() == 0` on the first frame after a warm-cache load. The patch fired its completion branch, killed its own latch, and produced an entire battle of blank loading screens that looked like the feature was completely broken.

**Sibling rule:** see `.claude/rules/csharp-architecture.md` "Entity State Matrix" for the lifecycle equivalent (*when does this entity die?*). Observation matrix and lifecycle matrix are different reviews — both are needed for static-state machines that observe external state.
