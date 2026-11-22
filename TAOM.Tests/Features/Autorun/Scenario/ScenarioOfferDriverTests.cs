using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.Autorun.Scenario;
using TAOM.Features.StoryMissions.Domain;

namespace TAOM.Tests.Features.Autorun.Scenario;

/// <summary>The story-offer kind (#1105 slices 3 to 5): the real offer path, driven against the fake host.</summary>
[TestClass]
public class ScenarioOfferDriverTests
{
    private const string Open = "taom.chronicle_open hornburg confirm";
    private const string OpenedText = "[WarChronicle] Opened: 'hornburg' is Active until day 5.";
    private const string Defend = "taom_story_offer_defend";
    private const string Continue = "taom_story_offer_continue";

    private FakeCampaignAutorunHost _host = null!;
    private IModLogger _logger = null!;
    private IOutcomeLog _outcomes = null!;

    [TestInitialize]
    public void Init()
    {
        _host = new FakeCampaignAutorunHost { CommandResult = _ => OpenedText };
        _logger = Substitute.For<IModLogger>();
        _outcomes = Substitute.For<IOutcomeLog>();
    }

    private static string Head(string choice = "defend", string extra = "") =>
        "kind=story-offer\nout=E:\\x\\out\nevent=hornburg\nchoice=" + choice + "\ncommand=" + Open + "\nafter=taom.chronicle_status\nspeed=2\n" + extra;

    private ScenarioAutorunDriver Driver(string choice = "defend", string extra = "", string? text = null)
    {
        var plan = ScenarioPlan.Resolve(n => n == ScenarioPlan.EnvVar ? @"E:\arm.txt" : null, _ => true, _ => text ?? Head(choice, extra));
        Assert.IsTrue(plan.Armed, plan.Problem);
        return new ScenarioAutorunDriver(plan, _host, _logger, _outcomes);
    }

    private static void Run(ScenarioAutorunDriver driver, int ticks, float dt = 1f)
    {
        for (var i = 0; i < ticks; i++)
            driver.Tick(dt);
    }

    private string Done() => _host.Files[ScenarioAutorunDone.DoneFile];

    private static string Value(string text, string key) =>
        text.Split('\n').First(l => l.StartsWith(key + "=", StringComparison.Ordinal)).Substring(key.Length + 1);

