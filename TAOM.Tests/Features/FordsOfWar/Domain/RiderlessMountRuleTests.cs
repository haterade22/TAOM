using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.Warg;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>A faded horse with a rider still attached crashed the game (crash bundle 2026-11-03, native +0x6E5BCD), so a horse goes only when nobody can be on it.</summary>
[TestClass]
public class RiderlessMountRuleTests
{
    [TestMethod]
    [DataRow("horse")]
    [DataRow("warg")]
    [DataRow("fell_warg")]
    [DataRow("dire_wolf")]
    public void IsSwept_HorsesAndWargs_Go(string monster) => Assert.IsTrue(RiderlessMountRule.IsSwept(monster));

    [TestMethod]
    [DataRow("taom_mumakil")]
    [DataRow("taom_war_elephant")]
    [DataRow("spider")]
    [DataRow(null)]
    public void IsSwept_OtherMounts_Stay(string? monster) => Assert.IsFalse(RiderlessMountRule.IsSwept(monster));

    [TestMethod]
    public void Wargs_AreEveryMonsterTheWargTreeTreatsAsAWarg() =>
        CollectionAssert.AreEquivalent(WargConfig.WargMonsterIds.ToArray(), RiderlessMountRule.Wargs);

    [TestMethod]
    public void MayFade_AHorseThatJustLostItsRider_Stays() =>
        Assert.IsFalse(RiderlessMountRule.MayFade(RiderlessMountRule.WaitSeconds - 0.1, false));

    [TestMethod]
    public void MayFade_AHorseEmptyForLongEnough_Goes() =>
        Assert.IsTrue(RiderlessMountRule.MayFade(RiderlessMountRule.WaitSeconds, false));

    [TestMethod]
    public void MayFade_AHorseASoldierHoldsOrHasChosen_Stays_HoweverLongItStoodEmpty() =>
        Assert.IsFalse(RiderlessMountRule.MayFade(RiderlessMountRule.WaitSeconds * 10, true));
}
