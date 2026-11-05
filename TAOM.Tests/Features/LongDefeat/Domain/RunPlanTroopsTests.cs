using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

[TestClass]
public class RunPlanTroopsTests
{
    private static RunPlan Plan(EndlessScale? endless = null) => new(
        "helms",
        new[] { new GarrisonRow("g_foot", FormationSlot.Infantry, 10), new GarrisonRow("g_bow", FormationSlot.Ranged, 5) },
        new WavePlan(
            new[]
            {
                new WaveSpec(110, new[] { new WaveTroop("a", FormationSlot.Infantry, 1), new WaveTroop("s", FormationSlot.Ranged, 1) }),
                new WaveSpec(120, new[] { new WaveTroop("a", FormationSlot.Infantry, 1), new WaveTroop("w", FormationSlot.Infantry, 1) }),
            },
            new WaveScaling(10, 300),
            endless),
        new ShopPlan(600, new[]
        {
            new ShopOffer("militia", "Militia", "shop_militia", FormationSlot.Infantry, 12, 180),
            new ShopOffer("yeomen", "Yeomen", "g_bow", FormationSlot.Ranged, 10, 200),
        }),
        RunRules.Default,
        RunOptions.Default);

    [TestMethod]
    public void AllTroopIds_HoldsTheGarrisonTheShopAndEveryAuthoredWave_OnceEach()
    {
        var ids = RunPlanTroops.AllTroopIds(Plan());

        CollectionAssert.AreEquivalent(new[] { "g_foot", "g_bow", "shop_militia", "a", "s", "w" }, ids.ToArray());
        Assert.AreEqual(ids.Count, ids.Distinct().Count());
    }

    [TestMethod]
    public void AllTroopIds_AlsoHoldsEveryTierTarget_SoALaunchRefusesAMissingOneBeforeTheRun()
    {
        var endless = new EndlessScale(0.05f, 0.03f, 3f, 3, new Dictionary<string, string> { ["a"] = "a2", ["a2"] = "a3", ["s"] = "s2" });

        var ids = RunPlanTroops.AllTroopIds(Plan(endless));

        foreach (var target in new[] { "a2", "a3", "s2" })
            CollectionAssert.Contains(ids.ToList(), target);
        Assert.AreEqual(ids.Count, ids.Distinct().Count());
    }
}
