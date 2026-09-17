# Mission Perf Heartbeat

## Overview

One `[MissionPerf]` line in the TAOM debug log every five seconds of wall clock while a mission
ticks: frames, fps, average / p95 / max frame time in milliseconds, agent and formation counts,
garbage collections since the last line. It is the in-mission counterpart of `[MemSample]`
and the measurement a battle-AI or content change is judged against. Written for the culture
doctrine A/B (#608) and kept for every mission.

## Why This Exists

- **Vanilla behavior:** no frame-time record. `[MemSample]` (Battle Load Diagnostics) has no
  frame counter; `[MapLoad]` reports fps on the campaign map only.
- **TAOM requirement:** an A/B of a team-AI change needs frame times from the same battle with
  the feature on and off, in a form a script can compare.
- **Without this feature:** "it felt fine" is the only evidence a perf change leaves.

## Architecture

`MissionPerfHeartbeatBehavior : MissionLogic` measures wall clock between consecutive
`OnMissionTick` calls (`Stopwatch.GetTimestamp`, no allocation), feeds `FrameStats`, and every
five seconds logs `MissionPerfLine.Build(...)` with `Mission.AllAgents.Count`,
`Mission.Agents.Count` (active), the count of formations with units, and the `GC.CollectionCount`
deltas. Wall clock, not mission time: mission time slows under time scaling and stops in the
order menu, and the line is about what the player feels.

`FrameStats` is pure: count, average and max cover every frame; the percentile set is bounded
to the newest 4,096 samples (nearest-rank p95 over a sorted copy once per window). It resets
its clock at `OnCreated` and `OnEndMission`. The behavior self-disables for the mission after a
single exception.

Cost: one timestamp per frame, one sort of at most 4,096 doubles per five seconds.

## Configuration

Battle Load Diagnostics MCM page, `Mission Performance` group: `EnableMissionPerfHeartbeat`,
default on. Read once per tick from the MCM instance; instrumentation only, so it is excluded
from the co-op settings fingerprint.

## Log line

```
[MissionPerf] t=+65s frames=300 fps=60.0 avgMs=16.67 p95Ms=25.50 maxMs=40.3 agents=812 active=640 formations=9 gc0=12 gc1=3 gc2=1
```

`t` is seconds since the mission was created; the first line lands at +5 s.

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/MissionPerf/FrameStats.cs` | Pure window: record, should-emit, emit |
| `Main/Features/MissionPerf/MissionPerfLine.cs` | The line format |
| `Main/Features/MissionPerf/Hooks/MissionPerfHeartbeatBehavior.cs` | The `MissionLogic` |
| `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs` | The toggle |

## Tests

`TAOM.Tests/Features/MissionPerf/FrameStatsTests.cs`: cadence, average, nearest-rank p95,
single sample, empty window (zeros, not NaN), window reset, bounded sample set, clock reset,
line format.

## Reading an A/B

Take the lines from 30 s after both sides are AI-controlled (F6) to the first rout, compare the
median `avgMs` and `p95Ms` between the two runs; `gc2` should not climb faster with the feature
on. Three runs per cell is the floor, AI battles vary.
