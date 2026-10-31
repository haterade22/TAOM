using System;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// The default price of a unit when an offer names none (build-spec 3.2): a base price from a tier grid, times 1.5 when
/// mounted, times the troop's elite weight. A pack costs the unit price times its size, rounded to the nearest 5.
/// </summary>
internal static class TroopPricing
{
    private const int MinPackPrice = 5;
    private const float MountedFactor = 1.5f;

    private static readonly int[] GridLevels = { 1, 6, 11, 16, 21, 26, 31, 36, 41, 46, 51 };
    private static readonly float[] GridPrices = { 4f, 6f, 9f, 13f, 18f, 25f, 34f, 45f, 60f, 80f, 105f };

    /// <summary>Between two grid levels the lower one prices the unit.</summary>
    public static float UnitPrice(int level, bool mounted, float eliteWeight)
    {
        var index = 0;
        for (var i = 0; i < GridLevels.Length; i++)
        {
            if (level >= GridLevels[i])
                index = i;
        }

        var price = GridPrices[index];
        if (mounted)
            price *= MountedFactor;
        return price * Math.Max(0f, eliteWeight);
    }

    public static int PackPrice(float unitPrice, int packSize)
    {
        if (packSize <= 0)
            return MinPackPrice;

        var rounded = (int)Math.Round(unitPrice * packSize / 5f, MidpointRounding.AwayFromZero) * 5;
        return Math.Max(MinPackPrice, rounded);
    }

    public static int PackPrice(int level, bool mounted, float eliteWeight, int packSize) =>
        PackPrice(UnitPrice(level, mounted, eliteWeight), packSize);
}
