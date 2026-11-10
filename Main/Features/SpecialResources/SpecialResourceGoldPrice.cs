using System;

namespace TAOM.Features.SpecialResources;

/// <summary>
/// The one conversion from special-resource points to gold, used by every gold-mode price (#1109):
/// the elite upgrade, recruit and upkeep surcharges, the emissary price and the encyclopedia badge.
/// </summary>
public static class SpecialResourceGoldPrice
{
    /// <summary>Under 2^24, so the amount is exact in <c>ExplainedNumber</c>'s float and an int sum cannot overflow.</summary>
    public const int MaxGold = 10_000_000;

    /// <summary>
    /// points x goldPerPoint rounded half away from zero. 0 unless both are positive (a positive
    /// requirement, so NaN gives 0). Saturates at <see cref="MaxGold"/>.
    /// </summary>
    public static int ToGold(double points, int goldPerPoint)
    {
        if (!(points > 0d) || goldPerPoint <= 0) return 0;

        var gold = Math.Round(points * goldPerPoint, MidpointRounding.AwayFromZero);
        return gold >= MaxGold ? MaxGold : (int)gold;
    }
}
