using System.Linq;

namespace TAOM.Features.FordsOfWar.Diagnostics;

internal static partial class FordsVerdicts
{
    private static FordsVerdictRow Check13(FordsRunEvidence run)
    {
        var captured = run.Points.Values.Sum(p => p.Captures);
        var contested = run.Points.Values.Sum(p => p.ContestedSeconds);
        var radius = run.Facts?.CaptureRadius ?? 0f;
        var approach = string.Join(" ", run.Points.Select(p => p.Key + ":" + (p.Value.NearestApproach == double.MaxValue ? "-" : N(p.Value.NearestApproach))));

        return Row(13, new[]
        {
            Part("capture", captured > 0 ? FordsResult.Pass : FordsResult.NotSeen, "captures=" + captured),
            Part("contested", contested > 0.0 ? FordsResult.Pass : FordsResult.NotSeen, "seconds=" + N(contested)),
        }, "captureRadius=" + N(radius) + " nearestApproach " + (approach == "" ? "-" : approach),
            "A point changed hands and was contested for some time; the row text and occupancy counting are unit-tested over the same samples.",
            "The owner, the percent and the contested mark are drawn.");
    }

    private static string Span(double? from, double? to) => from == null || to == null ? "-" : N(to.Value - from.Value);

    private static FordsVerdictRow Check14(FordsRunEvidence run)
    {
        var e = run.End;
        var keep = e.KeepFellMt != null ? FordsResult.Pass : FordsResult.NotSeen;

        var result = FordsResult.NotSeen;
        if (e.Winner != null)
        {
            if (e.ResultSuccess == null)
                result = run.Closed ? FordsResult.Fail : FordsResult.NotSeen;
            else
                result = e.ResultSuccess == (e.Winner == "West") ? FordsResult.Pass : FordsResult.Fail;
        }

        var exit = FordsResult.NotSeen;
        if (e.Winner != null && e.EndGameVia != null && e.ClosedWall != null && e.EndGameWall != null)
            exit = e.ClosedWall.Value - e.EndGameWall.Value <= 15.0 ? FordsResult.Pass : FordsResult.Fail;

        var menu = e.MenuReturnedWall != null ? FordsResult.Pass : FordsResult.NotSeen;
        var info = "winnerToExitDueMt=" + Span(e.WinnerMt, e.ExitDueMt) + " winnerToEndGameWall=" + Span(e.WinnerWall, e.EndGameWall)
            + " endGameToClosedWall=" + Span(e.EndGameWall, e.ClosedWall) + " closedToMenuWall=" + Span(e.ClosedWall, e.MenuReturnedWall);

        return Row(14, new[]
        {
            Part("keep", keep, "fellMt=" + (e.KeepFellMt == null ? "-" : N(e.KeepFellMt.Value))),
            Part("result", result, "winner=" + (e.Winner ?? "-") + " success=" + Tri(e.ResultSuccess)),
            Part("exit", exit, "via=" + (e.EndGameVia ?? "-") + (e.Winner != null && e.EndGameVia == null && run.Closed ? " left through the escape menu" : "")),
            Part("menu", menu),
        }, info,
            "A keep fell, the mission result matches the winner, the way out ended the mission within 15 seconds, and the main menu came back; the 25 second exit clock is arithmetic and is printed, not judged.",
            "The result panel appears and its button leaves at once.");
    }

