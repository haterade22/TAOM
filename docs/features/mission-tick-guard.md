# Mission Tick Guard

## Overview

One exception in a mission tick no longer freezes the game. The frame's agent tick is skipped and the exception still reaches the crash report. The guard is Patch91b, port decision D17 of the 1.4.8 port programme (`plans/106-offthread-and-tick-safety.md`).

## Why This Exists

- **Vanilla behavior (v1.4.8, `TaleWorlds.MountAndBlade.Mission.cs`):** `OnTick` (`:3636`) clears the private `tickCompleted` at `:3740`, runs every `MissionBehavior.OnMissionTick` at `:3741-3744`, and starts the agent tick last at `:3768-3775` (`TickAgentsAndTeamsAsync` when `doAsyncAITick`, otherwise `TickAgentsAndTeamsImp`). Only the agent tick sets the flag again (`:3613`). The next frame's `OnPreTick` (`:3529-3538`) calls `WaitTickCompletion` first, which spins with no timeout (`:3585-3591`).
- **The swallowers on 1.4.x:** every catcher above that window swallows, so nothing ends the wait:

| Frame (inner to outer) | Catcher | Swallows |
|---|---|---|
| `Mission.OnTick` | PatchShield, from a process's second game start (because Patch35 patches it) | `MissingMethod`, `MissingField`, `TypeLoad` only |
| `MissionState.OnTick` | PatchShield from the second start (Patch43) | missing-API only |
| `Module.OnApplicationTick` | Patch37, `[HarmonyPriority(800)]` | everything; writes the `[CrashReport]` capture |
| `Managed_ApplicationTick` shim | Native2Managed bridge | everything, until WP107's allowlist |

- **TAOM requirement:** a mod with this many `OnMissionTick` overrides needs a throw in one of them to cost less than the session. Ten of TAOM's 19 overrides have no `try` (a try/catch sweep is deferred, see the plan's Maintenance notes).
- **Without this feature:** a throw from any `OnMissionTick` (vanilla, TAOM or another mod), or the QA toggle "Throw On Next Mission Tick", freezes the game for good with audio still playing.

Neither this hazard nor the off-thread writes of #634 is shown to be the cause of the player freezes that prompted this work. This closes the hazard; it does not claim those reports.

## Architecture

### Design Challenge

`Mission` is sealed and the flag is private, and the only safe moment to restore it is narrow: once the agent tick has launched, a false flag means the job still runs, and a restore would let the next frame's behaviours overlap it.

### Solution Approach

