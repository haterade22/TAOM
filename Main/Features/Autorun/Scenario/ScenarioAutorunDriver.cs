using System;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.StoryMissions.Domain;

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
    internal const string CommandsFile = "commands.txt";

    // The story phases stay before Exiting: Tick and Finish test _phase < Phase.Exiting.
    private enum Phase { Off, WaitMenu, Creation, WaitMap, MissionMenu, MissionOpen, InMission, MissionReturn, Running, Exiting, Done }

    private readonly ScenarioPlan _plan;
    private readonly ICampaignAutorunHost _host;
    private readonly IModLogger _logger;
    private readonly IOutcomeLog _outcomes;
    private readonly string _startedAt = DateTime.Now.ToString("o");

    private Phase _phase;
    private double _wall, _phaseStart, _startWall, _held, _dayChanged, _lastStatus, _dt;

    // The tick after the console commands: its dt measures the frame that ran them, which no phase timer may count (#1434).
    private bool _skipNextDt;
    private int _dayStart = ScenarioRules.UnknownDay, _lastDay = ScenarioRules.UnknownDay;
    private int _inquiries, _notices, _eventWindows, _resets;
    private string _prepared = "";
    private bool _noticeSeen, _missionSeen, _backClicked;
    private double _missionStart, _missionEnd = -1.0, _freeMapHeld;
    private StoryOutcome? _verdict;

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

        _wall += _dt = _skipNextDt ? 0f : dt;
        _skipNextDt = false;
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
            case Phase.MissionMenu: MissionMenu(); break;
            case Phase.MissionOpen: MissionOpen(); break;
            case Phase.InMission: InMission(); break;
            case Phase.MissionReturn: MissionReturn(); break;
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

        RunCommands();
        _dayChanged = _wall;
        Enter(_plan.Kind == ScenarioPlan.KindStoryMission ? Phase.MissionMenu : Phase.Running);
    }

    // The story kind opens the dev story menu from the free map, picks "Open the mission" once that menu has settled, waits out the
    // mission and ends when the map is back. It never fast forwards: the mission runs at 1x and no Running phase follows.
    private void MissionMenu()
    {
        MapChores();
        if (!ScenarioRules.Waiting(_wall - _phaseStart, ScenarioRules.StoryStepWaitSeconds))
        {
            StoryRefused("the map never showed with no menu open in " + ScenarioRules.StoryStepWaitSeconds + " s");
            return;
        }

        if (!Held(_host.Screen == AutorunScreen.Map) || !ScenarioRules.MapSettled(_held))
            return;

        var opened = _host.OpenStoryMenu(_plan.MissionId, out var message);
        _logger.LogInfo(Prefix + "story menu: " + message);
        if (opened)
            Enter(Phase.MissionOpen);
        else
            StoryRefused("the story menu was refused: " + message);
    }

    private void MissionOpen()
    {
        MapChores();
        if (!ScenarioRules.Waiting(_wall - _phaseStart, ScenarioRules.StoryStepWaitSeconds))
        {
            StoryRefused("the story menu never settled in " + ScenarioRules.StoryStepWaitSeconds + " s");
            return;
        }

        if (!Held(_host.Screen == AutorunScreen.MapMenu && _host.StoryMenuShown) || !ScenarioRules.MapSettled(_held) || _host.EventWindowOpenOrWaiting)
            return;

        if (_host.ChooseOpenMission())
            Enter(Phase.InMission);
        else
            StoryRefused("the Open the mission option was not offered");
    }

    private void InMission()
    {
        HideInquiry();
        CloseEventWindow();
        if (!_missionSeen)
        {
            if (_host.Screen == AutorunScreen.Mission)
            {
                _missionSeen = true;
                _missionStart = _wall;
            }
            else if (!ScenarioRules.Waiting(_wall - _phaseStart, ScenarioRules.StoryStepWaitSeconds))
                Finish("mission_not_opened");
            return;
        }

        if (ScenarioRules.OnMap(_host.Screen))
        {
            _missionEnd = _wall;
            _verdict = _host.StoryVerdict;
            _logger.LogInfo(Prefix + "mission ended verdict=" + (_verdict?.ToString() ?? "-") + " seconds=" + ScenarioAutorunDone.F1(_missionEnd - _missionStart));
            var reason = ScenarioRules.MissionReason(_verdict);
            if (reason == null)
                Enter(Phase.MissionReturn);
            else
                Finish(reason);
        }
        else if (!ScenarioRules.WithinWallLimit(_wall - _missionStart, _plan.MissionLimitSeconds))
        {
            _logger.LogWarning(Prefix + "mission_limit: the mission did not end in " + ScenarioAutorunDone.F1(_plan.MissionLimitSeconds) + " s screen=" + _host.Screen);
            Finish("mission_limit");
        }
    }

    private void MissionReturn()
    {
        MapChores();
        if (!ScenarioRules.Waiting(_wall - _phaseStart, ScenarioRules.StoryStepWaitSeconds))
        {
            _logger.LogWarning(Prefix + "return_failed: the map did not settle in " + ScenarioRules.StoryStepWaitSeconds + " s screen=" + _host.Screen
                + " open_menu=" + (_host.OpenMenuId ?? "none"));
            Finish("return_failed");
            return;
        }

        var screen = _host.Screen;
        _freeMapHeld = screen == AutorunScreen.Map ? _freeMapHeld + _dt : 0.0;
        if (Held(!_backClicked && screen == AutorunScreen.MapMenu && _host.StoryMenuShown))
        {
            if (!ScenarioRules.MapSettled(_held))
                return;

            if (_host.ChooseBackToMap())
            {
                _backClicked = true;
                _logger.LogInfo(Prefix + "chose Back to the map");
            }
            else
            {
                _logger.LogWarning(Prefix + "return_failed: the Back to the map option was not offered");
                Finish("return_failed");
            }

            return;
        }

        if (ScenarioRules.MapSettled(_freeMapHeld))
        {
            _logger.LogInfo(Prefix + (_backClicked ? "back on the free map after Back to the map" : "back on the free map with no menu open"));
            Finish(ScenarioAutorunDone.ReasonOk);
        }
    }

    private void StoryRefused(string why)
    {
        _logger.LogWarning(Prefix + "mission_refused: " + why + " open_menu=" + (_host.OpenMenuId ?? "none"));
        Finish("mission_refused");
    }

    private void MapChores()
    {
        HideInquiry();
        HideSceneNotification();
        CloseEventWindow();
    }

    private void CloseEventWindow()
    {
        if (_host.CloseEventWindow() && ScenarioRules.TitleLogged(++_eventWindows))
            _logger.LogInfo(Prefix + "event window closed n=" + _eventWindows + " phase=" + _phase);
    }

    private void Running()
    {
        MapChores();
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
        var facts = new ScenarioRunFacts(reason, _dayStart, _lastDay, _inquiries, _notices, _eventWindows, _resets, _prepared, _wall, _startedAt,
            DateTime.Now.ToString("o")) with
        {
            Verdict = _verdict?.ToString() ?? "-",
            MissionSeconds = _missionSeen ? (_missionEnd >= 0.0 ? _missionEnd : _wall) - _missionStart : 0.0,
        };
        Guarded("restore the focus setting", _host.RestoreFocusSetting);
        // The text is built inside the guard, so a throw while building it still lets the outcome and the campaign end run.
        Guarded("write " + ScenarioAutorunDone.DoneFile, () => _host.WriteFile(ScenarioAutorunDone.DoneFile, ScenarioAutorunDone.Text(_plan, facts)));
        Guarded("log the result", () => _logger.LogInfo(Prefix + "done reason=" + reason + " scenario=" + _plan.Scenario
            + (_plan.Kind == ScenarioPlan.KindStoryMission
                ? " mission=" + _plan.MissionId + " verdict=" + facts.Verdict
                : " days_run=" + ScenarioRules.DaysRun(_dayStart, _lastDay) + " days_wanted=" + _plan.Days)
            + " quit=" + (_plan.Quit ? 1 : 0)));
        Guarded("report the outcome", () => ScenarioOutcomes.Report(_outcomes, _plan, facts));
        Guarded("end the campaign", _host.EndCampaign);
    }

    // A notice is closed only on its second sighting in a row: the engine marks it active one layer tick before its scene exists, and
    // closing it in that tick throws (GauntletSceneNotification.CloseNotification).
    private void HideSceneNotification()
    {
        var active = _host.SceneNotificationActive;
        if (active && _noticeSeen)
        {
            _host.HideSceneNotification();
            _notices++;
            _noticeSeen = false;
        }
        else
            _noticeSeen = active;
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

    // The armed console commands, once, on the settled map (#1434). A command that throws is reported in its place and the
    // run goes on; the output goes to the log and to commands.txt in the run folder.
    private void RunCommands()
    {
        if (_plan.Commands.Count == 0)
            return;

        var file = new System.Text.StringBuilder();
        foreach (var command in _plan.Commands)
        {
            string output;
            try
            {
                output = _host.RunConsoleCommand(command) ?? "";
            }
            catch (Exception ex)
            {
                output = "threw " + ex.GetType().Name + ": " + ex.Message;
            }

            _logger.LogInfo(Prefix + "command " + command + "\n" + output);
            file.Append("> ").Append(command).Append('\n').Append(output).Append("\n\n");
        }

        Write(CommandsFile, file.ToString());
        _skipNextDt = true;
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