    private int InfoLogs(string contains) =>
        _logger.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "LogInfo" && c.GetArguments()[0] is string s && s.Contains(contains));

    private int WarnLogs(string contains) =>
        _logger.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "LogWarning" && c.GetArguments()[0] is string s && s.Contains(contains));

    /// <summary>Menu, start, creation and a settled map: the open command ran and the driver waits for the popup.</summary>
    private ScenarioAutorunDriver ToOfferWait(string choice = "defend", string extra = "")
    {
        var driver = Driver(choice, extra);
        _host.OnStart = () => _host.ScreenValue = AutorunScreen.CharacterCreation;
        Run(driver, 5);
        _host.ScreenValue = AutorunScreen.Map;
        _host.Days = 10.4;
        Run(driver, 5);
        Assert.AreEqual("OfferWait", driver.PhaseName);
        return driver;
    }

    /// <summary>The popup was shown for the armed event and accepted: the menu has not shown yet.</summary>
    private ScenarioAutorunDriver ToOfferMenu(string choice = "defend", string extra = "")
    {
        var driver = ToOfferWait(choice, extra);
        _host.PendingOfferEventValue = "hornburg";
        _host.InquiryActiveValue = true;
        _host.InquiryTitle = "The Hornburg is besieged";
        Run(driver, 4);
        Assert.AreEqual("OfferMenu", driver.PhaseName);
        _host.InquiryActiveValue = false;
        _host.InquiryTitle = null;
        _host.ScreenValue = AutorunScreen.MapMenu;
        _host.OfferMenuShownValue = true;
        return driver;
    }

    private ScenarioAutorunDriver ToInMission(string choice = "defend")
    {
        var driver = ToOfferMenu(choice);
        Run(driver, 5);
        Assert.AreEqual("InMission", driver.PhaseName);
        return driver;
    }

    private ScenarioAutorunDriver ToOfferReturn()
    {
        var driver = ToInMission();
        _host.ScreenValue = AutorunScreen.Mission;
        Run(driver, 3);
        _host.Verdict = StoryOutcome.Won;
        _host.ScreenValue = AutorunScreen.MapMenu;
        driver.Tick(1f);
        Assert.AreEqual("OfferReturn", driver.PhaseName);
        return driver;
    }

    [TestMethod]
    public void Tick_SettledMap_RunsTheOpenCommandAndWaitsForThePopup()
    {
        ToOfferWait();

        CollectionAssert.AreEqual(new[] { Open }, _host.CommandsRun);
        Assert.AreEqual(0, _host.StoryMenuCalls);
        StringAssert.Contains(_host.Files[ScenarioAutorunDriver.CommandsFile], OpenedText);
    }

    [TestMethod]
    public void Tick_OpenCommandRefused_FinishesOfferRefusedAndLogsTheRefusal()
    {
        _host.CommandResult = _ => "Cheat mode is disabled!";
        var driver = Driver();
        _host.OnStart = () => _host.ScreenValue = AutorunScreen.CharacterCreation;
        Run(driver, 5);
        _host.ScreenValue = AutorunScreen.Map;
        _host.Days = 10.4;
        Run(driver, 5);

        Assert.AreEqual("offer_refused", Value(Done(), "reason"));
        Assert.AreEqual(1, WarnLogs("offer_refused: the open command did not open the window: Cheat mode is disabled!"));
        Assert.AreEqual(1, _host.Ends);
    }

    [DataTestMethod]
    [DataRow("[WarChronicle] Opened: 'breaking' Occurred.")]
    [DataRow("[WarChronicle] Opened: 'hornburg' is Active until day 5.")]
    public void Tick_OpenCommandAnsweredForNoWindowOfTheArmedEvent_FinishesOfferRefusedAtOnce(string answer)
    {
        _host.CommandResult = _ => answer;
        var driver = Driver("defend", "", Head().Replace("event=hornburg", "event=breaking"));
        _host.OnStart = () => _host.ScreenValue = AutorunScreen.CharacterCreation;
        Run(driver, 5);
        _host.ScreenValue = AutorunScreen.Map;
        _host.Days = 10.4;
        Run(driver, 5);

        Assert.AreEqual("offer_refused", Value(Done(), "reason"));
        Assert.AreEqual(1, WarnLogs("offer_refused: the open command did not open the window: " + answer));
    }

    [TestMethod]
    public void Tick_AcceptFindsNoMenu_FinishesOfferRefusedAtOnce()
    {
        _host.AcceptOpensMenu = false;
        var driver = ToOfferWait();
        _host.PendingOfferEventValue = "hornburg";
        _host.InquiryActiveValue = true;

        Run(driver, 4);

        Assert.AreEqual("offer_refused", Value(Done(), "reason"));
        Assert.AreEqual(1, WarnLogs("offer_refused: the accept found the map busy: no menu opened"));
        Assert.AreEqual(0, _host.OfferChoices.Count);
    }

    [TestMethod]
    public void Tick_NoCommandArmed_SkipsTheRefusalCheck()
    {
        var driver = Driver(text: "kind=story-offer\nout=E:\\x\\out\nevent=hornburg\nchoice=fate\n");
        _host.OnStart = () => _host.ScreenValue = AutorunScreen.CharacterCreation;
        Run(driver, 5);
        _host.ScreenValue = AutorunScreen.Map;
        _host.Days = 10.4;
        Run(driver, 5);

        Assert.AreEqual("OfferWait", driver.PhaseName);
    }

    [TestMethod]
    public void Tick_OfferWait_KeepsTheClockRunningAtThePlansSpeed()
    {
        var driver = ToOfferWait();

        Run(driver, 2);

        Assert.AreEqual(2f, _host.LastSpeed);
    }

    [TestMethod]
    public void Tick_OfferWait_HidesAnInquiryWhileNoOfferIsPending()
    {
        var driver = ToOfferWait();
        _host.InquiryTitle = "Some other popup";
        _host.InquiryActiveValue = true;

        Run(driver, 3);

        Assert.AreEqual(3, _host.InquiriesHidden);
        Assert.AreEqual(0, _host.Accepts);
    }

    [TestMethod]
    public void Tick_OfferWait_AnotherEventsOfferIsHiddenNotAccepted()
    {
        var driver = ToOfferWait();
        _host.PendingOfferEventValue = "breaking";
        _host.InquiryTitle = "Another window";
        _host.InquiryActiveValue = true;

        Run(driver, 4);

        Assert.AreEqual(0, _host.Accepts);
        Assert.IsTrue(_host.InquiriesHidden >= 1);
    }

    [TestMethod]
    public void Tick_OfferWait_TheArmedOffersPopupIsKeptOpenUntilItSettles()
    {
        var driver = ToOfferWait();
        _host.PendingOfferEventValue = "hornburg";
        _host.InquiryTitle = "The Hornburg is besieged";
        _host.InquiryActiveValue = true;

        Run(driver, 2);

        Assert.AreEqual(0, _host.InquiriesHidden);
        Assert.AreEqual(0, _host.Accepts);
        Assert.AreEqual("OfferWait", driver.PhaseName);
    }

    [TestMethod]
    public void Tick_OfferWait_ASettledPopupIsAcceptedOnceAndLogged()
    {
        var driver = ToOfferMenu();
        Run(driver, 2);

        Assert.AreEqual(1, _host.Accepts);
        Assert.AreEqual(0, _host.InquiriesHidden);
        Assert.AreEqual(1, InfoLogs("accepted the offer popup 'The Hornburg is besieged'"));
    }

    [TestMethod]
    public void Tick_OfferWait_PopupNeverShownFor120Seconds_FinishesOfferNotShownAndLogsTheCommandOutput()
    {
        var driver = ToOfferWait();

        Run(driver, 119);
        Assert.IsFalse(_host.Files.ContainsKey(ScenarioAutorunDone.DoneFile));
        Run(driver, 3);

        Assert.AreEqual("offer_not_shown", Value(Done(), "reason"));
        Assert.AreEqual(1, WarnLogs("offer_not_shown:"));
        Assert.AreEqual(1, WarnLogs(OpenedText));
    }

    [TestMethod]
    public void Tick_OfferWait_AcceptRefused_FinishesOfferRefused()
    {
        _host.AcceptResult = false;
        var driver = ToOfferWait();
        _host.PendingOfferEventValue = "hornburg";
        _host.InquiryActiveValue = true;

        Run(driver, 4);

        Assert.AreEqual("offer_refused", Value(Done(), "reason"));
        Assert.AreEqual(1, WarnLogs("offer_refused: the popup's accept button could not be pressed"));
    }

    [DataTestMethod]
    [DataRow("defend", "taom_story_offer_defend|taom_story_offer_stand", true)]
    [DataRow("attack", "taom_story_offer_attack|taom_story_offer_hunt", true)]
    [DataRow("fate", "taom_story_offer_decline", false)]
    public void Tick_OfferMenuSettled_ChoosesTheWantedOptionsOnce(string choice, string ids, bool bot)
    {
        _host.OfferedOptions = ids.Split('|').ToList();
        var driver = ToOfferMenu(choice);
        Run(driver, 4);
        Assert.AreEqual(0, _host.OfferChoices.Count);

        driver.Tick(1f);
        Run(driver, 2);

        CollectionAssert.AreEqual(new[] { (ids, bot) }, _host.OfferChoices.ToArray());
        Assert.AreEqual(choice == "fate" ? "OfferReturn" : "InMission", driver.PhaseName);
    }

    [TestMethod]
    public void Tick_OfferMenu_EventWindowWaiting_HoldsTheChoice()
    {
        var driver = ToOfferMenu();
        _host.EventWindowWaiting = true;

        Run(driver, 8);
        Assert.AreEqual(0, _host.OfferChoices.Count);

        _host.EventWindowWaiting = false;
        driver.Tick(1f);

        Assert.AreEqual(1, _host.OfferChoices.Count);
    }

    [TestMethod]
    public void Tick_OfferMenu_WantedOptionNotOffered_FinishesOfferRefusedAndLogsTheChoices()
    {
        _host.OfferedOptions = new() { "taom_story_offer_attack" };
        _host.PendingOfferChoicesValue = "Attack";
        _host.OpenMenuIdValue = "taom_story_offer";
        var driver = ToOfferMenu();

        Run(driver, 6);

        Assert.AreEqual("offer_refused", Value(Done(), "reason"));
        Assert.AreEqual(1, WarnLogs("offer_refused: no 'defend' option is offered, choices=Attack open_menu=taom_story_offer"));
    }

    [TestMethod]
    public void Tick_OfferMenuNeverSettles_FinishesOfferRefused()
    {
        var driver = ToOfferMenu();
        _host.OfferMenuShownValue = false;

        Run(driver, 122);

        Assert.AreEqual("offer_refused", Value(Done(), "reason"));
        Assert.AreEqual(0, _host.OfferChoices.Count);
    }

    [TestMethod]
    public void Tick_Fate_AfterTheClick_FinishesOkWithNoVerdictAndNoMissionOnceTheMapSettled()
    {
        _host.OfferedOptions = new() { "taom_story_offer_decline" };
        var driver = ToOfferMenu("fate");
        Run(driver, 5);
        Assert.AreEqual("OfferReturn", driver.PhaseName);
        _host.OfferMenuShownValue = false;
        _host.ScreenValue = AutorunScreen.Map;

        Run(driver, 4);
        Assert.IsFalse(_host.Files.ContainsKey(ScenarioAutorunDone.DoneFile));
        Run(driver, 3);

        var done = Done();
        Assert.AreEqual("ok", Value(done, "reason"));
        Assert.AreEqual("story-offer", Value(done, "kind"));
        Assert.AreEqual("hornburg", Value(done, "event"));
        Assert.AreEqual("fate", Value(done, "choice"));
        Assert.AreEqual("-", Value(done, "verdict"));
        CollectionAssert.AreEqual(new[] { Open, "taom.chronicle_status" }, _host.CommandsRun);
    }

    [TestMethod]
    public void Tick_Defend_TheMissionRunsWithTheBotAndTheVerdictComesFromTheOffer()
    {
        var driver = ToInMission();

        Assert.AreEqual(true, _host.OfferChoices.Single().Bot);
        _host.ScreenValue = AutorunScreen.Mission;
        Run(driver, 3);
        _host.Verdict = StoryOutcome.Lost;
        _host.ScreenValue = AutorunScreen.MapMenu;
        driver.Tick(1f);

        Assert.AreEqual("OfferReturn", driver.PhaseName);
        Assert.AreEqual(1, InfoLogs("mission ended verdict=Lost seconds=3.0"));
    }

    [TestMethod]
    public void Tick_Defend_NoMissionScreenIn120Seconds_FinishesMissionNotOpened()
    {
        var driver = ToInMission();

        Run(driver, 122);

        Assert.AreEqual("mission_not_opened", Value(Done(), "reason"));
    }

    [TestMethod]
    public void Tick_Defend_MissionEndsUndecided_FinishesMissionUndecided()
    {
        var driver = ToInMission();
        _host.ScreenValue = AutorunScreen.Mission;
        driver.Tick(1f);
        _host.Verdict = StoryOutcome.Undecided;
        _host.ScreenValue = AutorunScreen.MapMenu;
        driver.Tick(1f);

        Assert.AreEqual("mission_undecided", Value(Done(), "reason"));
        Assert.AreEqual("Undecided", Value(Done(), "verdict"));
    }

    [TestMethod]
    public void Tick_OfferReturn_ClicksContinueOnceAfterTheMenuSettledAndNeverBackToTheMap()
    {
        var driver = ToOfferReturn();
        _host.OfferMenuShownValue = true;
        _host.OfferChoices.Clear();

        Run(driver, 4);
        Assert.AreEqual(0, _host.OfferChoices.Count);

        driver.Tick(1f);
        _host.OfferMenuShownValue = false;
        _host.ScreenValue = AutorunScreen.Map;
        Run(driver, 3);

        CollectionAssert.AreEqual(new[] { (Continue, false) }, _host.OfferChoices.ToArray());
        Assert.AreEqual(0, _host.BackChoices);
        Assert.IsFalse(_host.Files.ContainsKey(ScenarioAutorunDone.DoneFile));
    }

    [TestMethod]
    public void Tick_OfferReturn_ContinueNotOffered_FinishesReturnFailed()
    {
        _host.OfferedOptions = new() { Defend };
        var driver = ToOfferReturn();
        _host.OfferMenuShownValue = true;

        Run(driver, 6);

        Assert.AreEqual("return_failed", Value(Done(), "reason"));
    }

    [TestMethod]
    public void Tick_OfferReturnNeverSettles_FinishesReturnFailed()
    {
        var driver = ToOfferReturn();
        _host.OfferMenuShownValue = false;
        _host.ScreenValue = AutorunScreen.Other;

        Run(driver, 122);

        Assert.AreEqual("return_failed", Value(Done(), "reason"));
    }

    [TestMethod]
    public void Tick_FullDefend_FinishesOkWithTheOfferDoneLineAndTheAfterCommandOutput()
    {
        _host.CommandResult = c => c == "taom.chronicle_status" ? "hornburg Held" : OpenedText;
        var driver = ToOfferReturn();
        _host.OfferMenuShownValue = true;
        Run(driver, 5);
        _host.OfferMenuShownValue = false;
        _host.ScreenValue = AutorunScreen.Map;
        _host.EventWindowOpen = true;
        Run(driver, 7);

        var done = Done();
        Assert.AreEqual("ok", Value(done, "reason"));
        Assert.AreEqual("story-offer", Value(done, "kind"));
        Assert.AreEqual("hornburg", Value(done, "event"));
        Assert.AreEqual("defend", Value(done, "choice"));
        Assert.AreEqual("Won", Value(done, "verdict"));
        Assert.AreEqual("3.0", Value(done, "mission_seconds"));
        Assert.IsFalse(done.Contains("days_run=") || done.Contains("mission=hornburg"));
        Assert.AreEqual(1, _host.EventWindowsClosed);
        CollectionAssert.AreEqual(new[] { Open, "taom.chronicle_status" }, _host.CommandsRun);
        var commands = _host.Files[ScenarioAutorunDriver.CommandsFile];
        StringAssert.Contains(commands, "> " + Open);
        StringAssert.Contains(commands, "> taom.chronicle_status\nhornburg Held");
        _outcomes.Received(1).Pass(ScenarioOutcomes.StoryOfferDoneId, "",
            Arg.Is<string>(s => s.StartsWith("reason=ok event=hornburg choice=defend verdict=Won mission_seconds=3.0")));
        _outcomes.DidNotReceive().Fail(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        Assert.AreEqual(1, InfoLogs("[Autorun][Scenario] done reason=ok scenario=- event=hornburg choice=defend verdict=Won"));
    }

    [TestMethod]
    public void Tick_FullDefend_TheAfterCommandsRunOnceAndAfterTheMapSettled()
    {
        var driver = ToOfferReturn();
        _host.OfferMenuShownValue = true;
        Run(driver, 5);
        _host.OfferMenuShownValue = false;
        _host.ScreenValue = AutorunScreen.Map;
        Run(driver, 4);

        CollectionAssert.AreEqual(new[] { Open }, _host.CommandsRun);
    }

    [TestMethod]
    public void Tick_StoryOfferKind_NeverCallsTheDevMenuMembers()
    {
        var driver = ToInMission();
        _host.ScreenValue = AutorunScreen.Mission;
        Run(driver, 2);

        Assert.AreEqual(0, _host.StoryMenuCalls);
        Assert.AreEqual(0, _host.OpenChoices);
        Assert.AreEqual(0, _host.BackChoices);
    }

    [TestMethod]
    public void Tick_StoryMissionKind_NeverCallsAnOfferMember()
    {
        var driver = Driver(text: "kind=story-mission\nout=E:\\x\\out\nmission=helms_deep_defend\n");
        _host.OnStart = () => _host.ScreenValue = AutorunScreen.CharacterCreation;
        Run(driver, 5);
        _host.ScreenValue = AutorunScreen.Map;
        _host.Days = 10.4;
        Run(driver, 12);

        Assert.AreEqual(0, _host.Accepts);
        Assert.AreEqual(0, _host.OfferChoices.Count);
    }
}
