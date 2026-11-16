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
using TAOM.Features.HeroAuras;

namespace TAOM.Tests.Features.HeroAuras;

/// <summary>
/// Pins hero_auras.json as shipped: the provider is fail-soft, so a bad value or a mistyped hero id would
/// load quietly and an aura would never appear. Every hero id must be an NPCCharacter in the repo data, and every
/// race name one the game registers.
/// </summary>
[TestClass]
public class ShippedHeroAurasConfigTests
{
    private static string RepoRoot => Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

    private static string ModuleDataPath => Path.Combine(RepoRoot, @"Main\_Module\ModuleData");

    private IModLogger _logger = null!;
    private HeroAurasConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(ModuleDataPath);
        _logger = Substitute.For<IModLogger>();
        _sut = new HeroAurasConfigProvider(pathService, _logger);
    }

    [TestMethod]
    public void ShippedConfig_LoadsWithoutWarningOrError()
    {
        var config = _sut.GetConfig();

        Assert.IsTrue(config.Auras.Count > 0, "hero_auras.json ships no aura");
        _logger.DidNotReceive().LogError(Arg.Any<string>());
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void ShippedConfig_EveryHeroIdIsACharacterInTheRepoData()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Named companions keep their rows outside characters/ (named_companions/named_companions.xml).
        foreach (var file in Directory.GetFiles(ModuleDataPath, "*.xml", SearchOption.AllDirectories))
        {
            if (file.Contains(@"\Languages\"))
                continue;
            foreach (Match match in Regex.Matches(File.ReadAllText(file), "<(?:NPCCharacter|Hero)\\b[^>]*?\\bid=\"([^\"]+)\""))
                ids.Add(match.Groups[1].Value);
        }

        var missing = _sut.GetConfig().Auras.SelectMany(a => a.HeroIds).Where(id => !ids.Contains(id)).ToList();

        Assert.AreEqual(0, missing.Count, "hero ids with no NPCCharacter: " + string.Join(", ", missing));
    }

    // A campaign character with no Hero behind it reads its own character id as the aura source (HeroAuraService.SourceId, #1366), so every
    // copy of a troop whose id an aura names would radiate it. is_hero="true" does not make a campaign Hero (CharacterObject.IsHero is
    // _heroObject != null); a <Hero> entry does. So every aura id that is an NPCCharacter this repo loads for Campaign has a <Hero> entry in this
    // repo's ModuleData, or is a mission hero (characters/taom_mission_heroes.xml), whose fallback to his own id is the intent. Only this repo
    // is scanned: an id of a TAOM_Map, Armory or vanilla character is not seen here.
    [TestMethod]
    public void ShippedConfig_EveryHeroIdOfACampaignCharacter_HasAHeroEntryOrIsAMissionHero()
    {
        var campaign = new HashSet<string>(StringComparer.Ordinal);
        var sub = XDocument.Load(Path.Combine(RepoRoot, @"Main\_Module\SubModule.xml"));
        foreach (var node in sub.Descendants("XmlNode").Where(n => (string?)n.Element("XmlName")?.Attribute("id") == "NPCCharacters"))
        {
            // A node with no IncludedGameTypes loads under every game type.
            var types = node.Descendants("GameType").Select(g => (string?)g.Attribute("value")).ToList();
            if (types.Count > 0 && !types.Contains("Campaign"))
                continue;

            var file = Path.Combine(ModuleDataPath, ((string)node.Element("XmlName")!.Attribute("path")!).Replace('/', Path.DirectorySeparatorChar) + ".xml");
            if (!File.Exists(file))
                continue;

            foreach (var npc in XDocument.Load(file).Descendants("NPCCharacter"))
            {
                if (npc.Attribute("id") is { } id)
                    campaign.Add(id.Value);
            }
        }

        var heroEntries = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(ModuleDataPath, "*.xml", SearchOption.AllDirectories))
        {
            var root = XDocument.Load(file).Root;
            if (root?.Name.LocalName == "Heroes")
                heroEntries.UnionWith(root.Elements("Hero").Select(h => (string?)h.Attribute("id")).OfType<string>());
        }

        var missionHeroes = XDocument.Load(Path.Combine(ModuleDataPath, @"characters\taom_mission_heroes.xml")).Descendants("NPCCharacter")
            .Select(n => (string?)n.Attribute("id")).OfType<string>().ToHashSet(StringComparer.Ordinal);

        var checkedIds = _sut.GetConfig().Auras.SelectMany(a => a.HeroIds).Where(campaign.Contains).Distinct().ToList();
        var byHero = checkedIds.Where(heroEntries.Contains).ToList();
        var byMission = checkedIds.Where(id => !heroEntries.Contains(id) && missionHeroes.Contains(id)).ToList();
        var neither = checkedIds.Except(byHero).Except(byMission).ToList();
        var seen = $"campaign ids={checkedIds.Count} with a <Hero> entry: {string.Join(", ", byHero)}; mission heroes: {string.Join(", ", byMission)}";

        Assert.IsTrue(campaign.Count > 100 && heroEntries.Count > 10, $"the scan found {campaign.Count} campaign characters and {heroEntries.Count} <Hero> entries; the files moved?");
        Assert.IsTrue(byHero.Count > 0 && byMission.Count > 0, "the guard read no case of one kind, so it proves nothing for it: " + seen);
        Assert.AreEqual(0, neither.Count, "aura ids of a campaign character with no <Hero> entry that is not a mission hero: " + string.Join(", ", neither) + " (" + seen + ")");
    }

    [TestMethod]
    public void ShippedConfig_RaceNamesAreSpelledAsTheGameSpellsThem()
    {
        // A misspelt race loads with one runtime warning and an aura that reaches nobody. The same list as
        // ShippedDreadAuraConfigTests: the races TAOM's skins register.
        var known = new HashSet<string>(StringComparer.Ordinal)
        {
            "human", "elf", "dwarf", "orc", "goblin", "uruk", "uruk_hai", "pale_uruk", "dg_uruk",
            "berserker", "sauron", "saruman", "nazghul", "cave_troll", "hill_troll",
        };

        var unknown = _sut.GetConfig().Auras.SelectMany(a => a.Races ?? new List<string>()).Where(r => !known.Contains(r)).ToList();

        Assert.AreEqual(0, unknown.Count, "race names TAOM does not ship: " + string.Join(", ", unknown));
    }

    [TestMethod]
    public void AragornAura_NamesTheMissionHero()
    {
        // #1359, #1366: in a Custom Battle (the Long Defeat) and in the Helm's Deep story mission of a campaign the aura finds
        // a hero by his character id, and that Aragorn is taom_hero_aragorn. The hired companion keeps his own row.
        var aura = _sut.GetConfig().Auras.Single(a => a.Id == "aragorn_heir_of_isildur");

        CollectionAssert.Contains(aura.HeroIds, "taom_hero_aragorn");
        CollectionAssert.Contains(aura.HeroIds, "named_companion_aragorn");
    }
}
