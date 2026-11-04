using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using static TAOM.Tests.Features.FordsOfWar.Domain.FordsMatchFixture;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>Past a small assault more bodies add nothing to a tower's damage, so a cheap horde cannot take a tower in seconds. Heroes and siege units are what breaks a tower faster.</summary>
[TestClass]
public class TowerAssaultLimitTests
{
    private const string Tower = "a";

    [TestMethod]
    public void Take_InsideTheRate_PassesTheWholeBlow()
    {
        Assert.AreEqual(30, new TowerAssaultLimit(50f).Take(Tower, 30, 10f));
    }

    [TestMethod]
    public void Take_PastTheRate_CutsTheBlowToWhatIsLeft_ThenToNothing()
    {
        var limit = new TowerAssaultLimit(50f);

        Assert.AreEqual(30, limit.Take(Tower, 30, 10f));
        Assert.AreEqual(20, limit.Take(Tower, 30, 10f));
        Assert.AreEqual(0, limit.Take(Tower, 30, 10f));
    }

    [TestMethod]
    public void Take_TimeRefillsTheBudget_UpToOneSecondsWorth()
    {
        var limit = new TowerAssaultLimit(50f);
        limit.Take(Tower, 50, 10f);

        Assert.AreEqual(25, limit.Take(Tower, 50, 10.5f), "half a second earns 25");
        Assert.AreEqual(50, limit.Take(Tower, 500, 100f), "a long quiet earns no more than one second's worth");
    }

    [TestMethod]
    public void Take_EachTowerHasItsOwnBudget()
    {
        var limit = new TowerAssaultLimit(50f);
        limit.Take(Tower, 50, 10f);

        Assert.AreEqual(50, limit.Take("b", 50, 10f));
    }

    [TestMethod]
    public void Take_AZeroRate_MeansNoLimit()
    {
        var limit = new TowerAssaultLimit(0f);

        Assert.AreEqual(100000, limit.Take(Tower, 100000, 10f));
    }

    [TestMethod]
    public void Take_ABackwardClock_AddsNothing()
    {
        var limit = new TowerAssaultLimit(50f);
        limit.Take(Tower, 50, 10f);

        Assert.AreEqual(0, limit.Take(Tower, 50, 9f));
    }

    private static FordsMatch Match(float perSecond) =>
        new(Config(c => c with { Towers = c.Towers with { AssaultDamagePerSecond = perSecond } }), Layout(), Factions(), Slots("slow"), new SeededFordsRandom(7));

    private static TowerHit Blow(UnitRole role, int raw = 30) => new(FordsTestLayout.WestOuter0, false, role, raw, MatchSide.East);

    [TestMethod]
    public void AdjustTowerDamage_TooManyBlowsAtOnce_StopAtTheLimit_AndSayWhy()
    {
        var match = Match(50f);
        var before = match.GetView(WestSlot).Lanes[0].Towers[0].HitPoints;

        Assert.AreEqual(30, match.AdjustTowerDamage(Blow(UnitRole.Infantry), out var first));
        Assert.AreEqual(TowerDamageReason.Applied, first);
        Assert.AreEqual(20, match.AdjustTowerDamage(Blow(UnitRole.Infantry), out _));
        Assert.AreEqual(0, match.AdjustTowerDamage(Blow(UnitRole.Infantry), out var third));
        Assert.AreEqual(TowerDamageReason.Crowded, third);
        Assert.AreEqual(before - 50, match.GetView(WestSlot).Lanes[0].Towers[0].HitPoints, "only what the limit passed reaches the tower");
    }

    [TestMethod]
    public void AdjustTowerDamage_HeroesAndSiege_PassTheLimit_AndDoNotSpendIt()
    {
        var match = Match(50f);
        match.AdjustTowerDamage(Blow(UnitRole.Infantry, 50), out _);

        Assert.IsTrue(match.AdjustTowerDamage(Blow(UnitRole.Hero), out var hero) > 0);
        Assert.AreEqual(TowerDamageReason.Applied, hero);
        Assert.IsTrue(match.AdjustTowerDamage(Blow(UnitRole.Siege), out var siege) > 0);
        Assert.AreEqual(TowerDamageReason.Applied, siege);
        Assert.AreEqual(0, match.AdjustTowerDamage(Blow(UnitRole.Infantry), out _), "the budget is still spent");

        var fresh = Match(50f);
        fresh.AdjustTowerDamage(Blow(UnitRole.Hero, 500), out _);
        Assert.AreEqual(30, fresh.AdjustTowerDamage(Blow(UnitRole.Infantry), out _), "a hero's blow spends none of it");
    }

    [TestMethod]
    public void ReasonFor_ChangesNoBudget()
    {
        var match = Match(50f);

        for (var i = 0; i < 5; i++)
            Assert.AreEqual(TowerDamageReason.Applied, match.ReasonFor(Blow(UnitRole.Infantry, 50)));

        Assert.AreEqual(50, match.AdjustTowerDamage(Blow(UnitRole.Infantry, 50), out _));
    }
}
