using System;
using TAOM.Core.Logging;

namespace TAOM.Features.LongDefeat.Autorun;

/// <summary>
/// Plays one Long Defeat run with nobody at the keyboard: starts it from the main menu, leaves when it ends or a limit passes,
/// writes <c>long_defeat_done.txt</c> and quits. An unarmed driver does nothing.
/// </summary>
internal sealed class LongDefeatAutorunDriver
{
    internal const double MenuSettleSeconds = 4.0;
    internal const double StartWaitSeconds = 600.0;
    internal const double ExitWaitSeconds = 120.0;

    internal const double EndPanelSeconds = 2.0;
    private const double StatusSeconds = 5.0;
    private const int HiddenLogCap = 20;
    private const string Prefix = "[LongDefeat][Autorun] ";

    private enum Phase { Off, WaitMenu, WaitStart, Live, WaitMenuBack, Done }

    private readonly LongDefeatAutorunPlan _plan;
    private readonly LongDefeatSession _session;
    private readonly LongDefeatConfigCatalog _catalog;
    private readonly ILongDefeatAutorunHost _host;
    private readonly IModLogger _logger;
    private readonly string _startedAt = DateTime.Now.ToString("o");

    private Phase _phase;
    private string _outcome = "";
    private double _wall;
    private double _phaseStart;
    private double _startWall;
    private double _resultWall = -1.0;
    private double _menuSeconds;
    private double _lastStatus;
    private int _hidden;
    private int _nextScreenshot;

    public LongDefeatAutorunDriver(LongDefeatAutorunPlan plan, LongDefeatSession session, LongDefeatConfigCatalog catalog, ILongDefeatAutorunHost host,
        IModLogger logger)
    {
        _plan = plan;
        _session = session;
        _catalog = catalog;
        _host = host;
        _logger = logger;
        if (!plan.Armed)
            return;

        _logger.LogInfo(Prefix + "armed plan=" + plan.Describe());
        foreach (var warning in plan.Warnings)
            _logger.LogWarning(Prefix + "arm file: " + warning);
        _phase = Phase.WaitMenu;
    }

    /// <summary>One engine application tick. <paramref name="dt"/> is wall seconds. An unarmed or finished driver, or a bad dt, returns at once.</summary>
    internal void Tick(float dt)
    {
        if (_phase == Phase.Off || _phase == Phase.Done || !(dt > 0f))
            return;

        _wall += dt;
        switch (_phase)
        {
            case Phase.WaitMenu: WaitMenu(dt); break;
            case Phase.WaitStart: WaitStart(); break;
            case Phase.Live: Live(); break;
            case Phase.WaitMenuBack: WaitMenuBack(); break;
        }

        if (_phase != Phase.Done && _wall - _lastStatus >= StatusSeconds)
        {
            _lastStatus = _wall;
            Write(LongDefeatAutorunDone.StatusFile, LongDefeatAutorunDone.Status(_phase.ToString(), _wall, _session));
        }
    }

    private void WaitMenu(float dt)
    {
        if (!_host.AtMainMenu)
        {
            _menuSeconds = 0.0;
            return;
        }

        _menuSeconds += dt;
        if (_host.HideAnyInquiry() && ++_hidden <= HiddenLogCap)
            _logger.LogInfo(Prefix + "inquiry hidden n=" + _hidden + " phase=WaitMenu");
        if (!(_menuSeconds > MenuSettleSeconds))
            return;

        var fortress = FortressId();
        if (fortress == null)
        {
            _logger.LogWarning(Prefix + "start refused: no such fortress '" + (_plan.Fortress ?? "(first)") + "' in the shipped config");
            Finish("start_refused");
            return;
        }

        _startWall = _wall;
        if (_host.Start(_plan.ToSetup(fortress)))
            Enter(Phase.WaitStart);
        else
            Finish("start_refused");
    }

    private string? FortressId()
    {
        if (_plan.Fortress != null)
            return _catalog.TryGetFortress(_plan.Fortress, out var named) ? named.Id : null;

        return _catalog.Fortresses.Count > 0 ? _catalog.Fortresses[0].Id : null;
    }

    private void WaitStart()
    {
        if (_session.IsActive || _session.Result != null || _session.Fault != null)
            Enter(Phase.Live);
        else if (_wall - _phaseStart > StartWaitSeconds)
            Leave("never_started");
    }

    private void Live()
    {
        TakeDueScreenshots();
        if (_session.Fault != null)
            Leave("fault");
        else if (_session.Result is { } result)
        {
            if (_resultWall < 0.0)
                _resultWall = _wall;
            if (_wall - _resultWall >= EndPanelSeconds)
                Leave(LongDefeatAutorunDone.ReasonFor(result.Reason));
        }
        else if (_wall - _startWall >= _plan.WallLimitSeconds)
            Leave("wall_limit");
        else if (_session.LiveSeconds >= _plan.LiveLimitSeconds)
            Leave("live_limit");
    }

    private void TakeDueScreenshots()
    {
        var shots = _plan.ScreenshotSeconds;
        while (_nextScreenshot < shots.Count && _session.LiveSeconds >= shots[_nextScreenshot])
        {
            var name = "hud_" + LongDefeatAutorunDone.F1(shots[_nextScreenshot]).Replace('.', '_') + ".bmp";
            _nextScreenshot++;
            try
            {
                _host.TakeScreenshot(name);
                _logger.LogInfo(Prefix + "screenshot " + name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(Prefix + "screenshot " + name + " failed (" + ex.GetType().Name + ": " + ex.Message + ")");
            }
        }
    }

    private void WaitMenuBack()
    {
        if (_host.AtMainMenu && !_session.IsActive)
            Finish(_outcome);
        else if (_wall - _phaseStart > ExitWaitSeconds)
            Finish(_outcome + "+exit_stalled");
    }

    private void Leave(string reason)
    {
        _outcome = reason;
        _logger.LogInfo(Prefix + "leaving reason=" + reason + " live=" + LongDefeatAutorunDone.F1(_session.LiveSeconds));
        Enter(Phase.WaitMenuBack);
        _host.ReturnToMainMenu();
    }

    private void Finish(string reason)
    {
        Enter(Phase.Done);
        Write(LongDefeatAutorunDone.DoneFile, LongDefeatAutorunDone.Text(reason, _session, _logger.LogFilePath, _plan.Expect, _wall, _startedAt,
            DateTime.Now.ToString("o")));
        _logger.LogInfo(Prefix + "done reason=" + reason + " quit=" + (_plan.QuitWhenDone ? 1 : 0));
        if (_plan.QuitWhenDone)
            _host.Quit();
    }

    private void Enter(Phase next)
    {
        _logger.LogInfo(Prefix + "phase " + _phase + " -> " + next + " wall=" + LongDefeatAutorunDone.F1(_wall) + " live=" + LongDefeatAutorunDone.F1(_session.LiveSeconds));
        _phase = next;
        _phaseStart = _wall;
    }

    private void Write(string name, string text)
    {
        try
        {
            _host.WriteFile(name, text);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(Prefix + "could not write " + name + ": " + ex.GetType().Name + ": " + ex.Message);
        }
    }
}
