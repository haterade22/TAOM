using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Autorun;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Autorun;

[TestClass]
public class FordsAutorunDriverTests
{
    private const string DoneFile = "fords_autorun_done.txt";
    private const string StatusFile = "fords_autorun_status.txt";

    private sealed class FakeHost : IFordsAutorunHost
    {
        public FordsRunReport Report = null!;
        public bool AtMenu = true;
        public bool InquiryOpen;
        public bool StartResult = true;
        public bool ReturnLeavesTheGame = true;
        public bool WriteThrows;
        public int Hidden;
        public int Starts;
        public int Returns;
        public int Quits;
        public FordsMatchSetup? Setup;
        public readonly Dictionary<string, string> Files = new();

        public bool AtMainMenu => AtMenu;

        public bool HideAnyInquiry()
        {
            if (!InquiryOpen)
                return false;

            Hidden++;
            return true;
        }

        public bool Start(FordsMatchSetup setup)
        {
            Starts++;
            Setup = setup;
            Report.BeginRun("autorun", setup);
            if (!StartResult)
            {
                Report.Failure("C", "the game manager factory returned null");
                return false;
            }

            AtMenu = false;
            return true;
        }

        public void ReturnToMainMenu()
        {
            Returns++;
            if (!ReturnLeavesTheGame)
                return;

            Report.MissionClosed();
            Report.MainMenuReturned();
            AtMenu = true;
        }

        public void WriteFile(string name, string text)
        {
            if (WriteThrows)
                throw new IOException("disk full");

            Files[name] = text;
        }

        public void Quit() => Quits++;
    }

    private string _dir = "";
    private double _wall;
    private IModLogger _log = null!;
    private List<string> _lines = null!;
    private FordsRunReport _report = null!;
    private FakeHost _host = null!;
    private FordsConfigCatalog _catalog = null!;

    [TestInitialize]
    public void Init()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fords_autorun_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _wall = 0.0;
        _lines = new List<string>();
        _log = Substitute.For<IModLogger>();
        _log.LogFilePath.Returns(Path.Combine(_dir, "taom_debug_2026-10-31_12-30-45.log"));
        _log.When(l => l.LogInfo(Arg.Any<string>())).Do(c => _lines.Add(c.Arg<string>()));
        _log.When(l => l.LogWarning(Arg.Any<string>())).Do(c => _lines.Add(c.Arg<string>()));
        _report = new FordsRunReport(_log, () => _wall);
        _host = new FakeHost { Report = _report };
        _catalog = FordsConfigCatalog.FromTexts(null, FordsConfigFixtures.Factions, FordsConfigFixtures.Maps, Substitute.For<IModLogger>());
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    private FordsAutorunDriver Driver(string extra = "", bool armed = true)
    {
        var text = "west=gondor\neast=mordor\nai=hard\nlive_seconds=100\nlive_wait_seconds=60\nwall_limit_seconds=500\nout=E:\\out\nquit=1\n" + extra;
        var plan = FordsAutorunPlan.Resolve(_ => armed ? "arm.txt" : null, _ => true, _ => text);
        return new FordsAutorunDriver(plan, _report, _catalog, _host, _log);
    }

    private void Tick(FordsAutorunDriver driver, double seconds, float step = 0.5f)
    {
        for (var elapsed = 0.0; elapsed < seconds - 1e-9; elapsed += step)
        {
            _wall += step;
            driver.Tick(step);
        }
    }

    private void StartAndGoLive(FordsAutorunDriver driver)
    {
        Tick(driver, 5.0);
        Assert.AreEqual(1, _host.Starts, "the start is part of the setup");
        _report.Stage("live");
        Tick(driver, 1.0);
    }

    private void Mission(double seconds)
    {
        for (var i = 0; i < (int)seconds; i++)
            _report.MissionTick(1f, 16.0);
    }

    private Dictionary<string, string> Done() =>
        _host.Files[DoneFile].Split('\n').Where(l => l.Contains('=')).ToDictionary(l => l.Substring(0, l.IndexOf('=')).Trim(), l => l.Substring(l.IndexOf('=') + 1).Trim('\r'));

    [TestMethod]
    public void Tick_Unarmed_NeverTouchesTheHost()
    {
        var driver = Driver(armed: false);

        Tick(driver, 30.0);

        Assert.AreEqual(0, _host.Starts);
        Assert.AreEqual(0, _host.Files.Count);
        Assert.AreEqual(0, _host.Quits);
    }

