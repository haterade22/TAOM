using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.Autorun.Scenario;

namespace TAOM.Tests.Features.Autorun.Scenario;

[TestClass]
public class ScenarioAutorunDriverTests
{
    private const string Head = "kind=campaign-new\nout=E:\\x\\out\n";

    private FakeCampaignAutorunHost _host = null!;
    private IModLogger _logger = null!;
    private IOutcomeLog _outcomes = null!;

    [TestInitialize]
    public void Init()
    {
        _host = new FakeCampaignAutorunHost();
        _logger = Substitute.For<IModLogger>();
        _outcomes = Substitute.For<IOutcomeLog>();
    }

    private ScenarioAutorunDriver Driver(string extra = "")
    {
        var plan = ScenarioPlan.Resolve(n => n == ScenarioPlan.EnvVar ? @"E:\arm.txt" : null, _ => true, _ => Head + extra);
        Assert.IsTrue(plan.Armed, plan.Problem);
        return new ScenarioAutorunDriver(plan, _host, _logger, _outcomes);
    }

    private static void Run(ScenarioAutorunDriver driver, int ticks, float dt = 1f)
    {
        for (var i = 0; i < ticks; i++)
            driver.Tick(dt);
    }

    /// <summary>Menu, start, creation done and the map settled: the driver is in Running with day_start 10.</summary>
    private ScenarioAutorunDriver ToRunning(string extra = "")
    {
        var driver = Driver(extra);
        _host.OnStart = () => _host.ScreenValue = AutorunScreen.CharacterCreation;
        Run(driver, 4);
        driver.Tick(1f);
        _host.ScreenValue = AutorunScreen.Map;
        _host.Days = 10.4;
        Run(driver, 6);
        Assert.AreEqual("Running", driver.PhaseName);
        return driver;
    }

    private string Done() => _host.Files[ScenarioAutorunDone.DoneFile];

    private static string Value(string text, string key) =>
        text.Split('\n').First(l => l.StartsWith(key + "=", StringComparison.Ordinal)).Substring(key.Length + 1);

