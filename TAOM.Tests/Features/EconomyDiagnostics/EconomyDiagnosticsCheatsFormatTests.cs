using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.EconomyDiagnostics;
using TAOM.Features.EconomyDiagnostics.Cheats;

namespace TAOM.Tests.Features.EconomyDiagnostics;

/// <summary>
/// Pins the `taom.print_town_ledger` report.
///
/// The report has one job: name the flow that is emptying the town. A breakdown table alone invites
/// the reader to pick the biggest number rather than the biggest *outflow* — the daily mint is
/// usually the largest single figure and is an inflow — so the summary line has to do that reading
/// for them.
/// </summary>
[TestClass]
public class EconomyDiagnosticsCheatsFormatTests
{
    private static TownGoldDay Day(params (TownGoldFlow Flow, int Amount)[] entries)
    {
        var ledger = new TownGoldLedger();
        foreach (var (flow, amount) in entries)
            ledger.Record("x", flow, amount);

        return ledger.GetTotals("x");
    }

    [TestMethod]
    public void FormatLedger_NoMovementYet_SaysSoInsteadOfPrintingAnEmptyTable()
    {
        var report = EconomyDiagnosticsCheats.FormatLedger(
            "town_G3", "Minas Tirith", 173, new List<TownGoldDay>(), new TownGoldDay());

        StringAssert.Contains(report, "No movement recorded yet");
    }

    /// <summary>
    /// The Minas Tirith shape: a healthy mint entirely consumed by villager deliveries. The mint is
    /// the biggest absolute number, so a naive "largest flow" summary would report the income as the
    /// finding and send the reader to raise a regen rate that is already working.
    /// </summary>
    [TestMethod]
    public void FormatLedger_MintOutweighedByADrain_NamesTheDrainNotTheIncome()
    {
        var totals = Day(
            (TownGoldFlow.DailyMint, 19242),
            (TownGoldFlow.VillagerDelivery, -19100),
            (TownGoldFlow.Trade, -60));

        var report = EconomyDiagnosticsCheats.FormatLedger(
            "town_G3", "Minas Tirith", 173, new List<TownGoldDay> { totals }, totals);

        StringAssert.Contains(report, "largest drain: VillagerDelivery");
        Assert.IsFalse(report.Contains("largest drain: DailyMint"), "an inflow must never be reported as a drain");
    }

    [TestMethod]
    public void FormatLedger_AllFlowsPositive_SaysThereIsNoDrain()
    {
        var totals = Day((TownGoldFlow.DailyMint, 5000), (TownGoldFlow.ResidentConsumption, 800));

        var report = EconomyDiagnosticsCheats.FormatLedger(
            "town_G3", "Minas Tirith", 40000, new List<TownGoldDay> { totals }, totals);

        StringAssert.Contains(report, "no net drain recorded");
    }

    [TestMethod]
    public void FormatLedger_WithHistory_ReportsTheDayCountAndCurrentGold()
    {
        var totals = Day((TownGoldFlow.DailyMint, 100));
        var history = new List<TownGoldDay> { totals, totals, totals };

        var report = EconomyDiagnosticsCheats.FormatLedger("town_G3", "Minas Tirith", 173, history, totals);

        StringAssert.Contains(report, "gold=173");
        StringAssert.Contains(report, "last 3 day(s)");
    }

    /// <summary>
    /// A large `Other` is a real result, not a gap to hide: it means gold is moving through a site
    /// no tag covers (workshops are deliberately untagged), and that is the next thing to instrument.
    /// </summary>
    [TestMethod]
    public void FormatLedger_UntaggedDrain_SurfacesOtherRatherThanOmittingIt()
    {
        var totals = Day((TownGoldFlow.DailyMint, 5000), (TownGoldFlow.Other, -7000));

        var report = EconomyDiagnosticsCheats.FormatLedger(
            "town_G3", "Minas Tirith", 0, new List<TownGoldDay> { totals }, totals);

        StringAssert.Contains(report, "Other");
        StringAssert.Contains(report, "largest drain: Other");
    }
}