    [TestMethod]
    public void Tick_AtTheMenuForFourSecondsOrLess_DoesNotStart()
    {
        var driver = Driver();

        Tick(driver, 4.0);

        Assert.AreEqual(0, _host.Starts);
    }

    [TestMethod]
    public void Tick_AtTheMenuPastFourSeconds_StartsExactlyOnceWithThePlansChoices()
    {
        var driver = Driver();

        Tick(driver, 12.0);

        Assert.AreEqual(1, _host.Starts);
        Assert.AreEqual("gondor", _host.Setup!.WestFaction);
        Assert.AreEqual("mordor", _host.Setup.EastFaction);
        Assert.AreEqual("hard", _host.Setup.AiProfile);
        Assert.AreEqual("taom_rohan_battle_001_forceatmo", _host.Setup.SceneName);
        Assert.IsFalse(_host.Setup.WestAi);
    }

    [TestMethod]
    public void Tick_PlanWithWestAi_StartsASetupWithAnAiOnTheWestSide()
    {
        var driver = Driver("west_ai=1\n");

        Tick(driver, 12.0);

        Assert.IsTrue(_host.Setup!.WestAi);
    }

    [TestMethod]
    public void Tick_LeavingTheMenu_RestartsTheFourSecondSettleClock()
    {
        var driver = Driver();
        Tick(driver, 3.0);
        _host.AtMenu = false;
        Tick(driver, 10.0);
        _host.AtMenu = true;

        Tick(driver, 3.5);

        Assert.AreEqual(0, _host.Starts);
    }

    [TestMethod]
    public void Tick_InquiryOpenAtTheMenu_IsHiddenUntilTheMatchStarts()
    {
        var driver = Driver();
        _host.InquiryOpen = true;

        Tick(driver, 5.0);
        var hiddenBeforeStart = _host.Hidden;
        Tick(driver, 20.0);

        Assert.IsTrue(hiddenBeforeStart > 0);
        Assert.AreEqual(hiddenBeforeStart, _host.Hidden, "once the match has started the start-failed notice keeps its acknowledgement evidence");
    }

    [TestMethod]
    public void Tick_UnknownFactionInThePlan_WritesStartRefusedAndQuitsOnce()
    {
        var driver = Driver("west=narnia\n");

        Tick(driver, 30.0);

        Assert.AreEqual(0, _host.Starts);
        Assert.AreEqual("start_refused", Done()["reason"]);
        Assert.AreEqual(1, _host.Quits);
    }

    [TestMethod]
    public void Tick_HostRefusesTheStart_WritesStartRefusedAndQuitsOnce()
    {
        _host.StartResult = false;
        var driver = Driver();

        Tick(driver, 30.0);

        Assert.AreEqual(1, _host.Starts);
        Assert.AreEqual("start_refused", Done()["reason"]);
        Assert.AreEqual("1", Done()["run"]);
        Assert.AreEqual(1, _host.Quits);
    }

    [TestMethod]
    public void Tick_BelowLiveSeconds_DoesNotEndTheGame()
    {
        var driver = Driver();
        StartAndGoLive(driver);

        Mission(99.0);
        Tick(driver, 10.0);

        Assert.AreEqual(0, _host.Returns);
        Assert.IsNull(_report.Run!.End.EndGameVia);
    }

    [TestMethod]
    public void Tick_LiveSecondsReached_EndsTheGameOnceThroughTheReportsExitApi()
    {
        var driver = Driver();
        StartAndGoLive(driver);

        Mission(100.0);
        Tick(driver, 1.0);
        Tick(driver, 10.0);

        Assert.AreEqual(1, _host.Returns);
        Assert.AreEqual("autorun", _report.Run!.End.EndGameVia);
        Assert.AreEqual("AutorunLimit", _report.Run.End.ExitReason);
    }

    [TestMethod]
    public void Tick_WinnerAlreadyDecided_LeavesTheExitToTheMissionAndIssuesNoAutorunEndGame()
    {
        var driver = Driver();
        StartAndGoLive(driver);
        _report.Winner(MatchSide.West);
        Mission(500.0);

        Tick(driver, 10.0);

        Assert.AreEqual(0, _host.Returns);
        Assert.IsNull(_report.Run!.End.EndGameVia);
    }

