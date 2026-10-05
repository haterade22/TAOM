using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CreatureSiegeRole.Domain;
using static TAOM.Tests.Features.CreatureSiegeRole.CreatureSiegeFixtures;

namespace TAOM.Tests.Features.CreatureSiegeRole;

// The exclusion ids: a creature never climbs, because the ladder queue recruits only an agent whose path already crosses
// its face. Native keeps one set per exact ordered id list and marks each face with one byte: bit 0 is the engine's
// no-exclusion bit and bit 7 the navmesh base boundary, so the order is fixed, nothing is ever removed, and the cap is 6.
public partial class CreatureSiegeRulesTests
{
    private const int Cap = CreatureSiegeRules.MaxExcludedFaceGroups;

    [TestMethod]
    public void TheCap_IsSix_BecauseBitsZeroAndSevenOfTheFaceByteAreTaken()
    {
        Assert.AreEqual(6, CreatureSiegeRules.MaxExcludedFaceGroups);
    }

    [TestMethod]
    public void BuildExclusionIds_AreInPriorityOrder_TowerEntrancesThenLaddersAscendingThenBridges()
    {
        var towers = List(new TowerFaces(1000050, 601), new TowerFaces(1000100, 602));
        var plan = CreatureSiegeRules.BuildExclusionIds(towers, List(555, 333, 666, 444), Cap + 3);

        CollectionAssert.AreEqual(new[] { 1000052, 1000102, 333, 444, 555, 666, 601, 602 }, plan.Ids.ToArray());
    }

    [TestMethod]
    public void BuildExclusionIds_TheTypicalTaomScene_KeepsEntrancesAndLadders_AndDropsTheBridgeQuietly()
    {
        // Two towers sharing one scene-authored bridge id (601) and four ladders: 2 + 4 = 6 fill the cap, the bridge is last.
        var plan = CreatureSiegeRules.BuildExclusionIds(
            List(new TowerFaces(1000050, 601), new TowerFaces(1000100, 601)), List(333, 444, 555, 666), Cap);

        CollectionAssert.AreEqual(new[] { 1000052, 1000102, 333, 444, 555, 666 }, plan.Ids.ToArray());
        CollectionAssert.AreEqual(new[] { 601 }, plan.Dropped.ToArray());
        Assert.IsFalse(plan.WasTruncated, "a dropped bridge is not worth a warning");
        Assert.AreEqual(0, plan.Skipped.Count);
    }

    [TestMethod]
    public void BuildExclusionIds_DistinctBridgeIdsPerTower_AreAllDroppedQuietlyAtTheCap()
    {
        var plan = CreatureSiegeRules.BuildExclusionIds(
            List(new TowerFaces(1000050, 1000053), new TowerFaces(1000100, 1000103)), List(333, 444, 555, 666), Cap);

        CollectionAssert.AreEqual(new[] { 1000052, 1000102, 333, 444, 555, 666 }, plan.Ids.ToArray());
        CollectionAssert.AreEqual(new[] { 1000053, 1000103 }, plan.Dropped.ToArray());
        Assert.IsFalse(plan.WasTruncated);
    }

    [TestMethod]
    public void BuildExclusionIds_ALadderDroppedAtTheCap_IsTruncated()
    {
        var plan = CreatureSiegeRules.BuildExclusionIds(
            List(new TowerFaces(1000050, 601), new TowerFaces(1000100, 601)), List(333, 444, 555, 666, 777), Cap);

        CollectionAssert.AreEqual(new[] { 777, 601 }, plan.Dropped.ToArray());
        Assert.IsTrue(plan.WasTruncated);
    }

    [TestMethod]
    public void BuildExclusionIds_ATowerEntranceDroppedAtTheCap_IsTruncated()
    {
        var plan = CreatureSiegeRules.BuildExclusionIds(
            List(new TowerFaces(1000050, 601), new TowerFaces(1000100, 601)), List(333), 1);

        CollectionAssert.AreEqual(new[] { 1000052 }, plan.Ids.ToArray());
        CollectionAssert.AreEqual(new[] { 1000102, 333, 601 }, plan.Dropped.ToArray());
        Assert.IsTrue(plan.WasTruncated);
    }

    [TestMethod]
    public void BuildExclusionIds_ADuplicateAnywhere_IsCountedOnceAtItsFirstTier()
    {
        // Ladder 601 and a tower bridge 601: the ladder tier comes first, the bridge is the duplicate.
        var plan = CreatureSiegeRules.BuildExclusionIds(List(new TowerFaces(1000050, 601)), List(601, 333, 333), Cap);

        CollectionAssert.AreEqual(new[] { 1000052, 333, 601 }, plan.Ids.ToArray());
        Assert.IsFalse(plan.WasTruncated);
    }

    [TestMethod]
    public void BuildExclusionIds_TheSameTowerEntranceTwice_IsCountedOnce()
    {
        var plan = CreatureSiegeRules.BuildExclusionIds(
            List(new TowerFaces(1000050, 0), new TowerFaces(1000050, 0)), List<int>(), Cap);

        CollectionAssert.AreEqual(new[] { 1000052 }, plan.Ids.ToArray());
    }

    [TestMethod]
    public void BuildExclusionIds_ATowerWithNoNavmeshStart_IsSkippedAndNamed()
    {
        // Start 0 would make +2 the scene's own face group 2, which a creature must keep walking on.
        var plan = CreatureSiegeRules.BuildExclusionIds(List(new TowerFaces(0, 0), new TowerFaces(-4, 0)), List<int>(), Cap);

        Assert.AreEqual(0, plan.Ids.Count);
        CollectionAssert.Contains(plan.Skipped.ToList(), new SkippedFace(ExclusionTier.TowerEntrance, 0));
        CollectionAssert.Contains(plan.Skipped.ToList(), new SkippedFace(ExclusionTier.TowerEntrance, -4));
    }

