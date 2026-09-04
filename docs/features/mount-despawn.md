# Dead Mount Despawn

## Overview

Retires killed mounts a few seconds after they die instead of leaving them on the battlefield for
the rest of the fight. Ships enabled, five second default, MCM toggle plus a 3 to 30 second slider
under **Performance/Dead Mount Cleanup**.

## Why This Exists

Players reported dead horses lying on the field until the battle ended. A mount is a full `Agent`
carrying a skeleton and a live ragdoll, not a decal, and a horse rig is heavier than a human one, so
a cavalry engagement accumulates corpse agents that cost frame time and do nothing once the rider is
gone.

Scope was set deliberately narrow: killed mounts only. Riderless living mounts and troop corpses are
out, because a body vanishing mid-fight is a visible change to how the battlefield reads and the
report was specifically about mounts.

## Architecture

### Design Challenge

Three engine facts shaped this, all verified against installed v1.4.8 rather than assumed.

**There is nothing vanilla to lean on.** No `AgentRemovalLogic` exists, and no managed corpse-cleanup
mission behavior exists anywhere in the engine. Corpse retirement in singleplayer is entirely native,
driven by `BannerlordConfig.NumberOfCorpses` and `Mission.SetMissionCorpseFadeOutTimeInSeconds`
(`Mission.cs:1723`), whose only vanilla caller is the multiplayer duel mode. The closest precedent is
`SpawningBehaviorBase.cs:227-239`, which fades riderless LIVING mounts after 30 seconds once agent
count passes 90% of the cap, and that is multiplayer only.

**`FadeOut` deletes, it does not hide.** `Agent.FadeOut(bool hideInstantly, bool hideMount)`
(`Agent.cs:4270`) is the only public "make this agent go away" API. Only `hideInstantly` reaches
native (`fade_out`); `hideMount` is managed sugar for fading a rider's mount and is meaningless for a
dead mount, which has no `MountAgent` of its own. Native later fires `Mission.OnAgentDeleted`
(`Mission.cs:2972`), which drops the agent from `_allAgents`, sets `State = Deleted`, nulls `Team`
and lets `Agent.Clear()` zero every native pointer. Holding a stale `Agent` past that point and
touching any property is a native access violation.

**Agent indices are reused.** So identity has to be dropped at deletion time, not inferred later.

### Solution Approach

The behavior owns the engine handles; the service owns every timing decision and sees nothing but
`int` and `float`. That split is what makes the scheduling rules unit-testable with no live
`Mission`, and it is why this feature adds nothing to `IAgentAdapter`.

