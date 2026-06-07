using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System.Collections.Generic;
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
}
