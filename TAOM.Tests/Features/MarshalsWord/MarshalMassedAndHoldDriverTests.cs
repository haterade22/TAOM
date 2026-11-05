using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;
using TAOM.Tests.Features.SeatOfSeeing;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>Massed fire (build-spec 7.2) and the hold-the-line proof slice (build-spec 9.2) in the archers' driver, over fake adapters.</summary>
[TestClass]
public class MarshalMassedAndHoldDriverTests
{
    private const int Company = 3;
    private const int Control = 4;

    private FakeMarshalOrders _orders = null!;
    private FakeMarshalFire _fire = null!;
    private RecordingOutcomeLog _log = null!;
    private ListLogger _logger = null!;
    private System.Collections.Generic.List<MarshalMessage> _said = null!;
    private MarshalFireDriver _driver = null!;

    [TestInitialize]
    public void Setup()
    {
        MarshalFireTable.Begin();
        _orders = new FakeMarshalOrders();
        _fire = new FakeMarshalFire();
        _log = new RecordingOutcomeLog();
        _logger = new ListLogger();
        _said = new System.Collections.Generic.List<MarshalMessage>();
        _driver = new MarshalFireDriver(_orders, _fire, _log, _logger, _said.Add, "7");
        _orders.Selected.Add(Company);
    }

    [TestCleanup]
    public void Cleanup() => MarshalFireTable.Clear();

    private void Pass(double now)
    {
        _orders.Now = now;
        _driver.Pass();
    }

    private void GoMassed()
    {
        _driver.PressFire();
        Pass(0.0);
        _driver.PressFire();
        Pass(0.1);
        Assert.AreEqual(FireMode.Massed, MarshalFireTable.ModeOf(Company));
    }

    private void Census(int holding, int drawing = 0, int idle = 0, int aside = 0, int stale = 0, int flagOff = 0) =>
        _fire.Census[Company] = new FireCensus(Company, drawing, holding, idle, aside, stale, flagOff);

    private static void Blows(int company, int count)
    {
        for (var i = 0; i < count; i++)
            MarshalFireTable.CountBlow(company);
    }

    // ---- Massed ----

    [TestMethod]
    public void Pass_MassedAndEnoughOfTheEligibleHold_GivesTheWord_AndLogsItWithItsShare()
    {
        GoMassed();
        Census(holding: 21, idle: 9);

        Pass(3.0);

        Assert.AreEqual(3.0, MarshalFireTable.WordTimeOf(Company));
        Assert.IsTrue(_logger.Lines.Any(l => l == "[Marshal] word company=3 time=3.0 holding=21"));
        Assert.IsTrue(_logger.Lines.Any(l => l == "[Marshal] massed company=3 holding=21 eligible=30 stale=0 idle=9 aside=0"));
        Assert.AreEqual(1, _log.Calls("pass", MarshalChecks.WordId));
        Assert.AreEqual(1, _driver.MassedWords);
    }

    [TestMethod]
    public void Pass_MassedAndOneReadySoldierAmongThirty_GivesNoWord()
    {
        GoMassed();
        Census(holding: 1, idle: 29);

        Pass(3.0);

        Assert.IsTrue(double.IsNegativeInfinity(MarshalFireTable.WordTimeOf(Company)));
        Assert.AreEqual(0, _driver.MassedWords);
    }

    [TestMethod]
    public void Pass_MassedStaleDrawsAreNotEligible_SoTheShareReadsOnTheSoldiersWhoCanStillShoot()
    {
        GoMassed();
        Census(holding: 21, drawing: 10, idle: 3, stale: 6);

        Pass(3.0);

        Assert.AreEqual(3.0, MarshalFireTable.WordTimeOf(Company), "21 of 28 eligible is 0.75");
        Assert.IsTrue(_logger.Lines.Any(l => l.Contains("massed company=3 holding=21 eligible=28 stale=6")));
    }

    [TestMethod]
    public void Pass_MassedWithTheStaleDrawsCounted_WouldNotHaveGivenTheWord()
    {
        GoMassed();
        Census(holding: 21, drawing: 10, idle: 3, stale: 0);

        Pass(3.0);

        Assert.IsTrue(double.IsNegativeInfinity(MarshalFireTable.WordTimeOf(Company)), "21 of 34 is 0.62");
    }

