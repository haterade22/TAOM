using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.EncounterLines;
using TAOM.Features.Execution;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.EncounterLines;

/// <summary>
/// Pins the shipped <c>encounter_lines/encounter_groups.json</c>. The provider is fail-soft, so a dropped row would only make
/// a people fall silent to vanilla; these tests make that loud, and hold the table to SPEC-lines.md (13 Nov 2026).
/// </summary>
[TestClass]
public class ShippedEncounterGroupsTests
{
    /// <summary>The speaker groups and their cultures, as the spec lists them.</summary>
    internal static readonly IReadOnlyDictionary<string, string[]> SpecGroups = new Dictionary<string, string[]>
    {
        ["west"] = new[] { "gondor", "arthedain", "cardolan", "rhudaur" },
        ["rohirrim"] = new[] { "vlandia" },
        ["northmen"] = new[] { "sturgia", "anduinvale", "angaladh", "southrhovanion", "dorwinion" },
        ["dunlendings"] = new[] { "empire" },
        ["southrons"] = new[] { "aserai", "abanissa", "shaghana", "umbar" },
        ["easterlings"] = new[] { "khuzait", "battania" },
        ["elves"] = new[] { "rivendell", "mirkwood", "lothlorien", "lindon" },
        ["dwarves"] = new[] { "erebor", "eredluin" },
        ["orcs"] = new[] { "mordor", "isengard", "gundabad", "dolguldur", "goblin", "mistymountainorcs", "bluecraig" },
    };

    private static readonly IReadOnlyDictionary<string, string> SpecGroupFamilies = new Dictionary<string, string>
    {
        ["west"] = "man", ["rohirrim"] = "man", ["northmen"] = "man", ["dunlendings"] = "man", ["southrons"] = "man",
        ["easterlings"] = "man", ["elves"] = "elf", ["dwarves"] = "dwarf", ["orcs"] = "orc",
    };

    /// <summary>The ten speaking bandit clans: side, race family and home group, as the spec and the lead's 13 Nov change list them.</summary>
    internal static readonly IReadOnlyDictionary<string, BanditClan> SpecBandits = new Dictionary<string, BanditClan>
    {
        ["dunland_raiders"] = new(FactionSide.Evil, "man", "dunlendings"),
        ["rhun_raiders"] = new(FactionSide.Evil, "man", "easterlings"),
        ["harad_raiders"] = new(FactionSide.Evil, "man", "southrons"),
        ["gundabad_raiders"] = new(FactionSide.Evil, "orc", null),
        ["feral_orcs"] = new(FactionSide.Evil, "orc", null),
        ["gondor_soldiers"] = new(FactionSide.Free, "man", "west"),
        ["umbar_corsairs"] = new(FactionSide.Neutral, "man", "southrons"),
        ["erebor_warriors"] = new(FactionSide.Neutral, "dwarf", null),
        ["mirkwood_stalkers"] = new(FactionSide.Neutral, "elf", null),
        ["looters"] = new(FactionSide.Neutral, "man", null),
    };

    private static readonly string[] RenamedVanillaCultures = { "empire", "vlandia", "sturgia", "aserai", "khuzait", "battania" };

    private static string ModuleDataPath => RepoPath("Main", "_Module", "ModuleData");

    private IModLogger _logger = null!;
    private EncounterGroupMap _map = null!;

