# Culture Doctrine

## Overview

Each culture's AI fights field battles with its own doctrine. At the start of a field battle the
feature replaces the tactic list the engine's team AI chooses from with the list its culture
authored in `culture_doctrines.json`: the nine vanilla tactics with per-culture weight
multipliers, plus eleven TAOM tactics (a Dwarven shield wall and its two-line variant, an
orcish infantry mass and its envelopment, Rohan's cavalry lead and its defending screen, an
Elven archer ring and archer advance, a disciplined line for Gondor, Rhun and Dale, Dunland's
hit-and-run, Harad's mumakil vanguard) built on five TAOM formation behaviours (a braced wall
holding and advancing, a cavalry cycle charge, a throwing-infantry skirmish, an enveloping
wing). The engine still picks the highest-weight tactic every 5 seconds and every formation
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
CultureDoctrineMissionLogic : MissionLogic            (entry point, main thread, 147 lines)
   EarlyStart: toggle, Mission.IsFieldBattle (live read), per AI team:
       TeamCombatantSelector (IBattleCombatants of the side) -> SideProfile (majority culture, best Tactics skill)
       -> DoctrineCatalog.Resolve -> TacticRoster.Build (side + skill filter, Charge guaranteed)
       -> TacticFactory.CreateAll -> team.ClearTacticOptions / AddTacticOption / ResetTactic
   OnMissionTick: [Doctrine] status line every 5 s when the debug toggle is on (TeamTacticProbe)
        |
   nine vanilla wrappers   TacticCharge : TaleWorlds.MountAndBlade.TacticCharge   weight = base * multiplier
   four TAOM tactics       TaomTacticShieldWall / InfantryMass / CavalryDominance / ArcherRing : TaomTacticBase
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
| `TaomTacticShieldWall` | `erebor`, `erebor_warriors`, `isengard`, `sturgia` (Dale) | Defender: infantry `BracedDefend` at the position the high-ground race picks (below) with a low `TacticalCharge`; cavalry `ProtectFlank` + `CavalryScreen` in both phases, never `Flank`. Attacker: infantry `BracedAdvance` with `TacticalCharge` 0.5 then 0.8; cavalry may take a flank once joined | `(Inf + Rng) * 1.2 * advantage / sqrt(RemainingPowerRatio)`, not gated on `IsDefenseApplicable` |
| `TaomTacticTwoLineWall` | `erebor`, `erebor_warriors` (defender only) | 2/1/2/1 split: the front line as the wall, the second line `BracedDefend` twelve paces behind it (facing the enemy) until joined, then `TacticalCharge` 1 over `BracedDefend` 0.5, the reserve committing | the wall's weight times 1.05, 0 under 80 infantry |
| `TaomTacticInfantryMass` | `mordor`, `dolguldur`, `gundabad`, `gundabad_raiders`, `mistymountainorcs`, `goblin`, `dunland_raiders` (attacker) | Always engaged: infantry `Charge` 1.5 + `TacticalCharge` 1, archers `Skirmish`, cavalry `TacticalCharge` + `Flank` | `1.5 * Inf * clamp(members / enemies, 0.5, 2) * sqrt(RemainingPowerRatio)` |
| `TaomTacticEnvelop` | the six orc cultures | 3/1/2/1 split, always engaged: centre `Advance` + `TacticalCharge`, left and right wings `EnvelopWing` (a deep line to a point beside the enemy body on its own side, then `ChargeToTarget`), archers `Skirmish`, cavalry `TacticalCharge` + `Flank` | the mass's weight times 1.1; 0 under 1.2x numbers or 60 infantry |
| `TaomTacticCavalryDominance` | `vlandia` (Rohan), `khuzait` (Rhun), `battania` (Khand) | 1/1/1/1 split, 7 s join threshold: cavalry `Advance` + `Vanguard`, then `CycleCharge` 1.2 + `TacticalCharge` 0.8 + `Flank` 0.8; infantry `Advance` behind | vanilla `FrontalCavalryCharge` formula times 1.3 |
| `TaomTacticEoredScreen` | `vlandia` (defender only) | 1/1/2/1 split, 7 s join: both cavalry blocks `CavalryScreen` + `ProtectFlank`, infantry `Defend` where the race puts it; once joined the blocks `CycleCharge` 1.2 + `Flank` 0.8 + `ProtectFlank` 0.5 | `Cav * 1.6 * advantage / sqrt(RemainingPowerRatio)`, defender only |
| `TaomTacticArcherRing` | `lindon`, `lothlorien`, `mirkwood`, `mirkwood_stalkers`, `rivendell` (defender only) | infantry `DefensiveRing` on a runtime `TacticalPosition` at the position the high-ground race picks (the archers' high ground, or where the infantry stands), facing the enemy; archers `FireFromInfantryCover` + `Skirmish` 0.5 under volley control; cavalry guards the flanks; the ring holds in both phases | vanilla `DefensiveRing` formula (`min(Inf, Rng) * 3 * advantage / sqrt(RemainingPowerRatio)`), 0 for an attacker, when out-shot (`IsDefenseApplicable`), or when the infantry cannot ring the archers' square (`RingGeometry.Fits`, vanilla's radius test) |
| `TaomTacticArcherAdvance` | the five Elven cultures (attacker only) | infantry `CautiousAdvance` behind the archers' `SkirmishLine` + `ScreenedSkirmish`, cavalry `ProtectFlank` + `CavalryScreen`; once joined infantry `Advance` + `TacticalCharge` 0.7, archers `ScreenedSkirmish` + `Skirmish`, cavalry may `Flank` 0.7; volley control throughout | `min(Inf, Rng) * 2 * sqrt(RemainingPowerRatio)`, attacker only |
| `TaomTacticDisciplinedLine` | `gondor`, `gondor_soldiers`, `khuzait`, `sturgia` | Defender: infantry `Defend` where the race puts it, archers `SkirmishLine` in front, cavalry `ProtectFlank` + `CavalryScreen`; attacker: `CautiousAdvance` instead of `Defend`. Once joined: infantry `Advance` + `TacticalCharge` 0.8 + `Defend` 0.5, archers `ScreenedSkirmish` + `Skirmish`, cavalry `TacticalCharge` + `Flank` + `ProtectFlank` 0.5 | `(Inf + Rng) * 1.5 * advantage / sqrt(RemainingPowerRatio)` |
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
lose it and it forms where it stands (`BehaviorDefend` at its own position). While marching the
race is re-checked once a second on the tactic's tick and a lost race re-forms on the spot;
once arrived (within `BehaviorDefend`'s 10 m), or once the closest enemy is inside
`BehaviorHoldHighGround`'s lock radius (`max(0.8 * archers' missile range, 30 m)`), the position
is locked. All of it is `HighGroundRace` (pure, `HighGroundRaceTests`) over eight cached engine
reads; the status line shows the state (`TaomTacticShieldWall:Defend:Marching|Holding|Arrived`). Cultures without a row (Harad, Rhun, Khand, Dale, Umbar, the
bandits) use `default`, which is exactly vanilla's set at multiplier 1.

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

**TAOM behaviours.** Five `BehaviorComponent`s on a shared `TaomBehaviorBase`
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

| Behaviour | Weight | What it does | Engine reads |
|---|---|---|---|
| `BehaviorBracedDefend` | 1 | `BehaviorDefend` with the anti-cavalry square: walks loosely to `DefensePosition`, faces the closest enemy, closes into ShieldWall on arrival (Line without shields, Loose under fire at a distance), and goes Square while a charge is inbound or was in the last 3 s, standing on the point where the brace began. The signal is the engine's `IsUnderCavalryChargeFromFront` OR `CavalryThreat`, a facing-independent scan of every enemy cavalry formation's velocity and ETA: the engine query sees only the single closest significant enemy and only when the wall already faces it within about 41 degrees (`FormationQuerySystem.cs:646-663`), so a horse round a flank behind an infantry screen was invisible to it. Arrangement is re-issued only on a change of stance (`SetArrangementOrder` expires the query cache on every real change) | `IsUnderCavalryChargeFromFront` (2 s), enemy `CachedCurrentVelocity` and medians, `HasShield`, `UnderRangedAttackRatio`, `ClosestSignificantlyLargeEnemyFormation`, `CachedClosestEnemyFormation` |
| `BehaviorBracedAdvance` | 1 | `BehaviorAdvance` with the square: a line at the enemy's main body (its median plus half its depth), ShieldWall or Loose inside vanilla's 10 to 80 m band under fire, Square and stand (on the brace point) under a charge; vanilla only stops and re-forms five metres on when the horse is 30 m out | the same, plus `IsUnderRangedAttack` |
| `BehaviorCycleCharge` | 1 for cavalry with an enemy, else 0 | `CycleChargeMachine`: `ChargeToTarget` the closest significant enemy (Skein); once the formation's average has passed through it, or after 10 s in the melee, ride clear to a reform point beyond the enemy at the charge's stop distance (35 to 60 m; vanilla's 20 to 50 m was written for infantry); gather in a Line facing it (2 s minimum, 8 s maximum, or sooner if the enemy comes within half the stop distance); charge again. It is the machine `BehaviorTacticalCharge` carries and short-circuits for cavalry (`BehaviorTacticalCharge.cs:149-153`). The navmesh penalty is off, as vanilla's charges have it | `CachedFormationIntegrityData` (gathered = deviation under half the average top speed), positions, velocity |
| `BehaviorInfantrySkirmish` | `0.1 + 0.9 * min(throwing share, 0.5) * 2`, 0 once spent | `InfantrySkirmishMachine`, `BehaviorSkirmish`'s dance with vanilla's thresholds: approach to the longest throw, throw (Loose), fall back from foot inside 40 percent of the average throw, and once nobody has thrown for the can't-shoot window (5 to 10 s by size) while inside 60 percent of the average throw, `Committed`: the behaviour weighs 0 and the plan's melee rows take the line. `Committed` survives a plan re-apply (`ResetBehaviorWeights` calls `ResetBehavior` on every behaviour, and a formation-set change elsewhere on the team re-applies the plan): javelins do not come back | `HasThrowingUnitRatio`, `MaximumMissileRange`, `MissileRangeAdjusted`, `MakingRangedAttackRatio`, `CachedCurrentVelocity` |
| `BehaviorEnvelopWing` | 1 with an enemy in view | A deep Line to the point beside the enemy body on the wing's side of the axis from our main formation (`EnvelopGeometry`: half of each width plus 6 m, level with the enemy median), then `ChargeToTarget` within 25 m of the point or 15 m of the enemy. Vanilla's `BehaviorFlank` weighs 0 whenever the enemy's closest formation is the flanker (`BehaviorFlank.cs:51-64`), which for infantry wings is most of the march | `MainFormation`, widths, `Formation.AI.Side` (Left or Right, set by the tactic) |

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

## Configuration

### Config File: `Main/_Module/ModuleData/culture_doctrine/culture_doctrines.json`

Reloads on game restart only (`Reuse.Singleton` provider).

| Field | Type | Description |
|---|---|---|
| `enabled` | bool | File-level switch, ANDed with the MCM toggle |
| `doctrines.<cultureId>.tactics[]` | list | Rows for that culture; `default` is the fallback for every culture without a row and must exist |
| `id` | string | A `DoctrineTactic` name, case-insensitive, without the engine's `Tactic` prefix |
| `multiplier` | float | Factor on the tactic's own weight, 0 to 5 (0 keeps the row but never picks it); reverts to 1 with a warning outside that range or when not finite |
| `minTactics` | int | Commander Tactics skill the side needs for the row, 0 to 300; reverts to 0 with a warning |
| `side` | string | `any` (default), `attacker` or `defender`; reverts to `any` with a warning |
| `doctrines.<cultureId>.morale` | object | `neverRout` (bool) and `bravery` (float, -30 to 30, added to every soldier's starting morale; reverts to 0 with a warning). Absent means vanilla |
| `doctrines.<cultureId>.aggression` | object | `attack`, `shield`, `shooterError`, `chargeDistance`: multipliers 0.25 to 4 on the engine's per-soldier AI values; each reverts to 1 with a warning. Absent means vanilla |
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

The `[MissionPerf]` heartbeat lives on the Battle Load Diagnostics page (`EnableMissionPerfHeartbeat`, on).

### Console

`taom.tactic_status`: for every team with a team AI, the current tactic and each formation's
active behaviour and arrangement. Works in Custom Battle.

## Log lines

```
[Doctrine] team=1 side=Defender player=no culture=erebor doctrine=erebor troops=300 tacticsSkill=0 morale=never-rout/bravery +15 registered=[ShieldWall*1.00, TwoLineWall*1.00, Charge*0.30, ...]
[Doctrine] formation routing subscribed (GetAgentTroopClass_Override)
[Doctrine] t=+65s team=1 side=Defender player=no tactic=TaomTacticShieldWall formations=[Infantry:210 BehaviorBracedDefend:ShieldWall/ShieldWall/FireAtWill, Ranged:60 BehaviorSkirmishLine/Scatter/HoldYourFire, Cavalry:30 BehaviorProtectFlank/Line/FireAtWill] taom=[TaomTacticShieldWall:Defend:Arrived]
[Doctrine] off: vanilla tactics stay (status line on)
[Doctrine] disabled for this mission after <exception>
[MissionPerf] t=+65s frames=300 fps=60.0 avgMs=16.67 p95Ms=25.50 maxMs=40.3 agents=812 active=640 formations=9 gc0=12 gc1=3 gc2=1
```

A TAOM tactic that throws on the AI thread reports `taom=[TaomTacticArcherRing:failed: ...]`
and weighs 0 from then on; the team falls back to its next tactic. A TAOM behaviour shows its
stage or stance after its name (`BehaviorCycleCharge:Reforming`, `BehaviorBracedDefend:Square`,
`BehaviorInfantrySkirmish:Committed`, or `failed: ...`), then the arrangement and the firing
order; an Elven tactic under volley control ends its status with `:Hold` or `:Loose`.

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
| `Main/Features/CultureDoctrine/Hooks/Behaviors/TaomBehaviorBase.cs`, `BehaviorBracedDefend.cs`, `BehaviorBracedAdvance.cs`, `BehaviorCycleCharge.cs`, `BehaviorInfantrySkirmish.cs`, `BehaviorEnvelopWing.cs`, `WallStances.cs` | The five TAOM formation behaviours and their wrapped lifecycle |
| `Main/Features/CultureDoctrine/Doctrines/DoctrinePlan.cs`, `DoctrinePlans.cs`, `DoctrineWeights.cs`, `TacticPhaseMachine.cs` | Pure plans, weights, ring geometry, phase machine |
| `Main/Features/CultureDoctrine/Doctrines/BraceDecision.cs`, `CavalryThreat.cs`, `CycleChargeMachine.cs`, `InfantrySkirmishMachine.cs`, `EnvelopGeometry.cs`, `VolleyDecision.cs` | The pure cores the behaviours and the volley control step |
| `Main/Features/CultureDoctrine/Domain/CultureMorale.cs`, `CultureAggression.cs`, `FormationRouting.cs`, `FormationRoutingRule.cs` | The three per-culture blocks below the tactics, and the routing rule |
| `Main/Features/CultureDoctrine/CultureMoraleService.cs`, `CultureAggressionService.cs` | The two services the models read (catalog lookups behind the sub-toggles) |
| `Main/Features/CultureDoctrine/Models/TaomBattleMoraleModel.cs`, `TaomCustomBattleMoraleModel.cs`, `TaomCustomBattleAgentStatCalculateModel.cs` | The morale seams (campaign, Custom Battle) and the Custom Battle aggression post-pass; the campaign post-pass is in `CareerSystem/Models/TaomAgentStatCalculateModel.cs` |
| `Main/Features/CultureDoctrine/Hooks/AgentAggressionApplier.cs`, `FormationRoutingSubscriber.cs`, `RoutedFormationGuard.cs`, `TeamDoctrineInstaller.cs` | The aggression post-pass on `AgentDrivenProperties`; the troop-class override; the routed formation's exemption from consolidation; one team's roster install |
| `Main/Features/CultureDoctrine/Cheats/CultureDoctrineCheats.cs` | `taom.tactic_status` |
| `Main/Features/MissionPerf/` | `FrameStats`, `MissionPerfLine`, `MissionPerfHeartbeatBehavior` |
| `Main/_Module/ModuleData/culture_doctrine/culture_doctrines.json` | The shipped doctrines |
| `Main/_Module/ModuleData/taom_module_strings.xml` | `str_team_ai_tactic_text.TaomTactic*` and `str_formation_ai_sergeant_instruction_behavior_text.Behavior*` (sergeant popups) |

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
- `FrameStatsTests`

Engine wiring (the `EarlyStart` swap, the tactics on the AI thread, the ring position) is
game-tested per ADR-008; the A/B below is that test.

## Verification: the A/B protocol

Custom Battle, `CultureDoctrineDebug` and `EnableMissionPerfHeartbeat` on, `[MemSample]` on. One
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
- Morale: Gundabad (attacker) vs Gondor at 300 v 450, `CultureDoctrineMorale` on then off; on,
  no Gundabad soldier routs before the last one falls (no `BehaviorRetreat`, no fleeing agents
  in the kill feed); off, the rout comes at the usual point.
- Aggression: the same matchup with `CultureDoctrineAggression` on then off; the on arm's orc
  attack rate is visibly higher and its shield use lower. Casualty ratios recorded, not gated.

Pass: over the steady window (30 s after F6 to the first rout) the median average frame ms on vs
off within 3 percent and p95 within 5 percent at 800 v 800, GC gen 2 not higher; the doctrine
side's current tactic is in its preferred set for at least 70 percent of samples on and at least
30 points less off; at 60 s Erebor's infantry holds the high ground in ShieldWall or Line,
Rohan's cavalry shows `BehaviorTacticalCharge` or `BehaviorFlank`, Lindon's infantry is a Circle
around a Square of archers, attacking Erebor's infantry shows `BehaviorAdvance` and never
`BehaviorCharge` before contact.

## Limitations and open items

- Not smoke-tested in game as of 2026-09-17; the toggle ships off until the A/B passes.
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
- The sergeant popup strings (eleven tactics, five behaviours) are seeded in the 12 language
  files with English text; run `tools/translate_with_claude.py --lang <L> --module TAOM
  --sync-ids --apply` with an API key to translate them.
- `BracedDefend` shows a shield wall only when `FormationQuerySystem.HasShield` (40 percent of
  the formation with shields); a Dwarven line below that stands in Line, which is also what the
  Uruk pikes want. The square is the engine's `SquareFormation`; whether the arrangement
  holds a charge better than a shield wall is what the A/B's Erebor vs Rohirrim cell measures.
- The Elven ring is the engine's circle, not a hollow square; `SquareFormation` builds four
  sides with `MaxRank = (UnitCountOfOuterSide + 1) / 2` and whether its centre stays hollow is
  unverified.
- `TaomTacticBase` is about 260 lines: it is the one place the vanilla lifecycle exists in TAOM
  (vanilla's tactics are 180 to 370 lines each) and the eleven tactics on top of it are 15 to
  45 lines. The slot code that writes `TacticComponent`'s protected fields cannot leave the
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
- Review record: `docs/reviews/rca-culture-doctrine-2026-09-16.md` (seven-agent deep review,
  twelve findings, all fixed). The Codex adversarial pass
  (`docs/reviews/codex-adversarial-culture-doctrine-2026-09-16.prompt.md`) ran out of ChatGPT
  usage after about 110k tokens of decompile work and returned no report; its three interim
  observations (both-side attrition in `TacticCharge`, `ResetTacticalPositions` true for three
  vanilla tactics, the ring centre half a diameter behind its order position, which vanilla's
  own pairing shares) are folded in above. Re-dispatch with the same prompt when credits allow.
- Per-formation flavour is still absent: a Rohan cavalry formation inside a Gondor army fights
  under Gondor's doctrine, morale and aggression (the side's majority culture decides).

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
| Erebor (Dwarves) | ShieldWall, race | ShieldWall on `BracedDefend`/`BracedAdvance` (the anti-cavalry square), TwoLineWall as defender; never rout, bravery +15; shield 1.5, attack 0.9 |
| Lindon, Rivendell, Lothlorien, Mirkwood (Elves) | ArcherRing (defender), weighted `RangedHarrassmentOffensive` (attacker) | ArcherRing and ArcherAdvance, both under volley control; Mirkwood `DefensiveLine` 0.8 (the forest bias vanilla's scoring already carries); never rout, bravery +10; shooterError 0.5 |
| Rohan (`vlandia`) | CavalryDominance | CavalryDominance with `CycleCharge` once joined, EoredScreen as defender; routs, bravery +5; attack 1.2, chargeDistance 1.3 |
| Rhun (`khuzait`) | default | CavalryDominance and DisciplinedLine (heavy cavalry like Rohan, better foot and bow, few horse archers: Mike, 2026-09-16); routs; attack 1.1, shield 1.2, chargeDistance 1.2 |
| Khand (`battania`) | default | CavalryDominance; routs; attack 1.2, chargeDistance 1.2 |
| Mordor, Dol Guldur | InfantryMass | InfantryMass and Envelop; Mordor routs, Dol Guldur never; attack 1.5, shield 0.7, shooterError 1.3 |
| Gundabad, Gundabad raiders | InfantryMass | InfantryMass and Envelop; never rout; attack 1.4, shield 0.7, shooterError 1.3 |
| Goblins, Misty Mountains | InfantryMass | InfantryMass and Envelop, `HoldChokePoint` 0.6 (the most the ordering allows: with the numbers they swarm, outnumbered they hold the pass); rout, bravery -10 and -5 |
| Isengard (Uruk-hai) | InfantryMass | ShieldWall both sides (the braced wall is the pike hedge: Line without shields, Square under horse); never rout; attack 1.3 |
| Dunland (`empire`) | weighted vanilla | HitAndRun as attacker on `InfantrySkirmish`; high ground, choke points and retreat as defender; routs, bravery -5; attack 1.3, shield 0.8 |
| Dunland raiders | weighted vanilla | HitAndRun and InfantryMass as attacker (a big mob swarms, a small band throws and runs: the numbers term decides); routs |
| Gondor | weighted vanilla | DisciplinedLine; routs, bravery +5; shield 1.3 |
| Dale (`sturgia`) | default | ShieldWall and DisciplinedLine; routs; shield 1.2 |
| Harad (`aserai`) | default | MumakVanguard as attacker with `harad_mumakil_rider` routed to `HeavyCavalry`, `RangedHarrassmentOffensive` 1.5; routs; shooterError 0.8, attack 1.1 |
| Umbar | default | Data only: harassment and retreat up; routs; shooterError 0.9, attack 1.1 |

## How to add a behaviour

1. Add the member to `BehaviorKind` (`Doctrines/DoctrinePlan.cs`).
2. Write the pure core in `Doctrines/` (a state machine or a decision table over plain
   inputs) and its tests; the behaviour class only reads the engine and steps it.
3. Write the class in `Hooks/Behaviors/` on `TaomBehaviorBase`: `Weigh()`, `Plan()`,
   `Activate()`, `OnActiveTick()`; a `(Formation)` constructor; no engine mutation outside the
   order setters; stages advance in `OnActiveTick()` only.
4. Add its case in `BehaviorWeightApplier.Apply`: `Ensure(formation, f => new ...)` then
   `SetBehaviorWeight<T>`; `DoctrineSwitchInvariantTests` reads the IL and fails without both.
5. Add `str_formation_ai_sergeant_instruction_behavior_text.<TypeName>` to
   `taom_module_strings.xml` and run `/localize`.
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

## How to give a culture a doctrine

Add a `doctrines.<cultureId>` block to `culture_doctrines.json` (the id is the StringId, see the
re-skin note above). Start from `default`, raise the multipliers of what the culture prefers,
lower the rest, and give it its TAOM tactic at `minTactics` 0; add `morale`, `aggression` and
`formations` blocks when the culture differs from vanilla there (a never-rout culture drops its
`CoordinatedRetreat` row). Restart the game; the `[Doctrine]` registration line shows what each
side received and how its men hold.
