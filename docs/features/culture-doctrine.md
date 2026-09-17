# Culture Doctrine

## Overview

Each culture's AI fights field battles with its own doctrine. At the start of a field battle the
feature replaces the tactic list the engine's team AI chooses from with the list its culture
authored in `culture_doctrines.json`: the nine vanilla tactics with per-culture weight
multipliers, plus four TAOM tactics (a Dwarven shield wall, an orcish infantry mass, Rohan's
cavalry lead, an Elven archer ring). The engine still picks the highest-weight tactic every
5 seconds and every formation still picks its own behaviour every 0.5 seconds; the doctrine
changes what is on the menu and how much each option weighs. Sieges are untouched. Issue #608.

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

What each doctrine looks like in the field comes from what the vanilla behaviours do with
arrangement: `BehaviorDefend` goes ShieldWall at its position when the formation has shields
(`HasShieldUnitRatio >= 0.4`), `BehaviorAdvance` goes ShieldWall under fire, `BehaviorDefensiveRing`
forms a Circle sized around the archers' `BehaviorFireFromInfantryCover` Square,
`BehaviorVanguard` rides ahead of the infantry.

| Tactic | Cultures | Plan | Weight |
|---|---|---|---|
| `TaomTacticShieldWall` | `erebor`, `erebor_warriors` | Defender: infantry `Defend` at the navmesh high ground (`HighGroundCloseToForeseenBattleGround`) with a low `TacticalCharge`; the position is re-read at a phase apply only while the closest enemy is beyond `max(0.8 * archers' missile range, 30 m)`, `BehaviorHoldHighGround`'s own lock rule, so an Engage apply cannot walk the wall into an enemy already on it; cavalry `ProtectFlank` + `CavalryScreen` in both phases, never `Flank`. Attacker: infantry `Advance` with `TacticalCharge` 0.5 then 0.8; cavalry may take a flank once joined | `(Inf + Rng) * 1.2 * advantage / sqrt(RemainingPowerRatio)`, not gated on `IsDefenseApplicable` |
| `TaomTacticInfantryMass` | `mordor`, `dolguldur`, `gundabad`, `gundabad_raiders`, `mistymountainorcs`, `goblin`, `isengard` | Always engaged: infantry `Charge` 1.5 + `TacticalCharge` 1, archers `Skirmish`, cavalry `TacticalCharge` + `Flank` | `1.5 * Inf * clamp(members / enemies, 0.5, 2) * sqrt(RemainingPowerRatio)` |
| `TaomTacticCavalryDominance` | `vlandia` (Rohan) | 1/1/1/1 split, 7 s join threshold: cavalry `Advance` + `Vanguard`, then `TacticalCharge` 1.2 + `Flank`; infantry `Advance` behind | vanilla `FrontalCavalryCharge` formula times 1.3 |
| `TaomTacticArcherRing` | `lindon`, `lothlorien`, `mirkwood`, `mirkwood_stalkers`, `rivendell` (defender only) | infantry `DefensiveRing` on a runtime `TacticalPosition` at the archers' high ground, facing the enemy; archers `FireFromInfantryCover` + `Skirmish` 0.5; cavalry guards the flanks; the ring holds in both phases | vanilla `DefensiveRing` formula (`min(Inf, Rng) * 3 * advantage / sqrt(RemainingPowerRatio)`), 0 for an attacker, when out-shot (`IsDefenseApplicable`), or when the infantry cannot ring the archers' square (`RingGeometry.Fits`, vanilla's radius test) |

The runtime `TacticalPosition` is what `BehaviorDefensiveRing` reads (position and direction
only); vanilla's `TacticDefensiveRing` constructs them the same way (`TacticDefensiveRing.cs:180`),
so no scene entity is needed. Cultures without a row (Harad, Rhun, Khand, Dale, Umbar, the
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

### What the plan rejected, and why

- Harmony postfixes on the nine `GetTacticWeight` methods: the wrappers do the same with no
  patch category and no per-call lookup on the AI thread.
- A `TeamAIGeneral` subclass swapped in with `Team.AddTeamAI`: it replaces the component and
  its tactic list, and behaviours registered from `OnUnitAddedToFormationForTheFirstTime` miss
  formations populated by `TransferUnits` during a split.
- Per-agent behaviour trees: the wrong layer (`docs/reviews/rca-warg-clip-on-horse-2026-09-13.md`).
- `ITeamAdapter` and a formation command adapter: nothing needs them yet; the tactics take a
  pure snapshot and the applier is one switch.

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

The `[MissionPerf]` heartbeat lives on the Battle Load Diagnostics page (`EnableMissionPerfHeartbeat`, on).

### Console

`taom.tactic_status`: for every team with a team AI, the current tactic and each formation's
active behaviour and arrangement. Works in Custom Battle.

## Log lines

```
[Doctrine] team=1 side=Defender player=no culture=erebor doctrine=erebor troops=300 tacticsSkill=0 registered=[ShieldWall*1.00, Charge*0.30, ...]
[Doctrine] t=+65s team=1 side=Defender player=no tactic=TaomTacticShieldWall formations=[Infantry:210 BehaviorDefend/ShieldWall, Ranged:60 BehaviorSkirmishLine/Scatter, Cavalry:30 BehaviorProtectFlank/Line] taom=[TaomTacticShieldWall:Defend]
[Doctrine] off: vanilla tactics stay (status line on)
[Doctrine] disabled for this mission after <exception>
[MissionPerf] t=+65s frames=300 fps=60.0 avgMs=16.67 p95Ms=25.50 maxMs=40.3 agents=812 active=640 formations=9 gc0=12 gc1=3 gc2=1
```

A TAOM tactic that throws on the AI thread reports `taom=[TaomTacticArcherRing:failed: ...]`
and weighs 0 from then on; the team falls back to its next tactic.

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
| `Main/Features/CultureDoctrine/Hooks/Tactics/TaomTacticBase.cs`, `TaomTactics.cs` | The shared lifecycle and the four TAOM tactics |
| `Main/Features/CultureDoctrine/Hooks/Tactics/BehaviorWeightApplier.cs`, `TeamQuerySnapshotFactory.cs`, `TacticFactory.cs` | The three engine-type switches |
| `Main/Features/CultureDoctrine/Doctrines/DoctrinePlan.cs`, `DoctrinePlans.cs`, `DoctrineWeights.cs`, `TacticPhaseMachine.cs` | Pure plans, weights, ring geometry, phase machine |
| `Main/Features/CultureDoctrine/Cheats/CultureDoctrineCheats.cs` | `taom.tactic_status` |
| `Main/Features/MissionPerf/` | `FrameStats`, `MissionPerfLine`, `MissionPerfHeartbeatBehavior` |
| `Main/_Module/ModuleData/culture_doctrine/culture_doctrines.json` | The shipped doctrines |
| `Main/_Module/ModuleData/taom_module_strings.xml` | `str_team_ai_tactic_text.TaomTactic*` (sergeant popup) |

## Tests

- `DoctrineTacticIdsTests`, `SideProfileTests` (majority, side skill floor), `TacticRosterTests`
  (vanilla-equivalence at skill 0/20/50, Charge guarantee, side and skill filters, dedupe,
  ensured rows), `TeamCombatantSelectorTests` (the engine's ally-team rule and the side-wide skill,
  over fakes of `IBattleCombatant`)
- `CultureDoctrineConfigProviderTests` (one test per validation rule, NaN included),
  `ShippedCultureDoctrinesConfigTests` (the shipped file parses clean, keys the re-skinned
  cultures by vanilla id, every culture carries its TAOM tactic on the right side)
- `DoctrinePlansTests` (no duplicates, roles match the split, the doctrine intent per plan),
  `DoctrineWeightsTests` (zero cases, monotonicity, clamps, NaN and zero-power inputs),
  `ShippedDoctrineOrderingTests` (every shipped TAOM row beats its vanilla neighbours by the
  sticky factor), `TacticPhaseMachineTests`
- `DoctrineSwitchInvariantTests` (`BindingVerification`): the applier's and the factory's switch
  bodies read as IL, every `BehaviorKind` reaching a `SetBehaviorWeight<T>` whose T
  `TeamAIGeneral` registers, every `DoctrineTactic` reaching its named constructor; both switches
  throw on an unmapped member
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

Pass: over the steady window (30 s after F6 to the first rout) the median average frame ms on vs
off within 3 percent and p95 within 5 percent at 800 v 800, GC gen 2 not higher; the doctrine
side's current tactic is in its preferred set for at least 70 percent of samples on and at least
30 points less off; at 60 s Erebor's infantry holds the high ground in ShieldWall or Line,
Rohan's cavalry shows `BehaviorTacticalCharge` or `BehaviorFlank`, Lindon's infantry is a Circle
around a Square of archers, attacking Erebor's infantry shows `BehaviorAdvance` and never
`BehaviorCharge` before contact.

## Limitations and open items

- Not smoke-tested in game as of 2026-09-16; the toggle ships off until the A/B passes.
- The sergeant popup strings for the four TAOM tactics are seeded in the 12 language files with
  English text; run `tools/translate_with_claude.py --lang <L> --module TAOM --sync-ids --apply`
  with an API key to translate them.
- ShieldWall shows a shield wall only when `FormationQuerySystem.HasShield` (40 percent of the
  formation with shields); a Dwarven line below that stands in Line. A TAOM `BehaviorComponent`
  is the fix if the A/B shows it (Phase C).
- The Elven ring is the engine's circle, not a hollow square; `SquareFormation` builds four
  sides with `MaxRank = (UnitCountOfOuterSide + 1) / 2` and whether its centre stays hollow is
  unverified (Phase C research).
- `TaomTacticBase` is 221 lines: it is the one place the vanilla lifecycle exists in TAOM
  (vanilla's tactics are 180 to 370 lines each) and the four tactics on top of it are 15 to 40
  lines. The formation-slot code cannot leave the class because it writes `TacticComponent`'s
  protected fields.
- The status line's console twin (`taom.tactic_status`) can run while an async tick is in flight;
  every read is a reference, an int or a 4-byte enum, so the worst case is one stale line.
- Review record: `docs/reviews/rca-culture-doctrine-2026-09-16.md` (seven-agent deep review,
  twelve findings, all fixed). The Codex adversarial pass
  (`docs/reviews/codex-adversarial-culture-doctrine-2026-09-16.prompt.md`) ran out of ChatGPT
  usage after about 110k tokens of decompile work and returned no report; its three interim
  observations (both-side attrition in `TacticCharge`, `ResetTacticalPositions` true for three
  vanilla tactics, the ring centre half a diameter behind its order position, which vanilla's
  own pairing shares) are folded in above. Re-dispatch with the same prompt when credits allow.
- Vanilla's `ManageFormationCounts` still merges formations to at most 1/1/2/1 or 1/1/1/1 when
  any vanilla tactic is current, so formation routing (Phase D) needs TAOM tactics that override
  it, or a routing doctrine that registers no vanilla tactic.

## Roadmap after this slice

Phase C: TAOM `BehaviorComponent`s (Dunland throwing-infantry skirmish keyed on
`HasThrowingUnitRatio`, shield wall below the 40 percent threshold, aggressive high ground),
ensured lazily by the tactic that weights them (`formation.AI.GetBehavior<T>() ?? AddAiBehavior`).
Phase D: `Mission.GetAgentTroopClass_Override` routing into `FormationClass.Skirmisher` /
`HeavyInfantry` / `HeavyCavalry`, a per-culture aggression profile on `AgentDrivenProperties`
in `TaomAgentStatCalculateModel.UpdateAgentStats` (`AIAttackOnDecideChance`,
`AiDefendWithShieldDecisionChanceValue`, `AiChargeHorsebackTargetDistFactor`; runs at spawn, never
per frame), cultural bravery in `BattleMoraleModel`. Phase E: per-formation culture weights inside
mixed armies (the `BannerBearerAssignmentMissionLogic.ResolveFormationCultureId` shape).

## How to add a tactic

1. Add the member to `DoctrineTactic` (`Domain/DoctrineTactic.cs`); `DoctrineTacticIdsTests`
   splits vanilla from TAOM ids by the `VanillaTactics` set.
2. Write its `DoctrinePlan` in `Doctrines/DoctrinePlans.cs` and its weight in
   `Doctrines/DoctrineWeights.cs`; add both to `DoctrinePlansTests` and `DoctrineWeightsTests`.
3. Add the class in `Hooks/Tactics/TaomTactics.cs` (a plan, a weight, optionally `BeforeApply`
   for a position) and its case in `TacticFactory.Create`.
4. Add `str_team_ai_tactic_text.<TypeName>` to `taom_module_strings.xml` and run `/localize`.
5. Reference it from `culture_doctrines.json`; `ShippedCultureDoctrinesConfigTests` reads the file.

## How to give a culture a doctrine

Add a `doctrines.<cultureId>` block to `culture_doctrines.json` (the id is the StringId, see the
re-skin note above). Start from `default`, raise the multipliers of what the culture prefers,
lower the rest, and give it its TAOM tactic at `minTactics` 0. Restart the game; the
`[Doctrine]` registration line shows what each side received.
