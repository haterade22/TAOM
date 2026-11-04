# Fell Foes

## Overview

Fell Foes is three small tweaks to how a battle ends, run by one mission behavior that ticks once a second.
A routing enemy soldier who is hit with a few of your men beside him turns and fights, and a few panicked men
near him turn with him. Idle enemy archers fix on you and shoot, a few at a time. When a large field battle is
down to its last few enemy soldiers, those stragglers are sent off the field so the vanilla rout victory fires
and they can be taken prisoner. Each tweak has its own MCM switch (`Battle Tactics/Fell Foes`) and fails on its
own without taking the others down.

## Why This Exists

- **Vanilla behavior:** a routed soldier runs until the engine fades him out or the battle ends. Enemy archers
  with nothing to shoot at stand idle. A rout victory needs every active enemy agent to be running away for the
  reinforcement interval plus three seconds, so one soldier who never routs (or is stuck) holds a won battle open.
- **TAOM requirement:** routs that feel like routs (a cornered man fights), a ranged threat that stays on the
  player, and a battle that ends once the result is certain.
- **Without this feature:** big field battles drag on after the outcome is decided, and the last survivors are
  killed one by one instead of being taken.

## Architecture

### Design Challenge

Every engine write here is a native call that must happen on the main thread, on an agent that is still the
occupant of its engine slot, without disturbing the vanilla panic and rout machinery. Two behaviours are not
provable from decompiled source and are owed an in-game spike (below).

### Solution Approach

A mission behavior, `FellFoesMissionLogic` (a `MissionLogic`), is added to every mission by the module list.
From `OnMissionTick` it calls `FellFoesService.Tick`, catches anything that escapes, and releases at mission end
and on removal. It keeps no clock and no co-op provider. The service owns the gate clock: it throttles to one
pass a second (after a validated delta), asks the gate whether the mission is still eligible, and only then
takes a scan and runs three passes, each in its own try and behind its own toggle. The module factory builds the
gate check as a `Func<bool>` over the mission (`FellFoesMissionGate.IsEligible(mission, coop)`), so a fresh
behavior and a fresh service exist per mission. The passes are pure decisions over a snapshot
(`FellFoesRules`); the adapter does the reads and writes. No Harmony patch, no event subscription, no saved
data.

**The gate** (`FellFoesMissionGate`) is true when all of these hold: the session is not a co-op session and no
co-op host owns the battle; the mission passes the battle allowlist shared with the corpse cleanup (field
battle, siege or sally-out in Battle mode with real combat); and `Mission.MissionEnded` is false. The co-op test
is `ICoopSessionProvider.IsSessionActive || ShouldDeferToHost`. `ShouldDeferToHost` is also true for a co-op
mod TAOM cannot probe, so a solo player who merely has a co-op mod enabled is stood down too. That is Mike's
decision (2026-10-19), kept for safety. It closes at `MissionEnded` because a write after the result is decided
could turn a capture into a rout. A closed gate scans nothing and frees every archer hold.

### Component Diagram

```
TaomSettings (MCM) -- FellFoesSettingsProvider -- FellFoesTunables (validated)
                                   |
FellFoesMissionLogic --> FellFoesService --> FellFoesRules (pure) + FellFoesMissionState (per mission)
  (OnMissionTick)         (clock, gate)  |
                       IFellFoesAdapter --> FellFoesAdapter --> Agent, Mission, CommonAIComponent
```

## The three tweaks

**Cornered routers.** A soldier is cornered when all of these hold on one pass:

- He is `IsPanicked` and retreating (an order retreat is never panicked, so it is never touched, and cultures
  that never rout, where `CanPanicDueToMorale` is false, never become panicked).
- His health dropped since the last pass, which is a hit. The service remembers health only for panicked
  enemies; the first sighting records it and is never a hit, and a handle that leaves the scan or stops being
  panicked is forgotten.
- One to three (default `CorneredMaxFoes`, slider 1 to 10) hostile soldiers are within 5 m.
- His health is at or above 30 percent.

