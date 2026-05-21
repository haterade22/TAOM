using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CultureMarketplace;
using TAOM.Features.CultureMarketplace.Domain;

namespace TAOM.Tests.Features.CultureMarketplace;

/// <summary>
/// Pure-function tests for the extracted classifier. The classifier is the shared
/// truth for "what culture is this item?" — used by BuildPools, the filter pass,
/// and the guaranteed-stock pass. Behavior must match the chain encoded in
/// BuildPools: attribute → prefix fallback → alias normalization → null.
/// </summary>
[TestClass]
public class CultureItemPoolServiceClassifierTests
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
        _config.GetItemRouting().Returns(new Dictionary<string, RoutedItem>());
    }

    private CultureItemPoolService NewSut() => new(_adapter, _config, _logger);

    [TestMethod]
    public void ClassifyEffectiveCulture_AttributePresent_AttributeWins()
    {
        var sut = NewSut();
        Assert.AreEqual("gondor", sut.ClassifyEffectiveCulture("gondor", "isengard"));
    }

    [TestMethod]
    public void ClassifyEffectiveCulture_AttributeEmpty_PrefixUsed()
    {
        var sut = NewSut();
        Assert.AreEqual("mirkwood", sut.ClassifyEffectiveCulture(null, "mirkwood"));
        Assert.AreEqual("mirkwood", sut.ClassifyEffectiveCulture("", "mirkwood"));
    }

    [TestMethod]
    public void ClassifyEffectiveCulture_RohanAttribute_NormalizesToVlandia()
    {
        var sut = NewSut();
        Assert.AreEqual("vlandia", sut.ClassifyEffectiveCulture("rohan", null));
    }

    [TestMethod]
    public void ClassifyEffectiveCulture_RohanPrefix_NormalizesToVlandia()
    {
        var sut = NewSut();
        Assert.AreEqual("vlandia", sut.ClassifyEffectiveCulture(null, "rohan"));
    }

    [TestMethod]
    public void ClassifyEffectiveCulture_BothEmpty_ReturnsNull()
    {
        var sut = NewSut();
        Assert.IsNull(sut.ClassifyEffectiveCulture(null, null));
        Assert.IsNull(sut.ClassifyEffectiveCulture("", ""));
        Assert.IsNull(sut.ClassifyEffectiveCulture(null, ""));
    }

    [TestMethod]
    public void ClassifyEffectiveCulture_AttributeCaseInsensitiveAlias()
    {
        var sut = NewSut();
        Assert.AreEqual("vlandia", sut.ClassifyEffectiveCulture("ROHAN", null));
        Assert.AreEqual("vlandia", sut.ClassifyEffectiveCulture("Rohan", null));
    }
}
