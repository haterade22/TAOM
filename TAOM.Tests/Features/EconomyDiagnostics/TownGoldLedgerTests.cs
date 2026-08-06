using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.EconomyDiagnostics;

namespace TAOM.Tests.Features.EconomyDiagnostics;

/// <summary>
/// Pins the town-gold drain ledger.
///
/// Why it exists: Minas Tirith sits at ~173 denars while its daily mint should be paying in roughly
/// 19,000 (`0.25 x (25000 + prosperity x 12 - gold)`). The mint is demonstrably not the problem, so
/// the question is where the money leaves — and no engine code logs, events, or otherwise reports a
/// town-gold movement. `SettlementComponent.ChangeGold` is the single mutator of that pool, so one
/// recorder there sees 100% of flows and nothing can be missed; the flow tag names the caller.
///
/// The ledger is deliberately a fixed-size ring: a diagnostic that grows without bound over a long
/// campaign becomes its own bug.
/// </summary>
[TestClass]
public class TownGoldLedgerTests
{
    private TownGoldLedger _sut;

    [TestInitialize]
    public void Setup() => _sut = new TownGoldLedger(daysRetained: 3);

    [TestMethod]
    public void Record_SingleFlow_AccumulatesIntoTheCurrentDay()
    {
        _sut.Record("town_G3", TownGoldFlow.VillagerDelivery, -500);
        _sut.Record("town_G3", TownGoldFlow.VillagerDelivery, -300);

        var today = _sut.GetHistory("town_G3").First();

        Assert.AreEqual(-800, today.AmountFor(TownGoldFlow.VillagerDelivery));
    }

    /// <summary>Separating inflow from drain is the entire point — a net figure answers nothing.</summary>
    [TestMethod]
    public void Record_DifferentFlows_KeptSeparate()
    {
        _sut.Record("town_G3", TownGoldFlow.DailyMint, 19242);
        _sut.Record("town_G3", TownGoldFlow.VillagerDelivery, -19000);

        var today = _sut.GetHistory("town_G3").First();

        Assert.AreEqual(19242, today.AmountFor(TownGoldFlow.DailyMint));
        Assert.AreEqual(-19000, today.AmountFor(TownGoldFlow.VillagerDelivery));
        Assert.AreEqual(242, today.Net);
    }

    [TestMethod]
    public void Record_DifferentSettlements_KeptSeparate()
    {
        _sut.Record("town_G3", TownGoldFlow.DailyMint, 100);
        _sut.Record("town_G8", TownGoldFlow.DailyMint, 250);

        Assert.AreEqual(100, _sut.GetHistory("town_G3").First().AmountFor(TownGoldFlow.DailyMint));
        Assert.AreEqual(250, _sut.GetHistory("town_G8").First().AmountFor(TownGoldFlow.DailyMint));
    }

    [TestMethod]
    public void GetHistory_UnknownSettlement_ReturnsEmptyRatherThanThrowing()
    {
        Assert.AreEqual(0, _sut.GetHistory("town_nope").Count);
    }

    /// <summary>
    /// The recorder sits on a getter called for every settlement every tick; a null or blank id from
    /// a half-initialized settlement must not create a phantom ledger entry.
    /// </summary>
    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Record_BlankSettlementId_IsIgnored(string id)
    {
        _sut.Record(id, TownGoldFlow.DailyMint, 100);

        Assert.AreEqual(0, _sut.GetHistory(id ?? string.Empty).Count);
    }

    /// <summary>`ChangeGold(0)` is a common no-op call; recording it would pad every report with noise.</summary>
    [TestMethod]
    public void Record_ZeroDelta_IsIgnored()
    {
        _sut.Record("town_G3", TownGoldFlow.DailyMint, 0);

        Assert.AreEqual(0, _sut.GetHistory("town_G3").Count);
    }

