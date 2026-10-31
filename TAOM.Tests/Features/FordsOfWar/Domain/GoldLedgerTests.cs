using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>Gold per commander slot: start, earn with a clamp, spend only what is there.</summary>
[TestClass]
public class GoldLedgerTests
{
    private static readonly FordsEconomyConfig Economy = FordsMatchConfig.Default.Economy;

    private static GoldLedger Ledger() => new(Economy, new[] { 1, 2 });

    [TestMethod]
    public void Balance_NewSlot_StartsAtStartingGold()
    {
        Assert.AreEqual(Economy.StartingGold, Ledger().Balance(1));
    }

    [TestMethod]
    public void Balance_UnknownSlot_IsZero()
    {
        Assert.AreEqual(0, Ledger().Balance(99));
    }

    [TestMethod]
    public void Earn_AddsToTheBalance()
    {
        var l = Ledger();

        var added = l.Earn(1, 50);

        Assert.AreEqual(50, added);
        Assert.AreEqual(Economy.StartingGold + 50, l.Balance(1));
    }

    [TestMethod]
    public void Earn_PastMaxGold_ClampsAndReportsWhatWasAdded()
    {
        var l = Ledger();

        var added = l.Earn(1, Economy.MaxGold * 2);

        Assert.AreEqual(Economy.MaxGold, l.Balance(1));
        Assert.AreEqual(Economy.MaxGold - Economy.StartingGold, added);
    }

    [TestMethod]
    public void Earn_NegativeAmount_IsIgnored()
    {
        var l = Ledger();

        Assert.AreEqual(0, l.Earn(1, -40));
        Assert.AreEqual(Economy.StartingGold, l.Balance(1));
    }

    [TestMethod]
    public void Earn_UnknownSlot_IsIgnored()
    {
        var l = Ledger();

        Assert.AreEqual(0, l.Earn(99, 40));
        Assert.AreEqual(0, l.Balance(99));
    }

    [TestMethod]
    public void TrySpend_Affordable_DeductsAndReturnsTrue()
    {
        var l = Ledger();

        Assert.IsTrue(l.TrySpend(1, 100));
        Assert.AreEqual(Economy.StartingGold - 100, l.Balance(1));
    }

    [TestMethod]
    public void TrySpend_ExactBalance_Succeeds()
    {
        var l = Ledger();

        Assert.IsTrue(l.TrySpend(1, Economy.StartingGold));
        Assert.AreEqual(0, l.Balance(1));
    }

    [TestMethod]
    public void TrySpend_TooExpensive_ChangesNothing()
    {
        var l = Ledger();

        Assert.IsFalse(l.TrySpend(1, Economy.StartingGold + 1));
        Assert.AreEqual(Economy.StartingGold, l.Balance(1));
    }

    [TestMethod]
    public void TrySpend_NegativeAmount_IsRefused()
    {
        var l = Ledger();

        Assert.IsFalse(l.TrySpend(1, -5));
        Assert.AreEqual(Economy.StartingGold, l.Balance(1));
    }

    [TestMethod]
    public void TrySpend_UnknownSlot_IsRefused()
    {
        Assert.IsFalse(Ledger().TrySpend(99, 1));
    }

    [TestMethod]
    public void Slots_AreIndependent()
    {
        var l = Ledger();

        l.TrySpend(1, 200);

        Assert.AreEqual(Economy.StartingGold, l.Balance(2));
    }
}