    [TestMethod]
    public void Pass_MassedWithinTheWordGap_GivesNoSecondWord_AndAfterTheGapGivesIt()
    {
        GoMassed();
        Census(holding: 30);
        Pass(3.0);

        Pass(3.2);
        Assert.AreEqual(3.0, MarshalFireTable.WordTimeOf(Company));
        Pass(5.4);
        Assert.AreEqual(3.0, MarshalFireTable.WordTimeOf(Company), "2.4 s is under the gap");
        Pass(5.7);
        Assert.AreEqual(5.7, MarshalFireTable.WordTimeOf(Company));
        Assert.AreEqual(2, _driver.MassedWords);
    }

    [TestMethod]
    public void Pass_MassedCountsFiveTimesASecond_WhileTheSweepKeepsItsOneSecond()
    {
        GoMassed();
        Census(holding: 3, idle: 30);
        var countsBefore = _fire.CountedAt.Count;
        var sweepsBefore = _fire.Swept.Count;

        Pass(10.0);
        Pass(10.1);
        Pass(10.25);
        Pass(10.5);

        Assert.AreEqual(3, _fire.CountedAt.Count - countsBefore, "10.0, 10.25 and 10.5 are due, 10.1 is not");
        Assert.AreEqual(1, _fire.Swept.Count - sweepsBefore, "the full sweep runs once in this second");
    }

    [TestMethod]
    public void Pass_AnOnWordCompany_IsNeverCountedAndNeverGivenAWordByTheRule()
    {
        _driver.PressFire();
        Pass(0.0);
        Census(holding: 30);

        Pass(3.0);
        Pass(6.0);

        Assert.AreEqual(0, _fire.Counted.Count);
        Assert.IsTrue(double.IsNegativeInfinity(MarshalFireTable.WordTimeOf(Company)));
    }

    [TestMethod]
    public void Pass_AMassedWord_NamesItsNumbersAtOnce_WhileItsReleaseWindowIsStillOpen()
    {
        GoMassed();
        Census(holding: 21, idle: 9, stale: 2);

        Pass(3.0);

        Assert.AreEqual(1, _driver.MassedWords);
        Assert.AreEqual(new MassedWord(21, 28, 2, 3.0), _driver.LastMassed, "a counted word and its numbers come together");
        Assert.AreEqual(0, _driver.ReleasesJudged, "the window is open, nothing is judged yet");
    }

    [TestMethod]
    public void Pass_ASecondMassedWord_ReplacesTheNumbersOfTheFirst()
    {
        GoMassed();
        Census(holding: 21, idle: 9);
        Pass(3.0);
        Census(holding: 28, idle: 2);

        Pass(6.0);

        Assert.AreEqual(2, _driver.MassedWords);
        Assert.AreEqual(new MassedWord(28, 30, 0, 6.0), _driver.LastMassed);
    }

    [TestMethod]
    public void Pass_AMassedWordFollowedByAVolley_WritesThePassingMassedCheck()
    {
        GoMassed();
        Census(holding: 10, idle: 2);
        Pass(3.0);
        for (var i = 0; i < 9; i++)
            MarshalFireTable.Count(Company, ShotKind.AfterWord);

        Pass(4.5);

        var line = _log.Lines.Single(l => l.Id == MarshalChecks.MassedId);
        Assert.AreEqual("pass", line.Result);
        Assert.AreEqual("mission=7 company=3 holding=10 shots=9 eligible=12 stale=0", line.Detail);
        Assert.AreEqual(1, _log.Calls("pass", MarshalChecks.VolleyReleaseId));
    }

    [TestMethod]
    public void Pass_AMassedWordFollowedByNoVolley_WritesTheFailingMassedCheck()
    {
        GoMassed();
        Census(holding: 10, idle: 2);
        Pass(3.0);
        MarshalFireTable.Count(Company, ShotKind.AfterWord);

        Pass(4.5);

        var line = _log.Lines.Single(l => l.Id == MarshalChecks.MassedId);
        Assert.AreEqual("fail", line.Result);
        Assert.AreEqual(1, _log.Calls("fail", MarshalChecks.VolleyReleaseId));
    }

