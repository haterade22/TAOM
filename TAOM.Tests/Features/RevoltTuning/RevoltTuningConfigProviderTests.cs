using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.RevoltTuning;

namespace TAOM.Tests.Features.RevoltTuning;

[TestClass]
public class RevoltTuningConfigProviderTests
{
    private string _tempDir = null!;
    private string _configsDir = null!;
    private IPathService _pathService = null!;
    private IModLogger _logger = null!;
    private RevoltTuningConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_RevoltTuning_" + Path.GetRandomFileName());
        _configsDir = Path.Combine(_tempDir, "configs");
        Directory.CreateDirectory(_configsDir);

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new RevoltTuningConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_configsDir, "revolt_tuning_config.json"), json);

    [TestMethod]
    public void GetConfig_ValidJson_ParsesAllFields()
    {
        WriteConfig(@"{
  ""rebellionStartLoyaltyThreshold"": 3,
  ""rebelliousStateStartLoyaltyThreshold"": 8,
  ""settlementOwnerDifferentCultureLoyaltyEffect"": -0.75
}");

        var config = _sut.GetConfig();

        Assert.AreEqual(3, config.RebellionStartLoyaltyThreshold);
        Assert.AreEqual(8, config.RebelliousStateStartLoyaltyThreshold);
        Assert.AreEqual(-0.75f, config.SettlementOwnerDifferentCultureLoyaltyEffect, 0.001f);
    }

    [TestMethod]
    public void GetConfig_MissingFile_ReturnsDefaultAndLogsWarning()
    {
        var config = _sut.GetConfig();

        Assert.AreEqual(5, config.RebellionStartLoyaltyThreshold);
        Assert.AreEqual(10, config.RebelliousStateStartLoyaltyThreshold);
        Assert.AreEqual(-1.0f, config.SettlementOwnerDifferentCultureLoyaltyEffect, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MalformedJson_ReturnsDefaultAndLogsError()
    {
        WriteConfig("not valid json {{{");

        var config = _sut.GetConfig();

        Assert.AreEqual(5, config.RebellionStartLoyaltyThreshold);
        Assert.AreEqual(10, config.RebelliousStateStartLoyaltyThreshold);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void GetConfig_PartialJson_MergesWithDefaults()
    {
        WriteConfig(@"{
  ""rebellionStartLoyaltyThreshold"": 2
}");

        var config = _sut.GetConfig();

        Assert.AreEqual(2, config.RebellionStartLoyaltyThreshold);
        Assert.AreEqual(10, config.RebelliousStateStartLoyaltyThreshold);
        Assert.AreEqual(-1.0f, config.SettlementOwnerDifferentCultureLoyaltyEffect, 0.001f);
    }

    [TestMethod]
    public void GetConfig_CalledTwice_ReturnsSameCachedInstance()
    {
        WriteConfig(@"{
  ""rebellionStartLoyaltyThreshold"": 7
}");

        var first = _sut.GetConfig();
        var second = _sut.GetConfig();

        Assert.AreSame(first, second);
    }

    [TestMethod]
    public void DefaultConfig_MatchesSoftTuneSpec()
    {
        var config = new RevoltTuningConfig();

        Assert.AreEqual(5, config.RebellionStartLoyaltyThreshold,
            "Soft tune: revolt threshold should be 5 (vanilla 15)");
        Assert.AreEqual(10, config.RebelliousStateStartLoyaltyThreshold,
            "Soft tune: rebellious state threshold should be 10 (vanilla 25)");
        Assert.AreEqual(-1.0f, config.SettlementOwnerDifferentCultureLoyaltyEffect, 0.001f,
            "Soft tune: owner-culture penalty should be -1.0 (vanilla -3.0)");
    }

    [TestMethod]
    public void GetConfig_RebellionThresholdOutOfRange_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""rebellionStartLoyaltyThreshold"": 200 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(5, config.RebellionStartLoyaltyThreshold);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("rebellionStartLoyaltyThreshold=200")));
    }

    [TestMethod]
    public void GetConfig_RebelliousThresholdNegative_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""rebelliousStateStartLoyaltyThreshold"": -5 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(10, config.RebelliousStateStartLoyaltyThreshold);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("rebelliousStateStartLoyaltyThreshold=-5")));
    }

    [TestMethod]
    public void GetConfig_RebelliousLowerThanRebellion_RevertsBothToDefaults()
    {
        WriteConfig(@"{
  ""rebellionStartLoyaltyThreshold"": 30,
  ""rebelliousStateStartLoyaltyThreshold"": 20
}");

        var config = _sut.GetConfig();

        Assert.AreEqual(5, config.RebellionStartLoyaltyThreshold);
        Assert.AreEqual(10, config.RebelliousStateStartLoyaltyThreshold);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("must be >=")));
    }

    [TestMethod]
    public void GetConfig_PositiveOwnerCulturePenalty_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""settlementOwnerDifferentCultureLoyaltyEffect"": 1.0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(-1.0f, config.SettlementOwnerDifferentCultureLoyaltyEffect, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("settlementOwnerDifferentCultureLoyaltyEffect=1")));
    }

    [TestMethod]
    public void GetConfig_ValidValues_LogsInfoNotWarning()
    {
        WriteConfig(@"{
  ""rebellionStartLoyaltyThreshold"": 3,
  ""rebelliousStateStartLoyaltyThreshold"": 8,
  ""settlementOwnerDifferentCultureLoyaltyEffect"": -0.5
}");

        _sut.GetConfig();

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("outside") || s.Contains("must be")));
    }

    [TestMethod]
    public void GetConfig_EmptyObject_ReturnsDefaultsAndLogsInfo()
    {
        WriteConfig("{}");

        var config = _sut.GetConfig();

        Assert.AreEqual(5, config.RebellionStartLoyaltyThreshold);
        Assert.AreEqual(10, config.RebelliousStateStartLoyaltyThreshold);
        Assert.AreEqual(-1.0f, config.SettlementOwnerDifferentCultureLoyaltyEffect, 0.001f);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
    }

    // v1.5.0 Civil Unrest support: one test per validation rule, per .claude/rules/tests.md.

    [TestMethod]
    public void GetConfig_HighRebellionDefaults_MatchTaomSofteningRatio()
    {
        var config = new RevoltTuningConfig();

        // Vanilla is 50/60 under Civil Unrest; TAOM softens by the same /3 and /2.5 it applies to
        // the base 15/25 pair, so the modifier stays meaningful without matching vanilla's severity.
        Assert.AreEqual(17, config.HighRebellionStartLoyaltyThreshold);
        Assert.AreEqual(24, config.HighRebellionRebelliousStateStartLoyaltyThreshold);
        Assert.IsTrue(config.HighRebellionStartLoyaltyThreshold > config.RebellionStartLoyaltyThreshold,
            "Civil Unrest must raise the rebellion threshold above the normal one or it does nothing.");
    }

    [TestMethod]
    public void GetConfig_HighRebellionThresholdOutOfRange_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""highRebellionStartLoyaltyThreshold"": 250 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(17, config.HighRebellionStartLoyaltyThreshold);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("highRebellionStartLoyaltyThreshold=250")));
    }

    [TestMethod]
    public void GetConfig_HighRebelliousLowerThanHighRebellion_RevertsBothToDefaults()
    {
        WriteConfig(@"{ ""highRebellionStartLoyaltyThreshold"": 40, ""highRebellionRebelliousStateStartLoyaltyThreshold"": 10 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(17, config.HighRebellionStartLoyaltyThreshold);
        Assert.AreEqual(24, config.HighRebellionRebelliousStateStartLoyaltyThreshold);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("must be >= highRebellionStartLoyaltyThreshold")));
    }
}
