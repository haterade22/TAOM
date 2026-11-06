using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;
using TAOM.Tests.Features.LoadTimeStamps;
using TAOM.Tests.Features.SeatOfSeeing;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>The thrower driver (build-spec 9.4): when a run starts and ends, what it publishes, and each line and check it writes.</summary>
[TestClass]
public class MarshalThrowerDriverTests
{
    private FakeMarshalThrowers _engine = null!;
    private RecordingOutcomeLog _log = null!;
    private TAOM.Tests.Features.LoadTimeStamps.RecordingLogger _logger = null!;
    private MarshalThrowerDriver _driver = null!;

    [TestInitialize]
    public void Setup()
    {
        MarshalHooks.Begin(Substitute.For<IMarshalHookSink>());
        MarshalHooks.Clear();
        MarshalFireTable.Begin();
        MarshalThrowerCost.Reset();
        _engine = new FakeMarshalThrowers();
        _log = new RecordingOutcomeLog();
        _logger = new TAOM.Tests.Features.LoadTimeStamps.RecordingLogger();
        _driver = new MarshalThrowerDriver(_engine, _log, _logger, "7");
    }

    [TestCleanup]
    public void Cleanup()
    {
        MarshalFireTable.Clear();
        MarshalHooks.Begin(Substitute.For<IMarshalHookSink>());
        MarshalHooks.Clear();
    }

    private void Pass(double now, bool allowed = true)
    {
        _engine.Now = now;
        _driver.Pass(allowed);
    }

    private string[] Checks(string id) => _log.Lines.Where(l => l.Id == id).Select(l => l.Result + " " + l.Detail).ToArray();

    private ThrowerFacts Facts(int company = 1) => _driver.FactsOf(company)!.Value;

    private void StandAt(double now = 1.0)
    {
        _engine.Standard(1);
        Pass(0.0);
        _engine.MoveTo(1, 65f);
        Pass(now - 0.25);
        Pass(now);
        Assert.AreEqual(ThrowerState.Stand, Facts().State);
    }

    [TestMethod]
    public void Pass_ACompanyOnAdvanceWithThrowers_StartsARun_AndPublishesItsStandPoint()
    {
        _engine.Standard(1);

        Pass(0.0);

        Assert.AreEqual(ThrowerState.Approach, Facts().State);
        CollectionAssert.AreEqual(new[] { new ThrowerPoint(1, 0f, 65f) }, _engine.Latest);
    }

    [TestMethod]
    public void Pass_ReadsTheRangeAtTheStartOnly()
    {
        _engine.Standard(1);

        Pass(0.0);
        Pass(0.25);
        Pass(0.5);

        CollectionAssert.AreEqual(new[] { 1 }, _engine.RangeReads);
    }

    [TestMethod]
    public void Pass_BetweenTwoSlowPasses_ReadsNothing()
    {
        _engine.Standard(1);
        Pass(0.0);
        _engine.MoveTo(1, 50f);

        Pass(0.1);

        Assert.AreEqual(1, _engine.Tables.Count);
        Assert.AreEqual(0f, Facts().MaxOvershoot);
    }

    [TestMethod]
    public void Pass_WithTheSettingOff_StartsNothing_AndPublishesNothing()
    {
        _engine.Standard(1);

        Pass(0.0, allowed: false);

        Assert.AreEqual(0, _engine.Tables.Count);
        Assert.IsNull(_driver.FactsOf(1));
    }

    [TestMethod]
    public void Pass_ACompanyNotOnAdvance_StartsNothing()
    {
        _engine.Standard(1);
        _engine.Companies[1] = new ThrowerCompany(true, false, false);

        Pass(0.0);

        Assert.IsNull(_driver.FactsOf(1));
        Assert.AreEqual(0, _engine.Tables.Count);
    }

    [TestMethod]
    public void Pass_AClosedGate_StartsNothing_AndNeverReadsTheRange()
    {
        _engine.GatesOf[1] = _engine.Standard(1) with { Held = true };

        Pass(0.0);

        Assert.IsNull(_driver.FactsOf(1));
        Assert.AreEqual(0, _engine.RangeReads.Count, "the range is read only when every other test passes");
    }

    [TestMethod]
    public void Pass_ACompanyAGateRefuses_IsNeverCensused_WhileACompanyTheGatesAdmitIs()
    {
        _engine.GatesOf[1] = _engine.Standard(1) with { RangedBranch = true };
        _engine.Standard(2);

        for (var i = 0; i < 10; i++)
            Pass(i * 0.25);

        Assert.IsNull(_driver.FactsOf(1));
        Assert.IsNotNull(_driver.FactsOf(2));
        Assert.IsFalse(_engine.CensusReads.Contains(1), "a gate refusal needs no walk of the company's soldiers");
        Assert.IsTrue(_engine.CensusReads.Contains(2));
    }

