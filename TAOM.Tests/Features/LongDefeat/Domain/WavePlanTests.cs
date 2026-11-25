using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

[TestClass]
public class WavePlanTests
{
    private static WaveTroop T(string id, FormationSlot slot, int weight) => new(id, slot, weight);

    private static WaveSpec Spec(int count, params WaveTroop[] mix) => new(count, mix);

    private static WavePlan Plan(int countPerWave = 10, int maxCount = 300) => new(
        new[]
        {
            Spec(110, T("a", FormationSlot.Infantry, 50), T("b", FormationSlot.Infantry, 25), T("c", FormationSlot.Ranged, 25)),
            Spec(120, T("a", FormationSlot.Infantry, 1), T("c", FormationSlot.Ranged, 1)),
        },
        new WaveScaling(countPerWave, maxCount));

    [TestMethod]
    public void ForWave_Authored_ReturnsThatEntry()
    {
        Assert.AreEqual(110, Plan().ForWave(1).Count);
        Assert.AreEqual(120, Plan().ForWave(2).Count);
    }

    [TestMethod]
    public void ForWave_PastTheList_ScalesTheLastRoster()
    {
        var third = Plan().ForWave(3);
        var fifth = Plan().ForWave(5);

        Assert.AreEqual(130, third.Count);
        Assert.AreEqual(150, fifth.Count);
        CollectionAssert.AreEqual(Plan().ForWave(2).Mix.ToList(), fifth.Mix.ToList());
    }

