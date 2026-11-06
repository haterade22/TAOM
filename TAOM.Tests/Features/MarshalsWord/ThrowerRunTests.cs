using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>One company's run for one Advance (build-spec 9.4): the approach, the stand, the release and the facts the checks read.</summary>
[TestClass]
public class ThrowerRunTests
{
    private static readonly ThrowerCensus Forty = new(Units: 40, Armed: 40, Ammo: 120);

    private static ThrowerRun NewRun(double now = 0.0) => new(Forty, range: 80f, now);

    private static ApproachAnswer Walk(float x, float y) => new(ApproachKind.Walk, new GroundPoint(x, y), 0f, 1f);

    private static ApproachAnswer Here(float x, float y) => new(ApproachKind.StandHere, new GroundPoint(x, y), 0f, 1f);

    private static readonly ApproachAnswer Nowhere = new(ApproachKind.NoPoint, default, 0f, 0f);

    private static ThrowerStep At(double now, float gap, float y, ApproachAnswer answer, int armed = 40, string why = "") =>
        new(new ThrowerCensus(40, armed, armed * 3), why, gap, new GroundPoint(0f, y), answer, now);

    private static ThrowerRun Standing(double standAt = 1.0)
    {
        var run = NewRun(now: -1.0);
        run.Step(At(-1.0, 90f, 0f, Walk(0f, 70f)));
        run.Step(At(standAt - 0.25, 34f, 67f, Walk(0f, 70f)));
        run.Step(At(standAt, 34f, 67f, Walk(0f, 70f)));
        Assert.AreEqual(ThrowerState.Stand, run.State);
        return run;
    }

    [TestMethod]
    public void Start_KeepsTheFirstCensusAndTheStandGapOfTheRange()
    {
        var run = new ThrowerRun(new ThrowerCensus(40, 38, 114), range: 80f, now: 7.5);

        Assert.AreEqual(ThrowerState.Approach, run.State);
        Assert.AreEqual(38, run.Armed0);
        Assert.AreEqual(114, run.Ammo0);
        Assert.AreEqual(80f, run.Range, 1e-4f);
        Assert.AreEqual(32f, run.StandGap, 1e-4f);
        Assert.AreEqual(7.5, run.StartedAt, 1e-9);
        Assert.IsNull(run.Published);
        Assert.AreEqual(ThrowerReleaseKind.None, run.ReleaseKind);
        Assert.AreEqual("", run.ReleaseReason);
        Assert.IsFalse(run.EverStood);
    }

    [TestMethod]
    public void Approach_FarFromTheEnemy_PublishesTheApproachPoint_AndStaysInApproach()
    {
        var run = NewRun();

        run.Step(At(0.0, 90f, 0f, Walk(0f, 70f)));

        Assert.AreEqual(ThrowerState.Approach, run.State);
        Assert.AreEqual(new GroundPoint(0f, 70f), run.Published);
    }

    [TestMethod]
    public void Approach_APointThatMovedUnderTheThreshold_IsNotPublishedAgain()
    {
        var run = NewRun();
        run.Step(At(0.0, 90f, 0f, Walk(0f, 70f)));

        run.Step(At(0.25, 90f, 1f, Walk(0f, 71.9f)));

        Assert.AreEqual(new GroundPoint(0f, 70f), run.Published);
    }

    [TestMethod]
    public void Approach_APointThatMovedOverTheThreshold_IsPublishedAgain()
    {
        var run = NewRun();
        run.Step(At(0.0, 90f, 0f, Walk(0f, 70f)));

        run.Step(At(0.25, 90f, 1f, Walk(0f, 72.1f)));

        Assert.AreEqual(new GroundPoint(0f, 72.1f), run.Published);
    }

    [TestMethod]
    public void Approach_AnAnswerWithNoPoint_PublishesNothingNew()
    {
        var run = NewRun();

        run.Step(At(0.0, 90f, 0f, Nowhere));

        Assert.IsNull(run.Published);
        Assert.AreEqual(ThrowerState.Approach, run.State);
    }