- **Prefix** on `Mission.OnTick`, `Priority.First`: `__state = armed && tickCompleted` (the flag read at entry). The state machine clears its launch mark. A false flag at entry means a previous agent tick may still run, so that call is never recovered.
- **Launch mark**: a void finalizer (not a prefix) on `Mission.TickAgentsAndTeamsAsync`, `Priority.First`. PatchShield strips prefixes, postfixes and transpilers but never finalizers (`PatchShield.cs:376-378`), and a finalizer runs whether or not the launch call threw.
- **Patch order (load-bearing)**: the launch class must be patched BEFORE the `Mission.OnTick` class. `TickAgentsAndTeamsAsync` is 18 bytes of IL, small enough to inline into an `OnTick` rebuilt before the target was patched, which would lose the mark. Plan review measured this on a stand-in (net472 x64, Lib.Harmony 2.4.2, 5000 calls): target patched first or `OnTick` rebuilt after it, 5000 marks; `OnTick` last rebuilt before the target was patched, 0 marks. `PatchCategoryIndex.Apply` patches classes in assembly order, which is source order inside one file, so the launch class stays first in the file. A binding test pins it.
- **Finalizer** on `Mission.OnTick`, void, `Priority.First` (it must see the exception before PatchShield's default-priority finalizer swallows a missing-API one). Void keeps Harmony's `rethrow`, so Patch37 still reports the exception with its stack.
- **Arming**: `MissionTickRecoveryWiring.VerifyAndArm` runs right after the category applies and arms only when both patches appear in `Harmony.GetPatchInfo` and the private field resolves. It never throws: a throw there would skip later categories of the game-init batch.

The decision cells, of which exactly one restores:

| Entered armed (flag set at entry) | Threw | Launched an agent tick | Flag now | Result |
|---|---|---|---|---|
| yes | yes | no | false | **restore** (throttled log) |
| yes | yes | no | true | nothing (the throw came before the clear, or an inline agent tick finished) |
| yes | yes | yes | any | nothing (the job owns the flag) |
| yes | no | any | any | nothing |
| no | any | any | any | nothing |

### Component Diagram

```
SubModule.OnGameInitializationFinished
        |
  TryPatchCategory("Patch91b_MissionTickRecovery")
        |
  MissionTickRecoveryWiring.VerifyAndArm
        |
  Mission_TickAgentsAndTeamsAsync_LaunchMark_Patch   Mission_OnTick_TickRecovery_Patch
                         \                               /
                          TickHandshakeRecovery (pure, no engine types)
```

## Configuration

None, by design: the guard acts only in a state that otherwise never ends, so there is no MCM toggle. Three log lines, all in the TAOM log:

| Line | Level | When |
|---|---|---|
| `[TickRecovery] armed: ...` | INFO | once per process, at game start |
| `[TickRecovery] off (...)` | WARNING | at game start, when a piece is missing; the guard is inert |
| `[TickRecovery] Mission.OnTick threw ...` | ERROR | on a restore: occurrences 1, 2, 10, 100 and so on |

## Performance

One flag read in the prefix, one in the finalizer, and one bool write at the launch mark on each `Mission.OnTick`. No allocation.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/MissionTickGuard/TickHandshakeRecovery.cs` | The pure state machine (no engine types, ADR-007) |
| `Main/Features/MissionTickGuard/Hooks/Patch91b_MissionTickRecovery.cs` | The two patch classes and `MissionTickRecoveryWiring` |
| `Main/SubModule.cs` | Applies the category and arms it in the game-init batch, after Patch91 |

## Dependencies

- Patch37 (`Module.OnApplicationTick`) still reports the exception: the recovery's finalizer is void, so the exception keeps travelling.
- Patch35 (`Patch35_Mission_OnTick`, a postfix) shares the `Mission.OnTick` target.
- PatchShield wraps both targets from a process's second game start and runs after the recovery.
- Patch91 ([battle-load-diagnostics.md](battle-load-diagnostics.md), the battle-freeze sampler) photographs what this guard cannot recover.

## Tests

- `TAOM.Tests/Features/MissionTickGuard/TickHandshakeRecoveryTests.cs`: 11 tests on the decision cells, the launch mark's one-call life and the log throttle.
- `TAOM.Tests/Features/MissionTickGuard/TickHandshakeRecoveryHarmonyTests.cs`: 4 tests with real Harmony on a stand-in shaped like `Mission.OnTick`: a throw before the launch restores and still reaches the caller, a postfix throw after the launch leaves the flag down, a lower-priority swallow still lets the recovery run first, and without its prefix the finalizer never restores.
- `TAOM.Tests/Features/MissionTickGuard/Patch91bMissionTickRecoveryBindingTests.cs`: 6 tests: five read the installed engine (the method shapes, the private field, the clear-behaviours-launch order in the IL, exactly two writes of the flag in all of `Mission`) and one pins the registration, including that the launch class patches first.

## How to verify in game

In a custom battle, open Mod Options, the TAOM Crash Report page, QA Dev Triggers group, and turn on "Throw On Next Mission Tick". The crash-report inquiry appears, the battle keeps running after you close it, and the log holds `[TickRecovery] armed` (from game start) and one `[TickRecovery] Mission.OnTick threw TaomDevTriggerException ...` ERROR. Repeat once more in the same battle: a second ERROR line (occurrence 2 is logged).

## Limitations

- A stuck agent tick is not recovered; Patch91's `[MissionStall]` line names it.
- An exception on the agent-tick thread is not recovered: until WP107 the Native2Managed bridge swallows it and the game still freezes; after WP107 it reaches native.
- A throw before the clear or after the launch needs no restore.
- A throw every frame skips every agent tick: degraded play, though menus still work.
- After PatchShield swallows a missing-API exception on `Mission.OnTick` it strips TAOM's prefix there (owner `com.taom.mod` is not on its protected list), so later calls are not recovered (WP107 hand-off).
- A foreign `Priority.First` finalizer registered earlier that swallows hides the exception from the guard.

## Changelog

- 2026-11-04: Ported to the 1.4.8 line (port decision D17).

## GitHub Issue

Pending (orchestrator).
