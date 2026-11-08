using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>A faded horse with a rider still attached crashed the game (crash bundle 2026-11-03, native +0x6E5BCD), so a mount goes only when nobody can be on it.</summary>
[TestClass]
public class RiderlessMountRuleTests
{
    [TestMethod]
    public void MayFade_AMountThatJustLostItsRider_Stays() =>
        Assert.IsFalse(RiderlessMountRule.MayFade(RiderlessMountRule.WaitSeconds - 0.1, false));

    [TestMethod]
    public void MayFade_AMountEmptyForLongEnough_Goes() =>
        Assert.IsTrue(RiderlessMountRule.MayFade(RiderlessMountRule.WaitSeconds, false));

    [TestMethod]
    public void MayFade_AMountASoldierHoldsOrHasChosen_Stays_HoweverLongItStoodEmpty() =>
        Assert.IsFalse(RiderlessMountRule.MayFade(RiderlessMountRule.WaitSeconds * 10, true));
}