    [TestMethod]
    public void Tick_TheMissionLeavesByItselfAfterAWinner_ReportsFinalWithoutAnAutorunEndGame()
    {
        var driver = Driver();
        StartAndGoLive(driver);
        _report.Winner(MatchSide.West);

        _report.MissionClosed();
        Tick(driver, 1.0);
        _report.MainMenuReturned();
        Tick(driver, 1.0);

        Assert.AreEqual(0, _host.Returns);
        Assert.AreEqual("final", Done()["reason"]);
        Assert.AreEqual(1, _host.Quits);
    }

    [TestMethod]
    public void Tick_EndGameAlreadyRequestedByTheMission_IsNotRequestedAgain()
    {
        var driver = Driver();
        StartAndGoLive(driver);
        _report.EndGameRequested("auto");
        Mission(100.0);

        Tick(driver, 10.0);

        Assert.AreEqual(0, _host.Returns);
    }

    [TestMethod]
    public void Tick_TheRunNeverGoesLive_EndsAfterTheLiveWaitAndReportsNeverLive()
    {
        var driver = Driver();
        Tick(driver, 5.0);

        Tick(driver, 59.0);
        Assert.AreEqual(0, _host.Returns, "still inside live_wait_seconds");
        Tick(driver, 4.0);

        Assert.AreEqual(1, _host.Returns);
        Assert.AreEqual("never_live", Done()["reason"]);
        Assert.AreEqual(1, _host.Quits);
    }

    [TestMethod]
    public void Tick_WallLimitHitWhileLive_EndsTheGameAndReportsWallLimit()
    {
        var driver = Driver();
        StartAndGoLive(driver);

        Tick(driver, 520.0);

        Assert.AreEqual(1, _host.Returns);
        Assert.AreEqual("AutorunWallLimit", _report.Run!.End.ExitReason);
        Assert.AreEqual("wall_limit", Done()["reason"]);
    }

    [TestMethod]
    public void Tick_StartFailsAfterTheHostAccepted_QuitsWithStartFailed()
    {
        var driver = Driver();
        Tick(driver, 5.0);

        _report.Failure("C", "the game manager failed to load");
        Tick(driver, 2.0);

        Assert.AreEqual("start_failed", Done()["reason"]);
        Assert.AreEqual(1, _host.Quits);
    }

    [TestMethod]
    public void Tick_NoFinalPassYet_DoesNotQuitAndTimesOutAfterTwoMinutes()
    {
        _host.ReturnLeavesTheGame = false;
        var driver = Driver();
        StartAndGoLive(driver);
        Mission(100.0);

        Tick(driver, 100.0);
        Assert.AreEqual(0, _host.Quits, "the final=1 pass has not run");
        Tick(driver, 30.0);

        Assert.AreEqual("final_timeout", Done()["reason"]);
        Assert.AreEqual(1, _host.Quits);
    }

    [TestMethod]
    public void Tick_FinalPassRan_WritesTheDoneFileOnceThenQuitsOnce()
    {
        var driver = Driver();
        StartAndGoLive(driver);
        Mission(100.0);

        Tick(driver, 1.0);
        Tick(driver, 30.0);

        var done = Done();
        Assert.AreEqual("final", done["reason"]);
        Assert.AreEqual("1", done["run"]);
        Assert.AreEqual(Path.Combine(_dir, "fords_report_2026-10-31_12-30-45_run1.json"), done["report"]);
        Assert.AreEqual(Path.Combine(_dir, "taom_debug_2026-10-31_12-30-45.log"), done["log"]);
        Assert.AreEqual("100.0", done["live_seconds"]);
        StringAssert.Matches(done["final_total"], new System.Text.RegularExpressions.Regex(@"^pass=\d+ fail=\d+ partial=\d+ notseen=\d+$"));
        Assert.IsTrue(done.ContainsKey("wall_seconds"));
        Assert.IsTrue(done.ContainsKey("started"));
        Assert.IsTrue(done.ContainsKey("ended"));
        Assert.AreEqual(1, _host.Quits);
        Assert.AreEqual(1, _host.Returns);
    }

    [TestMethod]
    public void Tick_QuitSwitchedOff_WritesTheDoneFileButNeverQuits()
    {
        var driver = Driver("quit=0\n");
        StartAndGoLive(driver);
        Mission(100.0);

        Tick(driver, 30.0);

        Assert.AreEqual("final", Done()["reason"]);
        Assert.AreEqual(0, _host.Quits);
    }

    [TestMethod]
    public void Tick_TheDoneFileCannotBeWritten_StillQuits()
    {
        var driver = Driver();
        StartAndGoLive(driver);
        Mission(100.0);
        _host.WriteThrows = true;

        Tick(driver, 30.0);

        Assert.AreEqual(1, _host.Quits);
    }

