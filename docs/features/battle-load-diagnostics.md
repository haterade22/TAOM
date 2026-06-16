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

| # | Phase | Hook | TaleWorlds seam (v1.4.5) |
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

### Scope: instruments ALL mission loads, by design

`Mission.Initialize` is the universal mission-setup path, so the loading window (and thus the watchdog + phase-5 logging) opens for **every** mission — field battle, siege, arena, town/conversation tableau, hideout — not only battles. **This is intentional.** Gating to battles would require detecting mission type at `Mission.Initialize` prefix time, and if that detection were unreliable at the moment of an *early* freeze, the gate would suppress the exact data we're hunting. For a diagnostic, a false-negative (missing the hang) is far worse than a false-positive (an extra bundle on a slow non-battle load). The watchdog marker embeds the scene name (`last phase=MissionInitialize scene='battle_terrain_b'` vs `scene='town_ES2'`), so a fired bundle self-identifies whether it was a battle or a town/arena load. Net effect: the tool catches *any* mission-load hang, which is strictly more coverage than the battle-only ask. (Deep-review 2026-06-01 MEDIUM finding — resolved as intentional scope; see `docs/reviews/rca-battle-load-diagnostics-2026-06-01.md`.)

## Configuration

MCM page **"TAOM — Battle Load Diagnostics"** (`BattleLoadDiagnosticsSettings`, auto-registered by MCM). Defaults are the "diagnose now" posture — everything ON.

| Setting | Default | Effect |
|---------|---------|--------|
| `EnableBattleLoadDiagnostics` | `true` | Master toggle. Off → every hook is an early-out no-op. |
| `EnableStallWatchdog` | `true` | Background stall detector. |
| `EnableStallWatchdogBundle` | `true` | Also write a crash-bundle ZIP on stall (needs Crash Report capture on). |
| `StallWatchdogSeconds` | `300` | Seconds of load before flagging a stall (range 10–600; NaN/range-guarded in the provider). Default is 5 min because large custom siege scenes (e.g. Minas Tirith) legitimately take minutes to load on first entry; 45 s false-positived on them. |

`Reuse.Singleton` — MCM caches for the whole process; changes apply on the next launch, not mid-session.

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
| `Main/Features/BattleLoadDiagnostics/Hooks/*` | The 6 phase hooks + `BattleLoadPhaseBehavior` |
| `Main/Adapters/IEquipmentSnapshotAdapter.cs` / `EquipmentSnapshotAdapter.cs` | ADR-007 boundary: `Agent`/`Equipment`/`ItemObject` → `EquipmentSnapshot` |
| `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsIoC.cs` | DryIoc registrations |

Wiring: `Main/IoC.cs` (registration), `Main/SubModule.cs` (`OnGameInitializationFinished` applies `Patch43` + starts the watchdog; `OnMissionBehaviorInitialize` adds `BattleLoadPhaseBehavior`).

## Dependencies

- `TAOM.Core.Logging.IModLogger` / `FileLogger` (the log sink; its per-line background flush is what makes the hang survivable).
- `TAOM.Features.CrashReport.ICrashReportService` (optional — the watchdog's bundle trigger).
- `TAOM.Core.Validation.FiniteFloatValidator` (watchdog threshold guard).
- MCM (`AttributeGlobalSettings`).

## Tests

`TAOM.Tests/Features/BattleLoadDiagnostics/` (26 tests, all green):

- `EquipmentDumpFormatterTests` — null/empty snapshots, `shieldBo=<null>` token on missing collision mesh, id/kind inclusion, one-line-per-slot.
- `BattleLoadLoadingWindowTests` — open/close/`OpenedAtUtc` transitions.
- `BattleLoadStallWatchdogTests` — `ShouldFire` at/above/below threshold, already-fired, window-closed.
- `BattleLoadDiagnosticsServiceTests` — disabled = no writes, scene/index/summary in markers, formatter delegation, begin-before-body ordering, status-line update, and **every phase method swallows a throwing logger** (the feature must never crash the game).

Hooks and the `MissionLogic` are game-only (ADR-008) and verified in-game.

## How-To

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
