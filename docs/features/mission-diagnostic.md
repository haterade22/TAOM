# Mission Diagnostic

## Overview

Captures structured diagnostic snapshots to the TAOM debug log at session start and on the first tick of every mission, so user-uploaded `taom_debug_*.log` files contain everything we need to identify mod-conflict bugs without asking the user to attach a debugger. The targeted offender pattern is `MissionBehavior` + `BehaviorType=Logic` without inheriting `MissionLogic` — those produce null-cast crashes every tick.

## Why This Exists

- **Vanilla behavior:** Bannerlord does not log a snapshot of loaded modules, mod-stack assembly versions, or `MissionBehaviors` after mission init. When a third-party mod ships a class that declares `BehaviorType=Logic` but doesn't inherit `MissionLogic`, `Mission.AddMissionBehavior` null-casts and the resulting NRE on `Mission.CheckMissionEnded` fires every tick. Without instrumentation, the user's crash report is unactionable — there is no easy way to identify *which* MissionBehavior produced the null.
- **TAOM requirement:** when a user attaches a TAOM log to a bug report, we need to identify the offending mod within seconds rather than hours. The diagnostic also captures `action_set` usage so we can spot LOTRLOME action-set mismatches early (an elf agent running `as_human_warrior` is a configuration bug, not a crash, but the same diagnostic surface is the right place).
- **Without this feature:** every cross-mod NRE report turns into a multi-hour repro session where the user has to disable mods one at a time. See memory `feedback_missionbehaviortype_logic_requires_missionlogic_inheritance` for the recurring pattern this diagnostic was authored against.

## Architecture

### Design Challenge

The diagnostic needs to run *after* vanilla and all other mods have added their `MissionBehaviors` to the mission. `OnMissionTick` is the right gate — `Mission` constructs the behaviors list before tick begins, so the first `OnMissionTick` call sees a stable snapshot. The behavior itself must inherit `MissionLogic` (not just `MissionBehavior`) per `feedback_missionbehaviortype_logic_requires_missionlogic_inheritance` — TAOM's own diagnostic would otherwise be the very kind of bug it is designed to detect.

### Solution Approach

`MissionDiagnosticBehavior : MissionLogic` (Hooks layer) is registered as a `MissionLogic` for every mission. On the first `OnMissionTick`, it dumps:

1. The full `Mission.MissionBehaviors` list, annotating any entry whose `BehaviorType=Logic` but `!is MissionLogic` as the suspected offender.
2. The full `Mission.MissionLogics` list with null-slot indices.

For 5 seconds after mission start, the same behavior scans `Mission.Agents` and logs every unique `(actionSetName, raceName)` combo seen — dedup is handled service-side so the boundary doesn't need a per-agent gate.

A separate `LogSessionSnapshot()` call (driven by an `OnSessionLaunched` boundary registered in `SubModule`) dumps OS, CLR, Bannerlord version, every active module, every loaded BUTR/MCM/Harmony assembly with its version, and a campaign-context line if a save is loaded. All collection reads are independently guarded — `Campaign.Current.GameStarted` can be `false` mid-init even when `Campaign.Current` is non-null.

### Component Diagram

```
OnSessionLaunched boundary    OnMissionTick (first tick)
        |                              |
        v                              v
 LogSessionSnapshot()          DumpMissionStart()
        |                              |
        v                              v
+----------------------------------------------+
|        IMissionDiagnosticService             |
|    (singleton, IModLogger-backed)            |
+----------------------------------------------+
        |                              |
        v                              v
   taom_debug_*.log              taom_debug_*.log
   (session lines)               (mission lines + action_set lines)
```

## Configuration

