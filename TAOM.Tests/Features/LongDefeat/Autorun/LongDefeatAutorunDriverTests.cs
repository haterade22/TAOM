using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Autorun;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat.Autorun;

[TestClass]
public class LongDefeatAutorunDriverTests
{
    private sealed class FakeHost : ILongDefeatAutorunHost
    {
        public LongDefeatSession Session = null!;
        public bool AtMenu = true;
        public bool InquiryOpen;
        public bool StartResult = true;
        public bool ReturnLeavesTheGame = true;
        public bool WriteThrows;
        public Action? OnStart;
        public int Hidden;
        public int Starts;
        public int Returns;
        public int Quits;
        public LongDefeatSetup? Setup;
        public readonly Dictionary<string, string> Files = new();
        public readonly List<string> Screenshots = new();
        public readonly List<string> Order = new();

        public bool AtMainMenu => AtMenu;

        public bool HideAnyInquiry()
        {
            if (!InquiryOpen)
                return false;

            Hidden++;
            return true;
        }

        public bool Start(LongDefeatSetup setup)
        {
            Starts++;
            Setup = setup;
            if (!StartResult)
                return false;

            AtMenu = false;
            OnStart?.Invoke();
            return true;
        }

        public void ReturnToMainMenu()
        {
            Returns++;
            if (!ReturnLeavesTheGame)
                return;

            Session.Clear();
            AtMenu = true;
        }

        public void WriteFile(string name, string text)
        {
            if (WriteThrows)
                throw new IOException("disk full");

            Files[name] = text;
            Order.Add("write " + name);
        }

        public void TakeScreenshot(string name) => Screenshots.Add(name);

        public void Quit()
        {
            Quits++;
            Order.Add("quit");
        }
    }

    private const string Done = "long_defeat_done.txt";
    private const string Status = "long_defeat_status.txt";

    private LongDefeatSession _session = null!;
    private FakeHost _host = null!;
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Init()
    {
        _session = new LongDefeatSession();
        _host = new FakeHost { Session = _session };
        _logger = Substitute.For<IModLogger>();
        _logger.LogFilePath.Returns(@"E:\logs\taom_debug_2026-11-04_12-00-00.log");
    }