    [TestMethod]
    public void Tick_WhileActive_RewritesTheStatusFileEveryFiveSeconds()
    {
        var driver = Driver();
        _host.AtMenu = false;
        Tick(driver, 1.0);
        Assert.IsFalse(_host.Files.ContainsKey(StatusFile), "not before the first beat is due");

        Tick(driver, 5.0);

        StringAssert.Contains(_host.Files[StatusFile], "phase=WaitMenu");
        StringAssert.Contains(_host.Files[StatusFile], "wall=");
    }

    [TestMethod]
    public void Tick_EveryPhaseChange_LogsAnAutorunLine()
    {
        var driver = Driver();
        StartAndGoLive(driver);
        Mission(100.0);
        Tick(driver, 5.0);

        var phases = _lines.Where(l => l.StartsWith("[FordsOfWar][Autorun] phase ")).ToList();

        Assert.IsTrue(phases.Any(l => l.Contains("WaitMenu -> WaitLive")));
        Assert.IsTrue(phases.Any(l => l.Contains("WaitLive -> Live")));
        Assert.IsTrue(phases.Any(l => l.Contains("Live -> WaitFinal")));
        Assert.IsTrue(phases.Any(l => l.Contains("WaitFinal -> Done")));
    }

    [TestMethod]
    public void Tick_StartFailsOnRouteBAfterTheHostAccepted_QuitsWithStartFailed()
    {
        var driver = Driver();
        Tick(driver, 5.0);

        _report.Failure("B", "the game state failed to push");
        Tick(driver, 1.0);
        Assert.AreEqual(0, _host.Quits, "route B finalizes on the menu return, which has not happened yet");
        _report.MainMenuReturned();
        Tick(driver, 1.0);

        Assert.AreEqual("start_failed", Done()["reason"]);
        Assert.AreEqual(1, _host.Quits);
    }

    [TestMethod]
    public void Tick_WallLimitHitAfterAWinner_StillEndsTheGameAndReportsWallLimit()
    {
        var driver = Driver();
        StartAndGoLive(driver);
        _report.Winner(MatchSide.West);

        Tick(driver, 520.0);

        Assert.AreEqual(1, _host.Returns);
        Assert.AreEqual("AutorunWallLimit", _report.Run!.End.ExitReason);
        Assert.AreEqual("wall_limit", Done()["reason"]);
    }

    [TestMethod]
    public void Tick_TheRunNeverGoesLiveAndTheExitNeverCompletes_KeepsBothReasons()
    {
        _host.ReturnLeavesTheGame = false;
        var driver = Driver();
        Tick(driver, 75.0);

        Tick(driver, 130.0);

        Assert.AreEqual("never_live+final_timeout", Done()["reason"]);
        Assert.AreEqual(1, _host.Quits);
    }

    [TestMethod]
    public void Tick_AnOutsideEndGameThatNeverClosesTheMission_StopsWaitingAfterTheFinalWait()
    {
        _host.ReturnLeavesTheGame = false;
        var driver = Driver();
        StartAndGoLive(driver);
        _report.EndGameRequested("auto");

        Tick(driver, 100.0);
        Assert.IsFalse(_host.Files.ContainsKey(DoneFile), "still inside the final wait");
        Tick(driver, 30.0);

        Assert.AreEqual("exit_stalled", Done()["reason"]);
        Assert.AreEqual(1, _host.Quits);
        Assert.AreEqual(0, _host.Returns, "the outside EndGame is the mission's, not the autorun's");
    }

    [TestMethod]
    public void Tick_LiveForThirtyWallSeconds_LogsTheClockLineWithTheMissionPerWallRatio()
    {
        var driver = Driver();
        StartAndGoLive(driver);

        Tick(driver, 31.0);

        Assert.IsTrue(_lines.Any(l => l.StartsWith("[FordsOfWar][Autorun] clock ") && l.Contains(" ratio=")));
    }

    [TestMethod]
    public void Constructor_ArmedPlan_LogsThePlanAndItsWarnings()
    {
        Driver("live_seconds=-1\n");

        Assert.IsTrue(_lines.Any(l => l.StartsWith("[FordsOfWar][Autorun] armed ")));
        Assert.IsTrue(_lines.Any(l => l.StartsWith("[FordsOfWar][Autorun] arm file: ") && l.Contains("live_seconds")));
    }
}
