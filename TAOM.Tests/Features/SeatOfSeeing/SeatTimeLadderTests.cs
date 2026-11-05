using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>The mission time ladder of stage S6 (build-spec section 10): the six levels, the ceiling, the per-frame rule and the two hand-offs, with no engine type.</summary>
[TestClass]
public class SeatTimeLadderTests
{
    private const int Quarter = 0, Half = 1, Normal = 2, Double = 3, Four = 4, Ten = 5;

    [TestMethod]
    public void Levels_AreTheSixTheContractNames()
    {
        CollectionAssert.AreEqual(new[] { 0.25f, 0.5f, 1f, 2f, 4f, 10f }, new[] { SeatTimeLadder.Level(0), SeatTimeLadder.Level(1), SeatTimeLadder.Level(2),
            SeatTimeLadder.Level(3), SeatTimeLadder.Level(4), SeatTimeLadder.Level(5) });
        CollectionAssert.AreEqual(new[] { "0.25", "0.5", "1", "2", "4", "10" }, new[] { SeatTimeLadder.Name(0), SeatTimeLadder.Name(1), SeatTimeLadder.Name(2),
            SeatTimeLadder.Name(3), SeatTimeLadder.Name(4), SeatTimeLadder.Name(5) });
        Assert.AreEqual(Normal, SeatTimeLadder.Normal);
    }

    [DataTestMethod]
    [DataRow(Quarter, true, false, false)]
    [DataRow(Half, true, false, false)]
    [DataRow(Normal, false, false, false)]
    [DataRow(Double, false, true, false)]
    [DataRow(Four, false, true, false)]
    [DataRow(Ten, false, false, true)]
    public void Modes_SlowIsBelowOne_TwoAndFourUseTheFixedStep_TenIsTheEnginesFastForward(int index, bool slow, bool fixedStep, bool fastForward)
    {
        Assert.AreEqual(slow, SeatTimeLadder.IsSlow(index));
        Assert.AreEqual(fixedStep, SeatTimeLadder.UsesFixedStep(index));
        Assert.AreEqual(fastForward, SeatTimeLadder.IsFastForward(index));
    }

    [TestMethod]
    public void RequestId_IsClearOfTheEnginesFour()
    {
        foreach (var engine in SeatTrialRules.EngineRequestIds)
            Assert.AreNotEqual(engine, SeatTimeLadder.RequestId);
    }

    [TestMethod]
    public void EngineRequestIds_AreTheFourTheEngineHolds_SoAnOpenPanelOrWheelMarksARatioAsNotTheSeats()
    {
        CollectionAssert.AreEquivalent(new[] { 813, 624, 864, 1121 }, SeatTrialRules.EngineRequestIds);
    }

    [DataTestMethod]
    [DataRow(Quarter, Half, Ten)]
    [DataRow(Half, Normal, Ten)]
    [DataRow(Normal, Double, Ten)]
    [DataRow(Double, Four, Ten)]
    [DataRow(Four, Ten, Ten)]
    [DataRow(Ten, Ten, Ten)]
    [DataRow(Four, Four, Four)]
    [DataRow(Normal, Normal, Normal)]
    [DataRow(Ten, Four, Four)]
    public void Step_Quicken_MovesUpOneAndStopsAtTheCeiling(int from, int to, int ceiling)
    {
        Assert.AreEqual(to, SeatTimeLadder.Step(from, SeatTimeKey.Quicken, ceiling));
    }

    [DataTestMethod]
    [DataRow(Normal, Half, Ten)]
    [DataRow(Half, Quarter, Ten)]
    [DataRow(Quarter, Quarter, Ten)]
    [DataRow(Ten, Four, Ten)]
    [DataRow(Ten, Four, Four)]
    [DataRow(Four, Double, Four)]
    [DataRow(Ten, Normal, Normal)]
    public void Step_Slow_MovesDownOneAndNeverBelowTheQuarter(int from, int to, int ceiling)
    {
        Assert.AreEqual(to, SeatTimeLadder.Step(from, SeatTimeKey.Slow, ceiling));
    }

    [DataTestMethod]
    [DataRow(true, true, false, Ten)]
    [DataRow(false, false, false, Ten)]
    [DataRow(false, true, false, Four)]
    [DataRow(true, false, false, Ten)]
    [DataRow(true, true, true, Normal)]
    [DataRow(false, false, true, Normal)]
    public void Ceiling_TenNeedsTheSeatUpOrNoHero_AndAnotherClockHoldsTheLadderAtOne(bool seatUp, bool heroAlive, bool ownedElsewhere, int expected)
    {
        Assert.AreEqual(expected, SeatTimeLadder.Ceiling(seatUp, heroAlive, ownedElsewhere));
    }