    [TestMethod]
    public void ForWave_ScaledCount_StopsAtMaxCount()
    {
        Assert.AreEqual(125, Plan(countPerWave: 10, maxCount: 125).ForWave(30).Count);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-3)]
    public void ForWave_BelowOne_IsWaveOne(int wave)
    {
        Assert.AreEqual(110, Plan().ForWave(wave).Count);
    }

    [TestMethod]
    public void Expand_GivesExactlyTheWaveCount()
    {
        foreach (var wave in new[] { 1, 2, 3, 7 })
            Assert.AreEqual(Plan().ForWave(wave).Count, Plan().Expand(wave).Count);
    }

    [TestMethod]
    public void Expand_SplitsByWeight_WithTheLargestRemainderTakingTheLeftover()
    {
        // 110 over 50:25:25 is 55, 27.5, 27.5: the two halves tie, so the earlier row takes the odd soldier.
        var counts = Plan().Expand(1).GroupBy(s => s.TroopId).ToDictionary(g => g.Key, g => g.Count());

        Assert.AreEqual(55, counts["a"]);
        Assert.AreEqual(28, counts["b"]);
        Assert.AreEqual(27, counts["c"]);
    }

    [TestMethod]
    public void Expand_RangedComeFirst_AndKeepTheirClass()
    {
        var list = Plan().Expand(1);
        var firstInfantry = list.ToList().FindIndex(s => s.Slot == FormationSlot.Infantry);

        Assert.IsTrue(firstInfantry > 0);
        Assert.IsTrue(list.Take(firstInfantry).All(s => s.Slot == FormationSlot.Ranged));
        Assert.IsTrue(list.Skip(firstInfantry).All(s => s.Slot == FormationSlot.Infantry));
    }

    [TestMethod]
    public void Expand_IsDeterministic()
    {
        CollectionAssert.AreEqual(Plan().Expand(4).ToList(), Plan().Expand(4).ToList());
    }

    private static EndlessScale Growth(int tierEveryWaves = 3) => new(0.05f, 0.03f, 3.0f, tierEveryWaves, new Dictionary<string, string>
    {
        ["a"] = "b",
        ["b"] = "d",
        ["c"] = "e",
    });

    private static WavePlan Endless(int tierEveryWaves = 3) => new(
        new[]
        {
            Spec(110, T("a", FormationSlot.Infantry, 50), T("b", FormationSlot.Infantry, 25), T("c", FormationSlot.Ranged, 25)),
            Spec(120, T("a", FormationSlot.Infantry, 60), T("c", FormationSlot.Ranged, 40)),
        },
        new WaveScaling(10, 300),
        Growth(tierEveryWaves));

    private static string[] Troops(WaveSpec spec) => spec.Mix.Select(m => m.TroopId).ToArray();

    [TestMethod]
    public void ForWave_InsideTheAuthoredList_HasFactorsOneAndNoTierSteps()
    {
        foreach (var wave in new[] { 1, 2 })
        {
            var spec = Endless().ForWave(wave);

            Assert.AreEqual(1f, spec.HealthFactor);
            Assert.AreEqual(1f, spec.DamageFactor);
            Assert.AreEqual(0, spec.TierSteps);
        }
    }

    [TestMethod]
    public void ForWave_PastTheList_GrowsHealthAndDamageLinearly()
    {
        var first = Endless().ForWave(3);
        var fifth = Endless().ForWave(7);

        Assert.AreEqual(1.05f, first.HealthFactor, 1e-5f);
        Assert.AreEqual(1.03f, first.DamageFactor, 1e-5f);
        Assert.AreEqual(1.25f, fifth.HealthFactor, 1e-5f);
        Assert.AreEqual(1.15f, fifth.DamageFactor, 1e-5f);
    }

    [TestMethod]
    public void ForWave_Factors_StopAtTheCap()
    {
        var spec = Endless().ForWave(102);

        Assert.AreEqual(3.0f, spec.HealthFactor);
        Assert.AreEqual(3.0f, spec.DamageFactor, "the damage line passes 3 at extra 67 and stays there");
        Assert.AreEqual(1.36f, Endless().ForWave(2 + 12).DamageFactor, 1e-5f, "below the cap the line is untouched");
    }

    [DataTestMethod]
    [DataRow(3, 0, "a,c")]
    [DataRow(4, 0, "a,c")]
    [DataRow(5, 1, "b,e")]
    [DataRow(7, 1, "b,e")]
    [DataRow(8, 2, "d,e")]
    [DataRow(11, 3, "d,e")]
    [DataRow(40, 12, "d,e")]
    public void ForWave_TiersStepEveryNthExtraWave_AndStopAtTheChainEnd(int wave, int steps, string troops)
    {
        var spec = Endless(tierEveryWaves: 3).ForWave(wave);

        Assert.AreEqual(steps, spec.TierSteps);
        CollectionAssert.AreEqual(troops.Split(','), Troops(spec));
    }

    [TestMethod]
    public void ForWave_TierStep_KeepsTheRowsClassAndWeight()
    {
        var spec = Endless(tierEveryWaves: 1).ForWave(3);

        CollectionAssert.AreEqual(new[] { ("b", FormationSlot.Infantry, 60), ("e", FormationSlot.Ranged, 40) },
            spec.Mix.Select(m => (m.TroopId, m.Slot, m.Weight)).ToArray());
    }

    [TestMethod]
    public void ForWave_TwoRowsLandingOnOneTroop_MergeTheirWeights_AtTheFirstRow()
    {
        var merging = new EndlessScale(0.05f, 0.03f, 3.0f, 1, new Dictionary<string, string> { ["a"] = "b", ["f"] = "b" });
        var plan = new WavePlan(
            new[] { Spec(110, T("a", FormationSlot.Infantry, 50), T("c", FormationSlot.Ranged, 25), T("f", FormationSlot.Infantry, 25)) },
            new WaveScaling(10, 300),
            merging);

        var spec = plan.ForWave(2);

        CollectionAssert.AreEqual(new[] { ("b", 75), ("c", 25) }, spec.Mix.Select(m => (m.TroopId, m.Weight)).ToArray());
        Assert.AreEqual(100, spec.Mix.Sum(m => m.Weight), "a merge never changes the total");
    }

    [TestMethod]
    public void ForWave_PastTheList_KeepsTheCountFormula_WhateverTheGrowth()
    {
        Assert.AreEqual(Plan().ForWave(9).Count, Endless().ForWave(9).Count, "the Phase 1 count line is unchanged");
        Assert.AreEqual(130, Endless().ForWave(3).Count);
        Assert.AreEqual(300, Endless().ForWave(60).Count);
    }

    [TestMethod]
    public void Expand_PastTheList_SpawnsTheSteppedTroops()
    {
        var troops = Endless(tierEveryWaves: 3).Expand(5).Select(s => s.TroopId).Distinct().OrderBy(x => x).ToArray();

        CollectionAssert.AreEqual(new[] { "b", "e" }, troops);
        Assert.AreEqual(Endless().ForWave(5).Count, Endless().Expand(5).Count);
    }

    [TestMethod]
    public void Expand_ByCountAndMix_IsWhatTheWaveExpansionDelegatesTo()
    {
        var plan = Plan();
        var spec = plan.ForWave(1);

        CollectionAssert.AreEqual(plan.Expand(1).ToList(), WavePlan.Expand(spec.Count, spec.Mix).ToList());
        Assert.AreEqual(0, WavePlan.Expand(0, spec.Mix).Count);
        Assert.AreEqual(0, WavePlan.Expand(10, new WaveTroop[0]).Count);
    }

    [TestMethod]
    public void Plan_WithNoGrowthGiven_IsTheScaleThatChangesNothing()
    {
        var spec = Plan().ForWave(40);

        Assert.AreEqual(1f, spec.HealthFactor);
        Assert.AreEqual(1f, spec.DamageFactor);
        Assert.AreEqual(0, spec.TierSteps);
        Assert.AreSame(EndlessScale.None, Plan().Endless);
    }

    // The shipped shape: ten authored counts, then 10 more a wave up to 300, health 0.05 and damage 0.03 a wave.
    private static WavePlan Shipped()
    {
        var counts = new[] { 110, 120, 120, 125, 130, 140, 150, 170, 185, 200 };
        return new WavePlan(
            counts.Select(c => Spec(c, T("a", FormationSlot.Infantry, 3), T("c", FormationSlot.Ranged, 1))).ToArray(),
            new WaveScaling(10, 300),
            Growth());
    }

    [DataTestMethod]
    [DataRow(1, 138)]
    [DataRow(2, 150)]
    [DataRow(3, 150)]
    [DataRow(4, 156)]
    [DataRow(5, 163)]
    [DataRow(10, 250)]
    [DataRow(13, 288)]
    [DataRow(20, 375)]
    [DataRow(60, 375)]
    public void Harder_ScalesTheCountOnceAfterTheCap_HalfAwayFromZero(int wave, int expected)
    {
        Assert.AreEqual(expected, Shipped().Harder(1.25f).ForWave(wave).Count);
    }

    [DataTestMethod]
    [DataRow(1, 110)]
    [DataRow(5, 130)]
    [DataRow(10, 200)]
    [DataRow(13, 230)]
    [DataRow(20, 300)]
    public void Harder_LeavesTheOriginalPlanAlone(int wave, int expected)
    {
        var normal = Shipped();
        normal.Harder(1.25f);

        Assert.AreEqual(expected, normal.ForWave(wave).Count);
    }

    [TestMethod]
    public void Harder_GrowsHealthAndDamageFasterAndKeepsTheCapAndTheTiers()
    {
        var hard = Shipped().Harder(1.25f);
        var wave13 = hard.ForWave(13);
        var wave20 = hard.ForWave(20);

        Assert.AreEqual(1.1875f, wave13.HealthFactor, 1e-5f);
        Assert.AreEqual(1.1125f, wave13.DamageFactor, 1e-5f);
        Assert.AreEqual(1.625f, wave20.HealthFactor, 1e-5f);
        Assert.AreEqual(1.375f, wave20.DamageFactor, 1e-5f);
        Assert.AreEqual(3.0f, hard.Endless.MaxFactor);
        Assert.AreEqual(Shipped().ForWave(13).TierSteps, wave13.TierSteps);
        Assert.AreEqual(1f, hard.ForWave(10).HealthFactor, "authored waves keep factor 1");
    }

    [TestMethod]
    public void Harder_KeepsTheAuthoredListAndTheCommanders()
    {
        var commanders = new CommanderPlan(4, new[] { new CommanderRank("captain", "{=K}Captain", "t", 100, 2f, new string[0]) },
            new Dictionary<string, CommanderAbility>(), CommanderBeyond.None);
        var normal = new WavePlan(Shipped().Authored, new WaveScaling(10, 300), Growth(), commanders);
        var hard = normal.Harder(1.25f);

        Assert.AreNotSame(CommanderPlan.None, hard.Commanders);
        Assert.AreSame(normal.Authored, hard.Authored);
        Assert.AreSame(normal.Commanders, hard.Commanders);
    }

    [TestMethod]
    public void SmallestAuthoredCount_IsTheSmallestWaveAfterTheHardFactor()
    {
        Assert.AreEqual(110, Plan().SmallestAuthoredCount);
        Assert.AreEqual(138, Plan().Harder(1.25f).SmallestAuthoredCount);
    }

    [TestMethod]
    public void Harder_ByOne_IsThePlanItself()
    {
        var normal = Shipped();

        Assert.AreSame(normal, normal.Harder(1f));
    }

    // 110 * 1.05f is 115.4999948 as the game's float holds 1.05, so it sends 115 where the decimal reading gives 116.
    [TestMethod]
    public void Harder_UsesTheGames32BitFactor()
    {
        var plan = new WavePlan(new[] { Spec(110, T("a", FormationSlot.Infantry, 1)) }, new WaveScaling(10, 300));

        Assert.AreEqual(115, plan.Harder(1.05f).ForWave(1).Count);
    }

    [TestMethod]
    public void Harder_StopsTheCountAtOneThousand()
    {
        var plan = new WavePlan(new[] { Spec(900, T("a", FormationSlot.Infantry, 1)) }, new WaveScaling(10, 1000));

        Assert.AreEqual(1000, plan.Harder(2f).ForWave(1).Count);
    }

    [TestMethod]
    public void Targets_ListsEveryLinkTarget()
    {
        CollectionAssert.AreEquivalent(new[] { "b", "d", "e" }, Growth().Targets.ToArray());
        Assert.AreEqual(0, EndlessScale.None.Targets.Count());
    }
}
