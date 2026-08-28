# GameModel Override Registry

> Every TAOM GameModel override (vanilla model -> TAOM override -> purpose). Extracted from CLAUDE.md 2026-07-18 (Tier 2 restructure). Override *pattern* + base-class rules live in `.claude/rules/gamemodels.md`.


| GameModel | Overrides | Purpose |
|-----------|-----------|---------|
| `TaomAgentStatCalculateModel` | `SandboxAgentStatCalculateModel` (SandBox) | Career passives on effective max health and agent stats + the mount-lock gate: elephant, spider and Mumakil monsters are never rideable (`CanAgentRideMount` returns false before `base`). The Rhun war chariot is deliberately EXEMPT — remountable mid-battle, gated only by the item's riding difficulty (#279) |
| `TaomClanTierModel` | `DefaultClanTierModel` | Career `CompanionLimit` passive applied on top of the vanilla clan-tier limit |
| `TaomInventoryCapacityModel` | `DefaultInventoryCapacityModel` | Career `InventoryCapacity` passive applied on top of the vanilla capacity |
| `TaomMapVisibilityModel` | `DefaultMapVisibilityModel` | Career `PartySpottingRange` passive, plus the StealthBonus ratio for how easily OTHERS spot the party (a lower ratio is better); since the camps port also the `IPartySpottingContributor` seam (FieldCamp's lookout widens sight through it, never a second AddModel) |
| `TaomBanditDensityModel` | `DefaultBanditDensityModel` | Player-progress-scaled hideout and bandit-party counts (BanditManagement). Every property returns `base` verbatim when scaling is disabled |
| `TaomNotableSpawnModel` | `DefaultNotableSpawnModel` | Culture notable-count feats applied to `GetTargetNotableCountForSettlement`; falls through to `base` when the settlement has no notables of that occupation |
| `TaomCharacterStatsModel` | `DefaultCharacterStatsModel` | `MaxCharacterTier => 10` (vanilla 6) + career `Health` passive on `MaxHitpoints` — the ONLY campaign-side consumer of that pip (character screen, `Hero.MaxHitPoints`, daily heal cap), and via `SandboxAgentStatCalculateModel`'s hero branch it feeds in-battle health too, so nothing else may add `Health` (#394) |
| `TaomPartyWageModel` | `DefaultPartyWageModel` | Extended tier wages (T0-T10) + culture wage/garrison/Rohan mounted feats + career TroopWages passive + AI-lord wage relief (#461), applied in `GetTotalWage` and deliberately NOT in `GetCharacterWage`, because `Campaign.AverageWage` is built from the latter and the garrison-donation math divides by it |
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
| `TaomPartySizeModel` | `DefaultPartySizeLimitModel` | Party size feats (all 12 cultures `ApplyPartySizeFeats` dispatches on: Mordor, Gundabad, Goblin, Blue Craig, Misty Mountain Orcs, Dol Guldur, Isengard, Gondor, Dunland, Rhûn, Harad, Khand) + career PartySize passive + **TroopWeight elite-tax limit deflation** (2026-07-11 count→limit rework: counts read raw, the LIMIT shrinks). Plus **AI lord scaling and a `CalculateGarrisonPartySizeLimit` override** (#461, `IAiPartySizeService`) so AI lords hold their spawned roster; it MUST run before the TroopWeight line, which caches the pre-deflation base the daily shed trims to. All of these land on ONE `ExplainedNumber`, so a heavy roster's tax can cancel a small culture bonus, and any FLAT contributor has to add in the result frame or the factors amplify it. See `docs/features/troop-weight-system.md` and `docs/features/cultural-feats.md` "Evil-culture party-size floor" |
| `TaomFoodConsumptionModel` | `DefaultMobilePartyFoodConsumptionModel` | Food consumption feats (elves, Dol Guldur) + AI-lord food relief (#461), because a starving large party takes -30 morale and vanilla morale desertion ignores the party size limit entirely |
| `TaomSettlementLoyaltyModel` | `DefaultSettlementLoyaltyModel` | Settlement loyalty feats (Gondor, Erebor, elves, Rohan) + JSON-tunable revolt thresholds + dampened different-culture penalties (RevoltTuning feature) |
| `TaomSettlementFoodModel` | `DefaultSettlementFoodModel` | Fixes the Troop-Weight garrison food leak (garrison term uses RAW count, not the weighted `NumberOfAllMembers`) + MCM/JSON-tunable food knobs (consumption divisors, base/village/flat production, storage caps); SettlementFood feature |
| `TaomPartyMoraleModel` | `DefaultPartyMoraleModel` | Party morale feats (Gondor, Rohan, Erebor, elves) + career TroopMorale passive |
| `TaomSmithingModel` | `DefaultSmithingModel` | Smithing energy cost feats (Erebor, Isengard) + career EnchantmentCostReduction passive |
| `TaomClanFinanceModel` | `DefaultClanFinanceModel` | Tariff income feat (Umbar); display-only enlistment wage line on BOTH `CalculateClanGoldChange` and `CalculateClanIncome` (they do not delegate to each other), excluded when `applyWithdrawals` so nobody is paid twice |
| `TaomRaidModel` | `DefaultRaidModel` | Raid damage feats (Mordor, Gundabad, Isengard) + career TroopDamage passive. **`CalculateHitDamage` is settlement raid SPEED, not combat damage** — it drains `SettlementHitPoints`. It was `TroopDamage`'s only consumer until 2026-08-06, which made 105 pips promising "+N% troop damage" inert in every battle; the battle half now lives on `CalculateDamageAmplification` (#395). Both consumers are intentional |
| `TaomMilitaryPowerModel` | `DefaultMilitaryPowerModel` | Configurable T7-T10 troop power (MCM + JSON) |
| `TaomCombatSimulationModel` | `DefaultCombatSimulationModel` | Configurable blunt/cut damage ratio per battle type (MCM); `SimulateHit` also applies the refuge defender bonus via `IRefugeDefenseService` + `RefugeDamageReduction` (the (1-r)-on-final contract shared with the real-time path) |
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
| `TaomCombatMechanicsModel` | `TaomAgentApplyDamageModel` (abstract) → `SandboxAgentApplyDamageModel` | CombatMechanics feel pack in the one `AgentApplyDamageModel` slot: crush-through-block, cleave, stagger immunity, charge knockdown, shield pen (SHIPS OFF since 2026-08-17, lists empty: javelins pierce shields only via the vanilla `Throwing.Impale` grant), per-race modifiers; refuge defender reduction consulted through `IRefugeDefenseService` + `RefugeDamageReduction` (same contract as the auto-resolve path); career damage passives inherited, including (since 2026-08-06) `TroopDamage` for the attacker's non-hero troops via `GetAttackerTroopLeaderHeroId` (the offensive mirror of the existing `TroopResistance` plumbing; resolves off `AttackerRiderAgentOrigin` on a mount hit, since a struck mount's own `Origin` is null). See `docs/features/combat-mechanics.md` |
| `TaomPrisonerRecruitmentCalculationModel` | `DefaultPrisonerRecruitmentCalculationModel` | PrisonerRecruitment: no morale lost recruiting a prisoner of your own faction (same culture) or own non-Neutral alignment side — Isengard absorbing Mordor/Gundabad/Dunland troops. Covers AI + party screen + cost label in one override. See `docs/features/prisoner-recruitment.md` |
| `TaomBattleBannerBearersModel` | `SandboxBattleBannerBearersModel` | BannerBearers: bearers-per-formation scales with size per class (vanilla hardcodes 1) + JSON race gate. **Disabled path must `return base.X()`** — a computed "off" suppresses vanilla's own banner path. Subclass, NOT `BaseModel`-decorate. See `docs/features/banner-bearers.md` |

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/troop-weight-system.md](../features/troop-weight-system.md)

<!-- backlinks-end -->
