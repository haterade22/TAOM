# Bannerlord formations + team AI — Formation / Team / TeamAIComponent (Phase 13)

> **One process, traced from the decompile** (`TaleWorlds.MountAndBlade`, v1.4.5): the in-battle troop-organisation
> system — how agents are grouped into **`Formation`s**, owned by a **`Team`**, arranged by an
> **`IFormationArrangement`**, moved by **orders**, and commanded by a **`TeamAIComponent`** (tactics). This is the
> system TAOM's SmartCavalryAI / MixedFormations / CompanionTactics all patch, and the prime suspect for the
> **riderless-spider DivideByZero-in-formation** crash. Part of the phased engine study; builds on Phase 4 (mission),
> Phase 1 (agents), Phase 12 (IDetachment).

## WHAT it is

In a battle, agents don't move individually — they belong to a **`Formation`** (a line/column/etc. of one troop
class). Each side is a **`Team`**, which owns up to 8 formations + an optional **`TeamAIComponent`** (the side's brain:
picks a **tactic**, which issues **orders** to the formations). A formation's physical layout is an
**`IFormationArrangement`** (computes each unit's local slot). Orders (`MovementOrder`/`FacingOrder`/`ArrangementOrder`)
say *where/which-way/how-spread*. The whole thing drives the native per-frame agent-positioning math.

## HOW it works

### `Formation : IFormation` (sealed — Formation.cs:14)
One group of agents. Key surface:
- **Geometry:** `Width` (:406, settable), `Depth` (:202), `Interval`/`Distance` (unit spacing, :504/:528),
  `UnitDiameter` (:208), `MinimumWidth`/`MaximumWidth` (:204/:206), `Direction`/`CurrentDirection` (:174/:210),
  `OrderPosition`/`CurrentPosition` (:196/:540).
- **Counts:** **`CountOfUnits => Arrangement.UnitCount + _detachedUnits.Count`** (:182); plus
  `CountOfUnitsWithoutDetachedOnes` (:188), `CountOfDetachedUnits` (:184), `CountOfDetachableNonPlayerUnits` (:194).
- **Orders:** `FacingOrder`/`ArrangementOrder` (:380/:382), **`SetMovementOrder(MovementOrder input)`** (:685 — the
  method TAOM patches), `SetControlledByAI`, `SetPositioning`.
- **Arrangement:** `Arrangement` (`IFormationArrangement`, :454), `RepresentativeClass`/`PhysicalClass`/`LogicalClass`
  (`FormationClass`, :170/:478/:420).
- **Detached units:** `_detachedUnits`/`_looseDetachedUnits` (:138/:142) — agents *in* the formation but *outside* the
  arrangement grid (skirmishing, using a machine, etc.).

### `Team : IMissionTeam` (Team.cs:12)
One side. `Side` (`BattleSideEnum` Attacker/Defender, :30); **`FormationsIncludingEmpty`** (`MBList<Formation>(8)` —
always 8 slots by `FormationClass`, :34/:268) and **`FormationsIncludingSpecialAndEmpty`** (10: the same eight plus
General (8) and Bodyguard (9), :36/:279-290; the list the engine's own enemy walks use,
`Formation.CacheClosestEnemyFormation` :1496-1519 and `TeamQuerySystem.MedianTargetFormation`, and the one a TAOM scan
of ENEMY formations must use too, since `GeneralsAndCaptainsAssignmentLogic` fills the bodyguard in an ordinary field
battle; own-team splits and snapshots stay on the eight); **`TeamAI`** (`TeamAIComponent`, :38); `GeneralsFormation`/
`BodyGuardFormation` (:112/:114). `GetFormation(FormationClass)` (:613); `AddTeamAI(...)` (:447).
**Friend/foe:** `IsEnemyOf(otherTeam)` (:629) and **`IsFriendOf(otherTeam) => !MBTeam.IsEnemyOf(...)`** (:634) — both
delegate to the **native `MBTeam`**.

### `IFormationArrangement` (IFormationArrangement.cs:8)
The layout strategy: `Width`/`Depth`/`UnitCount`/`RankCount` (:10-32), `IntervalMultiplier`/`DistanceMultiplier`
(:26/:28), `GetLocalPositionOfUnitOrDefault(unit)` (:60-64 — a unit's slot), `CreateNewPosition(unitIndex)` (:96),
`RemoveUnit`/batch add-remove. Concrete arrangements (LineFormation, ColumnFormation, …) compute the grid.

### `TeamAIComponent` (abstract — TeamAIComponent.cs:12)
The side's AI. Holds `_availableTactics` (`List<TacticComponent>`, :36) + `_currentTactic` (:48); each tick it picks
the **max-weight tactic** (`MaxBy(weight × 1.5 sticky-bonus-if-current)`, :301) and that tactic drives
per-formation **`BehaviorComponent`**s, which call `Formation.SetMovementOrder`/etc. `AddTacticOption` (:129),
`StrategicArea`s (:54).

### The tactic layer in v1.5.3 (verified 2026-09-16 for #608)

Re-read against the v1.5.3 dump; none of these types changed between 1.4.8 and 1.5.3 (file-list and
content diff of `Tactic*`, `Behavior*`, `TeamAI*`, `FormationAI`; only the two query systems renamed
their time source and ranged-hit members).

| Fact | Where |
|---|---|
| Field battles: every team gets `new TeamAIGeneral(mission, team)`; the tactic set is registered per team gated ONLY by the side's best commander Tactics skill (`Charge`; >= 20 `FullScaleAttack` + defender `DefensiveEngagement`/`DefensiveLine` or attacker `RangedHarrassmentOffensive`; >= 50 `FrontalCavalryCharge` + defender `DefensiveRing`/`HoldChokePoint` or attacker `CoordinatedRetreat`), then `QuerySystem.Expire()` + `ResetTactic()`. Nothing reads `Culture`. | `MissionCombatantsLogic.cs:109-238` |
| `MakeDecision` every hard-coded 5 s: `MaxBy(GetTacticWeight() * (current ? 1.5 : 1))`, forced `TacticCharge` (tested with `is`) when no enemy formation remains, `FirstOrDefault` fallback; `TickOccasionally` every 1 s drives the current tactic; the sergeant popup keys `str_team_ai_tactic_text` on `GetType().Name` | `TeamAIComponent.cs:196-344` |
| `_availableTactics` is a private unlocked `List` with no getter; `Team.AddTacticOption / RemoveTacticOption(Type) / ClearTacticOptions / ResetTactic` are public forwarders; `Team.AddTeamAI` is public and replaces the component and its list | `Team.cs:427-477`, `TeamAIComponent.cs:129-142` |
| Every field tactic is `public class`, unsealed, `(Team)`-constructible, `protected internal override float GetTacticWeight()`; the base exposes protected `_mainInfantry/_archers/_leftCavalry/_rightCavalry/_rangedCavalry`, `IsTacticReapplyNeeded`, `AreFormationsCreated`, `AssignTacticFormations1121`, `ManageFormationCounts(int,int,int,int)`, virtual `ManageFormationCounts()`, `CheckAndSetAvailableFormationsChanged()`, `ChooseAndSortByPriority`, static `CalculateNotEngagingTacticalAdvantage`, public static `SetDefaultBehaviorWeights` | `TacticComponent.cs` |
| The three defensive tactics return 0 unless `IsDefenseApplicable`, which is false for any non-Defender; `DefensiveLine`/`HoldChokePoint` need scene `TacticalPosition`s, `DefensiveRing` an insurmountable one; `DefensiveEngagement` and `BehaviorHoldHighGround` use the navmesh `FormationQuerySystem.HighGroundCloseToForeseenBattleGround` (Vec2, 10 s cache) and need no scene entity | `TeamAIComponent.cs:215-247`, `TacticDefensiveRing.cs:115-143` |
| `TacticalPosition` has a public runtime ctor `(WorldPosition, Vec2, float width, float slope, bool isInsurmountable, ...)` that vanilla itself uses; `TeamAIComponent.TacticalPositions` is a public mutable list | `TacticalPosition.cs:105`, `TacticDefensiveRing.cs:180` |
| `FormationAI.Tick` every 0.5 s (staggered), winner = `GetAIWeight() * WeightFactor` with 1.2 to 2.0 hysteresis over 10 s, orders applied only if `IsAIControlled`; `SetBehaviorWeight<T>` throws `MBException` for an unregistered T; `GetBehavior<T>`/`SetBehaviorWeight<T>` match with `is T`, so a TAOM behaviour derives from `BehaviorComponent` directly | `FormationAI.cs:120-155, 175, 245, 286` |
| `TeamAIGeneral.OnUnitAddedToFormationForTheFirstTime` registers the 24 field behaviours per formation on its first unit (from `Formation.AddUnit`, `TeamAIGeneral.cs:63-86`); a formation populated by `TransferUnits` during a split gets them too, because `TransferUnitsAux` goes through `Agent.Formation`'s setter, which calls `AddUnit` (the earlier "misses transfer-populated formations" reading was wrong; Phase C review 2026-09-17). A TAOM behaviour is never among the 24: `BehaviorWeightApplier.Ensure<T>` adds it the first time a plan names it | `TeamAIGeneral.cs`, `Formation.cs:2326`, `TacticComponent.cs:306,332,427` |
| THREAD: `Team.Tick` -> `TeamAI.Tick` -> tactics -> `Formation.Tick` -> `FormationAI.Tick` -> behaviours all run on the async AI thread; `EarlyStart`/`AfterStart`/`OnDeploymentFinished`/`OnAgentBuild` are main thread. `DeploymentMissionController` holds `AllowAiTicking = false` through deployment and `Team.Tick` gates on it, so the first decision sees every initial agent | `Mission.cs:3757-3760`, `Team.cs:591-606`, `harmony-patches.md` thread table |
| `MissionCombatantsLogic.GetAllCombatants()`, `SupportsAllyTeamOnPlayerSide(out)`, `GetCultureForPlayerSide()` are public; `IBattleCombatant` carries `Side`, `BasicCulture`, `GetNumberOfMissionReadyTroops()`, `GetTacticsSkillAmount()`; `CustomBattleCombatant.GetTacticsSkillAmount` is the roster max (0 for TAOM troops) | `MissionCombatantsLogic.cs:51-75, 245`, `CustomBattleCombatant.cs:63-70` |
| `Mission.GetAgentTroopClass_Override` (public event, consulted first) remains unsubscribed in vanilla and TAOM: the patch-free seam for routing troops into `FormationClass.Skirmisher/HeavyInfantry/LightCavalry/HeavyCavalry` | `Mission.cs:1564, 2557` |

TAOM's consumer: `docs/features/culture-doctrine.md` (#608).

### Measured tactic behaviour (v1.5.5) [yotthani]

What the default AI does in whole field battles. Source for every figure here: yotthani, MithrilForge
`docs/engine/battle-behaviour-measured.md`@0a2f4761, except the second choke-point test, which is @ae7834aa, measured with TAOM 2.0.34 loaded (plus Armory
and DualWield), v1.5.5, custom battles, both sides AI (about 120 recorded battles of 100 to 1500
soldiers, October 2026). Marks as upstream: **[M]** measured in recordings, **[C]** read in the
decompiled code, **[I]** inference, not checked.

| Fact | Mark, n | Where |
|---|---|---|
| The first decision is at 0.5 s and picks `FullScaleAttack` (weight about 1.0): every defensive tactic weighs 0 before its positions are evaluated. This is why the same defender sometimes holds and sometimes attacks | [M], no battle count given | upstream lines 79-81 |
| From 5 s `HoldChokePoint` weighs 0 or 1.35 to 1.93 depending on the start layout, and takes over only above about 1.49 (1.5 x 0.99). `DefensiveEngagement` (0.77 to 1.12) never takes over. A fresh choice at 6 s without the 1.5 bonus picked the same tactic in 8 of 8 battles, so `FullScaleAttack` is the real maximum at that moment | [M], n=8 | upstream lines 81-83 |
| The 1.5x bar: a switch needs the new weight above 1.5 x the current tactic's weight, and the current tactic also carries the 1.5 bonus in the `MaxBy`. Re-read on v1.5.5 by TAOM: `TeamAIComponent.cs:301` (`MaxBy`) and `:316-322` (the switch test) | [C], TAOM re-read | upstream lines 68-69 |
| Start layout beats tactic. Distance from the defender's start to the nearest choke point against the outcome (scene `battle_terrain_biome_005`): within 70 m the defender won 7 of 14, 70 to 160 m 2 of 9, beyond 160 m 1 of 16, whether or not its tactic was forced. Forcing `HoldChokePoint` from a bad layout won 1 of 5 | [M], n=39 (14, 9, 16) | upstream lines 112-118 |
| Teleporting the defender to a choke point (19 battles, 300 m radius) helped far less than the layout correlation suggests: 4 of 13 wins on the nearest choke point (4 of 9 when moved at most 150 m, 0 of 4 when moved 188 to 208 m, where the AI did not run `HoldChokePoint`), 1 of 6 on the nearest high ground as control, 3 of 13 unchanged. Yotthani's first text (@0a2f4761 lines 133-135) added that individual choke points differ and that the AI's own `HoldChokePoint` weight guides better than the type; **that sentence is gone at @ae7834aa** (line 133 now ends at "suggests"; see the next row) | [M], n=19; does not count (yotthani, @d09f44a4) | upstream lines 120-135. **Yotthani later wrote that the 30 battles of 15 and 16 Oct "do not count: the soldiers were mostly not moved" (@d09f44a4 section 6); that these 19 are among them is TAOM's reading, by their dates. See "Retraction" below.** |
| Second test, 2026-10-16 (11 battles, `deploy=Defender=BestChokePoint=300`, the choke point `TacticHoldChokePoint` rates highest, with the distance factor left out because the formation is placed there, @ae7834aa lines 135-138): the defender won 1 of 11 (9 placed, 2 with no eligible point within 300 m), against 3 of 13 unchanged and 4 of 13 on the nearest choke point; both tests together 5 of 22. Yotthani's reading: the natural layout correlation comes with the start layout as a whole (where the attacker spawns, which way it comes), not with the position the infantry stands on, and "this rejects 'choke point placement is a lever', it does not prove the layout explanation". Every placed position scored 1.5, the cap of the width-fit factor on these flat positions, so the score could not tell them apart | [M], n=22, small; does not count (yotthani, @d09f44a4); 100-man formations (upstream line 140; the mirror setup of line 99 is section 5's method and is not restated for this test), v1.5.5, TAOM 2.0.34 with Armory and DualWield | upstream `battle-behaviour-measured.md`@ae7834aa lines 133-152. **Yotthani later wrote that the 30 battles of 15 and 16 Oct "do not count: the soldiers were mostly not moved" (@d09f44a4 section 6); that these 11 are among them is TAOM's reading, by their dates. See "Retraction" below.** |
| **TAOM-verified, v1.5.5 `TacticHoldChokePoint.cs`:** a position is eligible only if its distance gate passes (it is refused when `num > 20f && num > num2 * 0.5f`, `:139-144`, `num` the team's distance to the position, `num2` the enemy's distance to the main infantry), the infantry's `MaximumWidth >= Width` (`:145`), and it faces the enemy (dot >= 0.5, or \|dot\| >= 0.5 when `IsInsurmountable`, `:149-154`). Score = slope `Lerp(1, 1.5)` (`:163`) x fit `Lerp(0.67, 1.5)` of formation width over position width (`:166`) x cliff with archers (`:168-170`) x distance `Lerp(0.7, 1)` (`:172-173`), multiplied at `:174`. Yotthani's own `ChokeScore` (`HoN/PerfProbe/Integration/BehaviourRecorder.cs`@2ff002be, about lines 352-370) leaves out the distance factor on purpose ("we put the formation there", lines 354-355), and with it the distance gate, and uses its own enemy centroid (the mean of all active enemy humans, lines 379-383): a documented simplification for a formation already standing on the position | [C], TAOM re-read | v1.5.5 source |
| Formation behaviour is re-chosen every 0.5 s; the running behaviour is weighted x2.0 for 5 s, then x1.2. Everything stands for 4 to 8 s, the attacker then advances as a closed body at 2.4 m/s, and the first formation charges when the centres are 13 to 17 m apart (10 to 23 m) | [C] and [M], no n given | upstream lines 74-78 |

**Retraction: the two teleport tests above mostly did not move the soldiers** (yotthani, MithrilForge
`docs/engine/battle-behaviour-measured.md`@d09f44a4 section 6, evidence: measured in game plus code reading, engine
1.5.x as that file states; the section does not name a patch version). Yotthani wrote that the "30 battles run with it
on 15. and 16.10.2026 do not count: the soldiers were mostly not moved": the `deploy=` switch fired at 0.5 s while the
mission was still in `MissionMode.Deployment`, and "there `Mission.IsFormationUnitPositionAvailableMT` refuses every
unit position outside the side's deployment area, `Formation.GetOrderPositionOfUnit` then hands the unit its own
position, and nothing is teleported". Targets 150 to 290 m away were not reached (the formation "stayed at its
start"); "only targets within ~55 m (single ones up to 114 m)" were. (TAOM's reading: the 19 and 11 battles of the two rows above, 30 in all, are those battles.) [TAOM-verified, v1.5.6 decompile, 2026-10-29] `Mission.cs:6661-6676`
(`IsFormationUnitPositionAvailableMT`) returns false when `Mode == MissionMode.Deployment`,
`DeploymentPlan.HasDeploymentBoundaries(team)` and the position is outside the boundaries, so the refusal applies
only when the side's plan has boundaries (`DefaultMissionDeploymentPlan.cs:250-252`). The fallback is
`Formation.cs:1331-1364` (`GetOrderPositionOfUnitAux` returns `unit.GetWorldPosition()`; the arrangement-slot path
at `:1333-1337` goes through the same check at `:645`, and in Deployment mode a Charge order routes to the Aux path,
`:1385-1388`). It gates formation orders only: `Agent.TeleportToPosition` is not affected, and TAOM's own
teleports (Howdah, Mumakil, enlisted soldier placement) use that call. TAOM code that sets a formation order while
the mission is in Deployment mode would move nobody; a grep of `Main/` and `Client/` found none at 0c5d81408 (the
unpushed staged-siege worktree was not checked). Yotthani's fix waits until the mission has left deployment, then
logs the body's distance to the target a second later.

Yotthani's re-run, 17 Oct 2026 (same file and section, measured in game; the source says "on this scene" and "real on this scene", so
one scene, as above): `deploy=Defender=ChokePoint=300`, 12 battles, placed every time 1 to 4 m from the
target; "Defender wins 3 of 12, mean margin -35 - the same as left alone (40 battles, -37, p = 0.93)". Ten seconds
before the first melee only 6 of the 12 "still stood within 20 m of a choke point" (`FullScaleAttack`,
`DefensiveLine` and `DefensiveEngagement` walk the defender out; `HoldChokePoint` "may pick another one"). By how
the defender got to a choke point: started there by itself (70 m or less) 13 battles, 7 wins, margin -4; walked there
8 battles, 1 win, -54; placed there by the switch 6 battles, 2 wins, -27, with "Started-there against the other two:
p = 0.11 - not shown" and the source's own limit "Outcomes are all-or-nothing ... a dozen battles per group cannot
separate 50 % from 25 %". Start within 70 m of a choke point against farther: 13 battles, 7 wins, -4 against 27
battles, 3 wins, -52, "p = 0.009 - real on this scene"; the attacker's start layout alone does not explain it (11
layouts, share of the spread 0.18, p = 0.81). Same section: every eligible choke point of that scene scores 1.5, "the
cap", because they are 30 to 40 m wide and flat while a 100-man formation is far wider, so the score cannot tell them
apart and "only the facing rule (dot >= 0.5 towards the enemy) picks" (TAOM re-read the cap and the facing rule on
v1.5.5 in the last row of the table). [yotthani]

