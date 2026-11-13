using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// The opening split of the defenders (Mike, 2026-11-12): the shares a fortress row may give, the size of each group, and the hold that keeps the groups in place until the
/// first contact of a wave, then hands them back to the siege AI.
/// </summary>
[TestClass]
public class DefenderSplitTests
{
    [TestMethod]
    public void TryCreate_TakesSharesInsideTheRange()
    {
        Assert.IsTrue(SplitShares.TryCreate(0.7f, 0.7f, out var shares));
        Assert.AreEqual(0.7f, shares!.WallFoot);
        Assert.AreEqual(0.7f, shares.LowArchers);
    }

    [DataTestMethod]
    [DataRow(0f, 0.7f)]
    [DataRow(1f, 0.7f)]
    [DataRow(0.7f, 0.01f)]
    [DataRow(0.7f, 0.99f)]
    [DataRow(float.NaN, 0.7f)]
    [DataRow(0.7f, float.PositiveInfinity)]
    public void TryCreate_RefusesAShareThatEmptiesAGroupOrIsNotANumber(float wall, float low)
    {
        Assert.IsFalse(SplitShares.TryCreate(wall, low, out var shares));
        Assert.IsNull(shares);
    }

    [DataTestMethod]
    [DataRow(100, 0.7f, 70)]
    [DataRow(125, 0.7f, 88)]
    [DataRow(60, 0.7f, 42)]
    [DataRow(10, 0.7f, 7)]
    [DataRow(2, 0.7f, 1)]
    [DataRow(2, 0.95f, 1)]
    [DataRow(3, 0.05f, 1)]
    public void FirstGroup_RoundsTheShareAndLeavesOneInEachGroup(int total, float share, int expected)
    {
        Assert.AreEqual(expected, DefenderSplit.FirstGroup(total, share));
    }

    [DataTestMethod]
    [DataRow(0, 0)]
    [DataRow(1, 1)]
    [DataRow(-4, 0)]
    public void FirstGroup_ASoleSoldierStaysInTheFirstGroup(int total, int expected)
    {
        Assert.AreEqual(expected, DefenderSplit.FirstGroup(total, 0.7f));
    }

    [TestMethod]
    public void Contact_IsACountThatRoseSinceTheHold()
    {
        Assert.IsFalse(DefenderSplit.Contact(new[] { 0, 0, 0 }, new[] { 0, 0, 0 }));
        Assert.IsTrue(DefenderSplit.Contact(new[] { 0, 0, 0 }, new[] { 0, 1, 0 }));
        Assert.IsFalse(DefenderSplit.Contact(new[] { 2, 0, 0 }, new[] { 2, 0, 0 }), "a count already up at the hold is not new contact");
        Assert.IsFalse(DefenderSplit.Contact(new[] { 2, 0, 0 }, new[] { 1, 0, 0 }), "a count that fell is not contact");
    }

    [TestMethod]
    public void Contact_AListLengthThatChangedIsContact()
    {
        Assert.IsTrue(DefenderSplit.Contact(new[] { 0, 0 }, new[] { 0, 0, 0 }));
        Assert.IsTrue(DefenderSplit.Contact(new[] { 0, 0, 0 }, new List<int>()));
    }

    [TestMethod]
    public void Hold_SplitsOnceTheGarrisonIsIn()
    {
        var hold = new SplitHold();
        Assert.AreEqual(SplitStep.None, hold.Next(RunPhase.Preparation, true, false));
        Assert.AreEqual(SplitStep.Split, hold.Next(RunPhase.Preparation, false, false));
        Assert.IsTrue(hold.Held);
        Assert.AreEqual(SplitStep.None, hold.Next(RunPhase.Preparation, false, false));
    }

    [TestMethod]
    public void Hold_SplitsAgainWhenAPackHasMustered()
    {
        var hold = new SplitHold();
        hold.Next(RunPhase.Preparation, false, false);
        Assert.AreEqual(SplitStep.None, hold.Next(RunPhase.Preparation, true, false));
        Assert.AreEqual(SplitStep.Resplit, hold.Next(RunPhase.Preparation, false, false), "a split again keeps the hold's clock and counts");
    }

    [TestMethod]
    public void Hold_HandsBackAtContactAndWaitsForTheNextCalm()
    {
        var hold = new SplitHold();
        hold.Next(RunPhase.Preparation, false, false);
        Assert.AreEqual(SplitStep.None, hold.Next(RunPhase.Wave, false, false));
        Assert.AreEqual(SplitStep.Release, hold.Next(RunPhase.Wave, false, true));
        Assert.IsFalse(hold.Held);
        Assert.AreEqual(SplitStep.None, hold.Next(RunPhase.Wave, false, false), "the AI keeps the wave once it has it");
        Assert.AreEqual(SplitStep.Split, hold.Next(RunPhase.Intermission, false, false));
    }

    [TestMethod]
    public void Hold_AWaveBeganWithAPackStillToComeSplitsWhenItIsIn()
    {
        var hold = new SplitHold();
        hold.Next(RunPhase.Preparation, false, false);
        Assert.AreEqual(SplitStep.None, hold.Next(RunPhase.Wave, true, false));
        Assert.AreEqual(SplitStep.Resplit, hold.Next(RunPhase.Wave, false, false));
    }

    [TestMethod]
    public void Hold_NoSplitInAWaveTheAIAlreadyHas()
    {
        var hold = new SplitHold();
        Assert.AreEqual(SplitStep.None, hold.Next(RunPhase.Wave, false, false), "a run that starts in a wave (a bot run at a later wave) leaves that wave to the AI");
        Assert.AreEqual(SplitStep.Split, hold.Next(RunPhase.Intermission, false, false));
    }

    [TestMethod]
    public void Hold_ContactInACalmIsIgnored()
    {
        var hold = new SplitHold();
        hold.Next(RunPhase.Preparation, false, false);
        Assert.AreEqual(SplitStep.None, hold.Next(RunPhase.Intermission, false, true));
        Assert.IsTrue(hold.Held);
    }

    [TestMethod]
    public void Drop_EndsTheHoldWithNoReleaseCall()
    {
        var hold = new SplitHold();
        hold.Next(RunPhase.Preparation, false, false);
        hold.Next(RunPhase.Wave, false, false);
        hold.Drop();
        Assert.IsFalse(hold.Held);
        Assert.AreEqual(SplitStep.None, hold.Next(RunPhase.Wave, false, true));
        Assert.AreEqual(SplitStep.Split, hold.Next(RunPhase.Intermission, false, false));
    }

    [TestMethod]
    public void Hold_AnEndedRunDoesNothing()
    {
        var hold = new SplitHold();
        Assert.AreEqual(SplitStep.None, hold.Next(RunPhase.Ended, false, false));
    }
}
