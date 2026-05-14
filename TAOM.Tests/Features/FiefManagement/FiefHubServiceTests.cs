using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.FiefManagement;
using TAOM.Features.FiefManagement.Models;

namespace TAOM.Tests.Features.FiefManagement;

[TestClass]
public class FiefHubServiceTests
{
    private ISettlementOwnershipAdapter _ownership = null!;
    private FiefHubService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _ownership = Substitute.For<ISettlementOwnershipAdapter>();
        _sut = new FiefHubService(_ownership);
    }

    private static FiefSummary Town(string name) => new FiefSummary(name, name, isTown: true, isCastle: false);
    private static FiefSummary Castle(string name) => new FiefSummary(name, name, isTown: false, isCastle: true);

    private void GivenFiefs(params FiefSummary[] fiefs)
    {
        _ownership.GetPlayerOwnedFiefs().Returns(new List<FiefSummary>(fiefs));
        // Audit #143: Count now delegates to the fast-path adapter method, not GetOrderedFiefs().
        // Stub both so existing Clamp/Next/Previous/GetAt tests stay green. Null entries in the
        // ordered-list fixture are filtered by the service; the fast-path counts only non-null
        // town/castle summaries — mirror that here.
        int fastCount = 0;
        foreach (var f in fiefs)
        {
            if (f == null) continue;
            if (f.IsTown || f.IsCastle) fastCount++;
        }
        _ownership.GetPlayerOwnedFiefCount().Returns(fastCount);
    }

    // ---------- GetOrderedFiefs: sort + classification ----------

    [TestMethod]
    public void GetOrderedFiefs_NoFiefs_ReturnsEmpty()
    {
        GivenFiefs();
        var result = _sut.GetOrderedFiefs();
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void GetOrderedFiefs_TownsAndCastles_TownsFirstThenCastles()
    {
        GivenFiefs(Castle("zenith"), Town("alpha"), Castle("beacon"), Town("bravo"));
        var result = _sut.GetOrderedFiefs();
        Assert.AreEqual(4, result.Count);
        Assert.AreEqual("alpha", result[0].Name);
        Assert.IsTrue(result[0].IsTown);
        Assert.AreEqual("bravo", result[1].Name);
        Assert.IsTrue(result[1].IsTown);
        Assert.AreEqual("beacon", result[2].Name);
        Assert.IsTrue(result[2].IsCastle);
        Assert.AreEqual("zenith", result[3].Name);
        Assert.IsTrue(result[3].IsCastle);
    }

    [TestMethod]
    public void GetOrderedFiefs_AlphabeticalWithinClass_OrdinalIgnoreCase()
    {
        GivenFiefs(Town("Bravo"), Town("alpha"), Town("ALPHA1"));
        var result = _sut.GetOrderedFiefs();
        Assert.AreEqual("alpha", result[0].Name);
        Assert.AreEqual("ALPHA1", result[1].Name);
        Assert.AreEqual("Bravo", result[2].Name);
    }

    [TestMethod]
    public void GetOrderedFiefs_NullEntriesInAdapter_AreSkipped()
    {
        _ownership.GetPlayerOwnedFiefs().Returns(new List<FiefSummary> { null, Town("alpha"), null });
        var result = _sut.GetOrderedFiefs();
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("alpha", result[0].Name);
    }

    // ---------- Count ----------

    [TestMethod]
    public void Count_NoFiefs_Zero()
    {
        GivenFiefs();
        Assert.AreEqual(0, _sut.Count);
    }

    [TestMethod]
    public void Count_ThreeFiefs_Three()
    {
        GivenFiefs(Town("a"), Town("b"), Castle("c"));
        Assert.AreEqual(3, _sut.Count);
    }

    // ---------- Audit #143: Count uses fast path (no Settlement.All iteration, no FiefSummary build) ----------

    [TestMethod]
    public void Count_UsesAdapterFastPath_DoesNotCallGetOrderedFiefs()
    {
        _ownership.GetPlayerOwnedFiefCount().Returns(7);
        var n = _sut.Count;
        Assert.AreEqual(7, n);
        // The whole point of #143 is to avoid Settlement.All iteration on every F6 press.
        // Count must NEVER fall back to building the ordered list.
        _ = _ownership.DidNotReceive().GetPlayerOwnedFiefs();
    }

    [TestMethod]
    public void Count_FastPathReturnsZero_ReturnsZeroWithoutOrderedList()
    {
        _ownership.GetPlayerOwnedFiefCount().Returns(0);
        Assert.AreEqual(0, _sut.Count);
        _ = _ownership.DidNotReceive().GetPlayerOwnedFiefs();
    }

    [TestMethod]
    public void Clamp_UsesFastPathForCount()
    {
        _ownership.GetPlayerOwnedFiefCount().Returns(5);
        // Clamp consults Count; with the fast path it must succeed without GetOrderedFiefs.
        Assert.AreEqual(4, _sut.Clamp(99));
        _ = _ownership.DidNotReceive().GetPlayerOwnedFiefs();
    }

    [TestMethod]
    public void Next_UsesFastPathForCount()
    {
        _ownership.GetPlayerOwnedFiefCount().Returns(3);
        Assert.AreEqual(0, _sut.Next(2));
        _ = _ownership.DidNotReceive().GetPlayerOwnedFiefs();
    }

    [TestMethod]
    public void Previous_UsesFastPathForCount()
    {
        _ownership.GetPlayerOwnedFiefCount().Returns(3);
        Assert.AreEqual(2, _sut.Previous(0));
        _ = _ownership.DidNotReceive().GetPlayerOwnedFiefs();
    }

    // ---------- Cycle: Next / Previous ----------

    [TestMethod]
    public void Next_EmptyList_ReturnsZero()
    {
        GivenFiefs();
        Assert.AreEqual(0, _sut.Next(0));
    }

    [TestMethod]
    public void Previous_EmptyList_ReturnsZero()
    {
        GivenFiefs();
        Assert.AreEqual(0, _sut.Previous(0));
    }

    [TestMethod]
    public void Next_SingleFief_ReturnsZero()
    {
        GivenFiefs(Town("a"));
        Assert.AreEqual(0, _sut.Next(0));
    }

    [TestMethod]
    public void Previous_SingleFief_ReturnsZero()
    {
        GivenFiefs(Town("a"));
        Assert.AreEqual(0, _sut.Previous(0));
    }

    [TestMethod]
    public void Next_AtLastIndex_WrapsToZero()
    {
        GivenFiefs(Town("a"), Town("b"), Town("c"));
        Assert.AreEqual(0, _sut.Next(2));
    }

    [TestMethod]
    public void Previous_AtZero_WrapsToLastIndex()
    {
        GivenFiefs(Town("a"), Town("b"), Town("c"));
        Assert.AreEqual(2, _sut.Previous(0));
    }

    [TestMethod]
    public void Next_FromMiddle_AdvancesByOne()
    {
        GivenFiefs(Town("a"), Town("b"), Town("c"), Town("d"));
        Assert.AreEqual(2, _sut.Next(1));
    }

    [TestMethod]
    public void Previous_FromMiddle_DecrementsByOne()
    {
        GivenFiefs(Town("a"), Town("b"), Town("c"), Town("d"));
        Assert.AreEqual(0, _sut.Previous(1));
    }

    // ---------- Clamp ----------

    [TestMethod]
    public void Clamp_NegativeIndex_ReturnsZero()
    {
        GivenFiefs(Town("a"), Town("b"));
        Assert.AreEqual(0, _sut.Clamp(-5));
    }

    [TestMethod]
    public void Clamp_IndexBeyondCount_ReturnsLastIndex()
    {
        GivenFiefs(Town("a"), Town("b"));
        Assert.AreEqual(1, _sut.Clamp(10));
    }

    [TestMethod]
    public void Clamp_EmptyList_ReturnsZero()
    {
        GivenFiefs();
        Assert.AreEqual(0, _sut.Clamp(5));
    }

    // ---------- GetAt ----------

    [TestMethod]
    public void GetAt_EmptyList_ReturnsNull()
    {
        GivenFiefs();
        Assert.IsNull(_sut.GetAt(0));
    }

    [TestMethod]
    public void GetAt_BeyondCount_ClampsAndReturnsLast()
    {
        GivenFiefs(Town("a"), Town("b"));
        var result = _sut.GetAt(99);
        Assert.AreEqual("b", result.Name);
    }

    // ---------- PlayerIsAt ----------

    [TestMethod]
    public void PlayerIsAt_NullFief_False()
    {
        Assert.IsFalse(_sut.PlayerIsAt(null));
    }

    [TestMethod]
    public void PlayerIsAt_AdapterReturnsTrue_ReturnsTrue()
    {
        var fief = Town("a");
        _ownership.IsPlayerCurrentlyAt("a").Returns(true);
        Assert.IsTrue(_sut.PlayerIsAt(fief));
    }

    [TestMethod]
    public void PlayerIsAt_AdapterReturnsFalse_ReturnsFalse()
    {
        var fief = Town("a");
        _ownership.IsPlayerCurrentlyAt("a").Returns(false);
        Assert.IsFalse(_sut.PlayerIsAt(fief));
    }
}
