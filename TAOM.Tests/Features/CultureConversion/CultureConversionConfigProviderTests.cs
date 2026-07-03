using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CultureConversion;

namespace TAOM.Tests.Features.CultureConversion;

[TestClass]
public class CultureConversionConfigProviderTests
{
    private string _tempDir = null!;
    private string _featureDir = null!;
    private IPathService _pathService = null!;
    private IModLogger _logger = null!;
    private CultureConversionConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_CultureConversion_" + Path.GetRandomFileName());
        _featureDir = Path.Combine(_tempDir, "culture_conversion");
        Directory.CreateDirectory(_featureDir);

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new CultureConversionConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_featureDir, "culture_conversion_config.json"), json);

    [TestMethod]
    public void GetConfig_ValidJson_ParsesAllFields()
    {
        WriteConfig(@"{
  ""enabled"": false,
  ""requiredHoldDays"": 30,
  ""requireStableLoyalty"": true,
  ""minLoyaltyToConvert"": 40,
  ""convertPlayerOwnedSettlements"": false,
  ""replaceNotablesOnConversion"": false
}");

        var config = _sut.GetConfig();

        Assert.IsFalse(config.Enabled);
        Assert.AreEqual(30, config.RequiredHoldDays);
        Assert.IsTrue(config.RequireStableLoyalty);
        Assert.AreEqual(40f, config.MinLoyaltyToConvert, 0.001f);
        Assert.IsFalse(config.ConvertPlayerOwnedSettlements);
        Assert.IsFalse(config.ReplaceNotablesOnConversion);
    }

    [TestMethod]
    public void GetConfig_ReplaceNotablesFieldMissing_DefaultsTrue()
    {
        // Pre-feature config files won't have the field — it must default on.
        WriteConfig(@"{ ""requiredHoldDays"": 60 }");

        var config = _sut.GetConfig();

        Assert.IsTrue(config.ReplaceNotablesOnConversion);
    }

    [TestMethod]
    public void GetConfig_MissingFile_ReturnsDefaultsAndLogsWarning()
    {
        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        Assert.AreEqual(45, config.RequiredHoldDays);
        Assert.IsFalse(config.RequireStableLoyalty);
        Assert.IsTrue(config.ConvertPlayerOwnedSettlements);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MalformedJson_ReturnsDefaultsAndLogsError()
    {
        WriteConfig("not valid json {{{");

        var config = _sut.GetConfig();

        Assert.AreEqual(45, config.RequiredHoldDays);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void GetConfig_HoldDaysZero_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""requiredHoldDays"": 0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(45, config.RequiredHoldDays);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("requiredHoldDays=0")));
    }

    [TestMethod]
    public void GetConfig_HoldDaysNegative_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""requiredHoldDays"": -10 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(45, config.RequiredHoldDays);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("requiredHoldDays=-10")));
    }

    [TestMethod]
    public void GetConfig_MinLoyaltyOutOfRange_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""minLoyaltyToConvert"": 150 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(50f, config.MinLoyaltyToConvert, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("minLoyaltyToConvert=150")));
    }

    [TestMethod]
    public void GetConfig_NaNMinLoyalty_RevertsToDefaultAndWarns()
    {
        // NaN comparisons are always false, so a plain range check would let it through — must be guarded.
        WriteConfig(@"{ ""minLoyaltyToConvert"": ""NaN"" }");

        var config = _sut.GetConfig();

        Assert.AreEqual(50f, config.MinLoyaltyToConvert, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("minLoyaltyToConvert")));
    }

    [TestMethod]
    public void GetConfig_ValidValues_LogsInfoNotWarning()
    {
        WriteConfig(@"{ ""requiredHoldDays"": 60, ""minLoyaltyToConvert"": 50 }");

        _sut.GetConfig();

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("outside") || s.Contains("revert")));
    }

    [TestMethod]
    public void GetConfig_CalledTwice_ReturnsSameCachedInstance()
    {
        WriteConfig(@"{ ""requiredHoldDays"": 60 }");

        Assert.AreSame(_sut.GetConfig(), _sut.GetConfig());
    }
}
