# Lost Footing

## Overview

Lost Footing is a census of battle agents that are stranded: standing where the navigation mesh has no face, or
stuck in water without moving, and (phase 2, 2026-11-10) a rescue that moves such an agent back to where it can walk.
A mission behavior reads a share of the agent list every frame, and while at least one agent is stranded it writes a
few `[LostFooting]` lines to the log every ten seconds, with one start line and one summary per battle. The census
changes nothing. The rescue, its own toggle, moves a soldier who stays stuck: first a short step at his own height,
then, if he is still stuck, to his place in his formation while the player is far away. Each try writes one line.

## Why This Exists

- **Vanilla behavior:** a formation position is handed to an agent only while its movement mode is Land
  (`Agent.GetBaseFormationFrame`, `Agent.cs:2089`). In water the frame is Invalid (`Agent.cs:2118-2119`) and
  `TrySetFormationFrame` disables it (`Agent.cs:3928-3933`), so a soldier that wades into a ford can stand there with
  nowhere to go. That is the suspected cause of "stuck wading" reports.
- **TAOM requirement:** know how often, where and in what state agents are stranded before building anything that
  moves them. The reports name a few scenes (a marsh, a ford, a river crossing) but no measurement exists.
- **Without this feature:** a rescue would be written blind, and could teleport agents that are fine.

## Architecture

### Design Challenge

Every read is a native call and a big battle has hundreds of agents, so the census cannot probe everyone every
tick. It also must not count agents that are only passing through a state (crossing a river, a single missed face
lookup), and the engine's own answers have traps: a face id of 0 collides with face group 0, and `GetTargetPosition`
is the movement-lock target, not where the AI is heading.

### Solution Approach

`LostFootingMissionLogic` (a `MissionLogic`) is added to every mission by the module list. From `OnMissionTick` it
calls `LostFootingService.Tick`, catches anything that escapes (the census then stops for that mission), and asks for
the summary at mission end and on removal. The service owns the one validated clock: a tick with a non-finite or
non-positive delta is ignored before anything else. It reads the gate once a second. Each tick it reads
`ceil(min(AgentsPerTick, agents * dt / SweepSeconds))` agents from where the last tick stopped, so a whole pass over the
list takes about `SweepSeconds` (1 s) at typical frame rates. At least one agent is read per tick, so with fewer agents than frames a pass takes one frame per agent, and above `AgentsPerTick` times the frame rate (64 x fps) the cap makes a pass longer than 1 s. No tick reads more than `AgentsPerTick` (64). It folds each
probe into the per-mission state through the pure `LostFootingRules`, asks the adapter for a second look at each newly
off-mesh agent, and writes the census. When the census pauses (the setting is switched off, or the gate closes) the
state is dropped, so paused time is never counted as stranded time. No Harmony patch, no event subscription, no saved
data, no `Agent.Index` anywhere: state is keyed on the `Agent` object by reference.

**The gate** is `FellFoesMissionGate.IsEligible` unchanged, passed in by the module: no stand-down for a live co-op
session or a co-op mod TAOM cannot probe (`ICoopSessionProvider.IsSessionActive || ShouldDeferToHost`), the battle
allowlist `MountDespawnMissionGate.IsEligible` (field battle, siege or sally-out in Battle mode with real combat, so
nothing during deployment, and no arena, town or hideout), and `Mission.MissionEnded` false. A change to that gate
moves the census too.

### Component Diagram

```
TaomSettings (MCM toggle) -- LostFootingSettingsProvider
                                   |
LostFootingMissionLogic --> LostFootingService --> LostFootingRules (pure) + LostFootingMissionState (per mission)
  (OnMissionTick)            (clock, gate, log)  |                CensusLine (text)
                       ILostFootingAdapter --> LostFootingAdapter (reads only) --> Agent, Mission, Scene, WorldPosition
```

## What the census logs and how to read it

All lines carry the `[LostFooting]` tag in the TAOM log. Times `t=` and `time=` count mission seconds from the mission's first
tick (the clock advances before the gate and toggle checks), so they include deployment and any paused time.