    private static string Shipped(string file) =>
        File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "long_defeat", file));

    private LongDefeatAutorunDriver Driver(string arm = "", bool armed = true)
    {
        var plan = armed
            ? LongDefeatAutorunPlan.Resolve(_ => @"E:\arm.txt", _ => true, _ => "out=E:\\out\n" + arm)
            : new LongDefeatAutorunPlan();
        var catalog = LongDefeatConfigCatalog.FromTexts(Shipped("fortresses.json"), Shipped("waves.json"), Shipped("shop.json"), _logger);
        return new LongDefeatAutorunDriver(plan, _session, catalog, _host, _logger);
    }

    private static void Run(LongDefeatAutorunDriver driver, double seconds, float step = 1f)
    {
        for (double t = 0; t < seconds; t += step)
            driver.Tick(step);
    }

    private static SurvivalRun NewRun()
    {
        var waves = new WavePlan(
            new[] { new WaveSpec(110, new[] { new WaveTroop("orc", FormationSlot.Infantry, 1) }) },
            new WaveScaling(10, 300));
        var shop = new ShopPlan(600, new[] { new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180) });
        var plan = new RunPlan("helms", new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) }, waves, shop, RunRules.Default,
            RunOptions.Default);
        var run = new SurvivalRun(plan);
        run.Begin();
        return run;
    }

    private LongDefeatAutorunDriver Started(string arm = "quit=1\n")
    {
        var driver = Driver(arm);
        _host.OnStart = () => _session.Fill(NewRun());
        Run(driver, 6);
        Assert.AreEqual(1, _host.Starts, "the menu should have settled and the run started");
        return driver;
    }

    private string DoneText() => _host.Files.TryGetValue(Done, out var text) ? text : "";

    [TestMethod]
    public void Unarmed_DoesNothing()
    {
        var driver = Driver(armed: false);

        Run(driver, 30);

        Assert.AreEqual(0, _host.Starts);
        Assert.AreEqual(0, _host.Files.Count);
        Assert.AreEqual(0, _host.Quits);
    }

    [TestMethod]
    public void Tick_ABadDt_IsIgnored()
    {
        var driver = Driver();

        driver.Tick(float.NaN);
        driver.Tick(0f);
        driver.Tick(-1f);
        Run(driver, 3);
        Assert.AreEqual(0, _host.Starts, "NaN, zero and negative dt must not count toward the settle time");

        // A NaN that got into the clock would keep the start from ever coming: four settled seconds are not yet more than four,
        // and one more real second must start the run.
        Run(driver, 1);
        Assert.AreEqual(0, _host.Starts);
        Run(driver, 1);
        Assert.AreEqual(1, _host.Starts, "a poisoned clock never reaches the settle time");
    }

    [TestMethod]
    public void WaitMenu_StartsOnlyAfterTheMenuHasBeenUpForMoreThanFourSeconds()
    {
        var driver = Driver();

        Run(driver, 4);
        Assert.AreEqual(0, _host.Starts);

        driver.Tick(1f);
        Assert.AreEqual(1, _host.Starts);
    }

    [TestMethod]
    public void WaitMenu_LeavingTheMenuRestartsTheWait_AndAnInquiryIsHidden()
    {
        var driver = Driver();
        _host.InquiryOpen = true;

        Run(driver, 3);
        _host.AtMenu = false;
        Run(driver, 3);
        _host.AtMenu = true;
        Run(driver, 4);
        Assert.AreEqual(0, _host.Starts);

        Run(driver, 2);
        Assert.AreEqual(1, _host.Starts);
        Assert.IsTrue(_host.Hidden > 0);
    }

    [TestMethod]
    public void Start_UsesThePlansFortress_ItsOptionsAndTheOutFolderAsTheRecordsFolder()
    {
        var driver = Driver("fortress=helms\nwaves=2\ngarrison_scale=0.2\n");

        Run(driver, 6);

        Assert.AreEqual("helms", _host.Setup!.FortressId);
        Assert.AreEqual("autorun", _host.Setup.Trigger);
        Assert.AreEqual(2, _host.Setup.Options.StopAfterWaves);
        Assert.AreEqual(0.2f, _host.Setup.Options.GarrisonScale);
        Assert.IsTrue(_host.Setup.Options.Autoplay);
        Assert.AreEqual(@"E:\out", _host.Setup.RecordsFolder);
    }

    [TestMethod]
    public void Start_WithNoFortressInThePlan_TakesTheFirstShippedOne()
    {
        var driver = Driver();

        Run(driver, 6);

        Assert.AreEqual("helms", _host.Setup!.FortressId);
    }

    [TestMethod]
    public void Start_AnUnknownFortress_IsRefusedWithoutAskingTheHost_AndTheDoneFileSaysSo()
    {
        var driver = Driver("fortress=nowhere\nquit=1\n");

        Run(driver, 6);

        Assert.AreEqual(0, _host.Starts);
        StringAssert.Contains(DoneText(), "reason=start_refused");
        Assert.AreEqual(1, _host.Quits);
    }

    [TestMethod]
    public void Start_TheHostRefusing_IsStartRefused()
    {
        _host.StartResult = false;
        var driver = Driver("quit=1\n");

        Run(driver, 6);

        StringAssert.Contains(DoneText(), "reason=start_refused");
        Assert.AreEqual(1, _host.Quits);
    }

    [TestMethod]
    public void WaitStart_NoMissionIn600Seconds_IsNeverStarted()
    {
        var driver = Driver("quit=1\n");
        Run(driver, 6);
        Assert.AreEqual(1, _host.Starts);

        Run(driver, 590);
        Assert.AreEqual("", DoneText());

        Run(driver, 20);
        StringAssert.Contains(DoneText(), "reason=never_started");
        Assert.AreEqual(1, _host.Quits);
    }

    [TestMethod]
    public void AnEndedRun_LeavesTheMission_AndTheDoneFileCarriesTheReason_TheReportAndTheCounts()
    {
        var driver = Started();
        Run(driver, 10);
        Assert.AreEqual("", DoneText(), "a running mission is not done");

        EndRun();
        _session.AddLiveSeconds(42f);
        _session.RecordReport(@"E:\logs\long_defeat_report_x_ld1.json");
        Run(driver, 6);

        var done = DoneText();
        StringAssert.Contains(done, "reason=abandoned");
        StringAssert.Contains(done, @"report=E:\logs\long_defeat_report_x_ld1.json");
        StringAssert.Contains(done, @"log=E:\logs\taom_debug_2026-11-04_12-00-00.log");
        StringAssert.Contains(done, "live_seconds=42.0");
        StringAssert.Contains(done, "waves_cleared=0");
        StringAssert.Contains(done, "started=");
        StringAssert.Contains(done, "ended=");
        Assert.IsTrue(_host.Returns >= 1, "an ended run leaves through the same exit the panel's Leave uses");
        CollectionAssert.AreEqual(new[] { "write " + Done, "quit" }, _host.Order.Where(o => o != "write " + Status).ToList(),
            "the done file is written once, before the quit");
    }

    private void EndRun()
    {
        // Abandon is the one end a test can reach without driving a whole run.
        var run = NewRun();
        run.Abandon();
        _session.Fill(run);
    }

    [TestMethod]
    public void AnEndedRun_WaitsTwoWallSecondsBeforeLeaving_SoTheEndPanelCanOpen()
    {
        var driver = Started();
        Run(driver, 5);
        EndRun();

        driver.Tick(1f);
        driver.Tick(1f);
        Assert.AreEqual(0, _host.Returns, "the panel opens on the view's next refresh, so the first seconds after the result are not spent leaving");
        driver.Tick(1f);
        driver.Tick(0.5f);

        Assert.AreEqual(1, _host.Returns);
    }

    [TestMethod]
    public void TheDoneFile_NamesTheLogByItsFullPath_WhenTheLoggerHoldsARelativeOne()
    {
        _logger.LogFilePath.Returns(@"Logs\taom_debug_2026-11-04_12-00-00.log");
        var driver = Started();
        EndRun();

        Run(driver, 6);

        StringAssert.Contains(DoneText(), "log=" + Path.GetFullPath(@"Logs\taom_debug_2026-11-04_12-00-00.log"));
    }

    [TestMethod]
    public void TheDoneFile_IsWrittenOnlyAfterTheMissionHasLeft()
    {
        var driver = Started();
        _host.ReturnLeavesTheGame = false;
        EndRun();

        Run(driver, 60);

        Assert.AreEqual("", DoneText(), "the end checks and the report come at mission teardown, so the file waits for the menu");
        Assert.AreEqual(0, _host.Quits);
    }

    [TestMethod]
    public void AnExitThatNeverReturns_EndsAfterTwoMinutes_AndTheReasonSaysSo()
    {
        var driver = Started();
        _host.ReturnLeavesTheGame = false;
        EndRun();

        Run(driver, 130);

        StringAssert.Contains(DoneText(), "reason=abandoned+exit_stalled");
        Assert.AreEqual(1, _host.Quits);
    }

    [TestMethod]
    public void AFault_IsItsOwnReason()
    {
        var driver = Started();
        EndRun();
        _session.RecordFault("OnMissionTick");  // after EndRun: Fill clears Fault. A real fault leaves both set.
        _host.AtMenu = true;

        Run(driver, 3);

        StringAssert.Contains(DoneText(), "reason=fault");
    }

    [TestMethod]
    public void AFaultRecordedBeforeAnySessionFill_LeavesAtOnce_NotAfterTheStartWait()
    {
        // A launch refusal and a failed OnStart record the fault with nothing to fill the session with, so IsActive never comes.
        var driver = Driver("quit=1\n");
        _host.OnStart = () => _session.RecordFault("OnStart");

        Run(driver, 6);
        Run(driver, 10);

        StringAssert.Contains(DoneText(), "reason=fault");
        Assert.AreEqual(1, _host.Quits);
    }

    [TestMethod]
    public void TheDoneFile_CarriesTheExpectationTheArmFileNamed()
    {
        var driver = Started("expect=lost\nquit=1\n");
        EndRun();

        Run(driver, 6);

        StringAssert.Contains(DoneText(), "expect=lost");
    }

    [TestMethod]
    public void TheDoneFile_SaysNoneWhenNoExpectationWasArmed()
    {
        var driver = Started();
        EndRun();

        Run(driver, 6);

        StringAssert.Contains(DoneText(), "expect=none");
    }

    [TestMethod]
    public void TheWallLimit_EndsTheRun_AndKeepsItsOwnReasonOverTheAbandonedResult()
    {
        var driver = Started("wall_limit_seconds=60\nquit=1\n");
        _host.ReturnLeavesTheGame = false;

        Run(driver, 70);
        Assert.AreEqual(1, _host.Returns);
        _host.ReturnLeavesTheGame = true;
        EndRun();
        _host.ReturnToMainMenu();
        Run(driver, 3);

        StringAssert.Contains(DoneText(), "reason=wall_limit");
    }

    [TestMethod]
    public void TheLiveLimit_IsMissionSeconds_NotWallSeconds()
    {
        var driver = Started("live_limit_seconds=100\nquit=1\n");

        Run(driver, 200);
        Assert.AreEqual(0, _host.Returns, "wall time alone must not trip a mission-seconds limit");

        _session.AddLiveSeconds(99f);
        Run(driver, 2);
        Assert.AreEqual(0, _host.Returns);

        _session.AddLiveSeconds(2f);
        Run(driver, 1);
        Assert.AreEqual(1, _host.Returns);
        Run(driver, 3);

        StringAssert.Contains(DoneText(), "reason=live_limit");
    }

    [TestMethod]
    public void TheStatusFile_IsRewrittenEveryFiveWallSeconds_UntilDone()
    {
        var driver = Started();
        _host.Files.Clear();

        Run(driver, 5);
        Assert.IsTrue(_host.Files.ContainsKey(Status));
        StringAssert.Contains(_host.Files[Status], "phase=");
        StringAssert.Contains(_host.Files[Status], "wall=");
        StringAssert.Contains(_host.Files[Status], "live=");

        _host.Files.Clear();
        Run(driver, 3);
        Assert.IsFalse(_host.Files.ContainsKey(Status), "five seconds have not passed again");
    }

    [TestMethod]
    public void Screenshots_AreTakenOnceEach_AtTheirLiveSeconds()
    {
        var driver = Started("screenshots=30,90\n");

        _session.AddLiveSeconds(31f);
        Run(driver, 2);
        CollectionAssert.AreEqual(new[] { "hud_30_0.bmp" }, _host.Screenshots);

        _session.AddLiveSeconds(100f);
        Run(driver, 2);
        CollectionAssert.AreEqual(new[] { "hud_30_0.bmp", "hud_90_0.bmp" }, _host.Screenshots);
    }

    [TestMethod]
    public void AFailedFileWrite_DoesNotStopTheQuit()
    {
        _host.WriteThrows = true;
        var driver = Started();
        EndRun();

        Run(driver, 6);

        Assert.AreEqual(1, _host.Quits);
    }

    [TestMethod]
    public void WithoutQuit_TheDoneFileIsWrittenAndTheGameStays()
    {
        var driver = Started("");
        EndRun();

        Run(driver, 6);

        StringAssert.Contains(DoneText(), "reason=abandoned");
        Assert.AreEqual(0, _host.Quits);
    }

    [TestMethod]
    public void ADoneDriver_WritesNothingMore()
    {
        var driver = Started();
        EndRun();
        Run(driver, 6);
        var writes = _host.Order.Count;

        Run(driver, 60);

        Assert.AreEqual(writes, _host.Order.Count);
    }

    [TestMethod]
    public void ReasonFor_NamesEveryRunEnd_AndTheNamesAreWhatTheJudgeReads()
    {
        Assert.AreEqual("final", LongDefeatAutorunDone.ReasonFor(RunEndReason.TargetReached));
        Assert.AreEqual("lost_defenders", LongDefeatAutorunDone.ReasonFor(RunEndReason.DefendersDead));
        Assert.AreEqual("lost_keep", LongDefeatAutorunDone.ReasonFor(RunEndReason.KeepLost));
        Assert.AreEqual("stalemate", LongDefeatAutorunDone.ReasonFor(RunEndReason.Stalemate));
        Assert.AreEqual("abandoned", LongDefeatAutorunDone.ReasonFor(RunEndReason.Abandoned));
        foreach (RunEndReason reason in Enum.GetValues(typeof(RunEndReason)))
            Assert.AreNotEqual("", LongDefeatAutorunDone.ReasonFor(reason), reason.ToString());
    }
}
