using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CultureMarketplace;
using TAOM.Features.CultureMarketplace.Domain;

namespace TAOM.Tests.Features.CultureMarketplace;

[TestClass]
public class GetRoutedItemsForCultureTests
{
    private IItemPoolAdapter _adapter;
    private ICultureMarketplaceConfigProvider _config;
    private IModLogger _logger;

    [TestInitialize]
    public void Setup()
    {
        _adapter = Substitute.For<IItemPoolAdapter>();
        _config = Substitute.For<ICultureMarketplaceConfigProvider>();
        _logger = Substitute.For<IModLogger>();
        _config.GetOverridesByCulture().Returns(new Dictionary<string, MarketplaceConfigOverride>());
    }

    private CultureItemPoolService NewSut() => new(_adapter, _config, _logger);

    private static Dictionary<string, RoutedItem> WargRouting() => new(System.StringComparer.Ordinal)
    {
        ["warg_brown"]  = new("warg_brown",  new List<string> { "isengard", "mordor", "gundabad", "dolguldur" }, 1),
        ["warg_dark"]   = new("warg_dark",   new List<string> { "isengard", "mordor", "gundabad", "dolguldur" }, 1),
        ["warg_albino"] = new("warg_albino", new List<string> { "isengard", "mordor", "gundabad", "dolguldur" }, 1),
        ["warg_saddle"] = new("warg_saddle", new List<string> { "isengard", "mordor", "gundabad", "dolguldur" }, 1),
    };

    [TestMethod]
    public void GetRoutedItemsForCulture_Isengard_ReturnsAllFourWargs()
    {
        _config.GetItemRouting().Returns(WargRouting());
        var sut = NewSut();

        var routed = sut.GetRoutedItemsForCulture("isengard");

        Assert.AreEqual(4, routed.Count);
        foreach (var entry in routed)
        {
            Assert.IsTrue(entry.ItemId.StartsWith("warg_"));
            Assert.AreEqual(1, entry.MinStock);
        }
    }

    [TestMethod]
    public void GetRoutedItemsForCulture_Mordor_ReturnsAllFourWargs()
    {
        _config.GetItemRouting().Returns(WargRouting());
        var sut = NewSut();

        Assert.AreEqual(4, sut.GetRoutedItemsForCulture("mordor").Count);
        Assert.AreEqual(4, sut.GetRoutedItemsForCulture("gundabad").Count);
        Assert.AreEqual(4, sut.GetRoutedItemsForCulture("dolguldur").Count);
    }

    [TestMethod]
    public void GetRoutedItemsForCulture_Gondor_ReturnsEmpty()
    {
        _config.GetItemRouting().Returns(WargRouting());
        var sut = NewSut();

        Assert.AreEqual(0, sut.GetRoutedItemsForCulture("gondor").Count);
        Assert.AreEqual(0, sut.GetRoutedItemsForCulture("vlandia").Count);
    }

    [TestMethod]
    public void GetRoutedItemsForCulture_AliasInRoutingTarget_NormalizedForLookup()
    {
        // Routing entry lists "rohan" — should be found when looking up "vlandia"
        // (alias normalization on both sides — input-side robustness too).
        var routing = new Dictionary<string, RoutedItem>(System.StringComparer.Ordinal)
        {
            ["special"] = new("special", new List<string> { "rohan", "mordor" }, 0),
        };
        _config.GetItemRouting().Returns(routing);
        var sut = NewSut();

        Assert.AreEqual(1, sut.GetRoutedItemsForCulture("vlandia").Count);
        // Looking up "rohan" also returns the entry — both sides normalize, so
        // a buggy caller asking with the invalid alias still finds the right pool.
        Assert.AreEqual(1, sut.GetRoutedItemsForCulture("rohan").Count);
    }

    [TestMethod]
    public void GetRoutedItemsForCulture_NullOrEmpty_ReturnsEmpty()
    {
        _config.GetItemRouting().Returns(WargRouting());
        var sut = NewSut();

        Assert.AreEqual(0, sut.GetRoutedItemsForCulture(null).Count);
        Assert.AreEqual(0, sut.GetRoutedItemsForCulture("").Count);
    }

    [TestMethod]
    public void GetRoutedItemsForCulture_EmptyRoutingConfig_ReturnsEmpty()
    {
        _config.GetItemRouting().Returns(new Dictionary<string, RoutedItem>());
        var sut = NewSut();

        Assert.AreEqual(0, sut.GetRoutedItemsForCulture("isengard").Count);
    }
}
