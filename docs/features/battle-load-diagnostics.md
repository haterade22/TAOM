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
| 4b | `MissionAfterStartBegin` / `Done` | `Mission_AfterStart_BattleLoad_Patch` (Prefix + Postfix) | `Mission.AfterStart()` — runs `OnMissionBehaviorInitialize` for **every** submodule |
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

### The money hook (phase 5)

The prefix builds an `EquipmentSnapshot` (via `IEquipmentSnapshotAdapter`, reading `Agent.SpawnEquipment` — the *full* `Equipment` incl. armor + horse, NOT `Agent.Equipment` which is weapons-only) and logs the full loadout **before** the engine equips the agent. The postfix logs `AgentEquipOk` only **after** the engine returns. So:

- **`AgentEquipBegin` with a matching `AgentEquipOk`** → that agent equipped fine.
- **`AgentEquipBegin` with NO matching `AgentEquipOk` (log ends here)** → the freeze is inside that agent's equipment spawn, and the dumped slots name the suspect — look for `bo=<null>` / `shieldBo=<null>`.

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

## Configuration

MCM page **"TAOM — Battle Load Diagnostics"** (`BattleLoadDiagnosticsSettings`, auto-registered by MCM). Defaults are the "diagnose now" posture — everything ON.

| Setting | Default | Effect |
|---------|---------|--------|
| `EnableBattleLoadDiagnostics` | `true` | Master toggle. Off → every hook is an early-out no-op. |
| `EnableStallWatchdog` | `true` | Background stall detector. |
| `EnableStallWatchdogBundle` | `true` | Also write a crash-bundle ZIP on stall (needs Crash Report capture on). |
| `StallWatchdogSeconds` | `300` | Seconds of load before flagging a stall (range 10–600; NaN/range-guarded in the provider). Default is 5 min because large custom siege scenes (e.g. Minas Tirith) legitimately take minutes to load on first entry; 45 s false-positived on them. |

