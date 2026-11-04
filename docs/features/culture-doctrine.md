# Culture Doctrine

## Overview

Each culture's AI fights field battles with its own doctrine. At the start of a field battle the
feature replaces the tactic list the engine's team AI chooses from with the list its culture
authored in `culture_doctrines.json`: the nine vanilla tactics with per-culture weight
multipliers, plus eleven TAOM tactics (a Dwarven shield wall and its two-line variant, an
orcish infantry mass and its envelopment, Rohan's cavalry lead and its defending screen, an
Elven archer ring and archer advance, a disciplined line for Gondor, Rhun and Dale, Dunland's
hit-and-run, Harad's mumakil vanguard) built on seven TAOM formation behaviours (a braced wall
holding and advancing, a foot charge that goes for the enemy foot and squares up against horse,
archers on the flank of the wall, a cavalry cycle charge, a throwing-infantry skirmish, an
enveloping wing). The engine still picks the highest-weight tactic every 5 seconds and every formation
still picks its own behaviour every 0.5 seconds; the doctrine changes what is on the menu and
how much each option weighs. Below the tactics, the same file says who never routs and how
brave each culture starts (the battle morale model), how each culture's soldiers fight the
melee (a per-culture post-pass on the AI decision values), and which named troops spawn into
a formation of their own (the Harad mumakil ride as `HeavyCavalry`). Sieges are untouched.
Issue #608.

## Why This Exists

- **Vanilla behavior:** `MissionCombatantsLogic.EarlyStart` gives every team a `TeamAIGeneral`
  and registers the same tactic set for everyone, gated only by the commander's Tactics skill
  (`Charge` always; `FullScaleAttack` plus `DefensiveEngagement`/`DefensiveLine` (defender) or
  `RangedHarrassmentOffensive` (attacker) at 20; `FrontalCavalryCharge` plus
  `DefensiveRing`/`HoldChokePoint` (defender) or `CoordinatedRetreat` (attacker) at 50,
  `MissionCombatantsLogic.cs:169-196`). Nothing in `TeamAI*`, `Tactic*`, `Behavior*`,
  `FormationAI` or either query system reads `Culture`. A Dwarven company, a Rohan host and a
  Mordor horde with the same composition and the same commander skill fight the same battle.
- **TAOM requirement:** Dwarves defensive and formation-first, Elves disciplined and
  archer-centred, Rohan aggressive with cavalry, Mordor and Gundabad aggressive with infantry
  mass, Dunland cautious on high ground; and a design that grows (per-formation flavour,
  custom behaviours, unit-level aggression) without rework.
- **Without this feature:** every AI army fights like Calradia.

## Architecture

### Design Challenge

Three constraints shaped this, all verified in the v1.5.3 dump on 2026-09-16:

