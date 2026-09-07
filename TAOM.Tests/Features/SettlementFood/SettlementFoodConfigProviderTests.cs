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

    // --- Hinterland rate: must stay STRICTLY below 1 / prosperityFoodDivisor ---
    //
    // At or above that value the net food balance stops falling as prosperity rises, so a surplus
    // fief overflows its store forever, vanilla turns the overflow into prosperity (+0.1/point), and
    // prosperity, town gold and garrison caps inflate without limit. This is the ordering-invariant
    // case in csharp-architecture.md: two individually-valid fields that are invalid together.

    [TestMethod]
    public void GetConfig_ValidHinterlandRate_ParsesThrough()
    {
        // 0.02 is strictly below 1/45 = 0.0222…
        WriteConfig(@"{ ""prosperityFoodDivisor"": 45, ""hinterlandFoodPerProsperity"": 0.02 }");

        var c = _sut.GetConfig();

        Assert.AreEqual(0.02f, c.HinterlandFoodPerProsperity, 0.000001f);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
    }

    [TestMethod]
    public void GetConfig_HinterlandRateEqualToInverseDivisor_RevertsToZeroAndWarns()
    {
        // The exact boundary: 1/40 = 0.025 cancels the consumption term outright, making net food
        // prosperity-INDEPENDENT. That is the runaway case, so the bound is strict, not inclusive.
        WriteConfig(@"{ ""prosperityFoodDivisor"": 40, ""hinterlandFoodPerProsperity"": 0.025 }");

        var c = _sut.GetConfig();

        Assert.AreEqual(0f, c.HinterlandFoodPerProsperity, 0.000001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("hinterlandFoodPerProsperity")));
    }

    [TestMethod]
    public void GetConfig_HinterlandRateAboveInverseDivisor_RevertsToZeroAndWarns()
    {
        WriteConfig(@"{ ""prosperityFoodDivisor"": 40, ""hinterlandFoodPerProsperity"": 0.05 }");

        var c = _sut.GetConfig();

        Assert.AreEqual(0f, c.HinterlandFoodPerProsperity, 0.000001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("hinterlandFoodPerProsperity")));
    }

    [TestMethod]
    public void GetConfig_NegativeHinterlandRate_RevertsToZeroAndWarns()
    {
        WriteConfig(@"{ ""hinterlandFoodPerProsperity"": -0.01 }");

        var c = _sut.GetConfig();

        Assert.AreEqual(0f, c.HinterlandFoodPerProsperity, 0.000001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("hinterlandFoodPerProsperity")));
    }

    [TestMethod]
    public void GetConfig_NaNHinterlandRate_RevertsToZero()
    {
        // Asserts the finiteness check runs BEFORE the ratio comparison: NaN < 1/40 is false, so a
        // bare ordering check alone would pass NaN straight through into the food formula.
        WriteConfig(@"{ ""hinterlandFoodPerProsperity"": NaN }");

        var c = _sut.GetConfig();

        Assert.IsTrue(FiniteFloatValidator.IsFinite(c.HinterlandFoodPerProsperity),
            "NaN hinterlandFoodPerProsperity must be rejected, never surfaced");
        Assert.AreEqual(0f, c.HinterlandFoodPerProsperity, 0.000001f);
    }

    [TestMethod]
    public void GetConfig_InfiniteHinterlandRate_RevertsToZero()
    {
        WriteConfig(@"{ ""hinterlandFoodPerProsperity"": Infinity }");

        var c = _sut.GetConfig();

        Assert.IsTrue(FiniteFloatValidator.IsFinite(c.HinterlandFoodPerProsperity));
        Assert.AreEqual(0f, c.HinterlandFoodPerProsperity, 0.000001f);
    }

    [TestMethod]
    public void GetConfig_HinterlandRateValidatedAgainstTheSanitizedDivisor_NotTheRawOne()
    {
        // A rejected divisor reverts to vanilla 40, so the ratio bound must be computed from the
        // SANITIZED divisor. Checking against the raw 0 would divide by zero; checking against a
        // raw absurd value would wave through a rate the surviving config cannot support.
        WriteConfig(@"{ ""prosperityFoodDivisor"": 0, ""hinterlandFoodPerProsperity"": 0.03 }");

        var c = _sut.GetConfig();

        Assert.AreEqual(40, c.ProsperityFoodDivisor, "invalid divisor reverts to vanilla");
        Assert.AreEqual(0f, c.HinterlandFoodPerProsperity, 0.000001f,
            "0.03 exceeds 1/40, so it must be rejected against the reverted divisor");
    }

    [TestMethod]
    public void GetConfig_HinterlandRateAtDivisorLowerBound_UsesTheWiderBound()
    {
        // prosperityFoodDivisor = 1 is the lowest valid divisor, so the hinterland bound widens to
        // 1/1 = 1.0. A rate that would be rejected at divisor 45 must be accepted here, proving the
        // bound tracks the divisor rather than being a hardcoded constant.
        WriteConfig(@"{ ""prosperityFoodDivisor"": 1, ""hinterlandFoodPerProsperity"": 0.5 }");

        var c = _sut.GetConfig();

        Assert.AreEqual(1, c.ProsperityFoodDivisor);
        Assert.AreEqual(0.5f, c.HinterlandFoodPerProsperity, 0.000001f);
    }

    [TestMethod]
    public void GetConfig_HinterlandRateAtDivisorUpperBound_UsesTheTighterBound()
    {
        // prosperityFoodDivisor = 10000 is the highest valid divisor, so the bound tightens to
        // 1/10000 = 0.0001. The shipped 0.02 would be far too large here and must be rejected.
        WriteConfig(@"{ ""prosperityFoodDivisor"": 10000, ""hinterlandFoodPerProsperity"": 0.02 }");

        var c = _sut.GetConfig();

        Assert.AreEqual(10000, c.ProsperityFoodDivisor);
        Assert.AreEqual(0f, c.HinterlandFoodPerProsperity, 0.000001f,
            "0.02 exceeds 1/10000, so it must be rejected at this divisor");
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
