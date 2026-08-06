using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CaravanTrade.Diagnostics;

namespace TAOM.Tests.Features.CaravanTrade;

/// <summary>
/// Pins the `taom.print_caravans` report.
///
/// The report's job is to answer one question in its first line — "how many caravans are stuck, and
/// why" — because the alternative is a human reading a dozen boolean flags per caravan and applying
/// the engine's gate precedence in their head. The per-caravan detail exists to confirm the verdict,
/// not to be the primary reading.
/// </summary>
[TestClass]
public class CaravanTradeCheatsFormatTests
{
    private static (CaravanGateSnapshot, CaravanGateVerdict) Entry(
        string name, CaravanGate gate, int gold = 9000, int cargo = 12)
    {
        var snapshot = new CaravanGateSnapshot
        {
            CaravanId = "caravan_" + name,
            CaravanName = name,
            CurrentSettlementId = "town_G3",
            TargetSettlementId = "town_G3",
            PartyTradeGold = gold,
            CargoItemCount = cargo,
        };

        var verdict = new CaravanGateVerdict
        {
            Gate = gate,
            WoundedFraction = 0.45f,
            EffectiveHourlyLeaveChance = 0f,
            Explanation = "test explanation",
        };

        return (snapshot, verdict);
    }

    [TestMethod]
    public void FormatReport_MixedGates_LeadsWithTheBlockedCount()
    {
        var report = CaravanGateReportFormatter.Format("Minas Tirith", new List<(CaravanGateSnapshot, CaravanGateVerdict)>
        {
            Entry("Aldamir", CaravanGate.Alerted),
            Entry("Faramir", CaravanGate.WoundedCannotLeave),
            Entry("Ingold", CaravanGate.NotBlocked),
        }, totalFound: 3, cap: 40);

        StringAssert.Contains(report, "Minas Tirith");
        StringAssert.Contains(report, "2 of the 3 shown are blocked");
    }

    /// <summary>
    /// The gate histogram is what turns this from a list into a diagnosis — five caravans all
    /// reading `Alerted` says "there is a battle nearby", which no single-caravan line conveys.
    /// </summary>
    [TestMethod]
    public void FormatReport_SeveralCaravansSameGate_GroupsThemInTheHistogram()
    {
        var report = CaravanGateReportFormatter.Format("Minas Tirith", new List<(CaravanGateSnapshot, CaravanGateVerdict)>
        {
            Entry("A", CaravanGate.Alerted),
            Entry("B", CaravanGate.Alerted),
            Entry("C", CaravanGate.NotBlocked),
        }, totalFound: 3, cap: 40);

        StringAssert.Contains(report, "Alerted: 2");
        StringAssert.Contains(report, "NotBlocked: 1");
    }

    [TestMethod]
    public void FormatReport_PerCaravanLine_CarriesTheDecisionInputs()
    {
        var report = CaravanGateReportFormatter.Format("Minas Tirith", new List<(CaravanGateSnapshot, CaravanGateVerdict)>
        {
            Entry("Aldamir", CaravanGate.NoViableDestination, gold: 0, cargo: 0),
        }, totalFound: 1, cap: 40);

        StringAssert.Contains(report, "Aldamir");
        StringAssert.Contains(report, "gold=0");
        StringAssert.Contains(report, "cargo=0");
        StringAssert.Contains(report, "test explanation");
    }

    /// <summary>
    /// A capped report that does not say it was capped reads as a complete census, which is exactly
    /// the "no silent caps" failure — the reader would conclude the other 60 caravans are fine.
    /// </summary>
    [TestMethod]
    public void FormatReport_MoreCaravansThanTheCap_SaysHowManyWereOmitted()
    {
        var report = CaravanGateReportFormatter.Format(null, new List<(CaravanGateSnapshot, CaravanGateVerdict)>
        {
            Entry("A", CaravanGate.Alerted),
        }, totalFound: 61, cap: 40);

        StringAssert.Contains(report, "21 more not shown");
    }

    [TestMethod]
    public void FormatReport_WithinTheCap_DoesNotClaimAnythingWasOmitted()
    {
        var report = CaravanGateReportFormatter.Format(null, new List<(CaravanGateSnapshot, CaravanGateVerdict)>
        {
            Entry("A", CaravanGate.Alerted),
        }, totalFound: 1, cap: 40);

        Assert.IsFalse(report.Contains("not shown"));
    }

    /// <summary>No settlement argument means the whole map — the header must not imply otherwise.</summary>
    [TestMethod]
    public void FormatReport_NoSettlementScope_SaysAllSettlements()
    {
        var report = CaravanGateReportFormatter.Format(null, new List<(CaravanGateSnapshot, CaravanGateVerdict)>
        {
            Entry("A", CaravanGate.NotBlocked),
        }, totalFound: 1, cap: 40);

        StringAssert.Contains(report, "all settlements");
    }
}