He then gets `CommonAIComponent.StopRetreating()` and fights. **The rally:** when a router is newly cornered,
every other panicked router within the same 5 m of him, who also passes the health floor, stops too, nearest
first, at most `CorneredRallyCap` of them per cornered router (default 4, range 0 to 10, validated, no MCM
slider; 0 turns the rally off). The cap keeps a rout from being undone wholesale. Rallied men join the cornered
set and follow the same rule as the first man. The set is rebuilt from the scan each pass, so a handle that left
the scan drops out by not being carried over.

A turned soldier stays turned while a foe is within the hold radius of him, twice the cornered radius clamped to
15 m (10 m by default), because a rallied man can stand up to 5 m from the hit router and so up to 10 m from the
foes. Admission of a fresh hit still uses the cornered radius and 1 to `CorneredMaxFoes`; retention uses the hold
radius and at least one foe. With no foe in the hold radius his morale is set to 0 and the engine panics him again by
its own rules. A man who panics again on his own is dropped from the set (not set to morale 0) and judged afresh.

**Contested only.** A router is cornered or rallied, and a turned man keeps fighting, only while somebody on the
enemy side still stands on his own. Standing means a human enemy who is not fleeing, not panicked and not in the
cornered set, or a non-human enemy (a creature) who is not running away. Once the whole side has broken, the
turned men run again (unsteadied) and nobody new is stopped, so vanilla's rout victory fires on its own
schedule.

**Real morale.** `StopRetreating` lifts morale only to 0.02 (`CommonAIComponent.cs:162-171`), which the next
hit would break again (the panic trigger is morale under 0.01, `CommonAIComponent.cs:84`). The adapter then sets
`ai.Morale = Math.Max(ai.Morale, ai.RecoveryMorale)`, the level a steady soldier recovers to (half the initial
morale, `CommonAIComponent.cs:74`; the engine would otherwise ramp there at 0.4 a second,
`CommonAIComponent.cs:95-97`). A router who is already fading out (`Agent.IsFadingOut()`) is left alone: he is
leaving the field for good and turning him would only cut the fade short.

**No release on switch-off.** The turned set is deliberately never un-turned when the toggle goes off, when the
pass fails, when the gate closes or at mission end. An unsteadied man at mission end would turn a capture into a
rout, and under co-op no write is allowed at all. Vanilla also settles the end by itself: once the mission has
ended, a panicked agent on the winning side is told to stop retreating (`CommonAIComponent.cs:109-116`). The
remaining risk is a rallied man who stands idle because `Agent.OnFleeing` already cleared his formation; that is
owed to the spike.

**Archers target the player.** An enemy archer that is AI controlled, carries a bow or crossbow with ammunition
(`Agent.IsRangedCached`; a thrown weapon does not count), has no immediate enemy, is not retreating, has morale
above 50, is not panicked or fleeing, and is within 35 m (20 m in a siege or sally-out; the rules pick the range
from `IsFieldBattle`, and the scan reads at the field range when archers are on) rolls the archer chance
(default 0.25) each second. A winner gets `SetAutomaticTargetSelection(false)` and `SetTargetAgent(player)` for
8 seconds, then automatic selection back. At most 8 hold the player at once and at most 3 start per second,
nearest first. A hold also ends when the archer is gone, shaken, running, or his team starts a horse stand, and
for every hold when the player dies, when the setting is switched off, when the gate closes, at mission end and
when the behavior is removed. If the archer pass itself throws, it is marked failed and the next pass releases
what it held. A team whose horse stand is running (`TeamTacticProbe.IsHorseStanding`, which reads the single
`TaomTacticBase.Standing` flag and is false for a failed tactic) is skipped: Culture Doctrine owns its archers
then.

**Stragglers leave the field.** In a field battle only, when the enemy side has no troops left to spawn
(`DefaultBattleMissionAgentSpawnLogic.NumberOfRemaining*Troops` is 0; unknown means off), no enemy hero is on
the field, no creature on the enemy team is still standing, the enemy peaked at 40 or more soldiers, and 3 or
fewer (default; slider 1 to 20) remain, a 30 second pause (slider 5 to 300) starts. After it, one soldier a
second who is not already running is sent off: `Formation.OnBatchUnitRemovalStart`,
`CommonAIComponent.Retreat`, `Mission.OnAgentFleeing`, `OnBatchUnitRemovalEnd`, the same sequence
`MissionAgentPanicHandler` runs for a panicked agent, then his morale is held at 1 or above so he is taken and
not counted as a rout casualty. The pause restarts if the count rises, reserves appear, a hero is on the field
or a creature is standing. The stragglers wait for a creature (a non-human enemy team member that is not running
away) because a standing creature keeps the vanilla victory open and would be left behind. A man sent off leaves
the cornered set. A send that fails is rolled back so the soldier can be tried again. Soldiers of never-rout
cultures are still sent off (a withdrawal for pacing; they are taken like any other). A soldier is never sent
twice. Sieges and sally-outs are left alone for now.

