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
One side. `Side` (`BattleSideEnum` Attacker/Defender, :30); **`FormationsIncludingEmpty`** (`MBList<Formation>(8)`, always 8 slots by `FormationClass`, :34/:268) and
**`FormationsIncludingSpecialAndEmpty`** (10: the same eight plus General (8) and Bodyguard (9), :36/:267; the list the
engine's own enemy walks use, `Formation.CacheClosestEnemyFormation` (`Formation.cs:1361-1371`) and `TeamQuerySystem`
(`:190`, `:234`, `:291`), and the one a TAOM scan of ENEMY formations must use too, since
`GeneralsAndCaptainsAssignmentLogic` fills the general and bodyguard formations in an ordinary field battle
(`SandBoxMissions.cs:746`, `:855`); own-team splits and snapshots stay on the eight); **`TeamAI`** (`TeamAIComponent`, :38); `GeneralsFormation`/
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

### The tactic layer on v1.4.8 (plan 144)

Adapted from the 1.5.x line's v1.5.3 table (`5dfa8f4cb`, `578770e03`); every row re-read on the installed v1.4.8
with taom-src or the dump.

| Fact | Where (v1.4.8) |
|---|---|
| Field battles: every team gets `new TeamAIGeneral(mission, team)`; tactic options are registered per team by the side's best commander Tactics skill (`Charge`; >= 20 `FullScaleAttack` plus defender `DefensiveEngagement`/`DefensiveLine` or attacker `RangedHarrassmentOffensive`; >= 50 `FrontalCavalryCharge` plus defender `DefensiveRing`/`HoldChokePoint` or attacker `CoordinatedRetreat`), then `QuerySystem.Expire()` and `ResetTactic()` | `MissionCombatantsLogic.cs:100`, `:152-177`, `:218-219` |
| The team decides every 5 s: `MaxBy(GetTacticWeight() * (current ? 1.5 : 1))`, and a switch happens only when the new weight beats 1.5 x the current tactic's; with no enemy formation left it forces `TacticCharge` | `TeamAIComponent.cs:206`, `:276-290`, `:301`, `:316-321` |
| `_availableTactics` is a private readonly `List` with no getter; `Team.AddTacticOption`, `RemoveTacticOption(Type)`, `ClearTacticOptions` and `ResetTactic` are public forwarders; `Team.AddTeamAI` is public and replaces the component | `TeamAIComponent.cs:36`, `:131-141`; `Team.cs:415-447` |
| A tactic's weight is `protected internal virtual float GetTacticWeight()`; the base exposes protected `_mainInfantry` and its siblings, `AssignTacticFormations1121`, `ManageFormationCounts(int, int, int, int)` and public static `SetDefaultBehaviorWeights` | `TacticComponent.cs:26`, `:475`, `:520`, `:581`, `:589` |
| The defensive tactics return 0 unless `IsDefenseApplicable`; `TacticDefensiveRing` builds a runtime `TacticalPosition` (public constructor) for an insurmountable region; `TeamAIComponent.TacticalPositions` is a public mutable list | `TeamAIComponent.cs:50`, `:66`, `:215-247`; `TacticDefensiveRing.cs:117`, `:180`; `TacticalPosition.cs:105` |
| `FormationAI` ticks every 0.5 s, staggered per team; the winning behaviour is `GetAIWeight() * WeightFactor`, the running one boosted by a factor that holds 2.0, then slides to 1.2 over the last 5 s of its 10 s preserve window; orders apply only if `IsAIControlled`; `SetBehaviorWeight<T>` throws `MBException` for an unregistered T | `FormationAI.cs:63`, `:112-116`, `:130`, `:138`, `:179-182`, `:245`, `:286` |
| `TeamAIGeneral.OnUnitAddedToFormationForTheFirstTime` registers 24 single-player field behaviours per formation on its first unit (from `Formation.AddUnit`); a formation filled by a split gets them too, because `TransferUnitsAux` and `TransferUnitsWithPriorityFunction` set `Agent.Formation`, whose setter calls `AddUnit` | `TeamAIGeneral.cs:57-85`; `Formation.cs:2112`, `:2158`, `:1988`/`:2048`, `:2490`/`:2505`; `Agent.cs:1098`, `:1142` |
| THREAD: `Team.Tick` runs inside `Mission.TickAgentsAndTeamsImp`, the agent-tick job (async in single player, see the thread table in `.claude/rules/harmony-patches.md`), before `tickCompleted = true`, and gates on `Mission.AllowAiTicking`, which `DeploymentMissionController` holds false through deployment | `Mission.cs:3601-3613`; `Team.cs:573-579`; `DeploymentMissionController.cs:31`, `:75` |
| `MissionCombatantsLogic.GetAllCombatants()` and the static `SupportsAllyTeamOnPlayerSide(...)` are public | `MissionCombatantsLogic.cs:228`, `:261` |
| `Mission.GetAgentTroopClass_Override` (a public event, consulted first) is subscribed by vanilla controllers outside field battles (hideouts, alley fights); TAOM does not subscribe it on this line. The 1.5.x line's "unsubscribed in vanilla" is wrong on v1.4.8 | `Mission.cs:1555`, `:2541-2543`; `HideoutMissionController.cs:441`, `HideoutAmbushMissionController.cs:155`, `AlleyFightMissionHandler.cs:34` |

