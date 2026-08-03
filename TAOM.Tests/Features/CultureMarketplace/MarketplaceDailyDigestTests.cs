using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CultureMarketplace.Domain;

namespace TAOM.Tests.Features.CultureMarketplace;

// The per-town DEBUG line ran at ~30 lines/min for the life of a session (1,687 lines in the
// 37-minute 2026-08-03 log, 36% of the file) and buried crash evidence in user uploads. The
// digest rolls a whole in-game day into one line.
[TestClass]
public class MarketplaceDailyDigestTests
{
    private MarketplaceDailyDigest _sut;

    [TestInitialize]
    public void Setup() => _sut = new MarketplaceDailyDigest();

    [TestMethod]
    public void Flush_WithNothingRecorded_ReturnsNull()
    {
        Assert.IsNull(_sut.Flush());
    }

    // A town that ticked but did nothing is not an event — matches the pre-digest gate, which
    // deliberately stayed silent for a no-op pass.
    [TestMethod]
    public void Flush_WhenEveryTownWasANoOp_ReturnsNull()
    {
        _sut.Record("town_A", picks: 0, injected: 0, guaranteed: 0, removed: 0);
        _sut.Record("town_B", picks: 0, injected: 0, guaranteed: 0, removed: 0);

        Assert.IsNull(_sut.Flush());
    }

    // Foreign-item strip alone still earns the line at daily scale. The old per-town gate excluded
    // `removed` because it fired ~3.6x/town/day forever; one rolled-up line a day is affordable.
    [TestMethod]
    public void Flush_WithOnlyForeignRemovals_ReturnsLine()
    {
        _sut.Record("town_A", picks: 0, injected: 0, guaranteed: 0, removed: 4);

        StringAssert.Contains(_sut.Flush(), "-4 foreign");
    }

    [TestMethod]
    public void Flush_SumsTotalsAcrossTowns()
    {
        _sut.Record("town_A", picks: 3, injected: 3, guaranteed: 1, removed: 4);
        _sut.Record("town_B", picks: 2, injected: 2, guaranteed: 0, removed: 6);

        var line = _sut.Flush();

        StringAssert.Contains(line, "+5 injected");
        StringAssert.Contains(line, "+1 guaranteed");
        StringAssert.Contains(line, "-10 foreign");
    }

    // Active towns (something happened) over towns touched — the second number is the sweep size.
    [TestMethod]
    public void Flush_ReportsActiveTownsOverTownsTouched()
    {
        _sut.Record("town_A", picks: 1, injected: 1, guaranteed: 0, removed: 0);
        _sut.Record("town_B", picks: 0, injected: 0, guaranteed: 2, removed: 0);
        _sut.Record("town_C", picks: 0, injected: 0, guaranteed: 0, removed: 0);

        StringAssert.Contains(_sut.Flush(), "2/3 town(s) active");
    }

    [TestMethod]
    public void Flush_ListsTopTownsByActivityDescending()
    {
        _sut.Record("town_small", picks: 1, injected: 1, guaranteed: 0, removed: 0);
        _sut.Record("town_big", picks: 7, injected: 7, guaranteed: 2, removed: 0);
        _sut.Record("town_mid", picks: 4, injected: 4, guaranteed: 0, removed: 0);

        StringAssert.Contains(_sut.Flush(), "top: town_big +9, town_mid +4, town_small +1");
    }

    [TestMethod]
    public void Flush_CapsTheTopListAtThreeTowns()
    {
        for (var i = 1; i <= 6; i++)
            _sut.Record($"town_{i}", picks: i, injected: i, guaranteed: 0, removed: 0);

        var line = _sut.Flush();

        StringAssert.Contains(line, "top: town_6 +6, town_5 +5, town_4 +4)");
        Assert.IsFalse(line.Contains("town_3"), "top list must cap at three towns");
    }

    // Ties resolve by settlement id so the same day never renders two different ways.
    [TestMethod]
    public void Flush_BreaksTopListTiesBySettlementId()
    {
        _sut.Record("town_z", picks: 2, injected: 2, guaranteed: 0, removed: 0);
        _sut.Record("town_a", picks: 2, injected: 2, guaranteed: 0, removed: 0);

        StringAssert.Contains(_sut.Flush(), "top: town_a +2, town_z +2");
    }

    [TestMethod]
    public void Flush_WithNoActiveTowns_OmitsTopList()
    {
        _sut.Record("town_A", picks: 0, injected: 0, guaranteed: 0, removed: 3);

        Assert.IsFalse(_sut.Flush().Contains("top:"));
    }

    // A pick the roster refused to accept used to be invisible unless you diffed `picks=` against
    // `+N injected` across every surviving line. Surfaced once per day instead.
    [TestMethod]
    public void Flush_WhenPicksExceedInjections_ReportsRejectedCount()
    {
        _sut.Record("town_A", picks: 5, injected: 3, guaranteed: 0, removed: 0);

        StringAssert.Contains(_sut.Flush(), "2 pick(s) rejected");
    }

    [TestMethod]
    public void Flush_WhenEveryPickLanded_OmitsRejectedCount()
    {
        _sut.Record("town_A", picks: 3, injected: 3, guaranteed: 0, removed: 0);

        Assert.IsFalse(_sut.Flush().Contains("rejected"));
    }

    [TestMethod]
    public void Flush_ResetsCounters()
    {
        _sut.Record("town_A", picks: 3, injected: 3, guaranteed: 1, removed: 4);
        _sut.Flush();

        Assert.IsNull(_sut.Flush());
    }

    [TestMethod]
    public void Flush_AfterReset_StartsAFreshDay()
    {
        _sut.Record("town_A", picks: 9, injected: 9, guaranteed: 0, removed: 0);
        _sut.Flush();

        _sut.Record("town_B", picks: 1, injected: 1, guaranteed: 0, removed: 0);
        var line = _sut.Flush();

        StringAssert.Contains(line, "+1 injected");
        Assert.IsFalse(line.Contains("town_A"), "previous day's towns must not leak into the next");
    }

    // Same town ticking twice in one accumulation window must add up, not overwrite.
    [TestMethod]
    public void Record_SameTownTwice_AccumulatesRatherThanReplaces()
    {
        _sut.Record("town_A", picks: 2, injected: 2, guaranteed: 0, removed: 0);
        _sut.Record("town_A", picks: 3, injected: 3, guaranteed: 1, removed: 0);

        StringAssert.Contains(_sut.Flush(), "top: town_A +6");
    }

    [TestMethod]
    public void Record_NullOrEmptySettlementId_DoesNotThrowAndStillCounts()
    {
        _sut.Record(null, picks: 1, injected: 1, guaranteed: 0, removed: 0);
        _sut.Record(string.Empty, picks: 1, injected: 1, guaranteed: 0, removed: 0);

        StringAssert.Contains(_sut.Flush(), "+2 injected");
    }
}
