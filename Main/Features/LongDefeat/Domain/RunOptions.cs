namespace TAOM.Features.LongDefeat.Domain;

/// <summary>What one run is told beyond the shipped data: a wave target, the unattended bot, a smaller garrison for a losing run.</summary>
internal sealed record RunOptions
{
    public static RunOptions Default { get; } = new();

    /// <summary>The run ends as TargetReached when this many waves are cleared; null plays until it is lost.</summary>
    public int? StopAfterWaves { get; init; }

    public bool Autoplay { get; init; }

    public float GarrisonScale { get; init; } = 1f;

    /// <summary>One garrison row's soldier count under <see cref="GarrisonScale"/>, at least 1. A scale that is not a positive finite number leaves the count as it is.</summary>
    public int ScaledCount(int count)
    {
        if (!(GarrisonScale > 0f) || float.IsInfinity(GarrisonScale))
            return count;

        return System.Math.Max(1, (int)System.Math.Round(count * GarrisonScale));
    }
}