    [TestMethod]
    public void Pass_AManualWordFollowedByAVolley_WritesNoMassedCheck()
    {
        _driver.PressFire();
        Pass(0.0);
        Census(holding: 10);
        Pass(1.0);
        _driver.PressWord();
        Pass(2.0);
        for (var i = 0; i < 9; i++)
            MarshalFireTable.Count(Company, ShotKind.AfterWord);

        Pass(3.5);

        Assert.AreEqual(0, _log.Lines.Count(l => l.Id == MarshalChecks.MassedId));
        Assert.AreEqual(1, _log.Calls("pass", MarshalChecks.VolleyReleaseId));
    }

    [TestMethod]
    public void Pass_AMassedWordWithFewerThanFiveHolding_SkipsTheMassedCheckWithItsReason()
    {
        GoMassed();
        Census(holding: 3);

        Pass(3.0);

        var line = _log.Lines.Single(l => l.Id == MarshalChecks.MassedId);
        Assert.AreEqual("skip", line.Result);
        StringAssert.Contains(line.Detail, "reason=too-few-holding");
    }

    [TestMethod]
    public void Pass_AManualWordInsideAMassedWordsWindow_LeavesTheMassedVerdictAsItWas()
    {
        GoMassed();
        Census(holding: 10, idle: 2);
        Pass(3.0);
        _driver.PressWord();
        Pass(3.2);
        for (var i = 0; i < 9; i++)
            MarshalFireTable.Count(Company, ShotKind.AfterWord);

        Pass(4.5);

        var line = _log.Lines.Single(l => l.Id == MarshalChecks.MassedId);
        Assert.AreEqual("pass", line.Result);
        Assert.AreEqual(1, _log.Calls("pass", MarshalChecks.VolleyReleaseId), "the second word opened no second window");
    }

    [TestMethod]
    public void Pass_AMassedCompanyThatGoesToTheAi_LosesItsMode_AndGetsNoWord()
    {
        GoMassed();
        Census(holding: 30);
        _orders.Lost.Add(Company);

        Pass(3.0);

        Assert.AreEqual(FireMode.Free, MarshalFireTable.ModeOf(Company));
        Assert.IsTrue(double.IsNegativeInfinity(MarshalFireTable.WordTimeOf(Company)));
    }

    // ---- hold the line ----

    [TestMethod]
    public void Press_ASelectedCompany_TurnsHoldOn_SaysSo_WritesItsLine_AndSweepsItAsHeld()
    {
        _driver.HoldLine.Press();
        Pass(1.0);

        Assert.IsTrue(MarshalFireTable.HoldOf(Company));
        CollectionAssert.AreEqual(new[] { MarshalMessage.HoldOn }, _said);
        var line = _log.Lines.Single(l => l.Id == MarshalChecks.HoldId);
        Assert.AreEqual("pass", line.Result);
        Assert.AreEqual("mission=7 hold=on company=3", line.Detail);
        CollectionAssert.AreEqual(new[] { Company }, _fire.SweptHeld.Last());
    }

    [TestMethod]
    public void Press_Twice_TurnsHoldOff_AndTheNextSweepHoldsNobody()
    {
        _driver.HoldLine.Press();
        Pass(1.0);
        _driver.HoldLine.Press();
        Pass(2.0);

        Assert.IsFalse(MarshalFireTable.HoldOf(Company));
        CollectionAssert.AreEqual(new[] { MarshalMessage.HoldOn, MarshalMessage.HoldOff }, _said);
        Assert.AreEqual(0, _fire.SweptHeld.Last().Length);
    }

    [TestMethod]
    public void Press_WithoutCommand_DoesNothing()
    {
        _orders.PlayerCommands = false;

        _driver.HoldLine.Press();
        Pass(1.0);

        Assert.IsFalse(MarshalFireTable.HoldOf(Company));
        Assert.AreEqual(0, _said.Count);
    }