**Under Culture Doctrine** the same bar applies to a doctrine tactic, which would need to weigh more
than 1.5 x the vanilla tactic holding the team. That is TAOM's inference from `TeamAIComponent.cs`,
not something yotthani measured with doctrine on; TAOM's own edges already use 1.6x for this reason
(`docs/features/culture-doctrine.md`, "Limitations and open items"). The A/B consequences are in
that doc's "Verification: the A/B protocol".

### Morale: casualty shock (v1.5.5)

Re-derived by TAOM from the v1.5.5 source on 2026-10-15. Morale is per agent, never per formation.

| Constant | Value | Source (v1.5.5) |
|---|---|---|
| Radius of the shock around a casualty or a panicking soldier | 4 m | `AgentMoraleInteractionLogic.cs:12` (`MoraleEffectRadius`), passed at `:53` and `:64` |
| Soldiers that lose morale, and soldiers that gain it | at most 10 each | `:14-16`, applied at `:81` and `:92` |
| Who loses | up to 10 random active AI-controlled friends of the casualty within the radius, topped up from the casualty's own formation when fewer than 10 | `:81-84` |
| Who gains | the killer if AI-controlled and an enemy, then random enemies of the casualty within the radius, then the killer's allies near the killer (when more than 1.5 m from the casualty), then the killer's formation | `:86-106` |
| Per-soldier change | the model's maximum divided by `max(1, MoraleResistance)` | `CustomBattleMoraleModel.cs:72-74` |
| Weapon factor k for a kill | 0.75 melee, 0.5 bow, crossbow or throwing, 0.25 area-of-effect or multiple-penetration, then x1.25 (0.3125) with the Burning and MultiplePenetration flags | `CustomBattleMoraleModel.cs:20-36`; `SandboxBattleMoraleModel.cs:227-244` (same constants) |
| Gain for the killer's side | `battleImportance * 3 * k` | `CustomBattleMoraleModel.cs:34`; `SandboxBattleMoraleModel.cs:242` |
| Loss for the casualty's side | `battleImportance * 4 * k * casualtiesFactor` | `CustomBattleMoraleModel.cs:35`; `SandboxBattleMoraleModel.cs:244` |
| `casualtiesFactor` | `1 + 2 * (share of the side already removed)` | `CustomBattleMoraleModel.cs:87-97` |
| A panic | friends within 4 m lose `battleImportance * casualtiesFactor * 1.1`, enemies gain `battleImportance * 2` | `CustomBattleMoraleModel.cs:51-69`; radius `AgentMoraleInteractionLogic.cs:64` |