    [TestMethod]
    public void Arrive_WithinFourMetresOfThePublishedPoint_Stands_AndKeepsTheGapItStoodAt()
    {
        var run = NewRun();
        run.Step(At(0.0, 90f, 0f, Walk(0f, 70f)));

        run.Step(At(1.0, 34f, 66f, Walk(0f, 70f)));
        run.Step(At(1.25, 34f, 66f, Walk(0f, 70f)));

        Assert.AreEqual(ThrowerState.Stand, run.State);
        Assert.IsTrue(run.EverStood);
        Assert.AreEqual(34f, run.GapAtStand, 1e-4f);
        Assert.AreEqual(new GroundPoint(0f, 70f), run.Published);
    }

    [TestMethod]
    public void Arrive_FiveMetresFromThePoint_KeepsApproaching()
    {
        var run = NewRun();
        run.Step(At(0.0, 90f, 0f, Walk(0f, 70f)));

        run.Step(At(1.0, 34f, 65f, Walk(0f, 70f)));
        run.Step(At(1.25, 34f, 65f, Walk(0f, 70f)));

        Assert.AreEqual(ThrowerState.Approach, run.State);
    }

    [TestMethod]
    public void Stand_LatchesItsAnchor_AndPublishesItUnchanged()
    {
        var run = Standing();

        run.Step(At(2.0, 36f, 68f, Walk(0f, 90f)));

        Assert.AreEqual(ThrowerState.Stand, run.State);
        Assert.AreEqual(new GroundPoint(0f, 70f), run.Published);
    }

    [TestMethod]
    public void StandHere_AnEnemyAlreadyInsideTheDistance_StandsAtTheCompanysOwnPosition_OnceStill()
    {
        var run = NewRun();

        run.Step(At(0.0, 26f, 10f, Here(0f, 10f)));
        run.Step(At(0.25, 26f, 10f, Here(0f, 10f)));

        Assert.AreEqual(ThrowerState.Stand, run.State);
        Assert.AreEqual(new GroundPoint(0f, 10f), run.Published);
        Assert.AreEqual(26f, run.GapAtStand, 1e-4f);
    }

    [TestMethod]
    public void Follow_TheGapPastTheStandGapPlusTenMetres_GoesBackToApproach_AndPublishesAgain()
    {
        var run = Standing();

        run.Step(At(3.0, 42.1f, 68f, Walk(0f, 90f)));

        Assert.AreEqual(ThrowerState.Approach, run.State);
        Assert.AreEqual(new GroundPoint(0f, 90f), run.Published);
    }

    [TestMethod]
    public void Follow_AGapOfExactlyTheStandGapPlusTenMetres_KeepsStanding()
    {
        var run = Standing();

        run.Step(At(3.0, 42f, 68f, Walk(0f, 90f)));

        Assert.AreEqual(ThrowerState.Stand, run.State);
    }

    [TestMethod]
    public void Follow_KeepsTheSecondsAlreadyStood_AndStandsAgainOnTheSameTally()
    {
        var run = Standing(standAt: 1.0);
        run.Step(At(6.0, 34f, 68f, Walk(0f, 70f)));
        Assert.AreEqual(5.0, run.StoodSeconds, 1e-9);

        run.Step(At(7.0, 50f, 68f, Walk(0f, 90f)));
        run.Step(At(9.0, 50f, 68f, Walk(0f, 90f)));
        Assert.AreEqual(ThrowerState.Approach, run.State);
        Assert.AreEqual(6.0, run.StoodSeconds, 1e-9, "the second pass of approach adds nothing; the pass that left the stand counted the time up to it");

        run.Step(At(10.0, 34f, 88f, Walk(0f, 90f)));
        run.Step(At(10.25, 34f, 88f, Walk(0f, 90f)));
        run.Step(At(12.25, 34f, 88f, Walk(0f, 90f)));
        Assert.AreEqual(8.0, run.StoodSeconds, 1e-9);
    }

    [TestMethod]
    public void StoodSeconds_CountOnlyThePassesThatBeganInStand()
    {
        var run = NewRun();
        run.Step(At(0.0, 90f, 0f, Walk(0f, 70f)));
        run.Step(At(5.0, 80f, 10f, Walk(0f, 70f)));

        Assert.AreEqual(0.0, run.StoodSeconds, 1e-9);
    }

