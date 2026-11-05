using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;
using TAOM.Tests.Features.SeatOfSeeing;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>The held line's 3 Hz pass in the driver (build-spec 9.2): its cadence, the slot proof and the strike check, over fake adapters.</summary>
[TestClass]
public class MarshalHoldLinePassTests
{
    private const int Company = 3;

    private FakeMarshalOrders _orders = null!;
    private FakeMarshalFire _fire = null!;
    private RecordingOutcomeLog _log = null!;
    private ListLogger _logger = null!;
    private MarshalFireDriver _driver = null!;

    [TestInitialize]
    public void Setup()
    {
        MarshalFireTable.Begin();
        _orders = new FakeMarshalOrders();
        _fire = new FakeMarshalFire();
        _log = new RecordingOutcomeLog();
        _logger = new ListLogger();
        _driver = new MarshalFireDriver(_orders, _fire, _log, _logger, _ => { }, "7");
        _orders.Selected.Add(Company);
    }

    [TestCleanup]
    public void Cleanup() => MarshalFireTable.Clear();

    private void Pass(double now)
    {
        _orders.Now = now;
        _driver.Pass();
    }

    private void GoHeld()
    {
        _driver.PressHold();
        Pass(0.0);
    }

    private static HoldCensus Line(string order = "Stop", int qualified = 20, int anchored = 20, int far = 0, int wounded = 5, int struckSettled = 0, int struckFlagOn = 0,
        int closer = 0, int farther = 0, int steered = 0, int loose = 0, int? flagOff = null) =>
        new(Company, 20, qualified, flagOff ?? qualified, anchored, far, wounded, struckSettled, struckSettled, struckFlagOn, 0.4f, 0.1f, order, "", closer, farther, steered, loose);

    private int SlotLines => _logger.Lines.Count(l => l.StartsWith("[Marshal] slot company=3 "));

    [TestMethod]
    public void Pass_NoCompanyIsHeld_NeverAsksTheAdapterToReadTheLine()
    {
        Pass(0.0);
        Pass(1.0);

        Assert.AreEqual(0, _fire.HoldPasses.Count);
    }

    [TestMethod]
    public void Pass_AHeldCompany_ReadsTheLineThreeTimesASecondAndNotMore()
    {
        GoHeld();
        var first = _fire.HoldPasses.Count;

        Pass(0.1);
        Pass(0.3);

        Assert.AreEqual(first, _fire.HoldPasses.Count, "inside the cadence");
        Pass(0.34);
        Assert.AreEqual(first + 1, _fire.HoldPasses.Count);
        CollectionAssert.AreEqual(new[] { Company }, _fire.HoldPasses.Last());
    }

    [TestMethod]
    public void Pass_ALineStruckAndSteeredCloserUnderStop_PassesTheSlotCheck_AndWritesTheSlotLineWithTheOrderBesideTheDistance()
    {
        GoHeld();
        _fire.HoldCensuses[Company] = Line(far: 2, steered: 8, closer: 6, farther: 2);

        Pass(1.0);

        var line = _log.Lines.Single(l => l.Id == MarshalChecks.HoldSlotId);
        Assert.AreEqual("pass", line.Result);
        Assert.IsTrue(line.Detail.Contains("order=Stop"), line.Detail);
        Assert.IsTrue(_logger.Lines.Any(l => l.StartsWith("[Marshal] slot company=3 ") && l.Contains(" order=Stop ") && l.Contains(" far=2 ")), string.Join("\n", _logger.Lines));
    }

    [TestMethod]
    public void Pass_AWindowWhereMoreSteeredSoldiersEndedFartherThanCloser_FailsTheSlotCheck_AndLatchesTheFailure()
    {
        GoHeld();
        _fire.HoldCensuses[Company] = Line(far: 2, steered: 8, closer: 2, farther: 6);

        Pass(1.0);

        Assert.AreEqual("fail", _log.Lines.Single(l => l.Id == MarshalChecks.HoldSlotId).Result);
        Assert.IsTrue(_driver.SlotFailed(Company));
    }

