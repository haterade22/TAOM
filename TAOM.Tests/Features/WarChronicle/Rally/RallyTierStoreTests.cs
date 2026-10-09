using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.WarChronicle.Rally;

namespace TAOM.Tests.Features.WarChronicle.Rally;

[TestClass]
public class RallyTierStoreTests
{
    private readonly RallyTierStore _sut = new RallyTierStore();

    [TestMethod]
    public void GetTier_NothingRestored_IsZeroForEveryKingdom()
    {
        Assert.AreEqual(0, _sut.GetTier("gondor"));
        Assert.AreEqual(0, _sut.GetTier(null!));
    }

    [TestMethod]
    public void Restore_KeepsValidTiers()
    {
        _sut.Restore(new Dictionary<string, int> { ["gondor"] = 2, ["rohan"] = 1, ["umbar"] = 0 });

        Assert.AreEqual(2, _sut.GetTier("gondor"));
        Assert.AreEqual(1, _sut.GetTier("rohan"));
        Assert.AreEqual(0, _sut.GetTier("umbar"));
    }

    [TestMethod]
    public void Restore_SkipsOutOfRangeTiersAndEmptyIds()
    {
        _sut.Restore(new Dictionary<string, int> { ["high"] = 3, ["low"] = -1, [""] = 1, ["ok"] = 1 });

        Assert.AreEqual(0, _sut.GetTier("high"));
        Assert.AreEqual(0, _sut.GetTier("low"));
        Assert.AreEqual(1, _sut.Snapshot().Count);
    }

    [TestMethod]
    public void Restore_ReplacesEarlierTiers_AndNullClears()
    {
        _sut.Restore(new Dictionary<string, int> { ["gondor"] = 2 });

        _sut.Restore(new Dictionary<string, int> { ["rohan"] = 1 });
        Assert.AreEqual(0, _sut.GetTier("gondor"));

        _sut.Restore(null);
        Assert.AreEqual(0, _sut.Snapshot().Count);
    }

    [TestMethod]
    public void Snapshot_ReturnsACopy()
    {
        _sut.Restore(new Dictionary<string, int> { ["gondor"] = 2 });

        var copy = (IDictionary<string, int>)_sut.Snapshot();
        copy["gondor"] = 0;

        Assert.AreEqual(2, _sut.GetTier("gondor"));
    }

    [TestMethod]
    public void SetTier_AValidTier_IsReadBackAndSaved()
    {
        _sut.SetTier("gondor", 2);

        Assert.AreEqual(2, _sut.GetTier("gondor"));
        Assert.AreEqual(2, _sut.Snapshot()["gondor"]);
    }

    [TestMethod]
    public void SetTier_Zero_DropsTheRowSoTheSaveStaysSmall()
    {
        _sut.SetTier("gondor", 1);

        _sut.SetTier("gondor", 0);

        Assert.AreEqual(0, _sut.GetTier("gondor"));
        Assert.AreEqual(0, _sut.Snapshot().Count);
    }

    [DataTestMethod]
    [DataRow(3)]
    [DataRow(-1)]
    public void SetTier_OutOfRange_IsIgnored(int tier)
    {
        _sut.SetTier("gondor", 1);

        _sut.SetTier("gondor", tier);

        Assert.AreEqual(1, _sut.GetTier("gondor"));
    }

    [TestMethod]
    public void SetTier_EmptyOrNullId_IsIgnored()
    {
        _sut.SetTier("", 1);
        _sut.SetTier(null!, 1);

        Assert.AreEqual(0, _sut.Snapshot().Count);
    }

    [TestMethod]
    public void ResetForNewSession_ClearsTheTiers()
    {
        _sut.Restore(new Dictionary<string, int> { ["gondor"] = 2 });

        _sut.ResetForNewSession();

        Assert.AreEqual(0, _sut.GetTier("gondor"));
    }
}
