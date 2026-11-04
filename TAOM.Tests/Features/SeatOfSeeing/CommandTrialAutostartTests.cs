using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Client.Features.SeatOfSeeing.Trial;
using TAOM.Core.Logging;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// The field trial's autostart (build-spec section 12): every way a run ends, driven through a fake game with its own clock, and the
/// two files it writes for the runner. <c>tools/tests/test_run_command_trial.py</c> parses the same done text with the runner's own reader.
/// </summary>
[TestClass]
public class CommandTrialAutostartTests
{
    [TestMethod]
    public void DoneText_HasTheContractsKeys_InOrder_OnePerLine_WithLfOnly()
    {
        var text = CommandTrialAutostart.DoneText("final", @"E:\Games\Logs\taom_debug_1.log", 31.44f, 187.26, "2026-11-04T10:00:00.0000000+01:00", "2026-11-04T10:03:07.0000000+01:00");

        Assert.AreEqual(
            "reason=final\nrun=1\nreport=none\nlog=E:\\Games\\Logs\\taom_debug_1.log\nlive_seconds=31.4\nwall_seconds=187.3\n"
            + "started=2026-11-04T10:00:00.0000000+01:00\nended=2026-11-04T10:03:07.0000000+01:00\n",
            text);
        Assert.IsFalse(text.Contains("\r"));
    }

    [TestMethod]
    public void DoneText_WithNoLogPath_SaysNone_SoTheRunnerFallsBackToTheNewestLog()
    {
        foreach (var path in new string?[] { null, "" })
            StringAssert.Contains(CommandTrialAutostart.DoneText("never_live", path, 0f, 301.0, "s", "e"), "\nlog=none\n");
    }

    [TestMethod]
    public void DoneText_CarriesTheReasonVerbatim_IncludingAJoinedOne()
    {
        StringAssert.StartsWith(CommandTrialAutostart.DoneText("final+exit_stalled", null, 1f, 1.0, "s", "e"), "reason=final+exit_stalled\n");
    }

    [TestMethod]
    public void StatusText_ChangesWithTheLiveSeconds_SoTheRunnersStallWatchSeesALivingRun()
    {
        var first = CommandTrialAutostart.StatusText("Live", "fly", 12.34f, 70.0);
        var later = CommandTrialAutostart.StatusText("Live", "fly", 17.5f, 75.0);

        Assert.AreEqual("phase=Live\nstep=fly\nlive=12.3\nwall=70.0\n", first);
        Assert.AreNotEqual(first, later);
        Assert.AreEqual(4, first.TrimEnd('\n').Split('\n').Count());
    }

    [TestMethod]
    public void UnfinishedReason_WhileTheRunLives_IsNull()
    {
        Assert.IsNull(CommandTrialAutostart.UnfinishedReason(12f, 420f, 90.0, sawMission: true, missionOpen: true));
        Assert.IsNull(CommandTrialAutostart.UnfinishedReason(0f, 420f, 20.0, sawMission: false, missionOpen: false), "still loading");
    }

    [TestMethod]
    public void UnfinishedReason_NamesEachWayAUnfinishedRunEnds()
    {
        Assert.AreEqual("live_limit", CommandTrialAutostart.UnfinishedReason(420f, 420f, 100.0, true, true));
        Assert.AreEqual("never_live", CommandTrialAutostart.UnfinishedReason(0f, 420f, 301.0, false, false));
        Assert.AreEqual("wall_limit", CommandTrialAutostart.UnfinishedReason(10f, 420f, 721.0, true, true), "a mission clock that stopped");
        Assert.AreEqual("mission_closed", CommandTrialAutostart.UnfinishedReason(10f, 420f, 100.0, true, false));
    }

    [TestMethod]
    public void UnfinishedReason_TheLiveLimitComesFirst_AndALongLimitMovesTheWallLimit()
    {
        Assert.AreEqual("live_limit", CommandTrialAutostart.UnfinishedReason(500f, 420f, 9999.0, true, false));
        Assert.IsNull(CommandTrialAutostart.UnfinishedReason(10f, 1000f, 721.0, true, true), "300 s of load plus the limit");
        Assert.AreEqual("wall_limit", CommandTrialAutostart.UnfinishedReason(10f, 1000f, 1301.0, true, true));
    }

