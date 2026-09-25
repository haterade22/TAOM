---
paths:
  - "Main/**/Hooks/**"
  - "Main/**/Patches/**"
  - "Main/**/*Patch.cs"
---

# Harmony Patch Rules

## Before writing ANY patch: read the lessons file (MANDATORY, and it is the step that gets skipped)

Read **[`docs/reviews/lessons/harmony-il.md`](../../docs/reviews/lessons/harmony-il.md)** before
writing or modifying a Harmony patch. It is the accumulated trap list for this exact subsystem, and
it is cheaper than re-deriving a trap from a crash or from a review round.

**This is not boilerplate.** On 2026-08-26 the UncapturableHeroes feature shipped two defects into
review that were already written down in that file, one of them six days old: a defer-on-error catch
that handed a hero back to vanilla capture after the patch had already made him a fugitive (the
lesson's own worked example is the same actor in the same state), and a `false`-returning prefix that
dropped a safety gate its sibling seam applied. CLAUDE.md's general "read the category file" line did
not fire because it is general; this one loads when you open the file you are about to break. RCA:
`docs/reviews/rca-uncapturable-heroes-2026-08-26.md`.

Append to the lessons file after a review, but the append is the cheap half. The read is the half
that prevents the bug.

## Before editing an EXISTING patch: also read its registry entry

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
- Every caller of the target, with the actor each one supplies, whenever the patch infers an actor
  the signature does not carry (the player, the killer, the owner). Write that caller list into the
  patch doc beside the claim. 2026-09-14: the blood-feud relation seam has no executor parameter;
  the port arrived from the player-only caller, wrote "always the player", and the AI-executes-
  player-kin caller ran the kinslaying multiplier against the bereaved (`lessons/harmony-il.md`).

## Patch Types
- **Prefix** — Runs before original method. Return `false` to skip original.
- **Postfix** — Runs after original method. Can modify `__result`.
- **Transpiler** — Modifies IL instructions. Most fragile — use sparingly.
- **Finalizer**: runs after the original on every call, with a null `__exception` when nothing threw. `return null` swallows. Returning the exception makes Harmony `throw` it (whenever any finalizer on the method returns a value), which erases the throw site, so hand it back as `return RethrowStackPreserver.PreserveForRethrow(__exception, null);`. An observe-only finalizer should be `void`, which keeps Harmony's `rethrow` and the trace. Why: `lessons/harmony-il.md` "A value-returning finalizer that hands back its exception erases the throw site".

## Architecture Requirements
- Patches are **thin entry points**: delegate ALL logic to a service, directly or through an `IOnXxx` hook interface when the patch needs a narrow seam or a test fake
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
houses Patch31_SmartCavalryAI (+ its Patch31b sibling on `Formation.SetTargetFormation`) + Patch35_CompanionTactics; add yours there, never a fresh category.

## Common Pitfalls
- Collection modification during iteration — use `.ToList()` copy
- Null handling — TaleWorlds often expects `TextObject.Empty` not `null`
- Event timing — verify when events fire vs when state changes
- Static state — avoid unless using thread-local pattern
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

**Why this rule exists:** RCA `docs/reviews/rca-shader-precompilation-initial-zero-latch-2026-05-04.md`. The shader-precompilation patch's `_lastShaderCount = -1` collided with `Utilities.GetNumberOfShaderCompilationsInProgress() == 0` on the first frame after a warm-cache load. The patch fired its completion branch, killed its own latch, and produced an entire battle of blank loading screens that looked like the feature was completely broken.

**Sibling rule:** see `.claude/rules/csharp-architecture.md` "Entity State Matrix" for the lifecycle equivalent (*when does this entity die?*). Observation matrix and lifecycle matrix are different reviews — both are needed for static-state machines that observe external state.

## Latches & Toggle Gates (MANDATORY for any window/latch flag spanning multiple hooks)

A latch (`_windowActive`, `_inflight`, `BattleLoadLoadingWindow`-style static flags) that is OPENED in one hook and CLOSED in others has three failure modes that unit tests on the owning service structurally miss. All three shipped in one changeset (tournament-exit diagnostics, 2026-07-06 — two caught by deep-review Data Flow, the third by Codex one review later):

1. **Closer coverage per opener path.** Enumerate every code path that can OPEN the latch and verify a closer exists on EACH (or gate the opener to the paths the closers cover, e.g. `Campaign.Current != null`). An opener that fires for "any mission" with closers that only fire for "campaign missions" leaks the latch.
2. **Toggles gate I/O, never state transitions.** `if (!IsEnabled) return;` above a `_latch = false` line means a mid-window toggle-off latches the flag forever. Structure every latch-touching method as: state transition first (unconditional), then the `IsEnabled` gate, then logging/side effects.
3. **Verify "unconditional" at the OUTERMOST gate.** After fixing #2 inside the service, grep every CALLER of the fixed method — a hook-level `!svc.IsEnabled` early-out re-conditions the "unconditional" transition and the service-layer regression tests cannot see it. The fix is only done when the outermost gate on every call path passes state transitions through.

**Why this rule exists:** RCA `docs/reviews/rca-tournament-exit-hang-2026-07-06.md` (findings 1, 2, 4) — the exit-window latch shipped with campaign-only closers for an any-mission opener plus toggle-gated closes; the service-layer fix for the toggle gate was then bypassed by hook-level gates, caught only by the Codex pass. Master record: `docs/reviews/LESSONS-LEARNED.md` "State, Lifecycle & Save" → "Diagnostics latches".

## MissionBehavior lifecycle for a behavior TAOM adds (MANDATORY before overriding a lifecycle virtual)

Every TAOM mission behavior is added from `SubModule.OnMissionBehaviorInitialize` (`AddTaomBehavior`). On
the installed v1.5.3, `Mission.AfterStart` runs `OnBehaviorInitialize` over the behaviors ALREADY in the
list (`Mission.cs:3827`) and only then calls the submodules that add TAOM's (`:3831`); `AddMissionBehavior`
calls only `OnCreated` (`:4699`). So for a TAOM behavior:

| Virtual | Fires? | Use it for |
|---|---|---|
| `OnCreated` | yes, from `AddMissionBehavior` itself | per-mission state reset; `Mission` is already set |
| `OnBehaviorInitialize` | **never** | nothing (#606; `MissionBehaviorLifecycleTests` fails a new override) |
| `EarlyStart`, `AfterStart` | yes (`:3835`, `:3841`, after the add) | setup that needs the initialized mission; agents are not spawned yet |
| a lazy first-use gate in `OnAgentBuild` / `OnMissionTick` | yes | a mission gate (`CombatType` is set by native `InitializeMission` before any of these) |

A behavior added from a postfix on the mission-opening call (before `AfterStart`, the CustomBattles shape)
IS in the list at `:3827` and does get the callback. The general rule, second occurrence in four days
(`lessons/state-lifecycle-save.md`, "An engine lifecycle virtual's firing set is read from its caller"):
open the caller of any lifecycle virtual before wiring it, and quote the line in the override's comment.

## Which thread runs your target (MANDATORY before the first line of a patch)

Decompile the caller chain up to the thread that invokes the target. `[MBCallback]` methods are entered
from native, and native decides the thread. Verified on v1.4.8, identical on v1.5.3 (#592, #595, #634):

| Runs on | Engine entry points reached from it |
|---|---|
| Main thread | `Mission.OnTick` -> every `MissionBehavior.OnMissionTick`, then LAST `TickAgentsAndTeamsAsync`; `OrderController` (player orders); `Mission.SpawnAgent` -> `OnAgentBuild`; `MissionAgentPanicHandler.OnPreMissionTick` -> `Mission.OnAgentFleeing`; views, UI and input after `Mission.OnTick` returns |
| Async AI thread (`Mission.TickAgentsAndTeams`, an `[MBCallback]`) | `Agent.Tick` -> `AgentComponent.OnTick` (vanilla's `CommonAIComponent.OnTick` -> `Panic` -> `Mission.OnAgentPanicked` -> every `MissionBehavior.OnAgentPanicked`, and `SetAlarmState`), `HumanAIComponent.OnTick` -> `Agent.UseGameObject` -> `Mission.OnObjectUsed`, and its `ItemPickupTick` -> `Agent.StopUsingGameObject` -> `Mission.OnObjectStoppedBeingUsed`, `TickAsAI`; `Team.Tick` -> `TeamAI` -> `Formation.SetMovementOrder` / `SetTargetFormation` (also the retreat branch for the PLAYER's team); `Formation.Tick`; `MBSubModuleBase.AfterAsyncTickTick` |
| TWParallel worker pool (`Mission.AgentTickMT`) | `Agent.TickParallel` -> `AgentComponent.OnTickParallel` (`CommonAIComponent.OnTickParallel` -> `Agent.StartFadingOut` for routers), `HumanAIComponent.ParallelUpdateFormationMovement` -> `Agent.GetBaseFormationFrame` -> `Formation.GetOrderPositionOfUnit` |
| **Either: native decides, and off-main is observed** | `Mission.OnAgentRemoved` (then `Agent.OnRemove` -> every `AgentComponent.OnAgentRemoved`), `OnAgentDeleted`, `OnAgentHit`, `OnAgentShootMissile`, `OnAgentDismount`, `Agent.OnAgentAlarmedStateChanged`. A v1.4.8 player log caught `OnAgentRemoved`, `OnAgentShootMissile`, `OnAgentDismount`, `OnAgentAlarmedStateChanged`, `OnObjectUsed` and `OnObjectStoppedBeingUsed` off the main thread (#634; the thread ids cannot say whether on the async tick thread or a pool worker). Vanilla's own handlers take no lock, so if they are safe native must serialise these with the agent tick rather than the main thread; nothing managed proves which |

The frame order matters: `OnMissionTick` never overlaps the same frame's agent tick (the next frame's
`OnPreTick` waits in `WaitTickCompletion`), but everything on the main thread after `Mission.OnTick`
returns does, and `AfterAsyncTickTick` runs after `tickCompleted` is set, so it can overlap the next
frame's `OnMissionTick`. A stuck agent tick freezes the game with no exception; Patch91's
`[MissionStall]` line names the frame.

A patch on a target in the last three rows may run concurrently with the first row. Its shared state
takes a lock (`FormationLayoutService`, `CavalryChargeService`, `TroopStanceManager` are the shape), it
never registers a blow or spawns an agent, and a team filter is not a thread filter (Patch35 gated on
`PlayerTeam` and still ran on the async tick whenever that team's formations were AI-controlled). The
`??=` lazy-static pattern is tolerable there only for an idempotent resolve. A `MissionBehavior` or
`AgentComponent` callback is not main-thread by virtue of being a callback: every callback in the last
row can arrive off the main thread. A behavior or component that owns main-thread collections routes the write
through `DeferredCallbackQueue.RunOrDefer` (inline on the main thread, parked for the next
`OnMissionTick` anywhere else; `BehaviorTreeMissionLogic`, `BehaviorTreeAgentComponent` and
`MountDespawnMissionBehavior`, `CareerPerkMissionBehavior`, `WargMissionBehavior`, `SpatialGrid.Remove` are the
shape; #595, #634), or keeps the state behind a lock or in a concurrent collection when replay order does
not matter (`FieldCommissionMeritService.RegisterKill` locks because its map's insertion order breaks kill
ties; `SignatureAgentRoster` is a `ConcurrentDictionary`). Such a queue's owner marks the main thread itself (`MissionThreadGuard.MarkMainThread`
at the top of its `OnMissionTick`), and reports a parked callback at WARNING to the file log, never to an
on-screen logger: the report runs on the thread that raised the callback. A row in this table is a claim until a log line proves it:
the #595 audit trusted a "main thread" row for `OnAgentRemoved` that one player log falsified.
