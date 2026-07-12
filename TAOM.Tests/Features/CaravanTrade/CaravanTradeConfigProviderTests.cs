using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CaravanTrade;

namespace TAOM.Tests.Features.CaravanTrade;

[TestClass]
public class CaravanTradeConfigProviderTests
{
    private string _tempDir = null!;
    private string _configDir = null!;
    private IPathService _pathService = null!;
    private IModLogger _logger = null!;
    private CaravanTradeConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_CaravanTrade_" + Path.GetRandomFileName());
        _configDir = Path.Combine(_tempDir, "caravan_trade");
        Directory.CreateDirectory(_configDir);

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new CaravanTradeConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_configDir, "caravan_trade_config.json"), json);

    [TestMethod]
    public void GetConfig_ValidJson_ParsesAllFieldsAndLogsInfo()
    {
        WriteConfig(@"{
  ""enabled"": true,
  ""applyToPlayerCaravans"": false,
  ""rangeMultiplier"": 2.0,
  ""distanceDecayExponent"": 0.6,
  ""nearFieldFlattenDays"": 3.0,
  ""maxCompensation"": 8.0,
  ""antiShuttlePenalty"": 0.5,
  ""homeDistanceReweight"": false,
  ""warTradePolicy"": ""IgnoreWar"",
  ""budgetFactorFloor"": 0.4,
  ""initialTradeGold"": 20000,
  ""maxGoldPerCategory"": 2500
}");

        var c = _sut.GetConfig();

        Assert.IsTrue(c.Enabled);
        Assert.IsFalse(c.ApplyToPlayerCaravans);
        Assert.AreEqual(2.0f, c.RangeMultiplier, 0.0001f);
        Assert.AreEqual(0.6f, c.DistanceDecayExponent, 0.0001f);
        Assert.AreEqual(3.0f, c.NearFieldFlattenDays, 0.0001f);
        Assert.AreEqual(8.0f, c.MaxCompensation, 0.0001f);
        Assert.AreEqual(0.5f, c.AntiShuttlePenalty, 0.0001f);
        Assert.IsFalse(c.HomeDistanceReweight);
        Assert.AreEqual("IgnoreWar", c.WarTradePolicy);
        Assert.AreEqual(0.4f, c.BudgetFactorFloor, 0.0001f);
        Assert.AreEqual(20000, c.InitialTradeGold);
        Assert.AreEqual(2500, c.MaxGoldPerCategory);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
    }

    [TestMethod]
    public void GetConfig_MissingFile_ReturnsDefaultsAndWarns()
    {
        var c = _sut.GetConfig();

        Assert.AreEqual(1.6f, c.RangeMultiplier, 0.0001f);
        Assert.AreEqual("SameAlignmentAndNeutral", c.WarTradePolicy);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MalformedJson_ReturnsDefaultsAndLogsError()
    {
        WriteConfig("not valid json {{{");

        var c = _sut.GetConfig();

        Assert.AreEqual(1.6f, c.RangeMultiplier, 0.0001f);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void GetConfig_PartialJson_MergesWithDefaults()
    {
        WriteConfig(@"{ ""rangeMultiplier"": 2.5 }");

        var c = _sut.GetConfig();

        Assert.AreEqual(2.5f, c.RangeMultiplier, 0.0001f);
        Assert.AreEqual(0.5f, c.DistanceDecayExponent, 0.0001f);
        Assert.AreEqual("SameAlignmentAndNeutral", c.WarTradePolicy);
    }

    [TestMethod]
    public void GetConfig_CalledTwice_ReturnsSameCachedInstance()
    {
        WriteConfig(@"{ ""rangeMultiplier"": 2.0 }");
        Assert.AreSame(_sut.GetConfig(), _sut.GetConfig());
    }

    [TestMethod]
    public void GetConfig_RangeMultiplierBelowOne_RevertsAndWarns()
    {
        // Below 1 shrinks the range below vanilla — worsening the very shuttle this feature fixes.
        WriteConfig(@"{ ""rangeMultiplier"": 0.5 }");
        Assert.AreEqual(1.6f, _sut.GetConfig().RangeMultiplier, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("rangeMultiplier")));
    }

    [TestMethod]
    public void GetConfig_RangeMultiplierOversized_RevertsAndWarns()
    {
        WriteConfig(@"{ ""rangeMultiplier"": 12 }");
        Assert.AreEqual(1.6f, _sut.GetConfig().RangeMultiplier, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("rangeMultiplier")));
    }

    [TestMethod]
    public void GetConfig_NaNRangeMultiplier_RevertsToFiniteDefault()
    {
        WriteConfig(@"{ ""rangeMultiplier"": NaN }");
        Assert.AreEqual(1.6f, _sut.GetConfig().RangeMultiplier, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("rangeMultiplier")));
    }

    [TestMethod]
    public void GetConfig_DecayExponentZero_RevertsAndWarns()
    {
        WriteConfig(@"{ ""distanceDecayExponent"": 0 }");
        Assert.AreEqual(0.5f, _sut.GetConfig().DistanceDecayExponent, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("distanceDecayExponent")));
    }

    [TestMethod]
    public void GetConfig_InfinityNearFieldFlatten_RevertsAndWarns()
    {
        WriteConfig(@"{ ""nearFieldFlattenDays"": Infinity }");
        Assert.AreEqual(2.0f, _sut.GetConfig().NearFieldFlattenDays, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("nearFieldFlattenDays")));
    }

    [TestMethod]
    public void GetConfig_MaxCompensationBelowOne_RevertsAndWarns()
    {
        WriteConfig(@"{ ""maxCompensation"": 0.5 }");
        Assert.AreEqual(6.0f, _sut.GetConfig().MaxCompensation, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("maxCompensation")));
    }

    [TestMethod]
    public void GetConfig_AntiShuttlePenaltyAboveOne_RevertsAndWarns()
    {
        // > 1 would flip the score sign.
        WriteConfig(@"{ ""antiShuttlePenalty"": 1.5 }");
        Assert.AreEqual(0.5f, _sut.GetConfig().AntiShuttlePenalty, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("antiShuttlePenalty")));
    }

    [TestMethod]
    public void GetConfig_NegativeAntiShuttlePenalty_RevertsAndWarns()
    {
        // < 0 would reward returning to a just-visited town.
        WriteConfig(@"{ ""antiShuttlePenalty"": -0.2 }");
        Assert.AreEqual(0.5f, _sut.GetConfig().AntiShuttlePenalty, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("antiShuttlePenalty")));
    }

    [TestMethod]
    public void GetConfig_MissingHomeDistanceReweight_DefaultsTrue()
    {
        WriteConfig(@"{ ""rangeMultiplier"": 1.6 }");
        Assert.IsTrue(_sut.GetConfig().HomeDistanceReweight);
    }

    [TestMethod]
    public void GetConfig_HomeDistanceReweightFalse_Honored()
    {
        WriteConfig(@"{ ""homeDistanceReweight"": false }");
        Assert.IsFalse(_sut.GetConfig().HomeDistanceReweight);
        // A bool has no invalid-but-parseable state -> no warning.
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("homeDistanceReweight")));
    }

    [TestMethod]
    public void GetConfig_UnknownWarTradePolicy_RevertsAndWarns()
    {
        // The M1 typo trap: an unknown string must revert, not silently take the service switch default.
        WriteConfig(@"{ ""warTradePolicy"": ""IgnoreWarr"" }");
        Assert.AreEqual("SameAlignmentAndNeutral", _sut.GetConfig().WarTradePolicy);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("warTradePolicy")));
    }

    [TestMethod]
    public void GetConfig_KnownWarTradePolicyCaseInsensitive_Accepted()
    {
        WriteConfig(@"{ ""warTradePolicy"": ""ignorewar"" }");
        Assert.AreEqual("ignorewar", _sut.GetConfig().WarTradePolicy);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("warTradePolicy")));
    }

    [TestMethod]
    public void GetConfig_BudgetFactorFloorAboveOne_RevertsAndWarns()
    {
        WriteConfig(@"{ ""budgetFactorFloor"": 1.5 }");
        Assert.AreEqual(0.35f, _sut.GetConfig().BudgetFactorFloor, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("budgetFactorFloor")));
    }

    [TestMethod]
    public void GetConfig_InitialTradeGoldTooLow_RevertsAndWarns()
    {
        WriteConfig(@"{ ""initialTradeGold"": 500 }");
        Assert.AreEqual(15000, _sut.GetConfig().InitialTradeGold);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("initialTradeGold")));
    }

    [TestMethod]
    public void GetConfig_MaxGoldPerCategoryTooHigh_RevertsAndWarns()
    {
        WriteConfig(@"{ ""maxGoldPerCategory"": 999999 }");
        Assert.AreEqual(1500, _sut.GetConfig().MaxGoldPerCategory);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("maxGoldPerCategory")));
    }

    [TestMethod]
    public void GetConfig_AllValid_LogsInfoNotWarning()
    {
        WriteConfig(@"{ ""rangeMultiplier"": 1.6, ""warTradePolicy"": ""None"" }");

        _sut.GetConfig();

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }
}