## Engine facts it rests on (v1.5.5)

- Rout victory: `BattleEndLogic` wins when every active enemy agent `IsRunningAway` for the reinforcement
  interval plus 3 s. `Agent.IsRunningAway` is set by native after `Retreat`; `StopRetreating` clears it.
- `CommonAIComponent.StopRetreating()` clears `IsRetreating` and `IsPanicked`, lifts morale to at least 0.02,
  and calls `Agent.StopRetreating`. `Agent.OnFleeing` already cleared the agent's formation.
- Morale below 0.01 on an agent that is not retreating triggers `Panic()` on the next agent tick when
  `CanPanic()` allows, and `MissionAgentPanicHandler.OnPreMissionTick` then retreats it.
- `GetClosestFleePositionForAgent` refreshes the team's cached enemy data itself, so the send-off needs no
  extra call.
- `Mission.GetNearbyEnemyAgentCount(Team, Vec2, float)` is the public wrapper (`Mission.cs:5141-5144`) over the
  native `GetAgentCountAroundPosition` (`Mission.cs:2176-2182`). The native walks only the grid cells under the
  query circle, so the 15 m cap on the radius is a cheap bound, not a guard against a whole-battle scan. The
  earlier reading of this section, that the native walks the whole battle beyond 15 m, was wrong. That the
  native (`IMBMission.GetAgentCountAroundPosition`, native 0x6F71A0) walks only the hashed grid cells covering the
  query square and counts an agent only when its team index is 0 or more, `AgentFlag.IsHumanoid` (0x800) is set,
  its state is Active and its squared distance is within the radius squared; ally and enemy are split by the team
  hostility table.
- Vanilla mounts get no team: `Mission.SpawnMonster` and `CreateHorseAgentFromRosterElements` never set one
  (`Mission.cs:4441-4458`, `4601-4611`), and `Agent.SetTeam` (`Agent.cs:2220`) is the only caller of
  `Team.AddAgentToTeam`, so vanilla's `Team.ActiveAgents` holds no mount. TAOM's creature bandits are the exception:
  `CreatureBanditSpawner` (`Wire`, around lines 72 and 128) spawns them with `SpawnMonster` and then calls `SetTeam`,
  so they sit on a team without being human. That is why the scan counts non-humans as "others standing" and keeps
  them out of the soldier list.
- Vanilla uses the same target pair: `TaskForceDetachment.AddAgentAtSlotIndex` calls
  `SetAutomaticTargetSelection(false)` then `SetTargetAgent(TargetAgent)` (`TaskForceDetachment.cs:71-72`) and
  `RemoveAgent` gives automatic selection back (`TaskForceDetachment.cs:225`). It shows the pair is a normal
  engine idiom; whether a held archer's target then sticks in a live battle is still owed to the spike.
- A deleted agent's index is recycled within a mission (#592). State is keyed on the `Agent` object by
  reference, never on `Agent.Index`, and every write goes through `AgentSlotIdentity.IsCurrentOccupant`.

## Threading

Every read and write runs in `OnMissionTick`, the main thread, which never overlaps the agent tick of the same
frame. `MissionThreadGuard.NoteCall` is the tripwire on each of the five adapter writes (the adapter caches its
warn delegate once). No engine event is subscribed, so no `DeferredCallbackQueue` is needed. The
cross-thread reads are `TaomTacticBase.Standing` and `Failed` (plain flags written on the team-AI thread) and the
`TeamAIComponent._currentTactic` reference; a stale read costs one second.

## Performance