    [TestMethod]
    public void Pass_TooFewArmed_StartsNothing()
    {
        _engine.Standard(1);
        _engine.Censuses[1] = new ThrowerCensus(40, 10, 30);

        Pass(0.0);

        Assert.IsNull(_driver.FactsOf(1));
    }

    [TestMethod]
    public void Pass_ARangeThatCannotBeRead_StartsNothing()
    {
        _engine.Standard(1);
        _engine.Ranges[1] = null;

        Pass(0.0);

        Assert.IsNull(_driver.FactsOf(1));
    }

    [TestMethod]
    public void Pass_ARangeTooShortForTheDepth_StartsNothing()
    {
        _engine.Standard(1);
        _engine.Ranges[1] = 30f;

        Pass(0.0);

        Assert.IsNull(_driver.FactsOf(1));
    }

    [TestMethod]
    public void Pass_ARangeTooShort_IsReadOnceForTheAdvance_NotOnEveryPass()
    {
        _engine.Standard(1);
        _engine.Ranges[1] = 30f;

        for (var i = 0; i < 6; i++)
            Pass(i * 0.25);

        CollectionAssert.AreEqual(new[] { 1 }, _engine.RangeReads);
    }

    [TestMethod]
    public void Pass_ARangeThatCannotBeRead_IsReadOnceForTheAdvance()
    {
        _engine.Standard(1);
        _engine.Ranges[1] = null;

        for (var i = 0; i < 6; i++)
            Pass(i * 0.25);

        CollectionAssert.AreEqual(new[] { 1 }, _engine.RangeReads);
    }

    [TestMethod]
    public void Pass_ARefusedRange_IsReadAgainByANewAdvance()
    {
        _engine.Standard(1);
        _engine.Ranges[1] = 30f;
        Pass(0.0);
        Pass(0.25);
        _engine.Ranges[1] = 80f;
        Pass(0.5);
        Assert.IsNull(_driver.FactsOf(1), "the refusal stands until the Advance is given again");

        _driver.OnAdvanceGiven(new[] { 1 });
        Pass(0.75);

        CollectionAssert.AreEqual(new[] { 1, 1 }, _engine.RangeReads);
        Assert.AreEqual(ThrowerState.Approach, Facts().State);
    }

    [TestMethod]
    public void Pass_ARefusedRange_IsReadAgainWhenTheOrderLapsesAndAnAdvanceComesBack()
    {
        _engine.Standard(1);
        _engine.Ranges[1] = 30f;
        Pass(0.0);
        _engine.Companies[1] = new ThrowerCompany(true, false, false);
        Pass(0.25);
        _engine.Companies[1] = new ThrowerCompany(true, true, false);
        _engine.Ranges[1] = 80f;

        Pass(0.5);

        CollectionAssert.AreEqual(new[] { 1, 1 }, _engine.RangeReads);
        Assert.AreEqual(ThrowerState.Approach, Facts().State);
    }

    [TestMethod]
    public void Pass_ARefusedRange_IsReadAgainAfterTheRunsWereDropped()
    {
        _engine.Standard(1);
        _engine.Ranges[1] = 30f;
        Pass(0.0);
        Pass(0.25, allowed: false);

        Pass(0.5);

        CollectionAssert.AreEqual(new[] { 1, 1 }, _engine.RangeReads);
    }

    [TestMethod]
    public void OnPlayerTeamChanged_ForgetsTheRefusedRanges_SinceCompanyNumbersNameNewFormations()
    {
        _engine.Standard(1);
        _engine.Ranges[1] = 30f;
        Pass(0.0);

        _driver.OnPlayerTeamChanged();
        Pass(0.25);

        CollectionAssert.AreEqual(new[] { 1, 1 }, _engine.RangeReads);
    }

    [TestMethod]
    public void Pass_ACompanyAlreadyWithinTwelveMetresOfTheEnemy_StartsNoRun_AndReadsNoRange()
    {
        _engine.Standard(1, y: 90f);

        Pass(0.0);
        Pass(0.25);

        Assert.IsNull(_driver.FactsOf(1));
        Assert.AreEqual(0, _engine.RangeReads.Count, "a gap of 7 m is engaged before the range is asked");
        Assert.AreEqual(0, _engine.Tables.Count);
        Assert.AreEqual(0, _log.Lines.Count, "no run, so no stand check that could fail or pass for a company that never stood");
    }

    [TestMethod]
    public void Pass_ACompanyBetweenEightAndTwelveMetres_StartsNoRun_SoNoStandPassesForAStandThatNeverWas()
    {
        _engine.Standard(1, y: 88f);

        Pass(0.0);

        Assert.IsNull(_driver.FactsOf(1), "the gap is 9 m: not in melee, yet a run would release enemy-close on its first pass");
        Assert.AreEqual(0, Checks(MarshalChecks.ThrowersStandId).Length);
    }

