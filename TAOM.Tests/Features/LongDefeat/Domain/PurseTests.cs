using System.Linq;
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

        Assert.IsTrue(p.TrySpend(100, PurseSink.Packs));

        Assert.AreEqual(0, p.Balance);
        Assert.AreEqual(100, p.Spent);
    }

    [TestMethod]
    public void TrySpend_BooksEachSpendToItsSink_AndTheSinksSumToSpent()
    {
        var p = new Purse(5000);

        p.TrySpend(180, PurseSink.Packs);
        p.TrySpend(200, PurseSink.Gate);
        p.TrySpend(250, PurseSink.Towers);
        p.TrySpend(150, PurseSink.FieldCare);
        p.TrySpend(350, PurseSink.Volley);
        p.TrySpend(20, PurseSink.Packs);
        p.TrySpend(99_999, PurseSink.Gate);

        Assert.AreEqual(200, p.SpentOn(PurseSink.Packs));
        Assert.AreEqual(200, p.SpentOn(PurseSink.Gate), "a refused spend books nothing");
        Assert.AreEqual(250, p.SpentOn(PurseSink.Towers));
        Assert.AreEqual(150, p.SpentOn(PurseSink.FieldCare));
        Assert.AreEqual(350, p.SpentOn(PurseSink.Volley));
        Assert.AreEqual(p.Spent, System.Enum.GetValues(typeof(PurseSink)).Cast<PurseSink>().Sum(p.SpentOn));
        Assert.AreEqual(p.Start + p.TotalEarned - p.Spent, p.Balance);
    }

    [TestMethod]
    public void SpentOn_ASinkNothingWasSpentOn_IsZero()
    {
        Assert.AreEqual(0, new Purse(10).SpentOn(PurseSink.Volley));
    }

    [DataTestMethod]
    [DataRow(101)]
    [DataRow(0)]
    [DataRow(-1)]
    public void TrySpend_MoreThanBalanceOrNotPositive_RefusesAndKeepsTheBalance(int amount)
    {
        var p = new Purse(100);

        Assert.IsFalse(p.TrySpend(amount, PurseSink.Packs));

        Assert.AreEqual(100, p.Balance);
        Assert.AreEqual(0, p.Spent);
    }

    [TestMethod]
    public void Balance_AlwaysEqualsStartPlusEarnedMinusSpent()
    {
        var p = new Purse(600);

        p.Earn(PurseSource.Bounty, 30);
        p.TrySpend(180, PurseSink.Packs);
        p.Earn(PurseSource.EarlyStart, 120);
        p.TrySpend(10_000, PurseSink.Packs);
        p.TrySpend(200, PurseSink.Packs);

        Assert.AreEqual(p.Start + p.TotalEarned - p.Spent, p.Balance);
    }
}
