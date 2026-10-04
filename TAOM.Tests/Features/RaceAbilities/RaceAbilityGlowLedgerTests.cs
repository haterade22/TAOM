using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.RaceAbilities;

// Who wears an active ability's outline: the soldiers nearest the camera, up to the cap, painted again on every
// pass (a re-equip strips the outline) and cleared as soon as they drop out. Plain objects stand in for agents.

namespace TAOM.Tests.Features.RaceAbilities;

[TestClass]
public class RaceAbilityGlowLedgerTests
{
    private const uint Red = 0xFFE03A2E;
    private const uint Blue = 0xFF5B9BD5;

    private RaceAbilityGlowLedger<object> _sut = null!;
    private List<(object Key, uint Color)> _paint = null!;
    private List<object> _clear = null!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new RaceAbilityGlowLedger<object>();
        _paint = new List<(object Key, uint Color)>();
        _clear = new List<object>();
    }

    private void Settle(int max)
    {
        _paint.Clear();
        _clear.Clear();
        _sut.Settle(max, _paint, _clear);
    }

    [TestMethod]
    public void Settle_MoreCandidatesThanTheCap_PaintsTheNearest()
    {
        var near = new object();
        var middle = new object();
        var far = new object();
        _sut.Offer(far, Red, 900f);
        _sut.Offer(near, Red, 4f);
        _sut.Offer(middle, Blue, 100f);

        Settle(max: 2);

        CollectionAssert.AreEquivalent(new[] { near, middle }, _paint.Select(p => p.Key).ToList());
        Assert.AreEqual(Blue, _paint.Single(p => p.Key == middle).Color);
        Assert.AreEqual(0, _clear.Count);
        Assert.AreEqual(2, _sut.LitCount);
    }

    [TestMethod]
    public void Settle_StillPicked_IsPaintedAgainEveryPass()
    {
        var soldier = new object();
        _sut.Offer(soldier, Red, 1f);
        Settle(max: 40);

        _sut.Offer(soldier, Red, 1f);
        Settle(max: 40);

        Assert.AreSame(soldier, _paint.Single().Key);
        Assert.AreEqual(0, _clear.Count);
    }

    [TestMethod]
    public void Settle_NoLongerOffered_IsCleared()
    {
        var soldier = new object();
        _sut.Offer(soldier, Red, 1f);
        Settle(max: 40);

        Settle(max: 40);

        Assert.AreSame(soldier, _clear.Single());
        Assert.AreEqual(0, _paint.Count);
        Assert.AreEqual(0, _sut.LitCount);
    }

    [TestMethod]
    public void Settle_PushedOutByANearerSoldier_IsCleared()
    {
        var first = new object();
        var nearer = new object();
        _sut.Offer(first, Red, 100f);
        Settle(max: 1);

        _sut.Offer(first, Red, 100f);
        _sut.Offer(nearer, Red, 1f);
        Settle(max: 1);

        Assert.AreSame(nearer, _paint.Single().Key);
        Assert.AreSame(first, _clear.Single());
    }

    [TestMethod]
    public void Settle_ColourChanged_PaintsTheNewColour()
    {
        var soldier = new object();
        _sut.Offer(soldier, Red, 1f);
        Settle(max: 40);

        _sut.Offer(soldier, Blue, 1f);
        Settle(max: 40);

        Assert.AreEqual(Blue, _paint.Single().Color);
    }

    [TestMethod]
    public void Settle_CapZero_PaintsNoneAndClearsEveryoneLit()
    {
        var soldier = new object();
        _sut.Offer(soldier, Red, 1f);
        Settle(max: 40);

        _sut.Offer(soldier, Red, 1f);
        Settle(max: 0);

        Assert.AreEqual(0, _paint.Count);
        Assert.AreSame(soldier, _clear.Single());
        Assert.AreEqual(0, _sut.LitCount);
    }

    [TestMethod]
    public void Settle_SameSoldierOfferedTwice_TakesOneSlot()
    {
        var soldier = new object();
        var other = new object();
        _sut.Offer(soldier, Red, 1f);
        _sut.Offer(soldier, Red, 2f);
        _sut.Offer(other, Blue, 3f);

        Settle(max: 2);

        CollectionAssert.AreEquivalent(new[] { soldier, other }, _paint.Select(p => p.Key).ToList());
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(-1f)]
    public void Offer_DistanceNotAFiniteSquare_NeverLights(float distanceSquared)
    {
        _sut.Offer(new object(), Red, distanceSquared);

        Settle(max: 40);

        Assert.AreEqual(0, _paint.Count);
    }

    [TestMethod]
    public void Forget_ALitSoldier_ReturnsTrueAndIsNotClearedAgain()
    {
        var soldier = new object();
        _sut.Offer(soldier, Red, 1f);
        Settle(max: 40);

        Assert.IsTrue(_sut.Forget(soldier));
        Settle(max: 40);

        Assert.AreEqual(0, _clear.Count);
        Assert.AreEqual(0, _sut.LitCount);
    }

    [TestMethod]
    public void Forget_AnUnlitSoldier_ReturnsFalse() =>
        Assert.IsFalse(_sut.Forget(new object()));

    [TestMethod]
    public void Clear_DropsEveryoneWithoutListingThem()
    {
        _sut.Offer(new object(), Red, 1f);
        Settle(max: 40);
        _sut.Offer(new object(), Blue, 1f);

        _sut.Clear();
        Settle(max: 40);

        Assert.AreEqual(0, _paint.Count);
        Assert.AreEqual(0, _clear.Count);
        Assert.AreEqual(0, _sut.LitCount);
    }
}