    private int InfoLogs(string contains) =>
        _logger.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "LogInfo" && c.GetArguments()[0] is string s && s.Contains(contains));

    [TestMethod]
    public void Tick_UnarmedPlan_DoesNothing()
    {
        var driver = new ScenarioAutorunDriver(new ScenarioPlan(), _host, _logger, _outcomes);

        Run(driver, 20);

        Assert.AreEqual(0, _host.Starts);
        Assert.AreEqual(0, _host.Files.Count);
        Assert.AreEqual("Off", driver.PhaseName);
    }

    [TestMethod]
    public void Constructor_ArmedPlan_LogsThePlanAndTheWarnings()
    {
        Driver("bogus=1\n");

        Assert.AreEqual(1, InfoLogs("[Autorun][Scenario] armed plan=kind=campaign-new"));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("bogus")));
    }

    [TestMethod]
    public void Tick_BadDt_IsIgnored()
    {
        var driver = Driver();

        foreach (var dt in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            Run(driver, 10, dt);

        Assert.AreEqual(0, _host.Starts);
        Assert.AreEqual("WaitMenu", driver.PhaseName);
    }

    [TestMethod]
    public void Tick_MenuHeldUnderFourSeconds_DoesNotStart()
    {
        Run(Driver(), 3);

        Assert.AreEqual(0, _host.Starts);
    }

    [TestMethod]
    public void Tick_MenuHeldFourSeconds_StartsTheCampaignOnce()
    {
        var driver = Driver();
        _host.OnStart = () => _host.ScreenValue = AutorunScreen.Other;

        Run(driver, 40);

        Assert.AreEqual(1, _host.Starts);
        Assert.AreEqual("Creation", driver.PhaseName);
    }

    [TestMethod]
    public void Tick_NotOnTheMainMenu_NeverStarts()
    {
        _host.ScreenValue = AutorunScreen.Other;

        Run(Driver(), 30);

        Assert.AreEqual(0, _host.Starts);
    }

    [TestMethod]
    public void Tick_MenuLeftBeforeSettling_RestartsTheWait()
    {
        var driver = Driver();
        Run(driver, 3);
        _host.ScreenValue = AutorunScreen.Other;
        driver.Tick(1f);
        _host.ScreenValue = AutorunScreen.MainMenu;

        Run(driver, 3);
        Assert.AreEqual(0, _host.Starts);

        driver.Tick(1f);
        Assert.AreEqual(1, _host.Starts);
    }

    [TestMethod]
    public void Tick_InquiryAtTheMenu_IsHiddenCountedAndItsTitleLoggedAtMostTwentyTimes()
    {
        _host.InquiryTitle = "Boot notice";
        var driver = Driver();
        _host.OnStart = () => _host.ScreenValue = AutorunScreen.Other;

        Run(driver, 3);
        _host.InquiryTitle = "Boot notice";
        Run(driver, 40);

        Assert.IsTrue(_host.InquiriesHidden > 20);
        Assert.AreEqual(20, InfoLogs("inquiry hidden"));
        Assert.AreEqual(1, InfoLogs("n=1 phase=WaitMenu title=Boot notice"));
    }

    [TestMethod]
    public void Tick_StartRefused_FinishesStartRefusedAndNeverRetries()
    {
        _host.StartResult = false;
        var driver = Driver();

        Run(driver, 30);

        Assert.AreEqual(1, _host.Starts);
        Assert.AreEqual("start_refused", Value(Done(), "reason"));
    }

    [TestMethod]
    public void Tick_VideoPlaying_FinishesTheVideoEachTick()
    {
        var driver = Driver();
        _host.OnStart = () => _host.ScreenValue = AutorunScreen.Video;
        Run(driver, 4);

        Run(driver, 3);

        Assert.AreEqual(3, _host.VideoFinishes);
        Assert.AreEqual("Creation", driver.PhaseName);
    }

    [TestMethod]
    public void Tick_CreationNotReady_AsksAgainNextTick()
    {
        var driver = Driver();
        _host.OnStart = () => _host.ScreenValue = AutorunScreen.CharacterCreation;
        _host.CreationReady = false;
        Run(driver, 4);

        Run(driver, 3);

        Assert.AreEqual(3, _host.CreationCalls);
        Assert.AreEqual("Creation", driver.PhaseName);
    }

    [TestMethod]
    public void Tick_CreationFinishes_MovesToWaitMapWithThePlansCulture()
    {
        var driver = Driver("culture=vlandia\n");
        _host.OnStart = () => _host.ScreenValue = AutorunScreen.CharacterCreation;
        Run(driver, 5);

        Assert.AreEqual("WaitMap", driver.PhaseName);
        Assert.AreEqual("vlandia", _host.CreationCulture);
    }

    [TestMethod]
    public void Tick_CreationReportsAProblem_FinishesCreationFailedAndLogsIt()
    {
        var driver = Driver();
        _host.OnStart = () => _host.ScreenValue = AutorunScreen.CharacterCreation;
        _host.CreationProblem = "no such culture 'gondor'";

        Run(driver, 6);

        Assert.AreEqual("creation_failed", Value(Done(), "reason"));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("no such culture")));
    }

    [TestMethod]
    public void Tick_StuckInCreation_FinishesCreationTimeoutAfterSixHundredSeconds()
    {
        var driver = Driver();
        _host.OnStart = () => _host.ScreenValue = AutorunScreen.Other;
        Run(driver, 4);

        Run(driver, 598);
        Assert.AreEqual(0, _host.Files.Count(f => f.Key == ScenarioAutorunDone.DoneFile));

        Run(driver, 3);
        Assert.AreEqual("creation_timeout", Value(Done(), "reason"));
    }

    [TestMethod]
    public void Tick_MapHeldUnderFiveSeconds_DoesNotPrepare()
    {
        var driver = Driver();
        _host.OnStart = () => _host.ScreenValue = AutorunScreen.CharacterCreation;
        Run(driver, 5);
        _host.ScreenValue = AutorunScreen.Map;
        _host.Days = 3.0;

        Run(driver, 4);

        Assert.AreEqual(0, _host.PrepareCalls);
    }

    [TestMethod]
    public void Tick_MapMenuHeldFiveSeconds_PreparesOnceLogsItAndStartsRunning()
    {
        var driver = Driver();
        _host.OnStart = () => _host.ScreenValue = AutorunScreen.CharacterCreation;
        Run(driver, 5);
        _host.ScreenValue = AutorunScreen.MapMenu;
        _host.Days = 3.7;

        Run(driver, 20);

        Assert.AreEqual(1, _host.PrepareCalls);
        Assert.AreEqual(1, InfoLogs("autosave off, holding"));
        Assert.AreEqual("Running", driver.PhaseName);
    }

    [TestMethod]
    public void Tick_MapLeftBeforeSettling_RestartsTheSettle()
    {
        var driver = Driver();
        _host.OnStart = () => _host.ScreenValue = AutorunScreen.CharacterCreation;
        Run(driver, 5);
        _host.Days = 3.0;
        _host.ScreenValue = AutorunScreen.Map;
        Run(driver, 4);
        _host.ScreenValue = AutorunScreen.Other;
        driver.Tick(1f);
        _host.ScreenValue = AutorunScreen.Map;

        Run(driver, 4);
        Assert.AreEqual(0, _host.PrepareCalls);

        driver.Tick(1f);
        Assert.AreEqual(1, _host.PrepareCalls);
    }

    [TestMethod]
    public void Tick_NoMapForSixHundredSeconds_FinishesMapTimeout()
    {
        var driver = Driver();
        _host.OnStart = () => _host.ScreenValue = AutorunScreen.CharacterCreation;
        Run(driver, 5);
        _host.ScreenValue = AutorunScreen.Other;

        Run(driver, 601);

        Assert.AreEqual("map_timeout", Value(Done(), "reason"));
        Assert.AreEqual(0, _host.PrepareCalls);
    }

    [TestMethod]
    public void Tick_ElapsedDaysNaNOnTheMap_FinishesMapTimeoutAfterPreparingOnce()
    {
        var driver = Driver();
        _host.OnStart = () => _host.ScreenValue = AutorunScreen.CharacterCreation;
        Run(driver, 5);
        _host.ScreenValue = AutorunScreen.Map;
        _host.Days = double.NaN;

        Run(driver, 20);

        Assert.AreEqual("map_timeout", Value(Done(), "reason"));
        Assert.AreEqual(1, _host.PrepareCalls);
        Assert.AreEqual(1, _host.Restores);
    }

    [TestMethod]
    public void Tick_Running_HidesInquiriesAndNoticesCountsThemAndKeepsTheSpeed()
    {
        var driver = ToRunning("speed=12\ndays=50\n");
        _host.InquiryTitle = "Event";
        _host.NoticeOpen = true;
        _host.FastForwardChanged = true;

        Run(driver, 3);

        Assert.AreEqual(12f, _host.LastSpeed);
        _host.Days = 60;
        driver.Tick(1f);
        var done = Done();
        Assert.IsTrue(int.Parse(Value(done, "inquiries_hidden")) >= 4);
        Assert.AreEqual("1", Value(done, "notices_hidden"));
        Assert.AreEqual("4", Value(done, "time_resets"));
    }

    // GauntletSceneNotification marks a notification active one layer tick before its scene exists, and closing it then throws on the
    // null scene (CloseNotification), so a notice is hidden only when it was active on the previous tick too.
    [TestMethod]
    public void Tick_SceneNotification_IsHiddenOnItsSecondSightingOnly()
    {
        var driver = ToRunning("days=50\n");
        _host.NoticeOpen = true;

        driver.Tick(1f);
        Assert.AreEqual(0, _host.NoticesHidden);

        driver.Tick(1f);
        Assert.AreEqual(1, _host.NoticesHidden);
    }

    [TestMethod]
    public void Tick_SceneNotificationGoneBeforeTheSecondTick_IsNeverHidden()
    {
        var driver = ToRunning("days=50\n");
        _host.NoticeOpen = true;
        driver.Tick(1f);
        _host.NoticeOpen = false;
        driver.Tick(1f);
        _host.NoticeOpen = true;

        driver.Tick(1f);

        Assert.AreEqual(0, _host.NoticesHidden);
    }

    // A TAOM story event window (the War Chronicle's) pauses the campaign while it shows; the first run stood 9 minutes on day 2.
    [TestMethod]
    public void Tick_Running_ClosesAnEventWindowLogsItAndCountsItApartFromNotices()
    {
        var driver = ToRunning("days=50\n");
        _host.EventWindowOpen = true;
        _host.NoticeOpen = true;

        driver.Tick(1f);
        driver.Tick(1f);

        Assert.AreEqual(1, _host.EventWindowsClosed);
        Assert.AreEqual(1, InfoLogs("event window closed n=1 phase=Running"));
        _host.Days = 60;
        driver.Tick(1f);
        var done = Done();
        Assert.AreEqual("1", Value(done, "event_windows_closed"));
        Assert.AreEqual("1", Value(done, "notices_hidden"));
    }

    [TestMethod]
    public void Tick_Running_WithNoEventWindow_ReportsNoneClosed()
    {
        var driver = ToRunning("days=50\n");

        Run(driver, 3);
        _host.Days = 60;
        driver.Tick(1f);

        Assert.AreEqual("0", Value(Done(), "event_windows_closed"));
        Assert.AreEqual(0, InfoLogs("event window closed"));
    }

    [TestMethod]
    public void Tick_WantedDaysPassed_FinishesOkWithTheDayNumbers()
    {
        var driver = ToRunning("days=3\nscenario=smoke\nculture=gondor\n");
        _host.Days = 12.9;
        driver.Tick(1f);
        Assert.IsFalse(_host.Files.ContainsKey(ScenarioAutorunDone.DoneFile));

        _host.Days = 13.0;
        driver.Tick(1f);

        var done = Done();
        Assert.AreEqual("ok", Value(done, "reason"));
        Assert.AreEqual("10", Value(done, "day_start"));
        Assert.AreEqual("13", Value(done, "day_end"));
        Assert.AreEqual("3", Value(done, "days_run"));
        Assert.AreEqual("3", Value(done, "days_wanted"));
        Assert.AreEqual("smoke", Value(done, "scenario"));
        Assert.AreEqual("gondor", Value(done, "culture"));
        Assert.AreEqual("autosave off, holding", Value(done, "prepared"));
    }

    [TestMethod]
    public void Tick_NoNewDayForStallSeconds_FinishesStalledAndLogsScreenAndTimeLock()
    {
        var driver = ToRunning("stall_seconds=30\ndays=50\n");
        _host.TimeLockedValue = true;
        _host.ScreenValue = AutorunScreen.MapMenu;

        Run(driver, 28);
        Assert.IsFalse(_host.Files.ContainsKey(ScenarioAutorunDone.DoneFile));

        Run(driver, 3);
        Assert.AreEqual("stalled", Value(Done(), "reason"));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("stalled") && s.Contains("screen=MapMenu") && s.Contains("time_locked=1")));
    }

    [TestMethod]
    public void Tick_NewDayResetsTheStallClock()
    {
        var driver = ToRunning("stall_seconds=30\ndays=50\n");
        Run(driver, 25);
        _host.Days = 11.0;
        driver.Tick(1f);

        Run(driver, 25);

        Assert.IsFalse(_host.Files.ContainsKey(ScenarioAutorunDone.DoneFile));
    }

    [TestMethod]
    public void Tick_ElapsedDaysTurnsNaNWhileRunning_StallsRatherThanFinishingOk()
    {
        var driver = ToRunning("stall_seconds=20\ndays=1\n");
        _host.Days = double.NaN;

        Run(driver, 25);

        Assert.AreEqual("stalled", Value(Done(), "reason"));
    }

    [TestMethod]
    public void Tick_WallLimitPassed_FinishesWallLimit()
    {
        var driver = ToRunning("wall_limit_seconds=60\nstall_seconds=1000\ndays=50\n");

        Run(driver, 60);

        Assert.AreEqual("wall_limit", Value(Done(), "reason"));
    }

    [TestMethod]
    public void Tick_StallAndWallLimitTogether_FinishOnceOnly()
    {
        var driver = ToRunning("wall_limit_seconds=30\nstall_seconds=30\ndays=50\n");

        Run(driver, 80);

        Assert.AreEqual(1, _host.Order.Count(o => o == "write:" + ScenarioAutorunDone.DoneFile));
        Assert.AreEqual(1, _host.Ends);
        Assert.AreEqual(1, _host.Restores);
    }

    [TestMethod]
    public void Finish_RunsTheStepsInOrderAndQuitsOnceWhenTheMenuIsBack()
    {
        var driver = ToRunning("days=1\nquit=1\n");
        _host.Days = 11.0;
        driver.Tick(1f);
        Assert.AreEqual(0, _host.Quits);

        _host.ScreenValue = AutorunScreen.MainMenu;
        Run(driver, 5);

        CollectionAssert.AreEqual(new[] { "restore", "write:" + ScenarioAutorunDone.DoneFile, "end", "quit" },
            _host.Order.Where(o => o != "write:" + ScenarioAutorunDone.StatusFile).ToList());
        Assert.AreEqual(1, _host.Quits);
        Assert.AreEqual(1, InfoLogs("[Autorun][Scenario] done reason=ok"));
    }

    [TestMethod]
    public void Finish_QuitNotRequested_NeverQuits()
    {
        var driver = ToRunning("days=1\nquit=0\n");
        _host.Days = 11.0;
        _host.ScreenValue = AutorunScreen.MainMenu;

        Run(driver, 200);

        Assert.AreEqual(0, _host.Quits);
        Assert.AreEqual("Done", driver.PhaseName);
    }

    // The launcher kills the game 120 s after it sees the done file (run_fords_autorun.py EXIT_GRACE_SECONDS), so the game quits first.
    [TestMethod]
    public void Finish_MenuNeverReturns_QuitsAfterSixtySecondsAndLogsTheStall()
    {
        var driver = ToRunning("days=1\nquit=1\n");
        _host.Days = 11.0;
        driver.Tick(1f);
        _host.ScreenValue = AutorunScreen.Other;

        Run(driver, 58);
        Assert.AreEqual(0, _host.Quits);

        Run(driver, 5);
        Assert.AreEqual(1, _host.Quits);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("exit_stalled")));
    }

    [TestMethod]
    public void Tick_HostThrowsWhileExiting_QuitsAndIsDone()
    {
        var driver = ToRunning("days=1\nquit=1\n");
        _host.Days = 11.0;
        driver.Tick(1f);
        _host.ScreenThrows = true;

        driver.Tick(1f);

        Assert.AreEqual(1, _host.Quits);
        Assert.IsTrue(driver.IsDone);
    }

    [TestMethod]
    public void Tick_WallLimit_CountsFromTheCampaignStart()
    {
        // The start is at wall 4 and Running begins at wall 11; the limit trips once wall - 4 exceeds 60.
        var driver = ToRunning("wall_limit_seconds=60\nstall_seconds=1000\ndays=50\n");

        Run(driver, 50);
        Assert.IsFalse(_host.Files.ContainsKey(ScenarioAutorunDone.DoneFile));

        Run(driver, 4);
        Assert.AreEqual("wall_limit", Value(Done(), "reason"));
    }

    [TestMethod]
    public void Tick_DaysDoneStalledAndOverTheWallLimitOnOneTick_FinishesOk()
    {
        var driver = Driver("days=1\nstall_seconds=0.5\nwall_limit_seconds=1\n");
        _host.OnStart = () => _host.ScreenValue = AutorunScreen.CharacterCreation;
        Run(driver, 5);
        _host.ScreenValue = AutorunScreen.Map;
        _host.Days = 10.4;
        Run(driver, 5);
        Assert.AreEqual("Running", driver.PhaseName);
        _host.Days = 11.0;

        driver.Tick(1f);

        Assert.AreEqual("ok", Value(Done(), "reason"));
    }

    [TestMethod]
    public void Tick_ElapsedDaysTurnsNaNWhileRunning_KeepsTheLastKnownDay()
    {
        var driver = ToRunning("stall_seconds=20\ndays=5\n");
        _host.Days = 11.2;
        driver.Tick(1f);
        _host.Days = double.NaN;

        Run(driver, 25);

        Assert.AreEqual("11", Value(Done(), "day_end"));
        Assert.AreEqual("1", Value(Done(), "days_run"));
    }

    [TestMethod]
    public void Tick_InquiryWhileWaitingForTheMap_IsHidden()
    {
        var driver = Driver();
        _host.OnStart = () => _host.ScreenValue = AutorunScreen.CharacterCreation;
        Run(driver, 5);
        Assert.AreEqual("WaitMap", driver.PhaseName);
        _host.ScreenValue = AutorunScreen.Other;
        _host.InquiryTitle = "Join a kingdom?";

        driver.Tick(1f);

        Assert.AreEqual(1, _host.InquiriesHidden);
    }

    [TestMethod]
    public void Finish_DoneTextThrows_StillReportsAndEndsTheCampaign()
    {
        // A host that breaks its contract (PrepareMap returns null) makes the done text throw while it is built.
        _host.Prepared = null!;
        var driver = ToRunning("days=1\n");
        _host.Days = 11.0;

        driver.Tick(1f);

        _outcomes.Received(1).Pass(ScenarioOutcomes.DoneId, "", Arg.Any<string>());
        Assert.AreEqual(1, _host.Ends);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("could not write " + ScenarioAutorunDone.DoneFile)));
    }

    [TestMethod]
    public void GameEnded_ArmedDriver_RestoresTheFocusSetting()
    {
        var driver = ToRunning();

        driver.GameEnded();

        Assert.AreEqual(1, _host.Restores);
    }

    [TestMethod]
    public void GameEnded_RestoreThrows_IsLoggedAndDoesNotEscape()
    {
        var driver = ToRunning();
        _host.RestoreThrows = true;

        driver.GameEnded();

        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("restore the focus setting")));
    }

    [TestMethod]
    public void Finish_RestoreThrows_LogsItAndTheNextStepsStillRun()
    {
        var driver = ToRunning("days=1\nquit=1\n");
        _host.RestoreThrows = true;
        _host.Days = 11.0;

        driver.Tick(1f);

        Assert.AreEqual("ok", Value(Done(), "reason"));
        Assert.AreEqual(1, _host.Ends);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("InvalidOperationException")));
        _outcomes.Received(1).Pass(ScenarioOutcomes.DoneId, "", Arg.Any<string>());
    }

    [TestMethod]
    public void Finish_WriteThrows_LogsItAndStillEndsTheCampaignAndReports()
    {
        var driver = ToRunning("days=1\n");
        _host.WriteThrows = true;
        _host.Days = 11.0;

        driver.Tick(1f);

        Assert.AreEqual(1, _host.Ends);
        _outcomes.Received(1).Pass(ScenarioOutcomes.DoneId, "", Arg.Any<string>());
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("could not write")));
    }

    [TestMethod]
    public void Finish_Ok_WritesAPassLineWithTheDetail()
    {
        var driver = ToRunning("days=2\nculture=gondor\n");
        _host.Days = 12.0;

        driver.Tick(1f);

        _outcomes.Received(1).Pass("taom.autorun.campaign-new.done", "", "reason=ok days_run=2 days_wanted=2 culture=gondor");
        _outcomes.DidNotReceive().Fail(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void Finish_AnyOtherReason_WritesAFailLine()
    {
        _host.StartResult = false;
        var driver = Driver();

        Run(driver, 6);

        _outcomes.Received(1).Fail("taom.autorun.campaign-new.done", "", "reason=start_refused days_run=0 days_wanted=3 culture=gondor");
        _outcomes.DidNotReceive().Pass(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void Finish_EarlyFailure_RestoresFocusAndEndsTheCampaign()
    {
        _host.StartResult = false;

        Run(Driver(), 6);

        Assert.AreEqual(1, _host.Restores);
        Assert.AreEqual(1, _host.Ends);
        Assert.AreEqual("-1", Value(Done(), "day_start"));
        Assert.AreEqual("0", Value(Done(), "days_run"));
    }

    [TestMethod]
    public void Tick_AfterDone_DoesNothingMore()
    {
        var driver = ToRunning("days=1\n");
        _host.Days = 11.0;
        driver.Tick(1f);
        Run(driver, 200);
        var writes = _host.Order.Count;

        Run(driver, 50);

        Assert.AreEqual(writes, _host.Order.Count);
        Assert.IsTrue(driver.IsDone);
    }

    [TestMethod]
    public void Tick_EveryFiveSeconds_WritesTheStatusFile()
    {
        _host.ScreenValue = AutorunScreen.Other;
        var driver = Driver();
        Run(driver, 4);
        Assert.IsFalse(_host.Files.ContainsKey(ScenarioAutorunDone.StatusFile));

        driver.Tick(1f);

        var status = _host.Files[ScenarioAutorunDone.StatusFile];
        Assert.AreEqual("WaitMenu", Value(status, "phase"));
        Assert.AreEqual("5.0", Value(status, "wall"));
        Assert.AreEqual("Other", Value(status, "screen"));
        Assert.AreEqual("0", Value(status, "time_locked"));
    }

    [TestMethod]
    public void Tick_HostThrows_FinishesDriverErrorInsteadOfEscapingToTheEngine()
    {
        var driver = Driver();
        _host.ScreenThrows = true;

        driver.Tick(1f);
        _host.ScreenThrows = false;

        Assert.AreEqual("driver_error", Value(Done(), "reason"));
        Assert.AreEqual(1, _host.Ends);
    }
}