    [TestInitialize]
    public void Setup()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(ModuleDataPath);
        _logger = Substitute.For<IModLogger>();
        _map = new EncounterGroupsConfigProvider(pathService, _logger).GetMap();
    }

    private static string[] MainCultureIds() =>
        Regex.Matches(File.ReadAllText(Path.Combine(ModuleDataPath, "taom_spcultures.xml")), @"<Culture\b[^>]*>")
            .Cast<Match>()
            .Where(m => m.Value.Contains("is_main_culture=\"true\""))
            .Select(m => Regex.Match(m.Value, @"\bid=""([^""]+)""").Groups[1].Value)
            .ToArray();

    [TestMethod]
    public void ShippedConfig_ParsesWithoutWarningOrError()
    {
        Assert.IsTrue(File.Exists(Path.Combine(ModuleDataPath, "encounter_lines", "encounter_groups.json")));
        _logger.DidNotReceive().LogError(Arg.Any<string>());
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void EveryMainCulture_AndTheSixRenamedVanillaCultures_HaveExactlyOneGroup()
    {
        var main = MainCultureIds();
        Assert.AreEqual(24, main.Length, "taom_spcultures.xml main-culture count changed: give the new culture a group");

        var missing = main.Concat(RenamedVanillaCultures).Where(c => _map.GroupOfCulture(c) == null).ToList();

        Assert.AreEqual(0, missing.Count, "Cultures with no speaker group (their lords would keep vanilla's lines): " + string.Join(", ", missing));
        Assert.AreEqual(30, _map.CultureGroups.Count, "a culture key that is not one of the 30 has crept in");
    }

    [TestMethod]
    public void TheGroupTable_EqualsTheSpec()
    {
        CollectionAssert.AreEquivalent(SpecGroups.Keys.ToList(), _map.GroupFamilies.Keys.ToList());
        foreach (var pair in SpecGroups)
        {
            Assert.AreEqual(SpecGroupFamilies[pair.Key], _map.FamilyOfGroup(pair.Key), pair.Key + " family");
            CollectionAssert.AreEquivalent(pair.Value, _map.CultureGroups.Where(c => c.Value == pair.Key).Select(c => c.Key).ToList(), pair.Key + " cultures");
        }
    }

    [TestMethod]
    public void EverySkinsRace_AndHuman_HaveAFamily()
    {
        var skins = File.ReadAllText(RepoPath("docs", "reference", "lotrlome-armory-snapshot", "skins.xml"));
        var races = Regex.Matches(skins, @"<race\s+id\s*=\s*""([^""]+)""")
            .Cast<Match>().Select(m => m.Groups[1].Value).Append("human").ToHashSet();
        Assert.AreEqual(15, races.Count, "skins.xml race count changed: give the new race a family");

        var missing = races.Where(r => _map.FamilyOfRace(r) == null).ToList();
        var unknown = _map.RaceFamilies.Keys.Where(r => !races.Contains(r)).ToList();

        Assert.AreEqual(0, missing.Count, "Races with no family: " + string.Join(", ", missing));
        Assert.AreEqual(0, unknown.Count, "Race keys that name no race: " + string.Join(", ", unknown));
    }

    [TestMethod]
    public void TheNonSpeakingRaces_HaveFamilyNone()
    {
        foreach (var race in new[] { "nazghul", "cave_troll", "hill_troll", "saruman", "sauron" })
            Assert.AreEqual("none", _map.FamilyOfRace(race), race);
        foreach (var race in new[] { "orc", "uruk", "uruk_hai", "pale_uruk", "dg_uruk", "goblin", "berserker" })
            Assert.AreEqual("orc", _map.FamilyOfRace(race), race);
        Assert.AreEqual("man", _map.FamilyOfRace("human"));
        Assert.AreEqual("elf", _map.FamilyOfRace("elf"));
        Assert.AreEqual("dwarf", _map.FamilyOfRace("dwarf"));
    }

    [TestMethod]
    public void TheBanditTable_EqualsTheSpec()
    {
        CollectionAssert.AreEquivalent(SpecBandits.Keys.ToList(), _map.Bandits.Keys.ToList());
        foreach (var pair in SpecBandits)
            Assert.AreEqual(pair.Value, _map.Bandit(pair.Key), pair.Key);
    }

    [TestMethod]
    public void EveryClanKey_IsABanditFaction_InTheShippedClanData_OrVanillasLooters()
    {
        var doc = XDocument.Load(Path.Combine(ModuleDataPath, "characters", "clans.xml"));
        var bandits = doc.Descendants("Faction")
            .Where(f => (string?)f.Attribute("is_bandit") == "true")
            .Select(f => (string)f.Attribute("id")!)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var clan in _map.Bandits.Keys.Where(k => k != "looters"))
            Assert.IsTrue(bandits.Contains(clan), clan + " is not a bandit Faction in clans.xml");
        Assert.IsFalse(_map.Bandits.ContainsKey("mirkwood_spiders") || _map.Bandits.ContainsKey("wild_trolls"), "the creature clans do not speak");
    }
}
