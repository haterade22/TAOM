using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.FactionMap;

namespace TAOM.Tests.Features.FactionMap;

[TestClass]
public class FactionConfigProviderTests
{
    private string _tempDir;
    private IPathService _pathService;
    private IModLogger _logger;
    private FactionConfigProvider _sut;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_FactionMap_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(Path.Combine(_tempDir, "factionmap"));

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new FactionConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [TestMethod]
    public void LoadRegions_ParsesValidJson()
    {
        File.WriteAllText(Path.Combine(_tempDir, "factionmap", "regions.json"), @"{
            ""kingdom_of_gondor"": {
                ""faction"": ""kingdom_of_gondor"",
                ""norm_bbox"": [0.2368, 0.5305, 0.3501, 0.2561],
                ""capital_pos"": [0.47, 0.57]
            },
            ""deco_1"": {
                ""faction"": """",
                ""norm_bbox"": [0.1, 0.2, 0.3, 0.4]
            }
        }");

        var result = _sut.LoadRegions();

        Assert.AreEqual(2, result.Count);
        Assert.IsTrue(result.ContainsKey("kingdom_of_gondor"));
        Assert.AreEqual("kingdom_of_gondor", result["kingdom_of_gondor"].FactionId);
        Assert.AreEqual(0.2368f, result["kingdom_of_gondor"].BBoxX, 0.001f);
        Assert.AreEqual(0.47f, result["kingdom_of_gondor"].CapitalX, 0.001f);
        Assert.IsTrue(result["kingdom_of_gondor"].HasCapitalPos);
        Assert.IsFalse(result["deco_1"].HasCapitalPos);
    }

    [TestMethod]
    public void LoadRegions_MissingFile_ReturnsEmptyAndLogs()
    {
        File.Delete(Path.Combine(_tempDir, "factionmap", "regions.json"));

        var result = _sut.LoadRegions();

        Assert.AreEqual(0, result.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void LoadFactions_ParsesValidJson()
    {
        File.WriteAllText(Path.Combine(_tempDir, "factionmap", "factions.json"), @"{
            ""kingdom_of_gondor"": {
                ""name"": ""Kingdom of Gondor"",
                ""color"": ""#4169E1FF"",
                ""playable"": true,
                ""game_faction"": ""gondor"",
                ""side"": ""free"",
                ""description"": ""The White City"",
                ""image"": ""faction_gondor"",
                ""traits"": [""Stalwart Defenders""],
                ""bonuses"": [{""text"": ""Infantry +10%"", ""positive"": true}],
                ""special_units"": [{""name"": ""Swan Knights"", ""description"": ""Elite cavalry""}],
                ""perks"": [{""name"": ""White Tower"", ""description"": ""Defense +15%""}],
                ""strengths"": [""Strong Infantry""],
                ""weaknesses"": [""Few Cavalry""],
                ""difficulty"": 2
            }
        }");

        var result = _sut.LoadFactions();

        Assert.AreEqual(1, result.Count);
        var gondor = result["kingdom_of_gondor"];
        Assert.AreEqual("Kingdom of Gondor", gondor.Name);
        Assert.AreEqual("#4169E1FF", gondor.Color);
        Assert.IsTrue(gondor.Playable);
        Assert.AreEqual("gondor", gondor.GameFaction);
        Assert.AreEqual("free", gondor.Side);
        Assert.AreEqual(1, gondor.Traits.Length);
        Assert.AreEqual(1, gondor.Bonuses.Length);
        Assert.IsTrue(gondor.Bonuses[0].Positive);
        Assert.AreEqual(1, gondor.SpecialUnits.Length);
        Assert.AreEqual("Swan Knights", gondor.SpecialUnits[0].Name);
        Assert.AreEqual(1, gondor.Perks.Length);
        Assert.AreEqual(1, gondor.Strengths.Length);
        Assert.AreEqual(1, gondor.Weaknesses.Length);
        Assert.AreEqual(2, gondor.Difficulty);
    }

    [TestMethod]
    public void LoadFactions_LegacySingleSpecialUnitForm_CoercedToArray()
    {
        File.WriteAllText(Path.Combine(_tempDir, "factionmap", "factions.json"), @"{
            ""kingdom_of_gondor"": {
                ""name"": ""Kingdom of Gondor"",
                ""special_unit"": {""name"": ""Swan Knights"", ""description"": ""Elite cavalry""}
            }
        }");

        var result = _sut.LoadFactions();

        var gondor = result["kingdom_of_gondor"];
        Assert.AreEqual(1, gondor.SpecialUnits.Length);
        Assert.AreEqual("Swan Knights", gondor.SpecialUnits[0].Name);
        Assert.AreEqual("Elite cavalry", gondor.SpecialUnits[0].Description);
    }

    [TestMethod]
    public void LoadFactions_MultipleSpecialUnits_ParsesAllEntries()
    {
        File.WriteAllText(Path.Combine(_tempDir, "factionmap", "factions.json"), @"{
            ""dominion_of_mordor"": {
                ""name"": ""Dominion of Mordor"",
                ""special_units"": [
                    {""name"": ""Black Uruks"", ""description"": ""Elite Uruk Lines""},
                    {""name"": ""Trolls"", ""description"": ""BAM BAM""}
                ]
            }
        }");

        var result = _sut.LoadFactions();

        var mordor = result["dominion_of_mordor"];
        Assert.AreEqual(2, mordor.SpecialUnits.Length);
        Assert.AreEqual("Black Uruks", mordor.SpecialUnits[0].Name);
        Assert.AreEqual("Elite Uruk Lines", mordor.SpecialUnits[0].Description);
        Assert.AreEqual("Trolls", mordor.SpecialUnits[1].Name);
        Assert.AreEqual("BAM BAM", mordor.SpecialUnits[1].Description);
    }

    [TestMethod]
    public void LoadFactions_MissingFile_ReturnsEmptyAndLogs()
    {
        var result = _sut.LoadFactions();

        Assert.AreEqual(0, result.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void LoadFactions_EmptyFaction_UsesDefaults()
    {
        File.WriteAllText(Path.Combine(_tempDir, "factionmap", "factions.json"), @"{
            ""empty_region"": {}
        }");

        var result = _sut.LoadFactions();

        Assert.AreEqual(1, result.Count);
        var faction = result["empty_region"];
        Assert.AreEqual("", faction.Name);
        Assert.AreEqual("", faction.GameFaction);
        Assert.IsFalse(faction.Playable);
        Assert.AreEqual(0, faction.Traits.Length);
        Assert.AreEqual(0, faction.Bonuses.Length);
        Assert.AreEqual(0, faction.SpecialUnits.Length);
        Assert.AreEqual(0, faction.Difficulty);
    }

    [TestMethod]
    public void LoadFactions_MalformedJson_ReturnsEmpty()
    {
        File.WriteAllText(Path.Combine(_tempDir, "factionmap", "factions.json"), "{ NOT VALID JSON!!!");

        var result = _sut.LoadFactions();

        Assert.AreEqual(0, result.Count);
        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("Failed to parse factions.json")));
    }

    [TestMethod]
    public void LoadRegions_MalformedJson_ReturnsEmpty()
    {
        File.WriteAllText(Path.Combine(_tempDir, "factionmap", "regions.json"), "{ NOT VALID JSON!!!");

        var result = _sut.LoadRegions();

        Assert.AreEqual(0, result.Count);
        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("Failed to parse regions.json")));
    }

    [TestMethod]
    public void LoadRegions_RegionWithoutCapitalPos_HasCapitalPosFalse()
    {
        File.WriteAllText(Path.Combine(_tempDir, "factionmap", "regions.json"), @"{
            ""deco_mountains"": {
                ""faction"": """",
                ""norm_bbox"": [0.5, 0.5, 0.1, 0.1]
            }
        }");

        var result = _sut.LoadRegions();

        Assert.IsFalse(result["deco_mountains"].HasCapitalPos);
        Assert.AreEqual(-1f, result["deco_mountains"].CapitalX, 0.001f);
    }
}
