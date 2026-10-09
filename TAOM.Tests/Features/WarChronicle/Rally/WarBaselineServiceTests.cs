using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.WarChronicle.Rally;

namespace TAOM.Tests.Features.WarChronicle.Rally;

[TestClass]
public class WarBaselineServiceTests
{
    private WarBaselineService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new WarBaselineService();
    }

    private static KingdomWarSnapshot Kingdom(string id, int towns, int castles) =>
        new KingdomWarSnapshot { Id = id, Towns = towns, Castles = castles };

    [TestMethod]
    public void EnsureBaselines_FirstSight_TakesTwoPerTownPlusOnePerCastle()
    {
        var taken = _sut.EnsureBaselines(new[] { Kingdom("gondor", 3, 1), Kingdom("rohan", 1, 4) });

        Assert.AreEqual(2, taken);
        Assert.AreEqual(7, _sut.GetBaseline("gondor"));
        Assert.AreEqual(6, _sut.GetBaseline("rohan"));
    }

    [TestMethod]
    public void EnsureBaselines_CalledAgainWithLessLand_KeepsTheFirstBaseline()
    {
        _sut.EnsureBaselines(new[] { Kingdom("gondor", 3, 1) });

        var taken = _sut.EnsureBaselines(new[] { Kingdom("gondor", 1, 0) });

        Assert.AreEqual(0, taken);
        Assert.AreEqual(7, _sut.GetBaseline("gondor"));
    }

    [TestMethod]
    public void EnsureBaselines_KingdomFirstSeenLater_GetsItsOwnBaseline()
    {
        _sut.EnsureBaselines(new[] { Kingdom("gondor", 3, 1) });

        var taken = _sut.EnsureBaselines(new[] { Kingdom("gondor", 3, 1), Kingdom("rebels", 1, 1) });

        Assert.AreEqual(1, taken);
        Assert.AreEqual(3, _sut.GetBaseline("rebels"));
        Assert.AreEqual(7, _sut.GetBaseline("gondor"));
    }

    [TestMethod]
    public void EnsureBaselines_NegativeCounts_CountAsZero()
    {
        _sut.EnsureBaselines(new[] { Kingdom("odd", -3, 2) });

        Assert.AreEqual(2, _sut.GetBaseline("odd"));
    }

    [TestMethod]
    public void EnsureBaselines_NullListOrEmptyId_TakesNothing()
    {
        Assert.AreEqual(0, _sut.EnsureBaselines(null!));
        Assert.AreEqual(0, _sut.EnsureBaselines(new[] { Kingdom("", 3, 1), Kingdom(null!, 3, 1), null! }));
        Assert.AreEqual(0, _sut.Snapshot().Count);
    }

    [TestMethod]
    public void GetBaseline_UnknownOrNullId_ReturnsNull()
    {
        Assert.IsNull(_sut.GetBaseline("nobody"));
        Assert.IsNull(_sut.GetBaseline(null!));
    }

    [TestMethod]
    public void RestoreFromSave_ThenANewKingdom_KeepsTheSavedBaselinesAndTakesOnlyTheNewOne()
    {
        _sut.RestoreFromSave(new Dictionary<string, int> { ["gondor"] = 9 });

        var taken = _sut.EnsureBaselines(new[] { Kingdom("gondor", 3, 1), Kingdom("rebels", 1, 0) });

        Assert.AreEqual(1, taken, "only the kingdom the save did not know");
        Assert.AreEqual(9, _sut.GetBaseline("gondor"));
        Assert.AreEqual(2, _sut.Count);
    }

    [TestMethod]
    public void Count_NewCampaign_IsZeroUntilTheFirstTake()
    {
        Assert.AreEqual(0, _sut.Count);

        _sut.EnsureBaselines(new[] { Kingdom("gondor", 3, 1), Kingdom("rohan", 2, 0) });

        Assert.AreEqual(2, _sut.Count);
    }

    [TestMethod]
    public void ResetForNewSession_ClearsTheBaselines()
    {
        _sut.EnsureBaselines(new[] { Kingdom("gondor", 3, 1) });

        _sut.ResetForNewSession();
        _sut.EnsureBaselines(new[] { Kingdom("rohan", 2, 0) });

        Assert.IsNull(_sut.GetBaseline("gondor"));
        Assert.AreEqual(4, _sut.GetBaseline("rohan"));
        Assert.AreEqual(1, _sut.Count);
    }

    [TestMethod]
    public void LossOf_NoBaseline_IsNull()
    {
        Assert.IsNull(WarBaselineService.LossOf(null, 4));
    }

    [TestMethod]
    public void LossOf_ABaselineOfZero_IsNull()
    {
        Assert.IsNull(WarBaselineService.LossOf(0, 0));
    }

    [TestMethod]
    public void LossOf_PointsAboveTheBaseline_IsZero()
    {
        Assert.AreEqual(0f, WarBaselineService.LossOf(10, 12));
    }

    [TestMethod]
    public void LossOf_HalfTheBaselineLost_IsOneHalf()
    {
        Assert.AreEqual(0.5f, WarBaselineService.LossOf(10, 5));
    }

    [TestMethod]
    public void Snapshot_ReturnsACopy()
    {
        _sut.EnsureBaselines(new[] { Kingdom("gondor", 3, 1) });

        var copy = (IDictionary<string, int>)_sut.Snapshot();
        copy["gondor"] = 99;
        copy["extra"] = 1;

        Assert.AreEqual(7, _sut.GetBaseline("gondor"));
        Assert.IsNull(_sut.GetBaseline("extra"));
    }

    [TestMethod]
    public void RestoreFromSave_SkipsNegativeAndEmptyIdRows()
    {
        _sut.RestoreFromSave(new Dictionary<string, int> { ["ok"] = 4, ["bad"] = -1, [""] = 3 });

        Assert.AreEqual(4, _sut.GetBaseline("ok"));
        Assert.IsNull(_sut.GetBaseline("bad"));
        Assert.AreEqual(1, _sut.Snapshot().Count);
    }

    [TestMethod]
    public void RestoreFromSave_ReplacesEarlierBaselines()
    {
        _sut.EnsureBaselines(new[] { Kingdom("stale", 3, 1) });

        _sut.RestoreFromSave(new Dictionary<string, int> { ["gondor"] = 5 });

        Assert.IsNull(_sut.GetBaseline("stale"));
        Assert.AreEqual(5, _sut.GetBaseline("gondor"));
    }
}