    private static FordsVerdictRow Check15(FordsRunEvidence run)
    {
        var f = run.Failure;
        var e = run.End;
        var routeA = f != null && f.Route == "A";

        var failure = routeA ? FordsResult.Pass : FordsResult.NotSeen;
        var failureNumbers = f == null ? "unreachable: no failure provoked"
            : routeA ? "route=A" : "route=" + f.Route + " notice path via B or C; the 3 s exit is route A only";

        var exit = FordsResult.NotSeen;
        if (routeA)
        {
            if (e.ExitDueMt != null && e.ExitDueMt.Value - f!.Mt > 8.0)
                exit = FordsResult.Fail;
            else if (run.Closed && e.ExitReason == null)
                exit = FordsResult.Fail;
            else if (e.EndGameWall != null && e.ClosedWall != null && e.ClosedWall.Value - e.EndGameWall.Value > 15.0)
                exit = FordsResult.Fail;
            else if (e.ExitReason == "AfterFailedStart" && e.EndGameVia == "auto" && run.Closed && e.EndGameWall != null && e.ClosedWall != null)
                exit = FordsResult.Pass;
        }

        var notice = e.NoticeOk == false || e.NoticeUnacknowledged ? FordsResult.Fail : e.NoticeOk == true && e.NoticeAckWall != null ? FordsResult.Pass : FordsResult.NotSeen;
        var menu = e.MenuReturnedWall != null ? FordsResult.Pass : FordsResult.NotSeen;

        return Row(15, new[]
        {
            Part("failure", failure, failureNumbers),
            Part("exit", exit, "reason=" + (e.ExitReason ?? "-") + " via=" + (e.EndGameVia ?? "-") + " endGameToClosedWall=" + Span(e.EndGameWall, e.ClosedWall)),
            Part("notice", notice, "requested=" + e.NoticeRequested + " ok=" + Tri(e.NoticeOk) + (e.NoticeUnacknowledged ? " never acknowledged" : "")),
            Part("menu", menu),
        }, "failureToExitDueMt=" + (f == null ? "-" : Span(f.Mt, e.ExitDueMt)) + " ackAfterMenu=" + e.AckAfterMenu,
            "A forced start failure on route A left the mission within 8 mission seconds of the failure and closed it within 15 seconds of EndGame, the notice was requested and acknowledged (a notice still unacknowledged when the next run begins fails), and the main menu came back.",
            "The notice is drawn over the main menu; ackAfterMenu=true says it was still up when the menu returned.");
    }

    private static FordsVerdictRow Check16(FordsRunEvidence run)
    {
        var p = run.Perf;
        var live = p.LiveSeconds;
        var facts = run.Facts;
        var cap = facts == null ? 0 : facts.MaxLivePerSide + facts.MaxAlivePerSide;

        var capResult = FordsResult.NotSeen;
        var capNumbers = "liveSeconds=" + N(live);
        if (facts != null)
        {
            capNumbers = "cap=" + cap + " peakWest=" + p.PeakLiveWest + " peakEast=" + p.PeakLiveEast + " peakMt=" + N(p.PeakLiveMt) + " peakBacklog=" + run.Spawn.PeakBacklog;
            if (p.PeakLiveWest > cap || p.PeakLiveEast > cap)
                capResult = FordsResult.Fail;
            else if (live >= 60.0)
                capResult = FordsResult.Pass;
        }

        var duration = live >= 1800.0 ? FordsResult.Pass : FordsResult.NotSeen;
        var frames = p.Windows.Sum(w => (long)w.Frames);
        var avg = frames == 0 ? 0.0 : p.Windows.Sum(w => w.AvgMs * w.Frames) / frames;
        var p95 = p.Windows.Count == 0 ? 0.0 : p.Windows.Max(w => w.P95Ms);
        var max = p.Windows.Count == 0 ? 0.0 : p.Windows.Max(w => w.MaxMs);

        var limit = facts?.AgentLimit ?? 0;
        var agents = FordsResult.NotSeen;
        if (limit > 0)
        {
            if (p.PeakAgentsAll > limit)
                agents = FordsResult.Fail;
            else if (p.PeakAgentsAll < limit && live >= 60.0)
                agents = FordsResult.Pass;
        }

        return Row(16, new[]
        {
            Part("cap", capResult, capNumbers),
            Part("agents", agents, "limit=" + (limit == 0 ? "-" : limit.ToString()) + " peakAllAgents=" + p.PeakAgentsAll),
            Part("duration", duration, duration == FordsResult.Pass ? "liveSeconds=" + N(live) : "partial: " + N(live) + " s live"),
        }, "avgMs=" + N3(avg) + " worstP95Ms=" + N3(p95) + " maxMs=" + N3(max) + " peakAllAgents=" + p.PeakAgentsAll + " no budget set",
            "Live units per side never passed TAOM's per-side config cap and all agents stayed below the engine's agent limit, each over at least a minute of live play, and the match ran the full 30 minutes; frame times are numbers only.",
            "The frame time felt playable.");
    }
}
