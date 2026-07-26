using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.TroopProgression;

/// <summary>
/// Pins the tavern-mercenary data contract on <c>&lt;basic_mercenary_troops&gt;</c> in
/// taom_spcultures.xml.
///
/// <para>
/// <c>RecruitmentCampaignBehavior.UpdateCurrentMercenaryTroopAndCount</c> draws a random entry from
/// <c>town.Culture.BasicMercenaryTroops</c> (70% of daily rerolls; the other 30% use
/// <c>CaravanGuard</c>), then randomly walks that troop's <c>UpgradeTargets</c> — each tier deeper
/// weighted 1/1.5×. Whatever it lands on is offered in the town's backstreet menu AND spawned as the
/// walking tavern NPC by <c>SandBox.RecruitmentAgentSpawnBehavior.CreateMercenary</c>.
/// </para>
///
/// <para>Three invariants, each guarding a distinct failure that shipped or nearly shipped:</para>
/// <list type="number">
/// <item>TAOM-defined troops only — every culture shipped vanilla's Calradian list verbatim, so
/// Minas Morgul's tavern sold "Hired Pike" (<c>western_mercenary_t4</c>, culture
/// <c>neutral_culture</c>).</item>
/// <item><c>occupation="Mercenary"</c> — the tavern NPC's hire dialogue only fires for
/// Mercenary/CaravanGuard/Gangster, and AI lord/caravan hiring gates on Mercenary.</item>
/// <item>No <c>upgrade_targets</c> — these are dedicated leaf copies. An upgrade target would let
/// the engine's walk drift out of the mercenary set and back onto a Soldier line troop.</item>
/// </list>
/// </summary>
[TestClass]
public class TavernMercenaryDataTests
{
    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

    private static string ModuleDataPath => Path.Combine(RepoRoot, @"Main\_Module\ModuleData");

    private static string RecruitmentPoolsPath => Path.Combine(
        RepoRoot, @"Main\Features\TroopProgression\RecruitmentPools");

    /// <summary>Suffix marking a troop as a tavern-mercenary copy of the same-named line troop.</summary>
    private const string MercenarySuffix = "_merc";

    private static readonly Regex CultureBlock = new Regex(
        "<Culture\\s+id=\"(?<id>[^\"]+)\"(?<body>.*?)</Culture>", RegexOptions.Singleline);

    private static readonly Regex MercenaryBlock = new Regex(
        "<basic_mercenary_troops>(?<body>.*?)</basic_mercenary_troops>", RegexOptions.Singleline);

    private static readonly Regex TemplateRef = new Regex(
        "<template\\s+name=\"NPCCharacter\\.(?<id>[^\"]+)\"");

