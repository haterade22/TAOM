using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SpecialResources;
using TAOM.Features.SpecialResources.Domain;
using TAOM.Features.TroopProgression;
using Newtonsoft.Json;
using TAOM.Tests.Core;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// Gates the shipped <c>troop_resource_costs.xml</c> against <c>special_resources_config.xml</c>.
/// Until #590 the row's <c>resource_id</c> was documentation only (every charge lands in the
/// PLAYER's resolved resource). The encyclopedia badge now reads it to pick the icon, so a row
/// naming a resource the config does not define would render a blank badge with no log.
/// </summary>
[TestClass]
public class TroopResourceCostDataTests
{
    private static XDocument Load(string fileName) =>
        XDocument.Load(Path.Combine(CultureDataFixture.ModuleDataPath(), "special_resources", fileName));

    private static Dictionary<string, string> IconByResourceId() =>
        Load("special_resources_config.xml").Root.Elements("Resource")
            .ToDictionary(r => (string)r.Attribute("id"), r => (string)r.Attribute("icon_sprite") ?? "");

    private static List<TroopResourceCostEntry> CostRows() =>
        Load("troop_resource_costs.xml").Root.Elements("Troop")
            .Select(t => new TroopResourceCostEntry(
                (string)t.Attribute("id"),
                (string)t.Attribute("resource_id"),
                (int)ParseFloat(t, "upgrade_cost"),
                ParseFloat(t, "daily_upkeep"),
                (int)ParseFloat(t, "recruit_cost"),
                (int)ParseFloat(t, "merchant_cost")))
            .ToList();

    private static float ParseFloat(XElement element, string attribute)
    {
        var raw = (string)element.Attribute(attribute);
        return string.IsNullOrEmpty(raw) ? 0f : float.Parse(raw, CultureInfo.InvariantCulture);
    }

    [TestMethod]
    public void EveryCostRow_ResourceId_NamesAConfiguredResource()
    {
        var known = IconByResourceId();
        var rows = CostRows();
        Assert.IsTrue(rows.Count > 0, "troop_resource_costs.xml has no rows");

        var unresolved = rows.Where(r => string.IsNullOrEmpty(r.ResourceId) || !known.ContainsKey(r.ResourceId))
            .Select(r => $"{r.TroopId} -> '{r.ResourceId}'").ToList();

        Assert.AreEqual(0, unresolved.Count,
            "Cost rows whose resource_id names no <Resource id> in special_resources_config.xml: " + string.Join(", ", unresolved));
    }

    [TestMethod]
    public void EveryMarkedRow_ResolvesAnIconSprite()
    {
        var icons = IconByResourceId();
        var marked = CostRows().Where(SpecialResourceTroopBadge.IsShown).ToList();
        Assert.IsTrue(marked.Count > 0, "no row carries an upgrade, recruit or upkeep cost, so the badge can never show");

        var blank = marked
            .Where(r => !icons.TryGetValue(r.ResourceId, out var icon) || !icon.StartsWith("SpecialResources\\"))
            .Select(r => r.TroopId).ToList();

        Assert.AreEqual(0, blank.Count,
            $"Of {marked.Count} badged rows, these reach no SpecialResources\\ icon sprite: " + string.Join(", ", blank));
    }

    /// <summary>
    /// Gondor's elites were never wired (#600): until 2026-09-15 its rows were merchant-only, so a
    /// Gondor player promoted into Fountain Guards for gold and XP alone. Every troop at level 41 or
    /// above in <c>troops_gondor.xml</c> must carry both an upgrade cost and a daily upkeep, so a
    /// future L41+ Gondor troop added without a row fails here instead of shipping free.
    /// </summary>
    [TestMethod]
    public void EveryGondorTroopAtLevel41OrAbove_CarriesUpgradeCostAndDailyUpkeep()
    {
        var troopsPath = Path.Combine(CultureDataFixture.ModuleDataPath(), "troops", "troops_gondor.xml");
        var elites = XDocument.Load(troopsPath).Root.Elements("NPCCharacter")
            .Where(c => int.TryParse((string)c.Attribute("level"), out var level) && level >= 41)
            .Select(c => (string)c.Attribute("id"))
            .ToList();
        Assert.IsTrue(elites.Count > 0, "troops_gondor.xml has no troop at level 41 or above");

        var rows = CostRows().ToDictionary(r => r.TroopId);
        var free = elites
            .Where(id => !rows.TryGetValue(id, out var row) || row.UpgradeCost <= 0 || row.DailyUpkeep <= 0f)
            .ToList();

        Assert.AreEqual(0, free.Count,
            $"Of {elites.Count} Gondor troops at level 41+, these carry no upgrade_cost or no daily_upkeep: " + string.Join(", ", free));
    }

