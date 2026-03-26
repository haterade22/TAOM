using System.Globalization;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.StartupResources;

namespace TAOM.Tests.Features.StartupResources;

[TestClass]
public class StartupResourcesConfigProviderTests
{
    private string _tempDir;
    private IPathService _pathService;
    private IModLogger _logger;
    private StartupResourcesConfigProvider _sut;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_Tests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(_tempDir, "startup_resources"));

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new StartupResourcesConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [TestMethod]
    public void LoadConfig_ValidXml_ParsesAllCultures()
    {
        WriteConfig(@"<?xml version=""1.0"" encoding=""utf-8""?>
<StartupResources>
  <Culture id=""rivendell"" gold=""6000000"" influence=""2000"" />
  <Culture id=""gondor"" gold=""500000"" influence=""100"" />
</StartupResources>");

        var config = _sut.LoadConfig();

        Assert.AreEqual(2, config.CultureEntries.Count);
        Assert.AreEqual("rivendell", config.CultureEntries[0].CultureId);
        Assert.AreEqual(6000000, config.CultureEntries[0].Gold);
        Assert.AreEqual(2000f, config.CultureEntries[0].Influence);
        Assert.AreEqual("gondor", config.CultureEntries[1].CultureId);
        Assert.AreEqual(500000, config.CultureEntries[1].Gold);
        Assert.AreEqual(100f, config.CultureEntries[1].Influence);
    }

    [TestMethod]
    public void LoadConfig_MissingFile_ReturnsEmptyConfigAndLogs()
    {
        var config = _sut.LoadConfig();

        Assert.AreEqual(0, config.CultureEntries.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void LoadConfig_MalformedXml_ReturnsEmptyConfigAndLogs()
    {
        WriteConfig("not valid xml {{{");

        var config = _sut.LoadConfig();

        Assert.AreEqual(0, config.CultureEntries.Count);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void LoadConfig_CachesResult()
    {
        WriteConfig(@"<?xml version=""1.0"" encoding=""utf-8""?>
<StartupResources>
  <Culture id=""gondor"" gold=""500000"" influence=""100"" />
</StartupResources>");

        var first = _sut.LoadConfig();
        var second = _sut.LoadConfig();

        Assert.AreSame(first, second);
    }

    [TestMethod]
    public void LoadConfig_DecimalInfluence_ParsesCorrectly()
    {
        WriteConfig(@"<?xml version=""1.0"" encoding=""utf-8""?>
<StartupResources>
  <Culture id=""erebor"" gold=""1000000"" influence=""1500.5"" />
</StartupResources>");

        var config = _sut.LoadConfig();

        Assert.AreEqual(1500.5f, config.CultureEntries[0].Influence, 0.01f);
    }

    [TestMethod]
    public void LoadConfig_MissingAttributes_DefaultsToZero()
    {
        WriteConfig(@"<?xml version=""1.0"" encoding=""utf-8""?>
<StartupResources>
  <Culture id=""gondor"" />
</StartupResources>");

        var config = _sut.LoadConfig();

        Assert.AreEqual(1, config.CultureEntries.Count);
        Assert.AreEqual("gondor", config.CultureEntries[0].CultureId);
        Assert.AreEqual(0, config.CultureEntries[0].Gold);
        Assert.AreEqual(0f, config.CultureEntries[0].Influence);
    }

    private void WriteConfig(string content)
    {
        File.WriteAllText(
            Path.Combine(_tempDir, "startup_resources", "startup_resources_config.xml"),
            content);
    }
}