    [TestMethod]
    public void Press_ACompanyUnderAiControl_IsSkippedWithItsCompany()
    {
        _orders.Lost.Add(Company);

        _driver.HoldLine.Press();
        Pass(1.0);

        Assert.IsFalse(MarshalFireTable.HoldOf(Company));
        var line = _log.Lines.Single(l => l.Id == MarshalChecks.HoldId);
        Assert.AreEqual("skip", line.Result);
        StringAssert.Contains(line.Detail, "reason=ai-controlled company=3");
        Assert.AreEqual(0, _said.Count);
    }

    [TestMethod]
    public void Press_TwoSelectedCompaniesInTwoStates_BothTakeTheOppositeOfTheFirst()
    {
        _orders.Selected.Add(Control);
        MarshalFireTable.SetHold(Control, true);

        _driver.HoldLine.Press();
        Pass(1.0);

        Assert.IsTrue(MarshalFireTable.HoldOf(Company));
        Assert.IsTrue(MarshalFireTable.HoldOf(Control), "a mixed selection converges on the first company's change");
    }

    [TestMethod]
    public void Pass_AHeldCompanyThatGoesToTheAi_LosesItsHold_SoNoSoldierStaysDisarmedWithNobodyToPressTheKey()
    {
        _driver.HoldLine.Press();
        Pass(1.0);
        _orders.Lost.Add(Company);

        Pass(2.0);

        Assert.IsFalse(MarshalFireTable.HoldOf(Company));
        Assert.AreEqual(0, _fire.SweptHeld.Last().Length);
        Assert.IsTrue(_log.Lines.Any(l => l.Id == MarshalChecks.FireId && l.Result == "skip" && l.Detail.Contains("reason=lost-control company=3")));
    }

    [TestMethod]
    public void OnPlayerTeamChanged_ClearsEveryHold()
    {
        _driver.HoldLine.Press();
        Pass(1.0);

        _driver.OnPlayerTeamChanged();

        Assert.IsFalse(MarshalFireTable.HoldOf(Company));
    }

    [TestMethod]
    public void Pass_AHeldCompanyAgainstAControlThatFought_PassesTheHoldLineCheck_WithBothCounts()
    {
        StartAProof(flagOff: 30, controlBlowsBefore: 5);
        Pass(3.0);
        Blows(Control, 20);

        Pass(13.25);

        var line = _log.Lines.Single(l => l.Id == MarshalChecks.HoldLineId);
        Assert.AreEqual("pass", line.Result);
        StringAssert.Contains(line.Detail, "company=3 flagOff=30 blows=0 control=4 controlBlows=20 seconds=10.3");
        Assert.AreEqual(HoldProof.Pass, _driver.HoldLine.LastVerdict!.Value.Proof);
    }

    [TestMethod]
    public void Pass_AHeldCompanyThatStruckAsOftenAsTheControl_FailsTheHoldLineCheck()
    {
        StartAProof(flagOff: 30, controlBlowsBefore: 5);
        Pass(3.0);
        Blows(Control, 20);
        Blows(Company, 20);

        Pass(13.25);

        var line = _log.Lines.Single(l => l.Id == MarshalChecks.HoldLineId);
        Assert.AreEqual("fail", line.Result);
        StringAssert.Contains(line.Detail, "blows=20 control=4 controlBlows=20");
    }

    [TestMethod]
    public void Pass_AControlThatFoughtTooLittle_SkipsTheHoldLineCheckAsNoControl()
    {
        StartAProof(flagOff: 30, controlBlowsBefore: 5);
        Pass(3.0);
        Blows(Control, 3);

        Pass(13.25);

        var line = _log.Lines.Single(l => l.Id == MarshalChecks.HoldLineId);
        Assert.AreEqual("skip", line.Result);
        StringAssert.Contains(line.Detail, "reason=no-control");
    }

