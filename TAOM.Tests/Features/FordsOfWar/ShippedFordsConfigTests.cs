using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.FordsOfWar;

/// <summary>
/// The REAL <c>fords_of_war/*.json</c> files the module ships, read the way the game reads them: they load with no warning, every
/// troop and hero id exists in ModuleData, every hero resolves in a Custom Battle (the mode runs in a CustomGame), every price
/// and pack size is positive, and match.json equals <see cref="FordsMatchConfig.Default"/>.
/// </summary>
[TestClass]
public class ShippedFordsConfigTests
{
    private static readonly string[] ExpectedFactions = { "gondor", "rohan", "mordor", "isengard" };

    private static string ModuleData => RepoPaths.RepoPath("Main", "_Module", "ModuleData");

    private static string ConfigFile(string name) => Path.Combine(ModuleData, "fords_of_war", name);

    private static FordsConfigCatalog Load(IModLogger logger)
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(ModuleData);
        return new FordsConfigCatalog(paths, logger);
    }

    private static Dictionary<string, XElement> NpcCharacters()
    {
        var all = new Dictionary<string, XElement>(StringComparer.Ordinal);
        var files = Directory.GetFiles(Path.Combine(ModuleData, "troops"), "*.xml")
            .Concat(Directory.GetFiles(Path.Combine(ModuleData, "characters"), "*.xml"));
        foreach (var file in files)
        {
            foreach (var npc in XDocument.Load(file).Descendants("NPCCharacter"))
            {
                var id = (string?)npc.Attribute("id");
                if (!string.IsNullOrEmpty(id) && !all.ContainsKey(id!))
                    all[id!] = npc;
            }
        }

        return all;
    }

    /// <summary>
    /// Lords that exist in a Custom Battle: characters/lords.xml is registered for CustomGame, and
    /// characters/custom_battle_lords.xml holds the stubs lords.xslt rebuilds for the vanilla-derived lords
    /// (CustomBattleCommandersShippedDataTests does the same split).
    /// </summary>
    private static HashSet<string> CustomBattleLordIds()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in new[] { "lords.xml", "custom_battle_lords.xml" })
        {
            ids.UnionWith(XDocument.Load(Path.Combine(ModuleData, "characters", file)).Descendants("NPCCharacter")
                .Select(c => (string?)c.Attribute("id")).Where(id => !string.IsNullOrEmpty(id)).Select(id => id!));
        }

        return ids;
    }

    private static Dictionary<string, float> TroopWeights() =>
        XDocument.Load(Path.Combine(ModuleData, "TroopWeights", "troop_weights.xml")).Descendants("TroopWeight")
            .ToDictionary(w => (string)w.Attribute("id")!, w => (float)w.Attribute("weight")!);

    private static IEnumerable<string> AllTroopIds(FordsConfigCatalog catalog)
    {
        foreach (var faction in catalog.Factions)
        {
            foreach (var unit in faction.Units)
                yield return unit.TroopId;
            foreach (var tier in faction.FreeWaves)
            {
                foreach (var unit in tier.Units)
                    yield return unit.TroopId;
            }
        }

        foreach (var map in catalog.Maps)
            yield return map.Camp.TroopId;
    }

    [TestMethod]
    public void ShippedFiles_Exist()
    {
        foreach (var name in new[] { "match.json", "factions.json", "maps.json" })
            Assert.IsTrue(File.Exists(ConfigFile(name)), name);
    }

    [TestMethod]
    public void ShippedFiles_LoadWithNoWarningsOrErrors()
    {
        var logger = Substitute.For<IModLogger>();

        var catalog = Load(logger);
        _ = catalog.Match;
        _ = catalog.Factions;
        _ = catalog.Maps;

        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void ShippedMatch_EqualsTheBuiltInDefault()
    {
        FordsMatchConfigLoadTests.AssertSameAsDefault(Load(Substitute.For<IModLogger>()).Match);
    }

    [TestMethod]
    public void ShippedMatch_LaneHoldLineIsOutsideTowerFireRange()
    {
        var shipped = Load(Substitute.For<IModLogger>()).Match;

        foreach (var (name, config) in new[] { ("match.json", shipped), ("the built-in default", FordsMatchConfig.Default) })
        {
            Assert.IsTrue(config.Lanes.HoldDistanceBeforeTower > config.Towers.FireRange,
                $"{name}: a lane gathering at its hold line must stand out of tower fire, so lanes.holdDistanceBeforeTower "
                + $"({config.Lanes.HoldDistanceBeforeTower}) must exceed towers.fireRange ({config.Towers.FireRange})");
        }
    }

    [TestMethod]
    public void TowerVolleyItem_IsACraftedJavelinSandBoxCoreLoadsForACustomGame()
    {
        var core = Path.Combine(GameDir(), "Modules", "SandBoxCore");
        var native = Path.Combine(GameDir(), "Modules", "Native");
        if (!Directory.Exists(core) || !Directory.Exists(native))
            Assert.Inconclusive("SandBoxCore or Native is not installed on this machine; the vanilla item data cannot be checked here.");

        var items = XDocument.Load(Path.Combine(core, "SubModule.xml")).Descendants("XmlNode")
            .Single(n => (string?)n.Element("XmlName")?.Attribute("id") == "Items");
        Assert.AreEqual("items", (string?)items.Element("XmlName")!.Attribute("path"));
        CollectionAssert.Contains(items.Descendants("GameType").Select(g => (string?)g.Attribute("value")).ToList(), "CustomGame",
            "SandBoxCore must load its items folder for a CustomGame, the mode Fords of War runs in");

        var crafted = Directory.GetFiles(Path.Combine(core, "ModuleData", "items"), "*.xml")
            .SelectMany(f => XDocument.Load(f).Descendants("CraftedItem"))
            .Where(i => (string?)i.Attribute("id") == FordsTowerAdapter.VolleyItemId).ToList();
        Assert.AreEqual(1, crafted.Count, $"{FordsTowerAdapter.VolleyItemId} must be one CraftedItem in SandBoxCore's item XML");
        Assert.AreEqual("Javelin", (string?)crafted[0].Attribute("crafting_template"));

        var template = XDocument.Load(Path.Combine(native, "ModuleData", "crafting_templates.xml")).Descendants("CraftingTemplate")
            .Single(t => (string?)t.Attribute("id") == "Javelin");
        Assert.AreEqual("Thrown", (string?)template.Attribute("item_type"), "a thrown item is fired as itself by a custom missile");
    }

    private static string GameDir()
    {
        var env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        return string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env!;
    }

    [TestMethod]
    public void ShippedMatch_NamesEveryNumberOfTheDefault()
    {
        var json = JObject.Parse(File.ReadAllText(ConfigFile("match.json")));

        foreach (var group in new[] { "economy", "waves", "towers", "heroes", "lanes", "capture" })
            Assert.IsNotNull(json[group] as JObject, group);
        Assert.AreEqual(8, ((JObject)json["economy"]!).Count);
        Assert.AreEqual(5, ((JObject)json["waves"]!).Count);
        Assert.AreEqual(13, ((JObject)json["towers"]!).Count);
        Assert.AreEqual(5, ((JObject)json["heroes"]!).Count);
        Assert.AreEqual(5, ((JObject)json["lanes"]!).Count);
        Assert.AreEqual(2, ((JObject)json["capture"]!).Count);
        Assert.IsNotNull(json["countdownSeconds"]);
        foreach (var profile in new[] { "easy", "normal", "hard" })
            Assert.AreEqual(4, ((JObject)json["aiProfiles"]![profile]!).Count, profile);
    }

    [TestMethod]
    public void ShippedFactions_AreTheFourTheSpecNames()
    {
        var catalog = Load(Substitute.For<IModLogger>());

        CollectionAssert.AreEqual(ExpectedFactions, catalog.Factions.Select(f => f.FactionId).ToList());
    }

    [TestMethod]
    public void ShippedFactions_CultureIdsAreRealCultures()
    {
        var cultures = TAOM.Tests.Core.CultureDataFixture.MainCultureIds();

        foreach (var faction in Load(Substitute.For<IModLogger>()).Factions)
            Assert.IsTrue(cultures.Contains(faction.CultureId), $"{faction.FactionId}: culture '{faction.CultureId}' is not a main culture");
    }

    [TestMethod]
    public void ShippedFactions_EveryTroopIdExistsAsANonHeroNpcCharacter()
    {
        var npcs = NpcCharacters();
        var missing = new List<string>();

        foreach (var id in AllTroopIds(Load(Substitute.For<IModLogger>())).Distinct())
        {
            if (!npcs.TryGetValue(id, out var npc))
                missing.Add(id + " (missing)");
            else if ((string?)npc.Attribute("is_hero") == "true")
                missing.Add(id + " (is a hero)");
            else if ((string?)npc.Attribute("is_obsolete") == "true")
                missing.Add(id + " (obsolete)");
        }

        Assert.AreEqual(0, missing.Count, "Troop ids that are not usable NPCCharacters: " + string.Join(", ", missing));
    }

    [TestMethod]
    public void ShippedFactions_EveryHeroIdExistsAsAnNpcCharacter()
    {
        var npcs = NpcCharacters();
        var missing = new List<string>();

        foreach (var faction in Load(Substitute.For<IModLogger>()).Factions)
        {
            foreach (var hero in faction.Heroes)
            {
                if (!npcs.ContainsKey(hero.HeroId))
                    missing.Add($"{faction.FactionId}:{hero.HeroId}");
            }
        }

        Assert.AreEqual(0, missing.Count, "Hero ids with no NPCCharacter: " + string.Join(", ", missing));
    }

    [TestMethod]
    public void ShippedFactions_EveryHeroResolvesInACustomBattle()
    {
        var inCustomBattle = CustomBattleLordIds();
        var missing = new List<string>();

        foreach (var faction in Load(Substitute.For<IModLogger>()).Factions)
        {
            foreach (var hero in faction.Heroes)
            {
                if (!inCustomBattle.Contains(hero.HeroId))
                    missing.Add($"{faction.FactionId}:{hero.HeroId}");
            }
        }

        Assert.AreEqual(0, missing.Count,
            "Heroes in neither characters/lords.xml nor a characters/custom_battle_lords.xml stub: " + string.Join(", ", missing));
    }

    [TestMethod]
    public void ShippedFactions_EveryPriceAndPackSizeIsPositive()
    {
        foreach (var faction in Load(Substitute.For<IModLogger>()).Factions)
        {
            foreach (var unit in faction.Units)
            {
                Assert.IsTrue(unit.PackSize > 0, $"{faction.FactionId}/{unit.OfferId} pack size");
                Assert.IsTrue(unit.PackPrice > 0, $"{faction.FactionId}/{unit.OfferId} price");
            }

            foreach (var hero in faction.Heroes)
                Assert.IsTrue(hero.Price > 0, $"{faction.FactionId}/{hero.HeroId} price");
            foreach (var tier in faction.FreeWaves)
            {
                foreach (var unit in tier.Units)
                    Assert.IsTrue(unit.Count > 0, $"{faction.FactionId} tier {tier.FromMinute} {unit.TroopId}");
            }
        }
    }

    [TestMethod]
    public void ShippedFactions_EveryFactionHasAFreeWaveTierAtMinuteZero()
    {
        foreach (var faction in Load(Substitute.For<IModLogger>()).Factions)
        {
            Assert.IsTrue(faction.FreeWaves.Any(t => t.FromMinute == 0f), faction.FactionId + " has no minute 0 tier");
            CollectionAssert.AreEqual(new[] { 0f, 10f, 20f }, faction.FreeWaves.Select(t => t.FromMinute).ToList(), faction.FactionId);
        }
    }

    [TestMethod]
    public void ShippedFactions_EachHasSixToTenUnitOffersAndTwoOrThreeHeroes()
    {
        foreach (var faction in Load(Substitute.For<IModLogger>()).Factions)
        {
            Assert.IsTrue(faction.Units.Count >= 6 && faction.Units.Count <= 10, $"{faction.FactionId}: {faction.Units.Count} unit offers");
            Assert.IsTrue(faction.Heroes.Count >= 2 && faction.Heroes.Count <= 3, $"{faction.FactionId}: {faction.Heroes.Count} heroes");
        }
    }

    [TestMethod]
    public void ShippedFactions_EachCoversInfantrySpearRangedAndCavalryWhereTheCultureHasIt()
    {
        foreach (var faction in Load(Substitute.For<IModLogger>()).Factions)
        {
            var roles = faction.Units.Select(u => u.Role).ToHashSet();
            Assert.IsTrue(roles.Contains(UnitRole.Infantry), faction.FactionId + " infantry");
            Assert.IsTrue(roles.Contains(UnitRole.Spear), faction.FactionId + " spear");
            Assert.IsTrue(roles.Contains(UnitRole.Ranged), faction.FactionId + " ranged");
            Assert.IsTrue(roles.Contains(UnitRole.Cavalry) || roles.Contains(UnitRole.HorseArcher), faction.FactionId + " mounted");
        }
    }

    [TestMethod]
    public void ShippedFactions_MordorAndIsengardOfferATrollAsTheirSiegeUnit()
    {
        var catalog = Load(Substitute.For<IModLogger>());

        foreach (var id in new[] { "mordor", "isengard" })
        {
            Assert.IsTrue(catalog.TryGetFaction(id, out var faction));
            var siege = faction.Units.Where(u => u.Role == UnitRole.Siege).ToList();
            Assert.AreEqual(1, siege.Count, id);
            Assert.AreEqual("hill_troll", siege[0].TroopId, id);
        }
    }

    [TestMethod]
    public void ShippedFactions_TheNamedHeroesArePinned()
    {
        var catalog = Load(Substitute.For<IModLogger>());

        CollectionAssert.AreEqual(new[] { "lord_1_17", "lord_1_15", "lord_1_48_1" },
            HeroIds(catalog, "mordor"));
        CollectionAssert.AreEqual(new[] { "lord_I1_0", "lord_I4_1", "lord_I1_1" },
            HeroIds(catalog, "isengard"));
    }

    private static List<string> HeroIds(FordsConfigCatalog catalog, string faction)
    {
        Assert.IsTrue(catalog.TryGetFaction(faction, out var f));
        return f.Heroes.Select(h => h.HeroId).ToList();
    }

    // The pack prices the shipped offers had when they stored "price" next to "level" and "eliteWeight"; the reader now computes them.
    private static readonly (string Offer, int Price)[] PricesBeforeTheyWereComputed =
    {
        ("gondor/levy", 110), ("gondor/guardsmen", 145), ("gondor/spears", 110), ("gondor/pikes", 150), ("gondor/bowmen", 145),
        ("gondor/archers", 150), ("gondor/riders", 135), ("gondor/knights", 205), ("gondor/fountain_guard", 240),
        ("rohan/militia", 110), ("rohan/axemen", 105), ("rohan/spears", 130), ("rohan/veteran_spears", 110), ("rohan/bowmen", 105),
        ("rohan/riders", 115), ("rohan/lancers", 150), ("rohan/horse_archers", 100), ("rohan/royal_riders", 270),
        ("mordor/orc_fighters", 125), ("mordor/morannon_warriors", 130), ("mordor/militia_spears", 110), ("mordor/morannon_spears", 145),
        ("mordor/orc_hunters", 90), ("mordor/uruk_archers", 150), ("mordor/warg_raiders", 80), ("mordor/warg_ravagers", 110),
        ("mordor/baraddur_guard", 270), ("mordor/hill_troll", 420),
        ("isengard/fighters", 110), ("isengard/warriors", 130), ("isengard/militia_spears", 110), ("isengard/spearmen", 145),
        ("isengard/skirmishers", 105), ("isengard/archers", 150), ("isengard/warg_scouts", 160), ("isengard/warg_enforcers", 215),
        ("isengard/nazg_hai", 270), ("isengard/hill_troll", 420),
    };

    [TestMethod]
    public void ShippedFactions_OffersCarryNoStoredPrice_TheReaderComputesIt()
    {
        var json = JObject.Parse(File.ReadAllText(ConfigFile("factions.json")));
        var stored = ((JObject)json["factions"]!).Properties()
            .SelectMany(f => f.Value["units"]!.Children<JObject>().Where(u => u["price"] != null).Select(u => f.Name + "/" + (string)u["id"]!))
            .ToList();

        Assert.AreEqual(0, stored.Count, "price is an optional override; the shipped offers state level and eliteWeight instead: " + string.Join(", ", stored));
    }

    [TestMethod]
    public void ShippedFactions_ComputedPrices_EqualThePricesTheOffersHadBefore()
    {
        var catalog = Load(Substitute.For<IModLogger>());

        var actual = catalog.Factions.SelectMany(f => f.Units.Select(u => (Offer: f.FactionId + "/" + u.OfferId, Price: u.PackPrice)))
            .OrderBy(x => x.Offer, StringComparer.Ordinal).ToList();
        var expected = PricesBeforeTheyWereComputed.OrderBy(x => x.Offer, StringComparer.Ordinal).ToList();

        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ShippedFactions_TheTwoDearestOffers_FallWithinTheFirstEightHotkeys()
    {
        // The panel binds F1 to F8 to the first eight offers of the list; the elite and the siege unit are the dearest and must have a key.
        var catalog = Load(Substitute.For<IModLogger>());

        foreach (var faction in catalog.Factions)
        {
            var dearest = faction.Units.OrderByDescending(u => u.PackPrice).Take(2).ToList();
            foreach (var offer in dearest)
                Assert.IsTrue(faction.Units.ToList().IndexOf(offer) < 8, $"{faction.FactionId}/{offer.OfferId} is past the eighth offer and has no hotkey");
        }
    }

    [TestMethod]
    public void ShippedFactions_LevelEliteWeightAndPriceAgreeWithTheDataAndTroopPricing()
    {
        var npcs = NpcCharacters();
        var weights = TroopWeights();
        var json = JObject.Parse(File.ReadAllText(ConfigFile("factions.json")));
        var problems = new List<string>();

        foreach (var faction in ((JObject)json["factions"]!).Properties())
        {
            foreach (var unit in faction.Value["units"]!.Children<JObject>())
            {
                var troop = (string)unit["troop"]!;
                var label = $"{faction.Name}/{(string)unit["id"]!}";
                var role = (UnitRole)Enum.Parse(typeof(UnitRole), (string)unit["role"]!);
                var level = (int)unit["level"]!;
                var weight = (float)unit["eliteWeight"]!;
                var size = (int)unit["packSize"]!;

                if (level != int.Parse((string)npcs[troop].Attribute("level")!))
                    problems.Add(label + ": level differs from the troop file");
                if (weight != (weights.TryGetValue(troop, out var w) ? w : 1f))
                    problems.Add(label + ": eliteWeight differs from troop_weights.xml");
                var mounted = role == UnitRole.Cavalry || role == UnitRole.HorseArcher;
                if (mounted != ((string?)npcs[troop].Attribute("default_group") is "Cavalry" or "HorseArcher"))
                    problems.Add(label + ": role mounted flag differs from default_group");
                // A price is an optional override; only an offer that states one has it checked.
                if (unit["price"] is { } price && (int)price != TroopPricing.PackPrice(level, mounted, weight, size))
                    problems.Add(label + ": price is not TroopPricing.PackPrice for its level, weight and size");
            }
        }

        Assert.AreEqual(0, problems.Count, string.Join("; ", problems));
    }

    [TestMethod]
    public void ShippedMaps_AreOpenFieldBattleScenesFromTheCustomBattleList()
    {
        var scenes = XDocument.Load(Path.Combine(ModuleData, "custom_battle_scenes.xml")).Descendants("Scene")
            .ToDictionary(s => (string)s.Attribute("id")!, StringComparer.OrdinalIgnoreCase);
        var catalog = Load(Substitute.For<IModLogger>());

        Assert.IsTrue(catalog.Maps.Count >= 2 && catalog.Maps.Count <= 3, "two or three scenes");
        foreach (var map in catalog.Maps)
        {
            Assert.IsTrue(scenes.TryGetValue(map.SceneId, out var scene), map.SceneId + " is not a custom_battle_scenes.xml row");
            Assert.AreNotEqual("true", (string?)scene.Attribute("is_siege_map"), map.SceneId + " is a siege scene");
            Assert.AreEqual(MapLayoutMode.Generated, map.Layout, map.SceneId);
            Assert.AreEqual((string)scene.Attribute("name")!, map.DisplayName, map.SceneId + ": the row's own key and default");
        }
    }

    [TestMethod]
    public void ShippedMaps_DefaultsBlockNamesItsLayoutNumbersOptions_AsTheScenesDo()
    {
        // "layout" is the scene's mode ("generated" or "tags"); the numbers are "options" in the defaults and in each scene.
        var defaults = (JObject)JObject.Parse(File.ReadAllText(ConfigFile("maps.json")))["defaults"]!;

        Assert.IsNotNull(defaults["options"], "defaults.options is missing");
        Assert.IsNull(defaults["layout"], "defaults.layout is the old name of defaults.options");
    }

    [TestMethod]
    public void ShippedMaps_DefaultsAreTheSpecNumbers()
    {
        var map = Load(Substitute.For<IModLogger>()).Maps[0];

        Assert.AreEqual(3, map.Options.LaneCount);
        Assert.AreEqual(45f, map.Options.LaneSpread);
        Assert.AreEqual(12f, map.Options.WaypointStep);
        CollectionAssert.AreEqual(new[] { 0.36f, 0.2f }, map.Options.TowerFractions.ToList());
        Assert.AreEqual(0.04f, map.Options.KeepInset);
        Assert.AreEqual("siege_barricade_a", map.Tower.Prefab);
        Assert.AreEqual("siege_barricade_a", map.Keep.Prefab);
    }

    [TestMethod]
    public void ShippedFactionNames_AreFowKeysWithAnEnglishDefault()
    {
        var catalog = Load(Substitute.For<IModLogger>());

        foreach (var faction in catalog.Factions)
        {
            var name = catalog.FactionDisplayName(faction.FactionId);
            StringAssert.StartsWith(name, "{=TAOM_FOW_Faction_", faction.FactionId);
            Assert.IsTrue(name.Length > name.IndexOf('}') + 1, faction.FactionId + " has no English default");
        }
    }

    [TestMethod]
    public void ShippedFactionNames_AreRegisteredInTheEnglishSourceAndAllTwelveLanguages()
    {
        var catalog = Load(Substitute.For<IModLogger>());
        var english = XDocument.Load(Path.Combine(ModuleData, "taom_module_strings.xml")).Descendants("string")
            .ToDictionary(s => (string)s.Attribute("id")!, s => (string)s.Attribute("text")!);
        var languageFiles = Directory.GetDirectories(Path.Combine(ModuleData, "Languages"))
            .Select(d => Directory.GetFiles(d, "std_taom_module_strings_*.xml").Single()).ToList();
        Assert.AreEqual(12, languageFiles.Count);

        foreach (var faction in catalog.Factions)
        {
            var name = catalog.FactionDisplayName(faction.FactionId);
            var key = name.Substring(2, name.IndexOf('}') - 2);
            Assert.IsTrue(english.TryGetValue(key, out var row), key + " is not registered in taom_module_strings.xml");
            Assert.AreEqual(name, row, key + ": the registered English must equal the JSON default");
            foreach (var file in languageFiles)
            {
                var ids = XDocument.Load(file).Descendants("string").Select(s => (string?)s.Attribute("id")).ToHashSet();
                Assert.IsTrue(ids.Contains(key), $"{key} has no row in {Path.GetFileName(file)}");
            }
        }
    }
}
