using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.BanditManagement;

namespace TAOM.Tests.Features.BanditManagement;

[TestClass]
public class BanditScalingConfigProviderTests
{
    private string _tempDir = null!;
    private string _featureDir = null!;
    private IPathService _pathService = null!;
    private IModLogger _logger = null!;
    private BanditScalingConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_BanditScaling_" + Path.GetRandomFileName());
        _featureDir = Path.Combine(_tempDir, "bandit_management");
        Directory.CreateDirectory(_featureDir);

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new BanditScalingConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_featureDir, "bandit_scaling_config.json"), json);

    [TestMethod]
    public void GetConfig_ValidJson_ParsesAllFields()
    {
        WriteConfig(@"{
  ""DensityCurve"": 2.0,
  ""PartySizeCurve"": 1.8,
  ""BossFightCurve"": 2.2,
  ""MaxHideoutsPerFactionCap"": 20,
  ""MaxPartiesPerHideoutCap"": 6,
  ""MinPartiesToInfest"": 3,
  ""InitialHideoutsPerFaction"": 12
}");

        var config = _sut.GetConfig();

        Assert.AreEqual(2.0f, config.DensityCurve, 0.001f);
        Assert.AreEqual(1.8f, config.PartySizeCurve, 0.001f);
        Assert.AreEqual(2.2f, config.BossFightCurve, 0.001f);
        Assert.AreEqual(20, config.MaxHideoutsPerFactionCap);
        Assert.AreEqual(6, config.MaxPartiesPerHideoutCap);
        Assert.AreEqual(3, config.MinPartiesToInfest);
        Assert.AreEqual(12, config.InitialHideoutsPerFaction);
    }

    [TestMethod]
    public void GetConfig_MissingFile_ReturnsDefaultsAndLogsWarning()
    {
        var config = _sut.GetConfig();

        Assert.AreEqual(1.5f, config.DensityCurve, 0.001f);
        Assert.AreEqual(1.5f, config.PartySizeCurve, 0.001f);
        Assert.AreEqual(1.5f, config.BossFightCurve, 0.001f);
        Assert.AreEqual(100, config.MaxHideoutsPerFactionCap);
        Assert.AreEqual(3, config.MaxPartiesPerHideoutCap);
        Assert.AreEqual(1, config.MinPartiesToInfest);
        Assert.AreEqual(14, config.InitialHideoutsPerFaction);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MalformedJson_ReturnsDefaultAndLogsError()
    {
        WriteConfig("not valid json {{{");

        var config = _sut.GetConfig();

        Assert.AreEqual(1.5f, config.DensityCurve, 0.001f);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void GetConfig_CalledTwice_ReturnsSameCachedInstance()
    {
        WriteConfig(@"{ ""DensityCurve"": 2.5 }");

        var first = _sut.GetConfig();
        var second = _sut.GetConfig();

        Assert.AreSame(first, second);
    }

    [TestMethod]
    public void GetConfig_DensityCurveNaN_RevertsToDefault()
    {
        WriteConfig(@"{ ""DensityCurve"": ""NaN"" }");

        var config = _sut.GetConfig();

        Assert.AreEqual(1.5f, config.DensityCurve, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("densityCurve")));
    }

    [TestMethod]
    public void GetConfig_DensityCurveOutOfRange_RevertsToDefault()
    {
        WriteConfig(@"{ ""DensityCurve"": 99.0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(1.5f, config.DensityCurve, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("densityCurve=99")));
    }

    [TestMethod]
    public void GetConfig_DensityCurveNegative_RevertsToDefault()
    {
        WriteConfig(@"{ ""DensityCurve"": -1.0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(1.5f, config.DensityCurve, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("densityCurve=-1")));
    }

    [TestMethod]
    public void GetConfig_PartySizeCurveOutOfRange_RevertsToDefault()
    {
        WriteConfig(@"{ ""PartySizeCurve"": 50.0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(1.5f, config.PartySizeCurve, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("partySizeCurve")));
    }

    [TestMethod]
    public void GetConfig_BossFightCurveOutOfRange_RevertsToDefault()
    {
        WriteConfig(@"{ ""BossFightCurve"": -2.0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(1.5f, config.BossFightCurve, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("bossFightCurve")));
    }

    [TestMethod]
    public void GetConfig_MaxHideoutsCapOutOfRange_RevertsToDefault()
    {
        WriteConfig(@"{ ""MaxHideoutsPerFactionCap"": 500 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(100, config.MaxHideoutsPerFactionCap);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("maxHideoutsPerFactionCap")));
    }

    [TestMethod]
    public void GetConfig_MaxPartiesCapZero_RevertsToDefault()
    {
        WriteConfig(@"{ ""MaxPartiesPerHideoutCap"": 0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(3, config.MaxPartiesPerHideoutCap);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("maxPartiesPerHideoutCap")));
    }

    [TestMethod]
    public void GetConfig_MinPartiesGreaterThanMax_RevertsMinToDefault()
    {
        WriteConfig(@"{
  ""MaxPartiesPerHideoutCap"": 3,
  ""MinPartiesToInfest"": 10
}");

        var config = _sut.GetConfig();

        Assert.AreEqual(1, config.MinPartiesToInfest, "Min must revert when it exceeds max");
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("minPartiesToInfest")));
    }

    [TestMethod]
    public void GetConfig_InitialHideoutsZero_RevertsToDefault()
    {
        WriteConfig(@"{ ""InitialHideoutsPerFaction"": 0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(14, config.InitialHideoutsPerFaction);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("initialHideoutsPerFaction")));
    }

    [TestMethod]
    public void GetConfig_InitialHideoutsTooHigh_RevertsToDefault()
    {
        WriteConfig(@"{ ""InitialHideoutsPerFaction"": 99 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(14, config.InitialHideoutsPerFaction);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("initialHideoutsPerFaction")));
    }

    [TestMethod]
    public void GetConfig_ValidValues_LogsInfoNotWarning()
    {
        WriteConfig(@"{
  ""DensityCurve"": 1.0,
  ""PartySizeCurve"": 1.0,
  ""BossFightCurve"": 1.0,
  ""MaxHideoutsPerFactionCap"": 10,
  ""MaxPartiesPerHideoutCap"": 4,
  ""MinPartiesToInfest"": 2
}");

        _sut.GetConfig();

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("outside") || s.Contains("=")));
    }

    [TestMethod]
    public void GetConfig_EmptyObject_ReturnsDefaultsAndLogsInfo()
    {
        WriteConfig("{}");

        var config = _sut.GetConfig();

        Assert.AreEqual(1.5f, config.DensityCurve, 0.001f);
        Assert.AreEqual(100, config.MaxHideoutsPerFactionCap);
        Assert.AreEqual(14, config.InitialHideoutsPerFaction);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
    }
}