    [TestMethod]
    public void Pass_AnEngagedCompanyWhoseEnemyDrawsOff_StartsARunWhenTheGapOpensPastTwelveMetres()
    {
        _engine.Standard(1, y: 90f);
        Pass(0.0);
        Assert.IsNull(_driver.FactsOf(1));

        _engine.MoveTo(1, 65f);
        Pass(0.25);
        Pass(0.5);

        Assert.AreEqual(ThrowerState.Stand, Facts().State);
        Assert.IsFalse(Facts().ClosedBeforeStand);
    }

    [TestMethod]
    public void Pass_AGapThatCollapsesAfterTheStart_StillFailsClosedBeforeStand()
    {
        _engine.Standard(1, y: 0f);
        Pass(0.0);
        _engine.MoveTo(1, 90f);

        Pass(0.25);

        StringAssert.Contains(Checks(MarshalChecks.ThrowersStandId).Single(), "fail mission=7 company=1 reason=closed-before-stand");
    }

    [TestMethod]
    public void Pass_AnArrivalStands_AndTheAnchorIsPublishedOnlyOnce()
    {
        StandAt();

        _engine.MoveTo(1, 64f);
        Pass(2.0);

        Assert.AreEqual(1, _engine.Tables.Count);
        Assert.AreEqual(32f, Facts().GapAtStand, 1e-4f);
    }

    [TestMethod]
    public void Pass_ACompanyThatWalksThroughItsPoint_DoesNotStand_UntilItStops()
    {
        _engine.Standard(1);
        Pass(0.0);
        _engine.MoveTo(1, 64f);
        Pass(0.25);
        _engine.MoveTo(1, 65f);
        Pass(0.5);
        _engine.MoveTo(1, 66f);
        Pass(0.75);
        Assert.AreEqual(ThrowerState.Approach, Facts().State);
        Assert.IsFalse(Facts().EverStood);

        Pass(1.0);

        Assert.AreEqual(ThrowerState.Stand, Facts().State);
        Assert.AreEqual(1f, Facts().MaxOvershoot, 1e-3f);
    }

    [TestMethod]
    public void Pass_ALostTarget_TakesNoFacts_AndReleasesNoTarget_WithNoMeleeCheck()
    {
        StandAt();
        _engine.GatesOf[1] = _engine.GatesOf[1] with { Target = new GroundPoint(0f, 115f) };
        Pass(1.25);
        Assert.AreEqual(ThrowerState.Approach, Facts().State, "the enemy drew off and the point is 80");

        _engine.MoveTo(1, 70f);
        _engine.GatesOf[1] = _engine.GatesOf[1] with { HasTarget = false, Target = default, TargetDepth = 0f };
        Pass(1.5);

        Assert.AreEqual(0f, Facts().MaxOvershoot, 1e-4f, "the origin is not an enemy");
        Assert.AreEqual("no-target", Facts().Why);
        Assert.AreEqual(ThrowerReleaseKind.Gate, Facts().Kind);
        CollectionAssert.AreEqual(new[] { "pass mission=7 company=1 gap_at_stand=32.0 stand_gap=32.0 overshoot=0.0" }, Checks(MarshalChecks.ThrowersStandId));
        Assert.AreEqual(0, Checks(MarshalChecks.ThrowersMeleeId).Length);
    }

    [TestMethod]
    public void Pass_ARepublish_HappensOnlyWhenTheApproachPointMovedPastTwoMetres()
    {
        _engine.Standard(1);
        Pass(0.0);
        _engine.GatesOf[1] = _engine.GatesOf[1] with { Target = new GroundPoint(0f, 101f) };
        Pass(0.25);
        Assert.AreEqual(1, _engine.Tables.Count, "a metre of drift");

        _engine.GatesOf[1] = _engine.GatesOf[1] with { Target = new GroundPoint(0f, 104f) };
        Pass(0.5);

        Assert.AreEqual(2, _engine.Tables.Count);
        Assert.AreEqual(69f, _engine.Latest[0].Y, 1e-3f);
    }

    [TestMethod]
    public void Pass_EachChangeOfState_WritesOneThrowersLine()
    {
        StandAt();

        var lines = _logger.Lines.Where(l => l.Contains("[Marshal] throwers ")).ToArray();

        Assert.AreEqual(2, lines.Length);
        StringAssert.Contains(lines[0], "company=1 state=approach why=none units=40 armed=40 armed0=40 ammo=120 range=80.0 gap=97.0 stand_gap=32.0 point=0.0,65.0 overshoot=0.0 speed=NaN shots=0");
        StringAssert.Contains(lines[1], "state=stand");
        StringAssert.Contains(lines[1], "gap=32.0");
    }

