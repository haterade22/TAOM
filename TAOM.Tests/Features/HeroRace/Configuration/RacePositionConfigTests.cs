using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Features.HeroRace.Configuration;
using System.IO;

namespace TAOM.Tests.Features.HeroRace.Configuration;

[TestClass]
public class RacePositionConfigTests
{
    private string _tempDir;
    private IPathService _pathService;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_Tests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);

        _pathService = Substitute.For<IPathService>();
        _pathService.ConfigPath.Returns(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [TestMethod]
    public void LoadConfig_ValidJson_DeserializesCorrectly()
    {
        var json = @"{""Items"":[{""Race"":""dwarf"",""Horizontal"":0.0,""Vertical"":0.15,""Zoom"":-0.1}]}";
        File.WriteAllText(Path.Combine(_tempDir, "TestConfig.json"), json);

        var config = RacePositionConfig.LoadConfig("TestConfig", _pathService);

        Assert.IsNotNull(config);
        Assert.AreEqual(1, config.Items.Count);
        Assert.AreEqual("dwarf", config.Items[0].Race);
        Assert.AreEqual(0.0f, config.Items[0].Horizontal);
        Assert.AreEqual(0.15f, config.Items[0].Vertical);
        Assert.AreEqual(-0.1f, config.Items[0].Zoom);
    }

    [TestMethod]
    public void LoadConfig_MultipleItems_DeserializesAll()
    {
        var json = @"{""Items"":[
            {""Race"":""dwarf"",""Horizontal"":0.0,""Vertical"":0.15,""Zoom"":-0.1},
            {""Race"":""mount_dwarf"",""Horizontal"":0.0,""Vertical"":0.1,""Zoom"":0.0}
        ]}";
        File.WriteAllText(Path.Combine(_tempDir, "MultiConfig.json"), json);

        var config = RacePositionConfig.LoadConfig("MultiConfig", _pathService);

        Assert.IsNotNull(config);
        Assert.AreEqual(2, config.Items.Count);
        Assert.AreEqual("dwarf", config.Items[0].Race);
        Assert.AreEqual("mount_dwarf", config.Items[1].Race);
    }

    [TestMethod]
    public void LoadConfig_FileDoesNotExist_ReturnsEmptyConfig()
    {
        var config = RacePositionConfig.LoadConfig("NonExistent", _pathService);

        Assert.IsNotNull(config);
        Assert.AreEqual(0, config.Items.Count);
    }

    [TestMethod]
    public void LoadConfig_InvalidJson_ReturnsEmptyConfig()
    {
        File.WriteAllText(Path.Combine(_tempDir, "BadConfig.json"), "not valid json {{{");

        var config = RacePositionConfig.LoadConfig("BadConfig", _pathService);

        Assert.IsNotNull(config);
        Assert.AreEqual(0, config.Items.Count);
    }

    [TestMethod]
    public void LoadConfig_EmptyItems_ReturnsEmptyList()
    {
        var json = @"{""Items"":[]}";
        File.WriteAllText(Path.Combine(_tempDir, "EmptyConfig.json"), json);

        var config = RacePositionConfig.LoadConfig("EmptyConfig", _pathService);

        Assert.IsNotNull(config);
        Assert.AreEqual(0, config.Items.Count);
    }

    [TestMethod]
    public void Constructor_InitializesEmptyItems()
    {
        var config = new RacePositionConfig();

        Assert.IsNotNull(config.Items);
        Assert.AreEqual(0, config.Items.Count);
    }

    [TestMethod]
    public void LoadConfig_CreatesDirectoryIfMissing()
    {
        var subDir = Path.Combine(_tempDir, "nested", "configs");
        _pathService.ConfigPath.Returns(subDir);

        var config = RacePositionConfig.LoadConfig("AnyName", _pathService);

        Assert.IsTrue(Directory.Exists(subDir));
    }
}