Each second, while the gate is open and at least one tweak is on, the adapter walks every active agent of every
team hostile to the player's (hundreds in a big battle) and builds one `EnemyAgentView` per human. On top of that
it makes a native `GetNearbyEnemyAgentCount` call for each panicked or cornered soldier, a native
`ImmediateEnemy` read for each possible archer inside the field range (after the managed `IsRangedCached`
check), and a target read for each held archer only when the debug log is on. The rules rebuild the cornered set
and the health map from the scan each pass; no army-sized set or dictionary is kept across passes except the
health map of panicked men. The cost has not been measured: the tick profiler A/B below is owed.

## Configuration

MCM group `Battle Tactics/Fell Foes` (GroupOrder 61; none apply a restart). Every value is validated by
`FellFoesTunables.Create` (NaN and infinity first, then range and sign, then ordering); a bad value reverts to
its default with a warning in the log. A changed default does not reach an existing `TAOM.json` (the persisted
value wins): to change a shipped default, rename the setting (with a comment saying why), never flip it.
Defaults ship ON (Mike, 2026-10-19), and the spike must run before the next `/release`.

| Setting | Type | Range | Default |
|---|---|---|---|
| Cornered Routers Fight | switch | | on |
| Cornered By No More Than | whole number | 1 to 10 | 3 |
| Idle Archers Target You | switch | | on |
| Archer Chance Each Second | decimal | 0 to 1 | 0.25 |
| Archers On You At Once | whole number | 1 to 30 | 8 |
| Last Stragglers Leave The Field | switch | | on |
| Stragglers, Most | whole number | 1 to 20 | 3 |
| Straggler Pause (seconds) | decimal | 5 to 300 | 30 |
| Fell Foes Debug Log | switch | | off |

Fixed numbers, validated the same way and not on a slider: cornered radius 5 m (1 to 15), cornered health floor
30 percent, rally cap 4 (0 to 10), archer hold 8 s, archer new per second 3 (1 to 10), archer morale floor 50,
archer range 35 m field and 20 m siege (the siege range may not exceed the field range), straggler peak 40 (the
straggler count may not exceed it). Co-op: the debug switch is instrumentation and excluded from the settings
fingerprint; the other eight change which soldiers fight, shoot or leave, so they are covered
(`CoopSettingsRelevance.cs`; Fell Foes stands down in co-op, so a mismatch on them would be a spurious line).

