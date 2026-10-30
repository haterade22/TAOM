---
paths:
  - "Main/Features/**/Models/*.cs"
  - "Main/Features/**/*Model.cs"
---

# GameModel Override Rules

TAOM has 70 GameModel overrides, the parked and abstract ones included (`tools/lint_docs.py` checks the total). All follow the same pattern.

## Pattern

```csharp
public class TaomFooModel : DefaultFooModel
{
    private readonly IFooService _service;

    public TaomFooModel(IFooService service)
    {
        _service = service;
    }

    public override float SomeCalculation(SealedType param)
    {
        var adapter = IoC.Resolve<IAdapterFactory>().GetAdapter(param);
        var taomResult = _service.Calculate(adapter);
        return taomResult ?? base.SomeCalculation(param);
    }
}
```

## Rules

1. **Research first** — Always decompile `DefaultXxxModel` with `pwsh tools/taom-src.ps1 path <Type>` before overriding. Never guess which base methods to call.
2. **Inherit from `Default*`** — Never override `GameModel` directly; inherit from the corresponding `Default*` class.
3. **Call `base.Method()`** — Unless deliberately replacing behavior, fall through to base for unhandled cases.
4. **Thin model class** — The model class is an entry point (<150 lines). **All logic goes in a `Service`.** Line count is a ceiling, not the test. The override body may contain ONLY one of: (a) a single constant expression (e.g. `MaxCharacterTier => 10`), (b) perk/adapter conversion at the boundary plus a direct delegate to the service. A body that contains `if`, `foreach`, `switch`, `yield` branching, or any multi-line computation is a violation — extract to a service even if the model is under 20 lines. "It's only a few lines" is not a carve-out; the rule is binary. Counter-example: `TaomCharacterStatsModel` (one constant) is legal; a 6-line `yield return` chain with a conditional is not.
5. **Adapter boundary** — Convert sealed TaleWorlds params to adapters immediately. Never pass `Hero`, `Settlement`, etc. into the service.
6. **JSON/XML config** — Configurable values live in `Main/_Module/ModuleData/configs/` or feature-specific XML, not hardcoded in the model.
7. **Register in SubModule.cs:** GameModel overrides are registered with `campaignStarter.AddModel<TBase>(new TaomXxxModel(...))` inside `SubModule.OnGameStart` (there is no `CreateGameModels()`); resolution is last-registered-wins, so TAOM's call must follow the engine's. `GameModelOverrideBindingTests` looks for the literal `new TaomXxxModel(` in `SubModule.cs`, so register with the unqualified type name and a `using`, not a namespace-qualified `new`. A feature module may instead declare `GameModelDecl.Of<TSlot, TModel>(target, ...)` in `TaomFeatureModule.GameModels`, which `FeatureModuleHooks` adds after the models `SubModule` adds itself; the binding test accepts either route (`SiegeForcesModule` uses it). Do not declare a Custom Battle model for a slot `RegisterCustomBattleModels` fills by hand: the module step runs after it and the last model added wins (`FeatureModulesTests`).
8. **Tests** — Service logic is fully unit-tested. The model class itself is thin enough to not need direct tests.
9. **Cross-entity propagation (MANDATORY when the override returns a per-entity capability/value)** — If your override returns a per-entity value (per party, per hero, per settlement) that the engine **propagates** to related entities or **recomputes per-entity**, you MUST decompile the engine *consumer* of your result and mirror that propagation — or related entities desync. Before overriding, answer two questions from the decompiled engine code: (a) **Propagation** — does the value get pushed onto attached/child entities (army attached parties, settlement bound villages, family members)? (b) **Recompute** — is it recomputed per-entity from a per-entity getter the engine drives across the group? If yes to either, the override is NOT a per-entity-isolated decision. Also encode **lifecycle**: an entity already mid-transition must retain the capability to *complete/exit* (e.g. a party already at sea keeps naval capability to reach land regardless of toggles; gates govern only new transitions). **Worked example (NavalTravel #296, the bug this rule prevents):** `PartyNavigationModel.HasNavalNavigationCapability` keyed only on `IsMainParty` stranded a player-led army's attached AI parties — the engine force-propagates `MobileParty.IsCurrentlyAtSea` down the attachment tree AND recomputes `NavigationCapability` per party (`MobileParty.cs:464-479,493-496`), so attached parties were dragged to sea with `Default`-only navigation. The donor model (NavalDLC's `NavalPartyNavigationModel`) encoded attached-to inheritance; the port dropped that limb when it rewrote the one method it changed. Caught by Codex, missed by all 5 deep-review agents (none opened `MobileParty`). Extract the full decision to a pure service method and pin it with a decision-matrix test. See `feedback_gamemodel_capability_engine_propagation` + `docs/reviews/rca-navaltravel-2026-06-24.md`. **Corollary:** when you keep N-1 limbs of a faithful engine-model port and rewrite limb N, re-audit limb N's *entire* behavior, not just the sub-change you intended.

## Existing Overrides (70 total)

| Model | Base | Feature |
|-------|------|---------|
| `TaomAgentStatCalculateModel` | `SandboxAgentStatCalculateModel` (SandBox) | `CareerSystem` (+ the career troop knockdown counter at the end of `GetKnockDownResistance`, campaign only, #769 E10; + the CultureDoctrine aggression post-pass + the #892 horse-stand polearm favour post-pass, right after it + the CombatMechanics mount charge multiplier #610 + the career mount bonuses #611 + the RaceAbilities post-pass and resistances + the CreatureSiegeRole detachment cost, read on the async AI thread; mount-side rules key on `RiderAgent`, a mount's own `Character` is null) |
| `TaomBattleMoraleModel` | `SandboxBattleMoraleModel` (SandBox) | `CultureDoctrine` (+ the RaceAbilities panic gate) |
| `TaomCustomBattleMoraleModel` | `CustomBattleMoraleModel` | `CultureDoctrine` (Custom Battle; + the RaceAbilities panic gate) |
| `TaomCustomBattleDamageModel` | `CustomAgentApplyDamageModel` | `CombatMechanics` (Custom Battle only, hand-wired in `SubModule.RegisterCustomBattleModels`, never declared by a module: the twin of `TaomCombatMechanicsModel`: the same base and hook calls in the same order as the campaign model (`CustomBattleDamageModelTests` pins it); includes the creature bandits' damage-taken step, the RaceAbilities hooks and the CreatureSiegeRole gate blow; no career passives, no Refuge) |
| `TaomCustomBattleAgentStatCalculateModel` | `CustomBattleAgentStatCalculateModel` | `CultureDoctrine` (Custom Battle; + the #892 horse-stand polearm favour post-pass; + the CombatMechanics mount charge multiplier, from `InitializeAgentStats` because that base writes `MountChargeDamage` once; + the RaceAbilities post-pass and resistances + the CreatureSiegeRole detachment cost) |
| `TaomClanTierModel` | `DefaultClanTierModel` | `CareerSystem` |
| `TaomInventoryCapacityModel` | `DefaultInventoryCapacityModel` | `CareerSystem` |
| `TaomPartyTradeModel` | `DefaultPartyTradeModel` | `CareerSystem` (`TradePrices`, #769 E11) |
| `TaomMapVisibilityModel` | `DefaultMapVisibilityModel` | `CareerSystem` (+ sight contributors) |
| `TaomBanditDensityModel` | `DefaultBanditDensityModel` | `BanditManagement` |
| `TaomNotableSpawnModel` | `DefaultNotableSpawnModel` | `CulturalFeats` |
| `TaomMarriageModel` | `DefaultMarriageModel` | `NazgulFamily` + `MarriageAlignment` (one slot, two rules) |
| `TaomSettlementEconomyModel` | `DefaultSettlementEconomyModel` | `SettlementEconomy` |
| `TaomSettlementFoodModel` | `DefaultSettlementFoodModel` | `SettlementFood` |
| `TaomSiegeEventModel` | `DefaultSiegeEventModel` | `Siege` |
| `TaomTargetScoreModel` | `DefaultTargetScoreCalculatingModel` | `ArmyTargeting` (+ the WarChronicle army focus during a siege window, through `ArmyTargetFocusRegistry`) |
| `TaomPartyNavigationModel` | `DefaultPartyNavigationModel` | `NavalTravel` — **not registered**; the `AddModel` call in `SubModule.cs` is commented out, so these overrides do not run |
| `TaomCharacterStatsModel` | `DefaultCharacterStatsModel` | `TroopProgression` |
| `TaomPartyWageModel` | `DefaultPartyWageModel` | `CulturalFeats` (+ career `RecruitCost`, #769 E11) |
| `TaomVolunteerModel` | `DefaultVolunteerModel` | `TroopProgression` (+ the WarChronicle `VolunteerRate` multiplier through `VolunteerProductionService`; none for the player's own clan; `AlignmentRecruitment`, `CastleRecruitment`) |
| `TaomArmyManagementModel` | `DefaultArmyManagementCalculationModel` | `CulturalFeats` (+ the `KingsMuster` ruler cost discount) |
| `TaomPartySpeedModel` | `DefaultPartySpeedCalculatingModel` | `CulturalFeats` (+ the EyeOfSauron hunter speed floor, last, `IEyeOfSauronHook`; worker threads, snapshot only) |
| `TaomSettlementProsperityModel` | `DefaultSettlementProsperityModel` | `CulturalFeats` |
| `TaomSettlementMilitiaModel` | `DefaultSettlementMilitiaModel` | `CulturalFeats` |
| `TaomBuildingConstructionModel` | `DefaultBuildingConstructionModel` | `CulturalFeats` |
| `TaomVillageProductionModel` | `DefaultVillageProductionCalculatorModel` | `CulturalFeats` |
| `TaomCaravanModel` | `DefaultCaravanModel` | `CulturalFeats` + `CaravanTrade` |
| `TaomBattleRewardModel` | `DefaultBattleRewardModel` | `CulturalFeats` (+ career `InfluenceGain`, `LootGold`, #769 E11) |
| `TaomPartyTroopUpgradeModel` | `DefaultPartyTroopUpgradeModel` | `CulturalFeats` |
| `TaomPartySizeModel` | `DefaultPartySizeLimitModel` | `CulturalFeats` (+ the KeepWardens castle patrol limit, `ICastlePatrolSizeHook`, and the patrol roster fit, `IPatrolRosterHook`; + the EyeOfSauron host size bonus before the TroopWeight line, `IEyeOfSauronHook`) |
| `TaomFoodConsumptionModel` | `DefaultMobilePartyFoodConsumptionModel` | `CulturalFeats` (+ career `FoodConsumption`, #769 E11) |
| `TaomSettlementLoyaltyModel` | `DefaultSettlementLoyaltyModel` | `CulturalFeats` |
| `TaomPartyMoraleModel` | `DefaultPartyMoraleModel` | `CulturalFeats` |
| `TaomSmithingModel` | `DefaultSmithingModel` | `CulturalFeats` |
| `TaomClanFinanceModel` | `DefaultClanFinanceModel` | `CulturalFeats` (+ career `DailyGold`, #769 E11) |
| `TaomRaidModel` | `DefaultRaidModel` | `CulturalFeats` (+ career `RaidLoot`, #769 E11) |
| `TaomMilitaryPowerModel` | `DefaultMilitaryPowerModel` | `BattleBalance` |
| `TaomCombatSimulationModel` | `DefaultCombatSimulationModel` | `BattleBalance` |
| `TaomPartyHealingModel` | `DefaultPartyHealingModel` | `Arena` (+ career `WoundRecovery`, #769 E11) |
| `TaomTournamentModel` | `DefaultTournamentModel` | `Arena` (+ the TournamentRewards renown and influence) |
| `TaomAgeModel` | `DefaultAgeModel` | `RaceAge` |
| `TaomPregnancyModel` | `DefaultPregnancyModel` | `RaceAge` |
| `TaomHeroCreationModel` | `DefaultHeroCreationModel` | `RaceAge` (+ the GeneratedLordKits adult-lord kit) |
| `TaomEquipmentSelectionModel` | `DefaultEquipmentSelectionModel` | `GeneratedLordKits` |
| `TaomAllianceModel` | `DefaultAllianceModel` | `Diplomacy` |
| `TaomKingdomDecisionPermissionModel` | `DefaultKingdomDecisionPermissionModel` | `Diplomacy` |
| `TaomDiplomacyModel` | `DefaultDiplomacyModel` | `Diplomacy` (+ `Relations`: `GetHeroesForEffectiveRelation`, `GetRelationCostOfDisbandingArmy`, `GetRelationChangeAfterClanLeaderIsDead`) |
| `TaomInformationRestrictionModel` | `DefaultInformationRestrictionModel` | `Encyclopedia` |
| `TaomCombatMechanicsModel` | `TaomAgentApplyDamageModel` (CareerSystem, **abstract**: career passives via inheritance; ultimately `SandboxAgentApplyDamageModel`) | `CombatMechanics` (+ the RaceAbilities damage, crush-through and shrug-off hooks + the career crush hold ending `DecideCrushedThrough`, #769 E10 + the CreatureSiegeRole gate blow in `ApplyDamageScaling`); campaign only, because the SandBox base throws on a mounted hit outside a campaign: Custom Battle runs `TaomCustomBattleDamageModel` |
| `TaomPrisonerRecruitmentCalculationModel` | `DefaultPrisonerRecruitmentCalculationModel` | `PrisonerRecruitment` (+ career `PrisonerConversion`, #769 E11) |
| `TaomBattleBannerBearersModel` | `SandboxBattleBannerBearersModel` (SandBox) | `BannerBearers` |
| `TaomCustomBattleBannerBearersModel` | `CustomBattleBannerBearersModel` | `BannerBearers` (Custom Battle) |
| `TaomBattleInitializationModel` | `SandboxBattleInitializationModel` (SandBox) | `Enlistment` |
| `TaomTroopSupplierProbabilityModel` | `DefaultTroopSupplierProbabilityModel` | `SiegeForces` (campaign only, declared by the feature module through `GameModelDecl`; base first, then the service drops the troops left out of a wall battle while a picker plan is armed) |
| `TaomVoiceOverModel` | `DefaultVoiceOverModel` | `VoiceAccents` (campaign only, declared by the feature module through `GameModelDecl`) |
| `TaomBannerItemModel` | `DefaultBannerItemModel` | `LordBanners` (campaign only, declared by the feature module through `GameModelDecl`) |
| `TaomVassalRewardsModel` | `DefaultVassalRewardsModel` | `LordBanners` (campaign only, declared by the feature module through `GameModelDecl`) |
| `TaomBarterModel` | `DefaultBarterModel` | `BondsOfBattle` (campaign only, declared by the feature module through `GameModelDecl`; only the overpay relation method, null from the service means `base`) |
| `TaomBuildingModel` | `DefaultBuildingModel` | `TaomBuildings` (campaign only; allows a TAOM building type in a new-game town only for its culture; detail in gamemodel-registry.md) |
| `TaomTroopSacrificeModel` | `DefaultTroopSacrificeModel` | `PosternGate` (campaign only; base first, then the besieged fief's Postern Gate factor; detail in gamemodel-registry.md) |
| `TaomSettlementPatrolModel` | `DefaultSettlementPatrolModel` | `KeepWardens` (campaign only; detail in gamemodel-registry.md) |
| `TaomSettlementSecurityModel` | `DefaultSettlementSecurityModel` | `KeepWardens` (campaign only; castle patrol security after base; detail in gamemodel-registry.md) |
| `TaomBuildingScoreModel` | `DefaultBuildingScoreCalculationModel` | `KeepWardens` (campaign only; AI Guard House pick before base; detail in gamemodel-registry.md) |
| `TaomPrisonerDonationModel` | `DefaultPrisonerDonationModel` | `GoodDeeds` (campaign only, declared by the feature module through `GameModelDecl`; base first, then the bonus and the captive-against-owner side scaling; detail in gamemodel-registry.md) |
| `TaomMobilePartyAIModel` | `DefaultMobilePartyAIModel` | `UnlookedForAid` + `FireAndTribute` (campaign only; `ShouldConsiderAttacking` and `ShouldConsiderAvoiding`: the hosts' own-side check first, then base, then the siege rule on avoid; async AI worker; detail in gamemodel-registry.md) |
| `TaomDefectionModel` | `DefaultDefectionModel` | `SwornAnew` (campaign only; `GameModelDecl` slot argument `DefaultDefectionModel`; `CanHeroDefectToFaction` only; detail in gamemodel-registry.md) |
| `TaomGenericXpModel` | `DefaultGenericXpModel` | `SeasonedHosts` (campaign only, declared by the feature module through `GameModelDecl`; `GetXpMultiplier` only, base times the service factor; detail in gamemodel-registry.md) |
| `TaomPartyTrainingModel` | `DefaultPartyTrainingModel` | `SeasonedHosts` + `CareerSystem` (campaign only, the one PartyTrainingModel; `GetEffectiveDailyExperience` and `CalculateXpGainFromBattles`, base first then one `AddFactor`; the daily member then adds the career `TroopTraining` flat term through `ApplyFlat`, #769 E11; detail in gamemodel-registry.md) |
| `TaomCharacterDevelopmentModel` | `DefaultCharacterDevelopmentModel` | `SeasonedHosts` (campaign only; `CalculateLearningRate` only, base first then a raised minimum; detail in gamemodel-registry.md) |
