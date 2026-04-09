# TAOM v1.4.0 Compatibility Report

Generated: 2026-04-09

## Summary

| Category | BREAKING | HIGH | MEDIUM | LOW |
|----------|----------|------|--------|-----|
| GameModel overrides | ~~2~~ 0 FIXED | ~~2~~ 0 VERIFIED | 0 | 4 |
| Harmony patches | 0 | 0 | 0 | 20+ safe |
| Reflection targets | 0 | 0 | 0 | 10+ safe |
| **Total** | **0 (all fixed)** | **0 (all verified)** | **1 monitor** | — |

**108 types checked, 46 unchanged, 37 changed, 0 removed, 4 new.**

---

## BREAKING CHANGES (must fix before build will compile)

### 1. TaomAllianceModel : DefaultAllianceModel — signature change

- **File:** `Main/Features/Diplomacy/Models/TaomAllianceModel.cs`
- **What changed:** `GetScoreOfStartingAlliance` lost the `IFaction evaluatingFaction` parameter. Old: `(Kingdom, Kingdom, IFaction, out TextObject, bool)` -> New: `(Kingdom, Kingdom, out TextObject, bool)`
- **Impact:** Compile error — wrong number of arguments on both override signature and `base.` call
- **Fix:** Remove `IFaction evaluatingFaction` from the override signature and from the `base.GetScoreOfStartingAlliance(...)` call

### 2. TaomBattleRewardModel : DefaultBattleRewardModel — new parameters

- **File:** `Main/Features/CulturalFeats/Models/TaomBattleRewardModel.cs`
- **What changed:** `CalculateRenownGain` gained 2 new params: `float renownMultiplierForWinnerSide, bool includeDescriptions`. Old: 3 params -> New: 5 params
- **Impact:** Compile error — override and base call have wrong arg count
- **Fix:** Update override to `(PartyBase winnerParty, float renownValueOfBattle, float contributionShare, float renownMultiplierForWinnerSide, bool includeDescriptions)` and pass all 5 to `base.`

---

## HIGH RISK (likely to cause issues at runtime)

### ~~3. Mission.RegisterBlow~~ — VERIFIED SAFE

- **File:** `Main/Features/AdvancedCombat/CustomAttacksUtils.cs`
- **Status:** Verified against 1.4.0 decompiled source. Signature unchanged: `(Agent, Agent, WeakGameEntity, Blow, ref AttackCollisionData, in MissionWeapon, ref CombatLogData)`. No action needed.

### ~~4. GuardsCampaignBehavior.PrepareGuardAgentDataFromGarrison~~ — VERIFIED SAFE

- **File:** `Main/Features/SettlementGuards/Hooks/GuardsCampaignBehavior_TakeGuardAgentData_Patch.cs`
- **Status:** Verified against 1.4.0 decompiled source. Both `TakeGuardAgentDataFromGarrisonTroopList` (line 183) and `PrepareGuardAgentDataFromGarrison` (line 212) exist with identical signatures. No action needed.

### 5. TaomKingdomDecisionPermissionModel — base logic rewrite

- **File:** `Main/Features/Diplomacy/Models/TaomKingdomDecisionPermissionModel.cs`
- **What changed:** `IsPeaceDecisionAllowedBetweenKingdoms` base logic completely rewritten with new call-to-war agreement checks. TAOM calls `base.` for non-WotR cases.
- **Impact:** No compile error. New base logic may block peace for unrelated call-to-war reasons in non-WotR scenarios.
- **Fix:** Audit new base implementation. Decide if TAOM needs to intercept before the new checks.

### 6. TaomArmyManagementModel — method removed

- **File:** `Main/Features/CulturalFeats/Models/TaomArmyManagementModel.cs`
- **What changed:** `GetMobilePartiesToCallToArmy` removed, replaced with `CanLordCreateArmy`. Also `Clan.CommanderLimit` renamed to `Clan.WarPartyLimit`.
- **Impact:** Compile error if any TAOM code calls `GetMobilePartiesToCallToArmy` or `Clan.CommanderLimit`
- **Fix:** Search TAOM codebase for both identifiers and update. TAOM's own overrides (`DailyBeingAtArmyInfluenceAward`, `CalculatePartyInfluenceCost`) are unaffected.

---

## MEDIUM RISK (may cause subtle issues)

### 7. AgentVisuals.Create — possible param change

- **File:** `Main/Features/BannerColorPersistence/Hooks/AgentVisuals_Create_Patch.cs`
- **What changed:** Agent gained +133 lines. If `AgentVisuals.Create` gained a parameter, the manual patch silently fails to bind.
- **Impact:** Clan armor color randomness suppression stops working
- **Fix:** Verify `AgentVisuals.Create` still has 5 parameters after update.

### 8. MapConversationTableau patches — private field stability

- **Files:** `MapConversationTableau_SpawnOpponentLeader_Patch.cs`, `MapConversationTableau_SpawnOpponentBodyguard_Patch.cs`
- **What changed:** 7 cached reflection fields into private view types. Internal restructuring could silently return null.
- **Impact:** Conversation banner colors revert to faction defaults. No crash.
- **Fix:** Verify 7 cached reflection targets after update. Already verified against 1.4.0 decompiled source — all intact.

### 9. SPInventoryVM — possible double color-apply

