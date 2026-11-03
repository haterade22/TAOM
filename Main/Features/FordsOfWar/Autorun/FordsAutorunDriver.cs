using System;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar.Diagnostics;

namespace TAOM.Features.FordsOfWar.Autorun;

/// <summary>
/// Plays one Fords of War match with nobody at the keyboard. Ticked from the client's application tick with wall time: waits for the
/// main menu, starts the match through the prompt-free route, lets it run for the plan's mission seconds, ends it through the same
/// exit the panel button uses, waits for the final=1 report pass, writes <c>fords_autorun_done.txt</c> and quits the game. An
/// unarmed driver does nothing. The phase decisions are <see cref="FordsAutorunRules"/>; every engine call goes through the host.
/// </summary>
internal sealed class FordsAutorunDriver
{
    internal const double FinalWaitSeconds = 120.0;
    private const double CountdownShotSeconds = 4.0;

    private enum Phase { Off, WaitMenu, WaitLive, Live, WaitFinal, Done }

    private readonly FordsAutorunPlan _plan;
    private readonly FordsRunReport _report;
    private readonly FordsConfigCatalog _catalog;
    private readonly IFordsAutorunHost _host;
    private readonly FordsAutorunOutput _out;
    private readonly string _startedAt = DateTime.Now.ToString("o");
    private readonly FordsAutorunBeat _beat = new();
    private readonly FordsAutorunMenuGate _menu = new();

    private Phase _phase;
    private string? _outcome;
    private double _wall;
    private double _phaseStart;
    private double _startWall;
    private double? _endGameSeenWall;
    private int _nextScreenshot;
    private bool _countdownShot;

    public FordsAutorunDriver(FordsAutorunPlan plan, FordsRunReport report, FordsConfigCatalog catalog, IFordsAutorunHost host, IModLogger logger)
    {
        _plan = plan;
        _report = report;
        _catalog = catalog;
        _host = host;
        _out = new FordsAutorunOutput(host, logger);
        _out.Announce(plan);
        if (plan.Armed)
            _phase = Phase.WaitMenu;
    }

    /// <summary>One engine application tick. <paramref name="dt"/> is wall seconds. An unarmed or finished driver returns at once.</summary>
    internal void Tick(float dt)
    {
        if (_phase == Phase.Off || _phase == Phase.Done || !(dt > 0f))
            return;

        _wall += dt;
        switch (_phase)
        {
            case Phase.WaitMenu: WaitMenu(dt); break;
            case Phase.WaitLive: WaitLive(); break;
            case Phase.Live: Live(); break;
            case Phase.WaitFinal: WaitFinal(); break;
        }

        if (_phase != Phase.Done && _beat.StatusDue(_wall))
            _out.Write(FordsAutorunDone.StatusFile, FordsAutorunDone.Status(_phase.ToString(), _wall, _report));
    }

    private void WaitMenu(float dt)
    {
        if (!_menu.Settled(_host, dt, _out.Info))
            return;

        if (!FordsSetupChoices.TryResolve(_catalog, _plan.West, _plan.East, _plan.Ai, _plan.Scene, out var setup, out var error))
        {
            _out.Warn("start refused: " + error);
            Finish("start_refused");
            return;
        }

        _startWall = _wall;
        if (_host.Start(setup with { WestAi = _plan.WestAi }))
            Enter(Phase.WaitLive);
        else
            Finish("start_refused");
    }

    private void WaitLive()
    {
        // The countdown's picture, a few seconds into it, so a run also shows what the player sees before the match goes live.
        if (!_countdownShot && _plan.ScreenshotSeconds.Count > 0 && _report.MissionSeconds >= CountdownShotSeconds)
        {
            _countdownShot = true;
            _out.Screenshot("hud_countdown.bmp");
        }

        switch (FordsAutorunRules.AfterTheStart(_report.Run, _wall - _phaseStart, _plan.LiveWaitSeconds))
        {
            case FordsAutorunRules.AfterStart.GoLive: Enter(Phase.Live); break;
            case FordsAutorunRules.AfterStart.StartFailed: _outcome = "start_failed"; Enter(Phase.WaitFinal); break;
            case FordsAutorunRules.AfterStart.NeverLive: EndMatch("AutorunNeverLive", "never_live"); break;
        }
    }

    private void Live()
    {
        var clock = _beat.ClockLine(_wall, _report.MissionSeconds, _report.Run?.Perf.LiveSeconds ?? 0.0);
        if (clock != null)
            _out.Info(clock);

        var run = _report.Run;
        TakeDueScreenshots(run?.Perf.LiveSeconds ?? 0.0);
        if (run?.End.EndGameWall != null && _endGameSeenWall == null)
            _endGameSeenWall = _wall;

        var sinceEndGame = _endGameSeenWall == null ? 0.0 : _wall - _endGameSeenWall.Value;
        switch (FordsAutorunRules.DuringTheMatch(run, _wall - _startWall, _plan.WallLimitSeconds, _plan.LiveSeconds, sinceEndGame))
        {
            case FordsAutorunRules.WhileLive.MissionEnded: Enter(Phase.WaitFinal); break;
            case FordsAutorunRules.WhileLive.WallLimit: EndMatch("AutorunWallLimit", "wall_limit"); break;
            case FordsAutorunRules.WhileLive.LiveLimit: EndMatch("AutorunLimit", null); break;
            case FordsAutorunRules.WhileLive.ExitStalled: Finish("exit_stalled"); break;
        }
    }

    private void TakeDueScreenshots(double live)
    {
        var shots = _plan.ScreenshotSeconds;
        while (_nextScreenshot < shots.Count && live >= shots[_nextScreenshot])
        {
            var name = "hud_" + FordsAutorunDone.F1(shots[_nextScreenshot]).Replace('.', '_') + ".bmp";
            _nextScreenshot++;
            _out.Screenshot(name);
        }
    }

    private void WaitFinal()
    {
        if (FordsAutorunRules.FinalPassRan(_report.Run))
            Finish(_outcome ?? "final");
        else if (_wall - _phaseStart > FinalWaitSeconds)
            Finish(_outcome == null ? "final_timeout" : _outcome + "+final_timeout");
    }

    /// <summary>The panel button's way out: the report's exit evidence, then EndGame. The mission closes, the menu returns, the final pass runs.</summary>
    private void EndMatch(string exitReason, string? outcome)
    {
        _outcome = outcome;
        _out.Info("endGame live=" + FordsAutorunDone.F1(_report.Run?.Perf.LiveSeconds ?? 0.0) + " reason=" + exitReason);
        _report.ExitDue(exitReason);
        _report.EndGameRequested("autorun");
        Enter(Phase.WaitFinal);
        _host.ReturnToMainMenu();
    }

    private void Finish(string reason)
    {
        Enter(Phase.Done);
        _out.Write(FordsAutorunDone.DoneFile, FordsAutorunDone.Text(reason, _report, _wall, _startedAt, DateTime.Now.ToString("o")));
        _out.Info("done reason=" + reason + " quit=" + (_plan.QuitWhenDone ? 1 : 0));
        if (_plan.QuitWhenDone)
            _host.Quit();
    }

    private void Enter(Phase next)
    {
        _out.Info("phase " + _phase + " -> " + next + " wall=" + FordsAutorunDone.F1(_wall) + " mt=" + FordsAutorunDone.F1(_report.MissionSeconds)
            + " live=" + FordsAutorunDone.F1(_report.Run?.Perf.LiveSeconds ?? 0.0));
        _phase = next;
        _phaseStart = _wall;
        if (next == Phase.Live)
            _beat.StartClock(_wall, _report.MissionSeconds);
    }
}