    [TestMethod]
    public void Pass_ASoldiersFlagThatReadsOnAtTheEnd_SkipsTheHoldLineCheckAsFlagNotOff()
    {
        StartAProof(flagOff: 30, controlBlowsBefore: 5);
        Pass(3.0);
        Blows(Control, 20);
        Census(0, idle: 40, flagOff: 2);

        Pass(13.25);

        var line = _log.Lines.Single(l => l.Id == MarshalChecks.HoldLineId);
        Assert.AreEqual("skip", line.Result);
        StringAssert.Contains(line.Detail, "reason=flag-not-off");
    }

    [TestMethod]
    public void Pass_NoOtherCompanyHasStruckYet_TakesNoBaseline_SoNoVerdictComesFromAnEmptyControl()
    {
        _driver.HoldLine.Press();
        Pass(0.0);
        Census(0, idle: 40, flagOff: 30);

        Pass(3.0);
        Pass(14.0);

        Assert.AreEqual(0, _log.Lines.Count(l => l.Id == MarshalChecks.HoldLineId));
    }

    [TestMethod]
    public void Pass_TheControlIsTheBusiestCompanyThatIsNotOnHold()
    {
        StartAProof(flagOff: 30, controlBlowsBefore: 2);
        Blows(5, 9);
        Pass(3.0);
        Blows(5, 20);

        Pass(13.25);

        StringAssert.Contains(_log.Lines.Single(l => l.Id == MarshalChecks.HoldLineId).Detail, "control=5 controlBlows=20");
    }

    [TestMethod]
    public void Pass_AControlIsNeverAnotherHeldCompany_HoweverMuchItStruck()
    {
        _orders.Selected.Add(Control);
        StartAProof(flagOff: 30, controlBlowsBefore: 0);
        Blows(Control, 90);
        Blows(5, 9);
        Pass(3.0);
        Blows(Control, 90);
        Blows(5, 20);

        Pass(13.25);

        StringAssert.Contains(_log.Lines.Single(l => l.Id == MarshalChecks.HoldLineId && l.Detail.Contains("company=3 ")).Detail, "control=5 controlBlows=20");
    }

    [TestMethod]
    public void Pass_TheControlGoesOnHoldInsideTheWindow_TheRunKeepsItAndSkipsAsNoControl_NotOnAnotherCompanysBlows()
    {
        StartAProof(flagOff: 30, controlBlowsBefore: 5);
        Pass(3.0);
        MarshalFireTable.SetHold(Control, true);
        Blows(5, 60);

        Pass(13.25);

        var line = _log.Lines.Single(l => l.Id == MarshalChecks.HoldLineId && l.Detail.Contains("company=3 "));
        Assert.AreEqual("skip", line.Result, "the old control dealt nothing after its baseline, and the busy company was never baselined");
        StringAssert.Contains(line.Detail, "control=4 controlBlows=0");
        StringAssert.Contains(line.Detail, "reason=no-control");
    }

    [TestMethod]
    public void Pass_TheControlStaysTheControl_WhileItIsNotOnHold_EvenWhenAnotherCompanyStrikesMoreInTheWindow()
    {
        StartAProof(flagOff: 30, controlBlowsBefore: 5);
        Pass(3.0);
        Blows(Control, 20);
        Blows(5, 90);

        Pass(13.25);

        StringAssert.Contains(_log.Lines.Single(l => l.Id == MarshalChecks.HoldLineId && l.Detail.Contains("company=3 ")).Detail, "control=4 controlBlows=20");
    }

    [TestMethod]
    public void Pass_AQuietControl_TheNextBaselineChoosesTheBusiestCompany()
    {
        StartAProof(flagOff: 30, controlBlowsBefore: 5);
        Pass(3.0);
        Blows(Control, 3);
        Pass(13.25);
        Assert.AreEqual("skip", _log.Lines.Single(l => l.Id == MarshalChecks.HoldLineId).Result, "the first control stayed quiet");

        Blows(5, 40);
        Pass(14.0);
        Blows(5, 20);
        Pass(24.5);

        var last = _log.Lines.Last(l => l.Id == MarshalChecks.HoldLineId);
        Assert.AreEqual("pass", last.Result);
        StringAssert.Contains(last.Detail, "control=5 controlBlows=20");
    }