    [TestMethod]
    public void Release_SpentAtThirtyPercentOfTheArmed_AndNotAboveIt()
    {
        var above = Standing();
        above.Step(At(2.0, 34f, 68f, Walk(0f, 70f), armed: 13));
        Assert.AreEqual(ThrowerState.Stand, above.State);

        var spent = Standing();
        spent.Step(At(2.0, 34f, 68f, Walk(0f, 70f), armed: 12));
        Assert.AreEqual(ThrowerState.Released, spent.State);
        Assert.AreEqual("spent", spent.ReleaseReason);
        Assert.AreEqual(ThrowerReleaseKind.Rule, spent.ReleaseKind);
        Assert.AreEqual(12, spent.ArmedAtRelease);
        Assert.AreEqual(2.0, spent.ReleasedAt, 1e-9);
    }

    [TestMethod]
    public void Release_TheEnemyWithinTwelveMetres_ReleasesEnemyClose_AndAGapOfTwelvePointOneDoesNot()
    {
        var near = Standing();
        near.Step(At(2.0, 12.1f, 68f, Walk(0f, 70f)));
        Assert.AreEqual(ThrowerState.Stand, near.State);

        var close = Standing();
        close.Step(At(2.0, 12f, 68f, Walk(0f, 70f)));
        Assert.AreEqual("enemy-close", close.ReleaseReason);
        Assert.AreEqual(ThrowerReleaseKind.Rule, close.ReleaseKind);
    }

    [TestMethod]
    public void Release_FortySecondsStoodInAll_ReleasesStandTime()
    {
        var short39 = Standing(standAt: 0.0);
        short39.Step(At(39.9, 34f, 68f, Walk(0f, 70f)));
        Assert.AreEqual(ThrowerState.Stand, short39.State);

        var run = Standing(standAt: 0.0);
        run.Step(At(40.0, 34f, 68f, Walk(0f, 70f)));
        Assert.AreEqual("stand-time", run.ReleaseReason);
    }

    [TestMethod]
    public void Release_StandTimeSpreadOverTwoStands_AddsUp()
    {
        var run = Standing(standAt: 0.0);
        run.Step(At(25.0, 34f, 68f, Walk(0f, 70f)));
        run.Step(At(26.0, 50f, 68f, Walk(0f, 90f)));
        run.Step(At(27.0, 34f, 88f, Walk(0f, 90f)));
        run.Step(At(27.25, 34f, 88f, Walk(0f, 90f)));
        Assert.AreEqual(ThrowerState.Stand, run.State);

        run.Step(At(41.25, 34f, 88f, Walk(0f, 90f)));

        Assert.AreEqual("stand-time", run.ReleaseReason);
        Assert.AreEqual(40.0, run.StoodSeconds, 1e-9);
    }

    [TestMethod]
    public void Release_ARunOfOneHundredTwentySeconds_ReleasesRunTime_WhereverItIs()
    {
        var run = NewRun();
        run.Step(At(0.0, 90f, 0f, Walk(0f, 70f)));
        run.Step(At(119.9, 90f, 1f, Walk(0f, 70f)));
        Assert.AreEqual(ThrowerState.Approach, run.State);

        run.Step(At(120.0, 90f, 1f, Walk(0f, 70f)));

        Assert.AreEqual("run-time", run.ReleaseReason);
    }

    [TestMethod]
    public void Release_AGateReason_IsKeptAsTheWhy()
    {
        var run = Standing();

        run.Step(At(2.0, 34f, 68f, Walk(0f, 70f), why: "held"));

        Assert.AreEqual(ThrowerState.Released, run.State);
        Assert.AreEqual("held", run.ReleaseReason);
        Assert.AreEqual(ThrowerReleaseKind.Gate, run.ReleaseKind);
    }

    [TestMethod]
    public void Release_TooFewFromTheGate_IsLeftToSpent_BecauseTheArmedFallAsTheyThrow()
    {
        var run = Standing();

        run.Step(At(2.0, 34f, 68f, Walk(0f, 70f), armed: 19, why: "too-few"));

        Assert.AreEqual(ThrowerState.Stand, run.State);
    }

