# Battle Load Diagnostics

## Overview

`BattleLoadDiagnostics` phase-stamps the **entire attack → battle-playable lifecycle** to the TAOM debug log (`Logs/taom_debug_*.log`) and runs a background-thread **stall watchdog**. When a battle gets stuck on the loading screen (the intermittent infinite-load hang), the **last line written before the freeze names the stuck phase** — and for the equipment phase, the exact agent and the item whose collision mesh (`bo_` / `shield_body_name`) is missing.

## Why This Exists

Users report that entering a battle *sometimes* hangs forever on the loading screen — **no crash, no stack trace**, the battle never initializes. It is intermittent, happens on user machines, and cannot be reproduced locally. A hang ≠ a crash: a crash throws (and TAOM's `CrashReport` feature already captures it); a hang means the **main thread is blocked**, so nothing is thrown and the existing crash pipeline never fires. The existing scene-reference audits (`audit_battle_scenes.py`, `audit_scene_names.py`) only catch *crashes* from missing scene folders, not this hang.

A missing `bo_` collision body on a weapon/shield in `LOTRLOME_Armory` is a **confirmed** cause (#352, 2026-07-16), no longer just the leading hypothesis: `PreloadHelper.WaitForMeshesToBeLoaded` polls every registered physics-body name and only exits once each resolves, so one unresolvable name spins the main thread forever. A user traced a permanent siege-load hang to exactly this with ClrMD; the culprit was a one-token `body_name` typo (the asset shipped fine). The engine also logs it itself — `rgl_log_errors_*.txt` contains `get_object failed for body: bo_X`. Catch it offline with the companion tool [mesh-ref-validation.md](mesh-ref-validation.md), and note its lesson: a clean run only means "clean within the scanned scope".

Confirmed ≠ exclusive — the hang can still be scene-side, and #352 hung in *preload*, not agent-spawn. This feature stays **cause-agnostic**: it localizes *any* battle-load hang by phase, so the next user report comes with a log that points at the culprit instead of a shrug.

## Architecture

### The lifecycle phases

Each phase is a thin Harmony hook (or `MissionLogic`) that delegates one call to `IBattleLoadDiagnosticsService`, which writes a consistent line:

```
[BattleLoad] seq=NN t=+1234ms phase=<PhaseName> <detail>
```

`seq` is a monotonic counter (`Interlocked.Increment`); `t=+Nms` is `Stopwatch` elapsed since the encounter began. A large gap between two consecutive `seq` lines is the stall location.

| # | Phase | Hook | TaleWorlds seam (v1.4.7) |
|---|-------|------|--------------------------|
| 1 | `EncounterStart` | `PlayerEncounter_Start_Patch` (Postfix) | `PlayerEncounter.Start()` — resets the lifecycle clock |
| 2 | `MissionOpenNew` | `MissionState_OpenNew_Patch` (Prefix) | `MissionState.OpenNew(string, MissionInitializerRecord, …)` — logs scene + attacker/defender/sizes/side from `PlayerEncounter.Current` |
| 2b | `MissionOpenNewDone` | `MissionState_OpenNew_Patch` (**Postfix**) | `OpenNew` returned — mission constructed + state pushed |
| 2c | `LoadMissionBegin` | `MissionState_LoadMission_BattleLoad_Patch` (Prefix) | `MissionState.LoadMission` (private) — the NEXT tick |
| 2d | `ResourceClearOldBegin` / `Done` | `Utilities_ClearOldResourcesAndObjects_BattleLoad_Patch` (Prefix + Postfix) | `Utilities.ClearOldResourcesAndObjects()` — **the one native call in the window** |
| 3 | `BattleSceneSelected` | `BattleSceneSelection_Patch` (Postfix) | `DefaultSceneModel.GetBattleSceneForMapPatch(MapPatchData, bool)` — logs `mapIndex → sceneId`. Fires BEFORE phase 2, and only for map-patch terrain — absent on village/town scenes |
| 4 | `MissionInitialize` | `Mission_Initialize_BattleLoad_Patch` (Prefix) | `Mission.Initialize` (public) — opens the loading window |
| 4a | `MissionInitializeDone` | `Mission_Initialize_BattleLoad_Patch` (**Postfix**) | `Mission.Initialize` returned — brackets the native `MBAPI.IMBMission.InitializeMission`, which is the *whole* body (`Mission.cs:1798-1809`) |
| — | *(no line)* | `MissionState_TickLoading_BattleLoad_Patch` (Prefix) | `MissionState.TickLoading(float)` (**private**) — a **counter, never a marker**. See below |
| 4d | `FinishMissionLoadingBegin` | `MissionState_FinishMissionLoading_BattleLoad_Patch` (Prefix) | `MissionState.FinishMissionLoading()` (**private**) — the native `IsLoadingFinished` poll finally returned true. Carries `polls=` / `waitMs=` |
| 4b | `MissionAfterStartBegin` / `Done` | `Mission_AfterStart_BattleLoad_Patch` (Prefix + Postfix) | `Mission.AfterStart()` — runs `OnMissionBehaviorInitialize` for **every** submodule. Called from *inside* `FinishMissionLoading` |
| 4e | `FinishMissionLoadingDone` | `MissionState_FinishMissionLoading_BattleLoad_Patch` (**Postfix**) | `FinishMissionLoading` returned — `OnMissionLoadingFinished` + `Scene.ResumeLoadingRenderings` done |
| 4c | `TaomBehaviorsBegin` / `TaomBehaviorAdded` / `TaomBehaviorsDone` | `AddTaomBehavior` helper in `SubModule.OnMissionBehaviorInitialize` (no patch) | TAOM's own behaviors, each stamped by name |
| 5 | `AgentEquipBegin` / `AgentEquipOk` | `Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch` (Prefix + Postfix) | `Agent.EquipItemsFromSpawnEquipment(bool,bool,bool,int)` — **the money hook** |
| 6 | `BattlePlayable` | `BattleLoadPhaseBehavior : MissionLogic` (first `OnMissionTick`) | closes the loading window — load succeeded |

All hooks share the Harmony category `Patch43_BattleLoadDiagnostics`. Phases 4 and 5 coexist with the pre-existing prefixes on the same methods (`Patch16_AtmospherePersistence` on `Mission.Initialize`, `Patch23_BannerColorPersistence` on `EquipItemsFromSpawnEquipment`) — Harmony runs all of them.

#### Why 2b–2d and 4b–4c exist (the 2026-07-16 blind window)

Phase 2 is a **Prefix**, so its line is written *before* `OpenNew`'s body runs. Until 2026-07-16 the next stamp was phase 4 — and between them sits `OpenNew`'s whole body, a **tick boundary**, `LoadMission`, every behavior's `OnMissionScreenPreLoad`, and a native resource clear. A player CTD at Nan Angren (TAOM v2.0.12, vanilla scene `battania_village_c`) produced a log ending at `MissionOpenNew`, which was consistent with *every* one of those and therefore proved none of them. The engine order is:

```
MissionOpenNew → MissionOpenNewDone → [tick] → LoadMissionBegin →
  ResourceClearOldBegin → ResourceClearOldDone → MissionInitialize →
  MissionAfterStartBegin → TaomBehaviorsBegin → TaomBehaviorAdded ×N →
  TaomBehaviorsDone → MissionAfterStartDone → AgentEquip… → BattlePlayable
```

Two of these earn their keep for a specific reason. `ResourceClearOld*` brackets the only **native** call in the window — the shape that access-violates, e.g. when a previous mission's exit left the native heap corrupt (cf. Patch62 / #339); it has exactly one caller in the shipping build, so it adds no noise. And `MissionAfterStartBegin` is what makes the TAOM stamps **exonerating**: `Mission.AfterStart` iterates every loaded submodule, so the gap between it and `TaomBehaviorsBegin` is *other mods'* behavior construction. Without it, "died after Initialize" could be pinned on nobody.

Verified engine seams (v1.4.7): `MissionState.cs:302` (`OpenNew`) · `:235` (`private void LoadMission()`) · `:241` (native clear) · `:243` (`Initialize`) · `:345` → `Mission.cs:3799` (`AfterStart`) → `:3815` (`OnMissionBehaviorInitialize` per submodule).

#### Why 4a/4d/4e exist — the 11.9-second bucket split (2026-08-07)

`MissionInitialize → MissionAfterStartBegin` was itself a blind window, and on an instrumented
29-second load it held **11.9 s** — the single largest unattributed span in the whole lifecycle. The
engine decomposes it into exactly three things (`MissionState.cs:221-350`, verified against the
installed v1.4.7):

| Bucket | Span | What runs in it |
|---|---|---|
| **1** | `MissionInitialize` → `MissionInitializeDone` | the native `MBAPI.IMBMission.InitializeMission` call — scene, physics, terrain construction |
| **2** | `MissionInitializeDone` → `FinishMissionLoadingBegin` | N × `TickLoading` frames polling the native `Mission.IsLoadingFinished` |
| **3a** | `FinishMissionLoadingBegin` → `MissionAfterStartBegin` | `Scene.SetOwnerThread` + two warm-up `Mission.Tick(0.001f)` calls + `Handler.OnMissionAfterStarting` |
| **3b** | `MissionAfterStartBegin` → `MissionAfterStartDone` | `Mission.AfterStart` — including the whole `AgentEquip` burst |
| **3c** | `MissionAfterStartDone` → `FinishMissionLoadingDone` | `OnMissionLoadingFinished` + `Scene.ResumeLoadingRenderings` |

There is now **no unattributed span** between `MissionInitialize` and `BattlePlayable`.

**`TickLoading` is a counter hook that never logs, and that is the design.** It fires once per frame
while the mission loads; at 60 fps a 12-second wait is ~720 calls, so a marker there would produce
far more noise than the blind spot it closes. Its prefix does one `Interlocked.Increment` and
deliberately does **not** read `IsEnabled` (that resolves MCM's static `Instance` every frame, and
the count is *state*, not I/O — it must survive a mid-load toggle). The result rides one token pair
on the `FinishMissionLoadingBegin` line. Reading that pair is the whole point:

| Reading | Meaning |
|---|---|
| `polls=1`, `waitMs` large | the block was **inside one frame** — a blocking native spin, the #352 `WaitForMeshesToBeLoaded` shape. Not "async waiting": a stalled main thread |
| `polls` ≈ `waitMs`/16 | genuine multi-frame async streaming at ~60 fps; the main thread is healthy and the engine waits on disk / worker threads |
| `polls` ≫ 1 but `waitMs`/`polls` ≫ 16 ms | frames are running but each is long — per-frame work inside the load |
| **`polls=0`** | **the `TickLoading` binding FAILED.** It does *not* mean "there was no wait." Pinned by `Patch43LoadPhaseBindingTests` |

`waitMs` is **omitted entirely** — never a fabricated `0` — when `MissionInitializeDone` was not
observed, the same never-invent-a-zero rule the `MemStats()` process tokens follow.

All three new markers carry `MemStats()`, which is what makes them answer a question no other
instrument can: whether the load stall and the process-commit growth are **one** problem. A `privMB`
curve that rises steeply across the dominant bucket means the stall *is* resource residency; a flat
`privMB` across an 11.9-second wait means the stall is I/O or CPU and the memory work is separate.
The refutation is as valuable as the confirmation.

`FinishMissionLoading` and `TickLoading` are both **private**, bound by string exactly like the
sibling `MissionState.LoadMission` / `Mission.BuildAgent` / `MissionState.OnFinalize` patches in this
category. `AccessTools.Method` searches non-public, so `HarmonyPatchBindingTests` covers them with no
wiring; `Patch43LoadPhaseBindingTests` adds named checks so a drift failure says *which* engine
method moved.

#### The stopwatch was dead in custom battles (fixed 2026-08-07)

`_stopwatch` was started **only** by `LogEncounterStart` / `ResetLifecycle`, and both are reachable
only from `PlayerEncounter_Start_Patch` — which is campaign-only. So in a **custom battle** the clock
never ran and *every* `[BattleLoad]` line read `t=+0ms`. Custom battle is the primary station for
creature, mount and equipment testing and for the commit-attribution matrix, so the timing instrument
was dead exactly where it was most needed.

The fix adds the existing `if (!_stopwatch.IsRunning) _stopwatch.Restart();` idiom to
`LogMissionOpenNew` (the universal funnel — it fires for campaign, custom battle and arena alike) and
to `LogMissionInitialize`. `IsRunning`-guarded, so the campaign path keeps `EncounterStart` as its
origin and every existing delta is unchanged.

**Residual, documented rather than fixed:** a second mission in the same process inherits the running
clock, so the `t=+` *absolute* keeps growing across chained missions. **Gaps stay valid** — read
deltas, never absolutes.

### The money hook (phase 5)

The prefix builds an `EquipmentSnapshot` (via `IEquipmentSnapshotAdapter`, reading `Agent.SpawnEquipment` — the *full* `Equipment` incl. armor + horse, NOT `Agent.Equipment` which is weapons-only) and logs the full loadout **before** the engine equips the agent. The postfix logs `AgentEquipOk` only **after** the engine returns. So:

- **`AgentEquipBegin` with a matching `AgentEquipOk`** → that agent's *equip call* returned fine.
- **`AgentEquipBegin` with NO matching `AgentEquipOk` (log ends here)** → the freeze is inside that agent's equipment spawn, and the dumped slots name the suspect — look for `bo=<null>` / `shieldBo=<null>`.
- **`AgentEquipOk` with NO matching `AgentBuildDone` (log ends here)** → the fault is in `Mission.BuildAgent`'s native tail. See phase 5b.

#### Phase 5c — the dump is per-loadout, the stamps are per-agent (2026-08-03)

A 429-agent arena audience drawn from 9 character kits emitted **1,146 `slot=` lines** — 186 KB, 29 % of a 644 KB session log — describing **18 distinct loadouts** (only 11 distinct *rows*; a loadout is a set of rows, so the two counts differ). The stamps have to be per-agent (they are the crash discriminator); the *dump* does not. So each distinct loadout is dumped once and every later agent wearing it carries only a `loadout=#N` token on its `AgentEquipBegin` line.

The key is the rendered slot rows **plus `race` / `monster` / `actionSet`** — deliberately *not* the character id. It has to mean "what the engine is about to assemble", because that is the thing that faults. Two consequences worth knowing before you read a log:

- **Divergence surfaces, it does not hide.** A mid-load `MatchEquipment` rewrite (`TaomTournamentModel.GetParticipantArmor`) yields a **new** id and a fresh dump mid-sequence, rather than being swallowed by an earlier agent in the same character.
- **Crash durability improves.** The DEBUG slot lines only ever reached disk because a following INFO flushed them. A deduped agent's block was written and flushed far *earlier* in the load, behind hundreds of subsequent synchronous flushes — strictly safer than the per-agent version.

The map is cleared at `Mission.Initialize` and `ResetLifecycle`, both **unconditionally** (gating the clear on `IsEnabled` would let a mid-load toggle-off strand a stale cache into the next load). Past `MaxTrackedLoadouts` (512) it stops growing and every agent dumps in full again — a load with that many distinct loadouts is already pathological, and unbounded growth on the spawn path is the worse failure.

#### Phase 5b — `AgentBuildDone`, and why `AgentEquipOk` was never enough (the 2026-08-02 blind window)

`AgentEquipOk` brackets one call. `Mission.BuildAgent` (`Mission.cs:4015`) keeps working on the same agent afterwards, all of it native, none of it stamped:

```csharp
agent.EquipItemsFromSpawnEquipment(...);   // :4034  ← the AgentEquipBegin/Ok bracket
agent.InitializeAgentRecord();             // :4035
agent.AgentVisuals.BatchLastLodMeshes();   // :4036   mesh/GPU batching
agent.PreloadForRendering();               // :4037
agent.SetActionChannel(0, ...);            // :4041   plays GetCurrentAction(0) on channel 0
agent.InitializeComponents();              // :4043
_activeAgents.Add(agent);                  // :4048
```

A CTD anywhere in there produced a log ending on `AgentEquipOk agent#N` — **indistinguishable from a death between two agents.** A Dunland tournament CTD (reporter FESTERLITTLE, `mission='TournamentFight' scene='arena_empire_a'`) ended exactly that way, and the 14-line range was as far as the log could narrow it. `Mission_BuildAgent_BattleLoad_Patch` is a postfix on `BuildAgent` (private in v1.4.7 — bound by string), so the two cases now read differently.

The `AgentEquipBegin` line also carries `race=`, `monster=` and `actionSet=`. A race/monster/action-set mismatch is the shape that access-violates in native mesh assembly with nothing logged, and it must sit on the line written *before* the engine touches the agent — a stamp that only fires afterwards is worthless for a crash.

`from=` names the engine method that built the agent, captured from a managed stack and bounded to `Agent.Index <= 2` (a stack capture is not free; the answer is only interesting at the head of the spawn sequence). Live output:

```
from=Agent.EquipItemsFromSpawnEquipment <- Mission.BuildAgent <- Mission.SpawnAgent
     <- MissionAudienceHandler.SpawnAudienceAgents <- MissionAudienceHandler.OnInit
     <- MissionAudienceHandler.EarlyStart
```

**Reading it:** frames are fully qualified on capture and shortened to `Type.Method`; Harmony's generated `_PatchN` wrappers are *normalised, not dropped* (a wrapper replaces the frame it stands for, so dropping it loses the method you want); consecutive duplicates collapse, because the wrapper and the original both appear once Harmony is in the chain. Budget is 6 frames — Harmony adds one per patched method in the chain.

> **The first cut of `from=` shipped useless, and the failure is instructive.** The patch built each frame from `DeclaringType.Name` (short) while the formatter filtered on namespaces, so *every* filter was inert; our own prefix plus two Harmony wrappers then ate all four slots and the real caller fell off the end. If you extend this token, keep the capture and the filter speaking the same vocabulary — pinned by `SpawnOriginFormatterTests`.

#### What `from=` answered first: there is no mystery agent

`agent#0 'Musician' char='musician_dunland'` in a `TournamentFight` looked impossible. `OpenTournamentFightMission` (`TournamentMissionStarter.cs:61-102`) builds 13 behaviors with no `MissionAgentHandler`; musicians are `LocationCharacter`s made by `TavernEmployeesCampaignBehavior` for `"tavern"`/`"center"` only; and `FightTournamentGame.GetParticipantCharacters` picks only heroes, tier 3–5 garrison troops and `BasicTroop` upgrade targets.

All of that is true and all of it is beside the point. **`MissionView`s are registered separately from the initializer delegate — the live mission holds 65 behaviors, not 13.** `MissionAudienceHandler` (`SandBox.View`) spawns the arena crowd:

```
Townswoman 0.2 · Townsman 0.2 · Armorer 0.1 · Merchant 0.1 · Musician 0.1
Weaponsmith 0.1 · RansomBroker 0.1 · Barber 0.05 · FemaleDancer 0.05
```

A Musician spectator is a 1-in-10 draw. In-house repros produced `townsman_dunland`, `armorer_dunland`, `merchant_dunland` and `ransom_broker_dunland` in the same slots.

**Rule this bought:** never infer what a mission contains from `InitializeMissionBehaviorsDelegate`. `MissionDiagnosticBehavior` already dumps the live behavior list into the same log — read that.

`FileLogger` writes every `[BattleLoad]` line (INFO) **synchronously on the calling thread**, so the begin line is on disk the moment the call returns — before the engine can freeze *or* crash inside the equip. Until 2026-07-16 it was queued to a background writer with a 50 ms poll, which was adequate for a **hang** (main thread frozen, writer thread alive to drain) but lost the queue outright on a **hard crash**. See "Crash-durability caveat" under *Read a hang log*.

### The loading window + stall watchdog

`BattleLoadLoadingWindow` is a static `volatile` latch: opened at `Mission.Initialize` (phase 4), closed at the first `OnMissionTick` (phase 6) or mission end. Phase-5 per-agent logging is gated on it, so **reinforcement waves after the battle is playable are not logged** (the symptom is the initial load only) — keeping the hot path a two-bool no-op outside the load window.

`BattleLoadStallWatchdog` runs on a **thread-pool `Timer`** (5 s poll) — it *must* be off the main thread, because a hang freezes the main thread and a main-thread timer could never fire. When the window has been open longer than the threshold (default 300 s / 5 min), it:

1. **Guaranteed:** writes `[BattleLoad] WATCHDOG STILL LOADING after Ns — last <CurrentStatusLine>` via `IModLogger` (thread-safe queue).
2. **Best-effort:** calls `ICrashReportService.HandleException(new BattleLoadStallException(...), "BattleLoadStallWatchdog")` to produce a full crash-bundle ZIP so the user can ship the log in one action. (Some collectors read live mission state from the background thread while the main thread is frozen and may return partial data — acceptable; the marker + flushed phase log are the primary signal.)

The pure decision `BattleLoadStallWatchdog.ShouldFire(windowOpen, elapsed, threshold, alreadyFired)` is unit-tested; the timer/CrashReport plumbing is not (game-only).

**Precompile suppression.** The watchdog honors a static `SuppressStallDetection` flag (`BattleLoadStallWatchdog.cs:38`): `Poll` early-returns while it is set (line 67), because a shader-precompile walk intentionally drives multi-minute cold-cache loads that would otherwise trip the 300 s threshold and emit a spurious crash bundle (false-positive found in a user's cold run, 2026-06-18). The flag is raised for the duration of a precompile walk; see [shader-precompilation.md](shader-precompilation.md).

### Scope: instruments ALL mission loads, by design

`Mission.Initialize` is the universal mission-setup path, so the loading window (and thus the watchdog + phase-5 logging) opens for **every** mission — field battle, siege, arena, town/conversation tableau, hideout — not only battles. **This is intentional.** Gating to battles would require detecting mission type at `Mission.Initialize` prefix time, and if that detection were unreliable at the moment of an *early* freeze, the gate would suppress the exact data we're hunting. For a diagnostic, a false-negative (missing the hang) is far worse than a false-positive (an extra bundle on a slow non-battle load). The watchdog marker embeds the scene name (`last phase=MissionInitialize scene='battle_terrain_b'` vs `scene='town_ES2'`), so a fired bundle self-identifies whether it was a battle or a town/arena load. Net effect: the tool catches *any* mission-load hang, which is strictly more coverage than the battle-only ask. (Deep-review 2026-06-01 MEDIUM finding — resolved as intentional scope; see `docs/reviews/rca-battle-load-diagnostics-2026-06-01.md`.)

### The mission-EXIT lifecycle (issue #331)

The load phases above answer "where did the *entry* hang?". The exit phases answer the mirror question — motivated by a user report of a **30 s–2 min constant hang exiting any tournament** (practice fights and field battles exit normally), which no static analysis could localize. Same line format, same `Patch43_BattleLoadDiagnostics` category, same master toggle; `LogExitBegin` restarts the seq counter + stopwatch so an exit reads as its own `seq=1..N` run.

| # | Phase | Hook | TaleWorlds seam (v1.4.6) |
|---|-------|------|--------------------------|
| 1 | `ExitBegin` | `Mission_EndMission_ExitPhase_Patch` (Postfix) | `Mission.EndMission()` — sets state `EndingNextFrame`; stamps mission/scene, `agents=<active>/<all>`, GC counts + heap |
| 2 | `ExitTeardownBegin` / `ExitTeardownDone` | `Mission_EndMissionInternal_ExitPhase_Patch` (Prefix + Postfix) | `Mission.EndMissionInternal()` (private) — behaviors' `OnEndMission*`, agent `OnRemove`/`OnDelete`, `FreeResources` + native `FinalizeMission` |
| 3 | `ExitStateFinalizeBegin` / `ExitStateFinalizeDone` | `MissionState_OnFinalize_ExitPhase_Patch` (Prefix + Postfix) | `MissionState.OnFinalize()` — wraps `Mission.OnMissionStateFinalize` (behavior removal + resource clear) |
| 4 | `ExitResourceClearBegin` / `ExitResourceClearDone` | `Mission_ClearUnreferencedResources_ExitPhase_Patch` (Prefix + Postfix) | `Mission.ClearUnreferencedResources(bool)` — `Common.MemoryCleanupGC()` (forced full GC) + native GPU `ClearResources` when `forceClearGPUResources` |
| 5 | `MapResumed` | `MapState_OnActivate_ExitPhase_Patch` (Postfix) | `MapState.OnActivate()` — loading screen over; stamps GC delta + `isSaving` (`SaveHandler.IsSaving`) |
| 6 | `FirstMapTick` | `MapState_OnTick_ExitPhase_Patch` (Postfix, one-shot) | `MapState.OnTick(float)` — menu/VM re-init done; **closes the exit window** |

**Exit-window gating.** `ExitBegin` opens a window (`IsExitWindowActive`); every other exit phase is silent outside it. This keeps the probes inert where their targets also fire elsewhere: `ClearUnreferencedResources` runs at mission *load*, `MapState.OnActivate` fires at campaign start/load, and `MapState.OnTick` runs **every map frame forever** (its postfix is a two-read early-out when the window is closed, per the hot-path rule). The window is **campaign-scoped**: `ExitBegin` opens only when `Campaign.Current != null` (custom battles have no `MapState` to complete the lifecycle, so opening there would leak the window). Closers, all **unconditional state transitions independent of the master toggle** (a mid-window toggle-off gates only the logging, never the close — deep-review data-flow finding 2026-07-06): `FirstMapTick` (normal path), the next `ResetLifecycle` (next campaign encounter), and the next `Mission.Initialize` (chained mission without map activation). `Mission.EndMission` re-invocation for the same mission is deduped by identity hash so the stopwatch is never restarted mid-exit. **Known limitation:** quitting to the main menu from *inside* a mission and then loading a campaign in the same process can emit one stale `MapResumed`/`FirstMapTick` pair with an implausibly large `t=+` value (self-heals immediately; cosmetic, and the huge timestamp self-identifies as stale).

**Reading an exit log:** the dominant gap names the sink — `ExitTeardownBegin→Done` = managed teardown / native finalize; `ExitResourceClearBegin→Done` = mission-end full GC / GPU clear (compare the `gc=`/`heapMB=` stamps on `ExitBegin` vs `MapResumed`); `MapResumed→FirstMapTick` = campaign/UI resume; `isSaving=True` = an autosave inside the window.

### The exit-stall stack sampler (`ExitStallSampler`, #331 round 2)

Phase gaps say *where* time went; only a stack says *what the frozen thread was doing*. `ExitStallSampler` is the watchdog's exit-side sibling: a background `Timer` polls the service's `ExitWindowOpenedUtcTicks` latch (nonzero exactly while the exit window is open; maintained by the same unconditional closers), and when a stall crosses **+15s/+30s/+60s** it suspends the MAIN thread (captured as `Thread.CurrentThread` in `SubModule.OnGameInitializationFinished`), walks it with the obsolete-as-warning `StackTrace(Thread, bool)` constructor (direct call under a `CS0618` pragma — verified present in both the net472 reference assemblies and runtime), resumes, and only then logs the frames as `[ExitStall] sample#N` lines (nothing inside the suspended window logs or allocates beyond the walk itself). The `Poll` tick carries an `Interlocked` reentrancy guard so a blocked capture can never overlap the next timer tick. It is **independently disableable** via MCM "Exit Stall Sampler → Enable Exit Stall Sampler" — the only diagnostics component that suspends the main thread, so it gets its own kill switch separate from the master toggle. Three samples of a deterministic stall show whether the top frames are stationary (one loop) or phased. Thresholds sit above the healthy tournament exit (~9.5s residual, measured 2026-07-10) so normal exits never log a false sample. Known accepted risk (documented in the class): suspending a thread mid-GC and allocating before resume can deadlock the sampler — acceptable for dev-machine diagnostics on reproducible stalls. **This sampler named the #331 round-2 sink in a single repro** (`PatchShield.ShieldFinalizerVoid` atop the `WidgetTemplate.OnRelease` recursion) after two multi-agent static rounds had bounded it wrong — see the RCA round-2 section and LESSONS-LEARNED "sample the live stack".

### Session-wide memory telemetry (`[MemSample]`, #386)

A native OOM CTD leaves no managed culprit: the commit allocation that fails is far from the leak that caused it, the AV lands on an engine worker thread the CrashReport finalizer cannot see, and the only artifact guaranteed to survive is the log written durably before death (#385: attributing a 20.3 GB-commit facegen CTD required parsing the 1.3 GB dump by hand). `MemoryPressureSampler` writes a periodic `[MemSample]` line so the tail of any crash log shows the memory trajectory, plus a one-shot WARN when system commit headroom runs low. Same construction as the stall watchdog: thread-pool `Timer` (5 s poll), `Interlocked` reentrancy guard (ExitStallSampler precedent), swallow-and-warn callback, pure static decision seams unit-tested per ADR-008.

Three line shapes (the `<message>` part — `FileLogger` wraps every line as `[yyyy-MM-dd HH:mm:ss] [LEVEL] <message>`; session + periodic are INFO, the warn is WARNING):

```
[MemSample] session totalPhysMB=16296 sysCommitLimitMB=31646
[MemSample] privMB=4211 wsMB=3900 heapMB=654 sysCommitUsedMB=14003 sysCommitLimitMB=31646 availPhysMB=6200 memLoad=61%
[MemSample] WARN LOW COMMIT HEADROOM headroomMB=1799 privMB=4211 wsMB=3900 heapMB=654 sysCommitUsedMB=29847 sysCommitLimitMB=31646 availPhysMB=310 memLoad=97%
```

The session line is emitted once, on the first enabled poll. `privMB` is `PROCESS_MEMORY_COUNTERS_EX.PrivateUsage`, `wsMB` is `WorkingSetSize`, `heapMB` is `GC.GetTotalMemory(false)`; commit used/limit and `memLoad` come from `GlobalMemoryStatusEx`. The reader is direct P/Invoke, deliberately NOT `System.Diagnostics.Process` — measured 7,711 µs/call on net472 (walks every process via `NtQuerySystemInformation`) vs 84 µs for the `GlobalMemoryStatusEx` + `GetProcessMemoryInfo` pair.

**Low-headroom contract** (single source of truth: `MemoryPressureSampler` constants; the Python triage mirror must cite that class): headroom = `sysCommitLimitMB − sysCommitUsedMB`; low when headroom < `WarnHeadroomFloorMb` (2048) OR < `WarnHeadroomPercent` (10 %) of the limit. The WARN latches (one per low episode) and re-arms only once headroom clears the threshold by `RearmHysteresisMb` (512), so a reading oscillating around the threshold cannot spam. Garbage inputs (limit ≤ 0, used < 0) never report low and never flip the latch — no WARN computed from garbage; used > limit is a legitimate over-committed reading whose headroom clamps to 0 (low).

**Gating.** The sampler gates ONLY on its own `EnableMemorySampler` toggle — deliberately NOT the master `EnableBattleLoadDiagnostics`. The master toggle governs battle-load *phase* logging; this is session-wide crash forensics, and turning off phase logging must not silently kill it. The emit interval is read live each poll, so the MCM slider takes effect without timer rescheduling. Reader failure warns once (latched, resets on the next success) and keeps polling.

**Per-phase anchors.** The service's `GcStats()` grew into `MemStats()`: the phase lines for `EncounterStart`, `MissionInitialize`, `BattlePlayable`, `ExitBegin`, and `MapResumed` now carry ` privMB={n} wsMB={n}` after the existing `gc=`/`heapMB=` tokens (omitted entirely on reader failure — never a fabricated 0 in a user log), anchoring each load/exit against the periodic `[MemSample]` trajectory. The AgentEquip/slot-dump lines are unchanged.

**Triage tool.** `tools/triage_battle_load.py` reads the `[MemSample]` telemetry riding the same log and appends a **Memory trend** section whenever samples are present: the session line, sample count, and a `first → peak → last` view of commit usage with computed headroom, plus the WARN count. When pressure is detected — a WARN anywhere, or the last sample's headroom below the floor (2048 MB) or 10 % of the limit — the section ends with `MEMORY PRESSURE: … the phase verdict above may be a symptom, not the cause.` `--mem-threshold-mb <N>` overrides the floor; `--json` gains a `memory` block (`null` for pre-#386 logs). The readout is additive decoration only: verdict classes, exit codes, and behavior on old logs are unchanged, and `[MemSample]` lines are inert to the phase timeline (never phase events, never disturb slot-dump attachment).

## Configuration

MCM page **"TAOM — Battle Load Diagnostics"** (`BattleLoadDiagnosticsSettings`, auto-registered by MCM). Defaults are the "diagnose now" posture — everything ON.

| Setting | Default | Effect |
|---------|---------|--------|
| `EnableBattleLoadDiagnostics` | `true` | Master toggle. Off → every hook is an early-out no-op. |
| `EnableStallWatchdog` | `true` | Background stall detector. |
| `EnableStallWatchdogBundle` | `true` | Also write a crash-bundle ZIP on stall (needs Crash Report capture on). |
| `StallWatchdogSeconds` | `300` | Seconds of load before flagging a stall (range 10–600; NaN/range-guarded in the provider). Default is 5 min because large custom siege scenes (e.g. Minas Tirith) legitimately take minutes to load on first entry; 45 s false-positived on them. |
| `EnableExitStallSampler` | `true` | The exit-stall stack sampler (#331 round 2) — the only diagnostics component that suspends the main thread; its own kill switch, separate from the master toggle. |
| `EnableMemorySampler` | `true` | Session-wide `[MemSample]` telemetry + low-commit-headroom WARN (#386). Independent of the master toggle (crash forensics, not phase logging). |
| `MemorySampleIntervalSeconds` | `30` | Seconds between `[MemSample]` lines (10–120; NaN/range-guarded in the provider, invalid → 30). Read live — no restart needed. |

`Reuse.Singleton` — the provider is a process singleton, but `IsEnabled` reads the MCM value live on each access, so an in-game toggle takes effect immediately. Every gate (the Mission.Initialize prefix, the watchdog poll, the behavior-add) reads through this one provider, so they stay consistent with each other at any instant.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/BattleLoadDiagnostics/IBattleLoadDiagnosticsService.cs` / `BattleLoadDiagnosticsService.cs` | Phase-marker API; owns the stopwatch + seq counter + line format; swallows all exceptions |
| `Main/Features/BattleLoadDiagnostics/IEquipmentDumpFormatter.cs` / `EquipmentDumpFormatter.cs` | Pure `EquipmentSnapshot → log lines` (the `bo=`/`shieldBo=` tokens) |
| `Main/Features/BattleLoadDiagnostics/MemoryPressureSampler.cs` | Background `Timer` + pure `ShouldSample`/`IsLowHeadroom`/`ShouldWarn`/`ShouldRearm`/`Format*` seams; owns the `[MemSample]` contract constants (floor 2048 / 10 % / hysteresis 512 / interval 30 s, 10–120) |
| `Main/Features/BattleLoadDiagnostics/MemorySampleReader.cs` | Direct-P/Invoke reader (`GlobalMemoryStatusEx` + `GetProcessMemoryInfo` + `GC.GetTotalMemory`); never throws, false on failure |
| `Main/Features/BattleLoadDiagnostics/Domain/MemorySample.cs` | Point-in-time memory reading DTO (no behavior) |
| `Main/Features/BattleLoadDiagnostics/BattleLoadLoadingWindow.cs` | Static volatile open/closed latch + `OpenedAtUtc` |
| `Main/Features/BattleLoadDiagnostics/BattleLoadStallWatchdog.cs` | Background `Timer` + pure `ShouldFire` predicate; triggers the bundle |
| `Main/Features/BattleLoadDiagnostics/BattleLoadStallException.cs` | Synthetic exception for the watchdog's bundle call (never thrown into the game) |
| `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs` + `…SettingsProvider.cs` | MCM page + the interface-wrapped provider |
| `Main/Features/BattleLoadDiagnostics/Domain/*` | `EquipmentSnapshot`, `EquipmentSlotSnapshot`, `BattleLoadPhase`, `MemorySample`, `EngineMemoryStats` DTOs |
| `Main/Features/BattleLoadDiagnostics/Hooks/*` | The 10 load-phase hooks + `BattleLoadPhaseBehavior` + the 6 exit-phase hooks (`*_ExitPhase_Patch`, issue #331) — 17 patch classes total |
| `Main/Features/BattleLoadDiagnostics/IEngineMemoryStatsReader.cs` / `EngineMemoryStatsReader.cs` | ADR-007 boundary over the four `TaleWorlds.Engine.Utilities` memory statics. Each call guarded separately — one unavailable native surface must not blank the other three. **Deliberately does not call `GetMemoryUsageOfCategory(int)`**: no category-count/name API exists, so a blind index walk is an AV risk |
| `Main/Features/BattleLoadDiagnostics/MemoryProbeReportFormatter.cs` | Pure `EngineMemoryStats + MemorySample? + label → string` for `taom.print_memory`. Owns the `[MemProbe]` tag and the station-label validator (the log-forgery guard) |
| `Main/Features/BattleLoadDiagnostics/Cheats/MemoryProbeCheats.cs` | `taom.print_memory [label] [gpu]` — Tier A, cheat gate (`RunAnywhere`). See [dev-console.md](dev-console.md) |
| `Main/Adapters/IEquipmentSnapshotAdapter.cs` / `EquipmentSnapshotAdapter.cs` | ADR-007 boundary: `Agent`/`Equipment`/`ItemObject` → `EquipmentSnapshot` |
| `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsIoC.cs` | DryIoc registrations |
| `Main/Core/Logging/FileLogger.cs` | **Not part of this feature, but load-bearing for its contract** — INFO/WARNING/ERROR drain synchronously so a stamp survives a hard crash; DEBUG stays async. Changing that reopens the blind window (#350) |

Wiring: `Main/IoC.cs` (registration), `Main/SubModule.cs` — `OnGameInitializationFinished` `Initialize(...)`s all 17 hooks then applies `Patch43` (try/catch-guarded: the category binds **four** private engine methods by string, and a diagnostics category must never break startup); `OnMissionBehaviorInitialize` adds `BattleLoadPhaseBehavior` and brackets TAOM's own behaviors via the local `AddTaomBehavior` helper, which stamps each by name.

`MemoryPressureSampler.Start()` runs from **`OnBeforeInitialModuleScreenSetAsRoot`**, not
`OnGameInitializationFinished`. That hook only fires once a game is loading, so no `[MemSample]` line
was ever written at the main menu — and the main-menu A-vs-B delta is the measurement
[`native-commit-audit-2026-08.md`](../investigations/native-commit-audit-2026-08.md) calls decisive.
`taom.print_memory` cannot fill that gap either: its `RunAnywhere` gate returns
`"Campaign was not started."` before a game loads, so the two instruments are complementary by
construction — `[MemSample]` covers the menu, `print_memory` covers map and battle. Safe this early
because `MemorySampleReader` is pure kernel32/psapi P/Invoke with no engine state and the settings
provider fails open when MCM has not registered yet. The hook re-fires on **every** return to the
main menu, so the relocation rests entirely on `Start()` being idempotent — pinned by
`MemoryPressureSamplerTests.Start_CalledTwice_ReusesTheSameTimer`.

`BattleLoadPhaseBehavior` is registered **unconditionally** (no `IsEnabled` check at the
`AddTaomBehavior` call). It is the loading window's only closer while the opener runs in
`Mission.Initialize`'s prefix, and the two evaluations are separated by a tick boundary and a
measured ~11.9 s native load — so a toggle flipped inside that window used to latch the window open
until the next `Mission.Initialize`, after which the stall watchdog fired at 300 s and wrote a
spurious bundle. Latch rule 3 (`.claude/rules/harmony-patches.md`): verify "unconditional" at the
OUTERMOST gate.

## Dependencies

- `TAOM.Core.Logging.IModLogger` / `FileLogger` (the log sink; its per-line background flush is what makes the hang survivable).
- `TAOM.Features.CrashReport.ICrashReportService` (optional — the watchdog's bundle trigger).
- `TAOM.Core.Validation.FiniteFloatValidator` (watchdog threshold guard).
- MCM (`AttributeGlobalSettings`).

## Tests

`TAOM.Tests/Features/BattleLoadDiagnostics/` (182 tests, all green — 13 cover the exit-phase lifecycle: window open/close gating, seq restart, GC/isSaving line tokens, silent-outside-window, plus 3 review-hardening regressions pinning that window-close state transitions run even when the master toggle is off and that `Mission.Initialize` closes a stale window). The feature's durability contract is pinned separately in `TAOM.Tests/Core/Logging/FileLoggerTests.cs` (14 tests) — see *Crash-durability caveat*:

- `EquipmentDumpFormatterTests` — null/empty snapshots, `shieldBo=<null>` token on missing collision mesh, id/kind inclusion, one-line-per-slot.
- `BattleLoadLoadingWindowTests` — open/close/`OpenedAtUtc` transitions.
- `BattleLoadStallWatchdogTests` — `ShouldFire` at/above/below threshold, already-fired, window-closed.
- `BattleLoadDiagnosticsServiceTests` — disabled = no writes, scene/index/summary in markers, formatter delegation, begin-before-body ordering, status-line update, and **every phase method swallows a throwing logger** (the feature must never crash the game). The phase-5c dedupe adds 8: body written once per distinct loadout, one `AgentEquipBegin` still emitted per agent, a differing loadout getting a new id + fresh dump, race alone forcing a new dump, cache cleared by both `Mission.Initialize` and `ResetLifecycle`, and the past-the-cap fallback to always-dump. The blind-window stamps add: enabled/disabled per phase, `LogTaomBehaviorAdded_UsesDurableLogInfo_NotLogDebug` (a "it's just noise, make it DEBUG" refactor would silently reopen the window with every test green), and `NewPhaseMethods_DoNotAlterExitWindowState` (the new stamps are pure probes, not latch closers).
- `BattleLoadStallMarkerTests` — `Format`/`Parse` round-trip (scene + UTC + **absolute** log path), write→consume→delete lifecycle, consume-once, `ClearInflight`, missing-directory creation, and a locked/undeletable marker still surfacing its parsed info (parse-before-delete).
- `BattleLoadDiagnosticsServiceTests`, bucket-split additions (21, 2026-08-07) — the headline guard is `NoteLoadingPoll_CalledOneThousandTimes_WritesNoLogLine`, which asserts `DidNotReceive()` on **both** `LogInfo` and `LogDebug`: it is the entire reason a per-frame hook is acceptable. Then `NoteLoadingPoll_WhenDisabled_StillCounts` (the counter is state, not I/O), one reset test per reset point (`ResetLifecycle` / `LogMissionInitialize` / `LogMissionInitializeDone`), `polls=0` emitted rather than suppressed, `waitMs` omitted rather than fabricated when the origin was never observed, `MemStats` tokens on all three new lines, and the twin literal pins `FormatFinishWaitDetail_With{,out}Wait_ProducesPinnedLiteral` (half A of the C#↔Python contract). The stopwatch blocker gets three: clock starts from `MissionOpenNew` and from `MissionInitialize`, and an already-running clock is **not** restarted.
- `MemoryProbeReportFormatterTests` (19) — `<unavailable>` for null *and* empty engine strings, per-line tagging of the multi-line blobs, CRLF normalisation, process tokens omitted wholesale on a failed read, GPU cost omitted rather than printed as 0, and the label validator: control characters rejected (the log-forgery guard — a newline in a label could inject a fake `[BattleLoad]` line into the file `triage_battle_load.py` parses), brackets/quotes rejected, and an accepted/rejected boundary pair either side of the 32-character limit.
- `Patch43LoadPhaseBindingTests` (3, `[TestCategory("BindingVerification")]`) — named resolution of the two new **private** targets plus a not-overloaded assertion on `TickLoading`, so a drift failure names the engine method instead of the generic "did not resolve".
- `MemoryPressureSamplerTests.Start_CalledTwice_ReusesTheSameTimer` — pins the idempotency the `OnBeforeInitialModuleScreenSetAsRoot` relocation depends on (that hook re-fires on every return to the main menu).

Hooks and the `MissionLogic` are game-only (ADR-008) and verified in-game. The four
`TaleWorlds.Engine.Utilities` memory statics are native and likewise untested by design — everything
downstream of them was extracted into `MemoryProbeReportFormatter` precisely so it *could* be.

### Reaching the dev: the stall marker + next-session notice

A hang freezes the **main thread**, so no in-the-moment dialog can render and the player force-quits — meaning the on-disk log + watchdog bundle never reach us. `IBattleLoadStallMarker` (`BattleLoadStallMarker`) closes that gap, mirroring `Dependencies/Foundation/IncompatibleModDetector`'s marker pattern:

- **phase 4** (`Mission.Initialize` prefix) writes `Logs/battle-load-inflight.marker` (scene + UTC + the current `taom_debug` log path);
- **phase 6 / mission end** (`BattleLoadPhaseBehavior`) deletes it once the load reaches a tick;
- the **next session's main menu** (`SubModule.OnBeforeInitialModuleScreenSetAsRoot`) calls `TryConsumeStaleMarker()` — a *surviving* marker means the previous load never finished, so `StallReportNotifier` shows a soft `ShowInquiry` ("last battle load may not have finished") with an **Open log folder** button pointing at the prior session's log.

This complements the watchdog: the watchdog fires for a player who **waits** past the threshold; the marker catches the (more common) player who **force-quits** the hang long before that. The marker lives in `Logs/` alongside `taom_debug_*.log` and the crash bundle, so one folder has everything. Wording is soft because a benign Alt-F4 during a load also leaves a marker — a low-harm false positive.

## How-To

### Triage a user's log automatically (equipment vs code)

Instead of reading the log by hand, run `tools/triage_battle_load.py` — it parses the `[BattleLoad]` lifecycle and prints a one-line **VERDICT** + the suspect agent/item/mesh:

```bash
# verdict from the log alone
python tools/triage_battle_load.py <taom_debug_*.log>
# authoritative: add the player's engine log to CONFIRM a missing mesh
python tools/triage_battle_load.py <taom_debug_*.log> --rgl-log <rgl_log_errors_*.txt>
# or hand it the whole crash bundle (it extracts both logs)
python tools/triage_battle_load.py --bundle <taom_crash_*.zip>
```

Verdicts: `EQUIPMENT` (ends at `AgentEquipBegin`, names the stuck agent's items), `EQUIPMENT_CONFIRMED` (+ the rgl_log's `get_object failed for body:` matches a suspect — reuses `validate_mesh_refs.parse_rgl_text`), `POST_EQUIP` (ends at `AgentEquipOk` **or `FinishMissionLoadingDone`** — everything equipped and the first `OnMissionTick` never came → not equipment), `SCENE` (ends at `MissionInitialize` / `BattleSceneSelected` / **`MissionInitializeDone`** / **`FinishMissionLoadingBegin`** — froze during mission construction before any agent equipped → code), `PRE_SCENE` (froze before scene selection → code), `COMPLETED`, `UNKNOWN` (diagnostics were off). Exit code is 1 for any diagnosed hang, 0 for COMPLETED/UNKNOWN, 2 for a bad path. Tests: `tools/tests/test_triage_battle_load.py`. The player-facing collection path (which files to ask for) is `.github/ISSUE_TEMPLATE/battle-load-hang.md`.

> **The three 2026-08-07 markers are load-bearing for the verdict, not just the timing.** Before they were mapped, `classify()` knew only `MissionInitialize`/`BattleSceneSelected` as `SCENE` and let every other phase fall through to `PRE_SCENE` — so a log that died in the native async load wait was reported as *"froze very early … before scene selection"*, pointing triage at the opposite end of the lifecycle.

A log carrying those markers also gets a **`Load timing`** report section (and a `timings` block in `--json`) with the six buckets the Phase-2 runbook records — `bucket1`, `bucket2`, `bucket3a`, `bucket3b`, `bucket3c`, `bucket4` (`FinishMissionLoadingDone` → `BattlePlayable`) — plus the dominant bucket, the `polls=`/`waitMs=` pair, and the `privMB` trajectory across the four `MemStats()`-bearing markers. Two rules the tool enforces so the numbers stay honest: spans are **gaps between markers, never absolute `t=+` values** (the stopwatch is `IsRunning`-guarded, so a chained second mission inherits the first's origin — the tool scopes to the last `MissionInitialize` segment), and an unmeasured value is **absent, never zero** — an omitted `waitMs` renders as `waitMs=<not observed>` and an unreached bucket as `?`. `polls=0` gets its own explicit "the binding FAILED, this is not 'there was no wait'" line. The section is omitted entirely for logs predating the markers, so old-log output is unchanged.

### Read a hang log

Open the user's `Modules/.../Logs/taom_debug_<timestamp>.log` and find the last `[BattleLoad]` line:

- ends at `phase=AgentEquipBegin agent#57 …` (no `AgentEquipOk`) → equipment hang. **Read the `loadout=#N` token on that line and scroll UP to the first `AgentEquipBegin` carrying the same id — the indented `slot=… bo=<null>/shieldBo=<null>` lines are under *that* one.** Since 2026-08-03 the dump is written once per distinct loadout, so the stuck agent usually has no block directly beneath it (see phase 5c). `triage_battle_load.py` does this resolution for you. Cross-check the named items with `python tools/validate_mesh_refs.py` and the troop rosters.
- ends at `phase=BattleSceneSelected` (no `MissionInitialize`) → scene-load hang, not equipment.
- a `WATCHDOG STILL LOADING after Ns — last phase=…` line → the watchdog fired; the `last phase` is the freeze point, and a `taom_crash_*.zip` bundle was written alongside.
- ends at `phase=BattlePlayable` → the load completed; the hang is elsewhere.

Within the OpenNew→Initialize window, the last stamp names the segment:

| Log ends at | The fault is in |
|---|---|
| `MissionOpenNew` (no `MissionOpenNewDone`) | `OpenNew`'s body — `OnMissionIsStarting`, the native `Mission` ctor, the SandBox behavior handler, or `PushState` |
| `MissionOpenNewDone` (no `LoadMissionBegin`) | the tick boundary — `MissionState.OnInitialize`/`OnActivate`, or the game never ticked again |
| `LoadMissionBegin` (no `ResourceClearOldBegin`) | a behavior's `OnMissionScreenPreLoad` |
| `ResourceClearOldBegin` (no `Done`) | **native** resource teardown — suspect heap corruption inherited from the previous mission's exit (cf. Patch62 / #339) |
| `ResourceClearOldDone` (no `MissionInitialize`) | between the clear and `Mission.Initialize` |
| `MissionInitialize` (no `MissionInitializeDone`) | the native `MBAPI.IMBMission.InitializeMission` — scene / physics / terrain construction |
| `MissionInitializeDone` (no `FinishMissionLoadingBegin`) | the async load wait never completed — native `Mission.IsLoadingFinished` never returned true. Read `polls=` on the *next* run to tell a spin from genuine streaming |
| `FinishMissionLoadingBegin` (no `MissionAfterStartBegin`) | `Scene.SetOwnerThread`, one of the two warm-up `Mission.Tick(0.001f)` calls, or `OnMissionAfterStarting` |
| `MissionAfterStartBegin` (no `TaomBehaviorsBegin`) | **another mod's** `OnMissionBehaviorInitialize` — not TAOM |
| `TaomBehaviorAdded behavior='X'` (no further stamp) | registering TAOM's `X` |
| `MissionAfterStartDone` (no `FinishMissionLoadingDone`) | `OnMissionLoadingFinished` or `Scene.ResumeLoadingRenderings` |

**Two reading traps on the new tokens.** `polls=0` on a `FinishMissionLoadingBegin` line means the
`MissionState.TickLoading` **binding failed**, not that there was no wait — check for a
`Patch43 diagnostics failed to apply` warning near the top of the log. And `t=+` is an *absolute*
that survives across chained missions in one process (the clock is `IsRunning`-guarded, so a second
mission inherits the first's origin): read **gaps between consecutive lines**, never the absolute.

**Crash-durability caveat.** `[BattleLoad]`/`[SaveLoad]` lines are INFO and, since 2026-07-16, written synchronously — so the last INFO line is a true record of how far execution got. `[DEBUG]` lines are still async and a hard crash drops whatever is queued, so **do not read the last DEBUG line as the stopping point**. Before that change every level was async, which is why the Nan Angren log could not be localized: `MissionInitialize` might have been reached and merely never written.

### Add a new lifecycle phase

Add a value to `BattleLoadPhase`, a method to `IBattleLoadDiagnosticsService`, a thin hook in `Hooks/` with `[HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]`, and an `Initialize(...)` call in `SubModule.OnGameInitializationFinished`. Keep the hook thin and exception-swallowing.

## Performance

- Outside the loading window, the phase-5 prefix is a two-bool read (`IsEnabled && IsOpen`) and returns. Inside, it does ~12 resident-property slot reads + one DTO alloc per spawning agent, only until the first tick.
- **The per-loadout dedupe (phase 5c) is the log's main size control, not a speed one.** Replaying the 2026-08-03 tournament repro through the real key takes the equipment dump from **1,146 lines / 186,345 B to 52 lines / 8,432 B**, against 5,148 B of added `loadout=#N` tokens — a net 172 KB on one 37-minute session. It adds one string build + one dictionary probe per agent under a lock — the lock is there because a torn `Dictionary` can spin the game thread forever, and a diagnostic must never be the thing that hangs the game.
- **Measured cost of the per-agent stamps, from the same run: 145 ms for all 429 agents** (`seq=30` @ +8137 ms → `seq=1316` @ +8282 ms), i.e. ~0.11 ms/agent for three synchronous INFO flushes. That is the honest figure for why the triplet is not worth trimming.
- Master toggle off → every hook early-outs immediately.
- The watchdog is one thread-pool timer ticking every 5 s; negligible.
- `seq` uses `Interlocked` and the status line is a `volatile` reference, so the off-thread watchdog reads are torn-free.
- The blind-window stamps add ~5 lines per load, plus one `TaomBehaviorAdded` per TAOM behavior (~11–15). All fire once per mission load; none is per-frame.
- **Synchronous INFO is paid on the game thread, and the honest figure is not "a few hundred lines".** Phase 5 emits INFO *per spawning agent*, so a large battle turns ~1000 stamps into game-thread flushes, each also draining the DEBUG queued behind it — during the equip burst the game thread ends up writing most of the log. Budget: ~15 ms across a multi-second load that already does native scene I/O. It is the same total work as before, on a different thread, and it holds **only** because `Flush()` lands in the OS page cache (which a dying process does not lose) — `WriteThrough` would turn each flush into a physical disk write and is deliberately not used. This is load-time, behind a loading screen, and it is the exact window the instrument exists to survive; making those lines async again would reopen #350.
- Lock contention is bounded by **one `Drain()` batch** (queue depth × per-line write), *not* by the writer's 50 ms poll — the `Thread.Sleep(50)` sits in `ProcessQueue`, outside `Drain()`'s lock, so a durable write can never block on it. (A review agent claimed a 50 ms stall; it conflated the wake interval with lock-hold time. See the RCA.)

## Related

- [mesh-ref-validation.md](mesh-ref-validation.md) — the companion tool that confirms/eliminates the missing-`bo_`-mesh hypothesis offline + via `rgl_log`.
- [mission-diagnostic.md](mission-diagnostic.md) — sibling diagnostic that dumps `MissionBehaviors`/`MissionLogics` on first tick (shares the same log file).
- [crash-report.md](crash-report.md) — the bundle pipeline the watchdog reuses.

## Changelog

- 2026-08-07 — **Split the 11.9-second `MissionInitialize` → `MissionAfterStartBegin` gap into three named buckets.** Added `MissionInitializeDone` (a Postfix on the existing `Mission.Initialize` patch) plus `FinishMissionLoadingBegin`/`Done` (Prefix + Postfix on the **private** `MissionState.FinishMissionLoading`), and a `MissionState.TickLoading` prefix that is a **counter and never logs** — 720 lines in a 12-second wait at 60 fps is not an acceptable instrument, so the frame count rides one `polls=`/`waitMs=` token pair on `FinishMissionLoadingBegin`. That pair is what separates a blocking native spin (`polls=1`, large `waitMs`) from genuine async streaming (`polls ≈ waitMs/16`) — opposite diagnoses that were previously indistinguishable. All three markers carry `MemStats()`, so the `privMB` curve *across the stall* can say whether the load stall and the commit growth are one problem or two. Patch43 went 14 → 17 hooks.
  - **Fixed: the stopwatch was dead in custom battles.** `_stopwatch` was only ever started from `LogEncounterStart`/`ResetLifecycle`, both reachable only from the campaign-only `PlayerEncounter_Start_Patch` — so every `[BattleLoad]` line in a custom battle read `t=+0ms`. Added the existing `IsRunning`-guarded restart idiom to `LogMissionOpenNew` (the universal funnel) and `LogMissionInitialize`. Pre-existing defect, unrecorded anywhere, and load-bearing for the measurement work above.
  - **Fixed: `BattleLoadPhaseBehavior` was registered only while enabled.** It is the loading window's only closer and the opener is `Mission.Initialize`'s prefix, so a toggle flipped across that window (now known to span a tick boundary and ~11.9 s) latched the window open and the stall watchdog wrote a spurious bundle at 300 s. Registered unconditionally; it already self-gates its logging. The 2026-07-06 RCA had deferred this as "the same synchronous call chain" — the bucket measurement disproves that premise.
- 2026-08-07 — **Added `taom.print_memory [label] [gpu]`** (Tier A, cheat gate) and the `IEngineMemoryStatsReader` boundary over `TaleWorlds.Engine.Utilities`' four memory statics, for the per-station rows of [`native-commit-audit-2026-08.md`](../investigations/native-commit-audit-2026-08.md)'s commit-attribution matrix. Output is mirrored into `taom_debug` under `[MemProbe]` — deliberately a tag `triage_battle_load.py` does *not* parse, so a matrix run self-records without creating a third cross-language contract. `GetMemoryUsageOfCategory(int)` is **not** called: no category-count or category-name API exists in either build and the index goes straight to native unvalidated, so a blind walk is an access-violation risk; the two statistics strings are read first and a numeric probe is built only if they turn out not to carry the breakdown. Also relocated `MemoryPressureSampler.Start()` from `OnGameInitializationFinished` to `OnBeforeInitialModuleScreenSetAsRoot` so a **main-menu** `[MemSample]` baseline exists — the A-vs-B menu delta is the audit's decisive measurement, and neither instrument could produce it before (`print_memory`'s gate returns "Campaign was not started." at the menu).
- 2026-08-05 — **Added `[MemSample]` session-wide memory telemetry** (#386, motivated by #385: a facegen CTD at 20.3 GB process commit on a 16 GB machine, diagnosable only by hand-parsing the dump). `MemoryPressureSampler` (own toggle, NOT the master), `MemorySampleReader` (P/Invoke, not `System.Diagnostics.Process` — 7,711 µs vs 84 µs measured), `MemStats()` phase-line tokens, and the triage tool's Memory-trend section + `MEMORY PRESSURE` note. The `[MemSample]` tag is a second cross-language log contract, pinned by twin literal tests (C# ↔ `tools/tests/test_triage_battle_load.py`).
- 2026-08-03 — **Deduped the equipment dump per loadout** (phase 5c). The 2026-08-03 tournament repro spent 1,146 slot lines on 18 distinct loadouts; each is now dumped once and later agents cite `loadout=#N` (measured by replay: 186,345 B → 8,432 B, +5,148 B of tokens). All three per-agent stamps are unchanged — they are the crash discriminator and cost 145 ms for 429 agents. `triage_battle_load.py` learned to resolve `loadout=#N` back to its block (without which the EQUIPMENT verdict would name no suspect), and its `_EQUIP_BEGIN_RE` was fixed: a lazy `culture='(.*?)'` had been swallowing the `race`/`monster`/`actionSet` tokens added on 2026-08-02 and reporting the whole run as the culture.
- 2026-07-16 — **Split the `MissionOpenNew` → `MissionInitialize` blind window** (#350) after a player CTD at Nan Angren left a log that could not be localized. Added an `OpenNew` Postfix, the private `MissionState.LoadMission`, the native `Utilities.ClearOldResourcesAndObjects` bracket, and the `Mission.AfterStart` bracket (which lets a log *exonerate* TAOM, not just accuse it), plus per-behavior `TaomBehaviorAdded` stamps. Patch43 went 11 → 14 hooks and its apply is now try/catch-guarded. Registry correction: `Mission.Initialize` is **public** (`Mission.cs:1798`), not private as claimed since this feature shipped.
- 2026-07-16 — **Made the stamps survive a hard crash.** `FileLogger` queued every line to a background writer (`IsBackground`, 50 ms idle sleep), so a dying process took the undrained queue with it — the forensics instrument systematically lost the lines it exists to capture, which is *why* the Nan Angren log was unlocalizable. INFO/WARNING/ERROR now drain synchronously; DEBUG stays async. Deep review then found 2 MED defects in that fix itself (a post-`Dispose` writer-thread hot-spin; a write fault that failed silently) — both fixed, both pinned by tests. RCA: [rca-battle-load-blind-window-2026-07-16.md](../reviews/rca-battle-load-blind-window-2026-07-16.md).
- 2026-06-17 — Added the `IBattleLoadStallMarker` / next-session notice: phase 4 writes `Logs/battle-load-inflight.marker`, a surviving marker on next launch surfaces a soft `StallReportNotifier` inquiry with an Open-log-folder button (plus a `battle-load-hang.md` issue template).
- 2026-06-17 — Added `tools/triage_battle_load.py`, which parses the `[BattleLoad]` lifecycle and prints a one-line EQUIPMENT / EQUIPMENT_CONFIRMED / POST_EQUIP / SCENE / PRE_SCENE verdict naming the stuck agent/item/mesh.
- 2026-06-17 — Fixed a startup CTD: `BattleLoadStallMarker`'s second public ctor made DryIoc throw `UnableToSelectSinglePublicConstructorFromMultiple`; the test-seam ctor was made `internal`, leaving one public ctor.
- 2026-06-01 — Introduced the `BattleLoadDiagnostics` feature (`Patch43`): phase-stamps the full attack→battle-playable lifecycle across 6 markers, dumps per-agent equipment with `bo=`/`shieldBo=` mesh names, and runs the background stall watchdog (CrashReport bundle on stall).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/atmosphere-persistence.md](./atmosphere-persistence.md)
- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
