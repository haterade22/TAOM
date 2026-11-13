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

The same behavior logs every unique `(actionSetName, raceName, sex)` combination among the agents of the mission. It does not poll agents: `OnAgentCreated` queues each new agent (`PendingAgentReads`) and the next `OnMissionTick` reads it once; an agent deleted or removed before that tick is dropped. Each agent is first checked on an integer key (the action set's engine index, the race id and the sex, `TryMarkActionSetKey`); only a combination not yet seen this mission reads the action set name, the race name and the agent name, so an agent costs three managed reads and one native index read, once. The reads run for the whole mission, so a reinforcement after the first 5 seconds still gets its line. The 5 seconds frame the census header and the single summary (see "How to Read the Output").

Why the next tick and why `OnAgentCreated` (decompiled v1.5.7): `Mission.CreateAgent` raises `OnAgentCreated` over every behavior (`missionBehavior.OnAgentCreated(agent);`), and `SpawnAgent` (`Agent agent = CreateAgent(agentBuildData.AgentMonster, ...)`), its mount (`CreateHorseAgentFromRosterElements`) and `SpawnMonster` (`Agent agent = CreateHorseAgentFromRosterElements(equipmentElement, ...)`) all reach it. `OnAgentBuild` is raised only by `SpawnAgent` (`missionBehavior3.OnAgentBuild(agent, ...)`); `SpawnMonster` ends with `BuildAgent(agent, null);` and never raises it. The action set an agent keeps is set after `SpawnAgent` returns: SandBox `MissionAgentHandler` runs `Agent agent = base.Mission.SpawnAgent(agentBuildData);` then `agent.SetActionSet(ref animationSystemData);` with `MBGlobals.GetActionSet(locationCharacter.ActionSetCode)`, and `Mission.SpawnTroopWithAgentBuildData` (and its equipment variant) calls `agent.SetActionSet(ref animationSystemData)` for a `specialActionSetSuffix`. Reading in `OnAgentBuild` therefore logged the build-time set, not the one the agent keeps (the `as_human<suffix>` case this census exists for), in towns, halls, villages, prisons and hideouts. A read that throws logs one warning and stops reading for the mission. The window logs one header when it opens and one summary when it closes (see "How to Read the Output").

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

No config. The diagnostic always runs, its overhead is bounded (single per-mission first-tick dump + action-set census), and the value of "this log file tells you what mod stack the user had" is unconditional.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/MissionDiagnostic/IMissionDiagnosticService.cs` | Service interface, 7 methods: `LogSessionSnapshot`, `LogMissionStartSnapshot`, `LogActionSetSeen`, `TryMarkActionSetKey`, `LogActionSetCensusOpened`, `LogActionSetCensusClosed`, `ResetForNewMission` |
| `Main/Features/MissionDiagnostic/MissionDiagnosticService.cs` | Singleton implementation. Holds the two action-set dedup sets (the integer key and the logged line's string key), the census totals + `_sessionLogged` once-only guard. All `IModLogger.LogXxx` calls live here. |
| `Main/Features/MissionDiagnostic/MissionDiagnosticIoC.cs` | DryIoc registration — `IMissionDiagnosticService → MissionDiagnosticService` as `Reuse.Singleton` |
| `Main/Features/MissionDiagnostic/Hooks/MissionDiagnosticBehavior.cs` | `MissionLogic` boundary. **Inherits `MissionLogic` deliberately**, not just `MissionBehavior`, otherwise this feature would be the bug it's designed to catch. First-tick gate via `_missionStartLogged`. Queues agents in `OnAgentCreated`, drops them in `OnAgentRemoved` and `OnAgentDeleted`, reads them on the next tick. 5-second census window via `_actionSetWindowSecondsLeft`. |

## Dependencies

- `IModLogger` (Core/Logging) — backend that writes to `taom_debug_*.log`
- `IRaceManager` (Core/Domain) — resolves `agent.Character.Race` integer to a human-readable race name (`as_human_warrior` on an elf agent is more obvious when the race column reads `elf` not `id=4`)
- TaleWorlds: `Mission.MissionBehaviors`, `Mission.MissionLogics`, `Mission.Agents`, `MissionBehavior.BehaviorType`, `Agent.ActionSet.GetName()`. All public, no reflection.

## Tests

`TAOM.Tests/Features/MissionDiagnostic/PendingAgentReadsTests.cs` covers the queue (read once, only on the next tick, dropped when removed, never twice for one agent, order, a throwing reader). `TAOM.Tests/Features/MissionDiagnostic/MissionDiagnosticServiceTests.cs` covers the action-set integer key (first and repeat sighting, each key part, the per-mission reset) and pins the census line text, the census header and the census summary. `TAOM.Tests/Features/MissionDiagnostic/MissionDiagnosticBehaviorTests.cs` (`RequiresGame`) covers the census window's single close: when the window runs out, when the mission is shorter than the window, and on a NaN frame time. The read of an engine `Agent` in the behavior is exercised in game on every launch; the census-opened-by-a-spawn-before-the-first-tick path is not unit tested (no `Agent` can be built in the tests).

## How to Read the Output

When a user reports a crash, search the attached `taom_debug_*.log` for `[MissionDiag]` and look for these signatures:

1. **`OFFENDER` lines** — any `[MissionDiag]   [<idx>] <Type>  BehaviorType=Logic  IsMissionLogic=False` is the bug. The `asm=` suffix names the mod. Often there will be multiple offenders if a third-party suite ships several.
2. **`NULL ENTRIES in MissionLogics at indices: [...]`** — confirms the engine-side null-cast happened. Cross-reference indices with the behaviors list above to identify the offender(s).
3. **`ActionSet '<name>' used by race='<race>'`** — for action-set debugging, look for cases where the action set's race prefix doesn't match the race column (e.g. `as_human_warrior` used by `race=elf`).
4. **`Mod-stack assemblies (...)`** — at the top of the log; tells you exact versions of Harmony, MCM, ButterLib, BUTR libraries the user has installed. Cross-reference with TAOM's `Directory.Build.props` references to identify version drift.
5. **The census window** (INFO, once each per mission). The header, written when the first agent is created or on the first tick, whichever comes first:
   `[MissionDiag] ActionSet census open: window=5.0s, one line per (action set, race, sex); names are read only for a new (action set index, race id, sex) key`.
   The summary, written when the window runs out, when a non-finite frame time ends the window, or at mission end if the census was opened and the mission was shorter (a mission that ends before its first tick still gets its summary):
   `[MissionDiag] ActionSet census closed: agentChecks=240 newKeys=6 lines=5`. `agentChecks` counts the agents read before the summary was written
   (once each; an agent read after the 5 s window gets its line but is in no summary), `newKeys` the checks that found a new integer key and read names, `lines` the
   `ActionSet` lines written. `newKeys` above `lines` means two keys shared a line (two unknown race ids both resolve
   to the human race name).

## Performance

- **First-tick dump:** O(N) over `MissionBehaviors` + `MissionLogics`. N is typically <50 across all loaded mods. Negligible — a few milliseconds at most, on a tick that already does mission init.
- **Action-set census:** one read per spawned agent, on the tick after its spawn; no per-frame poll of all agents. The integer pre-filter keeps the per-agent cost to managed reads, and the service-level `(actionSet, race, sex)` dedup keeps log volume bounded; most missions emit a handful of unique combos before saturating.
- **Session snapshot:** runs once per game launch. Negligible.
- **Memory:** both action-set dedup sets reset per mission via `ResetForNewMission`. No long-lived collections grow unbounded.

## Changelog

- 2026-05-24 — Initial MissionDiagnostic feature: comprehensive crash-investigation logging to `taom_debug_*.log` (session snapshot + first-tick mission-behavior/MissionLogic dump flagging `BehaviorType=Logic` non-`MissionLogic` offenders + 5s action-set capture), best-effort try/catch on every log path so a diagnostic failure never blocks gameplay.

## GitHub Issue

- **Issue:** not separately ticketed — the diagnostic was authored alongside the `feedback_missionbehaviortype_logic_requires_missionlogic_inheritance` rule. See CHANGELOG entries from late May 2026 for the BehaviorTreeWrapper.dll inlining RCA that motivated it.
- **Status:** shipping (in-tree, no toggle)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/battle-load-diagnostics.md](./battle-load-diagnostics.md)
- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
