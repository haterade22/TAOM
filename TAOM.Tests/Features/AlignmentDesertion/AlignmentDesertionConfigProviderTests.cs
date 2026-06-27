using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.AlignmentDesertion;

namespace TAOM.Tests.Features.AlignmentDesertion;

[TestClass]
public class AlignmentDesertionConfigProviderTests
{
    private string _tempDir = null!;
    private string _featureDir = null!;
    private IPathService _pathService = null!;
    private IModLogger _logger = null!;
    private AlignmentDesertionConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_AlignDesert_" + Path.GetRandomFileName());
        _featureDir = Path.Combine(_tempDir, "alignment_desertion");
        Directory.CreateDirectory(_featureDir);

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new AlignmentDesertionConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_featureDir, "alignment_desertion_config.json"), json);

    [TestMethod]
    public void GetConfig_ValidConfig_ParsesAllFields()
    {
        WriteConfig(@"{ ""Enabled"": false, ""Rate"": 0.25, ""ApplyToAi"": false, ""ApplyToPlayer"": false, ""ApplyToParties"": false, ""ApplyToGarrisons"": false }");

        var config = _sut.GetConfig();

        Assert.IsFalse(config.Enabled);
        Assert.AreEqual(0.25f, config.Rate);
        Assert.IsFalse(config.ApplyToAi);
        Assert.IsFalse(config.ApplyToPlayer);
        Assert.IsFalse(config.ApplyToParties);
        Assert.IsFalse(config.ApplyToGarrisons);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
    }

    [TestMethod]
    public void GetConfig_ValidRate_PreservedWithoutWarning()
    {
        WriteConfig(@"{ ""Rate"": 0.75 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(0.75f, config.Rate);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("rate")));
    }

    [TestMethod]
    public void GetConfig_RateAboveOne_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""Rate"": 1.5 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(0.5f, config.Rate); // compiled default
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("rate")));
    }

    [TestMethod]
    public void GetConfig_RateBelowZero_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""Rate"": -0.5 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(0.5f, config.Rate);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("rate")));
    }

    [TestMethod]
    public void GetConfig_RateNaN_RevertsToDefaultAndWarns()
    {
        // IEEE-754 NaN passes a naive `< min || > max` check; FiniteFloatValidator must reject it.
        WriteConfig(@"{ ""Rate"": ""NaN"" }");

        var config = _sut.GetConfig();

        Assert.AreEqual(0.5f, config.Rate);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("rate")));
    }

    [TestMethod]
    public void GetConfig_RateInfinity_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""Rate"": ""Infinity"" }");

        var config = _sut.GetConfig();

        Assert.AreEqual(0.5f, config.Rate);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("rate")));
    }

    [TestMethod]
    public void GetConfig_RateZero_Preserved()
    {
        // 0 is in-range (a valid "no desertion" rate) — must NOT revert.
        WriteConfig(@"{ ""Rate"": 0.0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(0f, config.Rate);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("rate")));
    }

    [TestMethod]
    public void GetConfig_RateOne_Preserved()
    {
        WriteConfig(@"{ ""Rate"": 1.0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(1f, config.Rate);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("rate")));
    }

    [TestMethod]
    public void GetConfig_MissingFile_ReturnsDefaultsAndLogsWarning()
    {
        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        Assert.AreEqual(0.5f, config.Rate);
        Assert.IsTrue(config.ApplyToAi);
        Assert.IsTrue(config.ApplyToPlayer);
        Assert.IsTrue(config.ApplyToParties);
        Assert.IsTrue(config.ApplyToGarrisons);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MalformedJson_ReturnsDefaultsAndLogsError()
    {
        WriteConfig("not valid json {{{");

        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        Assert.AreEqual(0.5f, config.Rate);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void GetConfig_EmptyObject_ReturnsDefaults()
    {
        WriteConfig("{}");

        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        Assert.AreEqual(0.5f, config.Rate);
        Assert.IsTrue(config.ApplyToAi);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
    }

    [TestMethod]
    public void GetConfig_CalledTwice_ReturnsSameCachedInstance()
    {
        WriteConfig(@"{ ""Rate"": 0.5 }");

        var first = _sut.GetConfig();
        var second = _sut.GetConfig();

        Assert.AreSame(first, second);
    }
}
