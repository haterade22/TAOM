using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar;

/// <summary>
/// maps.json: a scene with a bad id or layout mode is skipped with one warning; a bad layout option, prefab or camp value reverts
/// to the file's default (or the built-in one) with one warning and keeps the scene.
/// </summary>
[TestClass]
public class FordsMapsLoadTests
{
    private const string Scene = "taom_rohan_battle_001_forceatmo";

    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup() => _logger = Substitute.For<IModLogger>();

    private FordsConfigCatalog Load(string? maps) =>
        FordsConfigCatalog.FromTexts("{}", FordsConfigFixtures.Factions, maps, _logger);

    private static string Scenes(string scenes, string defaults = "") =>
        "{ " + (defaults.Length == 0 ? "" : "'defaults': " + defaults + ", ") + "'scenes': " + scenes + " }";

    private void AssertOneWarningNaming(params string[] parts) =>
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("maps.json") && parts.All(p => m.Contains(p))));

    [TestMethod]
    public void Maps_GoodFile_ParsesTheScene()
    {
        var catalog = Load(FordsConfigFixtures.Maps);

        Assert.AreEqual(1, catalog.Maps.Count);
        var map = catalog.Maps[0];
        Assert.AreEqual(Scene, map.SceneId);
        Assert.AreEqual("{=aom_taom_rohan_battle_001_forceatmo_name}[Rohan] Rohan Plains", map.DisplayName);
        Assert.AreEqual(MapLayoutMode.Generated, map.Layout);
        Assert.AreEqual(3, map.Options.LaneCount);
        Assert.AreEqual(45f, map.Options.LaneSpread);
        Assert.AreEqual(12f, map.Options.WaypointStep);
        CollectionAssert.AreEqual(new[] { 0.36f, 0.2f }, map.Options.TowerFractions.ToList());
        Assert.AreEqual(0.04f, map.Options.KeepInset);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Maps_GoodFile_ParsesPrefabsAndCampDefaults()
    {
        var map = Load(FordsConfigFixtures.Maps).Maps[0];

        Assert.AreEqual(new TowerPrefab("siege_barricade_a", 1.0f), map.Tower);
        Assert.AreEqual(new TowerPrefab("siege_barricade_a", 1.6f), map.Keep);
        Assert.AreEqual(new CampDefaults("hill_troll", 2, 250), map.Camp);
    }

    [TestMethod]
    public void Maps_NoDefaultsBlock_UsesTheBuiltInDefaults()
    {
        var map = Load(Scenes("[{ 'scene': '" + Scene + "' }]")).Maps[0];

        Assert.AreEqual(new LayoutOptions().LaneCount, map.Options.LaneCount);
        Assert.AreEqual(new LayoutOptions().LaneSpread, map.Options.LaneSpread);
        CollectionAssert.AreEqual(new LayoutOptions().TowerFractions.ToList(), map.Options.TowerFractions.ToList());
        Assert.AreEqual("siege_barricade_a", map.Tower.Prefab);
        Assert.AreEqual("siege_barricade_a", map.Keep.Prefab);
        Assert.AreEqual(MapLayoutMode.Generated, map.Layout);
        Assert.AreEqual(Scene, map.DisplayName, "no name: the scene id stands in");
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Maps_SceneOptionsOverrideTheDefaults()
    {
        var map = Load(Scenes("[{ 'scene': 'a', 'options': { 'laneSpread': 60, 'towerFractions': [0.4, 0.25, 0.1] } }]",
            "{ 'options': { 'laneSpread': 30, 'keepInset': 0.1 } }")).Maps[0];

        Assert.AreEqual(60f, map.Options.LaneSpread);
        Assert.AreEqual(0.1f, map.Options.KeepInset, "not overridden: the file default");
        CollectionAssert.AreEqual(new[] { 0.4f, 0.25f, 0.1f }, map.Options.TowerFractions.ToList());
    }

    [TestMethod]
    public void Maps_ScenePrefabAndCampOverrideTheDefaults()
    {
        var map = Load(Scenes("[{ 'scene': 'a', 'towerPrefab': { 'scale': 2 }, 'camp': { 'reward': 99 } }]",
            "{ 'towerPrefab': { 'prefab': 'siege_barricade_b', 'scale': 1 }, 'camp': { 'troopId': 'x', 'count': 3, 'reward': 10 } }")).Maps[0];

        Assert.AreEqual(new TowerPrefab("siege_barricade_b", 2f), map.Tower);
        Assert.AreEqual(new CampDefaults("x", 3, 99), map.Camp);
    }

    [TestMethod]
    public void Maps_TagsLayout_IsParsed()
    {
        var map = Load(Scenes("[{ 'scene': 'a', 'layout': 'Tags' }]")).Maps[0];

        Assert.AreEqual(MapLayoutMode.Tags, map.Layout);
    }

    [TestMethod]
    public void Maps_UnknownLayoutMode_SkipsTheSceneAndKeepsTheNext()
    {
        var catalog = Load(Scenes("[{ 'scene': 'bad', 'layout': 'hexagonal' }, { 'scene': 'good' }]"));

        CollectionAssert.AreEqual(new[] { "good" }, catalog.Maps.Select(m => m.SceneId).ToList());
        AssertOneWarningNaming("bad", "layout");
    }

    [TestMethod]
    public void Maps_SceneWithoutId_IsSkipped()
    {
        var catalog = Load(Scenes("[{ 'name': 'nameless' }, { 'scene': 'good' }]"));

        CollectionAssert.AreEqual(new[] { "good" }, catalog.Maps.Select(m => m.SceneId).ToList());
        AssertOneWarningNaming("scene");
    }

    [TestMethod]
    public void Maps_SceneNotAnObject_IsSkipped()
    {
        var catalog = Load(Scenes("[7, { 'scene': 'good' }]"));

        CollectionAssert.AreEqual(new[] { "good" }, catalog.Maps.Select(m => m.SceneId).ToList());
        AssertOneWarningNaming("not an object");
    }

    [TestMethod]
    public void Maps_DuplicateScene_KeepsTheFirst()
    {
        var catalog = Load(Scenes("[{ 'scene': 'a', 'name': 'first' }, { 'scene': 'A', 'name': 'second' }]"));

        Assert.AreEqual(1, catalog.Maps.Count);
        Assert.AreEqual("first", catalog.Maps[0].DisplayName);
        AssertOneWarningNaming("duplicate");
    }

    [TestMethod]
    public void Maps_LaneCountOutOfRange_RevertsOnlyThatOption()
    {
        var map = Load(Scenes("[{ 'scene': 'a', 'options': { 'laneCount': 9, 'laneSpread': 50 } }]")).Maps[0];

        Assert.AreEqual(3, map.Options.LaneCount);
        Assert.AreEqual(50f, map.Options.LaneSpread);
        AssertOneWarningNaming("a", "laneCount");
    }

    [TestMethod]
    public void Maps_NaNLaneSpread_Reverts()
    {
        var map = Load(Scenes("[{ 'scene': 'a', 'options': { 'laneSpread': NaN } }]")).Maps[0];

        Assert.AreEqual(45f, map.Options.LaneSpread);
        AssertOneWarningNaming("laneSpread");
    }

    [TestMethod]
    public void Maps_TowerFractionPastTheMiddle_RevertsTheWholeList()
    {
        var map = Load(Scenes("[{ 'scene': 'a', 'options': { 'towerFractions': [0.36, 0.7] } }]")).Maps[0];

        CollectionAssert.AreEqual(new[] { 0.36f, 0.2f }, map.Options.TowerFractions.ToList());
        AssertOneWarningNaming("towerFractions");
    }

    [TestMethod]
    public void Maps_EmptyTowerFractions_Reverts()
    {
        var map = Load(Scenes("[{ 'scene': 'a', 'options': { 'towerFractions': [] } }]")).Maps[0];

        Assert.AreEqual(2, map.Options.TowerFractions.Count);
        AssertOneWarningNaming("towerFractions");
    }

    [TestMethod]
    public void Maps_BadPrefabScale_RevertsToTheDefaultScale()
    {
        var map = Load(Scenes("[{ 'scene': 'a', 'towerPrefab': { 'scale': 0 } }]")).Maps[0];

        Assert.AreEqual(1f, map.Tower.Scale);
        AssertOneWarningNaming("towerPrefab.scale");
    }

    [TestMethod]
    public void Maps_BlankPrefabName_RevertsToTheDefaultPrefab()
    {
        var map = Load(Scenes("[{ 'scene': 'a', 'keepPrefab': { 'prefab': '  ' } }]")).Maps[0];

        Assert.AreEqual("siege_barricade_a", map.Keep.Prefab);
        AssertOneWarningNaming("keepPrefab.prefab");
    }

    [TestMethod]
    public void Maps_NegativeCampReward_Reverts()
    {
        var map = Load(Scenes("[{ 'scene': 'a', 'camp': { 'reward': -5 } }]")).Maps[0];

        Assert.AreEqual(250, map.Camp.Reward);
        AssertOneWarningNaming("camp.reward");
    }

    [TestMethod]
    public void Maps_Lookup_IsCaseInsensitive()
    {
        var catalog = Load(FordsConfigFixtures.Maps);

        Assert.IsTrue(catalog.TryGetMap(Scene.ToUpperInvariant(), out var map));
        Assert.AreEqual(Scene, map.SceneId);
        Assert.IsFalse(catalog.TryGetMap("nowhere", out _));
        Assert.IsFalse(catalog.TryGetMap("", out _));
    }

    [TestMethod]
    public void Maps_FileOrder_IsKept()
    {
        var catalog = Load(Scenes("[{ 'scene': 'c' }, { 'scene': 'a' }, { 'scene': 'b' }]"));

        CollectionAssert.AreEqual(new[] { "c", "a", "b" }, catalog.Maps.Select(m => m.SceneId).ToList());
    }

    [TestMethod]
    public void Maps_FileMissing_GivesNoMapsAndOneWarning()
    {
        var catalog = Load(null);

        Assert.AreEqual(0, catalog.Maps.Count);
        AssertOneWarningNaming("maps.json");
    }

    [TestMethod]
    public void Maps_NotJson_GivesNoMapsWithoutThrowing()
    {
        var catalog = Load("{{{");

        Assert.AreEqual(0, catalog.Maps.Count);
        AssertOneWarningNaming("maps.json");
    }

    [TestMethod]
    public void Maps_ScenesNotAnArray_GivesNoMapsAndOneWarning()
    {
        var catalog = Load("{ 'scenes': { 'a': 1 } }");

        Assert.AreEqual(0, catalog.Maps.Count);
        AssertOneWarningNaming("'scenes'");
    }
}