The campaign model repeats the same weapon constants for a kill (the SandBox lines above); TAOM did not
compare the rest of the two models. Upstream also reads, from the same files and not re-read by TAOM [C]: an
agent starts at 35 plus a random 0 to 29, recovers 0.4 per second but only up to half its start
value, and below 0.01 panics and runs to the border on its own; morale enters no tactic weight, so a
whole side retreats only through `TacticCoordinatedRetreat` (attacker only; upstream lines 85-92).

**The break cascade [M]** (yotthani, same source and setup, lines 94-95; no battle count given): a
side starts to break at about 54 % of its strongest number (range 42 to 65 %), then goes from 50 %
to 10 % in 3 to 14 s in small battles. It is a cascade of single panics, not formations turning.

The Dread Aura's morale constants and the fear scaling are in
[dread-aura.md](../../features/dread-aura.md); this section is the engine side of the same rules.

### Agent ↔ Formation ↔ Detachment (Agent.cs)
**`Agent.Formation`** setter (Agent.cs:1098) (de)registers the agent with the formation + recomputes its
**`Detachment`** (`IDetachment`, :1014 — the Phase 12 `UsableMachine` link: an agent's "use this machine" assignment
is a detachment off its formation). **Setting `Formation = null` makes the agent unattached** (no arrangement slot, no
formation orders). The spider spawns with `BuildAgent(agent, null)` → `Formation == null` → a free agent the team-AI
won't slot into a line.

