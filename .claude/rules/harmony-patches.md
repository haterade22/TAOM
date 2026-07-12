---
paths:
  - "Main/**/Hooks/**"
  - "Main/**/Patches/**"
  - "Main/**/*Patch.cs"
---

# Harmony Patch Rules

## Before editing ANY patch: read its registry entry

Every patch category's rationale, history, crash-guard semantics, and RCA links live in
[`docs/reference/harmony-patch-registry.md`](../../docs/reference/harmony-patch-registry.md) —
read the target patch's section before changing it. CLAUDE.md keeps only the thin routing table
(category | feature | target | status).

## Research First (MANDATORY)
ALWAYS decompile the target method with `ilspycmd` (`pwsh tools/taom-src.ps1 path <Type>`) before writing a patch. Verify:
- Exact method signature (parameters, return types, access modifiers)
- Whether the method is virtual, sealed, or static
- Correct namespace and class hierarchy
- Method existence in the installed engine version (see `.claude/pinned-game-version.txt`)

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
- Register in a `SubModule.cs` patch category (`[HarmonyPatchCategory]` + the SubModule apply batch — verify the apply TIMING fits the target: most apply in `OnGameInitializationFinished`, but targets that fire during new-game load/main menu need `OnSubModuleLoad`, e.g. `Patch58`/`Patch61`; see the registry)

## MovementOrder postfixes: use the shared deferred category (MANDATORY)
Any patch with `MovementOrder` in its postfix signature MUST join `Patch_MissionTime_SetMovementOrder`
(applied once from `OnMissionBehaviorInitialize`), because `MovementOrder.cctor` reads
`Mission.Current.CurrentTime` — null in `OnSubModuleLoad`/`OnGameInitializationFinished`. It currently
houses Patch31_SmartCavalryAI + Patch35_CompanionTactics; add yours there, never a fresh category.

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

## Latches & Toggle Gates (MANDATORY for any window/latch flag spanning multiple hooks)

A latch (`_windowActive`, `_inflight`, `BattleLoadLoadingWindow`-style static flags) that is OPENED in one hook and CLOSED in others has three failure modes that unit tests on the owning service structurally miss. All three shipped in one changeset (tournament-exit diagnostics, 2026-07-06 — two caught by deep-review Data Flow, the third by Codex one review later):

1. **Closer coverage per opener path.** Enumerate every code path that can OPEN the latch and verify a closer exists on EACH (or gate the opener to the paths the closers cover, e.g. `Campaign.Current != null`). An opener that fires for "any mission" with closers that only fire for "campaign missions" leaks the latch.
2. **Toggles gate I/O, never state transitions.** `if (!IsEnabled) return;` above a `_latch = false` line means a mid-window toggle-off latches the flag forever. Structure every latch-touching method as: state transition first (unconditional), then the `IsEnabled` gate, then logging/side effects.
3. **Verify "unconditional" at the OUTERMOST gate.** After fixing #2 inside the service, grep every CALLER of the fixed method — a hook-level `!svc.IsEnabled` early-out re-conditions the "unconditional" transition and the service-layer regression tests cannot see it. The fix is only done when the outermost gate on every call path passes state transitions through.

**Why this rule exists:** RCA `docs/reviews/rca-tournament-exit-hang-2026-07-06.md` (findings 1, 2, 4) — the exit-window latch shipped with campaign-only closers for an any-mission opener plus toggle-gated closes; the service-layer fix for the toggle gate was then bypassed by hook-level gates, caught only by the Codex pass. Master record: `docs/reviews/LESSONS-LEARNED.md` "State, Lifecycle & Save" → "Diagnostics latches".
