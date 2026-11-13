namespace TAOM.Features.LongDefeat.Domain;

/// <summary>What one run is told beyond the shipped data: a wave target, the unattended bot, a smaller garrison for a losing run.</summary>
internal sealed record RunOptions
{
    public static RunOptions Default { get; } = new();

    /// <summary>The largest seed, 2^53-1: a JavaScript reader (the leaderboard bot may be one) keeps every digit of a number up to there and rounds above it.</summary>
    public const ulong MaxSeed = (1UL << 53) - 1;

    public const int MaxStartGold = 100_000;

    /// <summary>The run ends as TargetReached when this many waves are cleared; null plays until it is lost.</summary>
    public int? StopAfterWaves { get; init; }

    public bool Autoplay { get; init; }

    public float GarrisonScale { get; init; } = 1f;

    /// <summary>The seed every roll of the run derives from (see <see cref="RunRandom"/>), 0 to <see cref="MaxSeed"/> (2^53-1). Null until <see cref="Seeded"/> takes the start time's ticks.</summary>
    public ulong? Seed { get; init; }

    /// <summary>A test option: the run begins at this wave number. <c>WavesCleared</c> still starts at 0, so <c>StopAfterWaves</c> counts clears, not wave numbers. A board run needs 1.</summary>
    public int StartWave { get; init; } = 1;

    /// <summary>A test option: boon rerolls in the bank at the start, 0 to 3. A board run needs 0.</summary>
    public int StartRerolls { get; init; }

    /// <summary>A test option: the gold the run starts with instead of the shop's, 0 to <see cref="MaxStartGold"/>. Null (a board run) keeps the shop's.</summary>
    public int? StartGold { get; init; }

    /// <summary>
    /// A test option (#1160): at this clear, when a wave follows it, every ram and siege tower that stands is destroyed, as a player's hits would destroy it, so an unattended run proves the next wave
    /// brings them back. Null (a board run) breaks nothing.
    /// </summary>
    public int? BreakEnginesAtClear { get; init; }

    /// <summary>
    /// The class the lord plays, by its id in classes.json. Null asks for none: the catalog then takes the first class on offer for a person, or the seed's draw for the bot. After
    /// <c>BuildPlan</c> this holds the class that was actually chosen (null when the fortress offers none), which is what the report writes.
    /// </summary>
    public string? ClassId { get; init; }

    /// <summary>These options with a seed: the one given, else the start time's ticks (0 is a valid seed, so only null takes the clock).</summary>
    public RunOptions Seeded(long startTicks) => Seed != null ? this : this with { Seed = (ulong)startTicks & MaxSeed };

    /// <summary>One garrison row's soldier count under <see cref="GarrisonScale"/>, at least 1. A scale that is not a positive finite number leaves the count as it is.</summary>
    public int ScaledCount(int count)
    {
        if (!(GarrisonScale > 0f) || float.IsInfinity(GarrisonScale))
            return count;

        return System.Math.Max(1, (int)System.Math.Round(count * GarrisonScale));
    }
}
