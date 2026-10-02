using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.FactionMap;
using TAOM.Features.FactionMap.Models;
using TAOM.Features.FactionUI;
using TAOM.Features.FactionUI.FactionScreen;
using TAOM.Features.FactionUI.Resources;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// Issue #704. The faction screen lists exactly TAOM's playable factions that own a region, with
/// TAOM's own text in the player's language, the side and difficulty label the faction map uses, and
/// Kysaro's art and minimap pins.
/// </summary>
[TestClass]
public class FactionScreenCatalogTests
{
    private const string Root = @"C:\Game\Modules\TAOM";

    private FakeFrontEndResourceAdapter _adapter = null!;
    private FactionUIPaths _paths = null!;
    private IModLogger _logger = null!;
    private Dictionary<string, FactionData> _factions = null!;
    private Dictionary<string, RegionData> _regions = null!;

    [TestInitialize]
    public void Setup()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleRootPath.Returns(Root);
        pathService.ModuleDataPath.Returns(Path.Combine(Root, "ModuleData"));
        _paths = new FactionUIPaths(pathService);
        _adapter = new FakeFrontEndResourceAdapter();
        _adapter.AddFile(Path.Combine(_paths.RuntimeSprites, "fs_emblem_kingdom_of_rohan.png"));
        _adapter.AddFile(Path.Combine(_paths.RuntimeSprites, "fs_territory_kingdom_of_rohan.png"));
        _logger = Substitute.For<IModLogger>();

