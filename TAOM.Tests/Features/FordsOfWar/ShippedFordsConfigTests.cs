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
    private static readonly string[] ExpectedFactions = { "gondor", "rohan", "mordor", "isengard", "rivendell", "erebor", "harad", "gundabad", "dale", "dolguldur", "easterlings", "umbar", "mirkwood" };

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
            .Concat(Directory.GetFiles(Path.Combine(ModuleData, "characters"), "*.xml"))
            .Concat(new[] { ConfigFile("fords_troops.xml") });
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

    private static IEnumerable<string> AllTroopIds(FordsConfigCatalog catalog)
    {
        foreach (var faction in catalog.Factions)
        {
            foreach (var unit in faction.AllOffers)
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
    public void TowerRock_IsTheShippedItemTheAdapterNames_AHurledBoulderWithARing()
    {
        var item = System.Xml.Linq.XDocument.Load(ConfigFile("fords_items.xml")).Descendants("Item")
            .Single(i => (string)i.Attribute("id") == TAOM.Adapters.FordsTowerAdapter.TowerRockItemId);
        var weapon = item.Descendants("Weapon").Single();

        Assert.AreEqual("Thrown", (string)item.Attribute("Type"));
        Assert.AreEqual("Boulder", (string)weapon.Attribute("weapon_class"));
        Assert.AreEqual("true", (string)weapon.Element("WeaponFlags")!.Attribute("AffectsArea"));
        Assert.IsTrue((int)weapon.Attribute("thrust_damage") > 0);
        Assert.IsTrue((int)weapon.Attribute("missile_speed") >= 20, "the adapter launches at the item's own speed");
    }

    [TestMethod]
    public void TowerArrow_IsAShippedItemThatDealsDamage_AndNeverSells()
    {
        var item = System.Xml.Linq.XDocument.Load(ConfigFile("fords_items.xml")).Descendants("Item").Single(i => (string)i.Attribute("id") == "fow_tower_arrow");

        Assert.AreEqual("Arrows", (string)item.Attribute("Type"));
        Assert.AreEqual("false", (string)item.Attribute("is_merchandise"));
        Assert.IsTrue((int)item.Descendants("Weapon").Single().Attribute("thrust_damage") > 0);
        Assert.IsTrue((int)item.Descendants("Weapon").Single().Attribute("missile_speed") >= 28);
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
    public void ShippedDefaults_AreGondorAgainstRohan_OnNormal_OnTheRohanPlainsIIMap()
    {
        var catalog = Load(Substitute.For<IModLogger>());

        Assert.IsTrue(FordsSetupChoices.TryResolve(catalog, null, null, null, null, out var setup, out var error), error);

        Assert.AreEqual("gondor", setup.WestFaction);
        Assert.AreEqual("rohan", setup.EastFaction);
        Assert.AreEqual("normal", setup.AiProfile);
        Assert.AreEqual("taom_rohan_battle_002_forceatmo", setup.SceneName, "the first scene listed in maps.json");
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
        Assert.AreEqual(17, ((JObject)json["towers"]!).Count);
        Assert.AreEqual(0.2, (double)json["towers"]!["thrustFloorFactor"]!, 1e-9);
        Assert.AreEqual(8, ((JObject)json["heroes"]!).Count);
        Assert.AreEqual(6, ((JObject)json["lanes"]!).Count);
        Assert.AreEqual(2, ((JObject)json["capture"]!).Count);
        Assert.IsNotNull(json["countdownSeconds"]);
        foreach (var profile in new[] { "easy", "normal", "hard" })
            Assert.AreEqual(4, ((JObject)json["aiProfiles"]![profile]!).Count, profile);
    }

    [TestMethod]
    public void ShippedRohan_AloneDiscountsItsRiders_SoItsFinestCavalryCanBeMassed()
    {
        var factions = (JObject)JObject.Parse(File.ReadAllText(ConfigFile("factions.json")))["factions"]!;

        static IEnumerable<float> RiderWeights(JToken faction) =>
            faction["units"]!.Where(u => (string?)u["role"] == "Cavalry").Select(u => (float)u["eliteWeight"]!);

        Assert.IsTrue(RiderWeights(factions["rohan"]!).All(w => w < 1f), "every Rohan rider pack is below the full price for its level");
        var cheapestElsewhere = factions.Properties().Where(p => p.Name != "rohan").SelectMany(p => RiderWeights(p.Value)).Min();
        Assert.IsTrue(RiderWeights(factions["rohan"]!).Max() < cheapestElsewhere, "no other faction's riders are as cheap for their level");
    }

    [TestMethod]
    public void ShippedFactions_AreTheThirteenDesigned()
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
        }
    }

    [TestMethod]
    public void ShippedFactions_EachShowsFiveToEightOffers_ForTheEightBuyKeys_AndTwoOrThreeHeroes()
    {
        foreach (var faction in Load(Substitute.For<IModLogger>()).Factions)
        {
            var shown = faction.Units.Count + (faction.Rotation?.Regions.Max(r => r.Units.Count) ?? 0);
            Assert.IsTrue(shown >= 5 && shown <= 8, $"{faction.FactionId}: {shown} unit offers");
            Assert.IsTrue(faction.Heroes.Count >= 2 && faction.Heroes.Count <= 3, $"{faction.FactionId}: {faction.Heroes.Count} heroes");
        }
    }

    [TestMethod]
    public void ShippedFactions_EachCanFieldInfantryRangedAndMounted()
    {
        foreach (var faction in Load(Substitute.For<IModLogger>()).Factions)
        {
            var roles = faction.AllOffers.Select(u => u.Role).ToHashSet();
            Assert.IsTrue(roles.Contains(UnitRole.Infantry) || roles.Contains(UnitRole.Spear), faction.FactionId + " infantry");
            Assert.IsTrue(roles.Contains(UnitRole.Ranged), faction.FactionId + " ranged");
            Assert.IsTrue(roles.Contains(UnitRole.Cavalry) || roles.Contains(UnitRole.HorseArcher), faction.FactionId + " mounted");
        }
    }

    [TestMethod]
    public void ShippedFactions_MordorCallsItsTrolls_AndIsengardBuysOne()
    {
        var catalog = Load(Substitute.For<IModLogger>());

        Assert.IsTrue(catalog.TryGetFaction("mordor", out var mordor));
        Assert.IsFalse(mordor.Units.Any(u => u.Role == UnitRole.Siege));
        CollectionAssert.IsSubsetOf(new[] { "fow_hill_troll", "fow_cave_troll" }, mordor.Creatures!.Offers.Select(o => o.TroopId).ToList());

        Assert.IsTrue(catalog.TryGetFaction("isengard", out var isengard));
        Assert.AreEqual("fow_hill_troll", isengard.Units.Single(u => u.Role == UnitRole.Siege).TroopId);
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
    public void ShippedFactions_LevelRoleAndPriceAgreeWithTheRosterAndTroopPricing()
    {
        var npcs = NpcCharacters();
        var json = JObject.Parse(File.ReadAllText(ConfigFile("factions.json")));
        var problems = new List<string>();

        foreach (var faction in ((JObject)json["factions"]!).Properties())
        {
            foreach (var unit in ((JContainer)faction.Value).Descendants().OfType<JObject>().Where(o => o["troop"] != null && o["level"] != null))
            {
                var troop = (string)unit["troop"]!;
                var label = $"{faction.Name}/{(string)unit["id"]!}";
                var role = (UnitRole)Enum.Parse(typeof(UnitRole), (string)unit["role"]!);
                var level = (int)unit["level"]!;
                var weight = (float)unit["eliteWeight"]!;
                var size = (int)unit["packSize"]!;

                if (level != int.Parse((string)npcs[troop].Attribute("level")!))
                    problems.Add(label + ": level differs from the troop file");
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

        Assert.IsTrue(catalog.Maps.Count >= 2 && catalog.Maps.Count <= 5, "two to five scenes");
        foreach (var map in catalog.Maps.Where(m => m.Layout == MapLayoutMode.Generated))
        {
            Assert.IsTrue(scenes.TryGetValue(map.SceneId, out var scene), map.SceneId + " is not a custom_battle_scenes.xml row");
            Assert.AreNotEqual("true", (string?)scene.Attribute("is_siege_map"), map.SceneId + " is a siege scene");
            Assert.AreEqual((string)scene.Attribute("name")!, map.DisplayName, map.SceneId + ": the row's own key and default");
        }
    }

    [TestMethod]
    public void ShippedMaps_ListRohanPlainsIIFirst_ThenPelennorMordorRohanPlainsAndFordsLast()
    {
        // The first scene is the default of a console start with no scene argument (FordsSetupChoices) and of a one-scene menu.
        var catalog = Load(Substitute.For<IModLogger>());

        CollectionAssert.AreEqual(new[]
        {
            "taom_rohan_battle_002_forceatmo", "taom_gondor_pelennor_fields_forceatmo", "taom_mordor_battle_001_forceatmo", "taom_rohan_battle_001_forceatmo",
            "taom_fords_01_forceatmo",
        }, catalog.Maps.Select(m => m.SceneId).ToList());
        Assert.AreEqual("{=aom_taom_rohan_battle_002_forceatmo_name}[Rohan] Rohan Plains II", catalog.Maps[0].DisplayName);
        Assert.AreEqual(MapLayoutMode.Generated, catalog.Maps[0].Layout);
    }

    [TestMethod]
    public void ShippedMaps_Fords01_IsATagsMapAndNotTheDefault()
    {
        var catalog = Load(Substitute.For<IModLogger>());

        var fords = catalog.Maps.Single(m => m.SceneId == "taom_fords_01_forceatmo");
        Assert.AreEqual(MapLayoutMode.Tags, fords.Layout);
        Assert.AreEqual("{=TAOM_FOW_Map_Fords01}[Fords] Fords of War", fords.DisplayName);
        Assert.AreSame(fords, catalog.Maps[catalog.Maps.Count - 1], "last, so Rohan Plains II stays the default");
        Assert.AreNotEqual("taom_fords_01_forceatmo", catalog.Maps[0].SceneId);
    }

    [TestMethod]
    public void EveryShippedMapName_HasARegisteredKey()
    {
        var catalog = Load(Substitute.For<IModLogger>());
        var english = XDocument.Load(Path.Combine(ModuleData, "taom_module_strings.xml")).Descendants("string")
            .ToDictionary(s => (string)s.Attribute("id")!, s => (string)s.Attribute("text")!);
        var vanilla = XDocument.Load(Path.Combine(ModuleData, "custom_battle_scenes.xml")).Descendants("Scene")
            .Select(x => (string)x.Attribute("id")!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var languageFiles = Directory.GetDirectories(Path.Combine(ModuleData, "Languages"))
            .Select(d => Directory.GetFiles(d, "std_taom_module_strings_*.xml").Single()).ToList();

        // A Custom Battle scene keeps the name its custom_battle_scenes.xml row gives it; a scene outside that list names itself with a TAOM_FOW_Map key.
        foreach (var map in catalog.Maps.Where(m => !vanilla.Contains(m.SceneId)))
        {
            Assert.IsTrue(map.DisplayName.StartsWith("{=TAOM_FOW_Map_"), map.SceneId + ": " + map.DisplayName);
            var key = map.DisplayName.Substring(2, map.DisplayName.IndexOf('}') - 2);
            Assert.IsTrue(english.TryGetValue(key, out var row), key + " is not registered in taom_module_strings.xml");
            Assert.AreEqual(map.DisplayName, row, key + ": the registered English must equal the JSON default");
            foreach (var file in languageFiles)
            {
                var ids = XDocument.Load(file).Descendants("string").Select(x => (string?)x.Attribute("id")).ToHashSet();
                Assert.IsTrue(ids.Contains(key), key + " has no row in " + Path.GetFileName(file));
            }
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

    [TestMethod]
    public void ShippedMaps_Fords01_DeclaresItsTowerHalfWidth()
    {
        var catalog = Load(Substitute.For<IModLogger>());

        Assert.AreEqual(5.5f, catalog.Maps.Single(m => m.SceneId == "taom_fords_01_forceatmo").TowerHalfWidth);
    }

    [TestMethod]
    public void ShippedMaps_GeneratedScenes_HaveNoRealTowerKeys()
    {
        var catalog = Load(Substitute.For<IModLogger>());

        foreach (var map in catalog.Maps.Where(m => m.Layout == MapLayoutMode.Generated))
            Assert.AreEqual(0f, map.TowerHalfWidth, map.SceneId);
    }
}
