using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment.Content;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Field report 2: "you are either not paid or it doesn't show up as a positive in the expected
/// change part of your wallet". Both halves were true — the 500-gold commander reserve silently
/// turns the wage into arrears, and the gold that DID arrive was transferred with notifications
/// disabled and a summary nobody read.
/// </summary>
[TestClass]
public class WageReportPolicyTests
{
    [TestMethod]
    public void PaidDay_Announces()
    {
        var report = WageReportPolicy.Describe(new WageDecision { PaidFromCommander = 22 });

        Assert.AreEqual(22, report.Received);
        Assert.IsTrue(report.ShouldAnnouncePayment);
        Assert.IsFalse(report.ShouldWarn);
    }

    [TestMethod]
    public void ClearedArrears_ReportAsOneNumber()
    {
        // The player watches one purse. A day that pays today's wage AND clears back pay changes it
        // once, so it is reported once — splitting it reads as two payments that never happened.
        var report = WageReportPolicy.Describe(
            new WageDecision { PaidFromCommander = 22, ArrearsReleased = 44 });

        Assert.AreEqual(66, report.Received);
    }

    [TestMethod]
    public void MintedWage_CountsAsReceived()
    {
        // The MCM escape hatch (PayFromCommanderGold=false) mints instead of drawing on the lord.
        // It is still gold in the purse and still owes the player a message.
        var report = WageReportPolicy.Describe(new WageDecision { Minted = 14 });

        Assert.AreEqual(14, report.Received);
        Assert.IsTrue(report.ShouldAnnouncePayment);
    }

    [TestMethod]
    public void BrokeCommander_WarnsInsteadOfSayingNothing()
    {
        // THE reported bug. A lord under the 500 reserve pays nothing, and before this the day was
        // indistinguishable from a paid one.
        var report = WageReportPolicy.Describe(new WageDecision { NewlyDeferred = 22 });

        Assert.AreEqual(0, report.Received);
        Assert.IsFalse(report.ShouldAnnouncePayment);
        Assert.IsTrue(report.ShouldWarn);
        Assert.AreEqual(22, report.Deferred);
    }

    [TestMethod]
    public void ForfeitedPay_Warns()
    {
        // Arrears past the cap are destroyed. That must never be silent — it is the player losing
        // earned money.
        var report = WageReportPolicy.Describe(new WageDecision { NewlyDeferred = 5, Forfeited = 17 });

        Assert.IsTrue(report.ShouldWarn);
        Assert.AreEqual(17, report.Forfeited);
    }

    [TestMethod]
    public void NothingHappened_SaysNothing()
    {
        // A day with no wage due (commander unavailable, contract lapsed) must not toast.
        var report = WageReportPolicy.Describe(new WageDecision());

        Assert.IsFalse(report.ShouldAnnouncePayment);
        Assert.IsFalse(report.ShouldWarn);
    }

    [TestMethod]
    public void NullDecision_SaysNothing()
    {
        var report = WageReportPolicy.Describe(null);

        Assert.IsFalse(report.ShouldAnnouncePayment);
        Assert.IsFalse(report.ShouldWarn);
    }
}
