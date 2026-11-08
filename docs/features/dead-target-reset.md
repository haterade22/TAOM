# Dead-target reset

## Overview

Once a second, in every mission, TAOM clears the target of each AI soldier whose target is dead or unconscious. It uses the
engine's own reset, `Agent.InvalidateTargetAgent`. This prevents a native crash at `TaleWorlds.Native.dll` RVA `0x6B716E`
(issue #1096). A soldier that still holds a dead target picks a new one within a second, as after any reset. Before the fix
such a soldier could hold the dead target until the engine deleted the corpse. A soldier whose target is alive is not touched.

## Why This Exists

- **Vanilla behavior:** a death does not clear the target of the soldiers that target the dead agent. The engine's corpse handler
  later fades the corpse without a rout, and 30 s after the fade starts it deletes the agent. The delete sets every soldier's target
  on it to -1 but leaves the soldier's "target is valid" flag set.
- **The crash:** on the next AI tick the engine trusts the flag and reads agent slot -1, which is null, and the game crashes
  (`movss xmm13, [r8+0xC]` with `r8` = 0).
- **TAOM requirement:** large fights where many soldiers die close together, with TAOM's corpse limits, hit it about once in 22
  unattended Fords of War matches. Nothing in the mechanism is specific to Fords of War, so the fix runs in every mission.

## Architecture

### Design Challenge

The bad state is inside the engine's AI record, and the fault is in native code that Harmony cannot reach. The managed
`Agent.SetTargetAgent(null)` is not a fix: natively it stores -1 as a pending target and also leaves the flag set.

### Solution Approach

What the native code does (Ghidra decompile of the v1.5.6 changeset 125270 DLL, read on 2026-11-07):

- The faulting soldier in the 2026-11-07 dump had the flag at AI record `+0x19c` = 1 and the target index at `+0x1504` = -1. Its
  last target, agent 80, was a slot deleted 30.07 s after its fade began, so it was deleted on the tick before the crash.
- The corpse handler `FUN_1805b3b80` starts the fade through `FUN_1805eb010` and never calls the rout `FUN_1805f02b0`. The delete
  `FUN_1805bced0` clears targets through `FUN_1805d6720`, which writes the index and not the flag. The change to the Deleted
  state (`IMBAgent.SetStateFlags`, the state 5 branch of `FUN_1805f02b0`) also writes the index without the flag. The sweep
  prevents both, because it clears the target before either runs.
- A rout is safe: it resets each targeting soldier through `FUN_1805d6640`, which clears the target and the flag together. So
  `Agent.FadeOut`, which routs first, is not the trigger (TAOM's tower gunners, riderless mounts and dead-mount despawn).
- `Agent.InvalidateTargetAgent` reaches `IMBAgent_InvalidateTargetAgent` (`0x6E3240`), which calls the same `FUN_1805d6640` when the
  agent's controller is AI (agent `+0x568` == 1), and does nothing for any other controller.

`DeadTargetResetMissionBehavior` sweeps `Mission.Agents` from `OnMissionTick`, once a second of mission time and once at the first
tick. That runs on the main thread, before the engine starts the asynchronous agent tick. For each active, AI-controlled agent whose
`GetTargetAgent()` is killed or unconscious, it calls `InvalidateTargetAgent`. A routed target is left alone: a routed agent
has left the battle, the rout already reset every soldier that targeted it, and target selection never picks an agent that is
not active. Automatic target selection accepts only active agents, so a reset soldier never picks the dead agent again. Only a
forced target (a detachment task, for example) can set a dead target, and the next sweep resets it again. The 30 s delete delay
is far longer than the 1 s sweep.

### Component Diagram

```
DeadTargetResetModule (feature module, every mission)
        |
DeadTargetResetMissionBehavior (OnMissionTick: sweep; OnEndMission: one log line)
        |
DeadTargetSweep (interval, IsStale rule, counts)
```

## Configuration

None. The sweep has no setting: it has no effect on a soldier whose target is alive.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/DeadTargetReset/DeadTargetResetModule.cs` | Feature module: adds the behavior to every mission |
| `Main/Features/DeadTargetReset/Hooks/DeadTargetResetMissionBehavior.cs` | The sweep and the mission-end line |
| `Main/Features/DeadTargetReset/DeadTargetSweep.cs` | The once-a-second clock, the stale-target rule and the counts |
| `Main/Features/CoopInterop/Missions/CoopMissionRoles.cs` | Its co-op role row (AgentOwner) |

## Dependencies

- `IModLogger`: the mission-end line and the one failure line.

## Tests

- `TAOM.Tests/Features/DeadTargetReset/DeadTargetSweepTests.cs`: the stale-target rule per agent state, the once-a-second clock
  (and a NaN, infinite or non-positive frame), the mission-end line.
- `TAOM.Tests/Features/DeadTargetReset/DeadTargetResetWiringTests.cs`: the module list, the declared behavior, the engine reset
  (never `SetTargetAgent`), the active AI gate and the co-op role.
- `DeadTargetSweepTests.TheInGameCheck_PassesOnASweepThatReset_AndOnlyThen`: the patterns in `tools/ingame_checks/1096.toml`
  match the mission-end line, so a format change cannot leave the check unable to pass.

## How to verify in game

**Preconditions:** any battle with deaths, for example an unattended Fords of War match (`/game-run`) or a large custom battle.

1. Fight until soldiers die, then end the mission. The TAOM log writes `[DeadTarget] mission end: sweeps=N reset=M`. `reset` above 0
   means the sweep cleared dead targets (`tools/ingame_checks/1096.toml`). A `[DeadTarget] sweep failed` line is a failure.
2. Over an unattended Fords of War round at 4x, no crash at `TaleWorlds.Native.dll` RVA `0x6B716E` (the round had five in about 110
   matches before the fix). Only a round of matches can show this; one match proves nothing.

## Performance

One `GetTargetAgent` call per active AI agent per second of mission time: about 2,000 native calls a second in the largest battle,
none per frame in between.

## Changelog

- 2026-11-07: added (#1096).

## GitHub Issue

- **Issue:** #1096: [CTD at TaleWorlds.Native 0x6B716E: a corpse delete leaves a soldier's stale target flag](https://github.com/haterade22/TAOM/issues/1096)
- **Status:** Open until the in-game round has run.
