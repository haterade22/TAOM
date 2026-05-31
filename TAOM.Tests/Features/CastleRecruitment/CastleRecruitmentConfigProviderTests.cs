using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CastleRecruitment;

namespace TAOM.Tests.Features.CastleRecruitment;

[TestClass]
public class CastleRecruitmentConfigProviderTests
{
    private string _tempDir = null!;
    private string _featureDir = null!;
    private IPathService _pathService = null!;
    private IModLogger _logger = null!;
    private CastleRecruitmentConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_CastleRecruitment_" + Path.GetRandomFileName());
        _featureDir = Path.Combine(_tempDir, "castle_recruitment");
        Directory.CreateDirectory(_featureDir);

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new CastleRecruitmentConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_featureDir, "castle_recruitment_config.json"), json);

    [TestMethod]
    public void GetConfig_ValidJson_ParsesAllFields()
    {
        WriteConfig(@"{ ""Enabled"": false, ""AiEnabled"": false, ""NotablesPerCastle"": 4 }");

        var config = _sut.GetConfig();

        Assert.IsFalse(config.Enabled);
        Assert.IsFalse(config.AiEnabled);
        Assert.AreEqual(4, config.NotablesPerCastle);
    }

    [TestMethod]
    public void GetConfig_MissingFile_ReturnsDefaultsAndLogsWarning()
    {
        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        Assert.IsTrue(config.AiEnabled);
        Assert.AreEqual(3, config.NotablesPerCastle);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MalformedJson_ReturnsDefaultsAndLogsError()
    {
        WriteConfig("not valid json {{{");

        var config = _sut.GetConfig();

        Assert.AreEqual(3, config.NotablesPerCastle);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void GetConfig_NotablesPerCastleZero_RevertsToDefault()
    {
        WriteConfig(@"{ ""NotablesPerCastle"": 0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(3, config.NotablesPerCastle);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("notablesPerCastle")));
    }

    [TestMethod]
    public void GetConfig_NotablesPerCastleTooHigh_RevertsToDefault()
    {
        WriteConfig(@"{ ""NotablesPerCastle"": 99 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(3, config.NotablesPerCastle);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("notablesPerCastle=99")));
    }

    [TestMethod]
    public void GetConfig_NotablesPerCastleNegative_RevertsToDefault()
    {
        WriteConfig(@"{ ""NotablesPerCastle"": -2 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(3, config.NotablesPerCastle);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("notablesPerCastle")));
    }

    [TestMethod]
    public void GetConfig_ValidValues_LogsInfoNotWarning()
    {
        WriteConfig(@"{ ""Enabled"": true, ""AiEnabled"": true, ""NotablesPerCastle"": 2 }");

        _sut.GetConfig();

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("outside") || s.Contains("notablesPerCastle=")));
    }

    [TestMethod]
    public void GetConfig_EmptyObject_ReturnsDefaultsAndLogsInfo()
    {
        WriteConfig("{}");

        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        Assert.AreEqual(3, config.NotablesPerCastle);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
    }

    [TestMethod]
    public void GetConfig_CalledTwice_ReturnsSameCachedInstance()
    {
        WriteConfig(@"{ ""NotablesPerCastle"": 2 }");

        var first = _sut.GetConfig();
        var second = _sut.GetConfig();

        Assert.AreSame(first, second);
    }
}
