using TAOM.Features.SupplyLines.Domain;

namespace TAOM.Features.SupplyLines;

/// <summary>
/// Pure pricing for a supply order. All inputs are primitives so the maths is fully testable;
/// callers gather market values and distances at the boundary.
/// </summary>
public interface ISupplyPricingService
{
    /// <summary>
    /// Builds the quote for one order.
    /// </summary>
    /// <param name="goodsMarketValue">Sum of unit price × qty over the chosen goods.</param>
    /// <param name="troopRecruitCost">Sum of per-troop recruitment cost over the chosen troops
    /// (already tier-scaled and war-scaled by the source service).</param>
    /// <param name="distance">Campaign-map distance from source to the player.</param>
    /// <param name="escort">Chosen escort. Mercenaries add a distance-scaled wage; a companion
    /// escort is free (the hero is the player's own).</param>
    SupplyQuote Quote(float goodsMarketValue, int troopRecruitCost, float distance, SupplyEscortOption escort);

    /// <summary>
    /// Per-troop recruitment price: the vanilla recruit cost scaled by tier premium and a wartime
    /// surcharge. Pure; the vanilla base cost is an input.
    /// </summary>
    int TroopPrice(int vanillaRecruitCost, int tier, bool atWar);

    /// <summary>Planned caravan travel time for a distance, clamped to a sane minimum.</summary>
    float PlannedHours(float distance);
}
