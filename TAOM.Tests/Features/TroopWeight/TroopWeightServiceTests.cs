using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System.Collections.Generic;
using System.Linq;
using TAOM.Core.Logging;
using TAOM.Features.TroopWeight;

namespace TAOM.Tests.Features.TroopWeight;

[TestClass]
public class TroopWeightServiceTests
{
    private TroopWeightService _sut;
    private ITroopWeightXmlLoader _xmlLoader;
    private IModLogger _logger;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _xmlLoader = Substitute.For<ITroopWeightXmlLoader>();
        _xmlLoader.GetTroopWeights().Returns(new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "cave_troll", 4.0f },
            { "imladris_blademaster", 2.0f },
            { "rivendell_royal_knight", 3.0f }
        });
        _sut = new TroopWeightService(_logger, _xmlLoader);
    }

    [TestMethod]
    public void GetTroopWeight_NullStringId_ReturnsDefault()
    {
        var result = _sut.GetTroopWeight((string)null);

        Assert.AreEqual(1.0f, result);
    }

    [TestMethod]
    public void GetTroopWeight_EmptyStringId_ReturnsDefault()
    {
        var result = _sut.GetTroopWeight(string.Empty);

        Assert.AreEqual(1.0f, result);
    }

    [TestMethod]
    public void GetTroopWeight_UnknownId_ReturnsDefault()
    {
        var result = _sut.GetTroopWeight("gondor_militia");

        Assert.AreEqual(1.0f, result);
    }

    [TestMethod]
    public void GetTroopWeight_KnownId_ReturnsConfiguredWeight()
    {
        var result = _sut.GetTroopWeight("cave_troll");

        Assert.AreEqual(4.0f, result);
    }

    [TestMethod]
    public void GetTroopWeight_CaseInsensitive_ReturnsSameWeight()
    {
        var lower = _sut.GetTroopWeight("cave_troll");
        var upper = _sut.GetTroopWeight("CAVE_TROLL");
        var mixed = _sut.GetTroopWeight("Cave_Troll");

        Assert.AreEqual(4.0f, lower);
        Assert.AreEqual(4.0f, upper);
        Assert.AreEqual(4.0f, mixed);
    }

    [TestMethod]
    public void GetTroopWeight_CachesResult_DoesNotCallLoaderTwice()
    {
        _sut.GetTroopWeight("cave_troll");
        _sut.GetTroopWeight("cave_troll");

        _xmlLoader.Received(1).GetTroopWeights();
    }

    [TestMethod]
    public void GetTroopWeight_MultipleIds_ReturnsCorrectWeights()
    {
        Assert.AreEqual(4.0f, _sut.GetTroopWeight("cave_troll"));
        Assert.AreEqual(2.0f, _sut.GetTroopWeight("imladris_blademaster"));
        Assert.AreEqual(3.0f, _sut.GetTroopWeight("rivendell_royal_knight"));
        Assert.AreEqual(1.0f, _sut.GetTroopWeight("gondor_recruit"));
    }

    [TestMethod]
    public void GetTroopWeight_EmptyWeightDictionary_ReturnsDefault()
    {
        _xmlLoader.GetTroopWeights().Returns(new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase));

        var freshSut = new TroopWeightService(_logger, _xmlLoader);
        var result = freshSut.GetTroopWeight("cave_troll");

        Assert.AreEqual(1.0f, result);
    }

    [TestMethod]
    public void ClearCache_AllowsReloadFromXml()
    {
        _sut.GetTroopWeight("cave_troll");
        _sut.ClearCache();
        _sut.GetTroopWeight("cave_troll");

        _xmlLoader.Received(2).GetTroopWeights();
    }

    // --- ComputeWeightedHealthyAndWounded (phantom-wounded fix core) ---

    // THE regression test. Before the fix, the vanilla tooltip computed
    // wounded = NumberOfAllMembers(weighted=46) - NumberOfHealthyMembers(unweighted=23) = 23 phantom.
    // The weighted split must report 0 wounded for a party that has never fought.
    [TestMethod]
    public void ComputeWeightedHealthyAndWounded_Weight2TroopsNoWounded_WoundedIsZero()
    {
        var elements = new[] { ("imladris_blademaster", 23, 0) };

        var (healthy, wounded) = _sut.ComputeWeightedHealthyAndWounded(elements);

        Assert.AreEqual(46, healthy, "23 weight-2 troops should count as 46 battle-ready");
        Assert.AreEqual(0, wounded, "No real wounds => 0 wounded, not the weight surplus");
    }

    // Invariant that makes the phantom impossible: weighted healthy + weighted wounded == weighted
    // member total, so any consumer doing (AllMembers - HealthyMembers) gets the real wounded count.
    [TestMethod]
    public void ComputeWeightedHealthyAndWounded_HealthyPlusWoundedEqualsWeightedTotal()
    {
        var elements = new[] { ("cave_troll", 5, 2), ("gondor_recruit", 10, 3) };

        var (healthy, wounded) = _sut.ComputeWeightedHealthyAndWounded(elements);

        // cave_troll w4: healthy (5-2)*4=12, wounded 2*4=8. recruit w1: healthy 7, wounded 3.
        Assert.AreEqual(19, healthy);
        Assert.AreEqual(11, wounded);
        int weightedTotal = (5 * 4) + (10 * 1); // Number*weight summed = 30
        Assert.AreEqual(weightedTotal, healthy + wounded, "healthy + wounded must equal weighted member total");
    }

    [TestMethod]
    public void ComputeWeightedHealthyAndWounded_Weight1NoWounded_ReturnsRealCounts()
    {
        var elements = new[] { ("gondor_recruit", 46, 0) };

        var (healthy, wounded) = _sut.ComputeWeightedHealthyAndWounded(elements);

        Assert.AreEqual(46, healthy);
        Assert.AreEqual(0, wounded);
    }

    [TestMethod]
    public void ComputeWeightedHealthyAndWounded_WithRealWounded_WeightsWounded()
    {
        var elements = new[] { ("imladris_blademaster", 10, 3) };

        var (healthy, wounded) = _sut.ComputeWeightedHealthyAndWounded(elements);

        Assert.AreEqual(14, healthy, "(10-3) healthy * weight 2");
        Assert.AreEqual(6, wounded, "3 wounded * weight 2");
    }

    [TestMethod]
    public void ComputeWeightedHealthyAndWounded_Empty_ReturnsZero()
    {
        var (healthy, wounded) = _sut.ComputeWeightedHealthyAndWounded(new (string, int, int)[0]);

        Assert.AreEqual(0, healthy);
        Assert.AreEqual(0, wounded);
    }

    [TestMethod]
    public void ComputeWeightedHealthyAndWounded_Null_ReturnsZero()
    {
        var (healthy, wounded) = _sut.ComputeWeightedHealthyAndWounded(null);

        Assert.AreEqual(0, healthy);
        Assert.AreEqual(0, wounded);
    }

    [TestMethod]
    public void ComputeWeightedHealthyAndWounded_NegativeWounded_TreatedAsZero()
    {
        var elements = new[] { ("gondor_recruit", 5, -2) };

        var (healthy, wounded) = _sut.ComputeWeightedHealthyAndWounded(elements);

        Assert.AreEqual(5, healthy);
        Assert.AreEqual(0, wounded);
    }

    [TestMethod]
    public void ComputeWeightedHealthyAndWounded_WoundedExceedsNumber_HealthyFloorsAtZero()
    {
        var elements = new[] { ("imladris_blademaster", 3, 5) };

        var (healthy, wounded) = _sut.ComputeWeightedHealthyAndWounded(elements);

        Assert.AreEqual(0, healthy, "Healthy must floor at 0, never negative");
        Assert.AreEqual(10, wounded, "5 wounded * weight 2");
    }

    [TestMethod]
    public void ComputeWeightedHealthyAndWounded_FractionalWeight_CeilingsEachSeparately()
    {
        _xmlLoader.GetTroopWeights().Returns(new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "light_scout", 0.5f }
        });
        var sut = new TroopWeightService(_logger, _xmlLoader);
        var elements = new[] { ("light_scout", 3, 1) };

        var (healthy, wounded) = sut.ComputeWeightedHealthyAndWounded(elements);

        Assert.AreEqual(1, healthy, "(3-1)*0.5 = 1.0 -> ceil 1");
        Assert.AreEqual(1, wounded, "1*0.5 = 0.5 -> ceil 1");
    }

    // --- PlanShed (shed-on-upgrade core) ---

    private static double WeightedAfter(
        IReadOnlyList<WeightedTroopEntry> entries, IReadOnlyList<ShedInstruction> plan)
    {
        var remaining = new Dictionary<string, int>();
        foreach (var e in entries)
            remaining[e.TroopId] = e.Count;
        foreach (var s in plan)
            remaining[s.TroopId] -= s.Count;

        double weighted = 0d;
        foreach (var e in entries)
            weighted += (double)remaining[e.TroopId] * e.Weight;
        return weighted;
    }

    [TestMethod]
    public void PlanShed_WithinLimit_ReturnsEmpty()
    {
        var entries = new List<WeightedTroopEntry>
        {
            new("gondor_recruit", 1, 1f, 20, false),
            new("imladris_blademaster", 5, 2f, 4, false), // weighted 20 + 8 = 28
        };

        var plan = _sut.PlanShed(entries, 30);

        Assert.AreEqual(0, plan.Count, "28 weighted <= 30 limit => nothing shed");
    }

    [TestMethod]
    public void PlanShed_ExactlyAtLimit_ReturnsEmpty()
    {
        var entries = new List<WeightedTroopEntry>
        {
            new("imladris_blademaster", 5, 2f, 15, false), // weighted 30
        };

        var plan = _sut.PlanShed(entries, 30);

        Assert.AreEqual(0, plan.Count, "weighted == limit is not over budget");
    }

    [TestMethod]
    public void PlanShed_OverLimit_ShedsLowestTierFirst_KeepingElites()
    {
        var entries = new List<WeightedTroopEntry>
        {
            new("gondor_recruit", 1, 1f, 20, false),       // weighted 20
            new("imladris_blademaster", 5, 2f, 10, false), // weighted 20
        };
        // total weighted 40, limit 30, overflow 10 -> shed 10 recruits (weight 1), elites untouched

        var plan = _sut.PlanShed(entries, 30);

        Assert.AreEqual(1, plan.Count);
        Assert.AreEqual("gondor_recruit", plan[0].TroopId, "lowest-tier fodder shed first");
        Assert.AreEqual(10, plan[0].Count);
        Assert.IsTrue(WeightedAfter(entries, plan) <= 30, "post-shed weighted within limit");
    }

    [TestMethod]
    public void PlanShed_AllElite_ShedsLowestTierElite()
    {
        var entries = new List<WeightedTroopEntry>
        {
            new("imladris_blademaster", 5, 2f, 10, false),   // weighted 20
            new("rivendell_royal_knight", 6, 3f, 10, false), // weighted 30
        };
        // total 50, limit 40, overflow 10 -> shed 5 blademasters (lowest tier), knights untouched

        var plan = _sut.PlanShed(entries, 40);

        Assert.AreEqual(1, plan.Count);
        Assert.AreEqual("imladris_blademaster", plan[0].TroopId, "lowest-tier elite shed, highest tier kept");
        Assert.AreEqual(5, plan[0].Count);
        Assert.IsTrue(WeightedAfter(entries, plan) <= 40);
    }

    [TestMethod]
    public void PlanShed_NeverShedsHeroes()
    {
        var entries = new List<WeightedTroopEntry>
        {
            new("hero_galadriel", 10, 1f, 1, true),    // hero — must never be shed
            new("gondor_recruit", 1, 1f, 50, false),   // weighted 50
        };
        // total weighted 51, limit 30, overflow 21 -> shed 21 recruits, hero kept

        var plan = _sut.PlanShed(entries, 30);

        Assert.IsFalse(plan.Any(s => s.TroopId == "hero_galadriel"), "heroes are never sheddable");
        Assert.AreEqual(1, plan.Count);
        Assert.AreEqual("gondor_recruit", plan[0].TroopId);
        Assert.AreEqual(21, plan[0].Count);
        Assert.IsTrue(WeightedAfter(entries, plan) <= 30);
    }

    [TestMethod]
    public void PlanShed_OnlyHeroesOverLimit_ReturnsEmpty()
    {
        var entries = new List<WeightedTroopEntry>
        {
            new("hero_a", 10, 1f, 20, true),
            new("hero_b", 10, 1f, 20, true), // weighted 40, all heroes
        };

        var plan = _sut.PlanShed(entries, 30);

        Assert.AreEqual(0, plan.Count, "nothing sheddable when only heroes exceed the limit");
    }

    [TestMethod]
    public void PlanShed_CascadesAcrossTiers_WhenLowestTierInsufficient()
    {
        var entries = new List<WeightedTroopEntry>
        {
            new("gondor_recruit", 1, 1f, 5, false),         // weighted 5
            new("imladris_blademaster", 5, 2f, 20, false),  // weighted 40
        };
        // total 45, limit 30, overflow 15. Shed all 5 recruits (-5 -> overflow 10),
        // then ceil(10/2)=5 blademasters (-10). Both tiers appear.

        var plan = _sut.PlanShed(entries, 30);

        Assert.AreEqual(2, plan.Count);
        Assert.AreEqual("gondor_recruit", plan[0].TroopId);
        Assert.AreEqual(5, plan[0].Count);
        Assert.AreEqual("imladris_blademaster", plan[1].TroopId);
        Assert.AreEqual(5, plan[1].Count);
        Assert.IsTrue(WeightedAfter(entries, plan) <= 30);
    }

    [TestMethod]
    public void PlanShed_NullEntries_ReturnsEmpty()
    {
        var plan = _sut.PlanShed(null, 30);

        Assert.AreEqual(0, plan.Count);
    }

    [TestMethod]
    public void PlanShed_EmptyEntries_ReturnsEmpty()
    {
        var plan = _sut.PlanShed(new List<WeightedTroopEntry>(), 30);

        Assert.AreEqual(0, plan.Count);
    }
}