**Start, once per eligible battle:** `start scene=<id> type=<field|siege|sally-out> water=<global level> agents=<n>`.
It appears only after deployment, because the gate needs Battle mode.

**Census, every 10 s, only while something is stranded.** One line per class that has anyone in it, then up to five
sample lines for that class:

| Class | An agent is in it when |
|---|---|
| `offmesh` | its own world position has no navigation mesh for at least `OffMeshConfirmSeconds`. `confirmed` counts those whose fresh position also has no mesh and is not on a dynamic mesh (a moving platform) |
| `waterstill` | its movement mode is not Land (the engine's own test for withholding the formation frame), it is slower than `StillSpeed` for `WaterStillSeconds`, and its goal is at least `MinDestinationMeters` away |
| `landbelowwater` | it is in Land mode, slower than `StillSpeed` for `WaterStillSeconds`, and stands below the scene's global water level |

The goal is the distance to the agent's formation order position while the engine gives one, otherwise the distance
to its AI move destination. An agent can be in more than one class. Each class line counts the agents in that class
and splits the count by kind: `soldier` (AI human in a formation), `router` (`IsRunningAway`), `mount` (a mount with
nobody on it), `creature` (carries a behavior tree that is not a race-ability tree; Race Abilities attaches one to
every profiled soldier, so those are soldiers), `object` (using a game object), `other` (for example a human outside
every formation). The most specific kind wins, in that order: object, router, creature, mount, soldier. `tracked` is
how many agents the census currently remembers.

A sample line gives `x y z` (scene metres), `depth` (water level at the spot minus its height), `secs` stranded,
`toFormation` (distance to the formation's order position for that agent) and `toDest` (distance to the AI move
destination, `Agent.GetAIMoveDestination`), and `unreachable` (the engine's `UnreachableViaNavMesh` flag). `n/a`
means the engine gave no usable number; `toFormation` and `toDest` are also `n/a` for a moving agent, because they are
read only for an agent slower than 2 m/s or off the mesh. `secs` counts from the first sighting of the condition,
including the confirm window, and has about one second of resolution (an agent is read about once a pass). An `offmesh`
sample adds `confirmed=yes|no`, `dynamic=yes|no`, and, for a confirmed agent, the engine snap candidate: `snap=<metres>`
and `snapOnMesh=yes|no`, or `snap=none`. The snap pair is the one `Agent.GetBaseFormationFrame` uses
(`Agent.cs:2121-2129`) to repair the formation position, not the agent: the nearest navigation mesh, then
`Scene.FindClosestExitPositionForPositionOnABoundaryFace`. `snapOnMesh` is a lookup at the exit point seeded with the
agent's own height, which is what a rescue seeded with his height would find.

**Summary, at the end of the battle:** `summary scene=<id> time=<s> episodes=<n> recovered=<n> removed=<n> atEnd=<n>
longest=<s> peak offmesh=<n> confirmed=<n> waterstill=<n> landbelowwater=<n> soldier=<n> router=<n> mount=<n>
creature=<n> object=<n> other=<n> cost probes=<n> totalMs=<x> maxTickMs=<y>`, then up to five `hotspot` lines: 5 m grid
squares ranked by agent-seconds stranded, with the square's x and y bounds in scene metres. An episode is one
unbroken stretch of one agent being stranded in any class. `recovered` episodes ended because the agent moved or got
back on the mesh, `removed` because it left the census (it died or was pruned unseen), `atEnd` because the battle ended
(or the census was paused) while it was still stranded. The kind counts are episodes by kind. `cost` is the time of
the census and the rescue together: agents probed, total milliseconds reading, folding and moving them, and the
slowest single tick. With the census off and the rescue on, the scan still runs and the start, summary and hotspot
lines are still written; only the census and sample lines stop.

Reading it: a high `waterstill` peak with hotspots on a river points at the formation-frame cause; `offmesh` with
`confirmed` close to its count and `snap=none` points at geometry the engine cannot repair; `offmesh` with a snap
candidate that is on the mesh says a rescue could use the engine's own pair. `creature` should be 0 in a battle with
no creatures, and `toDest` should differ from the square root of x squared plus y squared (the old target-position
read gave exactly that for an ordinary soldier).

## Engine facts (v1.5.5)

- The movement modes are `Land = 1`, `WaterSurface = 2`, `WaterDiving = 3` (`AgentMovementMode`); `IsOnLand` is at
  `Agent.cs:3348-3350`, `MovementMode` at `Agent.cs:708`. Not Land is the whole water test; `IsInWater` implies it.
- The off-mesh cheap test is `agent.GetWorldPosition().GetNavMesh() == UIntPtr.Zero`. The native `get_world_position`
  returns a position marked valid whose navigation mesh is filled only when the 2D in-face test passes, so
  `GetNavMesh` never re-validates (`WorldPosition.cs:124-128`). The face id (`GetCurrentNavigationFaceId`) is not used:
  when the in-face test fails the agent's current face falls back to the nearest face, so it reads non-zero, and 0
  collides with face group 0 anyway. The test is 2D, so an agent on a prop above the mesh counts as on it.
- `Mission.GetWaterLevelAtPosition(Vec2, bool)` is at `Mission.cs:2449`; `Scene.GetWaterLevel()` at `Scene.cs:265` is
  read once per mission.
- `Agent.GetAIMoveDestination()` (`Agent.cs:2066`) is where the AI is heading. `Agent.GetTargetPosition()`
  (`Agent.cs:2653`) is the movement-lock target and reads (0,0) for an ordinary soldier, so it is not used.
- `Formation.GetOrderPositionOfUnit` prints a warning for a charge order with no valid position, so the adapter reads
  it only for an agent slower than 2 m/s or off the mesh. It can also refresh the movement order's own position caches
  and set the active behaviour's navmeshless-target penalty (`MovementOrder.cs` around 469, 574-607 and 628). The engine
  makes the same call for each unit itself, so TAOM's own calls are very likely behaviour-neutral; the claim is that
  TAOM's own calls change nothing, not that nothing changes.

## Threading

Every read runs in `OnMissionTick`, the main thread, which never overlaps the agent tick of the same frame. The
adapter has read methods only and no `MissionThreadGuard` tripwire is needed. No engine event is subscribed. State is
per mission and released with the behavior at mission end.

## Configuration

MCM group `Battle Tactics/Lost Footing` (GroupOrder 64, no restart): **Lost Footing Census**, default on, and
**Lost Footing Rescue** (phase 2), default on (Mike, 2026-11-10: every battle). The census toggle is classified
Instrumentation in `CoopSettingsRelevance` (excluded from the settings fingerprint); the rescue moves agents, so it
stays included by default, as Fell Foes' toggles do, although it stands down in co-op with the census. The scan numbers
are **compiled defaults in `LostFootingTunables`, not settings**: nobody needs to tune a measurement, and a stale saved
value cannot reach them.

| Number | Default |
|---|---|
| `SweepSeconds` (one pass over the agent list) | 1 |
| `AgentsPerTick` (most agents in one tick) | 64 |
| `CensusIntervalSeconds` | 10 |
| `OffMeshConfirmSeconds` | 2 |
| `WaterStillSeconds` | 2 |
| `StillSpeed` (m/s) | 0.35 |
| `MinDestinationMeters` | 3 |
| `SamplesPerClass` | 5 |
| `HotspotCellMeters` | 5 |
| `RescueNearAfterSeconds` (stranded before the near move) | 10 |
| `RescueFarAfterSeconds` (stranded before the formation move) | 30 |
| `RescueMaxSpeed` (m/s; a faster agent is still walking) | 0.5 |
| `RescueNearMeters` (the near search radius, the issue's 6 m) | 6 |
| `RescueMaxStepMeters` (height between agent and near spot) | 1.5 |
| `RescueWaterStepMeters` (the same for a soldier in water) | 2.5 |
| `RescuePlayerMeters` (the player's distance from both ends of a formation move) | 25 |
| `RescueRetrySeconds` (wait before a formation move refused for `player-near` is tried again) | 10 |

## Exclusions

- The player and any agent a person controls are never counted.
- A mount with a rider is skipped (the rider is counted); a riderless mount is its own kind.
- A router is counted as its own kind, not skipped (the rescue skips routers; the census still counts them).
- Off during deployment (Battle mode is required), in co-op, outside field battles, sieges and sally-outs, and after
  `Mission.MissionEnded`.
- An entry not read for more than two complete passes over the agent list is dropped, and its open episode closed as
  `removed`. Switching both toggles off, or a closed gate, drops every entry (open episodes close as `atEnd`), so a
  later resume starts fresh and the paused time is not bridged.

## Dependencies

- `ICoopSessionProvider`, `FellFoesMissionGate`, `MountDespawnMissionGate`: the gate.
- `BehaviorTreeAgentComponent`, `RaceAbilityBehaviorTree`: the creature test.
- `AgentSlotIdentity`: the live-occupant check before the second look at an off-mesh agent.
- `IStampClock` (`StopwatchStampClock`): times the slice read for the summary's cost line.

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/LostFooting/LostFootingModule.cs` | Module: one mission behavior, settings provider and both adapters registered, the Fell Foes gate wired in |
| `Main/Features/LostFooting/Hooks/LostFootingMissionLogic.cs` | Entry point: tick, catch, summary at the end |
| `Main/Features/LostFooting/LostFootingService.cs` | Per-mission clock, gate read, per-frame slice, log lines, cost |
| `Main/Features/LostFooting/LostFootingRules.cs` | Pure: kinds, the three classes, episodes, samples, hotspots, cursor, pruning |
| `Main/Features/LostFooting/CensusLine.cs` | The log text |
| `Main/Features/LostFooting/Domain/` | `AgentProbe`, `SceneFacts`, `OffMeshDetail`, `LostFootingMissionState`, `LostFootingTunables` |
| `Main/Adapters/ILostFootingAdapter.cs`, `LostFootingAdapter.cs` | Engine reads (ADR-007) |
| `Main/Adapters/ILostFootingRescueAdapter.cs`, `LostFootingRescueAdapter.cs` | Phase 2 engine writes: the near search, the formation spot, the teleport (ADR-007) |
| `Main/Features/LostFooting/Domain/RescueTypes.cs` | Phase 2: `RescueMove`, `RescueCause`, `RescuePlan`, `RescueResult` |
| `Main/Features/LostFooting/ILostFootingSettingsProvider.cs`, `LostFootingSettingsProvider.cs` | MCM toggles |

## Tests

- `TAOM.Tests/Features/LostFooting/LostFootingRulesTests.cs`: kinds, off-mesh suspicion, the water and land-below-water
  gates (NaN and infinite inputs), the goal choice, episodes and how they end, tally and peaks, samples, hotspots, the
  pass cursor, pruning, pause.
- `LostFootingServiceTests.cs`: the validated clock, gate cadence, toggle, per-frame slicing, census cadence,
  confirmation once per episode, summary once, cost, pause and resume.
- `LostFootingMissionLogicTests.cs` (tagged RequiresGame): the error latch and the one summary.
- `LostFootingTunablesTests.cs`, `LostFootingCensusLineTests.cs`, `LostFootingSettingsProviderTests.cs`,
  `LostFootingWiringTests.cs` (module list, gate closure, teardown, per-mission state, adapter source pins, coop
  classification, no hard-coded scene id or coordinate).
- `LostFootingRescueRulesTests.cs` (phase 2): which agent gets which move and when, the dry-land rule, the resets.
  `LostFootingServiceTests.cs` also covers the rescue: off, each tier once, refusals, the player-near retry, the water
  step, census off, freed and not freed.
- Both adapters are game-tested only (ADR-008): the source pins in `LostFootingWiringTests.cs` hold their shape (the
  rescue moves only by teleport and keeps every refusal guard), and the runs measure them.
- **Localization:** both MCM hints are plain English, as most `TaomSettings` hints are; no `{=KEY}` is owed.

## Owed: the census runs

In game, with the Lost Footing census on (the default), send the whole `Logs/taom_debug_<timestamp>.log` for each run
and say which battle was which. Use the same Battle Size for comparable runs and note it.

1. **Custom Battle, player side Gondor, "[Mordor] Dead Marshes"**, then **"[Rohan] Fords of Isen"** (the picker
   names). These are the scenes `taom_mordor_battle_dead_marshes_forceatmo` and `taom_rohan_battle_fords_of_isen_forceatmo`.
2. **One campaign battle on a River face of campaign cell 85, 93 or 97** (the Anduin crossing scenes
   `battle_terrain_f`, `battle_terrain_s`, `battle_terrain_011`). Run `taom.print_battle_scene` there first and confirm
   the scene from the `[BattleLoad] ... sceneId=` line in the log.
3. **One siege**, any TAOM siege the player can reach.
4. **Inert checks:** a tournament, a hideout and a town write no `[LostFooting]` line, and in a battle the start line
   appears only after deployment ends.
5. **Cost check:** a big battle with the census on and then off, with Enable Tick Profiler switched on and the game restarted first (the setting needs a restart), reading the
   `[TickProfile] top=` and `[Hitch]` lines, plus the summary's `cost` line.
6. **Sanity checks:** a battle with no creatures reads `creature=0`; `toDest` differs from the square root of x squared
   plus y squared.

## UNVERIFIED (the census measures some of these)

- Whether native updates an agent's current face before or after `OnMissionTick`. The two-sighting rule absorbs a lag.
- Whether `FindClosestExitPositionForPositionOnABoundaryFace` returns a point inside the face or on its edge.
- The per-frame cost: at most 64 agents a tick, a few native reads each. Not measured; the summary's cost line is the
  measurement.

## Phase 2: the rescue

**What the census showed** (v1.5.7 logs, 2026-11-10, Long Defeat runs on Helm's Deep and six story mission scenes):
most episodes end by themselves (Helm's Deep, one hour: 3,095 of 3,121 recovered), but some last the whole battle
(the longest 3,571 s), nearly all of them soldiers on a wall. Over 4,279 samples the engine's exit point was under 1 m
away for 4,189, yet a lookup there at the soldier's height found no mesh for 2,852: the mesh is on the wall walk or on
the ground below. So a plain snap would drop a soldier off his wall, and the rescue tests the height of every spot.

**The rule** (`LostFootingRules.PlanRescue`, pure):

| Stranded as | Near move (after 10 s) | Formation move (after 30 s) |
|---|---|---|
| Off the mesh, confirmed, not on a dynamic mesh | soldier, riderless mount | soldier |
| Still in water, or on land below the water level | soldier, riderless mount (to dry land) | soldier (to a dry spot) |

An off-mesh agent who is also in water gets the dry-land test too. A soldier the engine flags unreachable is not a
cause: the engine gives a flagged unit no order spot (`Formation.cs`:1382-1385), so a formation move could never place
him (review, 2026-11-10).

Each move is tried once per episode; an agent free of every condition gets both tries back. One exception: a
formation move refused for `player-near` is tried again every 10 s, since the player can walk away; only the first
such refusal is logged and counted. The first Helm's Deep run (2026-11-10) left 10 soldiers stuck to the end beside
the player, each refused once for `player-near` and never tried again. An agent moving faster than
0.5 m/s is still walking and is left alone. Never moved: the player and any agent a person controls, routers, agents
holding a machine's or a ladder's standing point (its crew, raisers and fork pusher), agents on a scripted hold (a
ladder queue, a Long Defeat post, a Fords hold: `Agent.GetScriptedFlags`, since such an agent walks back to his
point), TAOM creatures with their own tree, ridden mounts, and agents on a dynamic navigation mesh (a siege tower deck,
a ship).

**The moves** (`LostFootingRescueAdapter`, the write side; the census adapter stays read-only):

- **Near:** rings of 0.5 to 6 m in 12 directions around the agent; the first spot where a lookup at the agent's height
  finds the mesh within 1.5 m of it (2.5 m in water), inside the mission boundary, not on a dynamic mesh and, in
  water, dry.
- **Formation:** the agent's `Formation.GetOrderPositionOfUnit` spot, spread by a random offset of up to the square root
  of the formation's unplaced units in metres (at least 1 m, six tries, then the spot itself), as vanilla's deployment
  spread does, since a unit without its own slot gets the formation's single point: in the first run 29 of 31 moves
  landed on one point. On the mesh, dry when needed, and only while the player is at least 25 m from both ends of the
  move.
- `Agent.TeleportToPosition` places a mount, its rider and the agent on the spot together (v1.5.7 `Agent.cs`:4287-4302).

**The lines:** `[LostFooting] rescue t=<s>s move=near|formation cause=offmesh|water kind=<kind>
result=moved meters=<m> from=(x,y,z) to=(x,y,z)`, or `result=refused reason=<why>` (`no-standing-spot`, `player-near`,
`no-formation`, `no-formation-spot`, `spot-off-mesh`, `spot-not-dry`, `gone`, `player`, `router`, `using-object`,
`ridden`, `scripted`, `dynamic-mesh`, `no-mission`). The summary ends `rescued=<n> freed=<n> refused=<n>`. `rescued`
counts moves, so one agent can count twice. `freed` counts moved agents whose episode then ended by recovery: seen on
the mesh again, or out of the water. It does not show that the agent walked afterwards. The in-game checks are in
`tools/ingame_checks/894.toml`.

**UNVERIFIED until a run:** that the AI re-paths at once after a move (native `IMBAgent.SetPosition`); which face
the mesh lookup picks where two levels overlap (the height test refuses the wrong one); that a moved soldier in a
siege rejoins the fight; whether a rider's `Position` is ground or saddle height. The 25 m test measures from the
player's agent, not from the camera: with Seat of Seeing's free view, or after the player falls (no active main agent,
so no test), a formation move can happen in view.

## How to verify in game

The `[[check]]` rows of `tools/ingame_checks/894.toml` judge the log; the `[[human]]` rows need a person.

1. Run a siege with walls, for example the Long Defeat on Helm's Deep: `python tools/run_long_defeat.py --fortress helms
   --waves 2`. Soldiers get stuck on the wall walk there in every run so far.
2. In the log, look for `[LostFooting] rescue ... result=moved` lines and the summary's `rescued=`, `freed=` and
   `refused=` counts. Run 1 (2026-11-10): `rescued=200 freed=167 refused=25`.
3. Watch one stuck soldier for 10 s: he steps a short way at his own height and walks. Nobody falls off a wall or lands
   in a wall.
4. Stand 25 m or more away from a soldier still stuck after 30 s: he appears in his formation. Stand closer: he does
   not move.
5. With Seat of Seeing's free view, fly over a stuck soldier: the 25 m test uses the hero's body, not the camera, so a
   formation move can happen in view (known, see UNVERIFIED above).
6. Switch **Lost Footing Rescue** off in MCM and repeat step 1: no `rescue` line, nobody moved.

## Changelog

- 2026-11-10: phase 2, the rescue, added: the near move, the formation move (spread), the player-near retry, the freed
  count. The unreachable case was dropped in review (the engine gives a flagged unit no order spot).
- 2026-10-20: phase 1, the census (read-only), added. Review fixes: race-ability soldiers are no longer creatures, the
  goal is the formation spot or the AI move destination, reads are spread per frame, the summary splits episodes by how
  they ended and reports its cost.

## GitHub Issue

- **Issue:** #894, Stuck agent rescue: off-navmesh soldiers and soldiers stranded in water
- **Status:** Open (phase 1 built; phase 2 built on `feat/lost-footing-rescue`, in-game proof owed)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reviews/rca-lost-footing-2026-10-20.md](../reviews/rca-lost-footing-2026-10-20.md)

<!-- backlinks-end -->
