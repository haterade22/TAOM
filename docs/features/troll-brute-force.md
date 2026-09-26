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

## Formation spacing (Patch92, 2026-09-26)

The engine spaces every foot unit for a 0.76 m human (`Formation.UnitDiameter` is `BipedalRadius` times two,
whatever the Monster), so trolls stood inside each other. Once trolls are a tenth of a formation, the share at
which vanilla spaces a formation for horses, the formation is spaced for its widest troll: the Monster's measured
shoulder width (`ShoulderWidthByMonster`: cave troll 0.75, hill troll 2.43, from the LOD0 meshes) times
`AgentScale`, about 1.43 m and 2.7 m, capped at 4 m. `TrollFormationSpacingTracker` counts the formations twice a
second on the main thread and writes the width to `TrollFormationSpacingStore`, a concurrent map keyed by reference
through `ReferenceIdentity` (`Formation.GetHashCode` reads its Team, which is null on a layout copy); the
`Formation.UnitDiameter` postfix reads it from any thread. A changed width calls `Formation.OnUnitAddedOrRemoved()`
and `Arrangement.OnFormationFrameChanged(updateCachedOrderedLocalPositions: true)`, which rebuilds the cached slot
positions. While the field is deploying (`Mission.IsTeleportingAgents`), the tracker also replays the tail of
vanilla's `Formation.OnMassUnitTransferEnd`, so the trolls, placed at human width before their width was known, jump
onto the wider slots; after deployment they walk there. A prefix and finalizer on the layout entry points
(`GetUnitPositionWithIndexAccordingToNewOrder`, `GetUnitSpawnFrameWithIndex`) name the real formation for the
length of the call, so the team-less copy formations of the order preview, the deployment placement and the spawn
frames take its width. PatchShield skips all five patched methods (`PatchShieldPolicy.ExcludedTargetMethods`),
because a shield finalizer would add a `GetMethodFromHandle` to every per-unit call. MixedFormations spaces its
slots by the same width through `IFormationAdapter.UnitDiameter`. Registry:
[Patch92](../reference/harmony-patch-registry.md).

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
| `FormationShare` | 0.1 | Trolls at this share of a formation space the whole formation for its widest troll |
| `ShoulderWidthByMonster` | 0.75, 2.43 | Cave and hill troll shoulder width in metres at `AgentScale` 1 (LOD0 meshes); times `AgentScale` gives the unit width |
| `MaxFormationUnitWidth` | 4 | The widest unit width a formation is ever spaced for, in metres |

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
| `Main/Features/TrollBruteForce/TrollBruteForceMissionBehavior.cs` | Attaches the trees; start-up drift guard; ticks the spacing tracker |
| `Main/Features/TrollBruteForce/TrollFormationSpacingTracker.cs` | Counts trolls per formation, stores the width, rebuilds the slots, replays the mass-transfer tail during deployment |
| `Main/Features/TrollBruteForce/TrollFormationSpacingStore.cs` | The per-formation width (reference-keyed concurrent map) and the thread-static layout scope |
| `Main/Features/TrollBruteForce/Hooks/Patch92_TrollFormationSpacing.cs` | `UnitDiameter` postfix and the simulation-copy scope |
| `Main/Features/TrollBruteForce/TrollBruteForceBehaviorTree.cs`, `BehaviorTreeElements/` | The tree, decorator and task |
| `Main/Features/TrollBruteForce/Hooks/BruteForceRing.cs` | Delivers the ring |
| `Main/Features/TrollBruteForce/TrollBruteForceIoC.cs` | Singleton service registration |

## Tests

`TAOM.Tests/Features/TrollBruteForce/`: `TrollBruteForceServiceTests` (every decision, NaN and bad-scale gates,
body size, the formation width and its gates), `TrollFormationSpacingStoreTests` (set, change and removal, a layout
copy borrowing the scoped formation's width, the scope nesting and staying per thread), `Patch92BindingTests` (the
four layout entry points resolve against the installed engine, and `Formation.Team` is still a public field),
`TrollBruteForceConfigTests`, and `TrollBruteForceWiringTests` (`LiveInstall`: each Monster names its
set, the action is declared once and untyped, each set binds it once to its own troll's clip, the cave troll keeps
the human eye height).

## How to verify in game

A Custom Battle with both trolls against infantry: the log's `[TrollBruteForce]` lines give the progress at which
the ring fired, the hits and knockdowns, and the body size the distances used. `[TrollSpacing]` lines give each
troll formation's troll count and its unit width (`vanilla -> new m`, or `back to vanilla`); the trolls should
stand apart in the line, already on the deployment screen in a battle that opens on one.

## Known limitations

- The tuning is first guesses on the cave troll; the hill troll's reach follows its eye height and is unverified
  until the smoke reads its ring.
- Only enemy humans are hit; mounts are skipped.
- Below a tenth trolls nothing widens: a formation of 11 with one troll (9%) keeps human spacing around it.
- Past that share every unit in the formation is spaced at troll width, orcs beside trolls included; no separate
  troll formation is built.
- The custom-width order preview measures its copy's occupation width through
  `Formation.GetLastSimulatedFormationsOccupationWidthIfLesserThanActualWidth`, which
  `OrderController.SimulateNewCustomWidthOrder` calls outside the Patch92 scope, so that one read is at human
  width and the preview can disagree with the width the order produces (unverified in game).
- The deployment plan sizes each formation's spawn area with the static `Formation.GetDefaultUnitDiameter`
  (`DefaultDeploymentPlan.GetFormationSpawnWidthAndDepth`), which Patch92 does not patch, so a troll formation's
  planned spawn area is sized for humans.

## Changelog

- 2026-09-26: formation spacing for both trolls (Patch92, measured shoulder widths); a temporary action trace used
  to find the swing crash was removed.
- 2026-09-25: extended to the hill troll (`ActionSetsByMonster`, `IsBruteForceTroll`); distances scale with body
  size (eye height); the log reports the scale the distances use.
- 2026-09-24: the prototype on the cave troll (#649).
