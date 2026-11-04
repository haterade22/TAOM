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
}