    // ---- The two files' writes ----

    private static string TempFolder() => Path.Combine(AppContext.BaseDirectory, "trial-write-" + Guid.NewGuid().ToString("N"));

    [TestMethod]
    public void TryWrite_WhileAnotherProcessHoldsTheFileWithoutDeleteSharing_LogsOneWarningAndReturnsFalse_ThenWritesOnceItLetsGo()
    {
        // File.Replace fails against an open read handle.
        var folder = TempFolder();
        var logger = Substitute.For<IModLogger>();
        var path = Path.Combine(folder, CommandTrialAutostart.StatusFile);
        try
        {
            Assert.IsTrue(CommandTrialAutostart.TryWrite(folder, CommandTrialAutostart.StatusFile, "phase=Live\nstep=enter\n", logger));

            bool written;
            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                written = CommandTrialAutostart.TryWrite(folder, CommandTrialAutostart.StatusFile, "phase=Live\nstep=fly\n", logger);

            Assert.IsFalse(written);
            logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains(CommandTrialAutostart.StatusFile) && m.Contains("IOException")));
            Assert.AreEqual("phase=Live\nstep=enter\n", File.ReadAllText(path), "the old status stays whole");

            Assert.IsTrue(CommandTrialAutostart.TryWrite(folder, CommandTrialAutostart.StatusFile, "phase=Live\nstep=hero_stands\n", logger));
            Assert.AreEqual("phase=Live\nstep=hero_stands\n", File.ReadAllText(path));
            logger.Received(1).LogWarning(Arg.Any<string>());
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
    }

    [TestMethod]
    public void TryWrite_WhenTheFolderCannotBeMade_LogsOneWarningNamingTheFile_AndReturnsFalse()
    {
        var folder = TempFolder();
        var logger = Substitute.For<IModLogger>();
        try
        {
            Directory.CreateDirectory(folder);
            var blocker = Path.Combine(folder, "a-file");
            File.WriteAllText(blocker, "x");

            var written = CommandTrialAutostart.TryWrite(blocker, CommandTrialAutostart.DoneFile, "reason=final\n", logger);

            Assert.IsFalse(written);
            logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains(CommandTrialAutostart.DoneFile)));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [TestMethod]
    public void TryWrite_WithAFreeFolder_WritesTheTextWhole_AndLogsNothing()
    {
        var folder = TempFolder();
        var logger = Substitute.For<IModLogger>();
        try
        {
            Assert.IsTrue(CommandTrialAutostart.TryWrite(folder, CommandTrialAutostart.DoneFile, "reason=final\n", logger));

            Assert.AreEqual("reason=final\n", File.ReadAllText(Path.Combine(folder, CommandTrialAutostart.DoneFile)));
            Assert.IsFalse(File.Exists(Path.Combine(folder, CommandTrialAutostart.DoneFile + ".tmp")));
            Assert.AreEqual(0, logger.ReceivedCalls().Count());
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [TestMethod]
    public void DoneLine_SaysPlainlyWhenTheDoneFileWasNotWritten_AndStillNamesTheQuit()
    {
        Assert.AreEqual("[SeatTrial] done file written: reason=final quit=1", CommandTrialAutostart.DoneLine(true, "final", true));

        var lost = CommandTrialAutostart.DoneLine(false, "final", true);
        StringAssert.Contains(lost, "NOT written");
        StringAssert.Contains(lost, "reason=final");
        StringAssert.Contains(lost, "quit=1");
        Assert.IsFalse(lost.Contains("done file written"));
    }

    // ---- Every way a run ends, through a fake game ----

    private string _folder = "";
    private ListLogger _log = null!;
    private FakeGame _game = null!;

    [TestInitialize]
    public void Setup()
    {
        CommandTrialRun.Reset();
        _folder = TempFolder();
        _log = new ListLogger();
        _game = new FakeGame();
    }

    [TestCleanup]
    public void Cleanup()
    {
        CommandTrialRun.Reset();
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, true);
    }

    private CommandTrialAutostart Autostart(string? steps = null, bool quit = true, int liveLimit = 420, string? outFolder = null)
    {
        var lines = new System.Collections.Generic.List<string> { "out=" + (outFolder ?? _folder), "quit=" + (quit ? "1" : "0"), "live_limit_seconds=" + liveLimit };
        if (steps != null)
            lines.Add("steps=" + steps);
        var options = CommandTrialOptions.Parse("arm.txt", _ => true, _ => lines.ToArray(), _log)!;
        CommandTrialRun.Arm(options);
        return new CommandTrialAutostart(options, _log, _game);
    }

    // One application tick of dt seconds: the fake's clock moves by dt first.
    private void Tick(CommandTrialAutostart sut, int ticks = 1, float dt = 1f)
    {
        for (var i = 0; i < ticks; i++)
        {
            _game.WallSeconds += dt;
            sut.Tick(dt);
        }
    }

    private string? DoneReason()
    {
        var path = Path.Combine(_folder, CommandTrialAutostart.DoneFile);
        return File.Exists(path) ? File.ReadAllLines(path).First(l => l.StartsWith("reason=", StringComparison.Ordinal)).Substring("reason=".Length) : null;
    }

    private string StatusPhase() => File.ReadAllLines(Path.Combine(_folder, CommandTrialAutostart.StatusFile)).First().Substring("phase=".Length);

    private CommandTrialAutostart Live(string? steps = null, bool quit = true, int liveLimit = 420)
    {
        var sut = Autostart(steps, quit, liveLimit);
        Tick(sut, 4);
        Assert.AreEqual(1, _game.Starts, "the game started after the settle");
        return sut;
    }

    private void EndTheRun(CommandTrialAutostart sut, string expected)
    {
        Tick(sut);
        Assert.AreEqual(1, _game.Returns, "the run goes back to the main menu");
        _game.AtMainMenu = true;
        Tick(sut);
        Assert.AreEqual(expected, DoneReason());
    }

    [TestMethod]
    public void WaitForMenu_StartsTheGameOnlyAfterFourSettledSeconds_AndLeavingTheMenuStartsTheCountAgain()
    {
        var sut = Autostart();
        _game.AtMainMenu = false;
        Tick(sut, 6);
        Assert.AreEqual(0, _game.Starts, "no menu, no start");

        _game.AtMainMenu = true;
        Tick(sut, 3);
        _game.AtMainMenu = false;
        Tick(sut);
        _game.AtMainMenu = true;
        Tick(sut, 3);
        Assert.AreEqual(0, _game.Starts, "three settled seconds twice is not four");

        Tick(sut);

        Assert.AreEqual(1, _game.Starts);
        Assert.IsTrue(_game.InquiriesHidden >= 7, "an inquiry is hidden on every tick at the menu");
        Assert.IsNull(DoneReason());
    }

    [TestMethod]
    public void WaitForMenu_AStepListTheGameRefuses_EndsAsStartRefused_WithoutStartingAndQuitsAfterTheDoneFile()
    {
        var sut = Autostart(steps: "enter,flye");

        Tick(sut, 4);

        Assert.AreEqual(0, _game.Starts);
        Assert.AreEqual("start_refused", DoneReason());
        Assert.AreEqual(1, _game.Quits);
        Assert.IsTrue(_log.Errors.Any(e => e.Contains("refused to start") && e.Contains("flye")));
    }

    [TestMethod]
    public void Live_WhenTheScriptReportsFinal_GoesBackToTheMenu_AndWritesTheDoneFileOnceTheMenuIsThere()
    {
        var sut = Live();
        _game.MissionOpen = true;
        Tick(sut, 2);
        Assert.AreEqual("Live", StatusPhase());
        CommandTrialRun.Finish("final");

        Tick(sut);
        Assert.AreEqual(1, _game.Returns);
        Tick(sut, 6);

        Assert.AreEqual("Ending", StatusPhase());
        Assert.IsNull(DoneReason(), "the done file waits for the menu");
        _game.AtMainMenu = true;
        Tick(sut);
        Assert.AreEqual("final", DoneReason());
        StringAssert.Contains(File.ReadAllText(Path.Combine(_folder, CommandTrialAutostart.DoneFile)), "live_seconds=3.0");
        Assert.AreEqual(1, _game.Quits);
        Tick(sut, 3);
        Assert.AreEqual(1, _game.Quits, "a finished run does nothing more");
    }

    [TestMethod]
    public void Ending_WhenTheMenuNeverReturns_EndsAsExitStalled_AfterSixtySeconds()
    {
        var sut = Live();
        _game.MissionOpen = true;
        CommandTrialRun.Finish("final");
        Tick(sut);

        Tick(sut, 59);
        Assert.IsNull(DoneReason(), "inside the allowance");
        Tick(sut, 2);

        Assert.AreEqual("final+exit_stalled", DoneReason());
        Assert.AreEqual(1, _game.Quits);
    }

    [TestMethod]
    public void Finish_WhenTheDoneFileCannotBeWritten_StillQuits_AndSaysTheFileWasLost()
    {
        var blocker = Path.Combine(AppContext.BaseDirectory, "trial-block-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(blocker, "x");
        try
        {
            var sut = Autostart(steps: "flye", outFolder: blocker);

            Tick(sut, 4);

            Assert.AreEqual(1, _game.Quits, "the quit follows a lost done file");
            Assert.IsTrue(_log.Lines.Any(l => l.Contains("NOT written") && l.Contains("reason=start_refused")));
        }
        finally
        {
            File.Delete(blocker);
        }
    }

    [TestMethod]
    public void Finish_WithQuitOff_LeavesTheGameRunning()
    {
        var sut = Autostart(steps: "flye", quit: false);

        Tick(sut, 4);

        Assert.AreEqual("start_refused", DoneReason());
        Assert.AreEqual(0, _game.Quits);
    }

    [TestMethod]
    public void Tick_WhenTheHostThrows_EndsAsAutostartFailed_LogsOnce_AndTicksNoMore()
    {
        var sut = Live();
        _game.MissionReadFails = true;

        Tick(sut);
        Tick(sut, 3);

        Assert.AreEqual("autostart_failed", DoneReason());
        Assert.AreEqual(1, _log.Errors.Count(e => e.Contains("the autostart failed")));
        Assert.AreEqual(1, _game.Quits);
    }

    [TestMethod]
    public void Tick_WhenTheHostThrows_LatchesTheOutcome_SoALaterMissionGetsNoScript()
    {
        var sut = Live();
        _game.MissionReadFails = true;

        Tick(sut);

        Assert.AreEqual("autostart_failed", CommandTrialRun.Outcome);
        Assert.IsNull(CommandTrialRun.Script(() => throw new InvalidOperationException("a later mission asked for a host"), _log),
            "with quit=0 a later mission would otherwise get a script and run the steps again");
    }

    [TestMethod]
    public void Live_WithNoMissionBeyondTheLoadAllowance_EndsAsNeverLive()
    {
        var sut = Live();

        Tick(sut, 290);
        Assert.AreEqual(0, _game.Returns, "inside the allowance");
        Tick(sut, 20);

        Assert.AreEqual("never_live", CommandTrialRun.Outcome);
        EndTheRun(sut, "never_live");
    }

    [TestMethod]
    public void Live_ApplicationTicksThatStoppedFeedingTheClock_EndAsWallLimit_PastTheLoadAllowancePlusTheLiveLimit()
    {
        var sut = Live(liveLimit: 60);
        _game.MissionOpen = true;

        for (var i = 0; i < 340; i++)
        {
            _game.WallSeconds += 1.0;
            sut.Tick(0f);
        }

        Assert.AreEqual(0, _game.Returns, "inside 300 s of load plus the limit");
        for (var i = 0; i < 40; i++)
        {
            _game.WallSeconds += 1.0;
            sut.Tick(0f);
        }

        EndTheRun(sut, "wall_limit");
    }

    [TestMethod]
    public void Live_AMissionThatClosesUnderTheScript_EndsAsMissionClosed()
    {
        var sut = Live();
        _game.MissionOpen = true;
        Tick(sut);
        _game.MissionOpen = false;

        Tick(sut);

        EndTheRun(sut, "mission_closed");
    }

    [TestMethod]
    public void Live_PastTheLiveLimit_EndsAsLiveLimit()
    {
        var sut = Live(liveLimit: 60);
        _game.MissionOpen = true;
        Tick(sut, 59);
        Assert.AreEqual(0, _game.Returns);

        Tick(sut);

        EndTheRun(sut, "live_limit");
    }

    [TestMethod]
    public void Live_NoMissionOpen_DoesNotCountLiveSeconds()
    {
        var sut = Live(liveLimit: 60);

        Tick(sut, 100);

        Assert.AreEqual(0, _game.Returns, "a load is not live time: only never_live bounds it");
    }

    [TestMethod]
    public void Live_AnOutcomeTheScriptAlreadySet_IsNotReplacedByALimit()
    {
        var sut = Live(liveLimit: 60);
        _game.MissionOpen = true;
        Tick(sut, 59);
        CommandTrialRun.Finish("final");

        Tick(sut);

        EndTheRun(sut, "final");
    }

    [TestMethod]
    public void TheStatusFile_IsRewrittenEveryFiveSeconds_WithThePhaseAndTheStep()
    {
        var sut = Live();
        CommandTrialRun.OnStep("fly");
        _game.MissionOpen = true;
        Tick(sut, 5);

        var first = File.ReadAllText(Path.Combine(_folder, CommandTrialAutostart.StatusFile));
        Tick(sut, 5);
        var later = File.ReadAllText(Path.Combine(_folder, CommandTrialAutostart.StatusFile));

        StringAssert.Contains(later, "phase=Live\nstep=fly\nlive=");
        Assert.AreNotEqual(first.Split('\n')[2], later.Split('\n')[2], "the live line counts real seconds, so it moves with the run");
        Assert.AreNotEqual(first, later, "the wall line changes, so the runner's stall watch sees a living run");
    }

    [TestMethod]
    public void Armed_WithTheVariableNamingAnArmFile_ArmsTheRun_AndWithItUnset_ReturnsNull()
    {
        var kept = Environment.GetEnvironmentVariable(CommandTrialOptions.EnvVar);
        var armFile = Path.Combine(AppContext.BaseDirectory, "trial-arm-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(armFile, "out=" + _folder + "\nquit=1\n");
        try
        {
            Environment.SetEnvironmentVariable(CommandTrialOptions.EnvVar, armFile);
            var armed = CommandTrialAutostart.Armed(_log);

            Assert.IsNotNull(armed);
            Assert.IsTrue(CommandTrialRun.IsArmed, "an armed process answers no to the escape menu on focus loss");
            Assert.AreEqual(_folder, CommandTrialRun.Options!.OutFolder);
            Assert.IsTrue(_log.Lines.Any(l => l.StartsWith("[SeatTrial] armed: ", StringComparison.Ordinal)));

            CommandTrialRun.Reset();
            Environment.SetEnvironmentVariable(CommandTrialOptions.EnvVar, null);
            Assert.IsNull(CommandTrialAutostart.Armed(_log));
            Assert.IsFalse(CommandTrialRun.IsArmed);
        }
        finally
        {
            Environment.SetEnvironmentVariable(CommandTrialOptions.EnvVar, kept);
            File.Delete(armFile);
            CommandTrialRun.Reset();
        }
    }

    private sealed class FakeGame : ICommandTrialGameHost
    {
        private bool _missionOpen;

        public bool AtMainMenu { get; set; } = true;

        public int InquiriesHidden { get; private set; }

        public int Starts { get; private set; }

        public int Returns { get; private set; }

        public int Quits { get; private set; }

        public bool MissionReadFails { get; set; }

        public bool MissionOpen
        {
            get => MissionReadFails ? throw new InvalidOperationException("the mission read failed") : _missionOpen;
            set => _missionOpen = value;
        }

        public double WallSeconds { get; set; }

        public void HideAnyInquiry() => InquiriesHidden++;

        public void StartGame()
        {
            Starts++;
            AtMainMenu = false;
        }

        public void ReturnToMainMenu() => Returns++;

        public void Quit() => Quits++;
    }

    [TestMethod]
    public void TheRun_IsUnarmedUntilArmed_AndTheFirstOutcomeWins()
    {
        CommandTrialRun.Reset();
        try
        {
            Assert.IsFalse(CommandTrialRun.IsArmed);
            Assert.IsNull(CommandTrialRun.Outcome);

            CommandTrialRun.Finish("start_refused");
            CommandTrialRun.Finish("final");

            Assert.AreEqual("start_refused", CommandTrialRun.Outcome);
        }
        finally
        {
            CommandTrialRun.Reset();
        }
    }
}
