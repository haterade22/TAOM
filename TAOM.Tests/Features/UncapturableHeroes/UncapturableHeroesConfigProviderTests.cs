using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.UncapturableHeroes;

namespace TAOM.Tests.Features.UncapturableHeroes;

/// <summary>
/// One test per validation rule. The syntax-error cases are the easy half; the one that matters is
/// <see cref="GetConfig_AuthoredHeroIds_ReplaceTheDefaults_RatherThanAppending"/>, because Json.NET's
/// default append-merge fails silently and looks completely correct in game.
/// </summary>
[TestClass]
public class UncapturableHeroesConfigProviderTests
{
    private string _tempDir = null!;
    private string _featureDir = null!;
    private IPathService _pathService = null!;
    private IModLogger _logger = null!;
    private UncapturableHeroesConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_Uncapturable_" + Path.GetRandomFileName());
        _featureDir = Path.Combine(_tempDir, "uncapturable_heroes");
        Directory.CreateDirectory(_featureDir);

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new UncapturableHeroesConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_featureDir, "uncapturable_heroes_config.json"), json);

    // ---- Syntax-level failures --------------------------------------------

    [TestMethod]
    public void GetConfig_MissingFile_ReturnsDefaultsAndWarns()
    {
        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        CollectionAssert.AreEqual(new[] { "lord_1_17" }, config.HeroIds.ToArray());
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MalformedJson_ReturnsDefaultsAndLogsError()
    {
        WriteConfig("{ this is not json");

        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        CollectionAssert.AreEqual(new[] { "nazgul_nine" }, config.HeroSets.ToArray());
        _logger.Received().LogError(Arg.Any<string>());
    }

    // ---- The append-merge regression --------------------------------------

    [TestMethod]
    public void GetConfig_AuthoredHeroIds_ReplaceTheDefaults_RatherThanAppending()
    {
        // Without ObjectCreationHandling.Replace this returns ["lord_1_17", "lord_9_9"] and the
        // author silently keeps a hero they deliberately removed.
        WriteConfig(@"{ ""heroIds"": [""lord_9_9""] }");

        var config = _sut.GetConfig();

        CollectionAssert.AreEqual(new[] { "lord_9_9" }, config.HeroIds.ToArray());
    }

    [TestMethod]
    public void GetConfig_AuthoredRaces_ReplaceTheDefaults_RatherThanAppending()
    {
        WriteConfig(@"{ ""uncapturableRaces"": [""cave_troll""] }");

        var config = _sut.GetConfig();

        CollectionAssert.AreEqual(new[] { "cave_troll" }, config.UncapturableRaces.ToArray());
    }

    // ---- Semantic validation ----------------------------------------------

    [TestMethod]
    public void GetConfig_NullHeroIds_RevertsToDefaultsAndWarns()
    {
        WriteConfig(@"{ ""heroIds"": null }");

        var config = _sut.GetConfig();

        CollectionAssert.AreEqual(new[] { "lord_1_17" }, config.HeroIds.ToArray());
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("HeroIds")));
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("contained invalid values")));
    }

    [TestMethod]
    public void GetConfig_NullHeroSets_RevertsToDefaultsAndWarns()
    {
        WriteConfig(@"{ ""heroSets"": null }");

        var config = _sut.GetConfig();

        CollectionAssert.AreEqual(new[] { "nazgul_nine" }, config.HeroSets.ToArray());
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("HeroSets")));
    }

    [TestMethod]
    public void GetConfig_NullExcludeHeroIds_RevertsToDefaultsAndWarns()
    {
        WriteConfig(@"{ ""excludeHeroIds"": null }");

        var config = _sut.GetConfig();

        Assert.AreEqual(0, config.ExcludeHeroIds.Count);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("ExcludeHeroIds")));
    }

    [TestMethod]
    public void GetConfig_EmptyLists_PassThroughUntouched()
    {
        // An empty list is a legitimate "protect nobody by this axis" switch, not an error.
        WriteConfig(@"{ ""heroIds"": [], ""heroSets"": [], ""uncapturableRaces"": [] }");

        var config = _sut.GetConfig();

        Assert.AreEqual(0, config.HeroIds.Count);
        Assert.AreEqual(0, config.HeroSets.Count);
        Assert.AreEqual(0, config.UncapturableRaces.Count);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("contained invalid values")));
    }

    [TestMethod]
    public void GetConfig_BooleansAreHonoured()
    {
        WriteConfig(@"{ ""enabled"": false, ""announceEscape"": false }");

        var config = _sut.GetConfig();

        Assert.IsFalse(config.Enabled);
        Assert.IsFalse(config.AnnounceEscape);
    }

    // ---- Lifetime ---------------------------------------------------------

    [TestMethod]
    public void GetConfig_ReadsTheFileOnce_AcrossManyCalls()
    {
        WriteConfig(@"{ ""enabled"": true }");

        _sut.GetConfig();
        File.Delete(Path.Combine(_featureDir, "uncapturable_heroes_config.json"));
        var second = _sut.GetConfig();

        // Lazy<T> means the deleted file is never noticed. That is also why retuning this file
        // requires a full game restart, not a save reload.
        Assert.IsTrue(second.Enabled);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("not found")));
    }
}