### Which formation a spawned agent joins (`GetFormationClass`) — v1.4.7

The sections above describe formations once they exist. This is the step *before*: how the engine decides
**which** of the 8 formations a spawning agent belongs to. The answer is different for troops and for heroes,
and the difference is easy to get backwards.

**`default_group` (XML) → `DefaultFormationClass`.** `BasicCharacterObject.Deserialize` sets
`DefaultFormationGroup = 0` unconditionally (`:478`), then overwrites it only if the attribute is present
(`:489-492`) — so **an absent `default_group` means Infantry**. An unparseable value is worse than absent:
`FetchDefaultFormationGroup` (`:534`) returns **`-1`** on a `TryParse` miss, producing an undefined enum value that
every downstream `switch` silently falls through. `Enum.TryParse` is case-insensitive and accepts *any* of the 15
`FormationClass` member names (plus bare integers), so `Skirmisher`, `General` and `Unset` all parse — TAOM's
`INVALID_ENUM` check narrowing this to the four regular classes is doing real work.

**Then two different resolvers:**

| | Troop (non-hero) | Lord / hero |
|---|---|---|
| Resolver | `BasicCharacterObject.GetFormationClass()` (`:543-546`) | `CharacterObject.GetFormationClass()` (`:818-839`) — `override`, and `CharacterObject` is `sealed` (`:16`), so this is final |
| Reads | `DefaultFormationClass`, i.e. `default_group` | **`Equipment` only — `default_group` is never consulted** |
| Rule | the XML value *is* the formation | horse in `EquipmentIndex.Horse` → Cavalry; `+` a Bow/Crossbow → HorseArcher; no horse → Infantry / Ranged |

