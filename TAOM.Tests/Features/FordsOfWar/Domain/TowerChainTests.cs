using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>Tower hit points from the layout, and the rule that a tower is hittable only when the ones in front have fallen.</summary>
[TestClass]
public class TowerChainTests
{
    private static readonly FordsTowerConfig Config = FordsMatchConfig.Default.Towers;

    private static TowerChain Chain() => new(FordsTestLayout.Create(), Config);

    [TestMethod]
    public void New_OuterTower_GetsTowerHitPoints()
    {
        var chain = Chain();

        Assert.AreEqual(Config.TowerHitPoints, chain.MaxHitPoints(FordsTestLayout.WestOuter0));
        Assert.AreEqual(Config.TowerHitPoints, chain.HitPoints(FordsTestLayout.EastOuter1));
    }

    [TestMethod]
    public void New_InnerTower_GetsInnerTowerHitPoints()
    {
        var chain = Chain();

        Assert.AreEqual(Config.InnerTowerHitPoints, chain.MaxHitPoints(FordsTestLayout.WestInner0));
        Assert.AreEqual(Config.InnerTowerHitPoints, chain.HitPoints(FordsTestLayout.EastInner1));
    }

    [TestMethod]
    public void New_Keep_GetsKeepHitPoints()
    {
        var chain = Chain();

        Assert.AreEqual(Config.KeepHitPoints, chain.MaxHitPoints(FordsTestLayout.WestKeep));
        Assert.AreEqual(Config.KeepHitPoints, chain.HitPoints(FordsTestLayout.EastKeep));
    }

    [TestMethod]
    public void IsTower_KnownAndUnknownIds()
    {
        var chain = Chain();

        Assert.IsTrue(chain.IsTower(FordsTestLayout.WestKeep));
        Assert.IsTrue(chain.IsTower(FordsTestLayout.WestInner1));
        Assert.IsFalse(chain.IsTower("barn"));
    }

    [TestMethod]
    public void Slot_KnownId_ReturnsTheLayoutSlot()
    {
        var slot = Chain().Slot(FordsTestLayout.EastOuter0);

        Assert.IsNotNull(slot);
        Assert.AreEqual(MatchSide.East, slot!.Owner);
        Assert.AreEqual(0, slot.Lane);
    }

    [TestMethod]
    public void Slot_UnknownId_IsNull()
    {
        Assert.IsNull(Chain().Slot("barn"));
    }

    [TestMethod]
    public void IsLocked_OuterTower_IsOpen()
    {
        Assert.IsFalse(Chain().IsLocked(FordsTestLayout.EastOuter0));
    }

    [TestMethod]
    public void IsLocked_InnerTowerWhileOuterStands_IsLocked()
    {
        Assert.IsTrue(Chain().IsLocked(FordsTestLayout.EastInner0));
    }

    [TestMethod]
    public void IsLocked_InnerTowerAfterOuterFalls_Unlocks()
    {
        var chain = Chain();

        chain.OnDestroyed(FordsTestLayout.EastOuter0);

        Assert.IsFalse(chain.IsLocked(FordsTestLayout.EastInner0));
    }

    [TestMethod]
    public void IsLocked_OtherLaneOuterTowerFalling_DoesNotUnlock()
    {
        var chain = Chain();

        chain.OnDestroyed(FordsTestLayout.EastOuter1);

        Assert.IsTrue(chain.IsLocked(FordsTestLayout.EastInner0));
        Assert.IsFalse(chain.IsLocked(FordsTestLayout.EastInner1));
    }

    [TestMethod]
    public void IsLocked_OtherSidesTowerFalling_DoesNotUnlock()
    {
        var chain = Chain();

        chain.OnDestroyed(FordsTestLayout.WestOuter0);

        Assert.IsTrue(chain.IsLocked(FordsTestLayout.EastInner0));
    }

    [TestMethod]
    public void IsLocked_Keep_StartsLocked()
    {
        Assert.IsTrue(Chain().IsLocked(FordsTestLayout.EastKeep));
    }

    [TestMethod]
    public void IsLocked_KeepWithOneLaneStillStanding_StaysLocked()
    {
        var chain = Chain();

        chain.OnDestroyed(FordsTestLayout.EastOuter0);

        Assert.IsTrue(chain.IsLocked(FordsTestLayout.EastKeep));
    }

    [TestMethod]
    public void IsLocked_KeepAfterOneLaneFullyLost_Unlocks()
    {
        var chain = Chain();

        chain.OnDestroyed(FordsTestLayout.EastOuter1);
        chain.OnDestroyed(FordsTestLayout.EastInner1);

        Assert.IsFalse(chain.IsLocked(FordsTestLayout.EastKeep));
        Assert.IsTrue(chain.IsLocked(FordsTestLayout.WestKeep));
    }

    [TestMethod]
    public void IsLocked_KeepWhenOwnerHasNoTowers_IsOpen()
    {
        var layout = FordsTestLayout.Create();
        var keepsOnly = layout with { Towers = layout.Towers.Where(t => t.Kind == TowerKind.Keep).ToList() };

        var chain = new TowerChain(keepsOnly, Config);

        Assert.IsFalse(chain.IsLocked(FordsTestLayout.EastKeep));
    }