    private string[] ThrowerLines() => _logger.Lines.Where(l => l.Contains("[Marshal] throwers ")).ToArray();

    [TestMethod]
    public void Pass_AnOrderThatChanged_WritesTheReleasedLineWithItsReason()
    {
        StandAt();
        var before = ThrowerLines().Length;
        _engine.Companies[1] = new ThrowerCompany(true, false, false);

        Pass(2.0);

        var lines = ThrowerLines();
        Assert.AreEqual(before + 1, lines.Length);
        StringAssert.Contains(lines[before], "company=1 state=released why=order-changed ");
        StringAssert.Contains(lines[before], "point=none");
    }

    [TestMethod]
    public void Pass_AnEmptiedCompany_WritesTheReleasedLineWithTheReason()
    {
        StandAt();
        _engine.Companies[1] = default;

        Pass(2.0);

        StringAssert.Contains(ThrowerLines().Last(), "company=1 state=released why=emptied ");
    }

    [TestMethod]
    public void OnAdvanceGiven_ALiveRun_WritesTheReleasedLineAtTheNextPass()
    {
        StandAt();
        var before = ThrowerLines().Length;
        _driver.OnAdvanceGiven(new[] { 1 });
        Assert.AreEqual(before, ThrowerLines().Length, "a screen tick writes nothing");

        Pass(2.0);

        StringAssert.Contains(ThrowerLines()[before], "state=released why=new-advance ");
    }

    [TestMethod]
    public void Pass_WhenThePlayerNoLongerCommands_WritesTheReleasedLineOnce()
    {
        StandAt();
        var before = ThrowerLines().Length;
        _engine.PlayerCommands = false;

        Pass(2.0);
        Pass(3.0);

        Assert.AreEqual(before + 1, ThrowerLines().Length);
        StringAssert.Contains(ThrowerLines().Last(), "state=released why=ai-control ");
    }

    [TestMethod]
    public void AFaultInThePostfix_WritesTheReleasedLineWithTheFaultReason()
    {
        StandAt();
        var before = ThrowerLines().Length;
        MarshalHooks.LatchThrowerFault(new InvalidOperationException("boom"));

        Pass(2.0);
        Pass(3.0);

        Assert.AreEqual(before + 1, ThrowerLines().Length);
        StringAssert.Contains(ThrowerLines().Last(), "state=released why=fault ");
    }

    [TestMethod]
    public void Pass_ARunReleasedByARule_AndThenAnOrderThatChanges_WritesTheReleasedLineOnce()
    {
        StandAt();
        _engine.Censuses[1] = new ThrowerCensus(40, 12, 12);
        Pass(3.0);
        var released = ThrowerLines().Length;
        StringAssert.Contains(ThrowerLines().Last(), "state=released why=spent ");

        _engine.Companies[1] = new ThrowerCompany(true, false, false);
        Pass(4.0);

        Assert.AreEqual(released, ThrowerLines().Length, "the run was released already: the lost order ends nothing more");
    }

    [TestMethod]
    public void Pass_ALiveRun_WritesASummaryEveryFiveSeconds()
    {
        StandAt();
        var before = _logger.Lines.Count(l => l.Contains("[Marshal] throwers "));

        Pass(4.0);
        Assert.AreEqual(before, _logger.Lines.Count(l => l.Contains("[Marshal] throwers ")));
        Pass(6.1);
        Assert.AreEqual(before + 1, _logger.Lines.Count(l => l.Contains("[Marshal] throwers ")));
    }

    [TestMethod]
    public void Release_Spent_PublishesNothing_AndJudgesTheStandAndTheThrow()
    {
        StandAt();
        for (var i = 0; i < 12; i++)
            MarshalFireTable.CountAnyShot(1);
        _engine.Censuses[1] = new ThrowerCensus(40, 12, 12);

        Pass(3.0);

        Assert.AreEqual(0, _engine.Latest.Length);
        Assert.AreEqual("spent", Facts().Why);
        CollectionAssert.AreEqual(new[] { "pass mission=7 company=1 gap_at_stand=32.0 stand_gap=32.0 overshoot=0.0" }, Checks(MarshalChecks.ThrowersStandId));
        CollectionAssert.AreEqual(new[] { "pass mission=7 company=1 shots=12 stood_s=2.0" }, Checks(MarshalChecks.ThrowersThrowId));
    }

    [TestMethod]
    public void Release_AStandThatWalkedPastItsPoint_FailsWalkedIn()
    {
        StandAt();
        _engine.MoveTo(1, 73f);
        Pass(1.5);
        _engine.Censuses[1] = new ThrowerCensus(40, 12, 12);

        Pass(3.0);

        StringAssert.Contains(Checks(MarshalChecks.ThrowersStandId).Single(), "fail mission=7 company=1 reason=walked-in");
    }