```csharp
// CharacterObject.cs:818 — the hero branch never touches DefaultFormationClass
public override FormationClass GetFormationClass() {
    if (IsHero && Equipment != null) {
        bool num  = Equipment[EquipmentIndex.ArmorItemEndSlot].Item?.HasHorseComponent ?? false;
        bool flag = Equipment.HasWeaponOfClass(WeaponClass.Bow) || Equipment.HasWeaponOfClass(WeaponClass.Crossbow);
        ...
    }
    return base.GetFormationClass();   // non-hero only: DefaultFormationClass
}
```

Three details that make this bite:
- **`EquipmentIndex.ArmorItemEndSlot` and `EquipmentIndex.Horse` are the same value, `10`** (`EquipmentIndex.cs:21,23`) — the slot read above *is* the horse slot, despite the name.
- **`CharacterObject.Equipment` is itself overridden** (`:100-109`): for a hero it returns `HeroObject.BattleEquipment`, the *live* equipment, not the XML template roster. A horse acquired at runtime counts; no static XML audit can see it.
- `IsHero => _heroObject != null` (`:294`) — true for every spawned lord, so lords always take the equipment branch.

**Consumer chain to the actual `Agent`:** `Mission.SpawnTroop` (`:4442`) does
`agentTeam.GetFormation(GetAgentTroopClass(agentTeam.Side, troop))` — unless an explicit `formationIndex` was passed,
which wins. `Mission.GetAgentTroopClass` (`:2539-2551`) calls `GetFormationClass()` and then, **for siege / naval /
naval-raid / sally-out-attacker only**, collapses it through `.DismountedClass()` (Cavalry→Infantry,
HorseArcher→Ranged). So mount-derived cavalry status only expresses itself in field battles.

