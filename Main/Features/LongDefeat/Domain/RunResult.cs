namespace TAOM.Features.LongDefeat.Domain;

internal enum RunEndReason
{
    DefendersDead,
    KeepLost,
    Stalemate,
    TargetReached,
    Abandoned,

    /// <summary>The hero the plan says must live has fallen (<see cref="RunPlan.MustLive"/>, #1105). Only a story mission names one.</summary>
    HeroFell,
}

/// <summary>How a run ended. <c>Wave</c> is the wave in play at the end, <c>WavesCleared</c> the ones that were won.</summary>
internal sealed record RunResult(RunEndReason Reason, int Wave, int WavesCleared, int Kills)
{
    public bool IsFinal => Reason == RunEndReason.TargetReached;
}
