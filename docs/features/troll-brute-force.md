# Troll Brute Force

## Overview

Both battle trolls (`cave_troll`, `hill_troll`) get one area attack, Brute Force (#649): when an enemy is close and
in front, the troll plays a two-handed smash, and at the moment the weapon lands every enemy human inside a ring
ahead of it takes a Blunt blow, is knocked back and, unless blocking with a shield, knocked down. A per-troll
behaviour tree drives it; the rest of the troll's fighting is the engine's ordinary melee.

## Why This Exists

- **Vanilla behavior:** a Bannerlord agent attacks one target per swing, whatever its size.
- **TAOM requirement:** a troll wading into a line should scatter it, the way the films show.
- **Without this feature:** a troll is a slow, tall man with a big mace.

## Architecture

### Design Challenge

The engine has no area melee for an agent, and the ring must hit on the frame the clip's weapon lands, not when
the action starts. A mission tick holds agent handles across frames, and the engine recycles agent slots (#592).

### Solution Approach

The creature behaviour-tree pattern (the elephant's and warg's): `TrollBruteForceMissionBehavior` (a
`MissionLogic`) attaches `TrollBruteForceBehaviorTree` to every agent whose Monster is a key of
`TrollBruteForceConfig.ActionSetsByMonster`; `BehaviorTreeMissionLogic.OnMissionTick` ticks the trees on the main
thread. `BruteForceReadyDecorator` passes when the smash is off cooldown, the troll is free and an enemy is in
reach and in front; `BruteForceTask` plays `act_troll_brute_force` on channel 0 and, once the clip's progress
reaches `ImpactFraction`, calls `BruteForceRing.Deliver`, which applies `CustomAttacksUtils.TakeDamage` to each
enemy human in the ring. Every decision (cooldown, engagement, impact centre, falloff, shield handling, body size)
is in the pure `TrollBruteForceService` (floats in, no TaleWorlds types, ADR-007; a NaN or infinity fails closed).
Each held agent handle is checked with `AgentSlotIdentity.IsCurrentOccupant` before it touches native state.

**Body size.** Every distance is multiplied by the troll's body size, `AgentScale` times its Monster's standing eye
height over the human's 1.70 (`BodySize`, Mike 2026-09-25), capped at 3. The cave troll keeps the human eye
height, so its size is all `AgentScale` (`min_scale` 1.9); the hill troll's size is in its own skeleton (eye height
3.58), so `AgentScale` alone would have given it about half the cave troll's reach.

```
TrollBruteForceMissionBehavior (MissionLogic, attaches per Monster id)
        |
TrollBruteForceBehaviorTree -> BruteForceReadyDecorator -> BruteForceTask
        |                                                       |
        +------------------ ITrollBruteForceService ------------+
                                                                |
                                     BruteForceRing -> CustomAttacksUtils.TakeDamage
```

## Configuration

Compiled constants in `Main/Features/TrollBruteForce/TrollBruteForceConfig.cs` (first guesses, to be tuned in the
Custom Battle smoke). Distances are metres at body size 1.

| Constant | Value | Meaning |
|---|---|---|
| `CooldownSeconds` | 15 | Mission time between smashes |
| `TriggerRange` | 2.6 | An enemy this close, and within about 60 degrees of the facing (`FacingDot` 0.5), starts it |
| `ImpactForward` | 1.8 | The ring's centre, ahead of the troll |
| `InnerRadius`, `OuterRadius` | 1.5, 3.5 | Full damage inside, the engine's area falloff to the edge |
| `CentreDamage` | 40 | Blunt, falling to about a ninth at the edge |
| `ShieldBlockedMultiplier` | 0.5 | A shield-blocking victim takes half and is not floored |
| `ImpactFraction` | 0.58 | Clip progress at which the weapon lands (measured on the Fab heavy attack) |
| `MaxBodyScale` | 3 | Cap on body size |
| `ReferenceEyeHeight` | 1.70 | The human Monster's eye height |

The action and its bindings live in the UNVERSIONED `LOTRLOME_Armory`: `act_troll_brute_force` (untyped) in
`action_types.xml`, bound in `as_cave_troll_warrior` by `tools/bind_troll_action_set.py` and in
`as_hill_troll_warrior` by `tools/bind_hill_troll_action_set.py` (`EXTRA_BINDINGS`, `anim_hill_troll_attack1`).
On each mission's first tick the mission behavior logs an error when the action resolves to `act_none`, when a
troll's set is missing, or when a set has no clip for the action (a reinstall drops all three).
`python tools/wire_hill_troll_race.py --check` is the out-of-game gate for the hill troll's set.

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/TrollBruteForce/TrollBruteForceConfig.cs` | Constants, Monster ids, `ActionSetsByMonster` |
| `Main/Features/TrollBruteForce/ITrollBruteForceService.cs`, `TrollBruteForceService.cs` | Pure decisions |
| `Main/Features/TrollBruteForce/TrollBruteForceMissionBehavior.cs` | Attaches the trees; start-up drift guard |
| `Main/Features/TrollBruteForce/TrollBruteForceBehaviorTree.cs`, `BehaviorTreeElements/` | The tree, decorator and task |
| `Main/Features/TrollBruteForce/Hooks/BruteForceRing.cs` | Delivers the ring |
| `Main/Features/TrollBruteForce/TrollBruteForceIoC.cs` | Singleton service registration |

## Tests

`TAOM.Tests/Features/TrollBruteForce/`: `TrollBruteForceServiceTests` (every decision, NaN and bad-scale gates,
body size), `TrollBruteForceConfigTests`, and `TrollBruteForceWiringTests` (`LiveInstall`: each Monster names its
set, the action is declared once and untyped, each set binds it once to its own troll's clip, the cave troll keeps
the human eye height).

## How to verify in game

A Custom Battle with both trolls against infantry: the log's `[TrollBruteForce]` lines give the progress at which
the ring fired, the hits and knockdowns, and the body size the distances used.

## Known limitations

- The tuning is first guesses on the cave troll; the hill troll's reach follows its eye height and is unverified
  until the smoke reads its ring.
- Only enemy humans are hit; mounts are skipped.

## Changelog

- 2026-09-25: extended to the hill troll (`ActionSetsByMonster`, `IsBruteForceTroll`); distances scale with body
  size (eye height); the log reports the scale the distances use.
- 2026-09-24: the prototype on the cave troll (#649).
