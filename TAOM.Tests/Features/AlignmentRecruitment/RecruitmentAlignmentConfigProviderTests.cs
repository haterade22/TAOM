using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.AlignmentRecruitment;

namespace TAOM.Tests.Features.AlignmentRecruitment;

[TestClass]
public class RecruitmentAlignmentConfigProviderTests
{
    private string _tempDir = null!;
    private string _featureDir = null!;
    private IPathService _pathService = null!;
    private IModLogger _logger = null!;
    private RecruitmentAlignmentConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_AlignRecruit_" + Path.GetRandomFileName());
        _featureDir = Path.Combine(_tempDir, "recruitment_alignment");
        Directory.CreateDirectory(_featureDir);

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new RecruitmentAlignmentConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_featureDir, "recruitment_alignment_config.json"), json);

    [TestMethod]
    public void GetConfig_ValidSymmetric_ParsesAllFields()
    {
        WriteConfig(@"{ ""enabled"": true, ""mode"": ""Symmetric"", ""applyToAi"": true, ""applyToPlayer"": false }");

        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        Assert.AreEqual("Symmetric", config.Mode);
        Assert.IsTrue(config.ApplyToAi);
        Assert.IsFalse(config.ApplyToPlayer);
        Assert.IsFalse(config.GoodRejectsEvilOnly);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
    }

    [TestMethod]
    public void GetConfig_ValidGoodRejectsEvil_DerivesFlag()
    {
        WriteConfig(@"{ ""enabled"": true, ""mode"": ""GoodRejectsEvil"", ""applyToAi"": false }");

        var config = _sut.GetConfig();

        Assert.AreEqual("GoodRejectsEvil", config.Mode);
        Assert.IsTrue(config.GoodRejectsEvilOnly);
        Assert.IsFalse(config.ApplyToAi);
    }

    [TestMethod]
    public void GetConfig_ModeCaseInsensitive_CanonicalizesCasing()
    {
        WriteConfig(@"{ ""mode"": ""goodrejectsevil"" }");

        var config = _sut.GetConfig();

        Assert.AreEqual("GoodRejectsEvil", config.Mode);
        Assert.IsTrue(config.GoodRejectsEvilOnly);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("reverting")));
    }

    [TestMethod]
    public void GetConfig_UnknownMode_RevertsToSymmetricAndWarns()
    {
        WriteConfig(@"{ ""mode"": ""Chaotic"" }");

        var config = _sut.GetConfig();

        Assert.AreEqual("Symmetric", config.Mode);
        Assert.IsFalse(config.GoodRejectsEvilOnly);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("mode='Chaotic'")));
    }

    [TestMethod]
    public void GetConfig_MissingFile_ReturnsDefaultsAndLogsWarning()
    {
        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        Assert.AreEqual("Symmetric", config.Mode);
        Assert.IsTrue(config.ApplyToAi);
        Assert.IsTrue(config.ApplyToPlayer);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MalformedJson_ReturnsDefaultsAndLogsError()
    {
        WriteConfig("not valid json {{{");

        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        Assert.AreEqual("Symmetric", config.Mode);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void GetConfig_EmptyObject_ReturnsDefaults()
    {
        WriteConfig("{}");

        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        Assert.AreEqual("Symmetric", config.Mode);
        Assert.IsTrue(config.ApplyToAi);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
    }

    [TestMethod]
    public void GetConfig_CalledTwice_ReturnsSameCachedInstance()
    {
        WriteConfig(@"{ ""mode"": ""Symmetric"" }");

        var first = _sut.GetConfig();
        var second = _sut.GetConfig();

        Assert.AreSame(first, second);
    }
}
