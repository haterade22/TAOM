using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.MarriageAlignment;

namespace TAOM.Tests.Features.MarriageAlignment;

[TestClass]
public class MarriageAlignmentConfigProviderTests
{
    private string _tempDir = null!;
    private string _featureDir = null!;
    private IPathService _pathService = null!;
    private IModLogger _logger = null!;
    private MarriageAlignmentConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_MarriageAlign_" + Path.GetRandomFileName());
        _featureDir = Path.Combine(_tempDir, "marriage_alignment");
        Directory.CreateDirectory(_featureDir);

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new MarriageAlignmentConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_featureDir, "marriage_alignment_config.json"), json);

    [TestMethod]
    public void GetConfig_ValidFile_ParsesAllFields()
    {
        WriteConfig(@"{ ""enabled"": true, ""applyToAi"": false, ""applyToPlayer"": true, ""steerAiPartnerSearch"": false }");

        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        Assert.IsFalse(config.ApplyToAi);
        Assert.IsTrue(config.ApplyToPlayer);
        Assert.IsFalse(config.SteerAiPartnerSearch);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
    }

    [TestMethod]
    public void GetConfig_MissingFile_WarnsAndUsesDefaults()
    {
        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        Assert.IsTrue(config.ApplyToAi);
        Assert.IsTrue(config.ApplyToPlayer);
        Assert.IsTrue(config.SteerAiPartnerSearch);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MalformedJson_LogsErrorAndUsesDefaults()
    {
        WriteConfig("{ this is not json ");

        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        Assert.IsTrue(config.SteerAiPartnerSearch);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void GetConfig_EmptyObject_KeepsCompiledDefaults()
    {
        WriteConfig("{}");

        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        Assert.IsTrue(config.ApplyToAi);
        Assert.IsTrue(config.ApplyToPlayer);
        Assert.IsTrue(config.SteerAiPartnerSearch);
    }

    [TestMethod]
    public void GetConfig_JsonNullLiteral_FallsBackToDefaults()
    {
        WriteConfig("null");

        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        Assert.IsTrue(config.SteerAiPartnerSearch);
    }

    [TestMethod]
    public void GetConfig_AllTogglesOff_RoundTripsFalse()
    {
        WriteConfig(@"{ ""enabled"": false, ""applyToAi"": false, ""applyToPlayer"": false, ""steerAiPartnerSearch"": false }");

        var config = _sut.GetConfig();

        Assert.IsFalse(config.Enabled);
        Assert.IsFalse(config.ApplyToAi);
        Assert.IsFalse(config.ApplyToPlayer);
        Assert.IsFalse(config.SteerAiPartnerSearch);
    }

    [TestMethod]
    public void GetConfig_CalledTwice_ReturnsSameCachedInstance()
    {
        WriteConfig(@"{ ""enabled"": true }");

        var first = _sut.GetConfig();
        var second = _sut.GetConfig();

        Assert.AreSame(first, second);
    }

    [TestMethod]
    public void GetConfig_ShippedFile_ParsesWithEveryToggleOn()
    {
        // Pins the file actually shipped in Main/_Module/ModuleData, not just a synthetic one.
        var shipped = Path.GetFullPath(Path.Combine(
            System.AppDomain.CurrentDomain.BaseDirectory,
            @"..\..\..\..\Main\_Module\ModuleData\marriage_alignment\marriage_alignment_config.json"));

        Assert.IsTrue(File.Exists(shipped), $"Shipped config missing at {shipped}");
        WriteConfig(File.ReadAllText(shipped));

        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        Assert.IsTrue(config.ApplyToAi);
        Assert.IsTrue(config.ApplyToPlayer);
        Assert.IsTrue(config.SteerAiPartnerSearch);
    }
}
