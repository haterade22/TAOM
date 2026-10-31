using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar;

/// <summary>
/// factions.json: a bad unit, hero or free-wave entry is skipped with one warning that names the faction and the entry, and never
/// costs a good entry its place; a faction left with no unit offers is skipped; a file that cannot be read leaves no factions.
/// </summary>
[TestClass]
public class FordsFactionsLoadTests
{
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup() => _logger = Substitute.For<IModLogger>();

    private FordsConfigCatalog Load(string? factions) =>
        FordsConfigCatalog.FromTexts("{}", factions, FordsConfigFixtures.Maps, _logger);

    private static string OneFaction(string units, string heroes = "[]", string waves = "[{ 'fromMinute': 0, 'units': [{ 'troop': 'a', 'count': 1 }] }]") =>
        "{ 'factions': { 'gondor': { 'name': 'Gondor', 'culture': 'gondor', 'units': " + units + ", 'heroes': " + heroes
        + ", 'freeWaves': " + waves + " } } }";

    private void AssertOneWarningNaming(params string[] parts) =>
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("factions.json") && parts.All(p => m.Contains(p))));

    [TestMethod]
    public void Factions_GoodFile_ParsesBothFactionsInFileOrder()
    {
        var catalog = Load(FordsConfigFixtures.Factions);

        CollectionAssert.AreEqual(new[] { "gondor", "mordor" }, catalog.Factions.Select(f => f.FactionId).ToList());
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Factions_GoodFile_ParsesUnitsHeroesAndCulture()
    {
        Assert.IsTrue(Load(FordsConfigFixtures.Factions).TryGetFaction("gondor", out var gondor));

        Assert.AreEqual("gondor", gondor.CultureId);
        Assert.AreEqual(new UnitOffer("militia", "gondor_ano_militia", UnitRole.Infantry, 10, 100), gondor.Units[0]);
        Assert.AreEqual(1, gondor.Heroes.Count);
        Assert.AreEqual(new HeroOffer("lord_1_9", 1000), gondor.Heroes[0]);
    }

    [TestMethod]
    public void Factions_UnitWithoutPrice_UsesTheMountedLevelPrice()
    {
        Assert.IsTrue(Load(FordsConfigFixtures.Factions).TryGetFaction("gondor", out var gondor));

        var riders = gondor.Units.Single(u => u.OfferId == "riders");
        Assert.AreEqual(TroopPricing.PackPrice(21, true, 1f, 5), riders.PackPrice);
        Assert.AreEqual(135, riders.PackPrice);
    }

    [TestMethod]
    public void Factions_UnitWithoutPrice_UsesTheEliteWeight()
    {
        var catalog = Load(OneFaction("[{ 'id': 'troll', 'troop': 'hill_troll', 'role': 'Siege', 'packSize': 1, 'level': 51, 'eliteWeight': 4 }]"));

        Assert.IsTrue(catalog.TryGetFaction("gondor", out var faction));
        Assert.AreEqual(420, faction.Units[0].PackPrice);
    }

    [TestMethod]
    public void Factions_ExplicitPrice_WinsOverLevel()
    {
        var catalog = Load(OneFaction("[{ 'id': 'a', 'troop': 'a', 'role': 'Infantry', 'packSize': 10, 'level': 11, 'price': 777 }]"));

        Assert.IsTrue(catalog.TryGetFaction("gondor", out var faction));
        Assert.AreEqual(777, faction.Units[0].PackPrice);
    }

    [TestMethod]
    public void Factions_FreeWaveTiers_AreSortedByMinute()
    {
        Assert.IsTrue(Load(FordsConfigFixtures.Factions).TryGetFaction("gondor", out var gondor));

        CollectionAssert.AreEqual(new[] { 0f, 10f }, gondor.FreeWaves.Select(t => t.FromMinute).ToList());
        Assert.AreEqual(new UnitCount("gondor_ano_militia", 6), gondor.FreeWaves[0].Units[0]);
        Assert.AreEqual(new UnitCount("gondor_ano_footman", 8), gondor.FreeWaves[1].Units[0]);
    }

    [TestMethod]
    public void Factions_DisplayName_IsTheNameFieldAndFallsBackToTheId()
    {
        var catalog = Load(FordsConfigFixtures.Factions);

        Assert.AreEqual("{=TAOM_FOW_Faction_Gondor}Gondor", catalog.FactionDisplayName("gondor"));
        Assert.AreEqual("nowhere", catalog.FactionDisplayName("nowhere"));
    }

    [TestMethod]
    public void Factions_Lookup_IsCaseInsensitive()
    {
        var catalog = Load(FordsConfigFixtures.Factions);

        Assert.IsTrue(catalog.TryGetFaction("GONDOR", out _));
        Assert.IsFalse(catalog.TryGetFaction("rohan", out _));
        Assert.IsFalse(catalog.TryGetFaction("", out _));
    }

    [TestMethod]
    public void Factions_FileMissing_GivesNoFactionsAndOneWarning()
    {
        var catalog = Load(null);

        Assert.AreEqual(0, catalog.Factions.Count);
        AssertOneWarningNaming("factions.json");
    }

    [TestMethod]
    public void Factions_NotJson_GivesNoFactionsWithoutThrowing()
    {
        var catalog = Load("not json at all");

        Assert.AreEqual(0, catalog.Factions.Count);
        AssertOneWarningNaming("factions.json");
    }

    [TestMethod]
    public void Factions_FactionsKeyNotAnObject_GivesNoFactionsAndOneWarning()
    {
        var catalog = Load("{ 'factions': [] }");

        Assert.AreEqual(0, catalog.Factions.Count);
        AssertOneWarningNaming("'factions'");
    }

    [TestMethod]
    public void Factions_UnitWithZeroPackSize_IsSkippedAndTheRestKept()
    {
        var catalog = Load(OneFaction("[{ 'id': 'bad', 'troop': 'a', 'role': 'Infantry', 'packSize': 0, 'price': 50 },"
            + " { 'id': 'good', 'troop': 'b', 'role': 'Infantry', 'packSize': 5, 'price': 50 }]"));

        Assert.IsTrue(catalog.TryGetFaction("gondor", out var faction));
        CollectionAssert.AreEqual(new[] { "good" }, faction.Units.Select(u => u.OfferId).ToList());
        AssertOneWarningNaming("gondor", "bad");
    }

    [TestMethod]
    public void Factions_UnitWithUnknownRole_IsSkipped()
    {
        var catalog = Load(OneFaction("[{ 'id': 'bad', 'troop': 'a', 'role': 'Wizard', 'packSize': 5, 'price': 50 },"
            + " { 'id': 'good', 'troop': 'b', 'role': 'spear', 'packSize': 5, 'price': 50 }]"));

        Assert.IsTrue(catalog.TryGetFaction("gondor", out var faction));
        Assert.AreEqual(UnitRole.Spear, faction.Units.Single().Role);
        AssertOneWarningNaming("gondor", "bad", "role");
    }

    [TestMethod]
    public void Factions_UnitWithHeroRole_IsSkipped()
    {
        var catalog = Load(OneFaction("[{ 'id': 'bad', 'troop': 'a', 'role': 'Hero', 'packSize': 1, 'price': 50 },"
            + " { 'id': 'good', 'troop': 'b', 'role': 'Infantry', 'packSize': 5, 'price': 50 }]"));

        Assert.IsTrue(catalog.TryGetFaction("gondor", out var faction));
        CollectionAssert.AreEqual(new[] { "good" }, faction.Units.Select(u => u.OfferId).ToList());
        AssertOneWarningNaming("bad", "role");
    }

    [TestMethod]
    public void Factions_UnitWithNeitherPriceNorLevel_IsSkipped()
    {
        var catalog = Load(OneFaction("[{ 'id': 'bad', 'troop': 'a', 'role': 'Infantry', 'packSize': 5 },"
            + " { 'id': 'good', 'troop': 'b', 'role': 'Infantry', 'packSize': 5, 'price': 50 }]"));

        Assert.IsTrue(catalog.TryGetFaction("gondor", out var faction));
        CollectionAssert.AreEqual(new[] { "good" }, faction.Units.Select(u => u.OfferId).ToList());
        AssertOneWarningNaming("bad", "price");
    }

    [TestMethod]
    public void Factions_UnitWithoutTroop_IsSkipped()
    {
        var catalog = Load(OneFaction("[{ 'id': 'bad', 'role': 'Infantry', 'packSize': 5, 'price': 50 },"
            + " { 'id': 'good', 'troop': 'b', 'role': 'Infantry', 'packSize': 5, 'price': 50 }]"));

        Assert.IsTrue(catalog.TryGetFaction("gondor", out var faction));
        CollectionAssert.AreEqual(new[] { "good" }, faction.Units.Select(u => u.OfferId).ToList());
        AssertOneWarningNaming("bad", "troop");
    }

    [TestMethod]
    public void Factions_DuplicateOfferId_KeepsTheFirst()
    {
        var catalog = Load(OneFaction("[{ 'id': 'a', 'troop': 'one', 'role': 'Infantry', 'packSize': 5, 'price': 50 },"
            + " { 'id': 'a', 'troop': 'two', 'role': 'Infantry', 'packSize': 5, 'price': 60 }]"));

        Assert.IsTrue(catalog.TryGetFaction("gondor", out var faction));
        Assert.AreEqual("one", faction.Units.Single().TroopId);
        AssertOneWarningNaming("duplicate", "a");
    }

    [TestMethod]
    public void Factions_FactionWithNoValidUnits_IsSkipped()
    {
        var catalog = Load(OneFaction("[{ 'id': 'bad', 'troop': 'a', 'role': 'Infantry', 'packSize': -1, 'price': 50 }]"));

        Assert.AreEqual(0, catalog.Factions.Count);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("gondor") && m.Contains("no valid unit")));
    }

    [TestMethod]
    public void Factions_HeroWithBadPrice_IsSkippedAndTheRestKept()
    {
        var catalog = Load(OneFaction("[{ 'id': 'u', 'troop': 'a', 'role': 'Infantry', 'packSize': 5, 'price': 50 }]",
            "[{ 'id': 'lord_x', 'price': 0 }, { 'id': 'lord_y', 'price': 800 }]"));

        Assert.IsTrue(catalog.TryGetFaction("gondor", out var faction));
        CollectionAssert.AreEqual(new[] { "lord_y" }, faction.Heroes.Select(h => h.HeroId).ToList());
        AssertOneWarningNaming("gondor", "lord_x");
    }

    [TestMethod]
    public void Factions_DuplicateHero_KeepsTheFirst()
    {
        var catalog = Load(OneFaction("[{ 'id': 'u', 'troop': 'a', 'role': 'Infantry', 'packSize': 5, 'price': 50 }]",
            "[{ 'id': 'lord_x', 'price': 500 }, { 'id': 'lord_x', 'price': 900 }]"));

        Assert.IsTrue(catalog.TryGetFaction("gondor", out var faction));
        Assert.AreEqual(500, faction.Heroes.Single().Price);
        AssertOneWarningNaming("duplicate", "lord_x");
    }

    [TestMethod]
    public void Factions_FreeWaveTierWithNoValidUnits_IsSkipped()
    {
        var catalog = Load(OneFaction("[{ 'id': 'u', 'troop': 'a', 'role': 'Infantry', 'packSize': 5, 'price': 50 }]", "[]",
            "[{ 'fromMinute': 0, 'units': [{ 'troop': 'a', 'count': 3 }] }, { 'fromMinute': 10, 'units': [{ 'troop': 'a', 'count': 0 }] }]"));

        Assert.IsTrue(catalog.TryGetFaction("gondor", out var faction));
        CollectionAssert.AreEqual(new[] { 0f }, faction.FreeWaves.Select(t => t.FromMinute).ToList());
        AssertOneWarningNaming("gondor", "freeWaves");
    }

    [TestMethod]
    public void Factions_FreeWaveWithNegativeMinute_IsSkipped()
    {
        var catalog = Load(OneFaction("[{ 'id': 'u', 'troop': 'a', 'role': 'Infantry', 'packSize': 5, 'price': 50 }]", "[]",
            "[{ 'fromMinute': -1, 'units': [{ 'troop': 'a', 'count': 3 }] }, { 'fromMinute': 0, 'units': [{ 'troop': 'a', 'count': 3 }] }]"));

        Assert.IsTrue(catalog.TryGetFaction("gondor", out var faction));
        Assert.AreEqual(1, faction.FreeWaves.Count);
        AssertOneWarningNaming("gondor", "fromMinute");
    }

    [TestMethod]
    public void Factions_FactionWithoutCulture_IsSkipped()
    {
        var catalog = Load("{ 'factions': { 'gondor': { 'name': 'G', 'units': [{ 'id': 'a', 'troop': 'a', 'role': 'Infantry', 'packSize': 5, 'price': 50 }] } } }");

        Assert.AreEqual(0, catalog.Factions.Count);
        AssertOneWarningNaming("gondor", "culture");
    }

    [TestMethod]
    public void Factions_FactionNotAnObject_IsSkippedAndTheNextKept()
    {
        var catalog = Load("{ 'factions': { 'broken': 5, 'ok': { 'name': 'Ok', 'culture': 'gondor',"
            + " 'units': [{ 'id': 'a', 'troop': 'a', 'role': 'Infantry', 'packSize': 5, 'price': 50 }] } } }");

        CollectionAssert.AreEqual(new[] { "ok" }, catalog.Factions.Select(f => f.FactionId).ToList());
        AssertOneWarningNaming("broken");
    }

    [TestMethod]
    public void Factions_NaNPrice_IsSkipped()
    {
        var catalog = Load(OneFaction("[{ 'id': 'bad', 'troop': 'a', 'role': 'Infantry', 'packSize': 5, 'price': NaN },"
            + " { 'id': 'good', 'troop': 'b', 'role': 'Infantry', 'packSize': 5, 'price': 50 }]"));

        Assert.IsTrue(catalog.TryGetFaction("gondor", out var faction));
        CollectionAssert.AreEqual(new[] { "good" }, faction.Units.Select(u => u.OfferId).ToList());
    }
}
