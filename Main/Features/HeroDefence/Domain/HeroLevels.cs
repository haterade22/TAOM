using System;

namespace TAOM.Features.HeroDefence.Domain;

/// <summary>
/// The hero ladder, from level 1 (bought) to <see cref="MaxLevel"/>. Level L costs <c>price x PriceShare x PriceGrowth^(L-2)</c>, and each
/// level adds a fixed share of the level-1 value. The defaults are the plan's first guesses; the config file replaces them.
/// </summary>
internal sealed record HeroLevels
{
    /// <summary>A cooldown never falls below this share of its level-1 length, whatever the rate.</summary>
    public const float CooldownFloor = 0.25f;

    public int MaxLevel { get; init; } = 20;

    public double PriceShare { get; init; } = 0.4d;

    public double PriceGrowth { get; init; } = 1.3d;

    public float HealthPerLevel { get; init; } = 0.10f;

    public float DamagePerLevel { get; init; } = 0.08f;

    public float AbilityPerLevel { get; init; } = 0.08f;

    public float CooldownPerLevel { get; init; } = 0.02f;

    /// <summary>
    /// The gold to go from <paramref name="level"/> - 1 to <paramref name="level"/>, at least 1. 0 outside levels 2 to
    /// <see cref="MaxLevel"/>. A hero price or setting that gives a zero, negative or NaN price makes the level cost
    /// <see cref="int.MaxValue"/>, out of reach, so a bad setting never sells a free level.
    /// </summary>
    public int PriceOf(int heroPrice, int level)
    {
        if (level < 2 || level > MaxLevel)
            return 0;

        var price = heroPrice * PriceShare * Math.Pow(PriceGrowth, level - 2);
        if (!(price > 0d) || price >= int.MaxValue)
            return int.MaxValue;

        return Math.Max(1, (int)Math.Round(price, MidpointRounding.AwayFromZero));
    }

    public float HealthFactor(int level) => 1f + HealthPerLevel * Steps(level);

    public float DamageFactor(int level) => 1f + DamagePerLevel * Steps(level);

    public float AbilityFactor(int level) => 1f + AbilityPerLevel * Steps(level);

    /// <summary>Never below <see cref="CooldownFloor"/>; a NaN rate also gives the floor.</summary>
    public float CooldownFactor(int level)
    {
        var factor = 1f - CooldownPerLevel * Steps(level);
        return factor > CooldownFloor ? factor : CooldownFloor;
    }

    /// <summary>Levels above level 1, with the level clamped to the ladder. 0 when <see cref="MaxLevel"/> is below 2.</summary>
    private int Steps(int level)
    {
        var top = Math.Min(level, MaxLevel);
        return top > 1 ? top - 1 : 0;
    }
}
