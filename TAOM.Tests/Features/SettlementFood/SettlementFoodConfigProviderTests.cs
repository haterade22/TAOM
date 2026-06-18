using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.SettlementFood;

namespace TAOM.Tests.Features.SettlementFood;

[TestClass]
public class SettlementFoodConfigProviderTests
{
    private string _tempDir = null!;
    private string _configDir = null!;
    private IPathService _pathService = null!;
    private IModLogger _logger = null!;
    private SettlementFoodConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_SettlementFood_" + Path.GetRandomFileName());
        _configDir = Path.Combine(_tempDir, "settlement_food");
        Directory.CreateDirectory(_configDir);

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new SettlementFoodConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_configDir, "settlement_food_config.json"), json);

    [TestMethod]
    public void GetConfig_ValidJson_ParsesAllFields()
    {
        WriteConfig(@"{
  ""garrisonFoodDivisor"": 30,
  ""prosperityFoodDivisor"": 60,
  ""townBaseFood"": 25,
  ""castleBaseFood"": 18,
  ""villageFoodMultiplier"": 9,
  ""flatFoodBonus"": 12,
  ""foodStocksUpperLimit"": 500,
  ""castleFoodStockUpperLimitBonus"": 250
}");

        var c = _sut.GetConfig();

        Assert.AreEqual(30, c.GarrisonFoodDivisor);
        Assert.AreEqual(60, c.ProsperityFoodDivisor);
        Assert.AreEqual(25f, c.TownBaseFood, 0.001f);
        Assert.AreEqual(18f, c.CastleBaseFood, 0.001f);
        Assert.AreEqual(9f, c.VillageFoodMultiplier, 0.001f);
        Assert.AreEqual(12f, c.FlatFoodBonus, 0.001f);
        Assert.AreEqual(500, c.FoodStocksUpperLimit);
        Assert.AreEqual(250, c.CastleFoodStockUpperLimitBonus);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
    }

    [TestMethod]
    public void GetConfig_MissingFile_ReturnsDefaultsAndLogsWarning()
    {
        var c = _sut.GetConfig();

        Assert.AreEqual(20, c.GarrisonFoodDivisor);
        Assert.AreEqual(40, c.ProsperityFoodDivisor);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MalformedJson_ReturnsDefaultsAndLogsError()
    {
        WriteConfig("not valid json {{{");

        var c = _sut.GetConfig();

        Assert.AreEqual(20, c.GarrisonFoodDivisor);
        Assert.AreEqual(40, c.ProsperityFoodDivisor);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void GetConfig_PartialJson_MergesWithDefaults()
    {
        WriteConfig(@"{ ""garrisonFoodDivisor"": 35 }");

        var c = _sut.GetConfig();

        Assert.AreEqual(35, c.GarrisonFoodDivisor);
        Assert.AreEqual(40, c.ProsperityFoodDivisor);
        Assert.AreEqual(15f, c.TownBaseFood, 0.001f);
    }

    [TestMethod]
    public void GetConfig_CalledTwice_ReturnsSameCachedInstance()
    {
        WriteConfig(@"{ ""garrisonFoodDivisor"": 25 }");

        Assert.AreSame(_sut.GetConfig(), _sut.GetConfig());
    }

    [TestMethod]
    public void GetConfig_ZeroGarrisonDivisor_RevertsToDefaultAndWarns()
    {
        // A 0 divisor would poison the vanilla food formula with Infinity — must be rejected.
        WriteConfig(@"{ ""garrisonFoodDivisor"": 0 }");

        var c = _sut.GetConfig();

        Assert.AreEqual(20, c.GarrisonFoodDivisor);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("garrisonFoodDivisor=0")));
    }

    [TestMethod]
    public void GetConfig_NegativeProsperityDivisor_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""prosperityFoodDivisor"": -10 }");

        var c = _sut.GetConfig();

        Assert.AreEqual(40, c.ProsperityFoodDivisor);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("prosperityFoodDivisor=-10")));
    }

    [TestMethod]
    public void GetConfig_NegativeTownBaseFood_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""townBaseFood"": -5 }");

        var c = _sut.GetConfig();

        Assert.AreEqual(15f, c.TownBaseFood, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("townBaseFood=-5")));
    }

    [TestMethod]
    public void GetConfig_NaNVillageMultiplier_RevertsToFiniteDefault()
    {
        // NaN must never reach the consumer (IEEE-754 range checks pass NaN through if written naively).
        WriteConfig(@"{ ""villageFoodMultiplier"": NaN }");

        var c = _sut.GetConfig();

        Assert.IsTrue(FiniteFloatValidator.IsFinite(c.VillageFoodMultiplier),
            "NaN villageFoodMultiplier must be rejected, never surfaced");
        Assert.AreEqual(6f, c.VillageFoodMultiplier, 0.001f);
    }

    [TestMethod]
    public void GetConfig_NegativeFlatFoodBonus_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""flatFoodBonus"": -20 }");

        var c = _sut.GetConfig();

        Assert.AreEqual(0f, c.FlatFoodBonus, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("flatFoodBonus=-20")));
    }

    [TestMethod]
    public void GetConfig_ZeroFoodStocksUpperLimit_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""foodStocksUpperLimit"": 0 }");

        var c = _sut.GetConfig();

        Assert.AreEqual(300, c.FoodStocksUpperLimit);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("foodStocksUpperLimit=0")));
    }

    [TestMethod]
    public void GetConfig_NegativeCastleStockBonus_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""castleFoodStockUpperLimitBonus"": -50 }");

        var c = _sut.GetConfig();

        Assert.AreEqual(150, c.CastleFoodStockUpperLimitBonus);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("castleFoodStockUpperLimitBonus=-50")));
    }

    [TestMethod]
    public void GetConfig_ValidValues_LogsInfoNotWarning()
    {
        WriteConfig(@"{ ""garrisonFoodDivisor"": 30, ""prosperityFoodDivisor"": 60 }");

        _sut.GetConfig();

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("outside") || s.Contains("must be")));
    }
}