    [TestMethod]
    public void Pass_ManyFarSoldiersWithNoSteeredRead_WritesNoVerdict_SoACrowdThatPushedThemIsNotTheClamp()
    {
        GoHeld();
        _fire.HoldCensuses[Company] = Line(far: 9, loose: 9);

        Pass(1.0);

        Assert.AreEqual(0, _log.Lines.Count(l => l.Id == MarshalChecks.HoldSlotId));
        Assert.IsFalse(_driver.SlotFailed(Company));
        Assert.IsTrue(_logger.Lines.Any(l => l.StartsWith("[Marshal] slot company=3 ") && l.Contains(" far=9 ")), "far stays on the line as a fact");
    }

    [TestMethod]
    public void Pass_FewerThanFiveSteeredReadsInAWindow_IsPending_AndTheReadsOfSeveralPassesAddUp()
    {
        GoHeld();
        _fire.HoldCensuses[Company] = Line(steered: 2, closer: 2);

        Pass(1.0);
        Assert.AreEqual(0, _log.Lines.Count(l => l.Id == MarshalChecks.HoldSlotId), "two steered reads are too few");
        Pass(1.4);
        Pass(1.8);
        Pass(6.1);

        var lines = _log.Lines.Where(l => l.Id == MarshalChecks.HoldSlotId).ToList();
        Assert.AreEqual(1, lines.Count, "the second window holds three passes of two");
        Assert.AreEqual("pass", lines[0].Result);
    }

    [TestMethod]
    public void SlotFailed_ACompanyWhoseClampWalkedTheWrongWay_ReadsTrue_UntilTheHoldGoesOnAgain()
    {
        GoHeld();
        _fire.HoldCensuses[Company] = Line(steered: 8, closer: 0, farther: 8);
        Pass(1.0);
        Assert.IsTrue(_driver.SlotFailed(Company));
        Assert.IsFalse(_driver.SlotFailed(Company + 1), "another company is its own");

        _driver.PressHold();
        Pass(1.4);
        _driver.PressHold();
        Pass(1.8);

        Assert.IsFalse(_driver.SlotFailed(Company), "a new hold starts without the old failure");
    }

    [TestMethod]
    public void Pass_ALineNobodyPressed_WritesTheSlotLineButNoVerdict_SoAZeroUnderStopIsNotAPass()
    {
        GoHeld();
        _fire.HoldCensuses[Company] = Line(wounded: 0);

        Pass(1.0);

        Assert.AreEqual(0, _log.Lines.Count(l => l.Id == MarshalChecks.HoldSlotId));
        Assert.IsTrue(_logger.Lines.Any(l => l.StartsWith("[Marshal] slot company=3 ")));
    }

    [TestMethod]
    public void Pass_TheSlotLine_IsWrittenEveryFiveSecondsAndNotEveryRead()
    {
        GoHeld();
        _fire.HoldCensuses[Company] = Line();

        for (var t = 1.0; t < 5.5; t += 0.4)
            Pass(t);

        Assert.AreEqual(1, _logger.Lines.Count(l => l.StartsWith("[Marshal] slot company=3 ")));
        Pass(6.5);
        Assert.AreEqual(2, _logger.Lines.Count(l => l.StartsWith("[Marshal] slot company=3 ")));
    }

    [TestMethod]
    public void Pass_TheSlotLine_NamesTheSteeredSoldiersWhoEndedCloser_SummedOverItsWindow()
    {
        GoHeld();
        _fire.HoldCensuses[Company] = Line(closer: 2, farther: 0, steered: 3, loose: 1);

        Pass(1.0);
        Pass(1.4);
        Pass(1.8);
        Pass(6.1);

        var lines = _logger.Lines.Where(l => l.StartsWith("[Marshal] slot company=3 ")).ToList();
        Assert.AreEqual(2, lines.Count);
        Assert.IsTrue(lines[0].EndsWith(" closer=2 farther=0 steered=3 loose=1"), lines[0]);
        Assert.IsTrue(lines[1].EndsWith(" closer=6 farther=0 steered=9 loose=3"), "the second window holds the passes after the first line: " + lines[1]);
    }