    [TestMethod]
    public void Release_FewShotsAfterALongStand_FailsFewThrows()
    {
        StandAt(1.0);
        Pass(11.5);
        Pass(12.0);
        _engine.Censuses[1] = new ThrowerCensus(40, 12, 12);

        Pass(13.0);

        StringAssert.Contains(Checks(MarshalChecks.ThrowersThrowId).Single(), "fail mission=7 company=1 reason=few-throws");
    }

    [TestMethod]
    public void Release_ShotsMadeOnlyOnTheApproach_FailFewThrows_AndTheRunTotalKeepsThem()
    {
        _engine.Standard(1);
        Pass(0.0);
        for (var i = 0; i < 12; i++)
            MarshalFireTable.CountAnyShot(1);
        _engine.MoveTo(1, 65f);
        Pass(0.75);
        Pass(1.0);
        _engine.Censuses[1] = new ThrowerCensus(40, 12, 12);

        Pass(13.0);

        CollectionAssert.AreEqual(new[] { "fail mission=7 company=1 reason=few-throws shots=0 stood_s=12.0" }, Checks(MarshalChecks.ThrowersThrowId));
        Assert.AreEqual(12, Facts().RunShots);
    }

    [TestMethod]
    public void Release_ShotsAfterTheRelease_AreNotCounted()
    {
        StandAt();
        for (var i = 0; i < 12; i++)
            MarshalFireTable.CountAnyShot(1);
        _engine.Censuses[1] = new ThrowerCensus(40, 12, 12);
        Pass(3.0);
        for (var i = 0; i < 5; i++)
            MarshalFireTable.CountAnyShot(1);

        Pass(4.0);

        Assert.AreEqual(12, Facts().Shots);
        Assert.AreEqual(12, Facts().RunShots);
    }

    [TestMethod]
    public void Release_BeforeAnyStand_SkipsTheStandCheckWithTheWhy()
    {
        _engine.GatesOf[1] = _engine.Standard(1);
        Pass(0.0);
        _engine.GatesOf[1] = _engine.GatesOf[1] with { Held = true };

        Pass(0.25);

        CollectionAssert.AreEqual(new[] { "skip mission=7 company=1 reason=released-before-stand why=held" }, Checks(MarshalChecks.ThrowersStandId));
        CollectionAssert.AreEqual(new[] { "skip mission=7 company=1 reason=never-stood" }, Checks(MarshalChecks.ThrowersThrowId));
    }

    [TestMethod]
    public void Release_ByARule_OpensTheMeleeCheck_AndBlowsPassIt()
    {
        StandAt();
        _engine.Censuses[1] = new ThrowerCensus(40, 12, 12);
        Pass(3.0);
        Assert.AreEqual(0, Checks(MarshalChecks.ThrowersMeleeId).Length);

        MarshalFireTable.CountBlow(1);
        _engine.MoveTo(1, 69f);
        Pass(8.0);

        CollectionAssert.AreEqual(new[] { "pass mission=7 company=1 blows=1 melee_s=5.0 advanced_m=4.0" }, Checks(MarshalChecks.ThrowersMeleeId));
        Assert.AreEqual("pass", Facts().Melee);
    }

    [TestMethod]
    public void Melee_BlowsWhileTheCompanyStaysAtItsStand_FailsNoAdvance()
    {
        StandAt();
        _engine.Censuses[1] = new ThrowerCensus(40, 12, 12);
        Pass(3.0);
        MarshalFireTable.CountBlow(1);

        Pass(8.0);
        Pass(47.9);
        Assert.AreEqual(0, Checks(MarshalChecks.ThrowersMeleeId).Length);
        Pass(48.2);

        CollectionAssert.AreEqual(new[] { "fail mission=7 company=1 reason=no-advance blows=1 melee_s=45.2 advanced_m=0.0" }, Checks(MarshalChecks.ThrowersMeleeId));
    }

    [TestMethod]
    public void Melee_TheFarthestAdvanceCounts_WhenTheCompanyIsPushedBack()
    {
        StandAt();
        _engine.Censuses[1] = new ThrowerCensus(40, 12, 12);
        Pass(3.0);
        _engine.MoveTo(1, 70f);
        Pass(5.0);
        _engine.MoveTo(1, 66f);
        MarshalFireTable.CountBlow(1);

        Pass(8.0);

        CollectionAssert.AreEqual(new[] { "pass mission=7 company=1 blows=1 melee_s=5.0 advanced_m=5.0" }, Checks(MarshalChecks.ThrowersMeleeId));
    }

