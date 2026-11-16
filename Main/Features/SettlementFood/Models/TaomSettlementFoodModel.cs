using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TAOM.Features.BattleBalance;
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
/// to <see cref="ISettlementFoodService"/>. Master toggle off ⇒ vanilla constants + zero delta. The #1283 garrison
/// food refund for a realm in SiegeDefence.UpkeepFreeGarrisons is a realm rule, not food tuning, so it applies with the
/// toggle off too.
/// </summary>
public class TaomSettlementFoodModel : DefaultSettlementFoodModel
{
    private readonly ISettlementFoodService _service;
    private readonly SettlementFoodConfig _config;
    private readonly IBattleBalanceConfigProvider _battleBalanceConfig;

    public TaomSettlementFoodModel(ISettlementFoodService service, ISettlementFoodConfigProvider configProvider,
        IBattleBalanceConfigProvider battleBalanceConfig)
    {
        _battleBalanceConfig = battleBalanceConfig;
        _service = service;
        _config = configProvider.GetConfig();
    }

    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    private bool Enabled => Settings?.EnableSettlementFoodTuning ?? true;

    public override int NumberOfMenOnGarrisonToEatOneFood => Enabled ? _config.GarrisonFoodDivisor : 20;

    public override int NumberOfProsperityToEatOneFood => Enabled ? _config.ProsperityFoodDivisor : 40;

    public override int FoodStocksUpperLimit => Enabled ? _config.FoodStocksUpperLimit : 300;

    public override int CastleFoodStockUpperLimitBonus => Enabled ? _config.CastleFoodStockUpperLimitBonus : 150;

    public override ExplainedNumber CalculateTownFoodStocksChange(Town town, bool includeMarketStocks = true, bool includeDescriptions = false)
    {
        var result = base.CalculateTownFoodStocksChange(town, includeMarketStocks, includeDescriptions);
        // #1283: a free realm's garrison eats nothing; give back the base's garrison term.
        var refund = GarrisonUpkeep.GarrisonFoodRefund(
            GarrisonUpkeep.IsUpkeepFree(_battleBalanceConfig?.GetConfig()?.SiegeDefence, town.MapFaction?.Culture?.StringId),
            town.GarrisonParty?.Party.NumberOfAllMembers ?? 0, NumberOfMenOnGarrisonToEatOneFood);
        if (refund > 0f)
            result.Add(refund, GameTexts.FindText("str_garrison"));
        _service.ApplyFoodAdjustment(TownFoodSnapshot.FromTown(town), _config, Enabled, ref result, includeDescriptions);
        return result;
    }
}
