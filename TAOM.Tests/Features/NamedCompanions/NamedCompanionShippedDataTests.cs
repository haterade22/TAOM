using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using TAOM.Features.NamedCompanions.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.NamedCompanions;

/// <summary>
/// The shipped <c>named_companion_config.json</c> against the data it points at. A text id with a dot never
/// resolves: the engine splits a GameText id at the first dot (GameTextManager.cs:190-197), so the lookup finds
/// neither the id nor its variation.
/// </summary>
[TestClass]
public class NamedCompanionShippedDataTests
{
    private static readonly Regex StringId = new("<string\\s+id=\"([^\"]+)\"", RegexOptions.Compiled);
    private static readonly Regex SettlementId = new("<Settlement\\s+id=\"([^\"]+)\"", RegexOptions.Compiled);

    private static List<NamedCompanionDefinition> Companions() =>
        JsonConvert.DeserializeObject<List<NamedCompanionDefinition>>(File.ReadAllText(
            RepoPaths.RepoPath("Main", "_Module", "ModuleData", "named_companions", "named_companion_config.json")))!;

    private static HashSet<string> ShippedStringIds()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in new[]
                 {
                     RepoPaths.RepoPath("Main", "_Module", "ModuleData", "named_companions", "named_companion_strings.xml"),
                     RepoPaths.RepoPath("Main", "_Module", "ModuleData", "taom_module_strings.xml"),
                 })
            foreach (Match match in StringId.Matches(File.ReadAllText(file)))
                ids.Add(match.Groups[1].Value);
        return ids;
    }

    private static IEnumerable<(string who, string field, string id)> TextIds(IEnumerable<NamedCompanionDefinition> companions)
    {
        foreach (var companion in companions)
        {
            if (!string.IsNullOrEmpty(companion.HireTerms?.LockedStringId))
                yield return (companion.CharacterId, "locked_string_id", companion.HireTerms!.LockedStringId);
            foreach (var topic in companion.Topics ?? new List<TopicDefinition>())
            {
                yield return (companion.CharacterId, topic.Id + ".prompt_string_id", topic.PromptStringId);
                yield return (companion.CharacterId, topic.Id + ".reply_string_id", topic.ReplyStringId);
            }
        }
    }

    [TestMethod]
    public void EveryTextId_IsAShippedStringRow_WithoutADot()
    {
        var shipped = ShippedStringIds();
        var problems = new List<string>();

        foreach (var (who, field, id) in TextIds(Companions()))
        {
            if (string.IsNullOrEmpty(id)) problems.Add($"{who} {field} is empty");
            else if (id.Contains('.')) problems.Add($"{who} {field} '{id}' contains a dot");
            else if (!shipped.Contains(id)) problems.Add($"{who} {field} '{id}' has no <string id> row");
        }

        Assert.AreEqual(0, problems.Count, string.Join("; ", problems));
    }

    [TestMethod]
    public void EveryHireItemId_ExistsInTheShippedItemData()
    {
        var itemIds = Companions()
            .SelectMany(c => (c.HireTerms?.Items ?? new List<ItemCostDefinition>())
                .Concat(c.Topics?.SelectMany(t => (t.GiveItems ?? new List<ItemCostDefinition>())
                    .Concat(t.TakeItems ?? new List<ItemCostDefinition>())) ?? Enumerable.Empty<ItemCostDefinition>()))
            .Select(i => i.ItemId).ToList();
        if (itemIds.Count == 0) return; // no shipped item cost today; the check starts to matter when one is added

        var known = new HashSet<string>(StringComparer.Ordinal);
        var itemIdPattern = new Regex("<Item\\s+id=\"([^\"]+)\"");
        foreach (var file in Directory.EnumerateFiles(RepoPaths.RepoPath("Main", "_Module", "ModuleData"), "*.xml", SearchOption.AllDirectories))
            foreach (Match match in itemIdPattern.Matches(File.ReadAllText(file)))
                known.Add(match.Groups[1].Value);

        var missing = itemIds.Where(id => !known.Contains(id)).ToList();
        Assert.AreEqual(0, missing.Count, "hire item ids not found in the repo's ModuleData: " + string.Join(", ", missing)
            + " (an Armory or vanilla id is outside this repo: check it against the live install by hand)");
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void EverySpawnSettlement_ExistsInTheLiveMapModule()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        string game = string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env;
        var path = Path.Combine(game, "Modules", "TAOM_Map", "ModuleData", "settlements.xml");
        if (!File.Exists(path))
            Assert.Inconclusive("The live TAOM_Map settlements.xml is missing: " + path);

        var settlements = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in SettlementId.Matches(File.ReadAllText(path)))
            settlements.Add(match.Groups[1].Value);

        var missing = Companions().Where(c => !settlements.Contains(c.SpawnSettlement)).Select(c => $"{c.CharacterId}: {c.SpawnSettlement}").ToList();
        Assert.AreEqual(0, missing.Count, string.Join("; ", missing));
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void EveryHomeSettlement_ExistsInTheLiveMapModule()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        string game = string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env;
        var path = Path.Combine(game, "Modules", "TAOM_Map", "ModuleData", "settlements.xml");
        if (!File.Exists(path))
            Assert.Inconclusive("The live TAOM_Map settlements.xml is missing: " + path);

        var settlements = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in SettlementId.Matches(File.ReadAllText(path)))
            settlements.Add(match.Groups[1].Value);

        var missing = Companions()
            .Where(c => !string.IsNullOrEmpty(c.HomeSettlement) && !settlements.Contains(c.HomeSettlement))
            .Select(c => $"{c.CharacterId}: {c.HomeSettlement}").ToList();
        Assert.AreEqual(0, missing.Count, string.Join("; ", missing));
    }

    private static readonly string[] BackstoryParts =
        { "prebackstory", "backstory_a", "backstory_b", "backstory_c", "backstory_d", "response_1", "response_2" };

    [TestMethod]
    public void EveryConfiguredCompanion_HasItsCharacter_ANeutralHeroRow_AndAllSevenBackstoryStrings()
    {
        // The four parts "How to Add a New Named Companion" lists, kept in agreement. A missing
        // Hero row makes the service skip the companion with a warning; a Hero row without
        // faction= throws in Hero.Deserialize. The backstory strings are shown by the intro dialog
        // (OathIntroDialog, #1061), which skips a companion missing any of the seven.
        var md = RepoPaths.RepoPath("Main", "_Module", "ModuleData");
        var characters = File.ReadAllText(Path.Combine(md, "named_companions", "named_companions.xml"));
        var heroes = File.ReadAllText(Path.Combine(md, "characters", "heroes.xml"));
        var strings = ShippedStringIds();

        var problems = new List<string>();
        foreach (var c in Companions())
        {
            if (!characters.Contains($"<NPCCharacter id=\"{c.CharacterId}\""))
                problems.Add($"{c.CharacterId}: no NPCCharacter");
            if (!heroes.Contains($"<Hero id=\"{c.CharacterId}\" faction=\"Faction.neutral\""))
                problems.Add($"{c.CharacterId}: no neutral Hero row");
            problems.AddRange(BackstoryParts.Where(p => !strings.Contains($"{p}.{c.CharacterId}"))
                .Select(p => $"{c.CharacterId}: no {p} string"));
        }

        Assert.AreEqual(0, problems.Count, string.Join("; ", problems));
    }

    [TestMethod]
    public void Elegost_IsAnArthedainRangerWaitingInBree()
    {
        // #1060: player feedback asked for a Dunedain wanderer met on the road in Eriador.
        var elegost = Companions().SingleOrDefault(c => c.CharacterId == "named_companion_elegost");
        Assert.IsNotNull(elegost, "named_companion_elegost is not in named_companion_config.json");
        Assert.AreEqual("town_AN3", elegost!.SpawnSettlement, "Elegost waits in Bree");
        Assert.AreEqual("human", elegost.Race);

        var characters = File.ReadAllText(RepoPaths.RepoPath(
            "Main", "_Module", "ModuleData", "named_companions", "named_companions.xml"));
        var entry = Regex.Match(characters, "<NPCCharacter id=\"named_companion_elegost\"[^>]*>");
        StringAssert.Contains(entry.Value, "culture=\"Culture.arthedain\"");
        StringAssert.Contains(entry.Value, "occupation=\"Wanderer\"");
        StringAssert.Contains(entry.Value, "is_hero=\"true\"");
    }
}