No config. The diagnostic always runs — its overhead is bounded (single per-mission first-tick dump + 5-second action-set capture window), and the value of "this log file tells you what mod stack the user had" is unconditional.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/MissionDiagnostic/IMissionDiagnosticService.cs` | Service interface — 4 methods: `LogSessionSnapshot`, `LogMissionStartSnapshot`, `LogActionSetSeen`, `ResetForNewMission` |
| `Main/Features/MissionDiagnostic/MissionDiagnosticService.cs` | Singleton implementation. Holds the action-set dedup `HashSet` + `_sessionLogged` once-only guard. All `IModLogger.LogXxx` calls live here. |
| `Main/Features/MissionDiagnostic/MissionDiagnosticIoC.cs` | DryIoc registration — `IMissionDiagnosticService → MissionDiagnosticService` as `Reuse.Singleton` |
| `Main/Features/MissionDiagnostic/Hooks/MissionDiagnosticBehavior.cs` | `MissionLogic` boundary. **Inherits `MissionLogic` deliberately**, not just `MissionBehavior` — otherwise this feature would be the bug it's designed to catch. First-tick gate via `_missionStartLogged`. 5-second action-set window via `_actionSetWindowSecondsLeft`. |

## Dependencies

- `IModLogger` (Core/Logging) — backend that writes to `taom_debug_*.log`
- `IRaceManager` (Core/Domain) — resolves `agent.Character.Race` integer to a human-readable race name (`as_human_warrior` on an elf agent is more obvious when the race column reads `elf` not `id=4`)
- TaleWorlds: `Mission.MissionBehaviors`, `Mission.MissionLogics`, `Mission.Agents`, `MissionBehavior.BehaviorType`, `Agent.ActionSet.GetName()`. All public, no reflection.

## Tests

No service-level unit tests yet. The behavior is integration-test territory (boundary against `Mission`, which can't be mocked cleanly) — manually exercised on every Bannerlord launch, and the log line shape is documented above for future automation.

## How to Read the Output

When a user reports a crash, search the attached `taom_debug_*.log` for `[MissionDiag]` and look for these signatures:

1. **`OFFENDER` lines** — any `[MissionDiag]   [<idx>] <Type>  BehaviorType=Logic  IsMissionLogic=False` is the bug. The `asm=` suffix names the mod. Often there will be multiple offenders if a third-party suite ships several.
2. **`NULL ENTRIES in MissionLogics at indices: [...]`** — confirms the engine-side null-cast happened. Cross-reference indices with the behaviors list above to identify the offender(s).
3. **`ActionSet '<name>' used by race='<race>'`** — for action-set debugging, look for cases where the action set's race prefix doesn't match the race column (e.g. `as_human_warrior` used by `race=elf`).
4. **`Mod-stack assemblies (...)`** — at the top of the log; tells you exact versions of Harmony, MCM, ButterLib, BUTR libraries the user has installed. Cross-reference with TAOM's `Directory.Build.props` references to identify version drift.

## Performance

- **First-tick dump:** O(N) over `MissionBehaviors` + `MissionLogics`. N is typically <50 across all loaded mods. Negligible — a few milliseconds at most, on a tick that already does mission init.
- **Action-set window:** 5 seconds × N agents per tick. Service-level `HashSet<(actionSet, race)>` dedup keeps log volume bounded — most missions emit a handful of unique combos before saturating.
- **Session snapshot:** runs once per game launch. Negligible.
- **Memory:** the dedup `HashSet` resets per-mission via `ResetForNewMission`. No long-lived collections grow unbounded.

## Changelog

- 2026-05-24 — Initial MissionDiagnostic feature: comprehensive crash-investigation logging to `taom_debug_*.log` (session snapshot + first-tick mission-behavior/MissionLogic dump flagging `BehaviorType=Logic` non-`MissionLogic` offenders + 5s action-set capture), best-effort try/catch on every log path so a diagnostic failure never blocks gameplay.

## GitHub Issue

- **Issue:** not separately ticketed — the diagnostic was authored alongside the `feedback_missionbehaviortype_logic_requires_missionlogic_inheritance` rule. See CHANGELOG entries from late May 2026 for the BehaviorTreeWrapper.dll inlining RCA that motivated it.
- **Status:** shipping (in-tree, no toggle)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