**Debug line.** With the debug log on, a pass that did something or holds an archer writes one line:
`[FellFoes] enemies=N peak=N stopped=N restopped=N unsteadied=N cornered=N sentOff=N released=N held H, aiming A of R checked`.
`stopped` counts routers stopped this pass (the rally included), `cornered` is the size of the turned set, and
`aiming A of R checked` counts both numbers over the same archers, the ones whose target was read this scan, so
A should equal R when the target sticks. A pass that did nothing and holds nobody writes no line. Per-handle
stop counts are not logged yet; a flicker check has to read `stopped` and `unsteadied` over time.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/FellFoes/FellFoesModule.cs` | Module: registrations and the mission behavior declaration, builds the gate check |
| `Main/Features/FellFoes/Hooks/FellFoesMissionLogic.cs` | Entry point (a `MissionLogic`): tick, catch, release at end and on removal |
| `Main/Features/FellFoes/Hooks/FellFoesMissionGate.cs` | Battle allowlist, co-op stand-down, `MissionEnded`; [Lost Footing](lost-footing.md) reuses it as it stands |
| `Main/Features/FellFoes/FellFoesService.cs` | Clock and gate, three isolated passes, `ReleaseHolds`, debug line |
| `Main/Features/FellFoes/FellFoesRules.cs` | The decisions, pure |
| `Main/Features/FellFoes/Domain/` | Tunables, per-mission state, scan snapshot, enemy view |
| `Main/Features/FellFoes/FellFoesSettingsProvider.cs`, `IFellFoesSettingsProvider.cs` | MCM to validated tunables, and its interface |
| `Main/Adapters/FellFoesAdapter.cs`, `IFellFoesAdapter.cs` | The only code that touches an agent, and its interface |
| `Main/Features/TaomSettings.cs` | The `Battle Tactics/Fell Foes` MCM group (GroupOrder 61) |
| `Main/Features/CoopInterop/CoopSettingsRelevance.cs` | Excludes the debug toggle from the settings fingerprint |
| `Main/Features/CultureDoctrine/Hooks/TeamTacticProbe.cs` | `IsHorseStanding`, the read-only probe |
| `Main/Features/CultureDoctrine/Hooks/Tactics/TaomTacticBase.cs` | `Standing`, the one flag the probe reads |

## Dependencies

- `ICoopSessionProvider` (CoopInterop): the stand-down test, the same one the stuck battle sweep uses.
- `MountDespawnMissionGate` (MountDespawn): the battle allowlist, shared with Battle Corpses.
- `TeamTacticProbe` and `TaomTacticBase` (CultureDoctrine): whether a team's horse stand is running.

## Tests

`TAOM.Tests/Features/FellFoes/`: `FellFoesTunablesTests` (every tunable: NaN, infinity, range, sign, ordering,
the rally cap), `FellFoesRulesTests` (the hit trigger and its window, order retreat, NaN health, the health
floor, the rally and its cap, contested only, re-flee, eviction; every archer gate, the cap of 8 and 3 a second,
the nearest first, each release path; the straggler peak, hold, reset, one send a tick, never twice, heroes,
reserves, a standing creature, field only), `FellFoesServiceTests` (throttle, the gate clock and a closed gate,
toggles, a failed write rolled back, one pass throwing, a throwing release, the debug line, release on
switch-off and at end), `FellFoesSettingsProviderTests` (defaults, stale values, slider ends, group order),
`FellFoesWiringTests` (one behavior and one state per mission, the gate reads `MissionEnded`, the co-op
classification). `HotPathSettingsProvidersTests` and `ReflectionSiteBindingTests` carry the provider and the
`TeamAIComponent._currentTactic` read. The adapter and the gate's engine half have no unit test; the owed checks
below cover them.

## Owed in game

1. **Custom Battle spike, before the next release.** Do stopped and rallied routers actually fight, and not
   flicker back to running (a formationless AI agent after `OnFleeing`)? Read `stopped`, `unsteadied` and
   `cornered` in the debug line over a few passes; `restopped` counts the handles stopped more than once this
   mission (tracked only while the debug log is on), so a flicker shows as a number above 0. Does a held archer's
   target stick (`held N, aiming M of N checked`; M should equal N)? `SetTargetAgent` only queues a request
   natively, and the only vanilla caller of the pair in the v1.5.5 source cache is `TaskForceDetachment`, so both
   are UNVERIFIED until this runs. If a spike fails, turn that switch off by default by renaming the setting; do
   not flip a persisted default.
2. **Campaign field battle with 500 or more troops.** Stragglers leave and the battle ends, with the vanilla
   message either "enemies are fleeing" or "battle won". Check both the prisoners and the rewards. A rallied man
   who stands idle after the rally, if any, shows here.
3. **Horse-stand skip.** A team in a horse stand ([culture-doctrine.md](culture-doctrine.md) "Horse stand") has
   its archers left alone. This can share #892's run.
4. **Tick profiler A/B.** With Enable Tick Profiler on, compare a big battle with Fell Foes on and off and read
   the `[TickProfile] top=` and `[Hitch]` lines.
5. **Siege.** The archer range is the shorter one and there is no straggler cleanup.
6. **Creature band.** Fight a creature bandit party to the end: the cleanup waits for the creatures.
7. **Tournament and hideout.** Inert.
8. **Javelin troops.** Troops with thrown weapons only are not held as archers.

## What is left for later

A captain's rally, hunters of the Ringbearer and reinforcements steadying routers (follow-up #940), straggler
cleanup in sieges and sally-outs. Routers regrouping and rampaging beasts are not planned.

## Changelog

- 2026-10-19: first version, three tweaks behind their own switches. Review fixes applied the same day: hit
  trigger and rally with a cap, contested only, real morale, the service owns the gate clock, the gate closes at
  `MissionEnded`, `IsRangedCached` for archers, a standing creature blocks the cleanup.

## GitHub Issue

- **Issue:** #893 Enemy battle behavior tweaks: cornered routers, archers, straggler cleanup
- **Follow-up:** #940 Fell Foes follow-up: Hunters of the Ringbearer and reinforcements steady the routers
- **Status:** Open until the in-game checks pass

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/culture-doctrine.md](./culture-doctrine.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reviews/rca-fell-foes-2026-10-19.md](../reviews/rca-fell-foes-2026-10-19.md)

<!-- backlinks-end -->
