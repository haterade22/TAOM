# Battle Load Diagnostics

## Overview

`BattleLoadDiagnostics` phase-stamps the **entire attack → battle-playable lifecycle** to the TAOM debug log (`Logs/taom_debug_*.log`) and runs a background-thread **stall watchdog**. When a battle gets stuck on the loading screen (the intermittent infinite-load hang), the **last line written before the freeze names the stuck phase** — and for the equipment phase, the exact agent and the item whose collision mesh (`bo_` / `shield_body_name`) is missing.

## Why This Exists

Users report that entering a battle *sometimes* hangs forever on the loading screen — **no crash, no stack trace**, the battle never initializes. It is intermittent, happens on user machines, and cannot be reproduced locally. A hang ≠ a crash: a crash throws (and TAOM's `CrashReport` feature already captures it); a hang means the **main thread is blocked**, so nothing is thrown and the existing crash pipeline never fires. The existing scene-reference audits (`audit_battle_scenes.py`, `audit_scene_names.py`) only catch *crashes* from missing scene folders, not this hang.

The leading hypothesis (historically the cause) is a missing `bo_` collision mesh on a weapon/shield in `LOTRLOME_Armory`: the engine stalls resolving the absent mesh while spawning an agent that equips that item. The engine even logs this itself — `rgl_log_errors_*.txt` contains `get_object failed for body: bo_X` (see the companion tool [mesh-ref-validation.md](mesh-ref-validation.md)). But the hang could also be scene-side. This feature is **cause-agnostic**: it localizes *any* battle-load hang by phase, so the next user report comes with a log that points at the culprit instead of a shrug.

## Architecture

### The six lifecycle phases

Each phase is a thin Harmony hook (or `MissionLogic`) that delegates one call to `IBattleLoadDiagnosticsService`, which writes a consistent line:

```
[BattleLoad] seq=NN t=+1234ms phase=<PhaseName> <detail>
```

`seq` is a monotonic counter (`Interlocked.Increment`); `t=+Nms` is `Stopwatch` elapsed since the encounter began. A large gap between two consecutive `seq` lines is the stall location.

| # | Phase | Hook | TaleWorlds seam (v1.4.6) |
|---|-------|------|--------------------------|
| 1 | `EncounterStart` | `PlayerEncounter_Start_Patch` (Postfix) | `PlayerEncounter.Start()` — resets the lifecycle clock |
| 2 | `MissionOpenNew` | `MissionState_OpenNew_Patch` (Prefix) | `MissionState.OpenNew(string, MissionInitializerRecord, …)` — logs scene + attacker/defender/sizes/side from `PlayerEncounter.Current` |
| 3 | `BattleSceneSelected` | `BattleSceneSelection_Patch` (Postfix) | `DefaultSceneModel.GetBattleSceneForMapPatch(MapPatchData, bool)` — logs `mapIndex → sceneId` |
| 4 | `MissionInitialize` | `Mission_Initialize_BattleLoad_Patch` (Prefix) | `Mission.Initialize` (private) — opens the loading window |
| 5 | `AgentEquipBegin` / `AgentEquipOk` | `Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch` (Prefix + Postfix) | `Agent.EquipItemsFromSpawnEquipment(bool,bool,bool,int)` — **the money hook** |
| 6 | `BattlePlayable` | `BattleLoadPhaseBehavior : MissionLogic` (first `OnMissionTick`) | closes the loading window — load succeeded |

All hooks share the Harmony category `Patch43_BattleLoadDiagnostics`. Phases 4 and 5 coexist with the pre-existing prefixes on the same methods (`Patch16_AtmospherePersistence` on `Mission.Initialize`, `Patch23_BannerColorPersistence` on `EquipItemsFromSpawnEquipment`) — Harmony runs all of them.

### The money hook (phase 5)

The prefix builds an `EquipmentSnapshot` (via `IEquipmentSnapshotAdapter`, reading `Agent.SpawnEquipment` — the *full* `Equipment` incl. armor + horse, NOT `Agent.Equipment` which is weapons-only) and logs the full loadout **before** the engine equips the agent. The postfix logs `AgentEquipOk` only **after** the engine returns. So:

- **`AgentEquipBegin` with a matching `AgentEquipOk`** → that agent equipped fine.
- **`AgentEquipBegin` with NO matching `AgentEquipOk` (log ends here)** → the freeze is inside that agent's equipment spawn, and the dumped slots name the suspect — look for `bo=<null>` / `shieldBo=<null>`.

`FileLogger` flushes every line on a background writer thread (50 ms poll), so the begin line is on disk within ~50 ms even though the main thread is frozen.

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
| `Main/Features/BattleLoadDiagnostics/Hooks/*` | The 6 load-phase hooks + `BattleLoadPhaseBehavior` + the 6 exit-phase hooks (`*_ExitPhase_Patch`, issue #331) |
| `Main/Adapters/IEquipmentSnapshotAdapter.cs` / `EquipmentSnapshotAdapter.cs` | ADR-007 boundary: `Agent`/`Equipment`/`ItemObject` → `EquipmentSnapshot` |
| `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsIoC.cs` | DryIoc registrations |

Wiring: `Main/IoC.cs` (registration), `Main/SubModule.cs` (`OnGameInitializationFinished` applies `Patch43` + starts the watchdog; `OnMissionBehaviorInitialize` adds `BattleLoadPhaseBehavior`).

## Dependencies

- `TAOM.Core.Logging.IModLogger` / `FileLogger` (the log sink; its per-line background flush is what makes the hang survivable).
- `TAOM.Features.CrashReport.ICrashReportService` (optional — the watchdog's bundle trigger).
- `TAOM.Core.Validation.FiniteFloatValidator` (watchdog threshold guard).
- MCM (`AttributeGlobalSettings`).

## Tests

`TAOM.Tests/Features/BattleLoadDiagnostics/` (50 tests, all green — 13 cover the exit-phase lifecycle: window open/close gating, seq restart, GC/isSaving line tokens, silent-outside-window, plus 3 review-hardening regressions pinning that window-close state transitions run even when the master toggle is off and that `Mission.Initialize` closes a stale window):

- `EquipmentDumpFormatterTests` — null/empty snapshots, `shieldBo=<null>` token on missing collision mesh, id/kind inclusion, one-line-per-slot.
- `BattleLoadLoadingWindowTests` — open/close/`OpenedAtUtc` transitions.
- `BattleLoadStallWatchdogTests` — `ShouldFire` at/above/below threshold, already-fired, window-closed.
- `BattleLoadDiagnosticsServiceTests` — disabled = no writes, scene/index/summary in markers, formatter delegation, begin-before-body ordering, status-line update, and **every phase method swallows a throwing logger** (the feature must never crash the game).
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

### Add a new lifecycle phase

Add a value to `BattleLoadPhase`, a method to `IBattleLoadDiagnosticsService`, a thin hook in `Hooks/` with `[HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]`, and an `Initialize(...)` call in `SubModule.OnGameInitializationFinished`. Keep the hook thin and exception-swallowing.

## Performance

- Outside the loading window, the phase-5 prefix is a two-bool read (`IsEnabled && IsOpen`) and returns. Inside, it does ~12 resident-property slot reads + one DTO alloc per spawning agent, only until the first tick.
- Master toggle off → every hook early-outs immediately.
- The watchdog is one thread-pool timer ticking every 5 s; negligible.
- `seq` uses `Interlocked` and the status line is a `volatile` reference, so the off-thread watchdog reads are torn-free.

## Related

- [mesh-ref-validation.md](mesh-ref-validation.md) — the companion tool that confirms/eliminates the missing-`bo_`-mesh hypothesis offline + via `rgl_log`.
- [mission-diagnostic.md](mission-diagnostic.md) — sibling diagnostic that dumps `MissionBehaviors`/`MissionLogics` on first tick (shares the same log file).
- [crash-report.md](crash-report.md) — the bundle pipeline the watchdog reuses.

## Changelog

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
