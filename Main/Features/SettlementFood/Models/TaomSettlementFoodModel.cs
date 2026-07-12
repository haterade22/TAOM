using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Features.SettlementFood;

namespace TAOM.Features.SettlementFood.Models;

/// <summary>
/// Tunable settlement-food override. Two jobs (see docs/features/settlement-food.md):
///
/// <list type="number">
/// <item>Garrison food term: reads the RAW body count. This originally corrected a Troop-Weight leak —
/// vanilla read <c>GarrisonParty.Party.NumberOfAllMembers</c>, which TroopWeight patched to a weighted
/// count, so elite garrisons ate 2–3× intended. Since the 2026-07-11 count→limit rework the getter is raw
/// again, so this correction is now an inert no-op (kept harmless); the garrison term is correct at source.</item>
/// <item>Expose vanilla's hardcoded constants (consumption divisors, base/village/flat production,
/// storage caps) as MCM/JSON knobs so the high-prosperity food squeeze can be dialled out.</item>
/// </list>
///
/// Thin per ADR-002 / gamemodels.md: the constant properties are single expressions; the calculation
/// override does boundary conversion (<see cref="TownFoodSnapshot.FromTown"/>) then delegates the math
/// to <see cref="ISettlementFoodService"/>. Master toggle off ⇒ vanilla constants + zero delta.
/// </summary>
public class TaomSettlementFoodModel : DefaultSettlementFoodModel
{
    private readonly ISettlementFoodService _service;
    private readonly SettlementFoodConfig _config;

    public TaomSettlementFoodModel(ISettlementFoodService service, ISettlementFoodConfigProvider configProvider)
    {
        _service = service;
        _config = configProvider.GetConfig();
    }

    private bool Enabled => TaomSettings.Instance?.EnableSettlementFoodTuning ?? true;

    public override int NumberOfMenOnGarrisonToEatOneFood => Enabled ? _config.GarrisonFoodDivisor : 20;

    public override int NumberOfProsperityToEatOneFood => Enabled ? _config.ProsperityFoodDivisor : 40;

    public override int FoodStocksUpperLimit => Enabled ? _config.FoodStocksUpperLimit : 300;

    public override int CastleFoodStockUpperLimitBonus => Enabled ? _config.CastleFoodStockUpperLimitBonus : 150;

    public override ExplainedNumber CalculateTownFoodStocksChange(Town town, bool includeMarketStocks = true, bool includeDescriptions = false)
    {
        var result = base.CalculateTownFoodStocksChange(town, includeMarketStocks, includeDescriptions);
        _service.ApplyFoodAdjustment(TownFoodSnapshot.FromTown(town), _config, Enabled, ref result, includeDescriptions);
        return result;
    }
}