    [TestMethod]
    public void IsLocked_KeepWhenOneOfItsOwnersLanesHasNoTowers_IsOpen()
    {
        var layout = FordsTestLayout.Create();
        var eastLaneOneGone = layout with { Towers = layout.Towers.Where(t => !(t.Owner == MatchSide.East && t.Lane == 1)).ToList() };

        var chain = new TowerChain(eastLaneOneGone, Config);

        Assert.IsFalse(chain.IsLocked(FordsTestLayout.EastKeep), "the lane with no towers sends its units at the keep");
        Assert.IsTrue(chain.IsLocked(FordsTestLayout.WestKeep), "the other side still has both lanes");
    }

    [TestMethod]
    public void IsLocked_UnknownId_IsLocked()
    {
        Assert.IsTrue(Chain().IsLocked("barn"));
    }

    [TestMethod]
    public void ApplyDamage_ReducesHitPointsAndReturnsRemaining()
    {
        var chain = Chain();

        var remaining = chain.ApplyDamage(FordsTestLayout.EastOuter0, 1000);

        Assert.AreEqual(Config.TowerHitPoints - 1000, remaining);
        Assert.AreEqual(remaining, chain.HitPoints(FordsTestLayout.EastOuter0));
        Assert.IsFalse(chain.IsDestroyed(FordsTestLayout.EastOuter0));
    }

    [TestMethod]
    public void ApplyDamage_LethalAmount_DestroysTheTower()
    {
        var chain = Chain();

        var remaining = chain.ApplyDamage(FordsTestLayout.EastOuter0, Config.TowerHitPoints + 500);

        Assert.AreEqual(0, remaining);
        Assert.IsTrue(chain.IsDestroyed(FordsTestLayout.EastOuter0));
        Assert.IsFalse(chain.IsLocked(FordsTestLayout.EastInner0));
    }

    [TestMethod]
    public void ApplyDamage_NonPositiveOrUnknown_ChangesNothing()
    {
        var chain = Chain();

        Assert.AreEqual(Config.TowerHitPoints, chain.ApplyDamage(FordsTestLayout.EastOuter0, 0));
        Assert.AreEqual(Config.TowerHitPoints, chain.ApplyDamage(FordsTestLayout.EastOuter0, -5));
        Assert.AreEqual(0, chain.ApplyDamage("barn", 100));
    }

    [TestMethod]
    public void ApplyDamage_DestroyedTower_StaysAtZero()
    {
        var chain = Chain();
        chain.OnDestroyed(FordsTestLayout.EastOuter0);

        Assert.AreEqual(0, chain.ApplyDamage(FordsTestLayout.EastOuter0, 50));
    }

    [TestMethod]
    public void Heal_DamagedTower_RestoresUpToMaxAndReturnsAmountHealed()
    {
        var chain = Chain();
        chain.ApplyDamage(FordsTestLayout.EastOuter0, 100);

        Assert.AreEqual(60, chain.Heal(FordsTestLayout.EastOuter0, 60));
        Assert.AreEqual(40, chain.Heal(FordsTestLayout.EastOuter0, 500));
        Assert.AreEqual(Config.TowerHitPoints, chain.HitPoints(FordsTestLayout.EastOuter0));
    }

    [TestMethod]
    public void Heal_DestroyedOrUnknownTower_DoesNothing()
    {
        var chain = Chain();
        chain.OnDestroyed(FordsTestLayout.EastOuter0);

        Assert.AreEqual(0, chain.Heal(FordsTestLayout.EastOuter0, 100));
        Assert.AreEqual(0, chain.Heal("barn", 100));
        Assert.IsTrue(chain.IsDestroyed(FordsTestLayout.EastOuter0));
    }

    [TestMethod]
    public void OnDestroyed_FirstCall_ReturnsTrueAndZeroesHitPoints()
    {
        var chain = Chain();

        Assert.IsTrue(chain.OnDestroyed(FordsTestLayout.EastOuter0));
        Assert.AreEqual(0, chain.HitPoints(FordsTestLayout.EastOuter0));
        Assert.IsTrue(chain.IsDestroyed(FordsTestLayout.EastOuter0));
    }

    [TestMethod]
    public void OnDestroyed_RepeatOrUnknown_ReturnsFalse()
    {
        var chain = Chain();
        chain.OnDestroyed(FordsTestLayout.EastOuter0);

        Assert.IsFalse(chain.OnDestroyed(FordsTestLayout.EastOuter0));
        Assert.IsFalse(chain.OnDestroyed("barn"));
    }

    [TestMethod]
    public void AliveTowers_ReportsUndestroyedLaneTowersOfASide()
    {
        var chain = Chain();
        chain.OnDestroyed(FordsTestLayout.EastOuter0);

        var alive = chain.AliveTowers(MatchSide.East, 0);

        Assert.AreEqual(1, alive.Count);
        Assert.AreEqual(FordsTestLayout.EastInner0, alive[0].Id);
    }

    [TestMethod]
    public void AliveTowers_ListsOutermostFirst()
    {
        var alive = Chain().AliveTowers(MatchSide.West, 1);

        Assert.AreEqual(2, alive.Count);
        Assert.AreEqual(FordsTestLayout.WestOuter1, alive[0].Id);
        Assert.AreEqual(FordsTestLayout.WestInner1, alive[1].Id);
    }

    [TestMethod]
    public void Ids_ListsEveryTowerAndKeep()
    {
        Assert.AreEqual(10, Chain().Ids.Count);
    }
}