    [TestMethod]
    public void Melee_NoBlowsWithinFortyFiveSeconds_Fails()
    {
        StandAt();
        _engine.Censuses[1] = new ThrowerCensus(40, 12, 12);
        Pass(3.0);

        Pass(47.9);
        Assert.AreEqual(0, Checks(MarshalChecks.ThrowersMeleeId).Length);
        Pass(48.2);

        StringAssert.Contains(Checks(MarshalChecks.ThrowersMeleeId).Single(), "fail mission=7 company=1 reason=no-melee");
    }

    [TestMethod]
    public void Melee_AnOrderThatChanged_Skips()
    {
        StandAt();
        _engine.Censuses[1] = new ThrowerCensus(40, 12, 12);
        Pass(3.0);
        _engine.Companies[1] = new ThrowerCompany(true, false, false);

        Pass(4.0);

        CollectionAssert.AreEqual(new[] { "skip mission=7 company=1 reason=order-changed" }, Checks(MarshalChecks.ThrowersMeleeId));
    }

    [TestMethod]
    public void Release_ByAGateReason_WritesNoMeleeCheck()
    {
        StandAt();
        _engine.GatesOf[1] = _engine.GatesOf[1] with { OpenArrangement = false };

        Pass(3.0);
        MarshalFireTable.CountBlow(1);
        Pass(10.0);

        Assert.AreEqual("arrangement", Facts().Why);
        Assert.AreEqual(0, Checks(MarshalChecks.ThrowersMeleeId).Length);
    }

    [TestMethod]
    public void AReleasedRun_IsNotRestartedWhileTheAdvanceLasts_ButANewAdvanceStartsANewRun()
    {
        StandAt();
        _engine.Censuses[1] = new ThrowerCensus(40, 12, 12);
        Pass(3.0);
        var first = Facts().Serial;
        _engine.Censuses[1] = new ThrowerCensus(40, 40, 120);
        Pass(4.0);
        Pass(5.0);
        Assert.AreEqual(first, Facts().Serial);
        Assert.AreEqual(ThrowerState.Released, Facts().State);

        _driver.OnAdvanceGiven(new[] { 1 });
        Pass(6.0);

        Assert.AreNotEqual(first, Facts().Serial);
        Assert.AreNotEqual(ThrowerState.Released, Facts().State);
    }

    [TestMethod]
    public void OnAdvanceGiven_ALiveRun_IsLostAndJudged_AndTheNextPassStartsANewOne()
    {
        StandAt();
        var first = Facts().Serial;

        _driver.OnAdvanceGiven(new[] { 1 });
        Pass(2.0);

        StringAssert.Contains(Checks(MarshalChecks.ThrowersStandId)[0], "pass mission=7 company=1");
        Assert.AreNotEqual(first, Facts().Serial);
    }

    [TestMethod]
    public void OnAdvanceGiven_ACompanyWithNoRun_DoesNothing()
    {
        _driver.OnAdvanceGiven(new[] { 3 });
        Pass(0.0);

        Assert.AreEqual(0, _log.Lines.Count);
    }

    [TestMethod]
    public void Pass_AnOrderThatIsNoLongerAdvance_LosesTheRun_AndWithdrawsThePoint()
    {
        StandAt();
        _engine.Companies[1] = new ThrowerCompany(true, false, false);

        Pass(2.0);

        Assert.AreEqual(0, _engine.Latest.Length);
        Assert.AreEqual("order-changed", Facts().Why);
        Assert.AreEqual(ThrowerReleaseKind.Lost, Facts().Kind);
    }

    [TestMethod]
    public void Pass_AnEmptiedCompany_LosesTheRunWithTheReason()
    {
        StandAt();
        _engine.Companies[1] = default;

        Pass(2.0);

        Assert.AreEqual("emptied", Facts().Why);
    }

    [TestMethod]
    public void Pass_WhenThePlayerNoLongerCommands_EndsTheRunAiControl_AndStartsNothing()
    {
        StandAt();
        _engine.PlayerCommands = false;

        Pass(2.0);
        Pass(3.0);

        Assert.AreEqual("ai-control", Facts().Why);
        Assert.AreEqual(0, _engine.Latest.Length);
        Assert.AreEqual(2, _engine.Tables.Count, "approach point, then the withdrawal, and nothing after");
    }

    [TestMethod]
    public void Pass_TheSettingGoingOff_DropsTheRunWithNoCheckLine()
    {
        StandAt();

        Pass(2.0, allowed: false);

        Assert.AreEqual(0, _engine.Latest.Length);
        Assert.AreEqual(0, Checks(MarshalChecks.ThrowersStandId).Length);
        Assert.AreEqual(0, Checks(MarshalChecks.ThrowersThrowId).Length);
    }

