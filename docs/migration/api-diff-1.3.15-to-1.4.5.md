# API Diff: 1.3.15 -> 1.4.5 (high-risk classes)

> Generated: 2026-05-22
> Source 1.3.15: `C:\Users\mikew\.taom-src\v1.3.15\` (on-demand cache; only a few classes are present here)
> Source 1.4.5: `E:\Decompiled_Bannerlord\`
>
> Methodology: extract `public`/`protected` method signatures from each side (return type + name + parameter list). Body changes are out of scope. Where a 1.3.15 cache file is absent for a target class, this doc lists the 1.4.5 surface only and flags the TAOM override for re-verification against 1.4.5 — pull the 1.3.15 file via a v1.3.15-correct `taom-src` (broken at time of writing — `tools/taom-src.ps1` ignores `$Version` and reads the v1.4.5 install DLLs) or via ILSpy on the installed v1.3.15 DLLs at a backup path before promoting any "looks fine" verdict.

## Legend

- **REMOVED** — present in 1.3.15, gone in 1.4.5. Any TAOM override of this method fails to compile.
- **NEW** — present in 1.4.5, not in 1.3.15. TAOM overrides do not need changes; new TaleWorlds surface area to optionally take advantage of.
- **SIGNATURE CHANGED** — method name preserved, parameter list / return type drifted. TAOM override must be re-aligned.
- **UNCHANGED** — same signature. TAOM override compiles as-is; behavior may still have shifted (out of scope here).
- **cache miss** — no 1.3.15 file in `~/.taom-src/v1.3.15/`. Need a working v1.3.15 decompile to do a real diff. For now, the TAOM override is listed and flagged for manual re-verification.

---

## DefaultAllianceModel

**1.4.5 path:** `Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultAllianceModel.cs`
**1.3.15 path:** `TaleWorlds.CampaignSystem.GameComponents.DefaultAllianceModel.cs` (cached)
**Risk:** **HIGH** — load-bearing TAOM override breaks at the compile step.

### Removed methods (1.3.15 -> 1.4.5)

_(none — every 1.3.15 method is still present in 1.4.5, but `GetScoreOfStartingAlliance` was reshaped — see below)_

### New methods (added in 1.4.5)

- `public override float GetSupportScoreOfStartingAllianceForClan(Kingdom querierKingdom, Kingdom queriedKingdom, Clan evaluatingClan, out TextObject explanationText, bool includeDescriptions = false)`
- `public override bool CanMakeAlliance(Kingdom kingdom, Kingdom targetKingdom, IFaction evaluatingFaction, out TextObject reason, bool includeReason = false)`
- `public override float GetAllianceFactorForDeclaringWar(IFaction factionDeclaresWar, IFaction factionDeclaredWar)`
- `public override float GetAllianceFactorForDeclaringPeace(IFaction factionDeclaresPeace, IFaction factionDeclaredPeace)`
- `public override Clan GetProposerClanForAllianceDecision(Kingdom proposerKingdom, Kingdom proposedKingdom)`

### Signature-changed methods

- **`GetScoreOfStartingAlliance`** — the `IFaction evaluatingFaction` parameter was REMOVED.
  - 1.3.15: `ExplainedNumber GetScoreOfStartingAlliance(Kingdom kingdomDeclaresAlliance, Kingdom kingdomDeclaredAlliance, IFaction evaluatingFaction, out TextObject explanationText, bool includeDescription = false)`
  - 1.4.5:  `ExplainedNumber GetScoreOfStartingAlliance(Kingdom querierKingdom, Kingdom queriedKingdom, out TextObject explanationText, bool includeDescription = false)`

### Unchanged

`GetCallToWarCost`, `GetInfluenceCostOfProposingStartingAlliance`, `GetScoreOfCallingToWar`, `GetScoreOfJoiningWar`, `GetInfluenceCostOfCallingToWar`.

### TAOM impact

`Main/Features/Diplomacy/Models/TaomAllianceModel.cs` overrides `GetScoreOfStartingAlliance` with the 1.3.15 5-arg signature (`Kingdom, Kingdom, IFaction, out TextObject, bool`). **This override no longer compiles against 1.4.5.** Drop the `IFaction evaluatingFaction` parameter from both the override signature and the `base.GetScoreOfStartingAlliance(...)` call site. The Lore-modifier logic itself is self-contained and survives.

Two scope-creep risks the S3 implementer should be aware of:
- `MaxNumberOfAlliances => int.MaxValue` is still effective, but 1.4.5 now adds `CanMakeAlliance` which uses `Campaign.Current.Models.AllianceModel.MaxNumberOfAlliances` directly as a gate — overriding only the property may not be enough; consider overriding `CanMakeAlliance` to skip the cap entirely.
- Several new methods (`GetSupportScoreOfStartingAllianceForClan`, `CanMakeAlliance`, `GetAllianceFactorForDeclaringWar/Peace`, `GetProposerClanForAllianceDecision`) may also need TAOM Lore-modifier integration — review the design intent before shipping the migration.

---

## DefaultKingdomDecisionPermissionModel

**1.4.5 path:** `Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultKingdomDecisionPermissionModel.cs`
**1.3.15 path:** `TaleWorlds.CampaignSystem.GameComponents.DefaultKingdomDecisionPermissionModel.cs` (cached)
**Risk:** **LOW** — surface unchanged.

### Removed / New / Signature-changed

_(none)_

### Unchanged

- `bool IsPolicyDecisionAllowed(PolicyObject policy)`
- `bool IsWarDecisionAllowedBetweenKingdoms(Kingdom, Kingdom, out TextObject reason)`
- `bool IsPeaceDecisionAllowedBetweenKingdoms(Kingdom, Kingdom, out TextObject reason)`
- `bool IsAnnexationDecisionAllowed(Settlement annexedSettlement)`
- `bool IsExpulsionDecisionAllowed(Clan expelledClan)`
- `bool IsKingSelectionDecisionAllowed(Kingdom kingdom)`
- `bool IsStartAllianceDecisionAllowedBetweenKingdoms(Kingdom, Kingdom, out TextObject reason)`

### TAOM impact

`TaomKingdomDecisionPermissionModel` overrides `IsStartAllianceDecisionAllowedBetweenKingdoms`, `IsWarDecisionAllowedBetweenKingdoms`, `IsPeaceDecisionAllowedBetweenKingdoms`. Compiles as-is against 1.4.5. Behavior re-verification still needed (the calling sites in TaleWorlds may have shifted, e.g., 1.4.5 `CanMakeAlliance` now consults this model).

---

## DefaultDiplomacyModel

**1.4.5 path:** `Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultDiplomacyModel.cs`
**1.3.15 path:** `TaleWorlds.CampaignSystem.GameComponents.DefaultDiplomacyModel.cs` (cached)
**Risk:** **LOW** — public surface unchanged (53 methods, all matching).

### Removed / New / Signature-changed

_(none)_

### Unchanged (selected highlights for S3 review)

`GetClanStrength`, `GetHeroCommandingStrengthForClan`, `GetHeroGoverningStrengthForClan`, `GetRelationIncreaseFactor`, `GetInfluenceAwardForSettlementCapturer`, `GetHourlyInfluenceAwardForBeingArmyMember/RaidingEnemyVillage/BesiegingEnemyFortification`, `GetScoreOfClanToJoinKingdom/LeaveKingdom`, `GetScoreOfKingdomToGetClan/SackClan/HireMercenary/SackMercenary`, `GetScoreOfMercenaryToJoinKingdom/LeaveKingdom`, `GetScoreOfDeclaringPeace[ForClan]`, `GetWarProgressScore`, `GetScoreOfDeclaringWar`, `GetScoreOfLettingPartyGo`, `GetValueOfHeroForFaction`, `GetRelationCostOfExpellingClanFromKingdom`, `GetInfluenceCostOfSupportingClan/ExpellingClan/ProposingPeace/ProposingWar/Annexation/ChangingLeaderOfArmy/DisbandingArmy/PolicyProposalAndDisavowal/AbandoningArmy`, `GetRelationCostOfDisbandingArmy`, `GetInfluenceValueOfSupportingClan/RelationValueOfSupportingClan`, `GetBaseRelation/EffectiveRelation/GetHeroesForEffectiveRelation`, `GetRelationChangeAfterClanLeaderIsDead/AfterVotingInSettlementOwnerPreliminaryDecision`, `GetCharmExperienceFromRelationGain`, `GetNotificationColor`, `DenarsToInfluence`, `GetDecisionMakingThreshold`, `CanSettlementBeGifted`, `GetValueOfSettlementsForFaction`, `GetBarterGroups`, `IsPeaceSuitable`, `GetDailyTributeToPay`, `IsClanEligibleToBecomeRuler`, `GetShallowDiplomaticStance`, `GetDefaultDiplomaticStance`, `IsAtConstantWar`.

### TAOM impact

`TaomDiplomacyModel` overrides `IsAtConstantWar` and `GetRelationChangeAfterVotingInSettlementOwnerPreliminaryDecision`. Both methods are signature-identical. Compiles as-is.

---

## DefaultPartyWageModel

**1.4.5 path:** `Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultPartyWageModel.cs`
**1.3.15 path:** `TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.cs` (cached)
**Risk:** **LOW** — public method surface unchanged. `MaxWagePaymentLimit` property is also unchanged (`=> 10000` in both versions).

### Removed / New / Signature-changed

_(none)_

### Unchanged

- `int GetCharacterWage(CharacterObject character)`
- `ExplainedNumber GetTotalWage(MobileParty mobileParty, TroopRoster troopRoster, bool includeDescriptions = false)`
- `ExplainedNumber GetTroopRecruitmentCost(CharacterObject troop, Hero buyerHero, bool withoutItemCost = false)`
- `int MaxWagePaymentLimit { get; }` — note TAOM overrides this with `=> 20000`.

### TAOM impact

`TaomPartyWageModel` overrides all four (MaxWagePaymentLimit + three methods). All signatures match. Compiles as-is.

---

## DefaultArmyManagementCalculationModel

**1.4.5 path:** `Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultArmyManagementCalculationModel.cs`
**1.3.15 path:** **cache miss** — verify against TAOM override against 1.4.5.
**Risk:** **MEDIUM** — abstract base shape is well-defined in 1.4.5 and matches the TAOM override surface; manual verification recommended before declaring stable.

### 1.4.5 method surface

- `float DailyBeingAtArmyInfluenceAward(MobileParty armyMemberParty)`
- `int CalculatePartyInfluenceCost(MobileParty armyLeaderParty, MobileParty party)`
- `bool CanLordCreateArmy(MobileParty mobileParty, out MBList<MobileParty> possibleArmyMembers)`
- `int CalculateTotalInfluenceCost(Army army, float percentage)`
- `float GetPartySizeScore(MobileParty party)`
- `ExplainedNumber CalculateDailyCohesionChange(Army army, bool includeDescriptions = false)`
- `int CalculateNewCohesion(Army army, PartyBase newParty, int calculatedCohesion, int sign)`
- `int GetCohesionBoostInfluenceCost(Army army, int percentageToBoost = 100)`
- `int GetPartyRelation(Hero hero)`
- `bool CanPlayerCreateArmy(out TextObject disabledReason)`
- `bool CheckPartyEligibility(MobileParty party, out TextObject explanation)`

The abstract base `ArmyManagementCalculationModel` matches one-for-one in 1.4.5 (11 abstract methods).

### TAOM impact

`TaomArmyManagementModel` overrides `DailyBeingAtArmyInfluenceAward` and `CalculatePartyInfluenceCost`. Both signatures match 1.4.5. Likely compiles. Confirm 1.3.15 surface to be sure nothing renamed.

---

## DefaultTargetScoreCalculatingModel

**1.4.5 path:** `Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultTargetScoreCalculatingModel.cs`
**1.3.15 path:** **cache miss** — verify against TAOM override against 1.4.5.
**Risk:** **MEDIUM-HIGH** — TAOM override is signature-load-bearing (`Army.ArmyTypes` parameter shape).

### 1.4.5 method surface

- `float GetDefensivePatrollingFactor(bool isNavalPatrolling)`
- `float GetOffensivePatrollingFactor(bool isNavalPatrolling)`
- `float CalculateOffensivePatrollingScoreForSettlement(Settlement settlement, bool isTargetingPort, MobileParty mobileParty)`
- `float CurrentObjectiveValue(MobileParty mobileParty)`
- `float CalculateDefensivePatrollingScoreForSettlement(Settlement settlement, bool isTargetingPort, MobileParty mobileParty)`
- `float GetTargetScoreForFaction(Settlement targetSettlement, Army.ArmyTypes missionType, MobileParty mobileParty, float ourStrength)`

The 1.4.5 patrolling family methods take a NEW `bool isNavalPatrolling` / `bool isTargetingPort` parameter — likely Naval DLC addition. If 1.3.15 lacked these arguments, that's a signature break for any override.

### TAOM impact

`TaomTargetScoreModel` overrides only `GetTargetScoreForFaction(Settlement, Army.ArmyTypes, MobileParty, float)`. 1.4.5 signature is unchanged for this one — compiles. Patrolling-family overrides are not in TAOM and need no action.

---

## DefaultBattleRewardModel

**1.4.5 path:** `Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultBattleRewardModel.cs`
**1.3.15 path:** **cache miss** — verify against TAOM override against 1.4.5.
**Risk:** **MEDIUM** — Naval DLC added Ship/Figurehead methods. TAOM uses only `CalculateRenownGain` which appears unchanged.

### 1.4.5 method surface (notable Naval additions in BOLD)

- `int GetPlayerGainedRelationAmount(MapEvent mapEvent, Hero hero)`
- `ExplainedNumber CalculateRenownGain(PartyBase winnerParty, float renownValueOfBattleForWinnerSide, float contributionShareOfWinnerParty, float renownMultiplierForWinnerSide, bool includeDescriptions)`
- `ExplainedNumber CalculateInfluenceGain(PartyBase winnerParty, float influenceValueOfBattleForWinnerSide, float contributionShareOfWinnerParty, float influenceMultiplierForWinnerSide, bool includeDescriptions)`
- `ExplainedNumber CalculateMoraleGainVictory(PartyBase winnerParty, ...)`
- `int CalculateGoldLossAfterDefeat(Hero partyLeaderHero)`
- `EquipmentElement GetLootedItemFromTroop(CharacterObject character, float targetValue)`
- `float GetExpectedLootedItemValueFromCasualty(Hero winnerPartyLeaderHero, CharacterObject casualtyCharacter)`
- `float GetAITradePenalty()`
- `float GetMainPartyMemberScatterChance()`
- `int CalculatePlunderedGoldAmountFromDefeatedParty(PartyBase defeatedParty)`
- `void GetCaptureMemberChancesForWinnerParties(MapEvent endedMapEvent, MBReadOnlyList<MapEventParty> winnerParties, out MBList<KeyValuePair<MapEventParty, float>> woundedMemberChances, out MBList<KeyValuePair<MapEventParty, float>> healthyMemberChances)`
- **`float CalculateShipDamageAfterDefeat(Ship ship)`** (NAVAL — almost certainly new in 1.4.5)
- `float GetBannerLootChanceFromDefeatedHero(Hero defeatedHero)`
- `ItemObject GetBannerRewardForWinningMapEvent(MapEvent mapEvent)`
- **`float GetSunkenShipMoraleEffect(PartyBase shipOwner, Ship ship)`** (NAVAL)
- `float CalculateMoraleChangeOnRoundVictory(PartyBase party, MapEventSide partySide, BattleSideEnum roundWinner)`
- **`float GetShipSiegeEngineHitMoraleEffect(Ship ship, SiegeEngineType siegeEngineType)`** (NAVAL)
- **`Figurehead GetFigureheadLoot(MBReadOnlyList<MapEventParty> defeatedParties, PartyBase defeatedSideLeaderParty)`** (NAVAL)
- **`MBReadOnlyList<MapEventParty> GetWinnerPartiesThatCanPlunderGoldFromShips(MBReadOnlyList<MapEventParty> winnerParties)`** (NAVAL)
- `bool CanTroopBeTakenPrisoner(CharacterObject troop)`

### TAOM impact

`TaomBattleRewardModel` overrides only `CalculateRenownGain`. The 1.4.5 signature is the standard 5-arg shape, which matches the TAOM override. Compiles. The new Naval-DLC methods are inherited unchanged from the 1.4.5 base — no action needed unless TAOM wants to extend Naval reward logic.

---

## DefaultPartyHealingModel

**1.4.5 path:** `Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultPartyHealingModel.cs`
**1.3.15 path:** **cache miss** — verify against TAOM override against 1.4.5.
**Risk:** **MEDIUM** — TAOM override of `GetSurvivalChance` matches 1.4.5; 1.4.5 added `GetSiegeBombardmentHitSurgeryChance`.

### 1.4.5 method surface

- `float GetSurgeryChance(PartyBase party)`
- `float GetSiegeBombardmentHitSurgeryChance(PartyBase party)` — likely NEW in 1.4.x (siege bombardment is a re-balanced damage type)
- `float GetSurvivalChance(PartyBase party, CharacterObject character, DamageTypes damageType, bool canDamageKillEvenIfBlunt, PartyBase enemyParty = null)`
- `int GetSkillXpFromHealingTroop(PartyBase party)`
- `ExplainedNumber GetDailyHealingForRegulars(PartyBase party, bool isPrisoners, bool includeDescriptions = false)`
- `ExplainedNumber GetDailyHealingHpForHeroes(PartyBase party, bool isPrisoners, bool includeDescriptions = false)`
- `int GetHeroesEffectedHealingAmount(Hero hero, float healingRate)`
- `ExplainedNumber GetBattleEndHealingAmount(PartyBase party, Hero hero)`

### TAOM impact

`TaomPartyHealingModel` overrides `GetSurvivalChance` only. Signature matches 1.4.5. Compiles. Confirm 1.3.15 didn't have a 4-arg shape (without `enemyParty`) — the optional last parameter is plausible drift.

---

## DefaultCombatSimulationModel

**1.4.5 path:** `Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultCombatSimulationModel.cs`
**1.3.15 path:** **cache miss** — verify against TAOM override against 1.4.5.
**Risk:** **MEDIUM** — multiple new Naval methods; TAOM only touches `GetBluntDamageChance`.

### 1.4.5 method surface

- `ExplainedNumber SimulateHit(CharacterObject strikerTroop, CharacterObject struckTroop, PartyBase strikerParty, PartyBase struckParty, float strikerAdvantage, MapEvent battle, float strikerSideMorale, float struckSideMorale)`
- `ExplainedNumber SimulateHit(Ship strikerShip, Ship struckShip, PartyBase strikerParty, PartyBase struckParty, SiegeEngineType siegeEngine, float strikerAdvantage, MapEvent battle, out int troopCasualties)` (NAVAL)
- `float GetMaximumSiegeEquipmentProgress(Settlement settlement)`
- `int GetNumberOfEquipmentsBuilt(Settlement settlement)`
- `float GetSettlementAdvantage(Settlement settlement)`
- `void GetBattleAdvantage(MapEvent mapEvent, out ExplainedNumber defenderAdvantage, out ExplainedNumber attackerAdvantage)`
- `float GetShipSiegeEngineHitChance(Ship ship, SiegeEngineType siegeEngineType, BattleSideEnum battleSide)` (NAVAL)
- `int GetPursuitRoundCount(MapEvent mapEvent)`
- `float GetBluntDamageChance(CharacterObject strikerTroop, CharacterObject strikedTroop, PartyBase strikerParty, PartyBase strikedParty, MapEvent battle)`
- `CampaignTime GetSimulationTickInterval(MapEvent mapEvent)`
- `MBList<(Ship, MapEventParty)> GetSimulationShips(MapEvent mapEvent, MBList<MapEventParty> battleParties)` (NAVAL)
- `int GetParticipatingTroopCount(MapEventSide side)`

### TAOM impact

`TaomCombatSimulationModel` overrides `GetBluntDamageChance(CharacterObject, CharacterObject, PartyBase, PartyBase, MapEvent)`. Signature matches 1.4.5. Compiles. Naval `SimulateHit(Ship,...)` overload is new and irrelevant for TAOM.

---

## DefaultMilitaryPowerModel

**1.4.5 path:** `Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultMilitaryPowerModel.cs`
**1.3.15 path:** **cache miss** — verify against TAOM override against 1.4.5.
**Risk:** **MEDIUM** — 1.4.5 adds `GetContextForPosition(CampaignVec2)` and a Ship `GetContextModifier` overload.

### 1.4.5 method surface

- `float GetTroopPower(CharacterObject troop, BattleSideEnum side, MapEvent.PowerCalculationContext context, float leaderModifier)`
- `float GetPowerOfParty(PartyBase party, BattleSideEnum side, MapEvent.PowerCalculationContext context)`
- `float GetPowerModifierOfHero(Hero leaderHero)`
- `float GetContextModifier(CharacterObject troop, BattleSideEnum battleSide, MapEvent.PowerCalculationContext context)`
- `MapEvent.PowerCalculationContext GetContextForPosition(CampaignVec2 position)` — likely NEW (uses 1.4 `CampaignVec2`)
- `float GetDefaultTroopPower(CharacterObject troop)`
- `float GetContextModifier(Ship ship, BattleSideEnum battleSideEnum, MapEvent.PowerCalculationContext context)` — Ship overload likely NEW (Naval)

### TAOM impact

`TaomMilitaryPowerModel` overrides `GetDefaultTroopPower(CharacterObject)`. Signature is unchanged. Compiles.

---

## DefaultExecutionRelationModel

**1.4.5 path:** `Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultExecutionRelationModel.cs`
**1.3.15 path:** **cache miss** — verify against TAOM override against 1.4.5.
**Risk:** **LOW** — single-method surface, TAOM override matches.

### 1.4.5 method surface

- `int GetRelationChangeForExecutingHero(Hero victim, Hero hero, out bool showQuickNotification)`

### TAOM impact

`TaomExecutionRelationModel` overrides the only public method. Signature matches. Compiles.

---

## DefaultHeroCreationModel

**1.4.5 path:** `Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultHeroCreationModel.cs`
**1.3.15 path:** **cache miss** — verify against TAOM override against 1.4.5.
**Risk:** **MEDIUM** — broad surface and TAOM override hits `GetCharacterTemplateForOffspring` which carries an `isOffspringFemale` parameter — verify the 1.3.15 sig matched, as 1.4 hero/offspring code has had churn.

### 1.4.5 method surface

- `Settlement GetBornSettlement(Hero hero)`
- `StaticBodyProperties GetStaticBodyProperties(Hero hero, bool isOffspring, float variationAmount = 0.35f)`
- `FormationClass GetPreferredUpgradeFormation(Hero hero)`
- `Clan GetClan(Hero hero)`
- `CultureObject GetCulture(Hero hero, Settlement bornSettlement, Clan clan)`
- `CharacterObject GetRandomTemplateByOccupation(Occupation occupation, Settlement settlement = null)`
- `List<(TraitObject trait, int level)> GetTraitsForHero(Hero hero)`
- `Equipment GetCivilianEquipment(Hero hero)`
- `Equipment GetBattleEquipment(Hero hero)`
- `CharacterObject GetCharacterTemplateForOffspring(Hero mother, Hero father, bool isOffspringFemale)`
- `List<(SkillObject, int)> GetDefaultSkillsForHero(Hero hero)`
- `List<(SkillObject, int)> GetInheritedSkillsForHero(Hero hero)`
- `bool IsHeroCombatant(Hero hero)`

### TAOM impact

`TaomHeroCreationModel` overrides `GetCharacterTemplateForOffspring(Hero, Hero, bool)`. Signature matches 1.4.5. Compiles.

---

## EquipmentSelectionModel (abstract base + DefaultEquipmentSelectionModel)

**1.4.5 abstract base path:** `Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.ComponentInterfaces\EquipmentSelectionModel.cs`
**1.4.5 default impl path:** `Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultEquipmentSelectionModel.cs`
**1.3.15 path:** **cache miss** — verify against TAOM override against 1.4.5.
**Risk:** **LOW-MEDIUM** — no TAOM override exists today. Important for S3 because 1.4.5 added a `Hero hero` parameter to most methods (vs the older `BasicCharacterObject character` parameter pattern that may have been in 1.3.15 — needs verification).

### 1.4.5 abstract surface (`EquipmentSelectionModel`)

- `abstract Equipment GetEquipmentForHeroComeOfAge(Hero hero, Equipment.EquipmentType equipmentType)`
- `abstract Equipment GetEquipmentForHeroReachesTeenAge(Hero hero)`
- `abstract Equipment GetEquipmentForInitialChildrenGeneration(Hero hero)`
- `abstract Equipment GetEquipmentForDeliveredOffspring(Hero hero)`
- `abstract Equipment GetEquipmentForCompanionWhenTurningToLord(Hero companionHero, Equipment.EquipmentType equipmentType)`

### 1.4.5 default impl surface (`DefaultEquipmentSelectionModel`)

Matches the abstract one-for-one (overrides of the five methods).

### TAOM impact

No TAOM `TaomEquipmentSelectionModel` exists in the current tree. If TAOM ever needed to control coming-of-age / offspring equipment (relevant for the RaceAge + career-archetype starter-equipment feature), 1.4.5 is the model to hook. The new `Equipment.EquipmentType` enum parameter (Battle / Civilian / Stealth) is the same shape as 1.3.15. Note: in 1.4.5 `Equipment` also exposes a new `ItemEquipmentType` getter (`public EquipmentType ItemEquipmentType => _equipmentType;`) that is absent in 1.3.15 — useful for inspecting whether an `Equipment` instance is civilian/battle/stealth without subclassing.

---

## EquipmentFlags

**Status:** No `EquipmentFlags` enum found in either `E:\Decompiled_Bannerlord\Core\` (v1.4.5) or `~/.taom-src/v1.3.15/` (the file was not cached, but spot-grep across the v1.4.5 dump turned up zero hits for `EquipmentFlags` as a type). This is likely either (a) a renamed concept in TW source, or (b) a TAOM-internal name. If S3 implementers are looking for the flag-y enum that decides whether equipment is battle / civilian / stealth, that's `Equipment.EquipmentType` (see EquipmentSet below).

### Action

Verify the original intent of the diff request. If the target was `Equipment.EquipmentType`, the values are unchanged between 1.3.15 and 1.4.5:

- `Invalid`
- `Battle`
- `Civilian`
- `Stealth`

If the target was some other type (e.g., `ItemFlags`, `ItemUsageSetFlags`), name it and re-run.

---

## EquipmentSet / Equipment.EquipmentType schema

**Status:** No top-level `EquipmentSet` class. The `equipmentType` XML attribute on `<EquipmentRoster>` elements deserialises to `Equipment.EquipmentType` (see `MBEquipmentRoster.cs` in both versions).

### XML schema

```xml
<EquipmentRoster equipmentType="Battle | Civilian | Stealth | (omitted → Civilian)">
  <equipment slot="..." id="..." />
  ...
