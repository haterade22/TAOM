using System;
using System.Globalization;
using System.IO;
using TAOM.Core.Logging;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

/// <summary>The field trial's driver on the application tick (build-spec section 12), in the shape of <c>FordsAutorunDriver</c>: it waits for the main menu, starts the game, watches the run, takes the game back to the menu and writes the done file, all over <see cref="ICommandTrialGameHost"/>.</summary>
internal sealed class CommandTrialAutostart
{
    internal const string DoneFile = "command_trial_done.txt";
    internal const string StatusFile = "command_trial_status.txt";

    private const float SettleSeconds = 4f;
    private const double StatusSeconds = 5.0;
    private const double NeverLiveWallSeconds = 300.0;
    private const double ExitStallWallSeconds = 60.0;

    private enum Phase { WaitMenu, Live, Ending, Done }

    private readonly CommandTrialOptions _options;
    private readonly IModLogger _logger;
    private readonly ICommandTrialGameHost _host;
    private readonly string _startedAt = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);

    private Phase _phase = Phase.WaitMenu;
    private float _settled;
    private double _phaseStart;
    private double _nextStatus;
    private float _liveSeconds;
    private bool _sawMission;
    private string _reason = "";

    internal CommandTrialAutostart(CommandTrialOptions options, IModLogger logger, ICommandTrialGameHost host)
    {
        _options = options;
        _logger = logger;
        _host = host;
    }

    /// <summary>The autostart for this process, or null when no arm file is set, which is every player session. Reads the environment once.</summary>
    public static CommandTrialAutostart? Armed(IModLogger logger)
    {
        var options = CommandTrialOptions.FromEnvironment(logger);
        if (options == null)
            return null;

        CommandTrialRun.Arm(options);
        logger.LogInfo("[SeatTrial] armed: " + options);
        return new CommandTrialAutostart(options, logger, new CommandTrialGameHost(options, logger));
    }

    public void Tick(float dt)
    {
        if (_phase == Phase.Done)
            return;
        try
        {
            WriteStatusWhenDue();
            if (_phase == Phase.WaitMenu)
                WaitForMenu(dt);
            else if (_phase == Phase.Live)
                WhileLive();
            else
                WhileEnding();
        }
        catch (Exception ex)
        {
            _logger.LogError($"[SeatTrial] the autostart failed, so the run ends: {ex}");
            Finish("autostart_failed");
        }
    }

    /// <summary>The text of the done file: <c>reason</c> and the run's evidence, one key=value per line.</summary>
    internal static string DoneText(string reason, string? logPath, float liveSeconds, double wallSeconds, string started, string ended) =>
        "reason=" + reason + "\nrun=1\nreport=none\nlog=" + (string.IsNullOrEmpty(logPath) ? "none" : logPath) + "\nlive_seconds=" + Seconds(liveSeconds)
        + "\nwall_seconds=" + Seconds((float)wallSeconds) + "\nstarted=" + started + "\nended=" + ended + "\n";

    /// <summary>The text of the status file, which the runner's stall watch reads: it must change while the run lives.</summary>
    internal static string StatusText(string phase, string step, float missionSeconds, double wallSeconds) =>
        "phase=" + phase + "\nstep=" + step + "\nmt=" + Seconds(missionSeconds) + "\nwall=" + Seconds((float)wallSeconds) + "\n";

    private static string Seconds(float value) => value.ToString("F1", CultureInfo.InvariantCulture);

    private void WaitForMenu(float dt)
    {
        if (!_host.AtMainMenu)
        {
            _settled = 0f;
            return;
        }

        _host.HideAnyInquiry();
        _settled += dt;
        if (_settled < SettleSeconds)
            return;

        if (!SeatTrialSteps.TryChoose(_options.Steps, out _, out var error))
        {
            _logger.LogError($"[SeatTrial] refused to start: {error}");
            Finish("start_refused");
            return;
        }

        Enter(Phase.Live);
        _host.StartGame();
    }

    private void WhileLive()
    {
        var missionSeconds = _host.MissionSeconds;
        if (missionSeconds != null)
        {
            _sawMission = true;
            _liveSeconds = Math.Max(_liveSeconds, missionSeconds.Value);
        }

        var unfinished = CommandTrialRun.Outcome == null
            ? UnfinishedReason(_liveSeconds, _options.LiveLimitSeconds, _host.WallSeconds - _phaseStart, _sawMission, missionSeconds != null)
            : null;
        if (unfinished != null)
            CommandTrialRun.Finish(unfinished);

        var outcome = CommandTrialRun.Outcome;
        if (outcome == null)
            return;

        _reason = outcome;
        _logger.LogInfo($"[SeatTrial] ending the run: {outcome} at mt={Seconds(_liveSeconds)}");
        Enter(Phase.Ending);
        _host.ReturnToMainMenu();
    }

    /// <summary>
    /// Why the run must end although the script has not reported, or null while it may go on: the live limit (mission seconds), a mission
    /// that never opened, a wall limit past the load allowance and the live limit (a mission clock that stopped would otherwise hold the run
    /// until the runner's own limit), and a mission that closed under the script.
    /// </summary>
    internal static string? UnfinishedReason(float liveSeconds, float liveLimit, double wallSeconds, bool sawMission, bool missionOpen)
    {
        if (liveSeconds >= liveLimit)
            return "live_limit";
        if (!sawMission && wallSeconds > NeverLiveWallSeconds)
            return "never_live";
        if (wallSeconds > NeverLiveWallSeconds + liveLimit)
            return "wall_limit";
        return sawMission && !missionOpen ? "mission_closed" : null;
    }

    private void WhileEnding()
    {
        if (_host.AtMainMenu)
            Finish(_reason);
        else if (_host.WallSeconds - _phaseStart > ExitStallWallSeconds)
            Finish(_reason + "+exit_stalled");
    }

    private void Enter(Phase next)
    {
        _phase = next;
        _phaseStart = _host.WallSeconds;
    }

    private void Finish(string reason)
    {
        Enter(Phase.Done);
        var written = Write(DoneFile, DoneText(reason, _logger.LogFilePath is { Length: > 0 } path ? Path.GetFullPath(path) : null, _liveSeconds,
            _host.WallSeconds, _startedAt, DateTime.Now.ToString("o", CultureInfo.InvariantCulture)));
        var line = DoneLine(written, reason, _options.Quit);
        if (written)
            _logger.LogInfo(line);
        else
            _logger.LogWarning(line);

        if (_options.Quit)
            _host.Quit();
    }

    /// <summary>The log line that closes the run: the done file is the runner's only sign that the run ended, so a lost one is said plainly.</summary>
    internal static string DoneLine(bool written, string reason, bool quit) =>
        (written ? "[SeatTrial] done file written: reason=" : "[SeatTrial] the done file was NOT written, so the runner sees no end: reason=")
        + reason + " quit=" + (quit ? "1" : "0");

    private void WriteStatusWhenDue()
    {
        var wall = _host.WallSeconds;
        if (wall < _nextStatus)
            return;

        _nextStatus = wall + StatusSeconds;
        Write(StatusFile, StatusText(_phase.ToString(), CommandTrialRun.Step, _liveSeconds, wall));
    }

    private bool Write(string name, string text) => TryWrite(_options.OutFolder, name, text, _logger);

    internal static bool TryWrite(string folder, string name, string text, IModLogger logger)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, name);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, text);
            if (File.Exists(path))
                File.Replace(temporary, path, null);
            else
                File.Move(temporary, path);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning($"[SeatTrial] could not write {name}: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }
}