**Extension point:** `Mission.GetAgentTroopClass_Override` (`:1555`,
`Func<BattleSideEnum, BasicCharacterObject, FormationClass>`) is checked **first** and short-circuits everything above.
Verified 2026-08-04: **no subscriber in vanilla or in TAOM** — a clean, patch-free hook for overriding formation
assignment from a `MissionBehavior`.

**What still reads the raw `DefaultFormationClass`** (so it stays meaningful for heroes even though battle doesn't use
it): party-screen composition/sorting and formation icons, tooltips, `CharacterCode` (`:62` — the character-preview
mannequin), `DefaultMapVisibilityModel` (`:71` — the Mounted Scouts perk, counting *regular troops* at ≥50% Cavalry),
and the banner-morale check in `CustomBattleMoraleModel`/`SandboxBattleMoraleModel`. A hero whose `default_group`
disagrees with his equipment therefore shows one class on the party screen and fights as another.
**Not** a consumer: `DefaultMilitaryPowerModel` (no formation reference at all).

### Runtime flow
```
Character spawn → Mission.GetAgentTroopClass(side, character)
  ├─ GetAgentTroopClass_Override, if subscribed → wins outright
  ├─ hero?  → CharacterObject.GetFormationClass()  → from live BattleEquipment (horse / bow)
  ├─ troop? → BasicCharacterObject.GetFormationClass() → DefaultFormationClass (default_group)
  └─ siege / naval / sally-out attacker → .DismountedClass() collapse
     → Team.GetFormation(class) → Agent placed in that Formation

Mission start → each Team gets up to 8 Formations + (AI side) a TeamAIComponent
  TeamAIComponent.Tick → pick max-weight TacticComponent → BehaviorComponents → Formation.SetMovementOrder/SetPositioning
    Formation → Arrangement.GetLocalPositionOfUnitOrDefault(unit) per agent → native per-frame agent positioning (AutoGenerated.dll)
  Agent.Formation set → (de)attach to arrangement + recompute Detachment
```

## WHY it's shaped this way

Decoupling **Team** (who) → **TeamAIComponent/Tactic** (the plan) → **Formation** (a maneuver unit) →
**IFormationArrangement** (the geometry) → **orders** (the deltas) lets the AI reason about a battle at the
formation level (a dozen formations, not thousands of agents) while the arrangement + native interop handle the
per-agent positions. Always-8 `FormationsIncludingEmpty` gives every `FormationClass` a stable slot. Detachments let
agents temporarily leave the grid (skirmish, man a siege engine) without leaving the formation.

## TAOM relevance + gotchas
- **`default_group` does not keep a lord off a horse** (2026-08-04). Because `CharacterObject.GetFormationClass()`
  ignores it for heroes, the only thing that decides a lord's battlefield class is what sits in his Horse slot.
  This is why the `MOUNTED_DWARF` validator check gates **both** the enum and the reachable mount rather than the
  enum alone — an enum-only gate passes a lord tagged `Infantry` who still spawns mounted and, on the dwarf
  skeleton's misaligned rider bone, renders inside the horse. Data-side check:
  [`moduledata-validation.md`](../../features/moduledata-validation.md); runtime twin:
  `Patch46_TournamentDwarfDismount`, which strips Horse + HorseHarness for dwarf tournament entrants.
  Corollary for any future "race X never rides" rule: audit the **equipment**, not the attribute — and note that a
  hero's `Equipment` is live `BattleEquipment`, so a runtime-acquired mount escapes static XML validation entirely.
  `Mission.GetAgentTroopClass_Override` is the unused, patch-free hook if that runtime hole ever needs closing.
