# Troll Brute Force

## Overview

Both battle trolls (`cave_troll`, `hill_troll`) get one area attack, Brute Force (#649): when an enemy is close and
in front, the troll plays a two-handed smash, and at the moment the weapon lands up to five enemy humans inside a
ring ahead of it take a Blunt blow, are knocked back and, unless blocking with a shield, knocked down. Each smash
rolls how many it may hit, 1 to 5, and hits that many of the enemies nearest the impact; the rest of the ring is
untouched (Mike, 2026-09-26: one swing was clearing a whole infantry block). A per-troll behaviour tree drives it;
the rest of the troll's fighting is the engine's ordinary melee.

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
reaches `ImpactFraction`, calls `BruteForceRing.Deliver`. The ring gathers every enemy human the service gives a
blow, draws the smash's cap at the boundary with `MBRandom.RandomInt(RingMinTargets, RingMaxTargets + 1)`, a
uniform 1 to 5 (`MBFastRandom.Next`'s upper bound is exclusive in the v1.5.3 decompile), and applies
`CustomAttacksUtils.TakeDamage` only to the victims the service's `NearestRingVictims(distances, cap)` returns: the
cap's worth nearest the impact centre, nearest first, never one at a non-finite or negative distance. The log line
reports hits over the cap and how many the cap spared. Every decision (cooldown, engagement, impact centre, falloff,
shield handling, body size, who the cap picks) is in the pure `TrollBruteForceService` (floats in, no TaleWorlds
types, ADR-007; a NaN or infinity fails closed); only the roll is taken outside it.
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
second on the main thread, from the first tick after the mission builds a troll (`TrollPresence`; a mission
with no troll never scans), and writes the width to `TrollFormationSpacingStore`, a concurrent map keyed by reference
through `ReferenceIdentity` (`Formation.GetHashCode` reads its Team, which is null on a layout copy); the
`Formation.UnitDiameter` postfix reads it from any thread. A changed width calls `Formation.OnUnitAddedOrRemoved()`
and `Arrangement.OnFormationFrameChanged(updateCachedOrderedLocalPositions: true)`, which rebuilds the cached slot
positions. While the field is deploying (`Mission.IsTeleportingAgents`), the tracker also replays the tail of
vanilla's `Formation.OnMassUnitTransferEnd`, so the trolls, placed at human width before their width was known, jump
onto the wider slots; after deployment they walk there. A prefix and finalizer on the layout entry points
(`GetUnitPositionWithIndexAccordingToNewOrder`, `GetUnitSpawnFrameWithIndex`) name the real formation for the
length of the call, so the team-less copy formations of the order preview, the deployment placement and the spawn
frames take its width. PatchShield skips all five patched methods (`PatchShieldPolicy.ExcludedTargetMethods`),
because the shield finalizer took `__originalMethod` until plan 034, which added a `GetMethodFromHandle` to every
per-unit call (a stand-in finalizer of the new shape added about 3.5 ns per call in plan 034's Debug benchmark, 5.4 against 1.9 ns; the exclusion stands). MixedFormations spaces its
slots by the same width through `IFormationAdapter.UnitDiameter`. Registry:
[Patch92](../reference/harmony-patch-registry.md).

**Timing (plan 030, Codex review).** The tracker's 0.5 second stride starts at its first tick, no longer at the
mission's first tick. The first scan after the first troll is built (a reinforcement, a deployment spawn) comes on the
next tick, where the old clock would have run it up to 0.5 s later: sooner, never later. Every later scan keeps the new
phase, so a re-space that falls due after that can land up to about 0.5 s sooner or later than before. That covers a
formation that reaches the 10% troll share only once more trolls are built, and the formation of a troll built after the
first one. Example: the old clock scans at 0.0, 0.5, 1.0, 1.5 and 2.0 s. The first troll is built at 1.2 s, so the new
scans run at about 1.2, 1.7 and 2.2 s. The first troll is counted about 0.3 s sooner, but a troll that brings another
formation to the share at 1.75 s gets that formation re-spaced at about 2.2 s, not about 2.0 s. Either way it is a small
change to when real formations re-space and, during deployment, teleport their units, not only to when a line is logged.
The width formula and the positions it produces are unchanged, and a troll present on the mission's first tick sees the
old timing (the latch is set before the same tick's tracker call). Restoring the old phase would mean advancing the
deadline while the latch is closed; that was left out because it would only delay the first response by under half a
second. The clip trace shares the latch and the stride and only logs.

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
| `RingMinTargets`, `RingMaxTargets` | 1, 5 | Each smash draws a cap uniformly from these, both included, and hits only that many of the enemies nearest the impact |
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
| `Main/Features/TrollBruteForce/TrollBruteForceMissionBehavior.cs` | Attaches the trees; start-up drift guard; ticks the spacing tracker and the clip trace once the mission has built a troll (`TrollPresence`) |
| `Main/Features/TrollBruteForce/TrollClipTrace.cs`, `TrollClipLog.cs` | The temporary `[TrollClips]` trace for the animation tuning pass: the trace reads each troll's channel 0 and 1 actions every tick and the action set's bound clip when they change; the pure log formats one line per Monster and action. To be deleted, with `TrollClipLogTests`, once the tuning pass ends. Like the spacing tracker, it runs only once the mission has built a troll. |
| `Main/Features/TrollBruteForce/TrollFormationSpacingTracker.cs` | Counts trolls per formation, stores the width, rebuilds the slots, replays the mass-transfer tail during deployment |
| `Main/Features/TrollBruteForce/TrollFormationSpacingStore.cs` | The per-formation width (reference-keyed concurrent map) and the thread-static layout scope |
| `Main/Features/TrollBruteForce/TrollPresence.cs` | The per-mission latch: set when a Brute Force troll is built, it gates the spacing tracker and the clip trace |
| `Main/Features/TrollBruteForce/Hooks/Patch92_TrollFormationSpacing.cs` | `UnitDiameter` postfix and the simulation-copy scope |
| `Main/Features/TrollBruteForce/TrollBruteForceBehaviorTree.cs`, `BehaviorTreeElements/` | The tree, decorator and task |
| `Main/Features/TrollBruteForce/Hooks/BruteForceRing.cs` | Delivers the ring; draws the smash's target cap (`MBRandom.RandomInt`) and hits the victims `NearestRingVictims` picks |
| `Main/Features/TrollBruteForce/TrollBruteForceIoC.cs` | Singleton service registration |

## Tests

`TAOM.Tests/Features/TrollBruteForce/`: `TrollBruteForceServiceTests` (every decision, NaN and bad-scale gates,
body size, the formation width and its gates, and the cap's pick: `NearestRingVictims_*` for nearest first, fewer
victims than the cap, ties in scan order, none eligible, a cap below 1, and NaN, infinite or negative distances),
`TrollFormationSpacingStoreTests` (set, change and removal, a layout
copy borrowing the scoped formation's width, the scope nesting and staying per thread), `Patch92BindingTests` (the
four layout entry points resolve against the installed engine, and `Formation.Team` is still a public field),
`TrollBruteForceConfigTests` (`RingTargets_AtLeastOne_MinNoMoreThanMax_AndAtMostFive` among them), `TrollClipLogTests`
(one line per Monster and action, troll against vanilla clips, the melee-table family tag, a mission clear),
`TrollPresenceTests` (the latch: unset before any troll, set by either battle troll, kept after, cleared at mission
end, and its two log lines pinned literally), and `TrollBruteForceWiringTests` (`LiveInstall`: each Monster names its
set, the action is declared once and untyped, each set binds it once to its own troll's clip, the cave troll keeps
the human eye height). The cap's draw itself (`RandomInt` with the exclusive `+ 1`) is boundary code, checked in
game by the ring line below.

## How to verify in game

A Custom Battle with both trolls against infantry. Each smash logs `[TrollBruteForce] Brute Force by <troll> at
progress P: ring=<Hit>/<Cap> hit (rolled cap), <Spared> spared by the cap, <KnockedDown> knocked down, <Skipped>
skipped (body size X).` Pass: Hit <= Cap <= 5 on every line (Cap 0 means no ring was cast), P near
`ImpactFraction`, and X the troll's body size. `[TrollSpacing]` lines give each
troll formation's troll count and its unit width (`vanilla -> new m`, or `back to vanilla`); the trolls should
stand apart in the line, already on the deployment screen in a battle that opens on one.

The troll gate (`TrollPresence`) logs two INFO lines, at most one each per mission. When the first Brute Force
troll is built (the first tick's scan or a later `OnAgentBuild`):
`[TrollBruteForce] First Brute Force troll built ('hill_troll'): formation spacing and clip trace start ticking`,
naming the Monster that set it. At mission end, when no troll was built:
`[TrollBruteForce] No Brute Force troll built this mission: formation spacing and clip trace never ran`, so a
battle with no `[TrollSpacing]` or `[TrollClips]` line says why. `TrollPresenceTests` pins both.

`[TrollClips] <monster>: <action> -> <clip or -> (<troll clip|vanilla clip|no clip>[, melee-table family])` is
logged once per Monster and action a mission (the 15:40 build printed `, melee table`). It names the clip the
troll's action set binds to the action the troll entered (`MBActionSet.GetActionAnimationName` takes no agent), so
it proves the action was entered and which clip it is bound to, not that the clip's keyframes played. The
melee-table family tag marks the release and blocked codes, quick or not ([troll-race.md](troll-race.md) "The
swing CTD").

## Known limitations

- The tuning is first guesses on the cave troll. The hill troll's ring was read in the 2026-09-26 15:40 smoke
  (`taom_debug_2026-09-26_15-40-34.log`): 24 rings at body size 2.34, hitting up to 84 enemies (84, 78, 73, 70 at
  the top), before the cap existed. The cap is not yet seen in game: the next smoke should show every ring line
  within Hit <= Cap <= 5.
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

- 2026-09-26: each smash hits at most a drawn cap of 1 to 5 enemies, the nearest to the impact first
  (`RingMinTargets`, `RingMaxTargets`, `NearestRingVictims`; Mike: one swing was clearing a whole infantry block),
  and the ring line reports hits over the cap and how many it spared.
- 2026-09-26: the `[TrollClips]` trace, one line per troll Monster and action with the clip its set binds, for the
  animation tuning pass; temporary, to be deleted with its tests when that pass ends.
- 2026-09-26: formation spacing for both trolls (Patch92, measured shoulder widths); a temporary action trace used
  to find the swing crash was removed.
- 2026-09-25: extended to the hill troll (`ActionSetsByMonster`, `IsBruteForceTroll`); distances scale with body
  size (eye height); the log reports the scale the distances use.
- 2026-09-24: the prototype on the cave troll (#649).