### Morale: casualty shock (v1.4.8, plan 144)

Re-derived on the 1.5.x line from v1.5.5 (`d690ff8d2`) and re-read on v1.4.8 (the dump; `SandboxBattleMoraleModel`
through taom-src). Morale is per agent, never per formation.

| Constant | Value | Source (v1.4.8) |
|---|---|---|
| Radius of the shock around a casualty or a panicking soldier | 4 m | `AgentMoraleInteractionLogic.cs:12`, passed at `:53` and `:64` |
| Soldiers that lose morale, and soldiers that gain it | at most 10 each | `:14-16` |
| Who loses | up to 10 random friends of the casualty within the radius, topped up from the casualty's own formation when fewer than 10 | `:81-84` |
| Who gains | the killer if AI-controlled, human and an enemy, then random enemies of the casualty within the radius | `:86-93` |
| Per-soldier change | the model's maximum divided by `max(1, MoraleResistance)` | `CustomBattleMoraleModel.cs:72-75` |
| Weapon factor k for a kill | 0.75 melee, 0.5 ranged, 0.25 area-of-effect or multiple-penetration, then x1.25 with both Burning and MultiplePenetration | `CustomBattleMoraleModel.cs:20-31`; taom-src `SandboxBattleMoraleModel.CalculateMaxMoraleChangeDueToAgentIncapacitated` |
| Gain for the killer's side | `battleImportance * 3 * k` | `CustomBattleMoraleModel.cs:34`; `SandboxBattleMoraleModel.CalculateMaxMoraleChangeDueToAgentIncapacitated` |
| Loss for the casualty's side | `battleImportance * 4 * k * casualtiesFactor` | `CustomBattleMoraleModel.cs:35`; `SandboxBattleMoraleModel.CalculateMaxMoraleChangeDueToAgentIncapacitated` |
| `casualtiesFactor` | `1 + 2 * (share of the side already removed)` | `CustomBattleMoraleModel.cs:87-98` |
| A panic | friends lose `battleImportance * casualtiesFactor * 1.1`, enemies gain `battleImportance * 2` | `CustomBattleMoraleModel.cs:51-57` |

**The break cascade** [yotthani, measured on v1.5.5 with TAOM 2.0.34, MithrilForge `docs/engine/battle-behaviour-measured.md`
lines 94-95; not re-measured on v1.4.8]: a side starts to break at about 54 % of its strongest number (range 42 to
65 %), then goes from 50 % to 10 % in 3 to 14 s in small battles, a cascade of single panics, not formations
turning. The Dread Aura's morale constants are in [dread-aura.md](../../features/dread-aura.md).

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

- [docs/INDEX.md](../../INDEX.md)
- [docs/reference/doc-lookup.md](../doc-lookup.md)
- [docs/reference/party-template-sizing.md](../party-template-sizing.md)
- [docs/reviews/rca-tournament-dwarf-dismount-2026-06-09.md](../../reviews/rca-tournament-dwarf-dismount-2026-06-09.md)

<!-- backlinks-end -->