- **Three TAOM features patch this system:** **SmartCavalryAI** (`Patch31` — coordinated charge state machine, a
  `Formation.SetMovementOrder` Postfix), **MixedFormations** (`Patch30` — arrangement/position math), **CompanionTactics**
  (`Patch35` — a `SetMovementOrder` Postfix). The two `SetMovementOrder` postfixes share the **deferred
  `Patch_MissionTime_SetMovementOrder` category** (applied at `OnMissionBehaviorInitialize`, not `OnSubModuleLoad`,
  because `MovementOrder.cctor` reads `Mission.Current.CurrentTime` — `feedback_movementorder_cctor_mission_current`).
  **Any future patch with `MovementOrder` in its postfix signature must use this category** (CLAUDE.md).
- **SmartCavalryAI + MixedFormations are field-battle only** (2026-07-16, siege CTD #349): both gate on
  `Mission.IsFieldBattle` (`Mission.cs:1373` — true ONLY for `MissionTeamAIType == FieldBattle`), so neither touches
  formations in a siege / sally-out / hideout / naval / settlement mission. Two gotchas worth inheriting: (1) **never
  cache it** — `MissionTeamAIType` is set in `MissionCombatantsLogic.EarlyStart`, and the engine runs every
  `OnBehaviorInitialize` *before* any `EarlyStart` (`Mission.AfterStart`, :3799-3826), so an init-time cache reads
  `NoTeamAI` 100% of the time and silently disables the feature everywhere; (2) **gating the service does not gate the
  feature** — `SmartCavalryAIMissionBehavior.ApplyCollisionAvoidance` writes `agent.SetMovementDirection` per frame
  bypassing `ICavalryChargeService`, so the tick needs its own gate (deep-review HIGH,
  `docs/reviews/rca-siege-guards-2026-07-16.md`). Caveat: `OpenSiegeMissionNoDeployment` is engine-tagged
  `FieldBattle` (`SandBoxMissions.cs:1582`), so relief-force assaults still run both features.
- **Spider DivideByZero connection** (the active `/investigate`): a riderless non-humanoid monster agent in a player
  formation is the suspect for the `DivideByZero ×6 in AutoGenerated.dll` battle-load crash. The formation geometry
  **divides by unit counts** — `num / (float)CountOfUnitsWithoutDetachedOnes` (Formation.cs:1295),
  `1f / (float)num` (:372/:1058/:1428/:1449), file-count `(CustomFlankWidth - UnitDiameter) / (UnitDiameter + Interval)`
  (:756). The managed average-position paths guard with `CountOfUnits == 0` checks (:1307/:1326/:1453), but the
  **native** positioning math (AutoGenerated.dll) consumes `UnitDiameter`/`Interval`/collision-capsule dims — a monster
  with a zero/degenerate collision dimension or a zero-effective-count formation is a plausible native divide. (Stated
  as the lead, **not** proven root cause — that is the `/investigate` task; the fix may be to keep the spider out of
  formation arrangement, which `Formation == null` already does, or to give its Monster a non-zero capsule.)
  **`cave_troll` was investigated as a candidate for this divide (2026-07-16, siege CTD #349) and RULED OUT** — don't
  re-derive it: the Armory snapshot (`docs/reference/lotrlome-armory-snapshot/monsters.xml:974-1080`) shows it is
  `IsHumanoid="true"`, `monster_usage="human"`, on `human_skeleton`, with a **valid non-degenerate**
  `body_capsule radius="0.37"` (UnitDiameter ≈ 0.74), and `docs/features/troll-race.md` records it "confirmed working
  in battle". It is a formation-capable humanoid, not a riderless non-humanoid — a different profile from the spider.
  The still-open sub-question is the *live external* capsule in `LOTRLOME_Armory`, which the repo cannot see.
- **Riding orders follow the unit and the formation** (v1.5.5, read for the horse stand, #892). A unit
  added to a formation takes that formation's riding order (`unit.SetRidingOrder(RidingOrder.OrderEnum)`,
  `Formation.cs:2322`), so a dismount order on a formation reaches every later joiner, an emptied
  cavalry formation included. A transfer into an EMPTY formation first copies the source's orders,
  riding order among them, through `CopyOrdersFrom` (`Formation.cs:2709-2714`, called at `:2171` and
  `:2673`). A formation reset sets `RidingOrderFree` (`ResetAux`, `:2889`). Vanilla's only
  `RidingOrderDismount` setter is the player's Dismount order (`OrderController.cs:327`); its
  counterpart `RidingOrderMount` is set at `:337`. The AI seeks a loose mount only when the unit has
  none, is not retreating and its riding order is Mount, enum value 1 (`HumanAIComponent.cs:267`). So
  a Free order never remounts anyone, and a Dismount order left on an emptied formation keeps
  dismounting whoever is placed in it.
- **Self-friend invariant:** `IsFriendOf` is just `!IsEnemyOf`, so **`team.IsFriendOf(team) == true`** — add an explicit
  `if (team == myTeam) continue;` belt-and-braces around friend checks in multi-team scenarios
  (`feedback_taleworlds_invariant_check_explicit`).
- **Threading:** Bannerlord names multi-threaded formation helpers with the **`_MT` suffix**
  (`CreateNewOrderWorldPositionMT`, `IsFormationUnitPositionAvailableMT`) and guards shared state with
  `TWSharedMutexReadLock`. Patches on `Formation`/`Mission`/`Scene` can fire from **worker threads** — service state
  touched in a `SetMovementOrder` postfix must be lock-protected or immutable (`feedback_detect_engine_threading_via_mt_suffix`).
- **Cross-feature handshake:** SmartCavalryAI and MixedFormations both write `Formation` state via a shared adapter; the
  more-specific feature must win (a cavalry charge-line silently overwritten by a mixed-formation layout was a real bug)
  — `/deep-review` reviews features in isolation, so cross-feature formation collisions are invisible
  (`feedback_cross_feature_handshake_via_shared_adapter`).
- **Replicate vanilla safety gates:** a `SetMovementOrder`/positioning Prefix that returns false must replicate every
  vanilla gate in the full call chain (navmesh validation, `IsFormationUnitPositionAvailable`) — buried helper checks
  are easy to drop (`feedback_replicate_vanilla_safety_gates_in_prefix`).

## The native boundary
**Managed:** `Formation`, `Team`, `IFormationArrangement` implementations, `TeamAIComponent`/`TacticComponent`/
`BehaviorComponent`, the orders. **Native:** `MBTeam` (friend/foe state — `IsEnemyOf`), and the per-frame
**agent-positioning + collision math in `AutoGenerated.dll`** that consumes the arrangement's computed slots +
`UnitDiameter`/`Interval`. So battle *organisation + AI decisions* are managed; *agent placement/movement execution* is
native — which is why a degenerate formation-geometry input surfaces as a **native** `DivideByZero`/AV, not a managed
exception.

## Evidence (file:line, v1.4.5)
- `Formation.cs`:14 (`sealed : IFormation`), :182 (`CountOfUnits`), :380-382 (`FacingOrder`/`ArrangementOrder`), :406 (`Width`), :454 (`Arrangement`), :504/:528 (`Interval`/`Distance`), :685 (`SetMovementOrder`), :138/:142 (detached lists), :756/:1295/:1428/:1449 (count divisions), :1307/:1326/:1453 (zero-count guards).
- `Team.cs`:12 (`: IMissionTeam`), :30 (`Side`), :34/:268 (`FormationsIncludingEmpty` = 8), :38 (`TeamAI`), :613 (`GetFormation`), :629/:634 (`IsEnemyOf`/`IsFriendOf`→`MBTeam`), :447 (`AddTeamAI`).
- `IFormationArrangement.cs`:8-124 (`Width`/`Depth`/`UnitCount`/`RankCount`/multipliers/`GetLocalPositionOfUnitOrDefault`/`CreateNewPosition`).
- `TeamAIComponent.cs`:12 (`abstract`), :36/:48 (tactics list + current), :301 (max-weight pick), :129 (`AddTacticOption`).
- `Agent.cs`:1098 (`Formation` setter), :1014 (`Detachment`/`IDetachment` — Phase 12 link).
- **Formation-class assignment (v1.4.7, re-verified 2026-08-04 against the installed DLLs via `taom-src`):**
  `TaleWorlds.CampaignSystem.CharacterObject.cs`:16 (`sealed`), :100-109 (`Equipment` → `HeroObject.BattleEquipment`
  for heroes), :294 (`IsHero`), :818-839 (`GetFormationClass` override — equipment-derived, ignores
  `DefaultFormationClass`); `TaleWorlds.Core.BasicCharacterObject.cs`:478/:489-492 (`default_group` deserialize,
  Infantry default), :534 (`FetchDefaultFormationGroup` → `-1` on a parse miss), :543-546 (base
  `GetFormationClass`); `TaleWorlds.Core.EquipmentIndex.cs`:21,23 (`ArmorItemEndSlot` == `Horse` == 10);
  `TaleWorlds.MountAndBlade.Mission.cs`:1555 (`GetAgentTroopClass_Override`, no subscriber), :2539-2551
  (`GetAgentTroopClass` + `DismountedClass` collapse), :4442 (`SpawnTroop` → `Team.GetFormation`).
- TAOM patches: `Patch31_SmartCavalryAI`, `Patch30` MixedFormations, `Patch35` CompanionTactics, shared `Patch_MissionTime_SetMovementOrder` (CLAUDE.md). Gotcha memories: `feedback_movementorder_cctor_mission_current`, `feedback_taleworlds_invariant_check_explicit`, `feedback_detect_engine_threading_via_mt_suffix`, `feedback_cross_feature_handshake_via_shared_adapter`, `feedback_replicate_vanilla_safety_gates_in_prefix`.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/culture-doctrine.md](../../features/culture-doctrine.md)
- [docs/INDEX.md](../../INDEX.md)
- [docs/reference/doc-lookup.md](../doc-lookup.md)
- [docs/reference/party-template-sizing.md](../party-template-sizing.md)
- [docs/reviews/rca-tournament-dwarf-dismount-2026-06-09.md](../../reviews/rca-tournament-dwarf-dismount-2026-06-09.md)

<!-- backlinks-end -->