1. **The doctrine layer runs on the team-AI tick, which is usually the async AI thread.**
   `Mission.OnTick` runs every `MissionBehavior.OnMissionTick` on the main thread, then native
   calls back `Mission.TickAgentsAndTeams` on another thread, which runs `Team.Tick` ->
   `TeamAIComponent.Tick` -> `TacticComponent.GetTacticWeight`/`TickOccasionally` ->
   `FormationAI.Tick` -> every `BehaviorComponent` (`.claude/rules/harmony-patches.md`, "Which
   thread runs your target"). The same chain runs on the MAIN thread during deployment
   (`DeploymentMissionController.SetupAIOfEnemyTeam` calls `team.ResetTactic()` and
   `team.Tick(0)`, so the first decision of every field battle is main-thread) and in
   fast-forward; it never overlaps `OnMissionTick`, because `Mission.OnPreTick` waits for the
   previous async tick (`WaitTickCompletion`). That is where vanilla's tactics run, so a TAOM
   tactic runs there too, under the rules the async case imposes: the only state is what the
   engine hands over plus immutable values captured in the constructor; no `IoC.Resolve`, no
   `TaomSettings`, no logger; every override body wrapped, and a throw turns into a failed
   tactic (weight 0; formations keep their last weights until the next `MakeDecision`, which
   then switches unconditionally, because `ResetTacticalPositions` returns false for a TAOM
   tactic as it does for the six position-free vanilla tactics; `DefensiveLine`, `DefensiveRing`
   and `HoldChokePoint` return true and re-check the 1.5x on the way out) rather than an unwind
   into native.
2. **The tactic list is unlocked.** `TeamAIComponent._availableTactics` is a plain `List` read
   every 5 seconds by `MakeDecision`, so it is edited exactly once, in `EarlyStart`, before the
   first `Team.Tick`, and never again. The MCM toggle applies from the next battle.
3. **Multipliers cannot resurrect a zero.** The three vanilla defensive tactics return 0 unless
   `TeamAIComponent.IsDefenseApplicable`, which is false for any non-Defender team
   (`TeamAIComponent.cs:217-221`); `DefensiveLine` and `HoldChokePoint` also need
   `TacticalPosition` scene entities and `DefensiveRing` an insurmountable one. Weighting the
   vanilla set gives Rohan, Mordor, Gundabad and *defending* Dwarves their character; attacking
   Dwarves and the Elven ring needed TAOM tactics.

Two lifecycle facts decide where the entry point hooks in. `Mission.AfterStart`
(`Mission.cs:3814-3839`) runs `OnBehaviorInitialize` for the mission's own behaviors, then
`SubModule.OnMissionBehaviorInitialize` appends TAOM's, then `EarlyStart` and `AfterStart` loop
over the full list in order. So a TAOM `MissionLogic` gets `EarlyStart` after
`MissionCombatantsLogic.EarlyStart` (teams, team AIs, vanilla tactics and `MissionTeamAIType`
all exist) and never gets `OnBehaviorInitialize` (#606). Vanilla's own `ResetTactic` at the end
of that `EarlyStart` returned before choosing anything, because no formation has units yet
(`TeamAIComponent.cs:257`); the swap therefore lands before any decision.

### Solution Approach

```
Main/_Module/ModuleData/culture_doctrine/culture_doctrines.json
        |   per culture StringId: tactic rows (id, multiplier, minTactics, side), plus "default"
CultureDoctrineConfigProvider (validating loader, once per process)
        |
DoctrineCatalog -> Doctrine -> TacticEntry            (immutable domain)
        |
CultureDoctrineMissionLogic : MissionLogic            (entry point, main thread, 134 lines)
   EarlyStart: toggle, Mission.IsFieldBattle (live read), per AI team:
       TeamCombatantSelector (IBattleCombatants of the side) -> SideProfile (majority culture, best Tactics skill)
       -> DoctrineCatalog.Resolve -> TacticRoster.Build (side + skill filter, Charge guaranteed)
       -> TacticFactory.CreateAll -> team.ClearTacticOptions / AddTacticOption / ResetTactic
   OnMissionTick: [Doctrine] status line every 5 s when the debug toggle is on (TeamTacticProbe)
        |
   nine vanilla wrappers   TacticCharge : TaleWorlds.MountAndBlade.TacticCharge   weight = base * multiplier
   eleven TAOM tactics     TaomTacticShieldWall / InfantryMass / CavalryDominance / ArcherRing (TaomTactics.cs) and seven more (TaomTacticsPhaseC.cs) : TaomTacticBase
                              TaomTacticBase = vanilla lifecycle once (split, join test, TacticPhaseMachine, BehaviorWeightApplier)
                              DoctrinePlan (pure weight tables) + DoctrineWeights (pure weight functions over TeamQuerySnapshot)
```

**Vanilla wrappers.** Every field tactic is a public unsealed class with
`protected internal override float GetTacticWeight()`, so a subclass returning
`base.GetTacticWeight() * multiplier` scales the vanilla formula with no Harmony and preserves
the side effects some of those formulas carry (`TacticDefensiveLine.DetermineMainDefensiveLine`,
`TacticDefensiveEngagement` assigning `_mainInfantry`). The wrappers keep the vanilla simple type
names inside `TAOM.Features.CultureDoctrine.Hooks.Tactics`: when the player fights as a
sergeant, `MakeDecision` shows `GameTexts.FindText("str_team_ai_tactic_text", GetType().Name)`
(`TeamAIComponent.cs:330-334`), so the same name resolves the vanilla text. `MakeDecision`'s
forced-charge branch tests `item is TacticCharge` (`:280`), which the subclass satisfies.

**TAOM tactics.** `TaomTacticBase` is the vanilla lifecycle written once
(`TacticDefensiveEngagement.cs` was the template): `ManageFormationCounts` ->
`AssignTacticFormations1121` (or the 1/1/1/1 split of `TacticFrontalCavalryCharge`),
`TickOccasionally` = battle-joined test + formation-set-changed test + the pure
`TacticPhaseMachine` + the phase table through `BehaviorWeightApplier`, and a mandatory
`base.TickOccasionally()`. A tactic is a `DoctrinePlan` (which behaviours each formation role
weights in the Defend and Engage phases) plus a pure weight function over a
`TeamQuerySnapshot`, plus any position it needs computed just before the table is applied. The
weight functions are scaled like vanilla's so the wrapped vanilla tactics beside them compete on
one axis. `TeamQuerySnapshotFactory` is side-effect free, unlike vanilla's
`CheckAndDetermineFormation`: a weight query runs for every registered tactic every 5 seconds,
current or not, and must not reassign another tactic's formation slots.

What each doctrine looks like in the field comes from what the behaviours do with
arrangement: `BracedDefend` goes ShieldWall at its position when the formation has shields
(`HasShieldUnitRatio >= 0.4`, Line without them) and Square under a cavalry charge,
`BracedAdvance` goes ShieldWall under fire and Square under a charge, `BehaviorDefensiveRing`
forms a Circle sized around the archers' `BehaviorFireFromInfantryCover` Square,
`BehaviorVanguard` rides ahead of the infantry, `CycleCharge` charges through and reforms,
`EnvelopWing` walks round a flank in a deep line, `InfantrySkirmish` throws and falls back.

| Tactic | Cultures | Plan | Weight |
|---|---|---|---|
| `TaomTacticShieldWall` | `erebor`, `erebor_warriors`, `eredluin`, `isengard`, `sturgia` (Dale), `anduinvale`, `angaladh`, `dorwinion` | Defender: infantry `BracedDefend` at the position the high-ground race picks (below) with a low `TacticalCharge`; cavalry `ProtectFlank` + `CavalryScreen` in both phases, never `Flank`. Attacker: infantry `BracedAdvance` with `TacticalCharge` 0.5 then 0.8; cavalry may take a flank once joined | `(Inf + Rng) * 1.2 * advantage / sqrt(RemainingPowerRatio)`, not gated on `IsDefenseApplicable` |
| `TaomTacticTwoLineWall` | `erebor`, `erebor_warriors`, `eredluin` (defender only) | 2/1/2/1 split: the front line as the wall, the second line `BracedDefend` twelve paces behind it (facing the enemy) until joined, then `TacticalCharge` 1 over `BracedDefend` 0.5, the reserve committing | the wall's weight times 1.6 (`GatedEdge`), 0 under 80 infantry |
| `TaomTacticInfantryMass` | `mordor`, `dolguldur`, `gundabad`, `gundabad_raiders`, `mistymountainorcs`, `goblin`, `feral_orcs`, `dunland_raiders` (attacker) | Always engaged: infantry `Charge` 1.5 + `TacticalCharge` 1, archers `Skirmish`, cavalry `TacticalCharge` + `Flank` | `1.5 * Inf * clamp(members / enemies, 0.5, 2) * sqrt(RemainingPowerRatio)` |
| `TaomTacticEnvelop` | the seven orc cultures (`InfantryMass`'s list without `dunland_raiders`) | 3/1/2/1 split, always engaged: centre `Advance` + `TacticalCharge`, left and right wings `EnvelopWing` (a deep line to a point beside the enemy body on its own side, then `ChargeToTarget`), archers `Skirmish`, cavalry `TacticalCharge` + `Flank` | the mass's weight times 1.6 (`GatedEdge`); 0 under 1.2x numbers or 60 infantry |
| `TaomTacticCavalryDominance` | `vlandia` (Rohan), `khuzait` (Rhun), `battania` (Khand), `southrhovanion` | 1/1/1/1 split, 7 s join threshold: cavalry `Advance` + `Vanguard`, then `CycleCharge` 1.2 + `TacticalCharge` 0.8 + `Flank` 0.8; infantry `Advance` behind | vanilla `FrontalCavalryCharge` formula times 1.3 |
| `TaomTacticEoredScreen` | `vlandia`, `southrhovanion` (defender only) | 1/1/2/1 split, 7 s join: both cavalry blocks `CavalryScreen` + `ProtectFlank`, infantry `Defend` where the race puts it; once joined the blocks `CycleCharge` 1.2 + `Flank` 0.8 + `ProtectFlank` 0.5 | `Cav * 1.6 * advantage / sqrt(RemainingPowerRatio)`, defender only |
| `TaomTacticArcherRing` | `lindon`, `lothlorien`, `mirkwood`, `mirkwood_stalkers`, `rivendell` (defender only) | infantry `DefensiveRing` on a runtime `TacticalPosition` at the position the high-ground race picks (the archers' high ground, or where the infantry stands), facing the enemy; archers `FireFromInfantryCover` + `Skirmish` 0.5 under volley control; cavalry guards the flanks; the ring holds in both phases | vanilla `DefensiveRing` formula (`min(Inf, Rng) * 3 * advantage / sqrt(RemainingPowerRatio)`), 0 for an attacker, when out-shot (`IsDefenseApplicable`), or when the infantry cannot ring the archers' square (`RingGeometry.Fits`, vanilla's radius test) |
| `TaomTacticArcherAdvance` | the five Elven cultures (attacker only) | infantry `CautiousAdvance` behind the archers' `SkirmishLine` + `ScreenedSkirmish`, cavalry `ProtectFlank` + `CavalryScreen`; once joined infantry `Advance` + `TacticalCharge` 0.7, archers `ScreenedSkirmish` + `Skirmish`, cavalry may `Flank` 0.7; volley control throughout | `min(Inf, Rng) * 2 * sqrt(RemainingPowerRatio)`, attacker only |
| `TaomTacticDisciplinedLine` | `gondor`, `gondor_soldiers`, `khuzait`, `sturgia`, `anduinvale`, `angaladh`, `dorwinion` | Defender: infantry `Defend` where the race puts it, archers `SkirmishLine` in front, cavalry `ProtectFlank` + `CavalryScreen`; attacker: `CautiousAdvance` instead of `Defend`. Once joined: infantry `Advance` + `TacticalCharge` 0.8 + `Defend` 0.5, archers `ScreenedSkirmish` + `Skirmish`, cavalry `TacticalCharge` + `Flank` + `ProtectFlank` 0.5 | `(Inf + Rng) * 1.5 * advantage / sqrt(RemainingPowerRatio)` |
| `TaomTacticHitAndRun` | `empire` (Dunland), `dunland_raiders` (attacker only) | Always engaged: infantry `InfantrySkirmish` 1 + `TacticalCharge` 0.7 + `Advance` 0.5 (the skirmish weighs 0 once the javelins are spent, so the charge takes the line), archers `Skirmish`, cavalry `Flank` + `TacticalCharge` 0.8 | `Inf * 1.4 * clamp(throwing share / 0.5, 0, 1) * sqrt(RemainingPowerRatio)` |
| `TaomTacticMumakVanguard` | `aserai` (Harad, attacker only) | 1/1/2/1 with the team's `HeavyCavalry` formation kept apart as the vanguard: it rides ahead (`Vanguard` + `Advance` 0.8), infantry `Advance`, archers screened, cavalry guards; once joined the vanguard `TacticalCharge` 1.2 + `Charge`, infantry `Advance` + `TacticalCharge`, cavalry `TacticalCharge` + `Flank` | `(Inf + Cav) * 1.4 * sqrt(RemainingPowerRatio)`, 0 without a routed vanguard formation |

The runtime `TacticalPosition` is what `BehaviorDefensiveRing` reads (position and direction
only); vanilla's `TacticDefensiveRing` constructs them the same way (`TacticDefensiveRing.cs:180`),
so no scene entity is needed.

**The high-ground race.** Vanilla never asks whether there is time to get to the high ground:
`TacticDefensiveEngagement` only lowers its weight when it is far and `BehaviorHoldHighGround`
tracks then locks, so a wall that cannot make it is caught on the march. `HighGroundAnchor`
(shared by the wall and the ring) decides like a captain: our foot's travel time to the high
ground at `MovementSpeedMaximum`, plus a form-up allowance (`RaceTunables`: 6 s + 0.03 s per man
for a line, 10 s + 0.04 s per man for a ring) plus a margin (3 s / 4 s), against the earliest
arrival of any enemy infantry or archer formation at that spot (cavalry is not a racer: a wall
that forms late still receives a charge in a wall). Win the race and the formation marches;
lose it and it forms where it stands (`BehaviorDefend` at its own position). The ground itself
is no longer the engine's `HighGroundCloseToForeseenBattleGround`: that query searches a square
whose half-side is half the distance to the enemy's median formation (`FormationQuerySystem.cs:637-643`),
so at deployment range it names the biggest slope 150 m away and never the knoll 30 m off, which
is what the first A/B showed (`Marching` at 95 s with Mordor's horse on the dwarves).
`HighGroundAnchor.HighGroundOf` calls the same engine search
(`Mission.FindPositionWithBiggestSlopeTowardsDirectionInSquare`) on a square whose half-side is
`highGroundMaxMetres / sqrt(2)` (60 m), so every candidate is inside the cap; the race then
decides. One more gate before the race (`HighGroundRace.WorthGoing`): no enemy FOOT formation
inside `holdWhenEnemyWithinMetres` (50 m); horse do not end a march, the wall squares up
against them where it is, and a scout eored passing at 45 m must not lock the wall for the
battle. `Holding` is terminal on purpose: a wall that formed where it stood does not later
march off to a hill. While marching the race and the gate are re-checked once a second on the
tactic's tick and a lost race re-forms on the spot;
once arrived (within `BehaviorDefend`'s 10 m), or once the closest enemy is inside
`BehaviorHoldHighGround`'s lock radius (`max(0.8 * archers' missile range, 30 m)`), the position
is locked. All of it is `HighGroundRace` (pure, `HighGroundRaceTests`) over eight cached engine
reads; the status line shows the state (`TaomTacticShieldWall:Defend:Marching|Holding|Arrived`). Cultures without a row (`shaghana`, `abanissa`, `bluecraig`, `arthedain`, `cardolan`, `rhudaur`, the bandit cultures `harad_raiders`, `rhun_raiders`, `umbar_corsairs`, `mirkwood_spiders` and `wild_trolls`, and the looters) use `default`, which is exactly vanilla's set at multiplier 1.

**Per-side culture.** `MissionCombatantsLogic.GetAllCombatants()` is public and yields the
campaign's `MapEvent.InvolvedParties` or Custom Battle's two `CustomBattleCombatant`s.
`TeamCombatantSelector` hands `SideProfile` every combatant whose troops fight on the team. A
side normally owns one team; when the engine created `Mission.PlayerAllyTeam`, a combatant
belongs to it by the engine's own per-troop rule (`Mission.GetAgentTeam`, `Mission.cs:5233-5240`:
neither under the player's command nor in the player's army), and both teams register with the
side's best Tactics skill, as vanilla does (`MissionCombatantsLogic.cs:169`). `SideProfile` takes
the culture fielding the most troops. The culture a combatant reports is the party's map-faction
culture (`PartyBase.BasicCulture` -> `MapFaction.Culture`): a Dunlending clan sworn to Isengard
fights as Isengard, and recruited troops of another culture do not count; the agent-level read is
Phase E. A mixed army fights under its majority culture.

**One list, many writers.** `ClearTacticOptions` discards everything, including the
`TacticDefensiveLine` that vanilla's `MissionCaravanOrVillagerTacticsHandler.EarlyStart` gives a
caravan or villager side regardless of skill. `CaravanTacticsRule` mirrors that handler's
condition (a caravan party on the side, or a villager party with no settlement involved, in the
player's map event) and `TacticRoster.Build` keeps the row at neutral weight.

**Player team.** `FormationAI.Tick` applies orders only when `Formation.IsAIControlled`
(`FormationAI.cs:245,286`), so the player's own orders are untouched. `Team.DelegateCommandToAI`
(F6) sets every formation AI-controlled and applies the active behaviour at once, so the
player's army fights with its culture's doctrine the moment command is delegated. As a sergeant,
only the formation the player captains is exempt.

**Custom Battle.** `CustomBattleCombatant.GetTacticsSkillAmount` is the roster's maximum Tactics
skill, 0 for TAOM troops, so vanilla registers only `Charge` there. The doctrine registers its
own set, gated per row by `minTactics`; TAOM tactics ship at 0.

**The handover to Charge is total attrition.** `TacticCharge.GetTacticWeight` sums remaining
formation power and casualty power loss over the team AND every enemy team
(`TacticCharge.cs:66-79`), so its casualty term grows with the whole battle's losses, not the
doctrine side's. At full strength it is `1.6 * (maxRatio + 0.33 if defender) * (0.165 defender |
0.25 attacker)`, and it rises toward `1.6 * maxRatio` as the field empties: a wall or a mass that
out-weighs it at the start yields to a charge once the battle is largely decided, which is the
intended "commit when they break".

**The multipliers are tested, not felt.** `MakeDecision` picks `MaxBy(weight * 1.5 if current)`,
so a doctrine tactic is only as real as its margin over the rows beside it. The first authored
file had Rohan's `FrontalCavalryCharge*2.0` above `CavalryDominance` (the same formula times
1.3) and the Elven `DefensiveEngagement*1.5` above the ring. `ShippedDoctrineOrderingTests`
reproduces the nine vanilla formulas at their nominal scores (`VanillaTacticWeightReference`,
with line citations) and asserts, for every shipped TAOM row, that it beats every vanilla row it
shares a list with by the 1.5x sticky factor at the culture's canonical army. Retune the JSON to
the test, not the test to the JSON.

**TAOM behaviours.** Seven `BehaviorComponent`s on a shared `TaomBehaviorBase`
(`Hooks/Behaviors/`), the same wrapped lifecycle the tactics have: `GetAiWeight` ->
`Weigh()`, `CalculateCurrentOrder` -> `Plan()`, `OnBehaviorActivatedAux` -> `Activate()`,
`TickOccasionally` -> `OnActiveTick()` then `Plan()` then the orders pushed to the formation,
`OnBehaviorCanceled` -> `Canceled()`; a throw sets `Failed` and the weight is 0 from then on.
`FormationAI.TickOccasionally` calls `PrecalculateMovementOrder` (our `Plan()`) on any
candidate whose provisional weight beats the running maximum (the active behaviour is scanned
first with its bonus, `FormationAI.cs:169-210`) and ticks only the active one (`:240-256`), so
`Plan()` reads a stage and writes orders while stages advance in `OnActiveTick()` alone.
A TAOM behaviour derives from `BehaviorComponent` directly, never from a vanilla concrete
type: `GetBehavior<T>` and `SetBehaviorWeight<T>` match with `is T` (`FormationAI.cs:120-155`).
The engine registers 24 behaviours per formation at spawn (`TeamAIGeneral.cs:63-86`) and never
ours, so `BehaviorWeightApplier.Ensure<T>` adds a TAOM behaviour to a formation the first time a
plan names it (`GetBehavior<T>() ?? AddAiBehavior`), on the same team-AI tick that reads the
list. A full scene reset (`Team.Reset`, a Custom Battle restart) rebuilds every `FormationAI`
and drops the instance with its stage; the next apply adds a fresh one.

**Who a foot formation goes for.** Vanilla's `BehaviorCharge` and `BehaviorTacticalCharge` run at
`CachedClosestEnemyFormation` whatever its class (`BehaviorCharge.cs:18`,
`BehaviorTacticalCharge.cs:62,151`), and the first A/B (2026-09-17) showed what that means: a
passing eored is usually the nearest thing, so the whole line turns and chases horse it will
never catch, sometimes instead of the infantry in front of it. Every TAOM foot behaviour picks
its target through `TargetSelection` (pure) fed by `EnemyScan` (one walk over every enemy
team's formations, their 5 s class flags, cached medians and velocities; exactly one of the four
class flags is true for a formation with units, `FormationQuerySystem.cs:440-445`; the walk
covers all ten slots, `FormationsIncludingSpecialAndEmpty`, as vanilla's own closest-enemy
cache does, so a lord's bodyguard is a formation too): the target is the nearest enemy
infantry, or the nearest archers when no infantry stands within 1.5 times their distance (a
retreating archer block is a slower eored chase); horse are a target only when no foot is
left. Horse are a threat only when they are melee cavalry (horse archers wheel at range and
never charge; a square under arrows is the wrong stance), a real formation (5 riders and a
tenth of our number: a wall of 150 does not re-form for one rider), and either riding at us
(`CavalryThreat.IsInbound`, the engine's cosine and 15 s horizon) or already among us (inside
15 m whatever their velocity: a formation in the melee has none), inside `cavalryMattersMetres`
(100 m, about the 8 to 12 s a line needs to become a square at horse speed; 40 m was three
seconds). The line answers a threat by squaring up where it stands, never by turning, and keeps
the square until the horse are beyond 1.5 times the distance plus a 3 s hold, so a formation
cycling at the edge does not flip it. The engine's `IsUnderCavalryChargeFromFront` is no longer
read: it has no distance and sees one formation. `BehaviorFootCharge` replaces `BehaviorCharge`
and `BehaviorTacticalCharge` in every foot row of every plan (`DoctrinePlansTests` fails a foot
row that names either), and its row also zeroes the engine's default `BehaviorCharge`, which
`SetDefaultBehaviorWeights` arms at 1 on every apply and which charges the closest formation of
any class (`DoctrineSwitchInvariantTests` pins the IL); the cavalry rows keep vanilla's. The
wing and the javelin line take the target rule but not the brace: they are the flank and the
skirmish, not the wall.

| Behaviour | Weight | What it does | Engine reads |
|---|---|---|---|
| `BehaviorBracedDefend` | 1 | `BehaviorDefend` with the anti-cavalry square: walks loosely to `DefensePosition`, faces its foot target (the horse while braced), closes into ShieldWall on arrival (Line without shields, Loose under fire at a distance), and goes Square while horse ride at it inside `cavalryMattersMetres` or did in the last 3 s, standing on the point where the brace began. Arrangement is re-issued only on a change of stance (`SetArrangementOrder` expires the query cache on every real change) | enemy class flags, `CachedCurrentVelocity` and medians, `HasShield`, `UnderRangedAttackRatio`, `CachedClosestEnemyFormation` |
| `BehaviorBracedAdvance` | 1 | `BehaviorAdvance` with the square: a line at its foot target's main body (its median plus half its depth), ShieldWall or Loose inside vanilla's 10 to 80 m band under fire, Square and stand (on the brace point) while horse are on it; vanilla only stops and re-forms five metres on when the horse is 30 m out | the same, plus `IsUnderRangedAttack`, `MedianTargetFormation` as the fallback |
| `BehaviorArcherFlank` | 1 with a wall, 0 without | Archers on the flank of the main infantry (`ArcherFlankGeometry`): beside the wall's order position, a 10 m gap out from its end and 8 m back, the side chosen once where fewer enemy formations stand, Loose, fire at will, facing the enemy. While an enemy formation is inside 30 m of the archers and closer to them than to the wall they stand 15 m straight behind the wall, and return once it is beyond 45 m. The players' answer to cavalry and to bows caught in the open (Mike, 2026-09-17): the enemy AI goes for the closer wall and the bows keep a clear line, instead of vanilla's skirmish ahead of the wall and back through it. The wall is set by the tactic (`owner.MainInfantry`); every wall doctrine's archer row leads with it, the skirmish rows at 0.6 as the fallback for a wall that is gone | enemy medians, `Team.AverageEnemyPosition`, the wall's `OrderPosition`, `Width`, `Direction` |
| `BehaviorFootCharge` | vanilla's infantry charge curve against its own target (`ChargeWeight`: 0.8 ten seconds out, 1 at four, times 1.2 inside the 1.5 to 4 s window, times 1.2 when the target is not coming for us, half against horse; the navmesh slope term is dropped), 0.2 with an enemy but no target. Against `FormationAI`'s 1.2x hold on the active behaviour a row at 1 wins only close and against a target busy elsewhere (1.44), so the Engage rows sit at 1 and the Defend rows at 0.3 | `BehaviorCharge` with the target from `TargetSelection`: `ChargeToTarget` the nearest enemy foot formation, the nearest horse when no foot is left, vanilla's plain charge when nothing is known; Square and stand while horse are on it, then charge on. Every foot row's charge | enemy class flags, medians and velocities |
| `BehaviorCycleCharge` | 1 for cavalry with an enemy, else 0 | `CycleChargeMachine`: `ChargeToTarget` the closest significant enemy (Skein); once the formation's average has passed through it, or after 10 s in the melee, ride clear to a reform point beyond the enemy at the charge's stop distance (35 to 60 m; vanilla's 20 to 50 m was written for infantry); gather in a Line facing it (2 s minimum, 8 s maximum, or sooner if the enemy comes within half the stop distance); charge again. It is the machine `BehaviorTacticalCharge` carries and short-circuits for cavalry (`BehaviorTacticalCharge.cs:149-153`). The navmesh penalty is off, as vanilla's charges have it | `CachedFormationIntegrityData` (gathered = deviation under half the average top speed), positions, velocity |
| `BehaviorInfantrySkirmish` | `0.1 + 0.9 * min(throwing share, 0.5) * 2`, 0 once spent | `InfantrySkirmishMachine`, `BehaviorSkirmish`'s dance with vanilla's thresholds: approach to the longest throw, throw (Loose), fall back from foot inside 40 percent of the average throw, and once nobody has thrown for the can't-shoot window (5 to 10 s by size) while inside 60 percent of the average throw, `Committed`: the behaviour weighs 0 and the plan's melee rows take the line. `Committed` survives a plan re-apply (`ResetBehaviorWeights` calls `ResetBehavior` on every behaviour, and a formation-set change elsewhere on the team re-applies the plan): javelins do not come back. The line it dances with is its foot target | `HasThrowingUnitRatio`, `MaximumMissileRange`, `MissileRangeAdjusted`, `MakingRangedAttackRatio`, `CachedCurrentVelocity` |
| `BehaviorEnvelopWing` | 1 with an enemy in view | A deep Line to the point beside its foot target's body on the wing's side of the axis from our main formation (`EnvelopGeometry`: half of each width plus 6 m, level with the enemy median), then `ChargeToTarget` within 25 m of the point or 15 m of the enemy. Vanilla's `BehaviorFlank` weighs 0 whenever the enemy's closest formation is the flanker (`BehaviorFlank.cs:51-64`), which for infantry wings is most of the march | `MainFormation`, widths, `Formation.AI.Side` (Left or Right, set by the tactic) |

**Volley control.** The Elven plans (ring and advance) carry `VolleyTunables`: the archers
hold their fire until the closest enemy is inside 80 percent of the formation's average
adjusted missile range and loose until it is back beyond 95 percent (`VolleyDecision`). The
firing order is owned by the tactic (`VolleyControl`), re-asserted once a second on its tick
because every vanilla archer behaviour resets fire-at-will when it activates, released when the
tactic is cancelled or the archers change hands, and never written for a player-controlled
formation except once: a formation the player takes back while it holds fire gets fire-at-will
on the next tick, so the player never inherits a hold. The status line shows `Hold` or `Loose`.

**Mission scope.** The tactic tier is field battles only (`Mission.IsFieldBattle`, read live at
`EarlyStart`). The morale and aggression tiers are per soldier and follow the soldier into
every mission the models serve: sieges, sally-outs, hideouts, arenas and tournaments, naval.
That is deliberate: a Dwarf's pride and an orc's hatred do not stop at a wall, and the two
model seams run wherever the engine asks them; the A/B measures them in a field battle because
that is where the rest of the doctrine is.

**Morale.** `TaomBattleMoraleModel` (campaign, over `SandboxBattleMoraleModel`) and
`TaomCustomBattleMoraleModel` (Custom Battle, over `CustomBattleMoraleModel`) override the two
seams the engine asks per soldier: `CanPanicDueToMorale`, which `CommonAIComponent.CanPanic`
asks from the worker-thread tick before raising the panic flag (`CommonAIComponent.cs:174-176`),
and `GetEffectiveInitialMorale`, asked once at spawn (`:69-75`, base 35 plus a random 0 to 29,
then the model, then a clamp to 15..100). A culture with `neverRout` answers false to the
first (vanilla's own answer stays the gate, so the Loyalty and Honor perk still holds), and
`bravery` (-30..30) is added in the second. Mike's rule (2026-09-16): Gundabad, Isengard and
Dol Guldur never rout (their hatred of men and elves is too great), Dwarves and Elves never
rout (pride), Rohan, Gondor, Mordor, Rhun, Harad, Dale, Dunland and the rest rout. A
never-rout culture also carries no `CoordinatedRetreat` row, so neither the man nor the team
breaks. `ICultureMoraleService` behind the models is a dictionary lookup behind a bool: the
panic seam runs on the TWParallel workers.

**Aggression.** After the base model has derived a soldier's AI decision values from skill
(`AgentStatCalculateModel.SetAiRelatedProperties`, `AgentStatCalculateModel.cs:165-229`),
`AgentAggressionApplier` scales them by the culture's `CultureAggression` profile:
`attack` on `AIAttackOnDecideChance` (clamped to the engine's 0.05..1), `shield` on
`AiDefendWithShieldDecisionChanceValue` (0..2) and `AiUseShieldAgainstEnemyMissileProbability`
(0..1), `shooterError` on `AiShooterError` and the four ranger error terms (sign kept),
`chargeDistance` on `AiChargeHorsebackTargetDistFactor`. The campaign slot is
`TaomAgentStatCalculateModel` (CareerSystem, one slot, four rules); Custom Battle gets
`TaomCustomBattleAgentStatCalculateModel`. `Agent.Defensiveness` is not touched: the formation
writes it from the movement and arrangement orders (`Formation.cs:2842`). Runs wherever
`Agent.UpdateAgentProperties` runs: the main thread at spawn, and the async AI thread when a
formation's order changes its defensiveness (the formation re-issues it for every man,
`Formation.cs:2838-2844`), so it is nine multiplies over immutable data behind a bool.

**Formation routing.** `FormationRoutingSubscriber` subscribes
`Mission.GetAgentTroopClass_Override` at `EarlyStart` when any doctrine routes a troop; the
engine asks it for every spawned troop from `MissionAgentSpawnLogic.OnMissionTick`, after every
`EarlyStart`, so it is in time for the first spawn. The subscriber REPLACES the engine's body
(`Mission.cs:2555-2567`), so `FormationRoutingRule` reproduces the dismount rule for sieges,
naval battles and a sally-out's attackers and applies it to a routed class too. Vanilla's
tactics fold every class back into 1/1/2/1 through `ManageFormationCounts`, and so would a TAOM
tactic without a vanguard slot, so `RoutedFormationGuard` marks each routed formation
`enforceNotSplittableByAI` at install (`Formation.SetControlledByAI(true, true)`,
`Formation.cs:326-347`): `SplitFormationClassIntoGivenNumber` skips a formation that is not
`IsAIOwned` (`TacticComponent.cs:243`), which is the engine's own exemption from consolidation
and is read by nothing else in the field. The routed block therefore survives whichever tactic
is current; under a vanilla tactic it is picked as a cavalry slot like any other, under
`MumakVanguard` it is the vanguard. An EMPTY routed formation is a different case: the engine's
split takes any empty formation as a transfer target before it asks the predicate
(`TacticComponent.cs:229-259`), so the vanguard split falls back to the plain 1/1/2/1 whenever
the vanguard formation holds no troops. On the player's team the guard holds until the player
delegates (F6 clears the flag, `Team.cs:479-485`). The handler is removed at mission end.

### What the plan rejected, and why

- Harmony postfixes on the nine `GetTacticWeight` methods: the wrappers do the same with no
  patch category and no per-call lookup on the AI thread.
- A `TeamAIGeneral` subclass swapped in with `Team.AddTeamAI`: not needed for this slice. (An
  earlier version of this bullet said behaviours registered from
  `OnUnitAddedToFormationForTheFirstTime` miss formations populated by `TransferUnits`; they do
  not: `TransferUnitsAux` sets `Agent.Formation`, whose setter calls `AddUnit`, which fires the
  hook on the 0 to more-than-0 transition, `Formation.cs:2216`, `Agent.cs:1128-1180`,
  `Formation.cs:2324-2326`. The real trade-offs are in
  `docs/reviews/analysis-battle-ai-2026-09-17.md`, option B.)
- Per-agent behaviour trees: the wrong layer (`docs/reviews/rca-warg-clip-on-horse-2026-09-13.md`).
- `ITeamAdapter` and a formation command adapter: nothing needs them yet; the tactics take a
  pure snapshot and the applier is one switch.
- Driving SmartCavalryAI's `ICavalryChargeService` for AI cavalry (the first idea for the cycle
  charge): that machine is stepped from `OnMissionTick` for the player's team; a formation
  behaviour lives on the AI thread, so the cycle is its own pure machine
  (`CycleChargeMachine`), the one vanilla already wrote for `BehaviorTacticalCharge`.
- A separate `Pike` behaviour for the Uruks: the braced wall already picks Line without
  shields and Square under horse, which is the pike hedge; the only difference was the calm
  stance, and `HasShield` decides that.
- A forest-ambush tactic: `TacticDefensiveLine` already scores forest `TacticalRegion`s
  (three `Regional` positions per region, a cavalry factor that prefers the trees against a
  horse-heavy enemy, `TacticDefensiveLine.cs:266-296`); Mirkwood and Dunland raise its
  multiplier instead.

## Horse stand

Issue #892. A defending AI army that is outnumbered by an army of mostly horse stops marching
for a hill the horse will beat it to, stands on ground that breaks a charge, and, where its
riders have ordinary horses, takes them off their mounts and into the shield wall. It is not a
new tactic, a Harmony patch or an adapter: the shield wall and the two-line wall (the only
tactics already built on `BracedDefend`) read a gate, `HighGroundAnchor` gets a stand mode, and
the base tactic issues one order. `BracedDefend` already goes Square while horse ride at it and
ShieldWall otherwise, and it needs no mount and no shield to brace (a shieldless unit takes the
Line), so the infantry row is unchanged.

**Who stands.** A culture opts in with a `horseStand` block of its own. A culture with no row at
all resolves to `default`, which has no block, so it never stands (the looters, for one); a culture
with a row but no `horseStand` block uses its own row and never stands. A bandit culture can
still opt in: `erebor_warriors` is one, with its own row and stand block. Shipped:

| Culture | `dismount` | Why |
|---|---|---|
| `erebor` | false | every mounted troop rides a war ram |
| `erebor_warriors` | false | no mounted troops |
| `eredluin` (Ered Luin) | false | no mounted troops; Erebor's row, copied whole |
| `isengard` | false | every mounted troop rides a warg |
| `sturgia` (Dale) | true | ordinary horses |
| `anduinvale` | true | Dale's row, ordinary horses |
| `angaladh` | true | Dale's row whole; no Angaladh troop rides |
| `dorwinion` | true | Dale's row whole; its river riders sit ordinary horses |

Gondor and the Elven ring do not adopt it. The stand is armed at install by the pure
`HorseStand.PolicyFor(enabled, culture, isDefender, isPlayerTeam)`: the MCM switch on, the team
defending and not the player's own (`Team.IsPlayerTeam`), else no policy. The player's AI ally team
stands; the player's own team never does, so Enlistment and sergeant mode never stand either.

**The gate** (`Doctrines/HorseStand.Applies`, pure): the side defends, has infantry, the enemy's
melee cavalry share is at least `enemyCavalryShare`, and the enemy has more men than our whole
side times `outnumberedRatio`. The comparison is side against side: `AllyUnitCount` is
`TeamQuerySystem.AllyUnitCount`, which includes this team (`TeamQuerySystem.cs:240-249`), against
`EnemyUnitCount`, so an AI team of 50 with 250 allies against 200 enemies is not outnumbered. The
share is `TeamQuerySystem.EnemyCavalryRatio`, which counts melee cavalry only and which the engine
caches for 15 s, so a verdict can lag a charge by that long (the cavalry share lags up to 15 s
behind the field). It is checked at every phase apply and once a second in `OnPhaseTick`. Every
comparison is a positive requirement, so a NaN or infinite ratio fails the gate. Only the Defend
phase starts a stand.

**Who else reads the stand flag.** Fell Foes (#893, [fell-foes.md](fell-foes.md)) reads it through `TeamTacticProbe.IsHorseStanding`, which returns `TaomTacticBase.Standing` and is false for a failed tactic. Its archer rule skips every archer on a team whose stand is running, because this feature owns them then. The read is a plain bool written on the team-AI thread, so a stale read costs one second.

**The latch.** The first Defend apply in which the gate passes starts the stand
(`TaomTacticBase.RefreshStand`), and from then on it holds: neither the gate flickering nor the
Defend/Engage flip ends it (the Engage phase issues no further dismount order, but the order given
stays and the anchor keeps its ground). Without the latch the anchor reset at every flip and the
foot-only race re-picked ground at contact. The stand ends only when the tactic is cancelled
(`OnCancel`, another tactic takes the team). The weight edge below stays gate-driven, so the wall
still yields the moment the gate fails and the engine's decision can hand the team to another
tactic; that cancel is what releases the stand.

**The release.** At cancel the team's formations are swept (`Team.FormationsIncludingEmpty`, the
eight regular formations, empty ones included): every AI-controlled formation still holding
`RidingOrderDismount` is set to `RidingOrderFree`, and the anchor leaves stand mode (`EndStand`),
so a re-picked wall starts clean and evaluates the gate again. Free never remounts anyone (the AI
seeks a loose mount only under a Mount order, `HumanAIComponent.cs:267`). The sweep covers empty
formations because an emptied cavalry formation would keep the order and hand it to reinforcements
joining it (a unit added to a formation takes its riding order, `Formation.cs:2322`). The result
for the player: cavalry that arrive during the stand dismount into it; cavalry that arrive after it
ride. A formation that is not AI-controlled (a remote co-op human) is skipped by both the dismount
and the release.

**Weight.** The stand army (the A/B's: 200 men, the enemy at 1.5 times that with 60% melee
cavalry) is pinned in `ShippedDoctrineOrderingTests`. Without an edge the wall lost it: Dale's
disciplined line weighs 1.35 against the wall's 1.08, vanilla's choke hold 0.88 (Isengard) to
1.05 (Dale), and the wall must beat the best of them by the 1.5 that `TeamAIComponent.MakeDecision`
grants the current tactic. `HorseStand.StandEdge` (2.0) multiplies the wall's weight while the gate
passes and drops with it, so the wall yields the moment the gate fails. At 2.0 the wall has a 6%
margin on Dale's disciplined line and 20% or more on the rest. Vanilla's choke hold grows with the
enemy count (clamped at 3 times), so a far more lopsided battle on a map with a placed choke can
still go to it; that is vanilla also holding a choke, and it is left alone.

**Ground** (`Doctrines/StandGround.Pick`, pure; scan in `Hooks/Tactics/StandGroundScan.cs`).
Candidates in preference order: a choke point, forest, the existing slope point, where the wall
is. The nearest candidate of the first kind that qualifies wins. A candidate qualifies when it is
inside `engagement.highGroundMaxMetres`, no enemy foot is inside
`engagement.holdWhenEnemyWithinMetres` (the existing `HighGroundRace.WorthGoing`), and the wall
can reach it and form up before any enemy formation (`HighGroundRace.Decide`). Unlike the other
users of the anchor, the race counts enemy melee cavalry; horse archers never race for ground.
Reach time is the straight line over top speed, optimistic for the horse on a winding path, so a
march that wins on paper can still be contested; path distance is left out. The race itself is
one pure composition, `HighGroundRace.CanTake`, used by the picker and by the anchor's once-a-second
re-check while marching.

- Chokes: `TeamAI.TacticalPositions` of type `ChokePoint`. Every tactical position of the scene,
  region children included, is in that list (`TeamAIComponent.cs:96`, registered in
  `MissionObject.cs:121-143`), so there is no second walk over regions. Eligibility keeps vanilla's
  width and facing tests (`TacticHoldChokePoint.cs:145-154`) made pure: the facing dot with the
  enemy at least 0.5 (its absolute value when the position is insurmountable) and a width no
  greater than the infantry's `MaximumWidth`. Vanilla's distance gate (`:139-144`) is replaced by
  the march cap and the race.
- **Choke hold, vanilla's way.** `Pick` returns the chosen candidate and the pure
  `StandGround.HoldOf` gives how to hold it, from `BehaviorDefend.cs:20-34, 65-78`: a choke that can
  be crossed is held at its point, facing its own direction, with the frontage set to its width; an
  insurmountable one is held half a width toward the enemy, with the frontage set to its width, and
  faces the live enemy (`FacingOrderLookAtEnemy`, as vanilla does). Deliberate departure from
  vanilla: the hold point is fixed at decision time, where vanilla recomputes it every tick from the
  enemy centre; a wall that kept sliding toward the enemy would leave the gap. `Pick` qualifies each
  choke (cap and race) at that same hold point, which the anchor's once-a-second re-check also
  races. The anchor exposes `Facing`, `Width` and `LookAtEnemy` (zero or false for anything but a
  choke), the two wall tactics copy them into `DefenseFacing`, `DefenseWidth` and `DefenseLookAtEnemy` in `BeforeApply`,
  and `BehaviorWeightApplier` hands them to `BehaviorBracedDefend` (the second line gets the facing
  only, width 0: it stands behind the gap, not in it). The behaviour faces `DefenseFacing` (or the live enemy), issues
  `FormOrder.FormOrderCustom(width)` every tick while it is at its position (as `BehaviorDefend`
  does, because a later arrangement change makes the engine rewrite a custom width), and never goes Square while the
  width is above zero (`BraceDecision.Defend(..., holdingChoke)`): a square would open the gap, so it
  keeps ShieldWall, or Line without shields. A held choke also keeps its position on a brace
  instead of taking a brace point where the wall stands. Every other ground leaves facing and width
  at zero, so those walls behave as before.
- Forests: `TeamAI.TacticalRegions` of type `Forest`; the wall stands at the region's position
  only, with no facing and no width, so the forest is held at its centre.
- Hill: `HighGroundAnchor.HighGroundOf`, the best slope toward the enemy inside the cap. When it
  answers the formation's own ground (no enemy in view) the answer is `Here`.
- The candidates are copied once per anchor, lazily, into an immutable array on the first stand
  decision (the team-AI tick). After that, scoring is allocation-free. The installer counts the
  kinds in the array `StandGroundScan.Copy` returns for the registration line, so the log and the
  picker see one classification.
- Stand mode is off for every other user of the anchor (`ArcherRing`, `DisciplinedLine`,
  `EoredScreen`), so their behaviour is unchanged. `Stand()` is one way: the first call forgets the
  decision and picks again under the stand's rules, and only `EndStand()` (at the tactic's cancel)
  switches it back. The anchor also forgets its decision when the main infantry formation changes.

**Dismount** (`TaomTacticBase`, only for a culture with `dismount: true`). Once the stand has
started, every Defend apply orders `RidingOrderDismount` through `Formation.SetRidingOrder` on the
left, right and single cavalry seats and on the main and second infantry (a unit added to a
formation takes its riding order, `Formation.cs:2322`), skipping a formation that is not
AI-controlled. The order is idempotent and nothing ever orders a Mount, so it is one-way: horses
stay on the field and are not remounted (the AI only seeks a loose mount under a Mount riding
order). Riders without a horse count as infantry in the engine's class flags (5 s cache), the
cavalry seat's `Holds()` check fails, the recount runs and vanilla's merge folds them into the
wall; no transfer is issued by TAOM. Never dismounted: horse archers (their seat is not in the
list) and the player's team. The 1/1/2/1 and 2/1/2/1 splits both pass `vanguard: null`, so a routed
HeavyCavalry formation is no separate seat there: seated as cavalry, it is dismounted with the
rest. A mixed side dismounts by the majority culture's policy, and a formation order cannot skip a
creature rider (documented, not fixed).

**Polearm favour** (`Hooks/StandWeaponFavor.cs`). An AI human in a formation whose riding order is
Dismount gets `AiWeaponFavorMultiplierPolearm = polearmFavor` as a post-pass in both agent-stat
models, one line each, through `ICultureAggressionService.StandPolearmFavor`. For a `dismount: true`
team that is the whole standing wall (the infantry and the riders who got off), not the riders
alone; a team that never dismounts never holds the order and gets no favour. The base model writes
the property back to 1 on every update, so the post-pass cannot compound (the Custom Battle chain
and the campaign chain to that reset are pinned by binding tests). It is 1 when the MCM switch or
the doctrine is off. The stat update that follows a dismount can run off the main thread, so the
post-pass reads one team reference, one formation reference and an enum, and the value comes from
the immutable catalog. Men already in the infantry formation take the favour at their next stat
update, not at the order.

**Shield carriers never brace.** `Agent.CanPerformBrace` is true only for an agent with no mount,
no shield and a weapon with the brace usage flags (`Agent.cs:5066-5074`), so a shield carrier never
braces a spear against the charge, and the polearm favour is a weapon-choice weight, not a brace.

**Config** (all in `culture_doctrines.json`, validated in `CultureDoctrineConfigProvider`: NaN and
infinity first, then range; a bad value reverts to its default with a warning plus the summary
warning):

| Key | Range | Default | Meaning |
|---|---|---|---|
| `horseStand.enemyCavalryShare` | 0.05 to 1 | 0.5 | enemy melee cavalry share that triggers the stand |
| `horseStand.outnumberedRatio` | 0.5 to 5 | 1.0 | the enemy must exceed our whole side (us and allies) times this |
| `horseStand.polearmFavor` | 1 to 4 | 2.0 | polearm favour of the whole standing wall of a dismount:true team; below 1 is rejected |
| `doctrines.<cultureId>.horseStand.dismount` | bool | false | opts the culture in; true also dismounts its cavalry |

MCM: `Horse Stand` (default on, beside Culture Morale and Culture Aggression; needs Enable
Culture Doctrine). The tactics read it once per team at install, and the polearm favour reads it
again on every stat update, so the A/B arms switch without a restart but a battle already
installed keeps its stand. The sentence in its tooltip is plain English like its neighbours, not a
`{=KEY}` string.

**Left out of this slice.** Fords and the map edge as ground; path distance (the race is the
straight line over top speed, as the other anchors use); archer target priorities; a forced
re-wield (the favour only weights the engine's own weapon choice); shield-direction patches (no
Harmony patch is added); remounting when the stand ends; dismounting horse archers. Gondor and the
Elven ring do not adopt the stand. Whether standing at a forest's centre is good ground is
untested in game. Known and not fixed: a mixed side dismounts by the majority culture; the
abandoned horses can remount the enemy's unhorsed riders (the player's formations hold Mount);
the stand has not been smoke-tested.

**A/B** (Custom Battle, debug on, three runs per arm, 300 against 300 style, OFF control per the
protocol below). In every step the player plays the OTHER side (the Rohirrim attacker, F6 pressed
on the first frame), so the AI defender is armed: the stand never arms the player's own team. The
registration line reads `horseStand=on chokes=N forests=M dismount=yes|no` for the armed team and
`horseStand=off` for the player's; the status line ends in `:Stand:Choke|Forest|Hill|Here:<state>`.

Scenes by ground kind (counted 2026-10-19 from the live `TAOM_Map/SceneObj/<id>_forceatmo/scene.xscene`
files; the registration line's `chokes=` and `forests=` counts confirm them in game):

| Ground | Scene |
|---|---|
| Choke | `taom_rohan_battle_001` (2 chokes, one insurmountable), `taom_rohan_battle_fords_of_isen` (2), `taom_mordor_battle_003` (16) |
| Forest | `taom_gondor_village_001` (2 untyped regions, which read as Forest), `_002` (3), `_004` (2); these also hold 4 to 6 chokes, which the stand prefers. UNVERIFIED whether Custom Battle's village type opens them as a field battle |
| Hill or Here | a scene with neither, `taom_rohan_battle_002` |

1. Erebor 200 defending against Rohirrim 300, mostly cavalry, on a choke scene: the line reads
   `dismount=no`, the wall's suffix goes `:Stand:Choke:Marching` then `:Arrived`, and then holds.
2. Dale (`sturgia`) defending with horse of its own against the same Rohirrim: the cavalry seat
   reads `/Infantry` within a few seconds of the first stand apply, then merges into the wall
   within about 10 s, and the dismounted men hold spears (`AiWeaponFavorMultiplierPolearm`).
3. Erebor against Mordor: no stand (enemy cavalry share under 0.5).
4. Erebor 300 against Rohan 200: no stand (not outnumbered).
5. Player-team check: play Dale defending against outnumbering, mostly-horse AI Rohirrim. The
   player's line reads `player=yes ... horseStand=off` and the cavalry seat never reads
   `/Infantry`. If Custom Battle cannot put the player on the defending side, run this one in the
   campaign.
6. Looters and `erebor_warriors`: Custom Battle drops bandit cultures (`CustomBattleService.cs:41`),
   so move these to the campaign, or accept that `ShippedCultureDoctrinesConfigTests` is their
   only coverage (looters resolve to `default`: no stand; `erebor_warriors`: stand, `dismount=no`).
7. Ally team: a campaign cell, the player defending beside an allied Dale lord. The ally's line
   reads `player=no ... horseStand=on`.
8. Polearm favour arm: Dale as in step 2 with `"polearmFavor": 1.0` against the default 2.0, and
   count polearm wielders at contact.
9. OFF arm for the stand: MCM `Horse Stand` off with Culture Doctrine on. The other arms keep it on.

Watch items:

- The suffix stays `:Stand:<kind>:<state>` through contact (the latch: no re-pick at the first
  blow, no flip back at Engage).
- Reinforcements that arrive after the wall yields ride (the release at cancel); those that arrive
  during the stand dismount.
- A choke is filled edge to edge and does not open into a Square when the horse come in.
- A stand marching while horse are inside the race, and a mid-battle re-merge sending the wall
  marching again (the anchor resets when the main infantry formation changes).
- Rohan's unhorsed riders taking Dale's abandoned horses (the player's formations hold Mount).
- A cavalry seat on Stop still dismounting: UNVERIFIED native behaviour.

## Configuration

### Config File: `Main/_Module/ModuleData/culture_doctrine/culture_doctrines.json`

Reloads on game restart only (`Reuse.Singleton` provider).

| Field | Type | Description |
|---|---|---|
| `enabled` | bool | File-level switch, ANDed with the MCM toggle |
| `engagement.cavalryMattersMetres` | float | Enemy melee cavalry (a real formation) riding at a foot formation, or among it, inside this distance make it square up where it stands; the square holds until they are beyond 1.5 times it, then the line forms back up on its foot target. 0 to 500, default 100; reverts with a warning |
| `engagement.highGroundMaxMetres` | float | A wall or ring takes the best slope toward the enemy inside this distance, never a hill beyond it. Default 60 |
| `engagement.holdWhenEnemyWithinMetres` | float | Any enemy foot formation inside this distance ends the march; the foot forms where it stands. Horse do not end it. Default 50 |
| `doctrines.<cultureId>.tactics[]` | list | Rows for that culture; `default` is the fallback for every culture without a row and must exist |
| `id` | string | A `DoctrineTactic` name, case-insensitive, without the engine's `Tactic` prefix |
| `multiplier` | float | Factor on the tactic's own weight, 0 to 5 (0 keeps the row but never picks it); reverts to 1 with a warning outside that range or when not finite |
| `minTactics` | int | Commander Tactics skill the side needs for the row, 0 to 300; reverts to 0 with a warning |
| `side` | string | `any` (default), `attacker` or `defender`; reverts to `any` with a warning |
| `doctrines.<cultureId>.morale` | object | `neverRout` (bool) and `bravery` (float, -30 to 30, added to every soldier's starting morale; reverts to 0 with a warning). Absent means vanilla |
| `doctrines.<cultureId>.aggression` | object | `attack`, `shield`, `shooterError`, `chargeDistance`: multipliers 0.25 to 4 on the engine's per-soldier AI values; each reverts to 1 with a warning. Absent means vanilla |
| `horseStand.enemyCavalryShare`, `.outnumberedRatio`, `.polearmFavor` | float | The horse stand's numbers, 0.05 to 1 (default 0.5), 0.5 to 5 (default 1.0) and 1 to 4 (default 2.0); each reverts with a warning. See [Horse stand](#horse-stand) |
| `doctrines.<cultureId>.horseStand` | object | Opts the culture into the horse stand; `dismount` (bool, default false) also dismounts its cavalry. Absent means the culture never stands |
| `doctrines.<cultureId>.formations` | object | troop StringId to formation class name (`Infantry`, `Ranged`, `Cavalry`, `HorseArcher`, `Skirmisher`, `HeavyInfantry`, `LightCavalry`, `HeavyCavalry`); an unknown class or an empty id drops the row with a warning. Absent means the engine's own class |

Culture keys are StringIds, and TAOM re-skins six vanilla cultures without changing their ids:
Rohirrim = `vlandia`, Dunlendings = `empire`, Haradrim = `aserai`, Rhun = `khuzait`, Dale =
`sturgia`, Khand = `battania`. `ShippedCultureDoctrinesConfigTests` pins that.

A missing file or a parse failure gives the vanilla-equivalent catalog; an unknown tactic id
drops its row with a warning; a `null` tactics list drops the doctrine. Any rejection ends with
one summary warning.

### MCM: `Battle Tactics/Culture Doctrine`

| Setting | Default | Notes |
|---|---|---|
| `EnableCultureDoctrine` | off | Read once per mission at `EarlyStart`; applies from the next battle. Off until the in-game A/B passes |
| `CultureDoctrineDebug` | off | The `[Doctrine]` status line every 5 s of mission time |
| `CultureDoctrineMorale` | on | The never-rout and bravery tier; needs the master toggle. A/B knob |
| `CultureDoctrineAggression` | on | The per-soldier aggression post-pass; needs the master toggle. A/B knob |
| `CultureDoctrineHorseStand` | on | The horse stand (`Horse Stand` in MCM); needs the master toggle. Read once per team at install for the tactics, and on every stat update for the favour. A/B knob |

The `[MissionPerf]` heartbeat lives on the Battle Load Diagnostics page (`EnableMissionPerfHeartbeat`, on).

### Console

`taom.tactic_status`: for every team with a team AI, the current tactic and each formation's
active behaviour and arrangement. Works in Custom Battle.

## Log lines

```
[Doctrine] team=1 side=Defender player=no culture=erebor doctrine=erebor troops=300 tacticsSkill=0 morale=never-rout/bravery +15 horseStand=on chokes=2 forests=1 dismount=no registered=[ShieldWall*1.00, TwoLineWall*1.00, Charge*0.30, ...]
[Doctrine] formation routing subscribed (GetAgentTroopClass_Override)
[Doctrine] t=+65s team=1 side=Defender player=no tactic=TaomTacticShieldWall formations=[Infantry:210 BehaviorBracedDefend:ShieldWall/ShieldWall/FireAtWill, Ranged:60 BehaviorSkirmishLine/Scatter/HoldYourFire, Cavalry/Infantry:95 BehaviorFootCharge:Charging/Line/FireAtWill] taom=[TaomTacticShieldWall:Defend:Arrived]
[Doctrine] off: vanilla tactics stay (status line on)
[Doctrine] disabled for this mission after <exception>
[MissionPerf] t=+65s frames=300 fps=60.0 avgMs=16.67 p95Ms=25.50 maxMs=40.3 agents=812 active=640 formations=9 gc0=12 gc1=3 gc2=1 mainMs=4.20 renderMs=6.10
```

A TAOM tactic that throws on the AI thread reports `taom=[TaomTacticArcherRing:failed: ...]`
and weighs 0 from then on; the team falls back to its next tactic. A TAOM behaviour shows its
stage or stance after its name (`BehaviorCycleCharge:Reforming`, `BehaviorBracedDefend:Square`,
`BehaviorFootCharge:Charging` or `:Square`, `BehaviorInfantrySkirmish:Committed`, or
`failed: ...`), then the arrangement and the firing order; an Elven tactic under volley control
ends its status with `:Hold` or `:Loose`; a wall or ring tactic ends with `:Marching`,
`:Holding` or `:Arrived` (the high-ground race, refreshed the second the anchor moves, not only
at the next apply), and while the horse stand holds it ends `:Stand:Choke|Forest|Hill|Here:<state>`
instead. A formation prints as `slot:count[seat]`, or `slot/class:count[seat]` when
the engine's class ratios disagree with the slot, where the seat is the one the current TAOM
tactic gave it at its last recount (`M` main infantry, `S` second line, `L`/`R` wings, `A`
archers, `LC`/`RC`/`C` cavalry, `HA` horse archers, `V` vanguard, `-` none: an unseated
formation runs vanilla's default rows); an arrangement order other than Line is followed by
the engine's actual arrangement object and its size, `Square(RectilinearSchiltron 10x10)`,
because the order and the arrangement the men are in are two different things (the fourth
battle's wall was ordered Square and looked like a line); and each formation ends with its
armed rows, `{BracedDefend=1,FootCharge=0.3}`, or `{none}`: the slot is where the units were put and the engine's
consolidation moves units between slots freely (an infantry mass can sit in the `Cavalry`
slot), and riders without a live mount count as infantry (`QueryLibrary.IsCavalry` needs
`Agent.HasMount`), which is why a "Cavalry" slot can read `/Infantry` and be merged into the
foot.

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/CultureDoctrine/Domain/DoctrineTactic.cs` | The closed tactic id set, `DoctrineSide`, name parsing |
| `Main/Features/CultureDoctrine/Domain/Doctrine.cs`, `DoctrineCatalog.cs` | Immutable doctrine rows; the catalog with the vanilla-equivalent default |
| `Main/Features/CultureDoctrine/Domain/SideProfile.cs`, `TacticRoster.cs` | Pure: side combatants to culture and skill; doctrine to the registered list |
| `Main/Features/CultureDoctrine/CultureDoctrineConfigProvider.cs` | Validating JSON loader |
| `Main/Features/CultureDoctrine/Hooks/CultureDoctrineMissionLogic.cs` | Entry point (`EarlyStart` swap, status line) |
| `Main/Features/CultureDoctrine/Hooks/TeamCombatantSelector.cs`, `TeamTacticProbe.cs` | Engine reads at the boundary: combatants per team; current tactic and formation state |
| `Main/Features/CultureDoctrine/Hooks/Tactics/VanillaTacticWrappers.cs` | The nine weight wrappers |
| `Main/Features/CultureDoctrine/Hooks/Tactics/TaomTacticBase.cs`, `TaomTactics.cs`, `TaomTacticsPhaseC.cs` | The shared lifecycle, the four Phase B and the seven Phase C tactics |
| `Main/Features/CultureDoctrine/Hooks/Tactics/FormationSlots.cs`, `VolleyControl.cs` | Slot assignment per split (second line, wings, vanguard); the archers' firing order |
| `Main/Features/CultureDoctrine/Hooks/Tactics/HighGroundAnchor.cs`, `Doctrines/HighGroundRace.cs` | Where a position-holding tactic stands: the race, the re-check, the lock |
| `Main/Features/CultureDoctrine/Hooks/Tactics/BehaviorWeightApplier.cs`, `TeamQuerySnapshotFactory.cs`, `TacticFactory.cs` | The three engine-type switches; `Ensure<T>` adds a TAOM behaviour to a formation |
| `Main/Features/CultureDoctrine/Hooks/Behaviors/TaomBehaviorBase.cs`, `BehaviorBracedDefend.cs`, `BehaviorBracedAdvance.cs`, `BehaviorFootCharge.cs`, `BehaviorArcherFlank.cs`, `BehaviorCycleCharge.cs`, `BehaviorInfantrySkirmish.cs`, `BehaviorEnvelopWing.cs`, `WallStances.cs`, `EnemyScan.cs` | The seven TAOM formation behaviours, their wrapped lifecycle, and the enemy walk they target through |
| `Main/Features/CultureDoctrine/Doctrines/DoctrinePlan.cs`, `DoctrinePlans.cs`, `DoctrineWeights.cs`, `TacticPhaseMachine.cs` | Pure plans, weights, ring geometry, phase machine |
| `Main/Features/CultureDoctrine/Doctrines/TargetSelection.cs`, `ChargeWeight.cs`, `ArcherFlankGeometry.cs`, `Domain/EngagementTunables.cs`, `Hooks/Behaviors/EnemyScan.cs`, `BehaviorFootCharge.cs` | The foot-first target rule, the engagement distances from the `engagement` block, and the enemy-formation walk that feeds them |
| `Main/Features/CultureDoctrine/Doctrines/BraceDecision.cs`, `CavalryThreat.cs`, `CycleChargeMachine.cs`, `InfantrySkirmishMachine.cs`, `EnvelopGeometry.cs`, `VolleyDecision.cs` | The pure cores the behaviours and the volley control step |
| `Main/Features/CultureDoctrine/Domain/HorseStandTunables.cs`, `Doctrines/HorseStand.cs`, `Doctrines/StandGround.cs`, `Hooks/Tactics/StandGroundScan.cs`, `Hooks/StandWeaponFavor.cs` | The horse stand: its numbers and per-culture policy, the gate and weight edge, the ground picker, the scene scan, the polearm post-pass |
| `Main/Features/CultureDoctrine/Domain/CultureMorale.cs`, `CultureAggression.cs`, `FormationRouting.cs`, `FormationRoutingRule.cs` | The three per-culture blocks below the tactics, and the routing rule |
| `Main/Features/CultureDoctrine/CultureMoraleService.cs`, `CultureAggressionService.cs` | The two services the models read (catalog lookups behind the sub-toggles) |
| `Main/Features/CultureDoctrine/Models/TaomBattleMoraleModel.cs`, `TaomCustomBattleMoraleModel.cs`, `TaomCustomBattleAgentStatCalculateModel.cs` | The morale seams (campaign, Custom Battle) and the Custom Battle aggression post-pass; the campaign post-pass is in `CareerSystem/Models/TaomAgentStatCalculateModel.cs` |
| `Main/Features/CultureDoctrine/Hooks/AgentAggressionApplier.cs`, `FormationRoutingSubscriber.cs`, `RoutedFormationGuard.cs`, `TeamDoctrineInstaller.cs` | The aggression post-pass on `AgentDrivenProperties`; the troop-class override; the routed formation's exemption from consolidation; one team's roster install |
| `Main/Features/CultureDoctrine/Cheats/CultureDoctrineCheats.cs` | `taom.tactic_status` |
| `Main/Features/MissionPerf/` | `FrameStats`, `MissionPerfLine`, `MissionPerfHeartbeatBehavior` |
| `Main/_Module/ModuleData/culture_doctrine/culture_doctrines.json` | The shipped doctrines |
| `Main/_Module/ModuleData/taom_module_strings.xml` | `str_team_ai_tactic_text.TaomTactic*` (the sergeant tactic popup), `str_formation_ai_sergeant_instruction_behavior_text.Behavior*` (the sergeant's order text) and `str_formation_ai_behavior_text.Behavior*` (the "Infantry are ..." message after F6, `MissionOrderVM.OnDelegateCommandToAI`; the first A/B rendered `ERROR: Text with id` for every TAOM behaviour because only the first two tables were seeded). `DoctrinePopupStringsTests` pins all three per TAOM type |

## Tests

- `DoctrineTacticIdsTests`, `SideProfileTests` (majority, side skill floor), `TacticRosterTests`
  (vanilla-equivalence at skill 0/20/50, Charge guarantee, side and skill filters, dedupe,
  ensured rows), `TeamCombatantSelectorTests` (the engine's ally-team rule and the side-wide skill,
  over fakes of `IBattleCombatant`)
- `CultureDoctrineConfigProviderTests` (one test per validation rule, NaN included),
  `ShippedCultureDoctrinesConfigTests` (the shipped file parses clean, keys the re-skinned
  cultures by vanilla id, every culture carries its TAOM tactic on the right side)
- `DoctrinePlansTests` (no duplicates, roles match the split, every extra slot planned, the
  doctrine intent per plan, volley control on the Elven plans only),
  `DoctrineWeightsTests` (zero cases, monotonicity, clamps, NaN and zero-power inputs, the
  seven Phase C functions), `HighGroundRaceTests` (go, hold, earliest enemy decides, margin,
  form-up by unit count, NaN and zero speeds on either side),
  `ShippedDoctrineOrderingTests` (every shipped TAOM row beats its vanilla neighbours by the
  sticky factor at its canonical army), `TacticPhaseMachineTests`
- `EngagementRulesTests` (the foot-first target, the cavalry distance, the two high-ground gates,
  NaN), `DoctrinePopupStringsTests` (three string tables per TAOM type),
- `BraceDecisionTests`, `CavalryThreatTests`, `CycleChargeMachineTests`, `InfantrySkirmishMachineTests`,
  `EnvelopGeometryTests`, `VolleyDecisionTests`: the pure cores, every stage transition, every
  bar against vanilla's numbers, NaN inputs holding the stage, the reform that a short charge
  must not skip
- `ShippedDoctrineOrderingTests` also checks the TAOM pairs: a gated variant (TwoLineWall over
  ShieldWall, Envelop over InfantryMass) beats the tactic it refines by the sticky factor while
  its gate passes and is 0 when it fails, and the gate reads the class total, not the largest
  formation the tactic's own split will halve
- `CultureMoraleTests`, `CultureAggressionTests` (the math against the engine's clamps),
  `FormationRoutingTests` (parse, route, the dismount rule), the two service tests (on and off)
- `DoctrineSwitchInvariantTests` (`BindingVerification`): the applier's and the factory's switch
  bodies read as IL, every `BehaviorKind` reaching a `SetBehaviorWeight<T>` whose T
  `TeamAIGeneral` registers or the applier ensures, every TAOM behaviour on `TaomBehaviorBase`
  directly, every `DoctrineTactic` reaching its named constructor; both switches throw on an
  unmapped member
- `CultureDoctrinePhaseCBindingTests` (`BindingVerification`): the `BehaviorComponent`
  lifecycle the base overrides, `FormationAI` ticking one behaviour and cancelling the old one,
  the query members and order setters the behaviours use, the morale seams and their caller in
  `CommonAIComponent`, the agent-stat post-pass surface, the troop-class override's type and the
  body it replaces, the sergeant text lookup
- `CultureDoctrineBindingTests` (`BindingVerification`): the nine tactic types public,
  unsealed, `(Team)`-constructible and overriding `GetTacticWeight`; `_currentTactic`; the
  public team and combatant methods; the protected `TacticComponent` surface the base builds
  on; the behaviour parameter fields; the `TacticalPosition` runtime constructor; the query
  members the snapshot reads
- `HorseStandTests` (each gate input, equality at the edges, NaN and infinite ratio), `StandGroundTests`
  (preference order, choke facing and insurmountable, choke width, the cap, the hold distance, a
  race lost to horse that a foot-only race would win, NaN speeds, empty input),
  `StandWeaponFavorTests` (the predicate and the service's switch); `ShippedDoctrineOrderingTests` also
  pins the stand army and `StandEdge`, and `ShippedCultureDoctrinesConfigTests` the eight opted-in cultures
  and their `dismount` flags. `HorseStandTests` also pins the side-vs-side outnumbered rule and
  `PolicyFor` (switch off, player team, attacker, AI defender); `StandGroundTests` pins `HoldOf`;
  `BraceDecisionTests` pins no Square while holding a choke. The stand's binding tests
  (`BindingVerification`, in `CultureDoctrinePhaseCBindingTests`) pin the riding-order and scene
  surface, the polearm setter and the agent reads, and the reset of the polearm favour on every
  update in both chains: Custom Battle's, and the campaign's (`SandboxAgentStatCalculateModel`
  reaching `AgentStatCalculateModel`'s reset)
- `FrameStatsTests`

Engine wiring (the `EarlyStart` swap, the tactics on the AI thread, the ring position) is
game-tested per ADR-008; the A/B below is that test.

## Verification: the A/B protocol

Custom Battle, `CultureDoctrineDebug` and `EnableMissionPerfHeartbeat` on, `[MemSample]` on. Since #788
Custom Battle also runs Combat Mechanics' crush-through, cleave, stagger and charge knockdown rules
([combat-mechanics.md](combat-mechanics.md), "Custom Battle"), so a result recorded before it is a pre-#788
baseline: re-baseline before the next comparison. One
flat open scene and one sloped scene. Press F6 on the first frame so both sides are AI-controlled.
300 v 300 for behaviour, 800 v 800 for the perf ceiling. Matchups: Erebor (defender) vs Mordor,
Rohirrim vs Dunlendings, Lindon (defender) vs Gundabad, Erebor as attacker vs Mordor; toggle off
and on, three runs each.

Per run the log carries the `[Doctrine]` registration line per team, the 5 s status line (it
runs in the off arm too; only the registration line is doctrine-on),
`[MissionPerf]` every 5 s, and `rgl_log` must show no `MBException`, no `ran off the main mission
thread`, no `ERROR: Text with id` (sergeant popup), no `taom=[...:failed`.

The race adds a check to the Erebor and Lindon cells: at 60 s the `[Doctrine]` status shows
`Marching` then `Arrived` when the enemy started far, and `Holding` from the first line when the
scene puts the enemy foot within a minute of the high ground; a wall that shows `Marching` while
enemy infantry is already in contact is the failure the race exists to prevent.

Phase C and D add cells, all 300 v 300, three runs, the toggles above per tier:

- Erebor (defender) vs Rohirrim: the wall shows `BehaviorBracedDefend:Square` while the eored
  closes and `:ShieldWall` again within 10 s of the charge breaking; a wall hit in
  `ShieldWall` by a frontal charge is the failure.
- Rohirrim (attacker) vs Gondor: the cavalry cycles `Charging` -> `RidingThrough` -> `Reforming`
  -> `Charging` at least twice in the first two minutes; a formation stuck in `Charging` for
  more than a minute is the failure (the machine's 10 s cap should make it impossible).
- Dunlendings (attacker) vs Gondor: `BehaviorInfantrySkirmish` shows `Approaching` then
  `Throwing`, `PullingBack` when the Gondor line closes, and `Committed` once the javelins are
  gone, after which the line charges.
- Mordor (attacker, 400) vs Erebor (defender, 250): `TaomTacticEnvelop` current, both wings
  `BehaviorEnvelopWing:Marching` then `:Charging` from the flanks.
- Lindon (attacker) vs Gundabad: the archers' firing order reads `HoldYourFire` while the
  enemy is beyond 80 percent of their range and `FireAtWill` inside it.
- Harad (attacker) vs Gondor with `harad_mumakil_rider` in the roster: the registration line
  shows the routing subscribed, the status line a `HeavyCavalry` formation, and
  `TaomTacticMumakVanguard` current.
- Archers on the flank (Erebor or Gondor defending): the archer formation reads
  `BehaviorArcherFlank:Flank` beside the wall, a gap out and a little back, from the first
  Defend apply; `:Behind` while an enemy formation is closing on the archers rather than the
  wall, `:Flank` again once it is 45 m off; the enemy foot goes for the wall, not the bows.
- Foot never chases horse (any matchup with cavalry on one side): a TAOM foot formation's
  status never reads `Charging` toward a horse formation while an enemy foot formation
  stands; a line with melee cavalry riding at it inside 100 m reads `:Square` with time to
  form it, holds the square while they cycle inside 150 m, and within about 3 s of the horse
  passing 150 m it reads `Charging`, `ShieldWall` or `Line` again, on its foot target. Horse
  archers wheeling at range never square it; a lone rider never squares it.
- Stand and fight (Erebor defender): the wall's status reads `Marching` only toward a slope
  inside 60 m with no enemy foot inside 50 m, `Holding` otherwise from the first line; a horse
  formation passing at 45 m does not end a march.
- Morale: Gundabad (attacker) vs Gondor at 300 v 450, `CultureDoctrineMorale` on then off; on,
  no Gundabad soldier routs before the last one falls (no `BehaviorRetreat`, no fleeing agents
  in the kill feed); off, the rout comes at the usual point.
- Aggression: the same matchup with `CultureDoctrineAggression` on then off; the on arm's orc
  attack rate is visibly higher and its shield use lower. Casualty ratios recorded, not gated.

Pass: over the steady window (30 s after F6 to the first rout) the median average frame ms on vs
off within 3 percent and p95 within 5 percent at 800 v 800, GC gen 2 not higher; the doctrine
side's current tactic is in its preferred set for at least 70 percent of samples on and at least
30 points less off; at 60 s Erebor's infantry stands in ShieldWall or Line where it formed (or
on a slope inside 60 m it reached first), Rohan's cavalry shows `BehaviorCycleCharge` once joined
(`BehaviorTacticalCharge` or `BehaviorFlank` before), Lindon's infantry is a Circle around a
Square of archers, attacking Erebor's infantry shows `BehaviorBracedAdvance` and never
`BehaviorCharge` or a `BehaviorFootCharge` at horse before contact. The horse stand passes when,
in the armed arm, the suffix reads `:Stand:...` from the first apply through contact without
changing kind, a `dismount=yes` team's cavalry seat reads `/Infantry` and its men hold polearms at
contact, and the wall that fills a choke keeps its ShieldWall or Line (never `:Square`) while it
holds the gap; the OFF arm shows none of it.

**Natural spread: a single pair of runs proves nothing.** yotthani ran 13 unchanged mirror battles
(100 v 100 of the same troop, no value touched) and got an attacker-to-defender strike rate ratio
of 0.96 to 1.72, with the attacker winning 10 of 13 on that map. A doctrine on-versus-off pair
whose difference falls inside that band, on strikes per second or on the winner, is noise. Keep an
OFF control arm, repeat the cell (the three runs above are the floor, not the proof), and compare
medians against that spread. A start layout can swamp a tactic as well: on one scene the defender
won 7 of 14 battles starting within 70 m of a choke point and 1 of 16 beyond 160 m, so match the
start layout across arms, not just the troops
([formations-and-team-ai.md](../reference/engine/formations-and-team-ai.md), "Measured tactic
behaviour"). Source: yotthani, MithrilForge `docs/engine/battle-behaviour-measured.md`@0a2f4761
lines 97-101 and 112-118, measured in recordings (n=13 and n=39), measured with TAOM 2.0.34 loaded
(plus Armory and DualWield), v1.5.5, custom battles, both sides AI.

## Limitations and open items

- Not smoke-tested in game as of 2026-09-17; the toggle ships off until the A/B passes. The horse
  stand (#892) is new and has no in-game run at all, the Horse Stand switch included.
- **Measured behaviour of the default AI (yotthani, v1.5.5).** Source: MithrilForge
  `docs/engine/battle-behaviour-measured.md`@0a2f4761, measured with TAOM 2.0.34 loaded (plus
  Armory and DualWield), custom battles, both sides AI.
  - The first team decision comes at 0.5 s and picks `FullScaleAttack`, because every defensive
    tactic weighs 0 before its positions are evaluated (lines 79-83, [M] and [C]). A doctrine
    tactic that wants to replace it has to clear the engine's 1.5x sticky factor
    (`TeamAIComponent.cs:301, 316-322`, v1.5.5); the TwoLineWall and Envelop edges of 1.6x in the
    review record below already do. That reading of the bar for a doctrine tactic is an inference from TAOM's code.
  - Start layout decides more than tactic: the defender won 7 of 14 within 70 m of a choke point,
    2 of 9 at 70 to 160 m, 1 of 16 beyond 160 m, whether or not its tactic was forced (n=39,
    lines 112-118). Forcing `HoldChokePoint` from a bad layout won 1 of 5.
  - AI value levers: the offensive and timing values move strikes by large factors, while more
    aggression traded wounds both ways with no measurable win gain (lines 97-110).
- **The `shield` profile is a live lever, with a cap.** `AgentAggressionApplier` scales
  `AiDefendWithShieldDecisionChanceValue` (driven property 28). TAOM decompile of the installed
  v1.5.5 client, 2026-10-15: the native melee AI reads it at 0x6A10A1, 0x6A10FF and 0x6A144C
  (`FUN_1806a05a0`) through `min(0.95, value)` or `min(0.95, 0.5 * value)`, and uses the result
  to enter decision state 6, which the decompile suggests is the defend state ([Likely]: the defend abilities lead
  to it, `AIAttackOnDecideChance` leads to state 7). So lowering the value takes effect, while raising a value that is
  already at the cap changes nothing. Whether the shipped profile multipliers push any soldier's
  value past the cap depends on the base value, which was not measured here. yotthani's two
  battles that zeroed this value together with five others and showed no visible effect (n=2,
  line 107) remain **unexplained**: two of the seven places that set state 6 are not
  gated by the six values, and the run may not have applied them. The same decompile found no
  native reader for `AiAttackingShieldDefenseChance`, which TAOM does not write (the scan covered
  93.4 % of `.text`, so "no read found", not "proven dead").
- Review record for Phase C and D: six agents (standards, engine fidelity, performance and
  threads, logic and tests, lifecycle, and the systems analysis Mike asked for). Fixed before
  commit: the TwoLineWall and Envelop gates read the largest infantry formation, which their
  own split halves or thirds, so on armies of 80 to 179 foot the tactic cancelled itself at
  the next decision (the snapshot now carries `InfantryTotal`); their 1.05x and 1.1x edges
  could never beat the engine's 1.5x sticky factor once the plain tactic held the team (now
  1.6x); the cycle charge's reform ended on its first tick whenever the charge began inside
  30 m (the stop distance is 35 to 60 m for horse and the contact threshold is half of it);
  the brace saw only a charge the wall already faced (`CavalryThreat`); a plan re-apply
  re-armed a spent javelin line; any vanilla tactic folded the routed vanguard into the
  cavalry (`RoutedFormationGuard`); a formation the player took back kept the volley hold.
  The analysis, with the engine cost model and the rewrite verdict (do not), is
  `docs/reviews/analysis-battle-ai-2026-09-17.md`.
- The popup strings are three tables (eleven tactic rows, six sergeant-instruction rows, six F6
  rows). The tactic and sergeant rows were translated by the localization pass in `303bf2a7`;
  the six F6 rows and the FootCharge rows still carry English text in the 12 language files;
  run `tools/translate_with_claude.py --lang <L> --module TAOM --sync-ids --apply` with an API
  key. `DoctrinePopupStringsTests` checks that every language file's F6 rows keep the vanilla
  `{TROOP_NAMES_BEGIN}`/`{?IS_PLURAL}`/`{TROOP_NAMES_END}` variables after such a run.
- `BracedDefend` shows a shield wall only when `FormationQuerySystem.HasShield` (40 percent of
  the formation with shields); a Dwarven line below that stands in Line, which is also what the
  Uruk pikes want. The square is the engine's `SquareFormation`; whether the arrangement
  holds a charge better than a shield wall is what the A/B's Erebor vs Rohirrim cell measures.
- The Elven ring is the engine's circle, not a hollow square; `SquareFormation` builds four
  sides with `MaxRank = (UnitCountOfOuterSide + 1) / 2` and whether its centre stays hollow is
  unverified.
- `TaomTacticBase` is about 460 lines: it is the one place the vanilla lifecycle exists in TAOM
  (vanilla's tactics are 180 to 370 lines each) and the eleven tactics on top of it are 9 to
  52 lines. The slot code that writes `TacticComponent`'s protected fields cannot leave the
  class; the picking (`FormationSlots`), the volley (`VolleyControl`) and every decision
  (`Doctrines/`) already have.
- A TAOM behaviour added to a formation by `Ensure<T>` stays on that formation for the mission
  (vanilla never removes behaviours either); its weight factor is 0 again after any
  `ResetBehaviorWeights`, so a vanilla tactic taking the team never runs it.
- The cycle charge disengages with a `Move` order, which is how vanilla's own machine pulls a
  formation out; riders inside the melee turn and ride, and a few will be caught. The 10 s cap
  on the melee is the tunable if the A/B shows it too short or too long.
- The mumakil vanguard needs the routed troop in the roster; the routed formation itself is
  exempt from consolidation (`RoutedFormationGuard`), so it survives a vanilla tactic being
  current first and the vanguard tactic can take it later. Once the last mumak is down the
  slot is empty, the weight is 0 and the plain 1/1/2/1 split resumes. On the player's own team
  the exemption holds only until F6.
- Custom Battle's game type adds its models before the submodules run, so the two Custom
  Battle models here are last-registered; the scene editor's test battle takes the same path.
- The status line's console twin (`taom.tactic_status`) can run while an async tick is in flight;
  every read is a reference, an int or a 4-byte enum, so the worst case is one stale line.
- Review record: `docs/reviews/rca-culture-doctrine-2026-09-16.md` (Phase A and B, seven agents,
  twelve findings), `docs/reviews/rca-culture-doctrine-phase-c-2026-09-17.md` (Phase C and D, six
  agents, ten findings) and `docs/reviews/rca-culture-doctrine-engagement-2026-09-17.md` (the
  engagement slice after the first A/B, six agents, thirteen findings), all fixed before their
  commits. The Codex adversarial pass
  (`docs/reviews/codex-adversarial-culture-doctrine-2026-09-16.prompt.md`) ran out of ChatGPT
  usage after about 110k tokens of decompile work and returned no report; its three interim
  observations (both-side attrition in `TacticCharge`, `ResetTacticalPositions` true for three
  vanilla tactics, the ring centre half a diameter behind its order position, which vanilla's
  own pairing shares) are folded in above. Re-dispatch with the same prompt when credits allow.
- Per-formation flavour is still absent: a Rohan cavalry formation inside a Gondor army fights
  under Gondor's doctrine, morale and aggression (the side's majority culture decides).
- Every Custom Battle run recorded below is a pre-#788 baseline: #788 changed Custom Battle combat (crush-through,
  cleave, stagger, charge knockdown), so re-baseline before the next comparison.
- The first Custom Battle A/B (Erebor defender v Mordor, 300 v 300, 2026-09-17) ran clean:
  registration on both sides, routing subscribed, no failed latch, 172 to 183 fps at 661 agents,
  gen 2 collections zero. Its two failures (the wall marching 95 s to a far hill, the foot
  chasing the horse) are the engagement slice above. The second (120 v 120, 12:29, on the
  engagement slice) had the dwarf wall in ShieldWall within five seconds of F6 and standing
  there for the battle; Mordor's mass charged it and died to the last (never-rout dwarves lost
  18 of 120). Two things it showed: the tactic's status line kept saying `Marching` for 85 s on
  a wall that stood (the anchor moved to Arrived without a re-apply; fixed, the line refreshes
  on the anchor's own tick), and Mordor's two mounted slots stood on `BehaviorStop` for 30 s
  under Envelop and were then merged into the foot mass: the engine's class ratios read them as
  infantry, so no cavalry row seated them and a formation the plan does not seat kept stale
  weights (fixed: every unseated formation gets vanilla's defaults, as every vanilla tactic
  does). Mike confirmed Mordor fielded no mounted troops that battle, so the two slots were
  Envelop's own three-way infantry split landing in the empty Cavalry and HorseArcher slots
  (the engine takes any empty slot as a split target). The `[seat]` marker then showed them
  seated, and the standing is the DEPLOYMENT PHASE: `DeploymentMissionController` applies the
  AI team's tactic once from `ResetTactic` while `Mission.AllowAiTicking` is false, the first
  decision has no closest enemy cached (vanilla's battle-joined test reads that as joined, so
  the Engage rows go on first), and nothing ticks again until the player starts the battle;
  rows that weigh by the closest enemy read 0 until then. Vanilla stands the same way. The
  status line's `{armed rows}` list makes this readable in the log.
- The third and fourth battles (Mordor v Gondor 310 v 313; Erebor v Rohirrim 301 v 256, both
  13:00 to 13:12): Gondor's `DisciplinedLine` held (Defend, archers on SkirmishLine, cavalry
  ProtectFlank) and Mordor's mass died on it. Erebor's two-line wall stood in ShieldWall within
  five seconds, went `Square` on both lines the moment the 224 Rohirrim came in, released to
  ShieldWall as they passed and squared again, exactly as designed, and lost 299 of 301 for
  about 26 riders. Three things to weigh: the second line stayed braced in its own square 12 m
  behind the front for the whole fight and never fought (FootCharge holds while horse are on
  it, and horse were always inside 150 m); the 122 archers behind the wall had no cover and
  were cut down; and the front square itself did not hold against 224 heavy horse. Whether a
  dwarf square should beat that many Rohirrim is a numbers question; the second line and the
  archers are doctrine questions; the archers' answer is `BehaviorArcherFlank` (the players'
  own placement, above), the second line's is open.
- The fifth battle (Erebor v Rohirrim, 120 v 120, 13:51): the archers took the right flank from
  the first line, the wall's Square was a real block (`RectilinearSchiltron 5x5`, the engine's
  most compact form for 48 men), the eored went for the bows anyway and the 30 m trigger was
  late (fixed: the archers fall back on the wall's brace rule too). The roster was Erebor
  militia against Rohan veteran lancers with 1.5x charge damage and the outcome (120 for 0) is
  the roster's; a doctrine A/B needs matched tiers and an OFF control run.
- `taom_module_strings.xml` was registered for `Campaign` and `CampaignStoryMode` only, so every
  popup row was missing in Custom Battle through five runs; `CustomGame` and `EditorGame` added
  and pinned by `DoctrinePopupStringsTests`.
- `MixedFormations` (`Patch30_FormationGetOrderPositionOfUnit`) replaces the engine's unit slots
  for a player-team formation that holds a non-vanilla layout while it holds position, so a
  doctrine arrangement on such a formation is overridden. Mike does not use layouts, and the
  toggle should be OFF for the A/B; a doctrine-side guard is not written. The cavalry cells (Erebor v Rohirrim) are still owed, and the toggle stays off
  until they pass.
- The high-ground march is decided once: `Holding` is terminal, so a wall that formed where it
  stood at the first decision (the enemy foot inside 50 m, or no slope worth having inside 60 m)
  does not march later when the picture changes. Deliberate: a wall that walks off mid-battle is
  the failure the race exists to prevent.

## Roadmap after this slice

Phase E: per-formation culture weights inside mixed armies (the
`BannerBearerAssignmentMissionLogic.ResolveFormationCultureId` shape), so the culture of the
men in a formation, not the side's majority, picks its behaviour rows and its aggression. The
morale tier is already per soldier. Beyond that: a routed `Skirmisher` formation for Dunland's
javelin men under `HitAndRun` (the routing seam exists; the tactic would keep the slot the way
`MumakVanguard` keeps `HeavyCavalry`), and the hollow-square research.

## Doctrine per culture

What each culture carries after Phase C and D (2026-09-17), with the Phase B state it replaced.
"Data" rows are `culture_doctrines.json` alone.

| Culture | Phase B | Now |
|---|---|---|
| Erebor and Ered Luin (Dwarves) | ShieldWall, race | ShieldWall on `BracedDefend`/`BracedAdvance` (the anti-cavalry square), TwoLineWall as defender; never rout, bravery +15; shield 1.5, attack 0.9; horse stand with `dismount: false` (war rams) |
| Lindon, Rivendell, Lothlorien, Mirkwood (Elves) | ArcherRing (defender), weighted `RangedHarrassmentOffensive` (attacker) | ArcherRing and ArcherAdvance, both under volley control; Mirkwood `DefensiveLine` 0.8 (the forest bias vanilla's scoring already carries); never rout, bravery +10; shooterError 0.5 |
| Rohan (`vlandia`) | CavalryDominance | CavalryDominance with `CycleCharge` once joined, EoredScreen as defender; routs, bravery +5; attack 1.2, chargeDistance 1.3 |
| Rhun (`khuzait`) | default | CavalryDominance and DisciplinedLine (heavy cavalry like Rohan, better foot and bow, few horse archers: Mike, 2026-09-16); routs; attack 1.1, shield 1.2, chargeDistance 1.2 |
| Khand (`battania`) | default | CavalryDominance; routs; attack 1.2, chargeDistance 1.2 |
| Mordor, Dol Guldur | InfantryMass | InfantryMass and Envelop; Mordor routs, Dol Guldur never; attack 1.5, shield 0.7, shooterError 1.3 |
| Gundabad, Gundabad raiders | InfantryMass | InfantryMass and Envelop; never rout; attack 1.4, shield 0.7, shooterError 1.3 |
| Goblins, Misty Mountains | InfantryMass | InfantryMass and Envelop, `HoldChokePoint` 0.6 (the most the ordering allows: with the numbers they swarm, outnumbered they hold the pass); rout, bravery -10 and -5 |
| Isengard (Uruk-hai) | InfantryMass | ShieldWall both sides (the braced wall is the pike hedge: Line without shields, Square under horse); never rout; attack 1.3; horse stand with `dismount: false` (wargs) |
| Dunland (`empire`) | weighted vanilla | HitAndRun as attacker on `InfantrySkirmish`; high ground, choke points and retreat as defender; routs, bravery -5; attack 1.3, shield 0.8 |
| Dunland raiders | weighted vanilla | HitAndRun and InfantryMass as attacker (a big mob swarms, a small band throws and runs: the numbers term decides); routs |
| Gondor | weighted vanilla | DisciplinedLine; routs, bravery +5; shield 1.3 |
| Dale (`sturgia`) | default | ShieldWall and DisciplinedLine; routs; shield 1.2; horse stand with `dismount: true` (ordinary horses) |
| Harad (`aserai`) | default | MumakVanguard as attacker with `harad_mumakil_rider` routed to `HeavyCavalry`, `RangedHarrassmentOffensive` 1.5; routs; shooterError 0.8, attack 1.1 |
| Umbar | default | Data only: harassment and retreat up; routs; shooterError 0.9, attack 1.1 |

## How to add a behaviour

1. Add the member to `BehaviorKind` (`Doctrines/DoctrinePlan.cs`).
2. Write the pure core in `Doctrines/` (a state machine or a decision table over plain
   inputs) and its tests; the behaviour class only reads the engine and steps it.
3. Write the class in `Hooks/Behaviors/` on `TaomBehaviorBase`: `Weigh()`, `Plan()`,
   `Activate()`, `OnActiveTick()`; a `(Formation)` constructor; no engine mutation outside the
   order setters; stages advance in `OnActiveTick()` only.
4. Add its case in `BehaviorWeightApplier.Apply`: `Ensure(formation, f => new ..., owner)` (the
   three-argument overload, which hands the behaviour the tactic's `Engagement` distances) then
   `SetBehaviorWeight<T>`; `DoctrineSwitchInvariantTests` reads the IL and fails without both.
5. Add `str_formation_ai_sergeant_instruction_behavior_text.<TypeName>` AND
   `str_formation_ai_behavior_text.<TypeName>` (the F6 message, with the vanilla
   `{TROOP_NAMES_BEGIN}`/`{TROOP_NAMES_END}` variables) to `taom_module_strings.xml` and run
   `/localize`; `DoctrinePopupStringsTests` fails on a missing row.
5a. If it is a foot behaviour, pick its target with `EnemyScan.Pick` (the foot-first rule),
   never `CachedClosestEnemyFormation`; if it charges, add it to the `is` chain in
   `TaomTacticBase.HasBattleBeenJoined` so the tactic knows the battle is joined.
6. Name it in a plan; add the A/B cell that shows it.

## How to add a tactic

1. Add the member to `DoctrineTactic` (`Domain/DoctrineTactic.cs`); `DoctrineTacticIdsTests`
   splits vanilla from TAOM ids by the `VanillaTactics` set.
2. Write its `DoctrinePlan` in `Doctrines/DoctrinePlans.cs` and its weight in
   `Doctrines/DoctrineWeights.cs`; add both to `DoctrinePlansTests` and `DoctrineWeightsTests`.
3. Add the class in `Hooks/Tactics/TaomTacticsPhaseC.cs` (a plan, a weight, optionally
   `BeforeApply` for a position) and its case in `TacticFactory.Create`; a new split needs a
   `FormationSplit` member, a `FormationRole` for each new slot, and a case in
   `TaomTacticBase.ManageFormationCounts` and `FormationSlots.Assign`.
4. Add `str_team_ai_tactic_text.<TypeName>` to `taom_module_strings.xml` and run `/localize`.
5. Reference it from `culture_doctrines.json`; `ShippedCultureDoctrinesConfigTests` reads the file,
   and `ShippedDoctrineOrderingTests` needs a canonical army for the new id so the ordering
   against the culture's vanilla rows is checked.
6. A wall built on `BracedDefend` that should take the horse stand calls `RefreshStand(phase)` in
   `BeforeApply` and `OnPhaseTick`, calls the anchor's `Stand()` while `Standing`, overrides
   `OnStandEnded` to call `EndStand()`, copies the anchor's `Facing` and `Width` into
   `DefenseFacing` and `DefenseWidth`, and multiplies its weight by `StandEdge`. Two tests
   hard-code `ShieldWall || TwoLineWall` as the stand-capable set (the stand-weight ordering test
   in `ShippedDoctrineOrderingTests` and the adopting-row test in
   `ShippedCultureDoctrinesConfigTests`), so a third wall means editing both.

## How to give a culture a doctrine

Add a `doctrines.<cultureId>` block to `culture_doctrines.json` (the id is the StringId, see the
re-skin note above). Start from `default`, raise the multipliers of what the culture prefers,
lower the rest, and give it its TAOM tactic at `minTactics` 0; add `morale`, `aggression` and
`formations` blocks when the culture differs from vanilla there (a never-rout culture drops its
`CoordinatedRetreat` row). Restart the game; the `[Doctrine]` registration line shows what each
side received and how its men hold.

To opt the culture into the horse stand, add `"horseStand": { "dismount": <bool> }` to its row
(`true` only when its riders sit on ordinary horses; a culture whose mounts are rams, wargs or other
creatures says `false`). The row must also carry a `ShieldWall` or `TwoLineWall` entry with a
multiplier above 0 for the defender side, or the block does nothing, and
`ShippedCultureDoctrinesConfigTests` fails if an opted-in culture lacks one. The set of opted-in cultures is pinned there by id (today `anduinvale`, `angaladh`,
`dorwinion`, `erebor`, `erebor_warriors`, `eredluin`, `isengard`, `sturgia`), so a new one is added to that test too. A culture with no
row resolves to `default`; one with a row but no block uses its own row and never stands.

---

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/combat-mechanics.md](./combat-mechanics.md)
- [docs/features/dev-console.md](./dev-console.md)
- [docs/features/fell-foes.md](./fell-foes.md)
- [docs/features/race-abilities.md](./race-abilities.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reviews/rca-horse-stand-2026-10-19.md](../reviews/rca-horse-stand-2026-10-19.md)

<!-- backlinks-end -->
