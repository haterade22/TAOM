---
paths:
  - "Main/**/Hooks/**"
  - "Main/**/Patches/**"
  - "Main/**/*Patch.cs"
  - "Client/**/Hooks/**"
  - "Client/**/Patches/**"
  - "Client/**/*Patch.cs"
  - "Dependencies/Foundation/**"
---

# Harmony Patch Rules

## Before writing any patch: check the lessons

List the headings of [`docs/reviews/lessons/harmony-il.md`](../../docs/reviews/lessons/harmony-il.md) (`grep -n "^#"`) and read the entries matching your target type and patch shape (finalizer, prefix returning false, transpiler, latch). Do not read the file whole. Append after a review.

## Before editing an EXISTING patch: also read its registry entry

Every patch category's rationale, history, crash-guard semantics, and RCA links live in
[`docs/reference/harmony-patch-registry.md`](../../docs/reference/harmony-patch-registry.md) —
read the target patch's section before changing it.

## Research First (MANDATORY)
ALWAYS decompile the target method with `ilspycmd` (`pwsh tools/taom-src.ps1 path <Type>`) before writing a patch. Verify:
- Exact method signature (parameters, return types, access modifiers)
- Whether the method is virtual, sealed, or static
- Correct namespace and class hierarchy
- Method existence in the installed engine version (see `.claude/pinned-game-version.txt`)
- Every caller of the target, with the actor each one supplies, whenever the patch infers an actor
  the signature does not carry (the player, the killer, the owner). Write that caller list into the
  patch doc beside the claim. 2026-09-14: the blood-feud relation seam has no executor parameter;
  the port arrived from the player-only caller, wrote "always the player", and the AI-executes-
  player-kin caller ran the kinslaying multiplier against the bereaved (`lessons/harmony-il.md`).

## Patch Types
- **Prefix** — Runs before original method. Return `false` to skip original.
- **Postfix** — Runs after original method. Can modify `__result`.
- **Transpiler** — Modifies IL instructions. Most fragile — use sparingly.
- **Finalizer**: runs after the original on every call, with a null `__exception` when nothing threw. `return null` swallows. Returning the exception makes Harmony `throw` it (whenever any finalizer on the method returns a value), which erases the throw site, so hand it back as `return RethrowStackPreserver.PreserveForRethrow(__exception, null);` (gate: `FinalizerRethrowGateTests`). An observe-only finalizer should be `void`, which keeps Harmony's `rethrow` and the trace. Why: `lessons/harmony-il.md` "A value-returning finalizer that hands back its exception erases the throw site". Before a finalizer or shield rethrows what it used to swallow, follow the exception to its next catcher: out of any `OnMissionTick` a rethrow hangs the next frame (`lessons/harmony-il.md`, co-op safety nets 2026-10-21).
- **Prefix and finalizer pair**: the link between them is Harmony's `__state` (one zeroed local per call, set by the prefix before anything can throw or return early), never a static, a count or a thread-keyed flag. Harmony reruns every finalizer when a later one throws, and PatchShield strips prefixes but never finalizers, so a finalizer can run without its prefix. Test the pair through real Harmony. Why: `lessons/harmony-il.md` "a bracket must survive its prefix being stripped".
- **No finalizer of your own is not "no finalizer"**: a patch on an engine method usually also carries PatchShield's finalizer (a late-batch patch from the second game start; never under co-op or on an excluded target). Word a "nothing is swallowed" or rethrow claim for the method's whole finalizer set. Why: `lessons/harmony-il.md` "A new patch on an engine method also gets PatchShield's finalizer".

## Architecture Requirements
- Patches are **thin entry points**: delegate ALL logic to a service, directly or through an `IOnXxx` hook interface when the patch needs a narrow seam or a test fake
- Entry point files MUST be <150 lines (ADR-002)
- Resolve services from IoC container, never instantiate directly
- A prefix and finalizer on one method share `__state`. Patches on two different methods of one call chain share a `[ThreadStatic]`, cleared in a finalizer, never a Postfix

## Patch Organization
- Place in `Main/Features/{FeatureName}/Hooks/`; a patch that names a TAOM.Client type goes in
  `Client/Features/{FeatureName}/Hooks/` (ADR-013; the patch index reads both assemblies)
- A core patch class whose target, patch-method signature or `TargetMethod` names a client-only assembly carries `[ClientOnly]`, so a dedicated server never indexes it: gate 4, `ServerLoadGateTests.PatchClassesReferencingClientAssemblies_AreClientOnly`
- Name: `{TargetClass}_{TargetMethod}_Patch.cs`; a numbered category file is `Patch<N>_<Target>.cs`.
- A new patch number names one feature, and its category gets a `## <category>` heading in the registry: `PatchCategoryNumberTests` pins both.
- Register in a `SubModule.cs` patch category (`[HarmonyPatchCategory]` + the SubModule apply batch — verify the apply TIMING fits the target: most apply in `OnGameInitializationFinished`, but targets that fire during new-game load/main menu need `OnSubModuleLoad`, e.g. `Patch58`/`Patch61`; see the registry)