    [TestMethod]
    public void Pass_TheSlotLine_NamesTheSteeredSoldiersWhoEndedFarther_SummedOverItsWindow()
    {
        GoHeld();
        _fire.HoldCensuses[Company] = Line(closer: 0, farther: 3, steered: 3);

        Pass(1.0);
        Pass(1.4);
        Pass(6.1);

        var lines = _logger.Lines.Where(l => l.StartsWith("[Marshal] slot company=3 ")).ToList();
        Assert.IsTrue(lines[0].EndsWith(" closer=0 farther=3 steered=3 loose=0"), lines[0]);
        Assert.IsTrue(lines[1].EndsWith(" closer=0 farther=6 steered=6 loose=0"), lines[1]);
    }

    [TestMethod]
    public void SlotCloserOf_AndSlotFartherOf_AreTheTotalsSinceTheHoldWentOn_NotJustTheLatestWindow()
    {
        GoHeld();
        _fire.HoldCensuses[Company] = Line(closer: 2, farther: 1, steered: 4);

        Pass(1.0);
        Pass(1.4);
        Pass(6.1);

        Assert.AreEqual(6, _driver.SlotCloserOf(Company));
        Assert.AreEqual(3, _driver.SlotFartherOf(Company));
        Assert.AreEqual(12, _driver.SlotSteeredOf(Company), "the denominator the trial reads beside the two sides");
        Assert.AreEqual(0, _driver.SlotCloserOf(Company + 1), "another company has none");
    }

    [TestMethod]
    public void SlotCloserOf_TheHoldToggledOffAndOnAgain_StartsFromZero()
    {
        GoHeld();
        _fire.HoldCensuses[Company] = Line(closer: 2, farther: 1);
        Pass(1.0);
        _driver.PressHold();
        Pass(1.4);
        Assert.AreEqual(0, _driver.SlotCloserOf(Company), "off drops the run");

        _driver.PressHold();
        Pass(1.8);

        Assert.AreEqual(2, _driver.SlotCloserOf(Company), "only the pass after the second press counts");
    }

    [TestMethod]
    public void Pass_AWindowOfSteeredSoldiers_IsWrittenOnTheSlotLineWhateverTheVerdictSays()
    {
        GoHeld();
        _fire.HoldCensuses[Company] = Line(far: 9, closer: 1, farther: 4, steered: 6);

        Pass(1.0);

        Assert.AreEqual("fail", _log.Lines.Single(l => l.Id == MarshalChecks.HoldSlotId).Result);
        Assert.IsTrue(_logger.Lines.Single(l => l.StartsWith("[Marshal] slot company=3 ")).EndsWith(" closer=1 farther=4 steered=6 loose=0"));
    }

    [TestMethod]
    public void Pass_ALineWoundedOnceAndThenNoLongerInTheCount_StillJudgesTheSlot_BecauseWoundedEverLatches()
    {
        GoHeld();
        _fire.HoldCensuses[Company] = Line(wounded: 3, steered: 8, closer: 8);
        Pass(1.0);
        Assert.AreEqual("pass", _log.Lines.Single(l => l.Id == MarshalChecks.HoldSlotId).Result);

        _fire.HoldCensuses[Company] = Line(wounded: 0, steered: 8, closer: 0, farther: 8);
        Pass(6.1);

        var lines = _log.Lines.Where(l => l.Id == MarshalChecks.HoldSlotId).ToList();
        Assert.AreEqual(2, lines.Count, "the wounded left the census, and the line was still struck");
        Assert.AreEqual("fail", lines[1].Result);
    }

