using System;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.Autorun.Scenario;

/// <summary>
/// Starts a new campaign and lets it run with nobody at the keyboard (#1075): waits for the main menu, starts, finishes character
/// creation, waits for the map, fast forwards until the wanted days passed, then writes <c>autorun_done.txt</c>, ends the campaign
/// and quits when asked. A pure state machine ticked with wall seconds; the decisions are in <see cref="ScenarioRules"/>. An unarmed
/// driver does nothing.
/// </summary>
internal sealed class ScenarioAutorunDriver
{
    private const string Prefix = "[Autorun][Scenario] ";

    private enum Phase { Off, WaitMenu, Creation, WaitMap, Running, Exiting, Done }

    private readonly ScenarioPlan _plan;
    private readonly ICampaignAutorunHost _host;
    private readonly IModLogger _logger;
    private readonly IOutcomeLog _outcomes;
    private readonly string _startedAt = DateTime.Now.ToString("o");

    private Phase _phase;
    private double _wall, _phaseStart, _startWall, _held, _dayChanged, _lastStatus, _dt;
    private int _dayStart = ScenarioRules.UnknownDay, _lastDay = ScenarioRules.UnknownDay;
    private int _inquiries, _notices, _resets;
    private string _prepared = "";

    public ScenarioAutorunDriver(ScenarioPlan plan, ICampaignAutorunHost host, IModLogger logger, IOutcomeLog outcomes)
    {
        _plan = plan;
        _host = host;
        _logger = logger;
        _outcomes = outcomes;
        if (!plan.Armed)
            return;

        _logger.LogInfo(Prefix + "armed plan=" + plan.Describe());
        foreach (var warning in plan.Warnings)
            _logger.LogWarning(Prefix + "arm file: " + warning);
        _phase = Phase.WaitMenu;
    }

    internal bool IsDone => _phase == Phase.Done;

    internal string PhaseName => _phase.ToString();

    /// <summary>
    /// The client's OnGameEnd. Every game end, the shutdown's included, comes here before the engine saves BannerlordConfig (Module.cs
    /// FinalizeModule: Game.OnFinalize, then BannerlordConfig.Save), so the focus setting the host changed never reaches the player's
    /// config file, whoever ended the game.
    /// </summary>
    internal void GameEnded()
    {
        if (_phase != Phase.Off)
            Guarded("restore the focus setting", _host.RestoreFocusSetting);
    }

