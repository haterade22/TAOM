namespace TAOM.Features.Realms.Domain.Services;

/// <summary>
/// The liege army discount does not stack with a discount the army model already gave the same call (the King's Muster,
/// docs/features/kings-muster.md). The larger of the two wins: an AI ruler calling a vassal of their own title pays half,
/// not a quarter. Pure numbers, so it is tested without a campaign.
/// </summary>
public static class LiegeArmyDiscountRule
{
    /// <summary>
    /// The factor to multiply the model's result by, given the factor the model already applied
    /// (<paramref name="alreadyAppliedFactor"/>, 1 when none) and the liege discount (0.5 = half off).
    /// Returns 1 when the earlier discount is already as large or larger, and never more than 1.
    /// </summary>
    public static float RemainingFactor(float alreadyAppliedFactor, float liegeDiscount)
    {
        if (float.IsNaN(liegeDiscount) || liegeDiscount <= 0f)
            return 1f;
        var liegeFactor = liegeDiscount >= 1f ? 0f : 1f - liegeDiscount;
        if (float.IsNaN(alreadyAppliedFactor) || alreadyAppliedFactor <= 0f || alreadyAppliedFactor > 1f)
            alreadyAppliedFactor = 1f;
        if (alreadyAppliedFactor <= liegeFactor)
            return 1f;
        return liegeFactor / alreadyAppliedFactor;
    }
}