    [TestMethod]
    public void Release_TwoReasonsInOnePass_TheFirstInTheContractsOrderWins()
    {
        var spentAndClose = Standing();
        spentAndClose.Step(At(2.0, 12f, 68f, Walk(0f, 70f), armed: 12));
        Assert.AreEqual("spent", spentAndClose.ReleaseReason);

        var closeAndTime = Standing(standAt: 0.0);
        closeAndTime.Step(At(120.0, 12f, 68f, Walk(0f, 70f)));
        Assert.AreEqual("enemy-close", closeAndTime.ReleaseReason);

        var standAndRun = Standing(standAt: 0.0);
        standAndRun.Step(At(120.0, 34f, 68f, Walk(0f, 70f)));
        Assert.AreEqual("stand-time", standAndRun.ReleaseReason);

        var runAndGate = NewRun();
        runAndGate.Step(At(0.0, 90f, 0f, Walk(0f, 70f)));
        runAndGate.Step(At(120.0, 90f, 1f, Walk(0f, 70f), why: "held"));
        Assert.AreEqual("run-time", runAndGate.ReleaseReason);
    }

    [TestMethod]
    public void AfterARelease_NothingIsPublished_AndNoStepRestartsTheRun()
    {
        var run = Standing();
        run.Step(At(2.0, 12f, 68f, Walk(0f, 70f)));
        Assert.AreEqual(ThrowerState.Released, run.State);
        Assert.IsNull(run.Published);

        run.Step(At(3.0, 90f, 0f, Walk(0f, 70f)));
        run.Step(At(4.0, 20f, 69f, Here(0f, 69f), why: "held"));

        Assert.AreEqual(ThrowerState.Released, run.State);
        Assert.AreEqual("enemy-close", run.ReleaseReason);
        Assert.AreEqual(2.0, run.ReleasedAt, 1e-9);
        Assert.IsNull(run.Published);
    }

    [TestMethod]
    public void ClosedBeforeStand_AGapOfEightMetresBeforeTheFirstStand_IsKept()
    {
        var run = NewRun();
        run.Step(At(0.0, 90f, 0f, Walk(0f, 70f)));

        run.Step(At(1.0, 8f, 20f, Walk(0f, 70f)));

        Assert.IsTrue(run.ClosedBeforeStand);
        Assert.IsFalse(run.EverStood);
    }

    [TestMethod]
    public void ClosedBeforeStand_AGapOfEightPointOne_IsNot()
    {
        var run = NewRun();
        run.Step(At(0.0, 90f, 0f, Walk(0f, 70f)));

        run.Step(At(1.0, 8.1f, 20f, Walk(0f, 70f)));

        Assert.IsFalse(run.ClosedBeforeStand);
    }

    [TestMethod]
    public void ClosedBeforeStand_ACompanyThatStoodStandsAtMeleeGapLater_IsNotMarked()
    {
        var run = Standing();

        run.Step(At(2.0, 5f, 69f, Walk(0f, 70f)));

        Assert.IsFalse(run.ClosedBeforeStand);
    }

    [TestMethod]
    public void ClosedBeforeStand_AStandHereAtMeleeGap_IsMarked_AndReleasesEnemyCloseBeforeItCanStand()
    {
        var run = NewRun();

        run.Step(At(0.0, 7f, 10f, Here(0f, 10f)));

        Assert.IsTrue(run.ClosedBeforeStand);
        Assert.IsFalse(run.EverStood);
        Assert.AreEqual("enemy-close", run.ReleaseReason);
    }

    [TestMethod]
    public void MaxOvershoot_IsTheMostTheCompanyPassedItsPointTowardTheEnemy_AndNeverFalls()
    {
        var run = Standing();
        Assert.AreEqual(0f, run.MaxOvershoot, 1e-4f);

        run.Step(At(2.0, 31f, 73f, Walk(0f, 70f)));
        run.Step(At(3.0, 33f, 71f, Walk(0f, 70f)));
        run.Step(At(4.0, 36f, 60f, Walk(0f, 70f)));

        Assert.AreEqual(3f, run.MaxOvershoot, 1e-4f, "behind the point is no overshoot, and a smaller one later keeps the most");
    }

    [TestMethod]
    public void MaxOvershoot_AfterStandHere_IsMeasuredFromTheAnchor()
    {
        var run = NewRun();
        run.Step(At(0.0, 26f, 10f, Here(0f, 10f)));

        run.Step(At(1.0, 24f, 12.5f, Here(0f, 12.5f)));

        Assert.AreEqual(2.5f, run.MaxOvershoot, 1e-4f);
    }

