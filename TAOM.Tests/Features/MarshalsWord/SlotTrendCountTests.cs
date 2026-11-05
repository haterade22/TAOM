using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>One held soldier's slot trend (build-spec 9.2): the steered interval between two hold reads, and the counts the driver takes.</summary>
[TestClass]
public class SlotTrendCountTests
{
    private const float Far = 5f;

    [TestMethod]
    public void SlotTrendCount_SteerSteerRead_UsesTheFirstSteersDistance()
    {
        var trend = new SlotTrendCount();
        trend.Steer(Far);
        trend.Steer(1f);

        trend.Read(Far - 1f, strikingBack: false, inTheLine: true);

        Assert.AreEqual(new SlotCounts(Closer: 1, Farther: 0, Steered: 1, Loose: 0), trend.Take(), "nearer than the first steer; the second steer would have read farther");
    }

    [TestMethod]
    public void SlotTrendCount_ReadWithNoSteer_IsLoose()
    {
        var trend = new SlotTrendCount();

        trend.Read(Far, strikingBack: false, inTheLine: true);

        Assert.AreEqual(new SlotCounts(0, 0, 0, Loose: 1), trend.Take());
    }

    [TestMethod]
    public void SlotTrendCount_StruckSoldier_IsLeftOut()
    {
        var trend = new SlotTrendCount();
        trend.Steer(Far);

        trend.Read(Far - 2f, strikingBack: true, inTheLine: true);

        Assert.AreEqual(default(SlotCounts), trend.Take(), "a soldier in his strike back window counts in neither side, and a steered read is not loose");
    }

    [TestMethod]
    public void SlotTrendCount_SteeredRead_FartherAndSteadyAreCounted_TheDeadBandCountsAsSteered()
    {
        var trend = new SlotTrendCount();
        trend.Steer(Far);
        trend.Read(Far + 1f, strikingBack: false, inTheLine: true);
        trend.Steer(Far);
        trend.Read(Far + MarshalTuning.SlotTrendMetres / 2f, strikingBack: false, inTheLine: true);

        Assert.AreEqual(new SlotCounts(Closer: 0, Farther: 1, Steered: 2, Loose: 0), trend.Take());
    }

    [TestMethod]
    public void SlotTrendCount_ASoldierOutOfTheLine_IsNeverLoose()
    {
        var trend = new SlotTrendCount();

        trend.Read(Far, strikingBack: false, inTheLine: false);

        Assert.AreEqual(default(SlotCounts), trend.Take());
    }

    [TestMethod]
    public void SlotTrendCount_QualificationLostAfterASteer_CountsNoCloserFartherOrSteeredRead()
    {
        var trend = new SlotTrendCount();
        trend.Steer(Far);

        trend.Read(Far - 2f, strikingBack: false, inTheLine: false);

        Assert.AreEqual(default(SlotCounts), trend.Take(), "a soldier who left the line between his steer and his read adds to neither side of the trend");
    }

    [TestMethod]
    public void SlotTrendCount_QualificationLostAfterASteer_FartherIsNotCountedEither()
    {
        var trend = new SlotTrendCount();
        trend.Steer(Far);

        trend.Read(Far + 2f, strikingBack: false, inTheLine: false);

        Assert.AreEqual(default(SlotCounts), trend.Take());
    }

    [TestMethod]
    public void SlotTrendCount_ACountThatIsNotYetTaken_StillDrainsAfterTheSoldierLeavesTheLine()
    {
        var trend = new SlotTrendCount();
        trend.Steer(Far);
        trend.Read(Far - 2f, strikingBack: false, inTheLine: true);
        trend.Steer(Far);

        trend.Read(Far - 2f, strikingBack: false, inTheLine: false);

        Assert.AreEqual(new SlotCounts(Closer: 1, Farther: 0, Steered: 1, Loose: 0), trend.Take(), "the read made in the line is kept, and the read made out of it adds nothing");
    }

    [TestMethod]
    public void SlotTrendCount_TheOpenIntervalOfASoldierWhoLeftTheLine_IsDropped_SoHisNextReadInTheLineIsLoose()
    {
        var trend = new SlotTrendCount();
        trend.Steer(Far);
        trend.Read(Far - 2f, strikingBack: false, inTheLine: false);

        trend.Read(Far, strikingBack: false, inTheLine: true);

        Assert.AreEqual(new SlotCounts(0, 0, 0, Loose: 1), trend.Take(), "the steer was not kept for a later read");
    }

    [TestMethod]
    public void SlotTrendCount_ADistanceThatIsNotANumber_EndsTheIntervalAndCountsNothing()
    {
        var trend = new SlotTrendCount();
        trend.Steer(Far);
        trend.Read(float.NaN, strikingBack: false, inTheLine: true);

        trend.Read(Far, strikingBack: false, inTheLine: true);

        Assert.AreEqual(new SlotCounts(0, 0, 0, Loose: 1), trend.Take(), "the old steer is gone, so the next read has no replaced tick before it");
    }

    [TestMethod]
    public void SlotTrendCount_TakeHandsOverEachCountOnce()
    {
        var trend = new SlotTrendCount();
        trend.Read(Far, strikingBack: false, inTheLine: true);
        Assert.AreEqual(1, trend.Take().Loose);

        Assert.AreEqual(default(SlotCounts), trend.Take());
    }

    [TestMethod]
    public void SlotTrendCount_Forget_DropsTheOpenIntervalAndTheUntakenCounts()
    {
        var trend = new SlotTrendCount();
        trend.Read(Far, strikingBack: false, inTheLine: true);
        trend.Steer(Far);

        trend.Forget();
        trend.Read(Far - 2f, strikingBack: false, inTheLine: true);

        Assert.AreEqual(new SlotCounts(0, 0, 0, Loose: 1), trend.Take(), "the steer and the earlier loose read are gone; the new read is loose");
    }

    [TestMethod]
    public void SlotCounts_Plus_AddsEachCount()
    {
        var sum = new SlotCounts(1, 2, 3, 4) + new SlotCounts(10, 20, 30, 40);

        Assert.AreEqual(new SlotCounts(11, 22, 33, 44), sum);
    }
}
