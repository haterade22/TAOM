using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar.Diagnostics;

namespace TAOM.Features.FordsOfWar;

/// <summary>
/// The automatic way out of a Fords of War mission, owned by the mission logic and driven by its tick, so it runs whether or not the
/// command panel ever attached. A finished match returns to the main menu a while after the result; a start that failed does the
/// same and then shows the notice the setup prompt shows (the log has the reason). <c>EndGame</c> is <c>async void</c>, so only a
/// synchronous failure of the call is caught and logged as a warning; a failure inside it is rethrown on the main thread, as for
/// the escape menu's exit.
/// </summary>
internal sealed class FordsMissionExit
{
    private readonly IFordsMenuAdapter _menu;
    private readonly IModLogger _logger;
    private readonly FordsRunReport _report;
    private readonly FordsExitClock _clock = new();
    private bool _failureReported;

    public FordsMissionExit(IFordsMenuAdapter menu, IModLogger logger, FordsRunReport report)
    {
        _menu = menu;
        _logger = logger;
        _report = report;
    }

    /// <summary>One mission tick. <paramref name="dt"/> is mission time, so a paused engine adds nothing.</summary>
    public void Tick(float dt, bool failedStart, bool matchEnded)
    {
        if (failedStart && !_failureReported)
        {
            _failureReported = true;
            _report.Failure("A", "AfterStart could not start the match; the ERROR line above names the cause");
        }

        var reason = _clock.Tick(dt, failedStart, matchEnded);
        if (reason == FordsExit.None)
            return;

        _report.ExitDue(reason.ToString());
        try
        {
            _menu.EndGame();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[FordsOfWar] returning to the main menu after {reason} failed: {ex.Message}");
            return;
        }

        _report.EndGameRequested("auto");
        if (reason == FordsExit.AfterFailedStart)
            FordsSetupPrompt.ShowStartFailed(_menu, _logger, _report);
    }
}