Deliberately NOT routed through `IMissionAdapterFactory`: that cache is keyed on `agent.Index` and
holds a strong `Agent` reference ([MissionAdapterFactory.cs:24](../../Main/Adapters/MissionAdapterFactory.cs#L24)),
so pushing dead agents through it would keep cleared agents reachable and lean on index identity the
cache cannot guarantee across a delete.

### Component Diagram

```
Mission.OnAgentRemoved (Killed + IsMount)
        │
MountDespawnMissionBehavior          ← owns Dictionary<int, Agent>, the only place Agent is touched
        │  index + mission time
IDeadMountDespawnService             ← schedule, delay clamp, per-sweep budget. No engine types.
        │  indices whose delay elapsed
MountDespawnMissionBehavior.FadeOne  → Agent.FadeOut(hideInstantly: false, hideMount: false)
        │
Mission.OnAgentDeleted → behavior drops the handle, service forgets the index
```

Sweep runs on a 0.5 second accumulator inside `OnMissionTick`, and returns immediately when nothing
is scheduled.

### The traps this feature is built around

| Trap | Handling |
|---|---|
| `FadeOut` can drive `OnAgentDeleted` synchronously, which mutates the pending dictionary | The behavior copies the due list into its own scratch buffer before iterating, so the fade loop owns everything it walks. `CollectDue` returns the service's reused buffer, and the copy is what keeps that from becoming a cross-file invariant held together only by a comment (deep review 2026-09-03, finding 2). `Mission.AllAgents` is never enumerated at all, so the engine's unguarded `AgentList` is never at risk. |
| A deleted agent's getters dereference zeroed native pointers | `OnAgentDeleted` drops the handle first. The fade path reads only `IsFadingOut()`, never `State`. |
| Index reuse after deletion | Deletion always precedes an index being handed to a new agent, and `OnAgentDeleted` is where the entry dies. |
| The service is `Reuse.Singleton`, so it outlives the mission | `OnEndMission` clears. Mission time restarts near zero in the next battle, so a surviving entry would schedule a fade against a different agent entirely. |
| A NaN mission time or MCM delay | Every gate is a positive requirement (`!(elapsed >= delay)`), and the delay is clamped through `FiniteFloatValidator.IsFiniteInRange` at one chokepoint in the service, not at the provider. A rejected delay logs a warning once per mission rather than falling back in silence. |
| Town and story scenes deliberately keep corpses (`DisableCorpseFadeOut`, `CorpseDraggingMissionLogic`) | The mission gate is an allowlist of field battle, siege and sally-out. |
| A mass casualty moment | Eight fades per sweep at most. Thirty fades in one frame is the same stutter the feature exists to remove; the remainder comes back half a second later. |

### What is safe, and why

No loot or reward path reads a mount agent after death. `MountAgentLogic` (the player's lame-horse
roll and harness return), `BattleAgentLogic`, `CasualtyHandler`, `AgentVictoryLogic` and
`PartyAgentOrigin` all run inside `OnAgentRemoved`, so all of it has completed before the fade timer
even starts. A grep of the whole `Campaign` category for `MountAgent` returns zero hits: campaign
horse loot comes from troop `Equipment`, never from agents.

### Known consequence, accepted

Corpses occupy agent slots. `DefaultBattleMissionAgentSpawnLogic.NumberOfAgents` is
`Mission.AllAgents.Count`, and `CheckMinimumBatchQuotaRequirement` sizes reinforcement batches as
`MaxNumberOfAgentsForMission - NumberOfAgents`. Freeing slots therefore lets reinforcement batches
pass the quota check sooner, so troops arrive somewhat earlier than in vanilla. This is inherent and
cannot be designed around. The net win is the saved ragdoll and skeleton cost, not a smaller agent
count. The MCM hint says so.

This is also why both settings are **simulation-relevant** for co-op parity: two peers holding
different values would spawn reinforcements at different times in a shared battle. They are covered
by `CoopSettingsRelevance` include-by-default with no exclusion entry.

## Configuration

MCM group **Performance/Dead Mount Cleanup**, `GroupOrder = 49`.

| Setting | Type | Default | Range |
|---|---|---|---|
| `EnableDeadMountDespawn` | bool | `true` | |
| `DeadMountDespawnDelaySeconds` | float | `5` | 3 to 30, out-of-range falls back to 5 |

Both are re-read live every sweep, so a mid-battle toggle takes effect at once. The 3 second floor
exists because below it the corpse pops while the death animation is still playing.

## Key Files

| File | Role |
|---|---|
| [Main/Features/MountDespawn/Hooks/MountDespawnMissionBehavior.cs](../../Main/Features/MountDespawn/Hooks/MountDespawnMissionBehavior.cs) | Entry point. Owns the `Agent` handles, does the fade. |
| [Main/Features/MountDespawn/Hooks/MountDespawnMissionGate.cs](../../Main/Features/MountDespawn/Hooks/MountDespawnMissionGate.cs) | Mission allowlist. |
| [Main/Features/MountDespawn/DeadMountDespawnService.cs](../../Main/Features/MountDespawn/DeadMountDespawnService.cs) | Schedule, clamp, budget. |
| [Main/Features/MountDespawn/MountDespawnSettingsProvider.cs](../../Main/Features/MountDespawn/MountDespawnSettingsProvider.cs) | MCM isolation. Passes values through raw. |
| [Main/Features/MountDespawn/MountDespawnIoC.cs](../../Main/Features/MountDespawn/MountDespawnIoC.cs) | Both registrations `Reuse.Singleton`. |
| `Main/SubModule.cs` `OnMissionBehaviorInitialize` | Registers the behavior unconditionally. |

## Dependencies

None beyond the engine and MCM. No Harmony patch, no GameModel, no adapter change, no new
player-facing in-game text (so no `/localize` run).

## Tests

`TAOM.Tests/Features/MountDespawn/`, 26 tests.

- `DeadMountDespawnServiceTests` covers timing boundaries, `Forget`, both disabled paths, the
  per-sweep budget and its remainder, session reset, and the non-finite cases for both mission time
  and the MCM delay.
- `MountDespawnWiringTests` pins the `IoC.cs` and `SubModule.cs` registration lines, `MissionBehavior`
  inheritance, and `BehaviorType == Other`.

**Not unit tested:** the fade itself, which needs a live mission.

## Performance

The sweep is a 0.5 second accumulator that returns on the first line when nothing is scheduled, and
allocates nothing (the due list is a reused buffer). It never walks `Mission.AllAgents`.

## Changelog

2026-09-03: shipped. Deep review the same day: 4 findings fixed, 3 rejected, RCA at `docs/reviews/rca-mount-despawn-2026-09-03.md`.

## GitHub Issue

Not yet filed. Owed.
