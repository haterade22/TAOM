using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BannerInjection;

namespace TAOM.Tests.Features.BannerInjection;

[TestClass]
public class BannerExclusionServiceTests
{
    private BannerExclusionService _sut;

    [TestInitialize]
    public void Setup()
    {
        _sut = new BannerExclusionService();
    }

    [TestMethod]
    public void IsPlayerModified_WhenNotMarked_ReturnsFalse()
    {
        Assert.IsFalse(_sut.IsPlayerModified("clan_1"));
    }

    [TestMethod]
    public void MarkAsPlayerModified_ThenIsPlayerModified_ReturnsTrue()
    {
        _sut.MarkAsPlayerModified("clan_1");

        Assert.IsTrue(_sut.IsPlayerModified("clan_1"));
    }

    [TestMethod]
    public void MarkAsPlayerModified_DoesNotAffectOtherIds()
    {
        _sut.MarkAsPlayerModified("clan_1");

        Assert.IsFalse(_sut.IsPlayerModified("clan_2"));
    }

    [TestMethod]
    public void MarkAsPlayerModified_DuplicateId_DoesNotThrow()
    {
        _sut.MarkAsPlayerModified("clan_1");
        _sut.MarkAsPlayerModified("clan_1");

        Assert.AreEqual(1, _sut.ExclusionCount);
    }

    [TestMethod]
    public void ExclusionCount_ReflectsUniqueMarkedIds()
    {
        _sut.MarkAsPlayerModified("clan_1");
        _sut.MarkAsPlayerModified("kingdom_erebor");
        _sut.MarkAsPlayerModified("clan_2");

        Assert.AreEqual(3, _sut.ExclusionCount);
    }

    // Phase 9b #124 R1 — singleton reset on new campaign

    [TestMethod]
    public void Reset_WithExclusions_ClearsAll()
    {
        _sut.MarkAsPlayerModified("clan_1");
        _sut.MarkAsPlayerModified("clan_2");
        Assert.AreEqual(2, _sut.ExclusionCount);

        _sut.Reset();

        Assert.AreEqual(0, _sut.ExclusionCount);
        Assert.IsFalse(_sut.IsPlayerModified("clan_1"));
        Assert.IsFalse(_sut.IsPlayerModified("clan_2"));
    }

    [TestMethod]
    public void Reset_EmptyState_IsNoOp()
    {
        _sut.Reset();
        Assert.AreEqual(0, _sut.ExclusionCount);
    }
}