    [TestMethod]
    public void Pass_TheCensusOfALaterPass_CarriesTheLatchedWoundedCount()
    {
        GoHeld();
        _fire.HoldCensuses[Company] = Line(wounded: 3);
        Pass(1.0);
        _fire.HoldCensuses[Company] = Line(wounded: 1);
        Pass(1.4);

        Assert.AreEqual(3, _driver.HoldCensusOf(Company).WoundedEver, "the most the line has ever shown, so the step and the line read one value");
    }

    [TestMethod]
    public void Pass_AHoldTurnedOffAfterAWindowOfSteeredReads_WritesTheLastWindowWithNoVerdict()
    {
        GoHeld();
        _fire.HoldCensuses[Company] = Line(steered: 3, closer: 2, loose: 1);
        Pass(1.0);
        Pass(1.4);
        var before = SlotLines;
        var checks = _log.Lines.Count(l => l.Id == MarshalChecks.HoldSlotId);

        _driver.PressHold();
        Pass(1.8);

        Assert.AreEqual(before + 1, SlotLines);
        Assert.IsTrue(_logger.Lines.Last(l => l.StartsWith("[Marshal] slot company=3 ")).EndsWith(" closer=2 farther=0 steered=3 loose=1"), "the window since the line at 1.0 holds one pass");
        Assert.AreEqual(checks, _log.Lines.Count(l => l.Id == MarshalChecks.HoldSlotId), "the last window is a fact and not a verdict");
    }

    [TestMethod]
    public void Summarize_AHeldCompanyWithAnOpenWindow_WritesItsSlotLineAtTheFinish()
    {
        GoHeld();
        _fire.HoldCensuses[Company] = Line(steered: 3, closer: 3);
        Pass(1.0);
        Pass(1.4);
        var before = SlotLines;

        _driver.Summarize();

        Assert.AreEqual(before + 1, SlotLines);
        Assert.IsTrue(_logger.Lines.Last(l => l.StartsWith("[Marshal] slot company=3 ")).EndsWith(" closer=3 farther=0 steered=3 loose=0"));
    }

    [TestMethod]
    public void Pass_AHoldTurnedOffBeforeAnyPass_WritesNoSlotLine()
    {
        GoHeld();

        _driver.PressHold();
        Pass(0.1);

        Assert.AreEqual(0, SlotLines, "no census was ever read");
    }

    [TestMethod]
    public void Pass_AHoldTurnedOff_DropsItsLineCount_SoAStepThatSharesTheBattleReadsNoOldCensus()
    {
        GoHeld();
        _fire.HoldCensuses[Company] = Line(qualified: 17);
        Pass(1.0);
        Assert.AreEqual(17, _driver.HoldCensusOf(Company).Qualified);

        _driver.PressHold();
        Pass(1.4);

        Assert.AreEqual(0, _driver.HoldCensusOf(Company).Units);
    }

    [TestMethod]
    public void Pass_AStrikeProofWithNoSoldierReadingTheFlagOff_WritesNoStrikeLine_SoARowCannotPassOnAFlagNeverCleared()
    {
        GoHeld();
        _fire.HoldCensuses[Company] = Line(struckSettled: 3, struckFlagOn: 3, flagOff: 0);

        Pass(1.0);

        Assert.AreEqual(0, _log.Lines.Count(l => l.Id == MarshalChecks.StrikeId));
        Assert.IsFalse(_driver.StrikePassed(Company));
    }

    [TestMethod]
    public void Pass_AStrikeProofWithTheLineDisarmed_StillJudges_AtTheMinimumOfFlagsOff()
    {
        GoHeld();
        _fire.HoldCensuses[Company] = Line(struckSettled: 3, struckFlagOn: 3, flagOff: MarshalTuning.HoldMinFlagOff);

        Pass(1.0);

        Assert.AreEqual("pass", _log.Lines.Single(l => l.Id == MarshalChecks.StrikeId).Result);
    }