    private static readonly Regex NpcCharacter = new Regex(
        "<NPCCharacter\\b(?<attrs>[^>]*)>(?<body>.*?)</NPCCharacter>", RegexOptions.Singleline);

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ShippedCultures_EveryTavernMercenaryIsTaomDefined()
    {
        var troops = IndexTroopDefinitions();
        var references = CollectMercenaryReferences();

        Assert.AreNotEqual(0, references.Count,
            "no <basic_mercenary_troops> entries found — parser broken?");

        var undefined = references
            .Where(r => !troops.ContainsKey(r.TroopId))
            .Select(r => $"{r.CultureId} -> {r.TroopId}")
            .ToList();

        Assert.AreEqual(0, undefined.Count,
            "tavern mercenaries must be TAOM troops, not vanilla/neutral_culture ids: "
            + string.Join(", ", undefined));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ShippedCultures_EveryTavernMercenaryHasMercenaryOccupation()
    {
        var troops = IndexTroopDefinitions();

        var wrongOccupation = CollectMercenaryReferences()
            .Where(r => troops.ContainsKey(r.TroopId))
            .Where(r => troops[r.TroopId].Occupation != "Mercenary")
            .Select(r => $"{r.TroopId} (occupation={troops[r.TroopId].Occupation ?? "<none>"})")
            .Distinct()
            .ToList();

        Assert.AreEqual(0, wrongOccupation.Count,
            "tavern-mercenary troops need occupation=\"Mercenary\" or the tavern hire dialogue "
            + "never fires (RecruitmentCampaignBehavior gates on Mercenary/CaravanGuard/Gangster): "
            + string.Join(", ", wrongOccupation));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ShippedCultures_EveryTavernMercenaryIsALeafCopy()
    {
        var troops = IndexTroopDefinitions();

        var withUpgrades = CollectMercenaryReferences()
            .Where(r => troops.ContainsKey(r.TroopId))
            .Where(r => troops[r.TroopId].UpgradeTargets.Count > 0)
            .Select(r => $"{r.TroopId} -> {string.Join("/", troops[r.TroopId].UpgradeTargets)}")
            .Distinct()
            .ToList();

        Assert.AreEqual(0, withUpgrades.Count,
            "tavern-mercenary troops must be leaves — the engine walks UpgradeTargets when picking "
            + "the town's offer, so an upgrade target lets it drift onto a Soldier line troop: "
            + string.Join(", ", withUpgrades));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ShippedCultures_EveryTavernMercenaryCopiesARarestVolunteerPoolEntry()
    {
        var pools = IndexVolunteerPools();
        var offenders = new List<string>();

        foreach (var reference in CollectMercenaryReferences())
        {
            if (!pools.TryGetValue(reference.CultureId, out var pool) || pool.Count == 0)
            {
                offenders.Add($"{reference.CultureId} has no CultureMap recruitment pool");
                continue;
            }

            if (!reference.TroopId.EndsWith(MercenarySuffix, StringComparison.Ordinal))
            {
                offenders.Add($"{reference.TroopId} is not a '{MercenarySuffix}' copy");
                continue;
            }

            string sourceId = reference.TroopId.Substring(
                0, reference.TroopId.Length - MercenarySuffix.Length);
            int rarest = pool.Values.Min();

            if (!pool.TryGetValue(sourceId, out int weight))
                offenders.Add($"{sourceId} absent from {reference.CultureId}'s recruitment pool");
            else if (weight != rarest)
                offenders.Add($"{sourceId} has weight {weight}, rarest in {reference.CultureId} is {rarest}");
        }

        Assert.AreEqual(0, offenders.Count,
            "tavern mercenaries must copy the rarest (lowest VolunteerChance weight) entries of "
            + "their culture's recruitment pool: " + string.Join("; ", offenders));
    }

    private sealed class TroopDefinition
    {
        public string? Occupation { get; set; }
        public List<string> UpgradeTargets { get; } = new List<string>();
    }

    private readonly struct MercenaryReference
    {
        public MercenaryReference(string cultureId, string troopId)
        {
            CultureId = cultureId;
            TroopId = troopId;
        }

        public string CultureId { get; }
        public string TroopId { get; }
    }

    private static List<MercenaryReference> CollectMercenaryReferences()
    {
        string xml = File.ReadAllText(Path.Combine(ModuleDataPath, "taom_spcultures.xml"));
        var references = new List<MercenaryReference>();

        foreach (Match culture in CultureBlock.Matches(xml))
        {
            var block = MercenaryBlock.Match(culture.Groups["body"].Value);
            if (!block.Success)
                continue;

            foreach (Match template in TemplateRef.Matches(block.Groups["body"].Value))
            {
                references.Add(new MercenaryReference(
                    culture.Groups["id"].Value, template.Groups["id"].Value));
            }
        }

        return references;
    }

    private static Dictionary<string, TroopDefinition> IndexTroopDefinitions()
    {
        var definitions = new Dictionary<string, TroopDefinition>(StringComparer.Ordinal);

        foreach (string path in TroopSourceFiles())
        {
            foreach (Match npc in NpcCharacter.Matches(File.ReadAllText(path)))
            {
                var id = Regex.Match(npc.Groups["attrs"].Value, "id=\"(?<id>[^\"]+)\"");
                if (!id.Success)
                    continue;

                var definition = new TroopDefinition
                {
                    Occupation = Regex.Match(npc.Groups["attrs"].Value, "occupation=\"(?<o>[^\"]+)\"")
                        is { Success: true } occupation
                        ? occupation.Groups["o"].Value
                        : null
                };

                foreach (Match upgrade in Regex.Matches(
                    npc.Groups["body"].Value, "<upgrade_target id=\"NPCCharacter\\.(?<id>[^\"]+)\""))
                {
                    definition.UpgradeTargets.Add(upgrade.Groups["id"].Value);
                }

                definitions[id.Groups["id"].Value] = definition;
            }
        }

        return definitions;
    }

    private static IEnumerable<string> TroopSourceFiles()
    {
        foreach (string path in Directory.GetFiles(
            Path.Combine(ModuleDataPath, "troops"), "*.xml"))
        {
            yield return path;
        }

        foreach (string path in Directory.GetFiles(
            Path.Combine(ModuleDataPath, "characters"), "*.xml"))
        {
            yield return path;
        }
    }

    /// <summary>
    /// culture id -&gt; (troop id -&gt; lowest VolunteerChance weight) from the shipped
    /// <c>CultureMap</c> declarations in VolunteerRecruitmentService's partial classes.
    /// </summary>
    private static Dictionary<string, Dictionary<string, int>> IndexVolunteerPools()
    {
        var pools = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        var cultureMap = new Regex(
            "CultureMap\\[\"(?<culture>[^\"]+)\"\\][^;]*?\\{(?<body>.*?)\\};", RegexOptions.Singleline);
        var entry = new Regex("\\(\"(?<troop>[a-z0-9_]+)\",\\s*(?<weight>\\d+)\\)");

        foreach (string path in Directory.GetFiles(RecruitmentPoolsPath, "*.cs"))
        {
            foreach (Match map in cultureMap.Matches(File.ReadAllText(path)))
            {
                if (!pools.TryGetValue(map.Groups["culture"].Value, out var pool))
                {
                    pool = new Dictionary<string, int>(StringComparer.Ordinal);
                    pools[map.Groups["culture"].Value] = pool;
                }

                foreach (Match troop in entry.Matches(map.Groups["body"].Value))
                {
                    string id = troop.Groups["troop"].Value;
                    int weight = int.Parse(troop.Groups["weight"].Value);
                    pool[id] = pool.TryGetValue(id, out int existing) ? Math.Min(existing, weight) : weight;
                }
            }
        }

        return pools;
    }
}
