using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>The judgments the S4 and S5 trial steps pass on (build-spec section 12), with no engine type so each edge is pinned.</summary>
[TestClass]
public class SeatTrialFightRulesTests
{
    [DataTestMethod]
    [DataRow(1.01f, true)]
    [DataRow(1.0f, false)]
    [DataRow(0.2f, false)]
    [DataRow(0.0f, false)]
    [DataRow(37.5f, true)]
    public void FoughtOn_NeedsMoreThanAMetre(float moved, bool fought)
    {
        Assert.AreEqual(fought, SeatTrialRules.FoughtOn(moved), "moved " + moved);
    }

    [TestMethod]
    public void FoughtOn_ADistanceThatIsNotANumber_ProvesNothing()
    {
        Assert.IsFalse(SeatTrialRules.FoughtOn(float.NaN));
        Assert.IsFalse(SeatTrialRules.FoughtOn(float.PositiveInfinity));
    }

    [DataTestMethod]
    [DataRow(0f, true)]
    [DataRow(0.01f, true)]
    [DataRow(0.011f, false)]
    [DataRow(50f, false)]
    [DataRow(-1f, false)]
    public void AtMoraleFloor_NeedsAMoraleBetweenZeroAndTheEnginesFloor(float morale, bool atFloor)
    {
        Assert.AreEqual(atFloor, SeatTrialRules.AtMoraleFloor(morale), "morale " + morale);
    }

    [TestMethod]
    public void AtMoraleFloor_AMoraleThatIsNotANumber_ProvesNothing()
    {
        Assert.IsFalse(SeatTrialRules.AtMoraleFloor(float.NaN));
        Assert.IsFalse(SeatTrialRules.AtMoraleFloor(float.NegativeInfinity));
    }

    [TestMethod]
    public void CommandHandedOver_NeedsTheControllerTheCountAndNoFormationOnTheAi()
    {
        Assert.IsTrue(SeatTrialRules.CommandHandedOver(3, new SeatHandOverReading(true, 3, 0)));
        Assert.IsFalse(SeatTrialRules.CommandHandedOver(3, new SeatHandOverReading(false, 3, 0)), "the controller names someone else");
        Assert.IsFalse(SeatTrialRules.CommandHandedOver(3, new SeatHandOverReading(true, 2, 0)), "fewer than the hero owned");
        Assert.IsFalse(SeatTrialRules.CommandHandedOver(3, new SeatHandOverReading(true, 4, 0)), "more than the hero owned");
        Assert.IsFalse(SeatTrialRules.CommandHandedOver(3, new SeatHandOverReading(true, 3, 1)), "one still on the AI");
    }

    [TestMethod]
    public void CommandHandedOver_AHeroWhoOwnedNothing_ProvesNoHandOver()
    {
        Assert.IsFalse(SeatTrialRules.CommandHandedOver(0, new SeatHandOverReading(true, 0, 0)));
    }

    [TestMethod]
    public void TheWindows_AreTheOnesTheContractNames()
    {
        Assert.AreEqual(5f, SeatTrialRules.FightWindowSeconds);
        Assert.AreEqual(2.5f, SeatTrialRules.MoraleWindowSeconds);
        Assert.AreEqual(3f, SeatTrialRules.HandOffWaitSeconds);
        Assert.AreEqual(1f, SeatTrialRules.FoughtOnMetres);
    }
}