- **File:** `Main/Features/BannerColorPersistence/Hooks/SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.cs`
- **What changed:** TaleWorlds added `UpdateCharacterArmorColor` method. If `UpdateCurrentCharacterIfPossible` now calls it internally, TAOM's postfix may double-apply.
- **Impact:** Cosmetically harmless but worth monitoring.
- **Fix:** Check if the base now calls `UpdateCharacterArmorColor` internally.

### 10. TaomDiplomacyModel — property renames

- **File:** `Main/Features/Diplomacy/Models/TaomDiplomacyModel.cs`
- **What changed:** `WarDeclarationScorePenaltyAgainstAllies` removed, `GetAllianceFactor` renamed to `GetTradeAgreementFactor`. `MinNeutralRelationLimit` changed from -25 to -50.
- **Impact:** Compile error if any TAOM code references `GetAllianceFactor` or `WarDeclarationScorePenaltyAgainstAllies`. Behavioral change in neutral relation range.
- **Fix:** Search for `GetAllianceFactor` and `WarDeclarationScorePenaltyAgainstAllies` across TAOM codebase.

### 11. TaomTargetScoreModel — base score scale change

- **File:** `Main/Features/ArmyTargeting/Models/TaomTargetScoreModel.cs`
- **What changed:** `CurrentObjectiveValue` completely rewritten in base. Patrolling methods renamed.
- **Impact:** Score scale may shift, affecting TAOM's multipliers. No compile error.
- **Fix:** Review new base score range. TAOM overrides `GetTargetScoreForFaction` only — method itself unchanged.

### 12. CultureSettingService — dynamic reflection unverified

- **File:** `Main/Features/FactionMap/CultureSettingService.cs`
- **What changed:** Uses dynamic `GetType()` lookups for `_characterCreationContent`, `SetSelectedCulture`, `_cultures`, `ExecuteSelectCulture`. Not covered by diff.
- **Impact:** Faction map culture selection silently disabled if any were renamed. Graceful fallback.
- **Fix:** Validate field names against 1.4.0 decompiled `CharacterCreationManager`.

---

## LOW RISK (confirmed safe or cosmetic)

- **TaomClanFinanceModel** — Base gained trade agreement income. TAOM's `CalculateTownIncomeFromTariffs` override unaffected.
- **TaomCombatSimulationModel** — Minor base changes. `GetBluntDamageChance` signature unchanged.
- **TaomMilitaryPowerModel** — Minor base changes. `GetDefaultTroopPower` signature unchanged.
- **TaomMapVisibilityModel** — Minor base changes. `GetPartySpottingRange` signature unchanged.
- **All CharacterTableau reflection** — 25+ private fields verified intact in 1.4.0.
- **All CharacterSpawner reflection** — All private fields verified intact in 1.4.0.
- **LoadingWindowViewModel.Update** — Correct assembly reference already in place. Method unchanged.
- **20+ Harmony patches** verified safe: Agent.EquipItemsFromSpawnEquipment, Mission.Initialize, Mission.SpawnAgent, Clan.UpdateBannerColor, PartyCharacterVM.InitializeUpgrades, all DefaultMapWeatherModel patches, all CharacterCreation patches, etc.

---

## New Types in v1.4.0

- **BodyGeneratorView** — now in `TaleWorlds.MountAndBlade.GauntletUI` (previously not separately decompiled)
- **CharacterSpawner** — now in `TaleWorlds.MountAndBlade.View`
- **CharacterTableau** — now in `TaleWorlds.MountAndBlade.View`
- **LoadingWindowViewModel** — now in `TaleWorlds.MountAndBlade.GauntletUI`

All 4 are covered by existing SandBox module DLL wildcard reference in TAOM.csproj. No assembly reference changes needed.

---

## Remediation Checklist

- [x] ~~Fix `TaomAllianceModel.GetScoreOfStartingAlliance`~~ — **FIXED** (removed `IFaction` param)
- [x] ~~Fix `TaomBattleRewardModel.CalculateRenownGain`~~ — **FIXED** (added 2 new params, threaded through)
- [x] ~~Verify `Mission.RegisterBlow` signature~~ — **SAFE** (unchanged in 1.4.0)
- [x] ~~Verify `GuardsCampaignBehavior.PrepareGuardAgentDataFromGarrison`~~ — **SAFE** (line 212, same sig)
- [x] ~~Search for `Clan.CommanderLimit` usage~~ — **SAFE** (not used in TAOM)
- [x] ~~Search for `GetAllianceFactor` / `GetMobilePartiesToCallToArmy` / `WarDeclarationScorePenaltyAgainstAllies`~~ — **SAFE** (none found in TAOM)
- [x] ~~Audit `TaomKingdomDecisionPermissionModel`~~ — **SAFE** (WotR gate-first pattern compatible with new bidirectional call-to-war checks; comment added)
- [x] ~~Verify `AgentVisuals.Create` still has 5 parameters~~ — **SAFE** (exact signature confirmed in 1.4.0)
- [x] ~~Validate `CultureSettingService` dynamic reflection targets~~ — **SAFE** (property-first fallback works; all 4 targets verified)
- [x] ~~TaomTargetScoreModel base rewrite exposure~~ — **SAFE** (only overrides `GetTargetScoreForFaction`, no renamed methods used)
- [ ] Monitor `SPInventoryVM` postfix — watch for inventory color flicker if vanilla now calls `UpdateCharacterArmorColor` elsewhere
- [ ] Run full build + test after fixes