## MovementOrder postfixes: use a deferred mission-time category (MANDATORY)
Any patch with `MovementOrder` in its postfix signature MUST go in its feature's own `Patch_MissionTime_<Feature>`
category, applied at the first mission (`OnMissionBehaviorInitialize`) because `MovementOrder.cctor` reads `Mission.Current.CurrentTime`: null in
`OnSubModuleLoad`/`OnGameInitializationFinished`. One category per feature, so a class that fails turns off only its own
feature (split 2026-11-13). A feature module declares its category at `ApplyPhase.FirstMission`, as `MarshalsWordModule` does:
that is the default route. `SubModule.cs` is single-owner and applies only the two existing ones, `Patch_MissionTime_SmartCavalryAI`
and `Patch_MissionTime_CompanionTactics`. Document it under its own `## <category>` registry heading, or in the section `Patch_MissionTime_SetMovementOrder`
plus a `NoRegistryHeading` row in `PatchCategoryNumberTests`; otherwise that test fails.

## Common Pitfalls
- Collection modification during iteration — use `.ToList()` copy
- Null handling — TaleWorlds often expects `TextObject.Empty` not `null`
- Event timing — verify when events fire vs when state changes
- Static state: avoid it; off the main thread it takes a lock or a concurrent collection ("Which thread runs your target").
- Writing a TaleWorlds-owned ViewModel property from a patch or hook: read `.claude/rules/gui-vm-properties.md` first. A guarded setter makes a same-value write a no-op, and a reflected field write skips the change notification.
- **No `ResetForUnload()` needed for a new patch.** Nothing reloads TAOM inside one process: the engine's only caller of `OnSubModuleUnloaded` is `Module.FinalizeModule` at shutdown (v1.5.3 `Module.cs:242-248,1296-1314`), and a rebuild means stopping the game and pressing Play again (Mike, 2026-09-24). A static service cache in a patch class therefore lives exactly as long as the process. The existing `ResetForUnload()` methods stay until their class is next touched, and `ResetForUnloadSweepTests` still requires every one that exists to be called from `OnSubModuleUnloaded`. A reload tool appearing later would change this; then the three once-per-process flags in `SubModule.cs` need resetting too (sprint finding COMP-05).
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

## Latches & Toggle Gates (MANDATORY for any window/latch flag spanning multiple hooks)

A latch (`_windowActive`, `_inflight`, `BattleLoadLoadingWindow`-style static flags) that is OPENED in one hook and CLOSED in others has three failure modes that unit tests on the owning service structurally miss.

1. **Closer coverage per opener path.** Enumerate every code path that can OPEN the latch and verify a closer exists on EACH (or gate the opener to the paths the closers cover, e.g. `Campaign.Current != null`). An opener that fires for "any mission" with closers that only fire for "campaign missions" leaks the latch.
2. **Toggles gate I/O, never state transitions.** `if (!IsEnabled) return;` above a `_latch = false` line means a mid-window toggle-off latches the flag forever. Structure every latch-touching method as: state transition first (unconditional), then the `IsEnabled` gate, then logging/side effects.
3. **Verify "unconditional" at the OUTERMOST gate.** After fixing #2 inside the service, grep every CALLER of the fixed method — a hook-level `!svc.IsEnabled` early-out re-conditions the "unconditional" transition and the service-layer regression tests cannot see it. The fix is only done when the outermost gate on every call path passes state transitions through.

## MissionBehavior lifecycle for a behavior TAOM adds (MANDATORY before overriding a lifecycle virtual)

A behavior added through `AddTaomBehavior` gets `OnCreated`, `EarlyStart` and `AfterStart`, and never `OnBehaviorInitialize` (`MissionBehaviorLifecycleTests`; order in [mission-and-missionbehavior-lifecycle.md](../../docs/reference/engine/mission-and-missionbehavior-lifecycle.md)). Open the caller of any lifecycle virtual before wiring it.

`AddTaomBehavior` adds a behavior to every mission, Custom Battle included, where there is no campaign: its
`EarlyStart`/`AfterStart` reads no campaign static unguarded (adapters.md), keeps every adapter call inside a
`try` (an exception leaving `AfterStart` is survived by Patch103 and cuts that setup short; with its toggle off it
restarts the mission load every frame, #699), and a change that makes
such code run for the first time gets one Custom Battle run.

A static a patch reads clears from both teardown paths, `OnMissionScreenFinalize` (or `OnEndMission`) and `OnRemoveBehavior`, through one safe-to-repeat method. A view's finished session is dropped there too: the screen can still tick a removed view (`MissionScreen.cs:3688`).

## Which thread runs your target (MANDATORY before the first line of a patch)

Read the caller chain up to the thread first. `[MBCallback]` methods run where native decides; off-main is observed for `OnAgentRemoved`, `OnAgentHit` and siblings. On any off-main target shared state takes a lock or a concurrent collection, never registers a blow or spawns, and a team filter is not a thread filter. Table: [mission-frame-threads-and-native-costs.md](../../docs/reference/engine/mission-frame-threads-and-native-costs.md) "Which thread runs your target".
