using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CheatMenu;

namespace TAOM.Tests.Features.CheatMenu;

/// <summary>
/// The troop picker's rule (CheatTroopList): the culture's own lists and the pooled troops that carry its tag, followed up
/// every upgrade, keeping only regular field soldiers, sorted by tier then name.
/// </summary>
[TestClass]
public class CheatTroopListTests
{
    private static CheatTroopFacts Troop(string id, int tier, string culture = "gondor", string[]? upgrades = null,
        bool hero = false, bool template = false, bool obsolete = false, bool fieldSoldier = true, string? name = null) =>
        new(id, name ?? id, tier, culture, hero, template, obsolete, fieldSoldier, upgrades ?? new string[0]);

    private static Dictionary<string, CheatTroopFacts> World(params CheatTroopFacts[] troops) => troops.ToDictionary(t => t.Id);

    private static List<string> Ids(IReadOnlyList<CheatTroopFacts> list) => list.Select(t => t.Id).ToList();

    [TestMethod]
    public void Tree_FromTheBasicRecruit_ReachesTheTopTier()
    {
        var world = World(
            Troop("recruit", 1, upgrades: new[] { "footman", "archer" }),
            Troop("footman", 2, upgrades: new[] { "knight" }),
            Troop("archer", 2),
            Troop("knight", 6));

        var list = CheatTroopList.Select(new CheatTroopSources("gondor", new[] { "recruit" }, new string[0]), world);

        CollectionAssert.AreEqual(new[] { "recruit", "archer", "footman", "knight" }, Ids(list));
    }

    [TestMethod]
    public void HeroesTemplatesObsoleteAndNonSoldiers_AreLeftOut()
    {
        var world = World(
            Troop("recruit", 1, upgrades: new[] { "lord_upgrade" }),
            Troop("lord_upgrade", 5, hero: true),
            Troop("lord_in_pool", 5, hero: true),
            Troop("template_row", 3, template: true),
            Troop("old_vanilla", 3, obsolete: true),
            Troop("villager", 1, fieldSoldier: false),
            Troop("caravan_guard", 2, fieldSoldier: false));

        var list = CheatTroopList.Select(new CheatTroopSources("gondor",
            new[] { "recruit", "lord_in_pool", "template_row", "old_vanilla", "villager", "caravan_guard" }, new string[0]), world);

        CollectionAssert.AreEqual(new[] { "recruit" }, Ids(list));
    }

    [TestMethod]
    public void PooledTroops_OnlyThoseOfTheCultureStartTheList()
    {
        var world = World(
            Troop("gondor_levy", 1, upgrades: new[] { "gondor_guard" }),
            Troop("gondor_guard", 5),
            Troop("mordor_orc", 1, culture: "mordor", upgrades: new[] { "mordor_uruk" }),
            Troop("mordor_uruk", 4, culture: "mordor"));

        var list = CheatTroopList.Select(new CheatTroopSources("gondor", new string[0], new[] { "gondor_levy", "mordor_orc" }), world);

        CollectionAssert.AreEqual(new[] { "gondor_levy", "gondor_guard" }, Ids(list));
    }

    // Rohan, Dale, Dunland and Rhun reuse vanilla culture ids, so vanilla's own troops carry their tag; no list holds them.
    [TestMethod]
    public void CultureTagAlone_DoesNotPutATroopOnTheList()
    {
        var world = World(
            Troop("rohan_recruit", 1, culture: "vlandia"),
            Troop("vlandian_knight", 6, culture: "vlandia"),
            Troop("gear_dummy_vlandia", 1, culture: "vlandia"));

        var list = CheatTroopList.Select(new CheatTroopSources("vlandia", new[] { "rohan_recruit" }, new string[0]), world);

        CollectionAssert.AreEqual(new[] { "rohan_recruit" }, Ids(list));
    }

    // Blue Craig fields the shared goblin tree, whose troops carry the goblin culture.
    [TestMethod]
    public void OwnRoots_OfAnotherCultureTag_AreKept()
    {
        var world = World(
            Troop("goblin_snaga", 1, culture: "goblin", upgrades: new[] { "goblin_chosen" }),
            Troop("goblin_chosen", 5, culture: "goblin"),
            Troop("bluecraig_capstone", 7, culture: "bluecraig"));

        var list = CheatTroopList.Select(new CheatTroopSources("bluecraig", new[] { "goblin_snaga", "bluecraig_capstone" }, new string[0]), world);

        CollectionAssert.AreEqual(new[] { "goblin_snaga", "goblin_chosen", "bluecraig_capstone" }, Ids(list));
    }

    [TestMethod]
    public void Order_IsTierThenName()
    {
        var world = World(
            Troop("c", 3, name: "Zulu"),
            Troop("a", 3, name: "alpha"),
            Troop("b", 1, name: "Mike"));

        var list = CheatTroopList.Select(new CheatTroopSources("gondor", new[] { "c", "a", "b" }, new string[0]), world);

        CollectionAssert.AreEqual(new[] { "b", "a", "c" }, Ids(list));
    }

    [TestMethod]
    public void UnknownIdsLoopsAndDuplicates_AreHarmless()
    {
        var world = World(
            Troop("a", 1, upgrades: new[] { "b", "missing" }),
            Troop("b", 2, upgrades: new[] { "a" }));

        var list = CheatTroopList.Select(new CheatTroopSources("gondor", new[] { "a", "a", "missing", "" }, new[] { "b" }), world);

        CollectionAssert.AreEqual(new[] { "a", "b" }, Ids(list));
    }

    [TestMethod]
    public void NoCulture_GivesAnEmptyList()
    {
        var world = World(Troop("a", 1));

        Assert.AreEqual(0, CheatTroopList.Select(new CheatTroopSources("", new[] { "a" }, new string[0]), world).Count);
    }
}
