using System;
using System.Collections.Generic;

namespace TAOM.Features.Enlistment.Equipment;

/// <summary>
/// Pure service-equipment money math. Payoff (mustering-out value of issued gear):
/// Σ max(25, itemValue) — the floor applies per item so unpriced armor
/// (ItemObject.Value 0) still counts. Quartermaster price: cost − 10×tier,
/// floored at 25; a negative tier never inflates the cost.
/// </summary>
public static class EquipmentPayoffCalculator
{
    public const int MinimumItemPayoff = 25;
    public const int DiscountPerTier = 10;

    public static int CalculatePayoff(IEnumerable<int> itemValues)
    {
        if (itemValues == null)
            return 0;
        var total = 0;
        foreach (var value in itemValues)
            total += Math.Max(MinimumItemPayoff, value);
        return total;
    }

    public static int ApplyQuartermasterDiscount(int cost, int tier)
    {
        var discount = DiscountPerTier * Math.Max(0, tier);
        return Math.Max(MinimumItemPayoff, cost - discount);
    }
}
