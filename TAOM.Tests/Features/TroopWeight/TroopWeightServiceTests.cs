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
}
