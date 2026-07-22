# GameModel Override Registry

> Every TAOM GameModel override (vanilla model -> TAOM override -> purpose). Extracted from CLAUDE.md 2026-07-18 (Tier 2 restructure). Override *pattern* + base-class rules live in `.claude/rules/gamemodels.md`.


| GameModel | Overrides | Purpose |
|-----------|-----------|---------|
| `TaomCharacterStatsModel` | `DefaultCharacterStatsModel` | `MaxCharacterTier => 10` (vanilla 6) |
| `TaomPartyWageModel` | `DefaultPartyWageModel` | Extended tier wages (T0-T10) + culture wage/garrison/Rohan mounted feats + career TroopWages passive |
| `TaomVolunteerModel` | `DefaultVolunteerModel` | `MaxVolunteerTier => 6` (vanilla 4) + alignment-gated recruitment (`MaximumIndexHeroCanRecruitFromHero` returns -1 to block recruiting at an enemy-aligned settlement — AlignmentRecruitment feature) |
| `TaomArmyManagementModel` | `DefaultArmyManagementCalculationModel` | Culture army influence award/cost feats |
| `TaomPartySpeedModel` | `DefaultPartySpeedCalculatingModel` | Culture forest speed + Rohan infantry speed feats + career PartyMovementSpeed passive |
| `TaomSettlementProsperityModel` | `DefaultSettlementProsperityModel` | Culture hearth growth feats |
| `TaomSettlementMilitiaModel` | `DefaultSettlementMilitiaModel` | Culture veteran militia feats |
| `TaomBuildingConstructionModel` | `DefaultBuildingConstructionModel` | Culture construction speed feats |
| `TaomVillageProductionModel` | `DefaultVillageProductionCalculatorModel` | Culture production feats |
| `TaomCaravanModel` | `DefaultCaravanModel` | Umbar caravan cost feat (CulturalFeats) + CaravanTrade basket-diversity overrides (`GetInitialTradeGold` floor, `GetMaxGoldToSpendOnOneItemCategory`) |
| `TaomBattleRewardModel` | `DefaultBattleRewardModel` | Umbar renown feat + career BattleRenownGain passive |
| `TaomPartyTroopUpgradeModel` | `DefaultPartyTroopUpgradeModel` | Mounted recruit cost feats (Isengard, Rohan) + career TroopUpgradeCost passive |
| `TaomPartySizeModel` | `DefaultPartySizeLimitModel` | Party size feats (Mordor, Gundabad, DG, Isengard, Gondor) + career PartySize passive + **TroopWeight elite-tax limit deflation** (2026-07-11 count→limit rework: counts read raw, the LIMIT shrinks). See `docs/features/troop-weight-system.md` |
| `TaomFoodConsumptionModel` | `DefaultMobilePartyFoodConsumptionModel` | Food consumption feats (elves, Dol Guldur) |
| `TaomSettlementLoyaltyModel` | `DefaultSettlementLoyaltyModel` | Settlement loyalty feats (Gondor, Erebor, elves, Rohan) + JSON-tunable revolt thresholds + dampened different-culture penalties (RevoltTuning feature) |
| `TaomSettlementFoodModel` | `DefaultSettlementFoodModel` | Fixes the Troop-Weight garrison food leak (garrison term uses RAW count, not the weighted `NumberOfAllMembers`) + MCM/JSON-tunable food knobs (consumption divisors, base/village/flat production, storage caps); SettlementFood feature |
| `TaomPartyMoraleModel` | `DefaultPartyMoraleModel` | Party morale feats (Gondor, Rohan, Erebor, elves) + career TroopMorale passive |
| `TaomSmithingModel` | `DefaultSmithingModel` | Smithing energy cost feats (Erebor, Isengard) + career EnchantmentCostReduction passive |
| `TaomClanFinanceModel` | `DefaultClanFinanceModel` | Tariff income feat (Umbar) |
| `TaomRaidModel` | `DefaultRaidModel` | Raid damage feats (Mordor, Gundabad, Isengard) + career TroopDamage passive |
| `TaomMilitaryPowerModel` | `DefaultMilitaryPowerModel` | Configurable T7-T10 troop power (MCM + JSON) |
| `TaomCombatSimulationModel` | `DefaultCombatSimulationModel` | Configurable blunt/cut damage ratio per battle type (MCM) |
| `TaomPartyHealingModel` | `DefaultPartyHealingModel` | Cultural survival bonuses (JSON per-faction death chance multiplier) |
| `TaomTournamentModel` | `DefaultTournamentModel` | Per-participant culture armor + culture-specific prize pools (Tierf-based) for regular and elite rewards |
| `TaomAgeModel` | `DefaultAgeModel` | Race-appropriate lifespans (elven immortality, dwarf/hobbit aging) |
| `TaomPregnancyModel` | `DefaultPregnancyModel` | Race-appropriate pregnancy durations |
| `TaomHeroCreationModel` | `DefaultHeroCreationModel` | Race-aware hero creation defaults |
| `TaomAllianceModel` | `DefaultAllianceModel` | Racial enmity constraints on alliance formation |
| `TaomKingdomDecisionPermissionModel` | `DefaultKingdomDecisionPermissionModel` | Culture/race-based decision permission rules |
| `TaomDiplomacyModel` | `DefaultDiplomacyModel` | Custom diplomacy logic for LOTR faction relationships |
| `TaomExecutionRelationModel` | `DefaultExecutionRelationModel` | Culture-specific relation penalties for executions |
| `TaomInformationRestrictionModel` | `DefaultInformationRestrictionModel` | Encyclopedia visibility restrictions per settings |
| `TaomSiegeEventModel` | `DefaultSiegeEventModel` | Adds Trebuchet to defender siege engine options (for Minas Tirith et al.); preserves vanilla Fire-variant perk gating |
| `TaomTargetScoreModel` | `DefaultTargetScoreCalculatingModel` | Besieger army: commitment stickiness (4×), faction priority lists, strength gate bypass per faction, distance compensation; `Patch22_ArmyTargeting` border proximity floor |
| `TaomPartyNavigationModel` | `DefaultPartyNavigationModel` | **PARKED 2026-06-26 — NOT registered** (#120/#296; vanilla model in use). Naval travel: naval capability + water-navigable terrain, player-initiated sailing. Re-enable steps + design: `docs/features/naval-travel.md` |
| `TaomMarriageModel` | `DefaultMarriageModel` | NazgulFamily: the 9 Ringwraiths are marriage-ineligible (`IsSuitableForMarriage` + `IsCoupleSuitableForMarriage` false for wraiths); non-wraiths fall through to vanilla. See `docs/features/nazgul-family.md` |
| `TaomSettlementEconomyModel` | `DefaultSettlementEconomyModel` | Tunable town market-gold regen, ONLY `GetTownGoldChange` (#317 — shipped base 25000 vs vanilla 10000 so drained markets recover; castles never reach it). See `docs/features/settlement-economy.md` |
| `TaomCombatMechanicsModel` | `TaomAgentApplyDamageModel` (abstract) → `SandboxAgentApplyDamageModel` | CombatMechanics feel pack in the one `AgentApplyDamageModel` slot: crush-through-block, cleave, stagger immunity, charge knockdown, shield pen, per-race modifiers; career damage passives inherited. See `docs/features/combat-mechanics.md` |
| `TaomPrisonerRecruitmentCalculationModel` | `DefaultPrisonerRecruitmentCalculationModel` | PrisonerRecruitment: no morale lost recruiting a prisoner of your own faction (same culture) or own non-Neutral alignment side — Isengard absorbing Mordor/Gundabad/Dunland troops. Covers AI + party screen + cost label in one override. See `docs/features/prisoner-recruitment.md` |
| `TaomBattleBannerBearersModel` | `SandboxBattleBannerBearersModel` | BannerBearers: bearers-per-formation scales with size per class (vanilla hardcodes 1) + JSON race gate. **Disabled path must `return base.X()`** — a computed "off" suppresses vanilla's own banner path. Subclass, NOT `BaseModel`-decorate. See `docs/features/banner-bearers.md` |