        _factions = new Dictionary<string, FactionData>
        {
            ["kingdom_of_rohan"] = new()
            {
                Name = "{=k}Kingdom of Rohan", Description = "Horse lords \u2014 of the Mark", Playable = true,
                GameFaction = "vlandia", Difficulty = 3, Side = "free",
                Bonuses = new[] { new FactionBonus { Text = "{=b}Fast horses", Positive = true }, new FactionBonus { Text = "{=b2}Few walls", Positive = false } },
                Strengths = new[] { "{=s}Cavalry" }, Weaknesses = new[] { "{=w}Sieges" },
            },
            ["kingdom_of_arthedain"] = new() { Name = "{=a}Arthedain", Playable = false, GameFaction = "vlandia" },
            ["havens_of_umbar"] = new() { Name = "{=u}Umbar", Playable = true, GameFaction = "umbar", Side = "neutral" },
            ["no_region_faction"] = new() { Name = "{=n}Nowhere", Playable = true, GameFaction = "empire" },
        };
        _regions = new Dictionary<string, RegionData>
        {
            ["Rohan"] = new() { FactionId = "kingdom_of_rohan", CapitalX = 0.2f, CapitalY = 0.3f },
            ["Rohan East"] = new() { FactionId = "kingdom_of_rohan" },
            ["Arthedain"] = new() { FactionId = "kingdom_of_arthedain" },
            ["Umbar"] = new() { FactionId = "havens_of_umbar" },
        };
    }

    private FactionScreenCatalog Catalog()
    {
        var localizer = Substitute.For<ITextLocalizerAdapter>();
        localizer.Localize(Arg.Any<string>()).Returns(c => ((string)c[0]).Substring(((string)c[0]).IndexOf('}') + 1));
        var selection = Substitute.For<IFactionSelectionService>();
        selection.FormatDifficultyText(3).Returns("{=taom_faction_difficulty_3}Difficulty: Medium");
        selection.FormatDifficultyText(Arg.Is<int>(d => d != 3)).Returns("");
        return new FactionScreenCatalog(
            new FactionScreenConfigProvider(_adapter, _paths, _logger),
            new FrontEndSpriteService(_adapter, _paths, _logger),
            localizer,
            selection,
            _logger);
    }

    private List<FactionInfo> LoadPlayable() => Catalog().LoadPlayable(_regions, _factions);

    private void WriteConfig(string file, string json) => _adapter.AddFile(Path.Combine(_paths.ConfigDirectory, file), json);

    [TestMethod]
    public void LoadPlayable_ListsOnlyPlayableFactionsThatOwnARegion()
    {
        CollectionAssert.AreEqual(new[] { "kingdom_of_rohan", "havens_of_umbar" }, LoadPlayable().Select(f => f.Key).ToList());
    }

    [TestMethod]
    public void LoadPlayable_ConfirmsThroughTheFactionsFirstRegion()
    {
        Assert.AreEqual("Rohan", LoadPlayable().First().RegionName);
    }

    [TestMethod]
    public void LoadPlayable_LocalizesTheTextAndSoftensLongDashes()
    {
        var rohan = LoadPlayable().First();

        Assert.AreEqual("Kingdom of Rohan", rohan.Name);
        Assert.AreEqual("Horse lords, of the Mark", rohan.Description);
        Assert.AreEqual("Fast horses", rohan.Benefits[0].Key);
        Assert.IsFalse(rohan.Benefits[1].Value);
        CollectionAssert.AreEqual(new[] { "Cavalry" }, rohan.Strengths);
        CollectionAssert.AreEqual(new[] { "Sieges" }, rohan.Weaknesses);
    }

    [TestMethod]
    public void LoadPlayable_TakesTheDifficultyLabelFromTheFactionMap()
    {
        var factions = LoadPlayable();

        Assert.AreEqual("Difficulty: Medium", factions[0].DifficultyText);
        Assert.AreEqual("", factions[1].DifficultyText, "a difficulty the faction map has no label for shows none");
    }

    [TestMethod]
    public void LoadPlayable_TakesTheAlignmentFromTheFactionsSide()
    {
        var factions = LoadPlayable();

        Assert.AreEqual("free", factions[0].Alignment);
        Assert.AreEqual("neutral", factions[1].Alignment);
    }

    [TestMethod]
    public void LoadPlayable_UsesTheEmblemAndTerritoryImagesWhenTheyExist()
    {
        var factions = LoadPlayable();

        Assert.AreEqual("fs_emblem_kingdom_of_rohan", factions[0].IconSprite);
        Assert.AreEqual("fs_territory_kingdom_of_rohan", factions[0].TerritorySprite);
        Assert.AreEqual(FactionScreenArt.DefaultEmblem, factions[1].IconSprite);
        Assert.AreEqual("", factions[1].TerritorySprite);
    }

    [TestMethod]
    public void LoadPlayable_TakesTheMinimapPinFromTheCapitalUnlessKysaroPlacedIt()
    {
        Assert.AreEqual(0.2, LoadPlayable()[0].MapX, 1e-6);

        WriteConfig(FactionScreenConfigProvider.MapPositionsFile, "{ \"kingdom_of_rohan\": [0.55, 0.54] }");

        Assert.AreEqual(0.55, LoadPlayable()[0].MapX, 1e-6);
    }

    [TestMethod]
    public void LoadPlayable_AppliesKysarosArtTableAndKingdomMap()
    {
        WriteConfig(FactionScreenConfigProvider.KingdomsFile, "{ \"kingdom_of_rohan\": \"vlandia\" }");

        var rohan = LoadPlayable()[0];

        Assert.AreEqual("fs_portrait_rohan", rohan.PaintedPortraitSprite);
        Assert.AreEqual("vlandia", rohan.KingdomId);
        Assert.AreEqual(FactionScreenArt.DefaultBackground, rohan.BackgroundSprite);
    }

    [TestMethod]
    public void LoadPlayable_ATuningKeyThatNamesNoPlayableFaction_IsReportedOnce()
    {
        WriteConfig(FactionScreenConfigProvider.KingdomsFile, "{ \"kingdom_of_rohan\": \"vlandia\", \"kingdom_of_rohhan\": \"vlandia\" }");
        WriteConfig(FactionScreenConfigProvider.ViewportFile, "{ \"kingdom_of_arthedain\": 10 }");
        var catalog = Catalog();

        catalog.LoadPlayable(_regions, _factions);
        catalog.LoadPlayable(_regions, _factions);

        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("kingdom_of_rohhan") && s.Contains(FactionScreenConfigProvider.KingdomsFile)));
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("kingdom_of_arthedain") && s.Contains(FactionScreenConfigProvider.ViewportFile)));
    }

    [TestMethod]
    public void LoadPlayable_EveryTuningKeyNamesAPlayableFaction_WarnsNothing()
    {
        WriteConfig(FactionScreenConfigProvider.KingdomsFile, "{ \"kingdom_of_rohan\": \"vlandia\" }");

        LoadPlayable();

        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }
}