    /// <summary>
    /// The Black Numenorean rows shipped at 1.0 to 3.0 a day, ten to twenty times every other tree
    /// troop, and forty of them drained a top battle payout every day (#558). The tree-troop band
    /// tops out at 0.4 (Gondor L51); only the three creatures sit above it by design.
    /// </summary>
    [TestMethod]
    public void NoTreeTroopUpkeep_ExceedsTheBandCeiling()
    {
        var creatures = new HashSet<string> { "harad_elephant_rider", "harad_mumakil_rider", "taom_spider_creature" };
        var rows = CostRows().Where(r => r.DailyUpkeep > 0f && !creatures.Contains(r.TroopId)).ToList();
        Assert.IsTrue(rows.Count > 0, "no tree troop carries a daily_upkeep, so the ceiling gates nothing");

        var over = rows.Where(r => r.DailyUpkeep > 0.4f)
            .Select(r => $"{r.TroopId} ({r.DailyUpkeep.ToString(CultureInfo.InvariantCulture)})")
            .ToList();

        Assert.AreEqual(0, over.Count,
            "Tree troops whose daily_upkeep exceeds the 0.4 band ceiling: " + string.Join(", ", over));
    }

    /// <summary>
    /// <c>upgrade_cost</c> fires only on the party-screen upgrade (Patch26). A troop a notable can
    /// hand out directly never takes that path: TAOM's <c>TaomVolunteerModel</c> seeds an empty
    /// volunteer slot straight from the recruitment pools, and vanilla's <c>MaxVolunteerTier</c> cap
    /// gates only the growth of an occupied slot, never the seed. So a pooled upkeep troop with no
    /// <c>recruit_cost</c> is free to acquire and only ever costs upkeep. Found in review of #600:
    /// the Ithilien Ranger (10 percent at Minas Tirith and both Osgiliaths) and the Fountain Guard
    /// (clan_empire_west_1) shipped that way for a day. The rams and the creatures already pair
    /// the two costs; this pins the pairing for every pool, hand-written and JSON.
    /// </summary>
    [TestMethod]
    public void EveryVolunteerPooledUpkeepTroop_CarriesRecruitCost()
    {
        var pooled = new HashSet<string>(VolunteerRecruitmentService.AllPooledTroopIds());
        foreach (var path in Directory.GetFiles(Path.Combine(CultureDataFixture.ModuleDataPath(), "recruitment_pools"), "*.json"))
        {
            var root = JsonConvert.DeserializeObject<GondorRecruitmentJsonRoot>(File.ReadAllText(path));
            foreach (var group in root?.ChanceGroups ?? new List<GondorRecruitmentChanceGroup>())
                foreach (var id in group.Troops?.Keys ?? Enumerable.Empty<string>())
                    pooled.Add(id);
        }
        Assert.IsTrue(pooled.Count > 0, "no volunteer pool yielded a troop id, so the gate checks nothing");

        var free = CostRows()
            .Where(r => r.DailyUpkeep > 0f && pooled.Contains(r.TroopId) && r.RecruitCost <= 0)
            .Select(r => r.TroopId)
            .ToList();

        Assert.AreEqual(0, free.Count,
            "Volunteer-pooled troops that cost upkeep but nothing to recruit (upgrade_cost never fires on a notable pick): " + string.Join(", ", free));
    }
}