    [TestMethod]
    public void OnPlayerTeamChanged_DropsTheRunsWithNoCheckLine_AndPublishesNothingMore()
    {
        StandAt();

        _driver.OnPlayerTeamChanged();
        _engine.Companies[1] = new ThrowerCompany(true, false, false);
        Pass(2.0);

        Assert.AreEqual(0, _engine.Latest.Length);
        Assert.AreEqual(0, Checks(MarshalChecks.ThrowersStandId).Length);
        Assert.IsNull(_driver.FactsOf(1), "company numbers named the old team's formations");
    }

    [TestMethod]
    public void OnPlayerTeamChanged_WithdrawsTheTableAtOnce_AndTheNewTeamsCompanyAtTheSamePointIsPublished()
    {
        StandAt();
        var tables = _engine.Tables.Count;

        _driver.OnPlayerTeamChanged();

        Assert.AreEqual(tables + 1, _engine.Tables.Count, "the old team's formations lose their stand at the call, not at the next pass");
        Assert.AreEqual(0, _engine.Latest.Length);
        Assert.AreEqual(1, Checks(MarshalChecks.ThrowersCostId).Length);

        _engine.Standard(1);
        Pass(1.25);

        CollectionAssert.AreEqual(new[] { new ThrowerPoint(1, 0f, 65f) }, _engine.Latest);
        Assert.AreEqual(tables + 2, _engine.Tables.Count, "a new formation at the same coordinates is still published");
    }

    [TestMethod]
    public void Pass_ARunLostWhileTheAiCommands_IsNotStartedAgainOnTheSameAdvance()
    {
        StandAt();
        _engine.PlayerCommands = false;
        Pass(2.0);
        var serial = Facts().Serial;

        _engine.PlayerCommands = true;
        Pass(3.0);

        Assert.AreEqual(serial, Facts().Serial);
        Assert.AreEqual(ThrowerState.Released, Facts().State);
        Assert.AreEqual(2, _engine.Tables.Count, "the approach point, then the withdrawal, and no new run");
    }

    [TestMethod]
    public void AFaultInThePostfix_IsWrittenOnce_FailsTheStand_AndPublishesNothingMore()
    {
        StandAt();
        MarshalHooks.LatchThrowerFault(new InvalidOperationException("boom"));

        Pass(2.0);
        Pass(3.0);
        Pass(4.0);

        Assert.AreEqual(1, _logger.Lines.Count(l => l.StartsWith("ERROR [MarshalsWord] the thrower stand failed: ", StringComparison.Ordinal)));
        StringAssert.StartsWith(Checks(MarshalChecks.ThrowersStandId).Single(), "fail mission=7 company=1 reason=fault");
        Assert.AreEqual(0, _engine.Latest.Length);
        Assert.AreEqual(2, _engine.Tables.Count);
    }

    [TestMethod]
    public void AFaultWithNoRun_StillWritesTheFailLineOnce()
    {
        MarshalHooks.LatchThrowerFault(new InvalidOperationException("boom"));

        Pass(0.0);
        Pass(1.0);

        Assert.AreEqual(1, _log.Lines.Count(l => l.Id == MarshalChecks.ThrowersStandId && l.Result == "fail"));
    }

    [TestMethod]
    public void TheFirstTable_StartsAnEpoch_ThatResetsTheBuckets()
    {
        MarshalThrowerCost.AddPublished(10, replaced: false);
        _engine.Standard(1);

        Pass(0.0);

        Assert.AreEqual(1, _driver.Epoch);
        Assert.AreEqual(0, MarshalThrowerCost.Read().Published.Calls);
        Assert.AreEqual(1, _driver.Published);
    }

    [TestMethod]
    public void AnEpochThatEnds_WritesTheCostLine_AndAPassingCheck()
    {
        StandAt();
        for (var i = 0; i < 400; i++)
            MarshalThrowerCost.AddPublished(2, replaced: false);
        _engine.Censuses[1] = new ThrowerCensus(40, 12, 12);

        Pass(3.0);

        var line = _logger.Lines.Single(l => l.Contains("[Marshal] throwers-cost "));
        StringAssert.Contains(line, "epoch=1 pub_calls=400");
        StringAssert.Contains(line, "published=1");
        Assert.AreEqual(1, Checks(MarshalChecks.ThrowersCostId).Length);
        StringAssert.StartsWith(Checks(MarshalChecks.ThrowersCostId)[0], "pass mission=7 epoch=1 calls=400 mean_us=");
    }

    [TestMethod]
    public void AnEpochWithFewCalls_SkipsTheCostCheck()
    {
        StandAt();
        MarshalThrowerCost.AddPublished(2, replaced: false);
        _engine.Censuses[1] = new ThrowerCensus(40, 12, 12);

        Pass(3.0);

        StringAssert.Contains(Checks(MarshalChecks.ThrowersCostId).Single(), "skip mission=7 epoch=1 reason=too-few-calls");
    }

