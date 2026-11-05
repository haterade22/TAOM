using System;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// The war chest boons folded into four multipliers (phase2-plan.md section 2): what a kill pays, what a clear pays, what the horn pays and caps at, and
/// what a pack costs. <see cref="One"/> is a run with no boon. A factor is never below 0.
/// </summary>
internal readonly record struct GoldFactors(float Bounty, float WaveBonus, float EarlyGold, float Price)
{
    public static GoldFactors One { get; } = new(1f, 1f, 1f, 1f);

    /// <summary>An amount times a factor, rounded half away from zero. A zero or negative amount is left as it is.</summary>
    public static int Apply(int amount, float factor) =>
        amount <= 0 ? amount : (int)Math.Round(amount * (double)factor, MidpointRounding.AwayFromZero);
}
