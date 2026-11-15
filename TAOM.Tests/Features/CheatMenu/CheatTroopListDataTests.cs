using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.Arachnophobia;
using TAOM.Features.CheatMenu;
using TAOM.Features.TroopProgression;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.CheatMenu;

/// <summary>
/// The troop picker on the repo's own data: Gondor's list built the way CheatWorldAdapter.ReadFactionTroops builds it (the
/// culture row of taom_spcultures.xml, its vassal reward template, the TAOM recruitment pools with the Gondor JSON, the
/// Elite Emissary offers), from the characters of troops/ and characters/. Clan party templates are left out here, so the
/// list can only be larger in game.
/// </summary>
[TestClass]
public class CheatTroopListDataTests
{
    private const string Gondor = "gondor";

    private static readonly Lazy<(Dictionary<string, CheatTroopFacts> Troops, Dictionary<string, int> Levels)> Characters = new(ReadCharacters);

    private static string ModuleData(params string[] parts) =>
        RepoPaths.RepoPath(new[] { "Main", "_Module", "ModuleData" }.Concat(parts).ToArray());

    private static IReadOnlyList<CheatTroopFacts> GondorList()
    {
        var culture = XDocument.Load(ModuleData("taom_spcultures.xml")).Descendants("Culture")
            .Single(c => (string?)c.Attribute("id") == Gondor);
        var own = new List<string>();
        foreach (var attr in new[] { "basic_troop", "elite_basic_troop", "melee_militia_troop", "ranged_militia_troop",
                     "melee_elite_militia_troop", "ranged_elite_militia_troop" })
            if (culture.Attribute(attr) is { } a) own.Add(Strip(a.Value));
        var vassal = Strip((string?)culture.Attribute("vassal_reward_party_template") ?? "");
        own.AddRange(XDocument.Load(ModuleData("taom_partyTemplates.xml")).Descendants("MBPartyTemplate")
            .Where(t => (string?)t.Attribute("id") == vassal)
            .SelectMany(t => t.Descendants("PartyTemplateStack"))
            .Select(s => Strip((string?)s.Attribute("troop") ?? "")));
        own.AddRange(culture.Descendants("basic_mercenary_troops").Descendants("template").Select(t => Strip((string?)t.Attribute("name") ?? "")));
        var recruitment = new VolunteerRecruitmentService(Substitute.For<IRandomProvider>(),
            Substitute.For<IModLogger>(), Substitute.For<IArachnophobiaService>());
        own.AddRange(recruitment.GetCulturePoolTroopIds(Gondor));
        own.AddRange(XDocument.Load(ModuleData("elite_emissary", "elite_emissary_config.xml")).Descendants("Culture")
            .Where(c => (string?)c.Attribute("id") == Gondor)
            .SelectMany(c => c.Elements("Troop")).Select(t => (string)t.Attribute("id")!));

        var fielded = new HashSet<string>(VolunteerRecruitmentService.AllPooledTroopIds());
        GondorRecruitmentJsonLoader.LoadFromPath(
            path: ModuleData("recruitment_pools", "gondor.json"),
            addSettlement: (_, entries) => { foreach (var e in entries) fielded.Add(e.Item1); },
            addSettlementConditional: (_, __, entries) => { foreach (var e in entries) fielded.Add(e.Item1); },
            logger: Substitute.For<IModLogger>());

        return CheatTroopList.Select(new CheatTroopSources(Gondor, own, fielded), Characters.Value.Troops);
    }

    [TestMethod]
    public void Gondor_ListsItsTopTierTroops()
    {
        var list = GondorList().Select(t => t.Id).ToHashSet();
        var levels = Characters.Value.Levels;
        var gondorSoldiers = XDocument.Load(ModuleData("troops", "troops_gondor.xml")).Descendants("NPCCharacter")
            .Where(c => (string?)c.Attribute("culture") == "Culture.gondor" && (string?)c.Attribute("occupation") == "Soldier")
            .Select(c => (string)c.Attribute("id")!).ToList();
        var top = gondorSoldiers.Max(id => levels[id]);
        var topTroops = gondorSoldiers.Where(id => levels[id] == top).ToList();

        Assert.IsTrue(topTroops.Count > 0, "troops_gondor.xml has no soldier");
        CollectionAssert.IsSubsetOf(topTroops, list.ToList(), "Gondor's highest-level soldiers are missing");
        // The Elite Emissary's Gondor elites, a literal list (the in-game tier 6 picks).
        CollectionAssert.IsSubsetOf(new[] { "gondor_ithilien_ranger", "gondor_mt_fountain_guard", "gondor_da_swan_knight" }, list.ToList());
        // The regional lines come through the pools, not through the basic recruit's tree.
        Assert.IsTrue(list.Count >= 100, $"Gondor's list has only {list.Count} troops");
    }

    [TestMethod]
    public void Gondor_LeavesOutHeroesGuardsAndDummies()
    {
        var list = GondorList();
        var troops = Characters.Value.Troops;

        Assert.AreEqual(0, list.Count(t => troops[t.Id].IsHero), "a hero is on the list");
        Assert.IsTrue(troops.Values.Any(t => t.IsHero && t.CultureId == Gondor), "no Gondor hero was read: the check is empty");
        foreach (var id in new[] { "guard_gondor", "gear_dummy_gondor", "gear_practice_dummy_gondor", "weapon_practice_stage_1_gondor" })
        {
            Assert.IsTrue(troops.ContainsKey(id), id + " was not read: the check is empty");
            Assert.IsFalse(list.Any(t => t.Id == id), id + " is on the list");
        }
    }

    private static string Strip(string value)
    {
        var dot = value.IndexOf('.');
        return dot >= 0 ? value.Substring(dot + 1) : value;
    }

    private static (Dictionary<string, CheatTroopFacts>, Dictionary<string, int>) ReadCharacters()
    {
        var troops = new Dictionary<string, CheatTroopFacts>(StringComparer.Ordinal);
        var levels = new Dictionary<string, int>(StringComparer.Ordinal);
        var files = Directory.GetFiles(ModuleData("troops"), "*.xml")
            .Concat(Directory.GetFiles(ModuleData("characters"), "*.xml"));
        foreach (var file in files)
        foreach (var c in XDocument.Load(file).Descendants("NPCCharacter"))
        {
            var id = (string?)c.Attribute("id");
            if (string.IsNullOrEmpty(id) || troops.ContainsKey(id!)) continue;
            var occupation = (string?)c.Attribute("occupation");
            var level = int.TryParse((string?)c.Attribute("level"), out var l) ? l : 0;
            levels[id!] = level;
            troops[id!] = new CheatTroopFacts(id!, id!, level, Strip((string?)c.Attribute("culture") ?? ""),
                (string?)c.Attribute("is_hero") == "true",
                (string?)c.Attribute("is_template") == "true",
                (string?)c.Attribute("is_obsolete") == "true",
                occupation == "Soldier" || occupation == "Mercenary",
                c.Descendants("upgrade_target").Select(u => Strip((string?)u.Attribute("id") ?? "")).ToList());
        }
        return (troops, levels);
    }
}
