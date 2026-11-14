using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.EncounterLines;
using TAOM.Features.Execution;

namespace TAOM.Tests.Features.EncounterLines;

/// <summary>
/// One test per validation rule of <c>encounter_lines/encounter_groups.json</c> (config-providers.md). The provider is
/// fail-soft: a bad row is dropped with a warning, and a missing or broken file gives an empty map, so every line stays vanilla.
/// </summary>
[TestClass]
public class EncounterGroupsConfigProviderTests
{
    private string _tempDir = null!;
    private string _featureDir = null!;
    private IModLogger _logger = null!;
    private EncounterGroupsConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_EncounterGroups_" + Path.GetRandomFileName());
        _featureDir = Path.Combine(_tempDir, "encounter_lines");
        Directory.CreateDirectory(_featureDir);

        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();
        _sut = new EncounterGroupsConfigProvider(pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) => File.WriteAllText(Path.Combine(_featureDir, "encounter_groups.json"), json);

    private const string Valid = @"{
      ""groups"": {
        ""west"": { ""family"": ""man"", ""cultures"": [""gondor"", ""arthedain""] },
        ""orcs"": { ""family"": ""orc"", ""cultures"": [""mordor""] }
      },
      ""races"": { ""human"": ""man"", ""orc"": ""orc"", ""sauron"": ""none"" },
      ""bandits"": {
        ""harad_raiders"": { ""side"": ""evil"", ""family"": ""man"", ""home_group"": ""west"" },
        ""looters"": { ""side"": ""neutral"", ""family"": ""man"" }
      }
    }";

    [TestMethod]
    public void GetMap_ValidFile_LoadsEverySection_WithNoWarning()
    {
        WriteConfig(Valid);

        var map = _sut.GetMap();

        Assert.AreEqual("west", map.GroupOfCulture("gondor"));
        Assert.AreEqual("west", map.GroupOfCulture("arthedain"));
        Assert.AreEqual("orcs", map.GroupOfCulture("mordor"));
        Assert.AreEqual("man", map.FamilyOfGroup("west"));
        Assert.AreEqual("orc", map.FamilyOfGroup("orcs"));
        Assert.AreEqual("none", map.FamilyOfRace("sauron"));
        Assert.AreEqual(new BanditClan(FactionSide.Evil, "man", "west"), map.Bandit("harad_raiders"));
        Assert.AreEqual(new BanditClan(FactionSide.Neutral, "man", null), map.Bandit("looters"));
        Assert.IsNull(map.Bandit("feral_orcs"));
        Assert.IsNull(map.GroupOfCulture("vlandia"));
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains("Loaded")));
    }

    [TestMethod]
    public void GetMap_ABadModulePath_ReturnsAnEmptyMap_AndWarnsOnce_EvenWhenReadTwice()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(_ => throw new System.ArgumentException("bad path"));
        var sut = new EncounterGroupsConfigProvider(pathService, _logger);

        var first = sut.GetMap();
        var second = sut.GetMap();

        Assert.IsNull(first.GroupOfCulture("gondor"));
        Assert.AreSame(first, second);
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetMap_FileMissing_ReturnsAnEmptyMap_AndWarnsOnce()
    {
        var map = _sut.GetMap();

        Assert.AreEqual(0, map.CultureGroups.Count);
        Assert.AreEqual(0, map.Bandits.Count);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("not found")));
    }

    [TestMethod]
    public void GetMap_InvalidJson_ReturnsAnEmptyMap_AndWarnsOnce()
    {
        WriteConfig("{ this is not json");

        var map = _sut.GetMap();

        Assert.AreEqual(0, map.CultureGroups.Count);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("parse")));
    }

    [TestMethod]
    public void GetMap_IsLoadedOnce()
    {
        WriteConfig(Valid);

        Assert.AreSame(_sut.GetMap(), _sut.GetMap());
    }

    [TestMethod]
    public void GetMap_CultureInTwoGroups_KeepsTheFirst_AndWarns()
    {
        WriteConfig(@"{ ""groups"": {
            ""west"": { ""family"": ""man"", ""cultures"": [""gondor""] },
            ""rohirrim"": { ""family"": ""man"", ""cultures"": [""gondor"", ""vlandia""] } } }");

        var map = _sut.GetMap();

        Assert.AreEqual("west", map.GroupOfCulture("gondor"));
        Assert.AreEqual("rohirrim", map.GroupOfCulture("vlandia"));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("'gondor'") && m.Contains("keeping 'west'")));
    }

    [TestMethod]
    public void GetMap_GroupWithAnUnknownFamily_IsDropped_WithItsCultures()
    {
        WriteConfig(@"{ ""groups"": { ""west"": { ""family"": ""human"", ""cultures"": [""gondor""] } } }");

        var map = _sut.GetMap();

        Assert.IsNull(map.FamilyOfGroup("west"));
        Assert.IsNull(map.GroupOfCulture("gondor"));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("groups['west']")));
    }

    [TestMethod]
    public void GetMap_GroupWithFamilyNone_IsDropped()
    {
        WriteConfig(@"{ ""groups"": { ""trolls"": { ""family"": ""none"", ""cultures"": [""wild""] } } }");

        Assert.IsNull(_sut.GetMap().FamilyOfGroup("trolls"));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("groups['trolls']")));
    }

    [TestMethod]
    public void GetMap_RaceWithAnUnknownFamily_IsDropped()
    {
        WriteConfig(@"{ ""races"": { ""human"": ""man"", ""ent"": ""tree"" } }");

        var map = _sut.GetMap();

        Assert.AreEqual("man", map.FamilyOfRace("human"));
        Assert.IsNull(map.FamilyOfRace("ent"));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("races['ent']")));
    }

    [TestMethod]
    public void GetMap_BanditWithAnUnknownSide_IsDropped()
    {
        WriteConfig(@"{ ""bandits"": { ""feral_orcs"": { ""side"": ""chaotic"", ""family"": ""orc"" } } }");

        Assert.IsNull(_sut.GetMap().Bandit("feral_orcs"));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("bandits['feral_orcs']")));
    }

    [TestMethod]
    public void GetMap_BanditWithAnUnknownFamily_IsDropped()
    {
        WriteConfig(@"{ ""bandits"": { ""feral_orcs"": { ""side"": ""evil"", ""family"": ""goblin"" } } }");

        Assert.IsNull(_sut.GetMap().Bandit("feral_orcs"));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("bandits['feral_orcs']")));
    }

    [TestMethod]
    public void GetMap_BanditWithAHomeGroupThatIsNotListed_IsDropped()
    {
        WriteConfig(@"{ ""groups"": { ""west"": { ""family"": ""man"", ""cultures"": [""gondor""] } },
            ""bandits"": { ""harad_raiders"": { ""side"": ""evil"", ""family"": ""man"", ""home_group"": ""harad"" } } }");

        Assert.IsNull(_sut.GetMap().Bandit("harad_raiders"));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("bandits['harad_raiders']")));
    }

    [TestMethod]
    public void GetMap_KeysAndValues_AreTrimmedAndLowerCased()
    {
        WriteConfig(@"{ ""groups"": { "" West "": { ""family"": "" MAN "", ""cultures"": ["" Gondor ""] } },
            ""bandits"": { ""Looters"": { ""side"": ""Neutral"", ""family"": ""Man"" } } }");

        var map = _sut.GetMap();

        Assert.AreEqual("west", map.GroupOfCulture("gondor"));
        Assert.AreEqual("man", map.FamilyOfGroup("west"));
        Assert.IsNotNull(map.Bandit("looters"));
    }

    [TestMethod]
    public void GetMap_InvalidRows_EndWithOneSummaryWarning()
    {
        WriteConfig(@"{ ""races"": { ""ent"": ""tree"", ""eagle"": ""bird"" } }");

        _sut.GetMap();

        _logger.Received(3).LogWarning(Arg.Any<string>());
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("See prior warnings")));
    }
}
