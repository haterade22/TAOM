namespace TAOM.Features.LongDefeat.Domain;

internal enum RunEndReason
{
    DefendersDead,
    KeepLost,
    Stalemate,
    TargetReached,
    Abandoned,
}

/// <summary>How a run ended. <c>Wave</c> is the wave in play at the end, <c>WavesCleared</c> the ones that were won.</summary>
internal sealed record RunResult(RunEndReason Reason, int Wave, int WavesCleared, int Kills)
{
    public bool IsFinal => Reason == RunEndReason.TargetReached;
}