    /// <summary>One engine application tick. <paramref name="dt"/> is wall seconds. An unarmed or finished driver, or a dt that is not a positive number, returns at once.</summary>
    internal void Tick(float dt)
    {
        if (_phase == Phase.Off || _phase == Phase.Done || !FiniteFloatValidator.IsFinite(dt) || !(dt > 0f))
            return;

        _wall += _dt = dt;
        try
        {
            Step();
            if (_phase != Phase.Done && ScenarioRules.StatusDue(_wall, _lastStatus))
            {
                _lastStatus = _wall;
                Write(ScenarioAutorunDone.StatusFile, ScenarioAutorunDone.Status(_phase.ToString(), _wall, _lastDay, _host.Screen, _host.TimeLocked));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(Prefix + "tick failed in " + _phase + ": " + ex.GetType().Name + ": " + ex.Message);
            if (_phase < Phase.Exiting)
                Finish("driver_error");
            else
                Guarded("quit", QuitIfWanted, Phase.Done);
        }
    }

    private void Step()
    {
        switch (_phase)
        {
            case Phase.WaitMenu: WaitMenu(); break;
            case Phase.Creation: Creation(); break;
            case Phase.WaitMap: WaitMap(); break;
            case Phase.Running: Running(); break;
            case Phase.Exiting: Exiting(); break;
        }
    }

    private bool Held(bool onScreen)
    {
        _held = onScreen ? _held + _dt : 0.0;
        return onScreen;
    }

    private void WaitMenu()
    {
        if (!Held(_host.Screen == AutorunScreen.MainMenu))
            return;

        HideInquiry();
        if (!ScenarioRules.MenuSettled(_held))
            return;

        _startWall = _wall;
        if (_host.StartNewCampaign())
            Enter(Phase.Creation);
        else
            Finish("start_refused");
    }

    private void Creation()
    {
        HideInquiry();
        if (!ScenarioRules.Waiting(_wall - _phaseStart, ScenarioRules.CreationWaitSeconds))
        {
            Finish("creation_timeout");
            return;
        }

        var screen = _host.Screen;
        if (screen == AutorunScreen.Video)
            _host.FinishVideo();
        else if (screen == AutorunScreen.CharacterCreation)
        {
            if (_host.TryFinishCreation(_plan.Culture, out var problem))
                Enter(Phase.WaitMap);
            else if (problem != null)
            {
                _logger.LogWarning(Prefix + "creation failed: " + problem);
                Finish("creation_failed");
            }
        }
    }

    private void WaitMap()
    {
        HideInquiry();
        if (!ScenarioRules.Waiting(_wall - _phaseStart, ScenarioRules.MapWaitSeconds))
        {
            Finish("map_timeout");
            return;
        }

        if (!Held(ScenarioRules.OnMap(_host.Screen)) || !ScenarioRules.MapSettled(_held))
            return;

        _prepared = _host.PrepareMap();
        _logger.LogInfo(Prefix + "prepared map: " + _prepared);
        _dayStart = _lastDay = ScenarioRules.WholeDay(_host.ElapsedDays);
        if (_dayStart == ScenarioRules.UnknownDay)
        {
            _logger.LogWarning(Prefix + "the campaign clock is not a day count on the map");
            Finish("map_timeout");
            return;
        }

        _dayChanged = _wall;
        Enter(Phase.Running);
    }

    private void Running()
    {
        HideInquiry();
        if (_host.HideSceneNotification())
            _notices++;
        if (_host.KeepFastForward(_plan.Speed))
            _resets++;

        var day = ScenarioRules.WholeDay(_host.ElapsedDays);
        if (day != ScenarioRules.UnknownDay && day != _lastDay)
        {
            _lastDay = day;
            _dayChanged = _wall;
        }

        if (ScenarioRules.DaysDone(_lastDay, _dayStart, _plan.Days))
            Finish(ScenarioAutorunDone.ReasonOk);
        else if (!ScenarioRules.Waiting(_wall - _dayChanged, _plan.StallSeconds))
        {
            _logger.LogWarning(Prefix + "stalled on day " + _lastDay + " screen=" + _host.Screen + " time_locked=" + (_host.TimeLocked ? 1 : 0));
            Finish("stalled");
        }
        else if (!ScenarioRules.WithinWallLimit(_wall - _startWall, _plan.WallLimitSeconds))
            Finish("wall_limit");
    }

    private void Exiting()
    {
        if (_host.Screen == AutorunScreen.MainMenu)
            Guarded("quit", QuitIfWanted, Phase.Done);
        else if (!ScenarioRules.Waiting(_wall - _phaseStart, ScenarioRules.ExitWaitSeconds))
        {
            _logger.LogWarning(Prefix + "exit_stalled: the main menu did not come back in " + ScenarioRules.ExitWaitSeconds + " s");
            Guarded("quit", QuitIfWanted, Phase.Done);
        }
    }

    private void QuitIfWanted()
    {
        if (_plan.Quit)
            _host.Quit();
    }

    private void Finish(string reason)
    {
        if (_phase >= Phase.Exiting)
            return;

        Enter(Phase.Exiting);
        var facts = new ScenarioRunFacts(reason, _dayStart, _lastDay, _inquiries, _notices, _resets, _prepared, _wall, _startedAt, DateTime.Now.ToString("o"));
        Guarded("restore the focus setting", _host.RestoreFocusSetting);
        // The text is built inside the guard, so a throw while building it still lets the outcome and the campaign end run.
        Guarded("write " + ScenarioAutorunDone.DoneFile, () => _host.WriteFile(ScenarioAutorunDone.DoneFile, ScenarioAutorunDone.Text(_plan, facts)));
        Guarded("log the result", () => _logger.LogInfo(Prefix + "done reason=" + reason + " scenario=" + _plan.Scenario + " days_run="
            + ScenarioRules.DaysRun(_dayStart, _lastDay) + " days_wanted=" + _plan.Days + " quit=" + (_plan.Quit ? 1 : 0)));
        Guarded("report the outcome", () => ScenarioOutcomes.Report(_outcomes, _plan, facts));
        Guarded("end the campaign", _host.EndCampaign);
    }

    private void HideInquiry()
    {
        var title = _host.HideInquiry();
        if (title != null && ScenarioRules.TitleLogged(++_inquiries))
            _logger.LogInfo(Prefix + "inquiry hidden n=" + _inquiries + " phase=" + _phase + " title=" + title);
    }

    private void Enter(Phase next)
    {
        _logger.LogInfo(Prefix + "phase " + _phase + " -> " + next + " wall=" + ScenarioAutorunDone.F1(_wall));
        _phase = next;
        _phaseStart = _wall;
        _held = 0.0;
    }

    private void Write(string name, string text) => Guarded("write " + name, () => _host.WriteFile(name, text));

    /// <summary>Runs one host step; a throw is logged and the caller goes on. <paramref name="then"/> is the phase to enter afterwards, if any.</summary>
    private void Guarded(string what, Action step, Phase? then = null)
    {
        try
        {
            step();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(Prefix + "could not " + what + ": " + ex.GetType().Name + ": " + ex.Message);
        }

        if (then is { } next)
            _phase = next;
    }
}