    [TestMethod]
    public void Pass_AQuietControlThatStillLeadsOnTheMissionsBlows_IsNotChosenAgain_TheCompanyThatFoughtInTheWindowIs()
    {
        Blows(6, 40);
        StartAProof(flagOff: 30, controlBlowsBefore: 60);
        Pass(3.0);
        Blows(Control, 3);
        Blows(5, 15);
        Pass(13.25);
        Assert.AreEqual("skip", _log.Lines.Single(l => l.Id == MarshalChecks.HoldLineId).Result, "the first control stayed quiet");

        Pass(13.5);
        Blows(5, 15);
        Pass(24.0);

        var last = _log.Lines.Last(l => l.Id == MarshalChecks.HoldLineId);
        Assert.AreEqual("pass", last.Result);
        StringAssert.Contains(last.Detail, "control=5 controlBlows=15");
    }

    [TestMethod]
    public void Pass_AQuietControlAndNobodyElseFoughtInTheWindow_TheNextBaselineRanksTheMissionsBlowsAgain()
    {
        StartAProof(flagOff: 30, controlBlowsBefore: 5);
        Pass(3.0);
        Blows(Control, 3);
        Pass(13.25);

        Pass(13.5);
        Blows(Control, 20);
        Pass(24.0);

        var last = _log.Lines.Last(l => l.Id == MarshalChecks.HoldLineId);
        Assert.AreEqual("pass", last.Result);
        StringAssert.Contains(last.Detail, "control=4 controlBlows=20");
    }

    [TestMethod]
    public void Pass_AQuietControl_TheCompanyOfTheWindowIsNeverAnotherHeldCompany_HoweverMuchItFought()
    {
        _orders.Selected.Add(6);
        StartAProof(flagOff: 30, controlBlowsBefore: 5);
        Pass(3.0);
        Blows(Control, 3);
        Blows(6, 30);
        Blows(5, 12);
        Pass(13.25);

        Pass(13.5);
        Blows(5, 14);
        Pass(24.0);

        var last = _log.Lines.Last(l => l.Id == MarshalChecks.HoldLineId && l.Detail.Contains("company=3 "));
        Assert.AreEqual("pass", last.Result);
        StringAssert.Contains(last.Detail, "control=5 controlBlows=14");
    }

    [TestMethod]
    public void Pass_AHeldCompany_WritesItsSummaryEveryFiveSeconds_WithTheBlowsOfBothCompaniesSinceTheToggle()
    {
        Blows(Company, 4);
        StartAProof(flagOff: 30, controlBlowsBefore: 5);
        var count = _logger.Lines.Count(l => l.StartsWith("[Marshal] hold company=3 "));
        Assert.AreEqual(1, count, "the first line is written at the first pass");
        Blows(Company, 2);
        Blows(Control, 3);

        Pass(3.0);
        Assert.AreEqual(count, _logger.Lines.Count(l => l.StartsWith("[Marshal] hold company=3 ")), "inside the five seconds");
        Pass(5.25);

        Assert.AreEqual(count + 1, _logger.Lines.Count(l => l.StartsWith("[Marshal] hold company=3 ")));
        Assert.AreEqual("[Marshal] hold company=3 units=40 flagOff=30 blows=2 control=4 controlBlows=3 seconds=5.3", _logger.Lines.Last(l => l.StartsWith("[Marshal] hold company=3 ")));
    }

    [TestMethod]
    public void Summarize_AHeldCompany_WritesOneLastHoldLine_ForTheMissionsEnd()
    {
        _driver.HoldLine.Press();
        Pass(1.0);

        _driver.Summarize();

        Assert.IsTrue(_logger.Lines.Any(l => l.StartsWith("[Marshal] hold company=3 ")));
    }

    private void StartAProof(int flagOff, int controlBlowsBefore)
    {
        Blows(Control, controlBlowsBefore);
        Census(0, idle: 40 - flagOff, aside: 0, flagOff: flagOff);
        _fire.Census[Company] = new FireCensus(Company, 0, 0, 40, 0, 0, flagOff);
        _driver.HoldLine.Press();
        Pass(0.0);
    }
}