</EquipmentRoster>
```

This is identical in 1.3.15 and 1.4.5. No XML migration required for the `equipmentType` attribute.

### One 1.4.5-only addition

`Equipment` (`E:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\Equipment.cs:48`):

```csharp
public EquipmentType ItemEquipmentType => _equipmentType;   // 1.4.5 only — not in 1.3.15
```

Useful if TAOM wants to read the equipment type back without re-parsing XML.

---

## Summary

### Total breaking changes (compile-blocking against 1.4.5)

**1 — `DefaultAllianceModel.GetScoreOfStartingAlliance`** lost its `IFaction evaluatingFaction` parameter. `TaomAllianceModel` breaks. Trivial fix (drop the param from override + base call).

### Models requiring rewrite

- `TaomAllianceModel` — drop `IFaction evaluatingFaction` from `GetScoreOfStartingAlliance` override.

### Models likely OK with re-verification (compile clean, behavior re-check needed)

- `TaomKingdomDecisionPermissionModel`, `TaomDiplomacyModel`, `TaomPartyWageModel`, `TaomArmyManagementModel`, `TaomTargetScoreModel`, `TaomBattleRewardModel`, `TaomPartyHealingModel`, `TaomCombatSimulationModel`, `TaomMilitaryPowerModel`, `TaomExecutionRelationModel`, `TaomHeroCreationModel`.

### Cache-miss caveat

Eleven of the fourteen target classes have **no 1.3.15 cached source** at `~/.taom-src/v1.3.15/`. `tools/taom-src.ps1` cannot currently re-fetch them because the script hardcodes `$Version = 'v1.3.15'` but then reads from the live `$env:BANNERLORD_GAME_DIR\bin\Win64_Shipping_Client` directory, which is now v1.4.5 (so re-running it would just overwrite the 1.4.5 surface back into the cache). Until the script is fixed or a backup v1.3.15 DLL path is supplied, the "TAOM signature compatibility" check for those eleven is "matches 1.4.5" only — the older signatures are inferred from the TAOM override sites (where they would have had to compile against 1.3.15) but are not directly diffed here.

### Top 3 highest-risk findings

1. **`DefaultAllianceModel.GetScoreOfStartingAlliance` lost `IFaction evaluatingFaction`** — `TaomAllianceModel` has a hard compile break here. Easy fix (drop one parameter both at override and at `base.` call), but failing to make it will cascade through the Diplomacy module.
2. **`DefaultAllianceModel` gained 5 new public methods** (`GetSupportScoreOfStartingAllianceForClan`, `CanMakeAlliance`, `GetAllianceFactorForDeclaringWar`, `GetAllianceFactorForDeclaringPeace`, `GetProposerClanForAllianceDecision`). `TaomAllianceModel.MaxNumberOfAlliances => int.MaxValue` may no longer be sufficient to allow unlimited alliances — `CanMakeAlliance` re-checks `MaxNumberOfAlliances` from `Campaign.Current.Models.AllianceModel`, which would respect the int.MaxValue override, but the new method also has player-support / score-threshold gates that could veto an alliance independently. Audit `CanMakeAlliance` behavior before declaring the lore-friendly alliances feature stable.
3. **Naval DLC surface across BattleReward / CombatSimulation / MilitaryPower / TargetScore.** TAOM does not override any of the new `Ship` / `Figurehead` / `IsTargetingPort` methods, so nothing breaks at compile time. But: vanilla 1.4.5 will now call `CalculateShipDamageAfterDefeat`, `GetFigureheadLoot`, etc. during any naval map event. If TAOM ships a map with naval encounters before evaluating these, expect undefined behavior. Document in the v1.4.x-overview that naval is "Out Of Scope" for the S3 migration unless explicitly opted in.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/migration/templates/README.md](templates/README.md)

<!-- backlinks-end -->
