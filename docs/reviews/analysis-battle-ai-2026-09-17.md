# Field-battle AI stack and TAOM's culture-doctrine layer: an engineering analysis

**Date:** 2026-09-17. **Question (Mike, verbatim):** "conduct a deep analysis of these systems so
that maybe we could even rewrite them entirely to make them better, more effective, more
efficient, etc. If possible." **Scope:** Bannerlord v1.5.3's field-battle AI stack (team AI to
tactics to formation AI to behaviours to agents) and TAOM's culture-doctrine layer on it
(`Main/Features/CultureDoctrine/`, #608, Phases A to D).

**Provenance.** Written by a dedicated analysis agent (Opus) over the v1.5.3 decompile at
`E:\Decompiled_Bannerlord\_categories_v1.5.3\` (which matches the installed game) and every
CultureDoctrine source file, then verified by the orchestrating session on the load-bearing
claims: the behaviour count in `TeamAIGeneral` (24, not the 27 the feature doc said), the
`FindBestBehavior` precalculation rule, the transfer-populated formation registration path, and
the self-cancelling gate in `TeamQuerySnapshotFactory`. Every `file:line` is from those files as
read on the date above. The three code defects it names (section 3.2 a, b, h) were fixed in the
same commit as this document; the rest is the roadmap.

**What this is not.** There is no measured battle profile in the repository. The cost model in
section 1.8 is an operation-count model derived from the source with its assumptions stated;
it says which layer can and cannot matter, not how many milliseconds a given machine spends.
The `[MissionPerf]` heartbeat exists to measure it; the A/B protocol in
`docs/features/culture-doctrine.md` is the gate.

---

## 1. The engine stack as built

### 1.1 Tick order and threads

`Mission.OnTick` (main thread) sets `tickCompleted = false` (`Mission.cs:3756`), runs every
`MissionBehavior.OnMissionTick` (`:3757-3760`), then either `TickAgentsAndTeamsAsync(dt)` or,
when async AI is off, `TickAgentsAndTeamsImp` inline (`:3784-3791`). Native calls back
`TickAgentsAndTeams` (`[MBCallback]`, `:1829-1832`) on another thread, and
`TickAgentsAndTeamsImp` (`:3617-3634`) does, in order:

1. `TWParallel.For(0, AllAgents.Count, num, AgentTickMT)` (`:3620`): `Agent.TickParallel` on the
   worker pool (`TWParallel.cs:72-82` falls through to a single-threaded body under 16 items,
   else the parallel driver).
2. `Agent.Tick` for every agent on the calling thread (`:3621-3624`).
3. `Team.Tick` for every team on the same thread (`:3625-3628`), then `tickCompleted = true` (`:3629`).

The next frame's `OnPreTick` (`:3546-3554`) spins in `WaitTickCompletion` (`:3601-3607`,
`Thread.Sleep(1)`) until the flag is set, so the async tick of frame N never overlaps
`OnMissionTick` of frame N+1. `Team.Tick` (`Team.cs:585-623`) gates on `Mission.AllowAiTicking`
(`:591`), issues Retreat to a routed side (`:593-602`), otherwise calls `TeamAI.Tick(dt)` when
`TeamAI != null && HasBots` (`:603-606`), then `Formation.Tick` for every non-empty formation
(`:616-622`). During deployment `DeploymentMissionController` holds `AllowAiTicking = false`
(`DeploymentMissionController.cs:31`) and `SetupAIOfEnemyTeam` flips it on, sets
`ForceTickOccasionally`, calls `team.ResetTactic()` and `team.Tick(0f)`, then flips both back
(`:141-160`). So the first decision of a field battle is on the main thread, and the steady
state is on the async AI thread. `Formation.SetControlledByAI(true)` (the F6 delegate) also
runs one `AI.Tick()` inline on the caller's thread (`Formation.cs:817-824`), a third main-thread
entry into the behaviour layer.

### 1.2 The team AI

`TeamAIComponent.Tick` (`TeamAIComponent.cs:196-213`) runs every frame from `Team.Tick`. It
gives the bodyguard formation a Charge weight when the general's formation is empty
(`:198-202`), calls `MakeDecision()` when `_nextTacticChooseTime.IsPast` and re-arms it with the
literal `MissionTime.SecondsFromNow(5f)` (`:203-207`), and calls `TickOccasionally()` on the
`_occasionalTickTime` cadence (`:208-212`), the constructor's `applyTimerTime` (`:94`), which
`TeamAIGeneral` passes as 1 s (`TeamAIGeneral.cs:12-15`). The 5 s is a literal, not a
constructor argument.

`MakeDecision` (`:254-336`, private):

- returns early when the mission is not continuing with an empty list, or when the team has no
  populated formation (`:257-260`);
- if no enemy team has a populated formation (`:261-269`), forces a `TacticCharge` from the list
  by `item is TacticCharge` (`:276-289`) and otherwise `availableTactics.FirstOrDefault()` (`:290-297`);
- calls `CheckIsDefenseApplicable()` (`:300`), false for any non-Defender (`:217-221`) and
  otherwise a ranged-pressure ratio test against 1.5 (`:222-246`);
- picks `availableTactics.MaxBy(to => to.GetTacticWeight() * (to == _currentTactic ? 1.5f : 1f))` (`:301`);
- if the winner differs from the current tactic and the current's `ResetTacticalPositions()`
  returns true, re-compares `winner.GetTacticWeight() > current.GetTacticWeight() * 1.5f` before
  switching (`:307-322`); the base returns false (`TacticComponent.cs:615-618`),
  `TacticDefensiveRing` and `TacticDefensiveLine` return true;
- switching runs the `CurrentTactic` setter, whose private body calls `OnCancel` on the old and
  `OnApply` plus one `TickOccasionally` on the new (`:76-85`), and shows the sergeant popup keyed
  on `GetType().Name` when the player is a sergeant on the player team (`:330-334`).

`TickOccasionally` (`:338-344`, public virtual) ticks the current tactic only when
`AllowAiTicking && Team.HasBots`. `ResetTactic` (`:184-194`, public) calls `MakeDecision()` then
`TickOccasionally()` at once. `_availableTactics` is a private list (`:36`) with public
`AddTacticOption`/`RemoveTacticOption`/`ClearTacticOptions` (`:129-142`). `TacticalPositions`
and `TacticalRegions` are public mutable lists (`:50-52`) filled from scene objects in the
constructor (`:96-97`). `OnNotifyTacticalDecision` is a public delegate field (`:32`); in the
field the only raiser is `TacticSallyOutHitAndRun.cs:333`.

### 1.3 Tactics

`TacticComponent` holds the five protected slot fields (`TacticComponent.cs:26-34`),
`IsTacticReapplyNeeded` (`:22`), and `AreFormationsCreated`, which on its first true runs
`ManageFormationCounts()`, `CheckAndSetAvailableFormationsChanged()` and sets the reapply flag
(`:46-64`). `ManageFormationCounts(int,int,int,int)` (`:520-526`) calls
`SplitFormationClassIntoGivenNumber` per class (`:220-461`), which moves units with
`TransferUnits` in both the split and the merge direction (`:268-346` split, `:347-442` merge),
then resets and defaults the behaviour weights of every touched formation (`:447-459`) and sets
the reapply flag (`:460`). `AssignTacticFormations1121` (`:475-506`) is the 1/1/2/1 default,
picking per class by `ChooseAndSortByPriority` (`:508-514`: AI-controlled first, then
`FormationPower`). `SetDefaultBehaviorWeights` (`:581-587`) enables Charge, PullBack, Stop and
Reserve at 1 on every apply.

Every field tactic is `public class ... : TacticComponent` with a `(Team)` constructor. The shape
is the same in all six phase tactics: `ManageFormationCounts` then `AssignTacticFormations1121`
(or the 1/1/1/1 of `TacticFrontalCavalryCharge.cs:290-301`), a `HasBattleBeenJoined` test on the
lead formation's distance over the enemy's `MovementSpeedMaximum` against 5 s (7 s for cavalry)
doubled once joined (`TacticDefensiveEngagement.cs:109-116`), and a `TickOccasionally` that
re-applies a fixed weight table on a phase flip, a formation-set change or the reapply flag
(`:146-171`). `TacticCharge.TickOccasionally` resets every formation's weights every second and
sets `BehaviorCharge` to 10000 (`TacticCharge.cs:14-26`). The weights are closed-form functions
of `TeamQuerySystem` ratios. Three of them have side effects inside the weight:
`TacticDefensiveEngagement` assigns `_mainInfantry` (`:184-187`), `TacticDefensiveRing` and
`TacticDefensiveLine` run `CheckAndDetermineFormation` (which reassigns slots and sets the
reapply flag, `TacticComponent.cs:594-613`) and re-determine their scene position when not current.

### 1.4 FormationAI and behaviours

`TeamAIGeneral.OnUnitAddedToFormationForTheFirstTime` (`TeamAIGeneral.cs:17-89`) is called from
`Formation.AddUnit` whenever a formation goes from 0 to more than 0 units (`Formation.cs:2324-2326`),
including a formation populated by `TransferUnits` (the `Agent.Formation` setter calls
`AddUnit`, `Agent.cs:1128-1180`, `Formation.cs:2216`). In single player it calls
`ForceCalculateCaches` and, if `BehaviorCharge` is absent, registers **24** behaviours
(`TeamAIGeneral.cs:63-86`) plus `BehaviorGeneral` or `BehaviorProtectGeneral` for the two
special formations (`:55-62`). Earlier TAOM documents said 27; the list has 24 entries.

`FormationAI` ticks from `Formation.Tick` only when the team has an AI and the formation is
AI-controlled or the player is a sergeant (`Formation.cs:2459-2462`). `Tick`
(`FormationAI.cs:232-238`) fires when `AllowAiTicking` and either `ForceTickOccasionally` or a
0.5 s timer staggered by formation and team index (`:102-118`). `TickOccasionally` (`:240-292`)
calls `FindBestBehavior` (`:169-210`):

- skips behaviours with `WeightFactor <= 1E-07` (`:175-178`);
- `num2 = GetAIWeight() * WeightFactor` (`:179`), where `GetAIWeight` is
  `GetAiWeight() * NavmeshlessTargetPositionPenalty` (`BehaviorComponent.cs:149-152`);
- multiplies the active behaviour by `Lerp(1.2, 2.0, clamp((PreserveExpireTime - now) / 5, 0, 1))`
  (`:180-183`); `PreserveExpireTime` is activation time plus 10 (`:63`), so the factor is 2.0 for
  the first 5 s, decays linearly to 1.2 at 10 s, and stays 1.2;
- calls `PrecalculateMovementOrder()` only on a candidate that beats the running maximum
  (`:184-197`), which is `CalculateCurrentOrder()` plus `CurrentOrder.GetPosition(Formation)`
  (`BehaviorComponent.cs:176-180`); the winner is moved to index 0 (`:202-206`), so on the next
  tick the active behaviour is evaluated first with its bonus and other candidates are
  precalculated only if they beat it.

When a winner exists and the formation is AI-controlled, `ActiveBehavior.TickOccasionally()`
runs (`:254`) and that is where every concrete behaviour pushes its orders; the base
`TickOccasionally` is empty (`BehaviorComponent.cs:145-147`). The special-behaviour path
(`:258-291`) is reached only when no regular behaviour has a weight factor and the active one is
`BehaviorStop`. Activation runs `OnBehaviorActivated` (`BehaviorComponent.cs:103-121`): the
soldier popup, the sergeant popup, and `OnBehaviorActivatedAux` only when AI-controlled.
`SetBehaviorWeight<T>` and `GetBehavior<T>` match with `is T` and the former throws for an absent
type (`:120-131`, `:138-155`). `ResetBehaviorWeights` calls every behaviour's `ResetBehavior`
(`:358-364`). `OnActiveBehaviorChanged` is a public event (`:100`), forwarded by
`Team.OnFormationAIActiveBehaviorChanged` (`Team.cs:419-425`).

Behaviour weights are mostly constants or cached reads: `BehaviorAdvance` 1, `BehaviorDefend` 1,
`BehaviorHoldHighGround` 1 with an enemy, `BehaviorProtectFlank`/`Vanguard`/`CavalryScreen` 1.2,
`BehaviorFlank` 0 whenever the enemy's closest formation is the flanker (`BehaviorFlank.cs:51-64`),
`BehaviorSkirmish` a ranged-ratio lerp (`:218-222`), `BehaviorStop` 0.01. Each computes its own
target in `CalculateCurrentOrder` from its own reads of `CachedClosestEnemyFormation`,
`ClosestSignificantlyLargeEnemyFormation`, `MainFormation`, and so on. `BehaviorTacticalCharge`
short-circuits cavalry to a bare `ChargeToTarget` (`:149-153`), which is why its charge-through,
reform, brace machine (`:59-139`) never runs for horse.

### 1.5 The two query systems

Every value is a `QueryData<T>` evaluated lazily on read when its lifetime has expired
(`QueryData.cs:18-37`); `Expire` zeroes the expiry (`:88-91`), so a query nobody reads costs
nothing and a lifetime only bounds the refresh rate.

`TeamQuerySystem` (`TeamQuerySystem.cs:226-557`):

| Query | Lifetime | Cost per evaluation |
|---|---|---|
| `MemberCount`, ally/enemy member counts | 2 s | O(formations) / O(teams) |
| `AveragePosition`, `MedianPosition` | 5 s | O(team agents) |
| `AverageEnemyPosition` | 5 s | O(enemy agents) |
| `MedianTargetFormation`, `MedianTargetFormationPosition` | 1 s | O(enemy formations) |
| flank edges | 5 s | O(1) |
| class ratios (own, ally, enemy) | 15 s | O(formations) over cached per-formation ratios |
| `TeamPower` | 5 s | O(formations) |
| `RemainingPowerRatio` | 5 s | O(formations) with `CasualtyHandler` |
| `TotalPowerRatio` | 10 s | |
| `MaxUnderRangedAttackRatio` | 3 s | O(all friendly units) with `Equipment.HasShield()` per agent |

`FormationQuerySystem` (`FormationQuerySystem.cs:315-665`):

| Query | Lifetime | Cost |
|---|---|---|
| `FormationPower`, `FormationMeleeFightingPower` | 2.5 s | O(units) |
| `EstimatedDirection`, `EstimatedInterval` | 0.2 s | O(units), two passes; read at about 1 Hz by `CacheFormationIntegrityData` |
| `AverageAllyPosition` | 5 s | O(formations x units) |
| `LocalAllyUnits`, `LocalEnemyUnits` | 5 s | native proximity map within 30 m |
| class and shield/throwing ratios | 2.5 s (sync group) | O(units) each |
| `MovementSpeedMaximum`, `MaximumMissileRange`, `MissileRangeAdjusted` | 10 s | O(units) |
| local ratios, local power | 15 s / 5 s | O(local agents) |
| `IsUnderRangedAttack`, `UnderRangedAttackRatio`, `MakingRangedAttackRatio` | 3 s | O(units) |
| `ClosestEnemyAgent` | 1.5 s | O(all enemy active agents) |
| `ClosestSignificantlyLargeEnemyFormation`, `Fastest...` | 1.5 s | O(enemy formations) with navmesh Z reads |
| `WeightedAverageEnemyPosition` | 0.5 s | O(all enemy active agents); read every frame by `FacingOrderLookAtEnemy`, so evaluated at 2 Hz per formation under that facing order |
| `HighGroundCloseToForeseenBattleGround` | 10 s | native slope search |
| `IsUnderCavalryChargeFromFront` | 2 s | O(1) over cached values (`:646-663`) |

`Formation.SetArrangementOrder` with a type change calls `QuerySystem.Expire()` and
`ForceCalculateCaches()` (`Formation.cs:762-766`); `TransferUnits` expires both formations and
both team systems (`:2150-2153`).

### 1.6 Formation.Tick, orders, arrangements

`Formation.Tick` (`Formation.cs:2440-2511`) runs every frame per non-empty formation: the
position/velocity cache on a 0.075 to 0.125 s timer, the closest-enemy-formation cache on 1.4 to
1.6 s, integrity data on 0.9 to 1.1 s, movement speed on 1.9 to 2.1 s, then `AI.Tick()`, order
substitution, `ArrangementOrder.TickOccasionally` and `Arrangement.OnTickOccasionally` on 0.5 s
timers, then every frame the movement order tick, `CreateNewOrderWorldPositionMT` (under a lock),
`FacingOrder.GetDirection`, `SetPositioning`, detachments, the `OnTick` event, and
`SmoothAverageUnitPosition`.

`SetMovementOrder` (`:707-737`) compares with `AreOrdersPracticallySame`
(`MovementOrder.cs:640-675`: same enum, same target, or a Move within 1 m for AI), and on a real
change runs `OnCancel`, the defensiveness update and `OnApply`. Two things scale with unit count
here. First, a change between the Charge family and anything else rewrites `Agent.Defensiveness`
on every unit (`:721-731`, `:2838-2844`), and that setter calls `UpdateAgentProperties()` on each
agent (`Agent.cs:1112-1126`), which is the full `AgentStatCalculateModel.UpdateDrivenProperties`
(including TAOM's aggression post-pass). Second, `MovementOrder.OnApply` calls
`RefreshBehaviorValues` on every unit (`MovementOrder.cs:702-705`), and so does
`ArrangementOrder.OnApply` (`ArrangementOrder.cs:136-151`, which also runs `UpdateAgentProperties`
per unit at `:143`). A `SetArrangementOrder` with a type change rebuilds the arrangement
(`Rearrange`, `Formation.cs:2549-2561`); an unchanged type is a `SoftUpdate` (`:768-771`).

The order vocabularies are closed enums: `MovementOrderEnum` has ten members, `ArrangementOrderEnum`
eight. `GetArrangement` maps them (`ArrangementOrder.cs:114-124`): `Square` builds a
`RectilinearSchiltronFormation` (`:121`), a solid block by construction
(`RectilinearSchiltronFormation.cs:5-30`, `SquareFormation.cs:99-108`, `:143-179`). The plain
`SquareFormation` (public, `:48`) fills concentric rings from the outside in and leaves the
centre empty whenever the rank count is below the maximum; under `FormOrder.OnApply` the rank
count is `ceil(N / maxFileCount)` (`FormOrder.cs:120-134`, `:237-239`). For 200 men that gives 64
files, 4 rings, 17 per side, and an empty 9 by 9 interior. It is unreachable through any
`ArrangementOrder`, but `Formation.Rearrange(IFormationArrangement)` is public (`Formation.cs:2549`)
and only rebuilds when the type differs; this is the hollow-square route in section 5.

### 1.7 The agent layer

`Agent.TickParallel` (workers) runs every component's `OnTickParallel` (`Agent.cs:4753-4756`) and,
on a 0.45 to 0.55 s timer, `HumanAIComponent.ParallelUpdateFormationMovement`
(`HumanAIComponent.cs:666-704`): the formation frame, the speed limit, the catch-up decision,
and the native `TrySetFormationFrame`. `Agent.Tick` (AI thread) runs every component's `OnTick`
and `TickAsAI` (`:4798-4807`), which on the same timer runs `ApplyFormationValuesPostUpdate`.

What the per-agent AI does with the formation's order is expressed through two channels. First,
`HumanAIComponent.SetBehaviorValueSet` (`:718-781`) loads one of five preset curve sets chosen by
`RefreshBehaviorValues` from the movement and arrangement order (`:783-815`: Charge family gives
the Charge set; Circle/ShieldWall/Square give DefensiveArrangementMove, whose Melee and Ranged
curves are near zero); `OverrideBehaviorParams` is public (`:144-153`) and marks the set as
Overriden until the next order-driven refresh. Second, `Agent.Defensiveness` (`Agent.cs:1112-1126`),
written by the formation from the movement and arrangement order (`Formation.cs:2838-2844`; the
Charge family gives 0, `MovementOrder.cs:422-429`), feeds the driven properties:
`AIDecideOnAttackChance = 0.5 * Defensiveness` (`AgentStatCalculateModel.cs:185`),
`AIAttackOnDecideChance` scaled by `(3 - Defensiveness)` (`:224`), shield use (`:207`, `:214`).
The native cadences are also driven properties (`:208-211`). Morale is per agent: initial morale
is 35 plus a random 0 to 29 plus component additions, then the model, clamped to 15..100
(`CommonAIComponent.cs:66-76`); `CanPanic` asks `BattleMoraleModel.CanPanicDueToMorale` first
(`:174-193`) from the worker tick; when the answer is no, the component floors morale at 0.01
and the agent keeps fighting under its formation's order (`:84-94`).

### 1.8 Where the time goes at 800 v 800

Assumptions: 1,600 humans plus roughly 300 mounts, 5 to 8 populated formations per side, 60 fps,
difficulty modifier 0.5. Operation counts follow from the periods above.

| Layer | Work per second of game time | Scales with | Where |
|---|---|---|---|
| Native per-agent AI, animation, physics, pathfinding, collision | 60 frames x 1,900 agents; simple-behaviour re-decisions every 1.5 to 2.25 s, movement recompute every 0.25 to 0.5 s, movement apply every 50 to 100 ms per agent | agents | native, workers and the AI thread |
| Managed per-agent tick | 60 x 1,900 `TickParallel` and `Tick`; about 3,800 formation-frame updates plus 3,800 `SetFormationInfo` | agents | workers, then the AI thread |
| Order-change cascades | per real Charge/non-Charge or defensive-arrangement change: O(units) `UpdateAgentProperties` (full stat model plus TAOM's post-pass) and O(units) `RefreshBehaviorValues`; a type change of arrangement also rebuilds the arrangement and expires the formation's whole cache | units of the formation, per change | AI thread, synchronous inside the tactic or behaviour tick |
| `Formation.Tick` per frame | 60 x ~12 formations: order position under a lock, facing direction (2 Hz O(enemy agents) per formation under LookAtEnemy), positioning, detachments; ~10 Hz O(units) average and median | formations, enemy agents | AI thread |
| Query caches | bounded by lifetimes: the O(units) formation queries at 2.5 to 10 s, `MaxUnderRangedAttackRatio` O(team) at 3 s, `ClosestEnemyAgent` O(enemy agents) at 1.5 s per reader | units, enemy agents | AI thread, on read |
| FormationAI | 2 Hz x ~12 formations: one pass over 24 to 29 behaviours, 6 to 8 weight functions with a factor, one to three `PrecalculateMovementOrder`, one active `TickOccasionally` with `SetMovementOrder` (early-out) and a `SetArrangementOrder` (`SoftUpdate` when unchanged) | formations | AI thread |
| Team AI | 0.2 Hz x 2 teams: 8 to 12 `GetTacticWeight` over cached ratios; 1 Hz x 2 teams: one tactic tick (a distance test and a formation-count loop; a full weight apply only on a phase change) | tactics | AI thread |
| TAOM doctrine on top | the same 0.2 Hz weights over a `TeamQuerySnapshot` of cached reads plus one O(formations) scan; 1 Hz tactic tick with the race check (O(enemy formations)) and the volley check; 2 Hz behaviour ticks with the brace and machine steps (cached reads) | formations | AI thread |

Two conclusions follow. First, the team, tactic and formation decision layers together are a
few thousand cached property reads and a few dozen small loops per second; they cannot be a
measurable share of a 16 ms frame at any army size the engine supports, and no rewrite of them
can buy frame time. Second, the managed costs that do scale are the per-agent formation-frame
update (2 Hz per agent, engine-owned, untouched by any team-AI design) and the order-change
cascades (`UpdateAgentProperties` and `RefreshBehaviorValues` per unit on every real order
change), which a decision layer controls only by how often it changes orders. A design that
flips a 300-man formation between Charge and Move every second would cost more than the whole
vanilla decision stack; the cycle charge's `ChargeToTarget` to `Move` transitions every 5 to
15 s on 40 to 100 riders are bounded and fine. The wall's arrangement changes go through
`SetArrangementOrder` only on a stance change, which is the right shape.

So a rewrite can affect: which orders each formation gets and when (the "brain"), the query
traffic it generates, and how often it triggers cascades. It cannot affect: the per-agent native
AI, animation, physics, the formation frame math, the arrangement slot math, morale ticking, or
the closed order vocabulary. That is the floor for every option in section 4.

---

## 2. What the engine layer cannot do

| # | Ceiling | Engine evidence | TAOM today |
|---|---|---|---|
| 1 | The tactic decision cadence is a 5 s literal, and the 1.5x hysteresis is inside a private method | `TeamAIComponent.cs:206`, `:301`, `MakeDecision` private (`:254`), `CurrentTactic` setter private (`:76`) | Works within it: the ordering test tunes multipliers to beat the 1.5x. Cannot change the cadence without a `TeamAIComponent` subclass (which can, see 4B) or Harmony |
| 2 | One tactic per team; a formation's doctrine is whatever the current tactic's table says | `_currentTactic` (`:48`); every weight table is written by the tactic | Per-side doctrine only; per-formation flavour is a table row, not a formation property |
| 3 | `ManageFormationCounts` folds every class to a fixed split, moving units and resetting weights | `TacticComponent.cs:520-526`, `:447-460` | Per-tactic override with 2/1/2/1, 3/1/2/1, 1/1/1/1 and the vanguard split; the churn on switch remains (3.2 g). A formation flagged `enforceNotSplittableByAI` is exempt from every consolidation (`Formation.cs:326-347`, `TacticComponent.cs:243`), which is how the routed vanguard now survives vanilla tactics |
| 4 | No shared battle picture: every behaviour and every weight recomputes its own target from its own query reads | `BehaviorDefend.cs:18-48`, `BehaviorTacticalCharge.cs:141-153`, `BehaviorFlank.cs:51-64` | Same shape: `TeamQuerySnapshot` is per weight call, and each TAOM behaviour resolves `Target()` itself |
| 5 | No reserve timing and no enemy-intent model beyond velocity dot products | the only intent reads are `CachedCurrentVelocity` dot tests (`FormationQuerySystem.cs:646-663`, `BehaviorTacticalCharge.cs:118-128`) | `TwoLineWall` commits the second line on the joined flag, a distance rule; no intent model |
| 6 | `TacticalPosition`/`TacticalRegion` come from scene entities | `TeamAIComponent.cs:96-97`; `TacticDefensiveLine.cs:335-340`, `TacticDefensiveRing.cs:399-421`; only the navmesh high ground needs no entity | Worked around for the ring with the public runtime constructor; not yet for `DefensiveLine`/`HoldChokePoint`, whose rows are dead on entity-less scenes |
| 7 | The arrangement set is closed by enum; `Square` is a solid schiltron | `ArrangementOrder.cs:114-124`, `RectilinearSchiltronFormation.cs:5-30` | The brace uses the solid square. A hollow square is reachable only through `Formation.Rearrange` with a plain `SquareFormation` (section 5) |
| 8 | Firing orders are per formation and every vanilla archer behaviour resets to fire-at-will on activation | `Formation.SetFiringOrder` (`Formation.cs:798-808`); `BehaviorDefend.cs:93`, `BehaviorTacticalCharge.cs:261` | `VolleyControl` re-asserts once a second; up to 1 s of fire-at-will after any activation |
| 9 | Morale is per agent with a side casualty factor; no formation cohesion concept | `CommonAIComponent.cs:66-76`, `:174-193`; `SandboxBattleMoraleModel.cs:18-31`, `:122` | Two seams overridden; the other five abstract seams are open |
| 10 | No commander memory across decisions | `MakeDecision` keeps only `_currentTactic`; `TacticalDecision` exists (`TacticalDecision.cs:5-28`) but nothing in the field raises it | None; each `GetTacticWeight` is stateless over the snapshot |
| 11 | The player/AI split is per formation, and `Team.DelegateCommandToAI` re-applies whatever behaviour is active | `Formation.IsAIControlled` (`Formation.cs:182`); orders apply only when AI-controlled (`FormationAI.cs:245`, `:286`); `SetControlledByAI` forces one AI tick and re-issues the active order (`Formation.cs:810-839`) | Respected: TAOM never writes a non-AI formation |
| 12 | The sergeant/soldier popups are keyed on type names inside private code | `TeamAIComponent.cs:330-334`, `BehaviorComponent.cs:103-121`, `:161-165` | Worked around with the same simple type names and string rows |
| 13 | The deployment plan is resolved at mission creation, before mod behaviours are appended | `Mission.cs:1811-1815` reads `MissionDeploymentPlanningLogic` in `Initialize`; TAOM's behaviours arrive in `AfterStart` (`:3829-3832`) | Not touched; Harmony-only |
| 14 | `TeamAIGeneral` registers behaviours only when a formation first fills | `TeamAIGeneral.cs:17-89`, `Formation.cs:2324-2326` | `BehaviorWeightApplier.Ensure<T>` adds TAOM types lazily |

Which of these a rewrite could lift: 1 (cadence, via a subclass), 4, 5 and 10 (a shared picture
and memory are TAOM-side objects), 6 (runtime positions), 7 (a `Rearrange` route), 8
(event-driven re-assert). Which no rewrite lifts: 3's unit transfers (any split moves men), 9's
per-agent mechanics (a model, not a controller), 11, 12, 13 (short of Harmony), and the closed
order vocabulary.

---

## 3. TAOM's current layer, evaluated

### 3.1 What it does well

- **Public seams only, no Harmony.** The swap is `ClearTacticOptions`/`AddTacticOption`/`ResetTactic`
  in `EarlyStart`, after `MissionCombatantsLogic.EarlyStart` and before any populated-formation
  decision (`TeamAIComponent.cs:257`). The vanilla wrappers scale `base.GetTacticWeight()` and
  keep the side effects. The gating on `Mission.IsFieldBattle` is a live read.
- **The vanilla lifecycle written once.** `TaomTacticBase` reproduces `AreFormationsCreated`, the
  joined test, `CheckAndSetAvailableFormationsChanged` extended to every slot, and the reapply
  flag, with the per-tactic variance in a `DoctrinePlan`. Eleven tactics are 15 to 45 lines each.
- **Wrapped lifecycle on the AI thread.** Every override body is a try/catch that latches
  `Failed` and reports weight 0, so a throw cannot unwind into `TickAgentsAndTeamsImp`. State is
  engine handles plus constructor values; no IoC on the tick.
- **Pure cores with tests.** The race, the brace table, the cycle, the skirmish machine, the
  volley rule, the envelop geometry, the phase machine are plain functions over plain inputs.
  The engine reads are at the boundary.
- **Real gaps in vanilla closed.** The race answers a question vanilla never asks
  (`TacticDefensiveEngagement.cs:189-191` only lowers weight by distance; `BehaviorHoldHighGround.cs:31-48`
  tracks then locks). The cycle charge runs the machine vanilla disables for cavalry
  (`BehaviorTacticalCharge.cs:149-153`). The brace reads the cavalry-charge query vanilla's wall
  never reads. The envelop wing avoids `BehaviorFlank`'s zero.
- **Per-culture data, three tiers.** Tactic rows, morale (two model seams), aggression (a
  post-pass on driven properties), and formation routing through the unsubscribed
  `GetAgentTroopClass_Override`.
- **Reachability tested, not felt.** The ordering test computes the nine vanilla formulas and
  asserts the 1.5x margin, which the 2026-09-16 RCA shows was needed.

### 3.2 Real weaknesses

**(a) Two TAOM tactics cancelled themselves within 5 s below an army-size threshold, because their
gate read the largest infantry formation after their own split.** (Fixed 2026-09-17.)
`TeamQuerySnapshotFactory.Take` set `infantryCount` to the largest infantry formation's count.
`TwoLineWall` returned 0 under 80 and `Envelop` under 60 (`DoctrineWeights.cs`). At the first
decision the infantry is one formation, so both passed; on apply, the 2/1/2/1 and 3/1/2/1 splits
halved or thirded it, and at the next `MakeDecision` the gate read half or a third, returned 0,
and the team switched back, whose 1/1/2/1 recount merged the lines or the wings again. The bands
were roughly 80 to 159 total infantry for the two-line wall and 60 to 179 for the envelopment.
The ordering test could not see it because its canonical snapshot passed the pre-split total. Fix:
the snapshot now carries `InfantryTotal` (the sum over infantry formations) and the two gates
read it; the edge multipliers were also raised from 1.05 and 1.1 to 1.6, because a 1.1x edge could
never beat the engine's 1.5x sticky factor once the other tactic held the team.

**(b) The brace was blind to a flank or rear charge.** (Fixed 2026-09-17.) `WallStances.Braced`
read only `IsUnderCavalryChargeFromFront`, and that query requires either a circle/square
arrangement or `v.DotProduct(Formation.Direction) < -0.75` (`FormationQuerySystem.cs:653-656`),
that is, the wall must already face the charge within about 41 degrees, and it evaluates only
the single closest significant enemy formation, so a cavalry sweep behind an infantry screen
was invisible. The signal is also 2 s stale. Fix: `CavalryThreat` scans every enemy cavalry
formation's velocity and ETA regardless of our facing (velocity 0.1 s fresh, `Formation.cs:2100`),
with the engine query kept as a second source.

**(c) Tactic weights are hand-tuned formulas competing with vanilla's on one axis.**
`DoctrineWeights` mirrors vanilla's constants so that eleven TAOM functions and nine scaled
vanilla functions can be compared by `MaxBy`. The decision is therefore an emergent property of
twenty formulas and a JSON of multipliers, gated by a test that computes them at one nominal
army. A doctrine choice ("Dwarves defending wall up") is stated nowhere as a rule; it is implied
by `1.2 * advantage / sqrt(power)` beating `1.1 * advantage * num2 / sqrt(power)` times a
multiplier. Every new tactic adds a row to that competition and a canonical army to the test.

**(d) The ordering test is a proxy for reachability, not reachability.** The snapshot fixes
`NotEngagingAdvantage`, `RemainingPowerRatio` and the scene score; `TacticDefensiveLine` and
`HoldChokePoint` at score 1 assume an entity that most TAOM scenes may not have, and the live
`RemainingPowerRatio` moves defensive and offensive weights in opposite directions, so the
ordering at one point says little about the ordering after casualties.

**(e) Static plan tables react only through the Defend/Engage flip and the race re-check.**
`TacticPhaseMachine.Step` has three inputs; `OnPhaseTick` is used by the race and the volley.
Nothing in a plan responds to what the enemy is doing: a defending wall keeps both cavalry
blocks on `ProtectFlank` regardless of whether the enemy has any cavalry.

**(f) Each TAOM behaviour re-derives its target and its threat.** Two wings can pick different
`ClosestSignificantlyLargeEnemyFormation`s and envelop different bodies, and the tactic that
owns them has no view of what they chose.

**(g) `ManageFormationCounts` churn on any switch between splits.** Every switch between a
1/1/2/1, 2/1/2/1, 3/1/2/1 and 1/1/1/1 tactic transfers units both ways, with the cascades of
section 1.6 and men walking to new slots mid-fight. `Envelop`'s live numbers gate makes it
happen whenever attrition crosses the 1.2x ratio; with the edge now at 1.6x the switch is
one-way per crossing rather than a flap.

**(h) Re-applying a plan reset the skirmish machine.** (Fixed 2026-09-17.) `ResetBehaviorWeights`
called `BehaviorInfantrySkirmish.ResetBehavior`, which reset the machine to `Throwing`, so any
reapply re-armed a `Committed` javelin line with no javelins until the can't-shoot window ran
out again. `Committed` now survives a reset and a re-activation.

**(i) Doctrine is per side, not per formation.** `SideProfile` takes the majority faction
culture. Morale and aggression are already per soldier, so the tactic layer is the odd one out.

**(j) Volley control fights activations at 1 Hz.** Section 2 row 8: a `BehaviorSkirmish`
activation between two tactic ticks fires at will for up to a second.

**(k) Cache periods versus the decisions that read them.** The race compares ETAs from
`MovementSpeedMaximum`, a 10 s cache of the units' maximum speed, which does not change with the
arrangement's walk restriction (that is `CachedMovementSpeed`, 2 s, `Formation.cs:1441-1494`);
the 3 s margin covers some of it, but "our ETA at maximum speed" is optimistic for a formation
walking in a wall.

**(l) No memory, no learning, no recognition of the enemy's doctrine.** Nothing carries between
decisions but `_currentTactic`.

**(m) Two documentation inaccuracies, corrected 2026-09-17.** The 27-behaviour count (24 in
`TeamAIGeneral.cs:63-86`); the "PrecalculateMovementOrder on every candidate" claim (it runs only
on a candidate that beats the running maximum).

**(n) The stated reason for rejecting a `TeamAIGeneral` subclass was not supported by the code.**
The doc said behaviours registered from `OnUnitAddedToFormationForTheFirstTime` "miss formations
populated by `TransferUnits` during a split". `TransferUnitsAux` assigns `item2.Formation = target`
(`Formation.cs:2216`); the `Agent.Formation` setter calls `_formation.AddUnit(this)`
(`Agent.cs:1128-1180`); `AddUnit` fires the hook on the 0 to more-than-0 transition
(`Formation.cs:2324-2326`). The real trade-offs of that route are in section 4B; the doc's bullet
is corrected.

---

## 4. Rewrite options

Common floor for all: the closed order vocabulary, the per-agent native AI and its driven
properties, `Formation.Tick`'s frame math, the arrangement classes, and the per-agent morale tick
(section 1.8). Common thread rule for all: whatever sits in `Team.Tick` runs on the async AI
thread in play and the main thread during deployment and on an F6 delegate (section 1.1).

### A. Keep `TeamAIComponent` and `FormationAI`; extend the doctrine layer (status quo plus a shared per-team picture and Phase E)

- **Replaces:** nothing. Adds a per-team `BattlePicture` object refreshed once per tactic tick
  (1 Hz) and exposed read-only to the team's TAOM tactics and behaviours, and per-formation plan
  rows keyed on the formation's majority culture.
- **Seams:** the ones in use, plus `FormationAI.ActiveBehavior` and
  `Formation.GetReadonlyMovementOrderReference()` on enemy formations (both public, read by
  vanilla itself at `FormationQuerySystem.cs:632`), `Formation.CachedCurrentVelocity` and
  `CachedMovementSpeed`, `Team.OnFormationAIActiveBehaviorChanged` (`Team.cs:419-425`).
- **Gains:** fixes (e), (f) and (j) directly; an enemy-intent classifier becomes exact rather
  than inferred (the enemy formation's order enum and active behaviour type are readable);
  reaction rules can live in `OnPhaseTick`, which already exists as a once-a-second hook; Phase E
  reuses `BannerBearerAssignmentMissionLogic.ResolveFormationCultureId`'s shape.
- **Cost:** two to four new files, no lifecycle change, no new thread rules (the picture is
  built and consumed on the same tick), no save or campaign impact. Risk is low; the one
  subtlety is that the picture must be an immutable snapshot swapped by reference, because
  `taom.tactic_status` reads from the main thread.
- **Verdict:** required regardless of anything else; it is the cheapest way to make the layer react.

### B. Replace the team AI with a TAOM subclass of `TeamAIGeneral` via `Team.AddTeamAI`

- **Seam:** `Team.AddTeamAI` (`Team.cs:459-477`): sets `TeamAI`, calls `SetControlledByAI` per
  formation (respecting the player general), `InitializeDetachments` (idempotent),
  `CreateMissionSpecificBehaviors`, `ResetTactic` (early return with empty formations), one
  formation AI tick each, `TickOccasionally`. `Tick(float)` is `protected internal virtual`
  (`TeamAIComponent.cs:196`) and `TickOccasionally` public virtual (`:338`), so a subclass in
  another assembly overrides both. `OnDeploymentFinished` is virtual (`:154`).
- **What is kept:** the behaviours already on every `FormationAI` (they belong to the formation,
  not the team AI); the inherited first-fill registration for formations that fill later
  (3.2 n); `TacticalPositions`/`TacticalRegions` (rebuilt by the base constructor); the sergeant
  popup (inside `MakeDecision`, which the subclass still reaches through `ResetTactic()`).
- **What is gained:** the cadence. `Tick` is called every frame; an override can run its own
  timers and call the public `ResetTactic()` whenever it wants a decision, and its own
  `TickOccasionally` at any rate, with the base's bodyguard rule reproduced in three lines. A
  per-tick battle picture computed once before the current tactic ticks. Commander memory as
  fields on the subclass.
- **What is not gained:** `MakeDecision`'s body and the `CurrentTactic` setter are private, so
  tactic selection still passes through `MaxBy` x 1.5 and the forced-Charge rule. The workaround
  needs no patch: the subclass (or, under A, a per-team commander object) decides, and every
  registered tactic's `GetTacticWeight` answers `chosen ? 1 : 0`. The 1.5x cannot resurrect a
  zero, so the engine's picker becomes an executor of a decision TAOM made explicitly, and the
  forced Charge when no enemy remains is kept for free. The vanilla wrappers can answer the same
  way, which removes the twenty-formula competition of (c) entirely and turns the ordering test
  into a decision-table test.
- **Cost:** one class of about 150 lines plus the commander. No save or campaign impact. Risk:
  medium-low; the new surface is the `Tick` override on the AI thread, which the existing
  wrapping discipline covers.
- **Verdict:** worth doing only when a faster cadence or a deployment-end hook is demonstrably
  needed; the weights-as-decision trick and the picture are available under A without it. Keep
  B as stage 4, not stage 1.

### C. Keep the team AI; replace the behaviour layer with one TAOM doctrine behaviour per formation over the shared picture

- **Seam evaluation against `FindBestBehavior`:** the four default behaviours are re-enabled at 1
  on every apply (`TacticComponent.cs:581-587`), so the doctrine behaviour competes with
  `BehaviorCharge` (up to about 1.2 with class factors), `BehaviorPullBack`, `BehaviorReserve`
  and `BehaviorStop` (0.01); a weight of 10 ends the competition, as `TacticCharge`'s 10000 does.
- **Gains:** (f) solved by construction; one place per formation for stance, target, firing
  order and arrangement; per-formation doctrine falls out.
- **Cost:** it reproduces the parts of the 24 vanilla behaviours a culture uses (about 1,500
  lines of vanilla logic), or it delegates to them by instantiating vanilla behaviour objects
  internally, which their `Formation.SetMovementOrder` calls in `TickOccasionally` would make
  double writers. Risk: medium; the fallback story when the doctrine behaviour fails is `Failed`
  plus weight 0, after which the vanilla rows at 0 leave the formation on the four defaults (a
  Charge), which is worse than today's failure mode.
- **Verdict:** not now. It is the natural end state if the picture-driven behaviours multiply,
  but it should be reached by growing the existing five TAOM behaviours to read the picture, not
  by a big-bang replacement.

### D. Full replacement: own team AI plus own formation controller issuing orders directly

- **Replaces:** decision-making at both levels. The subclass of B keeps every vanilla weight at
  0; `FindBestBehavior` then returns false (`FormationAI.cs:175-178`, `:199-209`) and no
  behaviour writes orders. The controller writes the five public order setters from the `Tick`
  override at its own cadence.
- **What the engine still does underneath:** everything in section 1.8 below the decision
  layer; also `SetControlledByAI`'s re-issue on F6 (`Formation.cs:819-831`, guarded by
  `AI.ActiveBehavior != null`, which stays null), the order substitution in `Formation.Tick`,
  retreat on a routed side (`Team.cs:593-602`), the `IsAIControlled` gate which the controller
  must honour itself.
- **Unavoidable Harmony:** none for the decision layer. The cadence comes from the `Tick`
  override; the popups can be raised by TAOM as vanilla does or dropped; the deployment plan
  (row 13) would need a prefix on the mission opener, which this option does not require.
- **True floor:** the controller still speaks ten movement orders and eight arrangements to
  native agents whose micro-decisions are driven properties; the "smartest" controller can only
  choose positions, targets, stances and timing. What it would have to reproduce: the
  target-position validity work vanilla does in `NavmeshlessTargetPositionPenalty` and
  `MovementOrder.GetPositionAux` (`MovementOrder.cs:1137-1198`), the skirmish and screen
  geometry, the soldier/sergeant information, and every guard the lessons list for
  `SetMovementOrder`.
- **Cost:** 3,000 to 5,000 lines to reach parity with what vanilla plus TAOM do today, all on
  the async thread, with a failure mode (a throw in the controller) that leaves formations under
  no behaviour at all.
- **Verdict:** no. It buys nothing the maintainer listed (uniqueness, reaction, growth) that A
  plus B do not, and it spends the one thing the current layer has, which is vanilla's
  twenty-four behaviours as free, tested fallbacks.

### E. Per-agent trees for doctrine

The RCA is definitive on the thread: `Agent.Tick` runs every `AgentComponent.OnTick` on the async
AI thread (`Agent.cs:4798-4807`, `Mission.cs:3621-3624`) and `TickParallel` runs `OnTickParallel`
on the worker pool; the freezes were unsynchronised reads racing main-thread writes, and the fix
moved the trees to `BehaviorTreeMissionLogic.OnMissionTick`
(`docs/reviews/rca-warg-clip-on-horse-2026-09-13.md`, "Mechanism 2"). Even if that were free,
the tool is wrong for doctrine for three reasons that do not depend on cost: a formation-level
decision evaluated per agent is N copies of the same picture with no way to coordinate (the
engine's own answer to "1,600 agents, 12 decisions" is the formation); the micro layer per agent
already exists natively and is tuned through driven properties, which TAOM already writes per
culture and could extend through `OverrideBehaviorParams` (`HumanAIComponent.cs:144-153`); and
the formation's order is the only channel through which a per-agent decision could express
"hold the line", so a tree would end up writing formation orders from N agents at once.
Per-agent work belongs in models and driven properties; nothing doctrinal belongs in a tree.

### F. A data-driven doctrine language versus C# plan tables

The plans are already data: `DoctrinePlans.cs` is static readonly tables over closed enums, and
the JSON carries the per-culture rows, morale, aggression and routing. Moving the plan tables to
JSON would trade compile-time checking of roles and behaviour kinds (`DoctrineSwitchInvariantTests`
pins the switch today) and the `DoctrinePlansTests` structural checks for a schema validator
TAOM would have to write, and it would not even give restart-free tuning, because the provider is
`Reuse.Singleton` and the toggle is read per mission. A weight-expression mini-DSL is worse: the
weights should stop being formulas (4B's decision trick), so a language for them would be built
for a thing about to be removed. What a solo maintainer does benefit from: the numeric tunables
(race, volley, cycle, brace hold, thresholds) in the JSON as a per-culture "personality" block
with defaults, a console command that reloads the file for the next battle, and the plans staying
in C#. Verdict: no DSL.

---

## 5. Recommendation

**Do not rewrite.** The decision layers cost nothing measurable (1.8), every ceiling that limits
the goals is liftable through public seams already in use or one subclass (section 2, last
paragraph), the true floor is the same for every option (closed orders, native agents), and the
observed weaknesses are all in the decision model and its instruments, not in the architecture
that hosts it. The current layer is the right shape: public seams, a wrapped lifecycle, pure
cores. What it lacked was a shared picture, an explicit decision, per-formation keys and a few
engine facts folded in.

### Staged path

Each stage has a gate. The A/B protocol in `docs/features/culture-doctrine.md` is the current
gate; the stages below add to it rather than replace it.

**Stage 0 (done 2026-09-17, same commit as this document):** the self-cancelling gates (3.2 a),
the facing-independent brace (3.2 b), the skirmish reset (3.2 h), the cycle charge's reform
bypass (a stop distance at or under the 30 m contact distance ended the reform on its first
tick), the vanguard's exemption from consolidation (`enforceNotSplittableByAI`), the volley
release when the player takes the archers back, and the doc corrections (3.2 m, n). Gate: the
A/B as written, plus a 150 v 150 cell for Erebor and Mordor that stays on `TwoLineWall`/`Envelop`
for the first two minutes.

**Stage 1: the battle picture (option A).** A per-team immutable `BattlePicture` built once per
tactic tick from cached reads: for every enemy formation, its class, median, velocity,
`CachedMovementSpeed`, movement order enum, active behaviour type, ETA to each of our formations;
for ours, the same plus stance and the ranged-attack reads. Published by reference from the
tactic tick; TAOM behaviours read it through the applier the way they receive `owner` today.
`CavalryThreat` (stage 0) is the first consumer and moves into it. Gate: the Erebor vs Rohirrim
A/B cell extended with a flank charge; the wall must show `Square` before contact.

**Stage 2: weights as decisions.** A per-team `DoctrineCommander` (a pure decision table over
the picture plus the culture's rows and a personality block) chooses the tactic; every
registered tactic, TAOM and wrapped vanilla alike, answers `chosen ? 1 : 0` from `GetTacticWeight`.
`MakeDecision` remains the executor at 5 s; the forced Charge when no enemy remains is kept. The
commander carries memory (last three decisions with timestamps, charges received, casualties per
minute) and the reaction rules of the ranked list below. The ordering test is replaced by a
decision-table test over picture fixtures. Gate: the "preferred set at least 70 percent of
samples" criterion becomes 100 percent by construction; the A/B measures only field behaviour
and frame time.

**Stage 3: per-formation doctrine (Phase E).** Plan rows keyed on the formation's majority
culture (resolved at apply time from the agents' `Character.Culture`, cached per formation and
refreshed on `OnUnitCountChanged`, `Formation.cs:605`), so a Rohan eored inside a Gondor army
runs the cycle charge while the Gondor foot holds the line. Aggression is already per soldier.
Gate: a mixed-army Custom Battle cell.

**Stage 4: the `TeamAIGeneral` subclass (option B), only if the A/B shows the 5 s cadence losing
fights.** The measurement that decides it: log the time between the commander's rule flipping
(from the picture) and the engine's `MakeDecision` acting on it; if the median exceeds what the
reaction rules need (a charge lands in under 5 s), install the subclass and drive `ResetTactic()`
on the commander's own timer.

**Stage 5: sieges.** `TeamAISiegeComponent` and its tactics are a different class family
(`MissionCombatantsLogic.cs:120-132`, `:199-214`); the picture and the commander port, the plans
do not. Later.

### Improvements to the current layer, ranked, with the engine evidence

1. **Class totals in the snapshot** (3.2 a; done). `TacticComponent.cs:268-346` splits after the
   gate read.
2. **A facing-independent charge signal** (3.2 b; done). `FormationQuerySystem.cs:646-663`
   requires our facing; enemy cavalry `CachedCurrentVelocity` is 0.1 s fresh (`Formation.cs:2100`).
3. **The shared battle picture** (3.2 f, e). Built from `FormationAI.ActiveBehavior` and
   `GetReadonlyMovementOrderReference()` of enemy formations (read by vanilla at
   `FormationQuerySystem.cs:632`), which give exact intent: `ChargeToTarget` with a target is a
   committed charge, `Move` toward its own median is a hold, `Retreat` is a rout, a
   `BehaviorSkirmish`/`SkirmishLine` type is skirmishing.
4. **Reaction rules in the tactic tick.** `OnPhaseTick` already runs once a second; rules such
   as "Defend phase, enemy cavalry all committed against the far flank or routed: release one
   `ProtectFlank` block to `CycleCharge`", "enemy infantry `Retreat`: `TacticalCharge` up", "our
   `RemainingPowerRatio` below 0.6 and never-rout culture: `Charge`, else `PullBack`". Each rule
   is a plan-row override, applied through the existing applier.
5. **Weights as decisions** (4B trick). Removes 3.2 c and d; the test becomes a table.
6. **Event-driven volley** (3.2 j). Subscribe `Team.OnFormationAIActiveBehaviorChanged`
   (`Team.cs:419-425`, raised from `FormationAI.ActiveBehavior`'s setter on the AI thread,
   `FormationAI.cs:64-67`) and re-assert `HoldYourFire` in the handler; keep the 1 Hz tick as
   the backstop.
7. **Runtime `TacticalPosition`s for the vanilla scene tactics.** `TeamAIComponent.TacticalPositions`
   is public and mutable (`TeamAIComponent.cs:50`); `TacticDefensiveLine.DetermineMainDefensiveLine`
   and `TacticDefensiveRing.DetermineRingPosition` read it (`TacticDefensiveLine.cs:335-340`,
   `TacticDefensiveRing.cs:399-421`) and accept the runtime constructor's objects
   (`TacticalPosition.cs:105-117`, which vanilla itself uses at `TacticDefensiveLine.cs:272-278`).
   Seeding one `HighGround` position from `HighGroundCloseToForeseenBattleGround` per team at
   `EarlyStart` makes `DefensiveLine`, `HoldChokePoint` (with a `ChokePoint` type) and
   `DefensiveRing` reachable on scenes without entities, which is what Dunland's and the
   goblins' rows assume today.
8. **Per-formation culture keys** (Phase E, 3.2 i).
9. **Commander personality from the lord.** `IBattleCombatant.GetTacticsSkillAmount()` is already
   read; traits (Valor, Calculating) come from the campaign side at `EarlyStart` on the main
   thread through a hero adapter (ADR-007), and feed the commander's thresholds: commit distance,
   race margin, hysteresis, the attrition point at which Charge takes over (`TacticCharge.cs:59-88`
   grows with total battle losses).
10. **A hollow square.** `Formation.Rearrange(new SquareFormation(formation))` after
    `SetArrangementOrder(Square)` (`Formation.cs:2549-2561`, `SquareFormation.cs:48`); depth
    follows the form order (`FormOrder.cs:120-134`, `:237-239`: Wide gives 4 rings at 200 men,
    Wider 2). `IsUnderCavalryChargeFromFront` and `FacingOrder` still see `is SquareFormation`
    (`FormationQuerySystem.cs:656`, `FacingOrder.cs:59`), shield directions still key on the enum
    (`ArrangementOrder.cs:185-195`). Repeated `SetArrangementOrder(Square)` is a `SoftUpdate`
    (`Formation.cs:768-771`) and keeps the instance; any other arrangement order rebuilds
    normally. Test in Custom Battle before relying on it; the archers' formation can then stand
    inside the ring.
11. **Per-culture agent curves.** `HumanAIComponent.OverrideBehaviorParams` (`:144-153`) sets the
    five native simple-behaviour curves per agent; `RefreshBehaviorValues` overwrites them on
    every order change (`:783-815`), so a TAOM override must be re-applied from
    `Formation.OnBeforeMovementOrderApplied` and `OnAfterArrangementOrderApplied`
    (`Formation.cs:613-615`) or accepted as order-scoped. No member named `Agent.SetAIBehaviorValues`
    exists; the extension `AgentComponentExtensions.OverrideBehaviorParams(this Agent, ...)` is the entry.
12. **More morale seams.** `CalculateCasualtiesFactor`, `CalculateMoraleChangeToCharacter`,
    `CalculateMaxMoraleChangeDueToAgentIncapacitated` are abstract on `BattleMoraleModel` and
    implemented in `SandboxBattleMoraleModel.cs:18-31`, `:95-98`, `:122`; per-culture cohesion
    (a formation whose neighbours die loses more, an orc mob loses less) fits there without
    touching the tactic layer.
13. **`TacticalDecision` as the commander's log.** `NotifyTacticalDecision` (`TeamAIComponent.cs:149-152`)
    with the public struct gives the status line and the memory a typed event; nothing in the
    field consumes it today.
14. **Cache-period notes for the race** (3.2 k): compare against `CachedMovementSpeed` (2 s,
    arrangement-aware) for our own ETA when the formation is walking in a wall, and keep
    `MovementSpeedMaximum` for the enemy's.

### Answer to the question in one paragraph

The engine gives a team one tactic every 5 s and a formation one behaviour every 0.5 s, and it
runs both for almost nothing; the real cost of a field battle is the per-agent native AI and the
per-unit cascades an order change triggers, and no team-AI design changes either. TAOM's layer
already sits on the only public seams that matter and already runs the vanilla lifecycle once,
wrapped, on the right thread. Its weaknesses were that it decides by formula competition
instead of by a stated rule, that its behaviours cannot see each other or the enemy's intent,
that its doctrine is per side, and that two of its gates read the wrong count after their own
split. The gates and the brace are fixed; next, give the team one shared picture per tick, make
the decision explicit and let the engine's picker execute it, key rows per formation, and only
then, if the 5 s cadence measurably loses fights, take over `TeamAIGeneral.Tick` with a subclass.
Each kingdom's character is a decision table over that picture plus its aggression and morale
tiers; that grows by adding rules and rows, not by rewriting the stack.

### Status after the first A/B (added 2026-09-17, later the same day)

The first Custom Battle A/B ran (Erebor v Mordor, 300 v 300): 172 to 183 fps at 661 agents, gen 2
collections zero, so section 1.8's cost model held. Its two failures were in the decision model,
as section 3.2 predicted, and both landed the same day (`39e3c8b5`): a foot formation's target is
now a stated rule (`TargetSelection`: nearest infantry, archers only when no infantry is within
1.5x their distance, horse only when no foot is left) rather than the engine's closest-anything,
and the brace is a stated rule over every enemy melee cavalry formation (real size, riding at us
or among us, inside 100 m, released at 150 m) rather than one engine query. That is the first
piece of item 3 (the shared picture) built per formation, and it retired weakness (b) for good
(the engine query is no longer read). Item 14 gained two gates: the high ground is now searched
inside a 60 m cap with the engine's own slope search, and only enemy foot inside 50 m ends the
march. Still open in the order given: the per-team picture (3), reaction rules (4), weights as
decisions (5), the event-driven volley (6), per-formation keys (8). The `TeamAIGeneral` subclass
(4B) is no nearer: nothing in the first battle was lost to the 5 s cadence.