    [TestMethod]
    public void MinGap_IsTheSmallestGapAnyPassSaw()
    {
        var run = NewRun();
        run.Step(At(0.0, 90f, 0f, Walk(0f, 70f)));
        run.Step(At(0.25, 60f, 5f, Walk(0f, 70f)));
        run.Step(At(0.5, 70f, 4f, Walk(0f, 70f)));

        Assert.AreEqual(60f, run.MinGap, 1e-4f);
    }

    [TestMethod]
    public void Arrive_ACompanyWalkingThroughItsPointAtFourMetresASecond_NeverStands()
    {
        var run = NewRun();
        run.Step(At(0.0, 90f, 0f, Walk(0f, 70f)));

        for (var i = 0; i < 9; i++)
        {
            var y = 66f + i;
            run.Step(At(10.0 + 0.25 * i, 36f - i, y, y < 70f ? Walk(0f, 70f) : Here(0f, y)));
        }

        Assert.AreEqual(ThrowerState.Approach, run.State);
        Assert.IsFalse(run.EverStood);
        Assert.AreEqual(4f, run.MaxOvershoot, 1e-4f);
    }

    [TestMethod]
    public void StandHere_FixesItsPointOnce_AndAgainAfterAWalkPoint()
    {
        var run = NewRun();
        run.Step(At(0.0, 26f, 10f, Here(0f, 10f)));
        run.Step(At(0.25, 26f, 11f, Here(0f, 11f)));
        Assert.AreEqual(new GroundPoint(0f, 10f), run.Published);

        run.Step(At(0.5, 40f, 11f, Walk(0f, 20f)));
        Assert.AreEqual(new GroundPoint(0f, 20f), run.Published);

        run.Step(At(0.75, 26f, 21f, Here(0f, 21f)));
        Assert.AreEqual(new GroundPoint(0f, 21f), run.Published);
    }

    [TestMethod]
    public void Speed_IsNotANumberUntilTwoPasses_ThenTheMetresPerSecondBetweenThem()
    {
        var run = NewRun();

        run.Step(At(0.0, 90f, 0f, Walk(0f, 70f)));
        Assert.IsTrue(float.IsNaN(run.Speed));

        run.Step(At(0.25, 89f, 1f, Walk(0f, 70f)));
        Assert.AreEqual(4f, run.Speed, 1e-4f);
    }

    [TestMethod]
    public void Step_WithNoTarget_TakesNoFacts_MovesNothing_AndCannotReleaseEnemyClose()
    {
        var run = Standing();

        run.Step(new ThrowerStep(new ThrowerCensus(40, 40, 120), "no-target", 5f, new GroundPoint(0f, 60f), new ApproachAnswer(ApproachKind.Walk, new GroundPoint(0f, 35f), 0f, -1f), 2.0, HasTarget: false));

        Assert.AreEqual(34f, run.MinGap, 1e-4f);
        Assert.AreEqual(0f, run.MaxOvershoot, 1e-4f);
        Assert.AreEqual(ThrowerReleaseKind.Gate, run.ReleaseKind);
        Assert.AreEqual("no-target", run.ReleaseReason);
    }

    [TestMethod]
    public void Release_AGapThatIsNotANumber_ReleasesEnemyClose_SoARunNeverGoesOnBlind()
    {
        var run = Standing();

        run.Step(At(2.0, float.NaN, 68f, Walk(0f, 70f)));

        Assert.AreEqual("enemy-close", run.ReleaseReason);
    }

    [TestMethod]
    public void Lose_BeforeAnyRelease_ReleasesWithTheReason_AndWithdrawsThePoint()
    {
        var run = Standing();

        run.Lose("order-changed", 9.0);

        Assert.AreEqual(ThrowerState.Released, run.State);
        Assert.AreEqual(ThrowerReleaseKind.Lost, run.ReleaseKind);
        Assert.AreEqual("order-changed", run.ReleaseReason);
        Assert.AreEqual(9.0, run.ReleasedAt, 1e-9);
        Assert.IsNull(run.Published);
    }

    [TestMethod]
    public void Lose_AfterARelease_KeepsTheFirstReason()
    {
        var run = Standing();
        run.Step(At(2.0, 12f, 68f, Walk(0f, 70f)));

        run.Lose("emptied", 9.0);

        Assert.AreEqual("enemy-close", run.ReleaseReason);
        Assert.AreEqual(ThrowerReleaseKind.Rule, run.ReleaseKind);
    }
}
