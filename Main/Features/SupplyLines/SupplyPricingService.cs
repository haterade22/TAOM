using System;
using TAOM.Core.Validation;
using TAOM.Features.SupplyLines.Domain;

namespace TAOM.Features.SupplyLines;

/// <summary>
/// Pure pricing maths for supply orders. Ported from the SupplyLines module's
/// <c>SupplyPricing.Compute</c> / <c>GetRecruitCost</c> / <c>SetTravelTime</c>, with the
/// engine-value gathering left at the boundary so every branch is testable.
///
/// <para>Every float that reaches a decision is gated positive-first: a NaN or infinite input
/// (corrupt json2, engine oddity) fails the gate and contributes a clamped component instead of
/// poisoning the whole quote. The source module had no such guards.</para>
/// </summary>
public sealed class SupplyPricingService : ISupplyPricingService
{
    // Behaviour constants from the source module's GetRecruitCost defaults
    // (RecruitTierSurcharge 0.15, WartimeRecruitSurcharge 1.5). Deliberately NOT settings:
    // the pinned MCM surface exposes only the knobs players asked for.
    private const float TierPremiumPerTier = 0.15f;
    private const float WartimeSurcharge = 1.5f;

    // The source clamped travel time to 2 hours so a zero-distance order still animates a trip.
    private const float MinimumPlannedHours = 2f;

    private readonly ISupplyLinesSettingsProvider _settings;

    public SupplyPricingService(ISupplyLinesSettingsProvider settings)
    {
        _settings = settings;
    }

    public SupplyQuote Quote(float goodsMarketValue, int troopRecruitCost, float distance, SupplyEscortOption escort)
    {
        float safeGoodsValue = NonNegativeOrZero(goodsMarketValue);
        float safeDistance = NonNegativeOrZero(distance);

        // A broken markup factor falls back to 1.0 (charge market value), never 0: a corrupt
        // setting must not make goods free. The fee knobs fall back to 0 instead, which only
        // ever under-charges a delivery fee, not the goods themselves.
        float markup = _settings.GoodsMarkupFactor;
        if (!FiniteFloatValidator.IsFinite(markup) || markup <= 0f)
            markup = 1f;

        int goods = RoundToDenars(safeGoodsValue * markup);
        int transport = RoundToDenars(safeDistance * NonNegativeOrZero(_settings.TransportFeePerDistance));
        int guard = escort == SupplyEscortOption.Mercenaries
            ? RoundToDenars(safeDistance * NonNegativeOrZero(_settings.MercenaryWagePerDistance))
            : 0;
        int troops = troopRecruitCost > 0 ? troopRecruitCost : 0;

        return new SupplyQuote(goods, troops, transport, guard);
    }

    public int TroopPrice(int vanillaRecruitCost, int tier, bool atWar)
    {
        if (vanillaRecruitCost <= 0)
            return 0;

        float price = vanillaRecruitCost;

        // Source guard kept as-is: the premium starts above tier 1, so a tier-0 or negative
        // tier (corrupt data) never turns the premium into a discount.
        if (tier > 1)
            price *= 1f + TierPremiumPerTier * (tier - 1);

        if (atWar)
            price *= WartimeSurcharge;

        return (int)Math.Round(price);
    }

    public float PlannedHours(float distance)
    {
        float hours = NonNegativeOrZero(distance) * NonNegativeOrZero(_settings.CaravanHoursPerDistance);

        // Positive-requirement clamp: NaN cannot get here (both factors are sanitized), but the
        // comparison is written so it would still clamp up rather than leak through.
        if (!(hours >= MinimumPlannedHours))
            hours = MinimumPlannedHours;

        return hours;
    }

    /// <summary>Positive-requirement gate: NaN/Infinity/negative all collapse to 0.</summary>
    private static float NonNegativeOrZero(float value) =>
        FiniteFloatValidator.IsFiniteAtLeast(value, 0f) ? value : 0f;

    private static int RoundToDenars(float value) => (int)Math.Round(value);
}