`Reuse.Singleton` — the provider is a process singleton, but `IsEnabled` reads the MCM value live on each access, so an in-game toggle takes effect immediately. Every gate (the Mission.Initialize prefix, the watchdog poll, the behavior-add) reads through this one provider, so they stay consistent with each other at any instant.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/BattleLoadDiagnostics/IBattleLoadDiagnosticsService.cs` / `BattleLoadDiagnosticsService.cs` | Phase-marker API; owns the stopwatch + seq counter + line format; swallows all exceptions |
| `Main/Features/BattleLoadDiagnostics/IEquipmentDumpFormatter.cs` / `EquipmentDumpFormatter.cs` | Pure `EquipmentSnapshot → log lines` (the `bo=`/`shieldBo=` tokens) |
| `Main/Features/BattleLoadDiagnostics/BattleLoadLoadingWindow.cs` | Static volatile open/closed latch + `OpenedAtUtc` |
| `Main/Features/BattleLoadDiagnostics/BattleLoadStallWatchdog.cs` | Background `Timer` + pure `ShouldFire` predicate; triggers the bundle |
| `Main/Features/BattleLoadDiagnostics/BattleLoadStallException.cs` | Synthetic exception for the watchdog's bundle call (never thrown into the game) |
| `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs` + `…SettingsProvider.cs` | MCM page + the interface-wrapped provider |
| `Main/Features/BattleLoadDiagnostics/Domain/*` | `EquipmentSnapshot`, `EquipmentSlotSnapshot`, `BattleLoadPhase` DTOs |
| `Main/Features/BattleLoadDiagnostics/Hooks/*` | The 8 load-phase hooks + `BattleLoadPhaseBehavior` + the 6 exit-phase hooks (`*_ExitPhase_Patch`, issue #331) — 14 patch classes total |
| `Main/Adapters/IEquipmentSnapshotAdapter.cs` / `EquipmentSnapshotAdapter.cs` | ADR-007 boundary: `Agent`/`Equipment`/`ItemObject` → `EquipmentSnapshot` |
| `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsIoC.cs` | DryIoc registrations |
| `Main/Core/Logging/FileLogger.cs` | **Not part of this feature, but load-bearing for its contract** — INFO/WARNING/ERROR drain synchronously so a stamp survives a hard crash; DEBUG stays async. Changing that reopens the blind window (#350) |

Wiring: `Main/IoC.cs` (registration), `Main/SubModule.cs` — `OnGameInitializationFinished` `Initialize(...)`s all 14 hooks then applies `Patch43` (try/catch-guarded: the category binds a private method by string, and a diagnostics category must never break startup); `OnMissionBehaviorInitialize` adds `BattleLoadPhaseBehavior` and brackets TAOM's own behaviors via the local `AddTaomBehavior` helper, which stamps each by name.

## Dependencies

- `TAOM.Core.Logging.IModLogger` / `FileLogger` (the log sink; its per-line background flush is what makes the hang survivable).
- `TAOM.Features.CrashReport.ICrashReportService` (optional — the watchdog's bundle trigger).
- `TAOM.Core.Validation.FiniteFloatValidator` (watchdog threshold guard).
- MCM (`AttributeGlobalSettings`).

## Tests

`TAOM.Tests/Features/BattleLoadDiagnostics/` (72 tests, all green — 13 cover the exit-phase lifecycle: window open/close gating, seq restart, GC/isSaving line tokens, silent-outside-window, plus 3 review-hardening regressions pinning that window-close state transitions run even when the master toggle is off and that `Mission.Initialize` closes a stale window). The feature's durability contract is pinned separately in `TAOM.Tests/Core/Logging/FileLoggerTests.cs` (14 tests) — see *Crash-durability caveat*:

- `EquipmentDumpFormatterTests` — null/empty snapshots, `shieldBo=<null>` token on missing collision mesh, id/kind inclusion, one-line-per-slot.
- `BattleLoadLoadingWindowTests` — open/close/`OpenedAtUtc` transitions.
- `BattleLoadStallWatchdogTests` — `ShouldFire` at/above/below threshold, already-fired, window-closed.
- `BattleLoadDiagnosticsServiceTests` — disabled = no writes, scene/index/summary in markers, formatter delegation, begin-before-body ordering, status-line update, and **every phase method swallows a throwing logger** (the feature must never crash the game). The blind-window stamps add: enabled/disabled per phase, `LogTaomBehaviorAdded_UsesDurableLogInfo_NotLogDebug` (a "it's just noise, make it DEBUG" refactor would silently reopen the window with every test green), and `NewPhaseMethods_DoNotAlterExitWindowState` (the new stamps are pure probes, not latch closers).
- `BattleLoadStallMarkerTests` — `Format`/`Parse` round-trip (scene + UTC + **absolute** log path), write→consume→delete lifecycle, consume-once, `ClearInflight`, missing-directory creation, and a locked/undeletable marker still surfacing its parsed info (parse-before-delete).

Hooks and the `MissionLogic` are game-only (ADR-008) and verified in-game.

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

Verdicts: `EQUIPMENT` (ends at `AgentEquipBegin`, names the stuck agent's items), `EQUIPMENT_CONFIRMED` (+ the rgl_log's `get_object failed for body:` matches a suspect — reuses `validate_mesh_refs.parse_rgl_text`), `POST_EQUIP` (equipped fine, froze before playable → not equipment), `SCENE` / `PRE_SCENE` (froze during/before scene load → code), `COMPLETED`, `UNKNOWN` (diagnostics were off). Exit code is 1 for any diagnosed hang, 0 for COMPLETED/UNKNOWN, 2 for a bad path. Tests: `tools/tests/test_triage_battle_load.py`. The player-facing collection path (which files to ask for) is `.github/ISSUE_TEMPLATE/battle-load-hang.md`.

### Read a hang log

Open the user's `Modules/.../Logs/taom_debug_<timestamp>.log` and find the last `[BattleLoad]` line:

- ends at `phase=AgentEquipBegin agent#57 …` (no `AgentEquipOk`) → equipment hang; the indented `slot=… bo=<null>/shieldBo=<null>` lines name the item. Cross-check with `python tools/validate_mesh_refs.py` and the troop rosters.
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
| `MissionAfterStartBegin` (no `TaomBehaviorsBegin`) | **another mod's** `OnMissionBehaviorInitialize` — not TAOM |
| `TaomBehaviorAdded behavior='X'` (no further stamp) | registering TAOM's `X` |

**Crash-durability caveat.** `[BattleLoad]`/`[SaveLoad]` lines are INFO and, since 2026-07-16, written synchronously — so the last INFO line is a true record of how far execution got. `[DEBUG]` lines are still async and a hard crash drops whatever is queued, so **do not read the last DEBUG line as the stopping point**. Before that change every level was async, which is why the Nan Angren log could not be localized: `MissionInitialize` might have been reached and merely never written.

### Add a new lifecycle phase

Add a value to `BattleLoadPhase`, a method to `IBattleLoadDiagnosticsService`, a thin hook in `Hooks/` with `[HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]`, and an `Initialize(...)` call in `SubModule.OnGameInitializationFinished`. Keep the hook thin and exception-swallowing.

## Performance

- Outside the loading window, the phase-5 prefix is a two-bool read (`IsEnabled && IsOpen`) and returns. Inside, it does ~12 resident-property slot reads + one DTO alloc per spawning agent, only until the first tick.
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
