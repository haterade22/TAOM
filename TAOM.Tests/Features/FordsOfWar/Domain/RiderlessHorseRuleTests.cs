using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>A faded horse with a rider still attached crashed the game (crash bundle 2026-11-03, native +0x6E5BCD), so a horse goes only when nobody can be on it.</summary>
[TestClass]
public class RiderlessHorseRuleTests
{
    [TestMethod]
    public void MayFade_AHorseThatJustLostItsRider_Stays() =>
        Assert.IsFalse(RiderlessHorseRule.MayFade(RiderlessHorseRule.WaitSeconds - 0.1, false));

    [TestMethod]
    public void MayFade_AHorseEmptyForLongEnough_Goes() =>
        Assert.IsTrue(RiderlessHorseRule.MayFade(RiderlessHorseRule.WaitSeconds, false));

    [TestMethod]
    public void MayFade_AHorseASoldierHoldsOrHasChosen_Stays_HoweverLongItStoodEmpty() =>
        Assert.IsFalse(RiderlessHorseRule.MayFade(RiderlessHorseRule.WaitSeconds * 10, true));
}