    [TestMethod]
    public void AnEpochWithASlowMean_FailsTheCostCheck()
    {
        StandAt();
        for (var i = 0; i < 400; i++)
            MarshalThrowerCost.AddPublished(Stopwatch.Frequency / 5000, replaced: false);
        _engine.Censuses[1] = new ThrowerCensus(40, 12, 12);

        Pass(3.0);

        StringAssert.Contains(Checks(MarshalChecks.ThrowersCostId).Single(), "fail mission=7 epoch=1 reason=mean");
    }

    [TestMethod]
    public void AnEpochWithOneSlowCall_FailsTheCostCheckOnTheMost()
    {
        StandAt();
        for (var i = 0; i < 400; i++)
            MarshalThrowerCost.AddPublished(2, replaced: false);
        MarshalThrowerCost.AddPublished(Stopwatch.Frequency / 100, replaced: false);
        _engine.Censuses[1] = new ThrowerCensus(40, 12, 12);

        Pass(3.0);

        StringAssert.Contains(Checks(MarshalChecks.ThrowersCostId).Single(), "fail mission=7 epoch=1 reason=max");
    }

    [TestMethod]
    public void WhileAnEpochIsOpen_ACostLineComesEveryTenSeconds_AndAddsThePostfixGrowthToTheCounter()
    {
        StandAt();
        for (var i = 0; i < 5; i++)
            MarshalThrowerCost.AddMoved();

        Pass(9.0);
        Assert.AreEqual(0, _logger.Lines.Count(l => l.Contains("[Marshal] throwers-cost ")));
        Pass(10.5);

        Assert.AreEqual(1, _logger.Lines.Count(l => l.Contains("[Marshal] throwers-cost ")));
        Assert.AreEqual(5, _log.Counter(MarshalChecks.ThrowersPostfixCounterId).Passes);
        MarshalThrowerCost.AddMoved();
        Pass(21.0);
        Assert.AreEqual(6, _log.Counter(MarshalChecks.ThrowersPostfixCounterId).Passes, "only the growth is added");
    }

    [TestMethod]
    public void Summarize_AnOpenEpoch_WritesItsCostLineAndCheck_AndPublishesNothing()
    {
        StandAt();
        var tables = _engine.Tables.Count;

        _driver.Summarize();
        _driver.Summarize();

        Assert.AreEqual(1, Checks(MarshalChecks.ThrowersCostId).Length);
        Assert.AreEqual(tables, _engine.Tables.Count);
    }

    [TestMethod]
    public void AmmoSpent_OfALiveRun_IsTheAmmoAtTheStartLessWhatTheLastPassCounted()
    {
        StandAt();
        _engine.Censuses[1] = new ThrowerCensus(40, 38, 90);

        Pass(1.25);

        Assert.AreNotEqual(ThrowerState.Released, Facts().State);
        Assert.AreEqual(30, Facts().AmmoSpent);
    }

    [TestMethod]
    public void AmmoSpent_AfterTheRelease_IsFrozenAtTheRelease()
    {
        StandAt();
        _engine.Censuses[1] = new ThrowerCensus(40, 12, 12);
        Pass(3.0);
        Assert.AreEqual(ThrowerState.Released, Facts().State);
        Assert.AreEqual(108, Facts().AmmoSpent);

        _engine.Censuses[1] = new ThrowerCensus(40, 0, 0);
        Pass(3.25);

        Assert.AreEqual(108, Facts().AmmoSpent);
    }

    [TestMethod]
    public void Facts_CarryTheStartGap_TheStandShots_TheRunShots_AndTheRangeFacts()
    {
        _engine.Standard(1);
        MarshalFireTable.CountAnyShot(1);
        Pass(0.0);
        MarshalFireTable.CountAnyShot(1);
        MarshalFireTable.CountAnyShot(1);

        _engine.MoveTo(1, 65f);
        Pass(0.75);
        Pass(1.0);
        MarshalFireTable.CountAnyShot(1);
        MarshalFireTable.CountAnyShot(1);
        MarshalFireTable.CountAnyShot(1);

        var facts = Facts();
        Assert.AreEqual(97f, facts.Gap0, 1e-3f);
        Assert.AreEqual(3, facts.Shots, "only the shots from the first stand count; the shot before the run and those on the approach do not");
        Assert.AreEqual(5, facts.RunShots, "the shot before the run began does not count, the rest of the run does");
        Assert.AreEqual(80f, facts.Range, 1e-3f);
        Assert.AreEqual(32f, facts.StandGap, 1e-3f);
        Assert.AreEqual(40, facts.Units);
    }
}