    [TestMethod]
    public void BeginDay_StartsAFreshBucket_LeavingYesterdayIntact()
    {
        _sut.Record("town_G3", TownGoldFlow.DailyMint, 100);
        _sut.BeginDay();
        _sut.Record("town_G3", TownGoldFlow.DailyMint, 250);

        var history = _sut.GetHistory("town_G3");

        Assert.AreEqual(2, history.Count);
        Assert.AreEqual(250, history[0].AmountFor(TownGoldFlow.DailyMint), "newest day must come first");
        Assert.AreEqual(100, history[1].AmountFor(TownGoldFlow.DailyMint));
    }

    /// <summary>
    /// The bound is the whole reason this is safe to leave running. Without it a 500-day campaign
    /// accumulates 500 buckets per town across ~78 towns.
    /// </summary>
    [TestMethod]
    public void BeginDay_BeyondRetention_EvictsTheOldestDay()
    {
        _sut.Record("town_G3", TownGoldFlow.DailyMint, 1);
        _sut.BeginDay();
        _sut.Record("town_G3", TownGoldFlow.DailyMint, 2);
        _sut.BeginDay();
        _sut.Record("town_G3", TownGoldFlow.DailyMint, 3);
        _sut.BeginDay();
        _sut.Record("town_G3", TownGoldFlow.DailyMint, 4);

        var history = _sut.GetHistory("town_G3");

        Assert.AreEqual(3, history.Count);
        CollectionAssert.AreEqual(
            new[] { 4, 3, 2 },
            history.Select(d => d.AmountFor(TownGoldFlow.DailyMint)).ToArray());
    }

    /// <summary>
    /// Recording before any explicit `BeginDay` must work — the recorder patch goes live the moment
    /// the game starts, which is before the first daily tick rolls the ledger.
    /// </summary>
    [TestMethod]
    public void Record_BeforeAnyBeginDay_StillCaptured()
    {
        _sut.Record("town_G3", TownGoldFlow.DailyMint, 100);

        Assert.AreEqual(1, _sut.GetHistory("town_G3").Count);
    }

    [TestMethod]
    public void AmountFor_FlowNeverRecorded_ReturnsZero()
    {
        _sut.Record("town_G3", TownGoldFlow.DailyMint, 100);

        Assert.AreEqual(0, _sut.GetHistory("town_G3").First().AmountFor(TownGoldFlow.VillagerDelivery));
    }

    /// <summary>
    /// The ledger is a process-level singleton and settlement ids repeat across campaigns, so stale
    /// buckets from an abandoned campaign would otherwise be attributed to a fresh one — the same
    /// cross-campaign leak already fixed once in `CaravanVisitMemory`.
    /// </summary>
    [TestMethod]
    public void ClearAll_DropsEveryTownsHistory()
    {
        _sut.Record("town_G3", TownGoldFlow.DailyMint, 100);
        _sut.Record("town_G8", TownGoldFlow.DailyMint, 100);

        _sut.ClearAll();

        Assert.AreEqual(0, _sut.GetHistory("town_G3").Count);
        Assert.AreEqual(0, _sut.GetHistory("town_G8").Count);
    }

    [TestMethod]
    public void GetTotals_AcrossRetainedDays_SumsPerFlow()
    {
        _sut.Record("town_G3", TownGoldFlow.VillagerDelivery, -1000);
        _sut.BeginDay();
        _sut.Record("town_G3", TownGoldFlow.VillagerDelivery, -1500);
        _sut.Record("town_G3", TownGoldFlow.DailyMint, 2000);

        var totals = _sut.GetTotals("town_G3");

        Assert.AreEqual(-2500, totals.AmountFor(TownGoldFlow.VillagerDelivery));
        Assert.AreEqual(2000, totals.AmountFor(TownGoldFlow.DailyMint));
    }

    /// <summary>A retention of zero or less would make every read empty and silently hide the drain.</summary>
    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-5)]
    public void Constructor_NonPositiveRetention_FallsBackToAUsableWindow(int requested)
    {
        var ledger = new TownGoldLedger(requested);

        ledger.Record("town_G3", TownGoldFlow.DailyMint, 100);

        Assert.AreEqual(1, ledger.GetHistory("town_G3").Count);
    }
}
