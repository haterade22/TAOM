using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Models;

namespace TAOM.Tests.Features.Diplomacy;

[TestClass]
public class DiplomacyConfigProviderTests
{
    private string _tempDir;
    private IPathService _pathService;
    private IModLogger _logger;
    private DiplomacyConfigProvider _sut;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_Diplomacy_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(Path.Combine(_tempDir, "diplomacy"));

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new DiplomacyConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [TestMethod]
    public void LoadConfig_ValidJson_ParsesRelationships()
    {
        File.WriteAllText(Path.Combine(_tempDir, "diplomacy", "diplomacy.json"), @"{
            ""relationships"": [
                { ""kingdomA"": ""empire_w"", ""kingdomB"": ""vlandia"", ""tier"": ""Permanent"" },
                { ""kingdomA"": ""empire_w"", ""kingdomB"": ""empire_s"", ""tier"": ""Hostile"" },
                { ""kingdomA"": ""erebor"", ""kingdomB"": ""mirkwood"", ""tier"": ""Natural"" }
            ]
        }");

        var result = _sut.LoadConfig();

        Assert.AreEqual(3, result.Relationships.Count);
        Assert.AreEqual("empire_w", result.Relationships[0].KingdomA);
        Assert.AreEqual("vlandia", result.Relationships[0].KingdomB);
        Assert.AreEqual(AllianceTier.Permanent, result.Relationships[0].Tier);
        Assert.AreEqual(AllianceTier.Hostile, result.Relationships[1].Tier);
        Assert.AreEqual(AllianceTier.Natural, result.Relationships[2].Tier);
    }

    [TestMethod]
    public void LoadConfig_MissingFile_ReturnsEmptyConfig()
    {
        var result = _sut.LoadConfig();

        Assert.AreEqual(0, result.Relationships.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void LoadConfig_InvalidJson_ReturnsEmptyConfig()
    {
        File.WriteAllText(Path.Combine(_tempDir, "diplomacy", "diplomacy.json"), "{ NOT VALID JSON!!!");

        var result = _sut.LoadConfig();

        Assert.AreEqual(0, result.Relationships.Count);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }
}
