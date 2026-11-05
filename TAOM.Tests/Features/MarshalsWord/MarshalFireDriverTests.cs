using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;
using TAOM.Tests.Features.SeatOfSeeing;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>The archers' driver (build-spec 7.2 and 7.3) over fake adapters: the two keys, the AI rule, the census cadence and the two judged volley checks.</summary>
[TestClass]
public class MarshalFireDriverTests
{
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
        _orders.Selected.Add(3);
    }

    [TestCleanup]
    public void Cleanup() => MarshalFireTable.Clear();

    private void Pass(double now)
    {
        _orders.Now = now;
        _driver.Pass();
    }

    private void Press(double now)
    {
        _driver.PressFire();
        Pass(now);
    }

    private void SetHolding(int holding) => _fire.Census[3] = new FireCensus(3, 0, holding);

    // ---- key 543 ----

    [TestMethod]
    public void PressFire_ASelectedCompany_GoesToOnWord_GivesFireAtWill_AndSaysSo()
    {
        Press(1.0);

        Assert.AreEqual(FireMode.OnWord, MarshalFireTable.ModeOf(3));
        Assert.AreEqual(1, _orders.Issued.Count);
        CollectionAssert.AreEqual(new[] { 3 }, _orders.Issued[0].Companies);
        CollectionAssert.AreEqual(new[] { MarshalMessage.FireOnWord }, _said);
        Assert.AreEqual(1, _log.Count("pass", MarshalChecks.FireId));
    }

    [TestMethod]
    public void PressFire_Twice_ReturnsTheCompanyToFree_WithNoOrder()
    {
        Press(1.0);
        Press(2.0);

        Assert.AreEqual(FireMode.Free, MarshalFireTable.ModeOf(3));
        Assert.AreEqual(1, _orders.Issued.Count, "only the change to OnWord gives fire at will");
        CollectionAssert.AreEqual(new[] { MarshalMessage.FireOnWord, MarshalMessage.FireFree }, _said);
    }

    [TestMethod]
    public void PressFire_ACompanyUnderAiControl_IsSkippedWithItsReason()
    {
        _orders.Lost.Add(3);

        Press(1.0);

        Assert.AreEqual(FireMode.Free, MarshalFireTable.ModeOf(3));
        Assert.AreEqual(0, _said.Count);
        StringAssert.Contains(_log.Lines.Single(l => l.Id == MarshalChecks.FireId && l.Result == "skip").Detail, "reason=ai-controlled");
    }

    [TestMethod]
    public void PressFire_TwoCompaniesUnderAiControl_EachWritesItsOwnSkipLine_SinceTheLogWritesOneLinePerKey()
    {
        _orders.Selected.Add(4);
        _orders.Lost.Add(3);
        _orders.Lost.Add(4);

        Press(1.0);

        var skips = _log.Lines.Where(l => l.Id == MarshalChecks.FireId && l.Result == "skip").ToList();
        Assert.AreEqual(2, skips.Count, "the second company's skip must not be lost to the first one's key");
        StringAssert.Contains(skips[0].Detail, "company=3");
        StringAssert.Contains(skips[1].Detail, "company=4");
    }

    [TestMethod]
    public void PressWord_TwoCompaniesOnFree_EachWritesItsOwnSkipLine()
    {
        _orders.Selected.Add(4);

        _driver.PressWord();
        Pass(1.0);

        var skips = _log.Lines.Where(l => l.Id == MarshalChecks.WordId && l.Result == "skip").ToList();
        Assert.AreEqual(2, skips.Count);
        StringAssert.Contains(skips[0].Detail, "reason=mode-free company=3");
        StringAssert.Contains(skips[1].Detail, "reason=mode-free company=4");
    }

    [TestMethod]
    public void PressWord_TwoCompaniesUnderAiControl_EachWritesItsOwnSkipLine()
    {
        _orders.Selected.Add(4);
        Press(1.0);
        MarshalFireTable.SetMode(4, FireMode.OnWord);
        _orders.Lost.Add(3);
        _orders.Lost.Add(4);

        _driver.PressWord();
        Pass(2.0);

        var skips = _log.Lines.Where(l => l.Id == MarshalChecks.WordId && l.Result == "skip").ToList();
        Assert.AreEqual(2, skips.Count);
        StringAssert.Contains(skips[0].Detail, "reason=ai-controlled company=3");
        StringAssert.Contains(skips[1].Detail, "reason=ai-controlled company=4");
    }

    [TestMethod]
    public void Pass_TwoCompaniesThatWentToTheAi_EachWritesItsOwnLostControlLine()
    {
        MarshalFireTable.SetMode(3, FireMode.OnWord);
        MarshalFireTable.SetMode(4, FireMode.OnWord);
        _orders.Lost.Add(3);
        _orders.Lost.Add(4);

        Pass(1.0);

        var skips = _log.Lines.Where(l => l.Id == MarshalChecks.FireId && l.Result == "skip").ToList();
        Assert.AreEqual(2, skips.Count);
        StringAssert.Contains(skips[0].Detail, "reason=lost-control company=3");
        StringAssert.Contains(skips[1].Detail, "reason=lost-control company=4");
    }

    [TestMethod]
    public void PressFire_WhenThePlayerCommandsNobody_DoesNothing()
    {
        _orders.PlayerCommands = false;

        Press(1.0);

        Assert.AreEqual(FireMode.Free, MarshalFireTable.ModeOf(3));
        Assert.AreEqual(0, _log.Lines.Count);
    }

    [TestMethod]
    public void PressFire_NothingSelected_ChangesNothing()
    {
        _orders.Selected.Clear();

        Press(1.0);

        Assert.AreEqual(0, _said.Count);
        Assert.AreEqual(0, _orders.Issued.Count);
    }

    [TestMethod]
    public void PressFire_TwoCompanies_EachWritesItsOwnPassLine_SinceTheLogWritesOneLinePerKey()
    {
        _orders.Selected.Add(4);

        Press(1.0);

        var passes = _log.Lines.Where(l => l.Id == MarshalChecks.FireId && l.Result == "pass").ToList();
        Assert.AreEqual(2, passes.Count, "the second company's change must not be lost to the first one's key");
        StringAssert.Contains(passes[0].Detail, "mode=OnWord company=3");
        StringAssert.Contains(passes[1].Detail, "mode=OnWord company=4");
    }

    [TestMethod]
    public void PressFire_TwoCompaniesInTwoModes_BothTakeTheNextModeOfTheFirst()
    {
        _orders.Selected.Add(4);
        MarshalFireTable.SetMode(4, FireMode.OnWord);

        Press(1.0);

        Assert.AreEqual(FireMode.OnWord, MarshalFireTable.ModeOf(3));
        Assert.AreEqual(FireMode.OnWord, MarshalFireTable.ModeOf(4));
    }

    // ---- key 544 ----

    [TestMethod]
    public void PressWord_ACompanyOnWord_GivesTheWord_LogsIt_AndSaysSo()
    {
        Press(1.0);
        SetHolding(6);
        Pass(2.5);

        _driver.PressWord();
        Pass(3.0);

        Assert.AreEqual(3.0, MarshalFireTable.WordTimeOf(3));
        Assert.IsTrue(_logger.Lines.Contains("[Marshal] word company=3 time=3.0 holding=6"), string.Join("\n", _logger.Lines));
        Assert.AreEqual(MarshalMessage.WordGiven, _said.Last());
        Assert.AreEqual(1, _log.Count("pass", MarshalChecks.WordId));
    }

    [TestMethod]
    public void PressWord_ACompanyOnFree_IsSkipped_AndNoWordIsGiven()
    {
        _driver.PressWord();
        Pass(1.0);

        Assert.IsTrue(double.IsNegativeInfinity(MarshalFireTable.WordTimeOf(3)));
        StringAssert.Contains(_log.Lines.Single(l => l.Id == MarshalChecks.WordId).Detail, "reason=mode-free");
        Assert.AreEqual(0, _said.Count);
    }

    [TestMethod]
    public void PressWord_ACompanyUnderAiControl_IsSkippedWithItsReason()
    {
        Press(1.0);
        _orders.Lost.Add(3);

        _driver.PressWord();
        Pass(2.0);

        Assert.IsTrue(_log.Lines.Any(l => l.Id == MarshalChecks.WordId && l.Result == "skip" && l.Detail.Contains("reason=ai-controlled")));
        Assert.IsTrue(double.IsNegativeInfinity(MarshalFireTable.WordTimeOf(3)));
    }

    // ---- the AI rule and the census ----

    [TestMethod]
    public void Pass_ACompanyThatWentToTheAi_LosesItsMode_AndTheSweepReleasesItsSoldiers()
    {
        Press(1.0);
        Pass(2.5);
        _orders.Lost.Add(3);

        Pass(4.0);

        Assert.AreEqual(FireMode.Free, MarshalFireTable.ModeOf(3));
        CollectionAssert.AreEqual(new int[0], _fire.Swept.Last());
        Assert.IsTrue(_log.Lines.Any(l => l.Id == MarshalChecks.FireId && l.Detail.Contains("reason=lost-control")));
    }

    [TestMethod]
    public void Pass_ACompanyWithAMode_IsSweptOncePerSecond_NotOnEveryQuarterSecondPass()
    {
        Press(1.0);
        for (var t = 1.25; t < 4.0; t += 0.25)
            Pass(t);

        Assert.AreEqual(3, _fire.Swept.Count, "passes at 1.0, 2.0 and 3.0 sweep; the quarter-second passes between do not");
    }

    [TestMethod]
    public void Pass_NoModeAndNothingTouched_NeverSweeps()
    {
        for (var t = 1.0; t < 4.0; t += 0.25)
            Pass(t);

        Assert.AreEqual(0, _fire.Swept.Count);
    }

    // ---- the proof log ----

    [TestMethod]
    public void Pass_FiveHoldingThroughFiveSecondsWithNoShots_PassesTheHoldCheck()
    {
        Press(1.0);
        SetHolding(6);
        for (var t = 1.25; t <= 8.0; t += 0.25)
            Pass(t);

        Assert.AreEqual(1, _log.Count("pass", MarshalChecks.VolleyHoldId));
        Assert.AreEqual(VolleyCheck.Hold, _driver.LastHold!.Value.Check);
        Assert.IsTrue(_driver.LastHold!.Value.Pass);
    }

    [TestMethod]
    public void Pass_SoldiersThatLooseWhileHolding_FailTheHoldCheck()
    {
        Press(1.0);
        SetHolding(10);
        Pass(2.0);
        MarshalFireTable.Count(3, ShotKind.WhileHolding);
        MarshalFireTable.Count(3, ShotKind.WhileHolding);
        for (var t = 2.25; t <= 8.0; t += 0.25)
            Pass(t);

        Assert.AreEqual(1, _log.Count("fail", MarshalChecks.VolleyHoldId));
        Assert.AreEqual(0, _log.Count("pass", MarshalChecks.VolleyHoldId));
    }

    [TestMethod]
    public void Pass_FewerThanFiveHolding_JudgesNoHoldWindow()
    {
        Press(1.0);
        SetHolding(4);
        for (var t = 1.25; t <= 12.0; t += 0.25)
            Pass(t);

        Assert.AreEqual(0, _log.Count("pass", MarshalChecks.VolleyHoldId) + _log.Count("fail", MarshalChecks.VolleyHoldId));
    }

    [TestMethod]
    public void PressWord_ThenMostHoldingLoose_PassesTheReleaseCheck()
    {
        Press(1.0);
        SetHolding(10);
        Pass(2.0);
        _driver.PressWord();
        Pass(2.25);
        for (var i = 0; i < 8; i++)
            MarshalFireTable.Count(3, ShotKind.AfterWord);
        for (var t = 2.5; t <= 4.0; t += 0.25)
            Pass(t);

        Assert.AreEqual(1, _log.Count("pass", MarshalChecks.VolleyReleaseId));
        Assert.AreEqual(8, _driver.LastRelease!.Value.Shots);
    }

    [TestMethod]
    public void PressWord_MeasuresTheHoldersAtTheWord_NotAtTheCensusUpToASecondBefore()
    {
        // The census said 5 holders; by the word the company holds 40. Four shots are 10 percent of 40, so the release check fails.
        Press(1.0);
        SetHolding(5);
        Pass(2.0);
        SetHolding(40);
        _driver.PressWord();
        Pass(2.5);
        for (var i = 0; i < 4; i++)
            MarshalFireTable.Count(3, ShotKind.AfterWord);
        for (var t = 2.75; t <= 4.5; t += 0.25)
            Pass(t);

        Assert.IsTrue(_logger.Lines.Contains("[Marshal] word company=3 time=2.5 holding=40"), string.Join("; ", _logger.Lines));
        Assert.AreEqual(1, _log.Count("fail", MarshalChecks.VolleyReleaseId));
        Assert.AreEqual(40, _driver.LastRelease!.Value.Holding);
    }

    [TestMethod]
    public void PressWord_ACompanyThatStoppedHolding_JudgesNoReleaseWindow()
    {
        Press(1.0);
        SetHolding(8);
        Pass(2.0);
        SetHolding(2);
        _driver.PressWord();
        Pass(2.5);
        for (var t = 2.75; t <= 4.5; t += 0.25)
            Pass(t);

        Assert.AreEqual(0, _log.Count("pass", MarshalChecks.VolleyReleaseId) + _log.Count("fail", MarshalChecks.VolleyReleaseId));
    }

    [TestMethod]
    public void PressWord_ThenTooFewLoose_FailsTheReleaseCheck()
    {
        Press(1.0);
        SetHolding(10);
        Pass(2.0);
        _driver.PressWord();
        Pass(2.25);
        MarshalFireTable.Count(3, ShotKind.AfterWord);
        for (var t = 2.5; t <= 4.0; t += 0.25)
            Pass(t);

        Assert.AreEqual(1, _log.Count("fail", MarshalChecks.VolleyReleaseId));
    }

    [TestMethod]
    public void PressFire_ChangedTwice_RestartsTheHoldWindow()
    {
        Press(1.0);
        SetHolding(6);
        for (var t = 1.25; t <= 3.0; t += 0.25)
            Pass(t);
        Press(3.25);
        Press(3.5);

        for (var t = 3.75; t <= 8.0; t += 0.25)
            Pass(t);

        Assert.AreEqual(0, _log.Count("pass", MarshalChecks.VolleyHoldId) + _log.Count("fail", MarshalChecks.VolleyHoldId), "no company holds a mode for 5 s since the second change");
    }

    [TestMethod]
    public void Pass_EveryFiveSeconds_WritesTheSummaryLine_WithTheCountersTheContractNames()
    {
        Press(1.0);
        SetHolding(6);
        MarshalFireTable.Count(3, ShotKind.Free);
        for (var t = 1.25; t <= 6.0; t += 0.25)
            Pass(t);

        var line = _logger.Lines.Last(l => l.StartsWith("[Marshal] fire ", System.StringComparison.Ordinal));

        StringAssert.Contains(line, "company=3 mode=OnWord drawing=0 holding=6 shotsWhileHolding=0 shotsAfterWord=0 shotsFree=1 shotsBeforeFirstWord=1");
        Assert.AreEqual(2, _logger.Lines.Count(l => l.StartsWith("[Marshal] fire ", System.StringComparison.Ordinal)), "one at the first pass and one five seconds later");
    }

    [TestMethod]
    public void Pass_TheSummaryLine_CarriesTheIdleAndTheAsideCounts_SoAShareOfTheCompanyAndTheReasonForALowOneCanBeRead()
    {
        // held is a window minimum, not a share: the idle count says how many of the company had not drawn at the census, and the aside count
        // how many of those stand aside by rule (no target within reach, a hold-fire order, no ranged weapon, and so on).
        _fire.Census[3] = new FireCensus(3, 2, 6, 27, 5);
        Press(1.0);

        var line = _logger.Lines.Last(l => l.StartsWith("[Marshal] fire ", System.StringComparison.Ordinal));

        StringAssert.Contains(line, "drawing=2 holding=6 ");
        StringAssert.Contains(line, " idle=27 aside=5 shotsAfterOther=0");
    }

    [TestMethod]
    public void PressWord_ShotsAfterAReleaseTheLimitCaused_AreNotTheWordsShots_SoTheyCannotPassTheReleaseCheck()
    {
        Press(1.0);
        SetHolding(10);
        Pass(2.0);
        _driver.PressWord();
        Pass(2.25);
        for (var i = 0; i < 8; i++)
            MarshalFireTable.Count(3, ShotKind.AfterOther);
        for (var t = 2.5; t <= 6.25; t += 0.25)
            Pass(t);

        Assert.AreEqual(1, _log.Count("fail", MarshalChecks.VolleyReleaseId), "eight shots, none of them the word's");
        Assert.AreEqual(0, _driver.LastRelease!.Value.Shots);
        var line = _logger.Lines.Last(l => l.StartsWith("[Marshal] fire ", System.StringComparison.Ordinal));
        StringAssert.Contains(line, "shotsAfterWord=0");
        StringAssert.Contains(line, "shotsAfterOther=8");
    }

    [TestMethod]
    public void Summarize_WritesOneLastSummaryForEachCompanyWithAMode()
    {
        Press(1.0);
        _logger.Lines.Clear();

        _driver.Summarize();

        Assert.AreEqual(1, _logger.Lines.Count(l => l.StartsWith("[Marshal] fire company=3 ", System.StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Summarize_ACompanyWipedOutJustBeforeTheEnd_StillGetsItsLine_FromWhatTheTableHolds()
    {
        // The finish can be the only evidence of a word given just before the battle ended, so it reads the table and does not ask who is still alive.
        Press(1.0);
        MarshalFireTable.SetMode(4, FireMode.OnWord);
        _orders.Lost.Add(3);
        _orders.Lost.Add(4);
        _logger.Lines.Clear();

        _driver.Summarize();

        Assert.AreEqual(1, _logger.Lines.Count(l => l.StartsWith("[Marshal] fire company=3 mode=OnWord ", System.StringComparison.Ordinal)), string.Join("\n", _logger.Lines));
        Assert.AreEqual(1, _logger.Lines.Count(l => l.StartsWith("[Marshal] fire company=4 mode=OnWord ", System.StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Summarize_WritesNothingForACompanyOnFree_AndNoSkipLine_AndLeavesTheModesAlone()
    {
        MarshalFireTable.SetMode(3, FireMode.OnWord);
        _orders.Lost.Add(3);
        _log.Lines.Clear();
        _logger.Lines.Clear();

        _driver.Summarize();

        Assert.AreEqual(1, _logger.Lines.Count, "company 3 only");
        Assert.AreEqual(0, _log.Lines.Count, "the finish writes no check line");
        Assert.AreEqual(FireMode.OnWord, MarshalFireTable.ModeOf(3), "the finish changes no mode; the hooks clear the table after it");
    }

    [TestMethod]
    public void Drop_ForgetsTheSoldiers_WithoutSweeping()
    {
        Press(1.0);
        var swept = _fire.Swept.Count;

        _driver.Drop();

        Assert.AreEqual(1, _fire.Dropped);
        Assert.AreEqual(swept, _fire.Swept.Count);
    }

    [TestMethod]
    public void OnPlayerTeamChanged_ClearsEveryMode()
    {
        Press(1.0);

        _driver.OnPlayerTeamChanged();

        Assert.AreEqual(FireMode.Free, MarshalFireTable.ModeOf(3));
    }
}