    [TestMethod]
    public void Pass_ACallbackThatFaulted_ActsAsHoldOff_SoNoPassReadsTheLineAndTheSweepIsToldNobodyIsHeld()
    {
        GoHeld();
        var reads = _fire.HoldPasses.Count;
        _fire.HasTouched = true;
        _fire.CallbackFaulted = true;

        Pass(1.0);

        Assert.AreEqual(reads, _fire.HoldPasses.Count, "no hold read once the callback is dead");
        CollectionAssert.AreEqual(new int[0], _fire.SweptHeld.Last(), "an empty held list lets the sweep give back every flag and order it took");
    }

    [TestMethod]
    public void Pass_ACallbackThatIsAlive_KeepsReadingTheLine_AndSweepsTheCompanyAsHeld()
    {
        GoHeld();
        var reads = _fire.HoldPasses.Count;
        _fire.HasTouched = true;

        Pass(1.0);

        Assert.IsTrue(_fire.HoldPasses.Count > reads);
        CollectionAssert.AreEqual(new[] { Company }, _fire.SweptHeld.Last());
    }

    [TestMethod]
    public void Pass_AWoundedSoldiersFlagIsOn_PassesTheStrikeCheckOnce()
    {
        GoHeld();
        _fire.HoldCensuses[Company] = Line(struckSettled: 3, struckFlagOn: 3);

        Pass(1.0);
        Pass(1.4);
        Pass(1.8);

        var lines = _log.Lines.Where(l => l.Id == MarshalChecks.StrikeId).ToList();
        Assert.AreEqual(1, lines.Count);
        Assert.AreEqual("pass", lines[0].Result);
        Assert.IsTrue(_driver.StrikePassed(Company));
    }

    [TestMethod]
    public void Pass_AWoundedSoldiersFlagStaysOff_FailsTheStrikeCheck()
    {
        GoHeld();
        _fire.HoldCensuses[Company] = Line(struckSettled: 3, struckFlagOn: 0);

        Pass(1.0);

        Assert.AreEqual("fail", _log.Lines.Single(l => l.Id == MarshalChecks.StrikeId).Result);
        Assert.IsFalse(_driver.StrikePassed(Company));
    }

    [TestMethod]
    public void Pass_NoWoundIsSettledYet_WritesNoStrikeLine()
    {
        GoHeld();
        _fire.HoldCensuses[Company] = Line(struckSettled: 0);

        Pass(1.0);

        Assert.AreEqual(0, _log.Lines.Count(l => l.Id == MarshalChecks.StrikeId));
    }

    [TestMethod]
    public void HoldCensusOf_AfterAPass_IsTheLatestCountOfThatCompany_AndEmptyBeforeOne()
    {
        Assert.AreEqual(0, _driver.HoldCensusOf(Company).Units);
        GoHeld();
        _fire.HoldCensuses[Company] = Line(qualified: 17);

        Pass(1.0);

        Assert.AreEqual(17, _driver.HoldCensusOf(Company).Qualified);
    }

    [TestMethod]
    public void Pass_AHeldCompanyThatGoesToTheAi_DropsItsLineCount()
    {
        GoHeld();
        _fire.HoldCensuses[Company] = Line();
        Pass(1.0);
        _orders.Lost.Add(Company);

        Pass(2.0);

        Assert.AreEqual(0, _driver.HoldCensusOf(Company).Units);
    }

    [TestMethod]
    public void OnPlayerTeamChanged_DropsTheLineCountsAndTheStrikeLatches()
    {
        GoHeld();
        _fire.HoldCensuses[Company] = Line(struckSettled: 2, struckFlagOn: 2);
        Pass(1.0);

        _driver.OnPlayerTeamChanged();

        Assert.IsFalse(_driver.StrikePassed(Company));
        Assert.AreEqual(0, _driver.HoldCensusOf(Company).Units);
    }
}
