using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.FactionUI;
using TAOM.Features.FactionUI.FactionScreen;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// Issue #704. Kysaro's four faction-screen files are hand-edited; every entry is validated, one bad
/// entry is skipped with a warning, and the rest still load (TAOM's config-provider rule).
/// </summary>
[TestClass]
public class FactionScreenConfigProviderTests
{
    private const string Root = @"C:\Game\Modules\TAOM";

    private FakeFrontEndResourceAdapter _adapter = null!;
    private FactionUIPaths _paths = null!;
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleRootPath.Returns(Root);
        pathService.ModuleDataPath.Returns(Path.Combine(Root, "ModuleData"));
        _paths = new FactionUIPaths(pathService);
        _adapter = new FakeFrontEndResourceAdapter();
        _logger = Substitute.For<IModLogger>();
    }

    private void Write(string file, string json) => _adapter.AddFile(Path.Combine(_paths.ConfigDirectory, file), json);

    private FactionScreenConfig Load() => new FactionScreenConfigProvider(_adapter, _paths, _logger).Config;

    [TestMethod]
    public void Config_ReadsEveryFile()
    {
        Write(FactionScreenConfigProvider.CharactersFile, "{ \"_readme\": \"x\", \"kingdom_of_rohan\": \"lord_4_1\" }");
        Write(FactionScreenConfigProvider.KingdomsFile, "{ \"kingdom_of_rohan\": \"vlandia\" }");
        Write(FactionScreenConfigProvider.MapPositionsFile, "{ \"kingdom_of_rohan\": [0.5487, 0.5429] }");
        Write(FactionScreenConfigProvider.ViewportFile, "{ \"dominion_of_mordor\": { \"offset\": 40, \"hide_weapons\": true, \"race\": 0 }, \"kingdom_of_rohan\": 12 }");

        var config = Load();

        Assert.AreEqual("lord_4_1", config.ViewportCharacters["kingdom_of_rohan"]);
        Assert.AreEqual("vlandia", config.Kingdoms["kingdom_of_rohan"]);
        Assert.AreEqual(0.5487, config.MapPositions["kingdom_of_rohan"].X, 1e-9);
        var mordor = config.ViewportTweaks["dominion_of_mordor"];
        Assert.AreEqual(40f, mordor.Offset);
        Assert.IsTrue(mordor.HideWeapons);
        Assert.AreEqual(0, mordor.Race);
        Assert.AreEqual(12f, config.ViewportTweaks["kingdom_of_rohan"].Offset);
        Assert.IsNull(config.ViewportTweaks["kingdom_of_rohan"].Race);
        Assert.IsFalse(config.ViewportCharacters.ContainsKey("_readme"));
    }

    [DataTestMethod]
    [DataRow("\"\"")]
    [DataRow("\"   \"")]
    [DataRow("42")]
    [DataRow("null")]
    public void Config_AnIdThatIsNotANonEmptyString_IsSkippedAndTheRestLoad(string value)
    {
        Write(FactionScreenConfigProvider.CharactersFile, "{ \"bad\": " + value + ", \"kingdom_of_rohan\": \"lord_4_1\" }");

        var config = Load();

        Assert.IsFalse(config.ViewportCharacters.ContainsKey("bad"));
        Assert.AreEqual("lord_4_1", config.ViewportCharacters["kingdom_of_rohan"]);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("\"bad\"")));
    }

    [DataTestMethod]
    [DataRow("[1.5, 0.5]")]
    [DataRow("[-0.1, 0.5]")]
    [DataRow("[0.5]")]
    [DataRow("[0.5, 0.5, 0.5]")]
    [DataRow("[\"0.5\", 0.5]")]
    [DataRow("[NaN, 0.5]")]
    [DataRow("[0.5, Infinity]")]
    [DataRow("0.5")]
    public void Config_AMapPositionOutsideTheMap_IsSkipped(string value)
    {
        Write(FactionScreenConfigProvider.MapPositionsFile, "{ \"bad\": " + value + " }");

        Assert.IsFalse(Load().MapPositions.ContainsKey("bad"));
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("\"bad\"")));
    }

    [DataTestMethod]
    [DataRow("5000")]
    [DataRow("NaN")]
    [DataRow("\"40\"")]
    [DataRow("{ \"offset\": 2000 }")]
    [DataRow("{ \"hide_weapons\": \"yes\" }")]
    [DataRow("{ \"race\": -1 }")]
    [DataRow("{ \"race\": 300 }")]
    [DataRow("{ \"race\": 1.5 }")]
    [DataRow("{ \"hide_weapon\": true }")]
    [DataRow("{ \"offset\": 40, \"Race\": 0 }")]
    public void Config_AViewportTweakOutOfRangeOrMisspelt_IsSkippedWithAWarning(string value)
    {
        Write(FactionScreenConfigProvider.ViewportFile, "{ \"bad\": " + value + " }");

        Assert.IsFalse(Load().ViewportTweaks.ContainsKey("bad"));
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("\"bad\"")));
    }

    [TestMethod]
    public void Config_AnySkippedEntry_EndsWithOneSummaryWarning()
    {
        Write(FactionScreenConfigProvider.CharactersFile, "{ \"bad\": 42 }");
        Write(FactionScreenConfigProvider.MapPositionsFile, "{ \"worse\": [9, 9] }");

        Load();

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("2 entries") && m.Contains("skipped")));
    }

    [TestMethod]
    public void Config_NothingSkipped_GivesNoSummaryWarning()
    {
        Write(FactionScreenConfigProvider.CharactersFile, "{ \"kingdom_of_rohan\": \"lord_4_1\" }");

        Load();

        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Config_MalformedJson_LoadsNothingFromThatFileAndWarns()
    {
        Write(FactionScreenConfigProvider.KingdomsFile, "{ not json");
        Write(FactionScreenConfigProvider.CharactersFile, "{ \"kingdom_of_rohan\": \"lord_4_1\" }");

        var config = Load();

        Assert.AreEqual(0, config.Kingdoms.Count);
        Assert.AreEqual(1, config.ViewportCharacters.Count);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("not valid JSON")));
    }

    [TestMethod]
    public void Config_NoFiles_IsEmpty()
    {
        var config = Load();

        Assert.AreEqual(0, config.ViewportCharacters.Count + config.Kingdoms.Count + config.MapPositions.Count + config.ViewportTweaks.Count);
    }
}
