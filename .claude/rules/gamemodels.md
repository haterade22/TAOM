---
paths:
  - "Main/Features/**/Models/*.cs"
  - "Main/Features/**/*Model.cs"
---

# GameModel Override Rules

TAOM has 31 GameModel overrides. All follow the same pattern.

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

1. **Research first** — Always decompile `DefaultXxxModel` with `/research` before overriding. Never guess which base methods to call.
2. **Inherit from `Default*`** — Never override `GameModel` directly; inherit from the corresponding `Default*` class.
3. **Call `base.Method()`** — Unless deliberately replacing behavior, fall through to base for unhandled cases.
4. **Thin model class** — The model class is an entry point (<150 lines). All logic goes in a `Service`.
5. **Adapter boundary** — Convert sealed TaleWorlds params to adapters immediately. Never pass `Hero`, `Settlement`, etc. into the service.
6. **JSON/XML config** — Configurable values live in `Main/_Module/ModuleData/configs/` or feature-specific XML, not hardcoded in the model.
7. **Register in SubModule.cs** — GameModel overrides must be returned from `CreateGameModels()` in `SubModule.cs`.
8. **Tests** — Service logic is fully unit-tested. The model class itself is thin enough to not need direct tests.

## Registration Pattern

```csharp
// In SubModule.cs
public override void OnBeforeInitialModuleScreenSetAsRoot()
{
    // Models registered via AddModel in GetGameModels
}

protected override void OnGameStart(Game game, IGameStarter gameStarter)
{
    if (gameStarter is CampaignGameStarter campaignStarter)
    {
        campaignStarter.AddModel(new TaomFooModel(IoC.Resolve<IFooService>()));
    }
}
```

## Existing Overrides (31 total)

| Model | Base | Feature |
|-------|------|---------|
| `TaomCharacterStatsModel` | `DefaultCharacterStatsModel` | `TroopProgression` |
| `TaomPartyWageModel` | `DefaultPartyWageModel` | `CulturalFeats` |
| `TaomVolunteerModel` | `DefaultVolunteerModel` | `TroopProgression` |
| `TaomArmyManagementModel` | `DefaultArmyManagementCalculationModel` | `CulturalFeats` |
| `TaomPartySpeedModel` | `DefaultPartySpeedCalculatingModel` | `CulturalFeats` |
| `TaomSettlementProsperityModel` | `DefaultSettlementProsperityModel` | `CulturalFeats` |
| `TaomSettlementMilitiaModel` | `DefaultSettlementMilitiaModel` | `CulturalFeats` |
| `TaomBuildingConstructionModel` | `DefaultBuildingConstructionModel` | `CulturalFeats` |
| `TaomVillageProductionModel` | `DefaultVillageProductionCalculatorModel` | `CulturalFeats` |
| `TaomCaravanModel` | `DefaultCaravanModel` | `CulturalFeats` |
| `TaomBattleRewardModel` | `DefaultBattleRewardModel` | `CulturalFeats` |
| `TaomPartyTroopUpgradeModel` | `DefaultPartyTroopUpgradeModel` | `CulturalFeats` |
| `TaomPartySizeModel` | `DefaultPartySizeLimitModel` | `CulturalFeats` |
| `TaomFoodConsumptionModel` | `DefaultMobilePartyFoodConsumptionModel` | `CulturalFeats` |
| `TaomSettlementLoyaltyModel` | `DefaultSettlementLoyaltyModel` | `CulturalFeats` |
| `TaomPartyMoraleModel` | `DefaultPartyMoraleModel` | `CulturalFeats` |
| `TaomSmithingModel` | `DefaultSmithingModel` | `CulturalFeats` |
| `TaomClanFinanceModel` | `DefaultClanFinanceModel` | `CulturalFeats` |
| `TaomRaidModel` | `DefaultRaidModel` | `CulturalFeats` |
| `TaomMilitaryPowerModel` | `DefaultMilitaryPowerModel` | `BattleBalance` |
| `TaomCombatSimulationModel` | `DefaultCombatSimulationModel` | `BattleBalance` |
| `TaomPartyHealingModel` | `DefaultPartyHealingModel` | `Arena` |
| `TaomTournamentModel` | `DefaultTournamentModel` | `Arena` |
| `TaomAgeModel` | `DefaultAgeModel` | `RaceAge` |
| `TaomPregnancyModel` | `DefaultPregnancyModel` | `RaceAge` |
| `TaomHeroCreationModel` | `DefaultHeroCreationModel` | `RaceAge` |
| `TaomAllianceModel` | `DefaultAllianceModel` | `Diplomacy` |
| `TaomKingdomDecisionPermissionModel` | `DefaultKingdomDecisionPermissionModel` | `Diplomacy` |
| `TaomDiplomacyModel` | `DefaultDiplomacyModel` | `Diplomacy` |
| `TaomExecutionRelationModel` | `DefaultExecutionRelationModel` | `Execution` |
| `TaomInformationRestrictionModel` | `DefaultInformationRestrictionModel` | `Encyclopedia` |