    [DataTestMethod]
    [DataRow(Double, false, Double, (int)SeatTimeAction.Apply)]
    [DataRow(Four, false, Four, (int)SeatTimeAction.Apply)]
    [DataRow(Double, true, Ten, (int)SeatTimeAction.Adopt)]
    [DataRow(Four, true, Ten, (int)SeatTimeAction.Adopt)]
    [DataRow(Ten, true, Ten, (int)SeatTimeAction.None)]
    [DataRow(Ten, false, Normal, (int)SeatTimeAction.Stop)]
    [DataRow(Normal, true, Ten, (int)SeatTimeAction.Adopt)]
    [DataRow(Half, true, Ten, (int)SeatTimeAction.Adopt)]
    [DataRow(Quarter, true, Ten, (int)SeatTimeAction.Adopt)]
    [DataRow(Normal, false, Normal, (int)SeatTimeAction.None)]
    [DataRow(Half, false, Half, (int)SeatTimeAction.None)]
    public void Frame_ReadsTheEnginesFlagFirst_AnotherWritersFlagMakesTheSeatGiveUpWhatItHolds_AndAtTenSomeoneCancelled(int index, bool flag, int newIndex, int action)
    {
        var frame = SeatTimeLadder.Frame(index, flag);

        Assert.AreEqual(newIndex, frame.Index);
        Assert.AreEqual((SeatTimeAction)action, frame.Action);
    }

    [DataTestMethod]
    [DataRow(Ten, true, Four)]
    [DataRow(Ten, false, Ten)]
    [DataRow(Four, true, Four)]
    [DataRow(Half, true, Half)]
    public void AfterLeave_TenWithALivingHeroStepsDownToFour(int index, bool heroAlive, int expected)
    {
        Assert.AreEqual(expected, SeatTimeLadder.AfterLeave(index, heroAlive));
    }

    [DataTestMethod]
    [DataRow(Quarter, Quarter)]
    [DataRow(Half, Half)]
    [DataRow(Normal, Normal)]
    [DataRow(Double, Normal)]
    [DataRow(Four, Normal)]
    [DataRow(Ten, Normal)]
    public void WithoutFastTime_FastTimeIsCancelledAndSlowTimeStays(int index, int expected)
    {
        Assert.AreEqual(expected, SeatTimeLadder.WithoutFastTime(index));
    }

    [TestMethod]
    public void FastLevel_IsTheWholeNumberTheFordsRuleTakes()
    {
        Assert.AreEqual(2, SeatTimeLadder.FastLevel(Double));
        Assert.AreEqual(4, SeatTimeLadder.FastLevel(Four));
        Assert.AreEqual(10, SeatTimeLadder.FastLevel(Ten));
    }

    [TestMethod]
    public void State_IsCleanOnlyWhenEveryPieceIsOff()
    {
        Assert.IsTrue(new SeatTimeState(false, false, false, false).IsClean(otherClock: false));
        Assert.IsFalse(new SeatTimeState(true, false, false, false).IsClean(otherClock: false), "fast forward");
        Assert.IsFalse(new SeatTimeState(false, true, false, false).IsClean(otherClock: false), "fixed step");
        Assert.IsFalse(new SeatTimeState(false, false, true, false).IsClean(otherClock: false), "paused");
        Assert.IsFalse(new SeatTimeState(false, false, false, true).IsClean(otherClock: false), "the Seat's request");
    }

    [TestMethod]
    public void State_WhenAnotherViewOwnsTheClock_IgnoresTheFlagsItWritesEveryFrame()
    {
        Assert.IsTrue(new SeatTimeState(true, true, false, false).IsClean(otherClock: true), "Long Defeat's own speed is not the Seat's debt");
        Assert.IsFalse(new SeatTimeState(true, true, true, false).IsClean(otherClock: true), "a pause is the Seat's whatever the clock");
        Assert.IsFalse(new SeatTimeState(true, true, false, true).IsClean(otherClock: true), "the Seat's request is the Seat's whatever the clock");
    }

    [TestMethod]
    public void CancelTries_AreThreeInAll()
    {
        Assert.AreEqual(3, SeatTimeLadder.CancelTries);
    }
}