    [TestMethod]
    public void BuildExclusionIds_ATowerStartTooHighToAddTwo_IsSkipped_NotWrappedNegative()
    {
        var plan = CreatureSiegeRules.BuildExclusionIds(List(new TowerFaces(int.MaxValue, 0)), List<int>(), Cap);

        Assert.AreEqual(0, plan.Ids.Count);
        Assert.IsTrue(plan.Skipped.Any(s => s.Tier == ExclusionTier.TowerEntrance));
    }

    [TestMethod]
    public void BuildExclusionIds_LadderIdsOfZeroOrLess_AreSkippedAndNamed()
    {
        var plan = CreatureSiegeRules.BuildExclusionIds(List<TowerFaces>(), List(0, 333, -1), Cap);

        CollectionAssert.AreEqual(new[] { 333 }, plan.Ids.ToArray());
        CollectionAssert.AreEquivalent(
            new[] { new SkippedFace(ExclusionTier.Ladder, 0), new SkippedFace(ExclusionTier.Ladder, -1) }, plan.Skipped.ToArray());
    }

    [TestMethod]
    public void BuildExclusionIds_ABridgeIdOfZero_IsSkippedAndNamed()
    {
        var plan = CreatureSiegeRules.BuildExclusionIds(List(new TowerFaces(1000050, 0)), List<int>(), Cap);

        CollectionAssert.AreEqual(new[] { 1000052 }, plan.Ids.ToArray());
        CollectionAssert.AreEqual(new[] { new SkippedFace(ExclusionTier.TowerBridge, 0) }, plan.Skipped.ToArray());
    }

    [TestMethod]
    public void BuildExclusionIds_PastTheCap_EntrancesAreKeptBeforeLaddersBeforeBridges()
    {
        // 3 towers + 6 ladders: 3 entrances and the first three ladders fill six; the rest drop in offered order.
        var towers = List(new TowerFaces(1000050, 701), new TowerFaces(1000100, 702), new TowerFaces(1000150, 703));
        var plan = CreatureSiegeRules.BuildExclusionIds(towers, List(111, 222, 333, 444, 555, 666), Cap);

        CollectionAssert.AreEqual(new[] { 1000052, 1000102, 1000152, 111, 222, 333 }, plan.Ids.ToArray());
        CollectionAssert.AreEqual(new[] { 444, 555, 666, 701, 702, 703 }, plan.Dropped.ToArray());
        Assert.IsTrue(plan.WasTruncated);
    }

    [TestMethod]
    public void BuildExclusionIds_NeverHoldsMoreThanTheCap_ForAnyMixOfInputs()
    {
        for (var towerCount = 0; towerCount <= 4; towerCount++)
        for (var ladderCount = 0; ladderCount <= 12; ladderCount++)
        for (var cap = 0; cap <= 9; cap++)
        {
            var towers = Enumerable.Range(0, towerCount).Select(i => new TowerFaces(1000050 + 50 * i, 600 + i)).ToArray();
            var ladders = Enumerable.Range(0, ladderCount).Select(i => 100 + i).ToArray();

            var plan = CreatureSiegeRules.BuildExclusionIds(towers, ladders, cap);

            Assert.IsTrue(plan.Ids.Count <= cap, $"{towerCount} towers, {ladderCount} ladders, cap {cap}: {plan.Ids.Count} ids");
            Assert.AreEqual(plan.Ids.Count + plan.Dropped.Count, plan.Ids.Concat(plan.Dropped).Distinct().Count(),
                "an id was listed twice");
        }
    }

    [TestMethod]
    public void BuildExclusionIds_ACapOfZeroOrLess_KeepsNothing_AndDropsEverythingValid()
    {
        var plan = CreatureSiegeRules.BuildExclusionIds(List(new TowerFaces(1000050, 601)), List(333), 0);

        Assert.AreEqual(0, plan.Ids.Count);
        CollectionAssert.AreEqual(new[] { 1000052, 333, 601 }, plan.Dropped.ToArray());
        Assert.AreEqual(0, CreatureSiegeRules.BuildExclusionIds(List(new TowerFaces(1000050, 601)), List(333), -3).Ids.Count);
    }

    [TestMethod]
    public void BuildExclusionIds_NullInputs_AreAnEmptyPlan()
    {
        var plan = CreatureSiegeRules.BuildExclusionIds(null, null, Cap);

        Assert.AreEqual(0, plan.Ids.Count);
        Assert.AreEqual(0, plan.Skipped.Count);
        Assert.IsFalse(plan.WasTruncated);
    }

    [TestMethod]
    public void BuildExclusionIds_TheSameInputsTwice_GiveTheSameListInTheSameOrder()
    {
        // The engine registers one set per exact ordered list: two agents must be excluded in the same order.
        var towers = List(new TowerFaces(1000050, 601), new TowerFaces(1000100, 601));
        var first = CreatureSiegeRules.BuildExclusionIds(towers, List(666, 333, 555, 444), Cap);
        var second = CreatureSiegeRules.BuildExclusionIds(towers, List(444, 555, 333, 666), Cap);

        CollectionAssert.AreEqual(first.Ids.ToArray(), second.Ids.ToArray());
    }
}
