using TAOM.Features.FordsOfWar.Diagnostics;

namespace TAOM.Features.FordsOfWar.Autorun;

/// <summary>The autorun's phase decisions, pure over the run's evidence and the clocks, so the driver only acts on the answer.</summary>
internal static class FordsAutorunRules
{
    internal enum AfterStart { Wait, GoLive, StartFailed, NeverLive }

    internal enum WhileLive { Wait, MissionEnded, WallLimit, LiveLimit, ExitStalled }

    /// <summary>Between the host accepting the start and the run going live. A run that closed or failed on route B or C never will.</summary>
    internal static AfterStart AfterTheStart(FordsRunEvidence? run, double phaseSeconds, double liveWaitSeconds)
    {
        if (run != null && run.Live)
            return AfterStart.GoLive;
        if (run != null && (run.Closed || run.Failure?.Route == "B" || run.Failure?.Route == "C"))
            return AfterStart.StartFailed;

        return phaseSeconds > liveWaitSeconds ? AfterStart.NeverLive : AfterStart.Wait;
    }

    /// <summary>
    /// A closed mission left by itself (a winner's exit). Once an EndGame is requested the exit is under way and nothing more is asked.
    /// The wall limit ends a match even after a winner; the live limit never does, because a winner's own exit follows. An exit that
    /// has not closed the mission <see cref="FordsAutorunDriver.FinalWaitSeconds"/> after the driver saw the EndGame is stalled.
    /// </summary>
    internal static WhileLive DuringTheMatch(FordsRunEvidence? run, double wallSinceStart, double wallLimitSeconds, double liveLimitSeconds,
        double secondsSinceEndGame)
    {
        if (run == null || run.Closed)
            return WhileLive.MissionEnded;
        if (run.End.EndGameWall != null)
            return secondsSinceEndGame > FordsAutorunDriver.FinalWaitSeconds ? WhileLive.ExitStalled : WhileLive.Wait;
        if (wallSinceStart >= wallLimitSeconds)
            return WhileLive.WallLimit;

        return run.End.Winner == null && run.Perf.LiveSeconds >= liveLimitSeconds ? WhileLive.LiveLimit : WhileLive.Wait;
    }

    /// <summary>The final=1 pass ran on the menu return, or a route C start failure finalized the run where it stood.</summary>
    internal static bool FinalPassRan(FordsRunEvidence? run) =>
        run != null && run.FinalPasses > 0 && (run.End.MenuReturnedWall != null || (run.Closed && run.Failure?.Route == "C"));
}
