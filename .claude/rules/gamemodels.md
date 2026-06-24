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
4. **Thin model class** — The model class is an entry point (<150 lines). **All logic goes in a `Service`.** Line count is a ceiling, not the test. The override body may contain ONLY one of: (a) a single constant expression (e.g. `MaxCharacterTier => 10`), (b) perk/adapter conversion at the boundary plus a direct delegate to the service. A body that contains `if`, `foreach`, `switch`, `yield` branching, or any multi-line computation is a violation — extract to a service even if the model is under 20 lines. "It's only a few lines" is not a carve-out; the rule is binary. Counter-example: `TaomCharacterStatsModel` (one constant) is legal; a 6-line `yield return` chain with a conditional is not.
5. **Adapter boundary** — Convert sealed TaleWorlds params to adapters immediately. Never pass `Hero`, `Settlement`, etc. into the service.
6. **JSON/XML config** — Configurable values live in `Main/_Module/ModuleData/configs/` or feature-specific XML, not hardcoded in the model.
7. **Register in SubModule.cs** — GameModel overrides must be returned from `CreateGameModels()` in `SubModule.cs`.
8. **Tests** — Service logic is fully unit-tested. The model class itself is thin enough to not need direct tests.
9. **Cross-entity propagation (MANDATORY when the override returns a per-entity capability/value)** — If your override returns a per-entity value (per party, per hero, per settlement) that the engine **propagates** to related entities or **recomputes per-entity**, you MUST decompile the engine *consumer* of your result and mirror that propagation — or related entities desync. Before overriding, answer two questions from the decompiled engine code: (a) **Propagation** — does the value get pushed onto attached/child entities (army attached parties, settlement bound villages, family members)? (b) **Recompute** — is it recomputed per-entity from a per-entity getter the engine drives across the group? If yes to either, the override is NOT a per-entity-isolated decision. Also encode **lifecycle**: an entity already mid-transition must retain the capability to *complete/exit* (e.g. a party already at sea keeps naval capability to reach land regardless of toggles; gates govern only new transitions). **Worked example (NavalTravel #296, the bug this rule prevents):** `PartyNavigationModel.HasNavalNavigationCapability` keyed only on `IsMainParty` stranded a player-led army's attached AI parties — the engine force-propagates `MobileParty.IsCurrentlyAtSea` down the attachment tree AND recomputes `NavigationCapability` per party (`MobileParty.cs:464-479,493-496`), so attached parties were dragged to sea with `Default`-only navigation. The donor model (NavalDLC's `NavalPartyNavigationModel`) encoded attached-to inheritance; the port dropped that limb when it rewrote the one method it changed. Caught by Codex, missed by all 5 deep-review agents (none opened `MobileParty`). Extract the full decision to a pure service method and pin it with a decision-matrix test. See `feedback_gamemodel_capability_engine_propagation` + `docs/reviews/rca-navaltravel-2026-06-24.md`. **Corollary:** when you keep N-1 limbs of a faithful engine-model port and rewrite limb N, re-audit limb N's *entire* behavior, not just the sub-change you intended.

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
