using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>What a kill, a tower and a hero are worth.</summary>
[TestClass]
public class BountyRulesTests
{
    private static readonly FordsEconomyConfig Economy = FordsMatchConfig.Default.Economy;

    [TestMethod]
    public void KillBounty_HighValue_IsFractionOfValue()
    {
        var rules = new BountyRules(Economy);

        Assert.AreEqual(35, rules.KillBounty(100));
    }

    [TestMethod]
    public void KillBounty_CheapUnit_PaysTheMinimum()
    {
        var rules = new BountyRules(Economy);

        Assert.AreEqual(Economy.MinKillBounty, rules.KillBounty(1));
        Assert.AreEqual(Economy.MinKillBounty, rules.KillBounty(0));
        Assert.AreEqual(Economy.MinKillBounty, rules.KillBounty(-10));
    }

    [TestMethod]
    public void KillBounty_RoundsToNearest()
    {
        var rules = new BountyRules(Economy with { KillBountyFraction = 0.5f, MinKillBounty = 0 });

        Assert.AreEqual(3, rules.KillBounty(5));
        Assert.AreEqual(2, rules.KillBounty(4));
    }

    [TestMethod]
    public void TowerBountyPerSlot_OneSlot_GetsTheFullBounty()
    {
        Assert.AreEqual(Economy.TowerBounty, new BountyRules(Economy).TowerBountyPerSlot(1));
    }

    [TestMethod]
    public void TowerBountyPerSlot_SeveralSlots_SplitsEqually()
    {
        Assert.AreEqual(Economy.TowerBounty / 3, new BountyRules(Economy).TowerBountyPerSlot(3));
    }

    [TestMethod]
    public void TowerBountyPerSlot_NoSlots_IsZero()
    {
        Assert.AreEqual(0, new BountyRules(Economy).TowerBountyPerSlot(0));
    }

    [TestMethod]
    public void HeroKillBounty_ReturnsConfiguredAmount()
    {
        Assert.AreEqual(Economy.HeroKillBounty, new BountyRules(Economy).HeroKillBounty);
    }
}
