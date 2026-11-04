namespace TAOM.Features.LongDefeat.Domain;

/// <summary>A fortress's best run: the most waves cleared, then the most kills.</summary>
internal sealed record BestRun(int Waves, int Kills)
{
    /// <summary>True when this run beats the stored one; an equal run does not, so a saved best is never rewritten for nothing.</summary>
    public bool IsBetter(BestRun? current) =>
        current == null || Waves > current.Waves || (Waves == current.Waves && Kills > current.Kills);
}
