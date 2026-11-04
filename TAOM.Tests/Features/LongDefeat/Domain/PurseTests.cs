using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

[TestClass]
public class PurseTests
{
    [TestMethod]
    public void New_StartsAtTheStartingGold()
    {
        var p = new Purse(600);

        Assert.AreEqual(600, p.Balance);
        Assert.AreEqual(600, p.Start);
    }

    [TestMethod]
    public void Earn_AddsToBalanceAndTheSource()
    {
        var p = new Purse(0);

        p.Earn(PurseSource.Bounty, 4);
        p.Earn(PurseSource.Bounty, 6);
        p.Earn(PurseSource.WaveBonus, 200);

        Assert.AreEqual(210, p.Balance);
        Assert.AreEqual(10, p.EarnedFrom(PurseSource.Bounty));
        Assert.AreEqual(200, p.EarnedFrom(PurseSource.WaveBonus));
        Assert.AreEqual(0, p.EarnedFrom(PurseSource.EarlyStart));
        Assert.AreEqual(210, p.TotalEarned);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-5)]
    public void Earn_NothingOrNegative_ChangesNothing(int amount)
    {
        var p = new Purse(10);

        p.Earn(PurseSource.Bounty, amount);

        Assert.AreEqual(10, p.Balance);
        Assert.AreEqual(0, p.TotalEarned);
    }

    [TestMethod]
    public void TrySpend_WithinBalance_Spends()
    {
        var p = new Purse(100);

        Assert.IsTrue(p.TrySpend(100));

        Assert.AreEqual(0, p.Balance);
        Assert.AreEqual(100, p.Spent);
    }

    [DataTestMethod]
    [DataRow(101)]
    [DataRow(0)]
    [DataRow(-1)]
    public void TrySpend_MoreThanBalanceOrNotPositive_RefusesAndKeepsTheBalance(int amount)
    {
        var p = new Purse(100);

        Assert.IsFalse(p.TrySpend(amount));

        Assert.AreEqual(100, p.Balance);
        Assert.AreEqual(0, p.Spent);
    }

    [TestMethod]
    public void Balance_AlwaysEqualsStartPlusEarnedMinusSpent()
    {
        var p = new Purse(600);

        p.Earn(PurseSource.Bounty, 30);
        p.TrySpend(180);
        p.Earn(PurseSource.EarlyStart, 120);
        p.TrySpend(10_000);
        p.TrySpend(200);

        Assert.AreEqual(p.Start + p.TotalEarned - p.Spent, p.Balance);
    }
}
