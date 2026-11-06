using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.TroopProgression;

/// <summary>
/// Pins the promise of troops_lothlorien.xml: a Lothlorien player meets Lothlorien's own troops,
/// not Rivendell's. Before the tree existed the culture borrowed Rivendell's whole line, and when it
/// shipped, the troop slots moved but seven military party-template bindings stayed on Rivendell,
/// so joining Lothlorien still handed out Rivendell troops and no gate noticed:
/// CulturePartyTemplateTests allows sharing another culture's TAOM templates on purpose.
///
/// <para>
/// The villager is its own. Other civilian bindings (caravans, townsfolk, notables) are deliberately
/// outside this class: Lothlorien still shares Rivendell's, and that is a separate decision.
/// </para>
/// </summary>
[TestClass]
public class LothlorienOwnTreeDataTests
{
    private const string Culture = "lothlorien";

    private static readonly string[] TroopAttributes =
    {
        "basic_troop", "elite_basic_troop",
        "melee_militia_troop", "ranged_militia_troop",
        "melee_elite_militia_troop", "ranged_elite_militia_troop",
    };

    private static readonly string[] MilitaryTemplateAttributes =
    {
        "default_party_template",
        "settlement_patrol_template_level_1", "settlement_patrol_template_level_2", "settlement_patrol_template_level_3",
        "militia_party_template", "rebels_party_template", "vassal_reward_party_template",
    };

    [TestMethod]
    public void LothlorienCulture_TroopAndMilitiaSlots_NameOnlyLothlorienTroops()
    {
        var own = OwnTroopIds();
        var culture = CultureBlock();

        var foreign = TroopAttributes
            .Select(a => new { Attribute = a, Id = StripPrefix(culture.Attribute(a)?.Value, "NPCCharacter.") })
            .Where(x => string.IsNullOrEmpty(x.Id) || !own.Contains(x.Id))
            .Select(x => x.Attribute + "=" + (x.Id ?? "(missing)"))
            .ToList();

        Assert.AreEqual(0, foreign.Count,
            "These Lothlorien culture slots do not name a troop defined in troops_lothlorien.xml: " +
            string.Join(", ", foreign));
    }

    [TestMethod]
    public void LothlorienCulture_MilitaryPartyTemplates_AreLothlorienTemplatesOfLothlorienTroops()
    {
        var own = OwnTroopIds();
        var templates = TemplateStacks();
        var culture = CultureBlock();
        var problems = new List<string>();

        foreach (var attribute in MilitaryTemplateAttributes)
        {
            var id = StripPrefix(culture.Attribute(attribute)?.Value, "PartyTemplate.");
            if (string.IsNullOrEmpty(id))
            {
                problems.Add(attribute + " is not bound");
                continue;
            }

            if (id.IndexOf(Culture, StringComparison.Ordinal) < 0)
            {
                problems.Add(attribute + " -> " + id + " (not a Lothlorien template)");
                continue;
            }

            if (!templates.TryGetValue(id, out var stacks) || stacks.Count == 0)
            {
                problems.Add(attribute + " -> " + id + " (template missing or empty)");
                continue;
            }

            problems.AddRange(stacks.Where(t => !own.Contains(t)).Select(t => id + " fields " + t));
        }

        Assert.AreEqual(0, problems.Count,
            "Lothlorien's military templates must be its own and field only troops_lothlorien.xml troops:" +
            Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    [TestMethod]
    public void LothlorienClanTemplates_FieldOnlyLothlorienTroops()
    {
        var own = OwnTroopIds();
        var clanTemplates = TemplateStacks()
            .Where(kv => kv.Key.StartsWith("kingdom_hero_party_lothlorien_", StringComparison.Ordinal))
            .ToList();

        Assert.IsTrue(clanTemplates.Count >= 3,
            "Expected the Lothlorien default template and both clan templates, found " + clanTemplates.Count);

        var foreign = clanTemplates
            .SelectMany(kv => kv.Value.Where(t => !own.Contains(t)).Select(t => kv.Key + " fields " + t))
            .ToList();

        Assert.AreEqual(0, foreign.Count, string.Join(Environment.NewLine, foreign));
    }

    [TestMethod]
    public void LothlorienTroopsFromLevel31_PayTheLevelBandTroopWeight()
    {
        var rows = TroopWeightRows();
        var levels = LothlorienLevels();
        var problems = new List<string>();

        foreach (var troop in levels.Where(kv => kv.Value >= 31
                     && kv.Key.IndexOf("_militia_", StringComparison.Ordinal) < 0
                     && !kv.Key.EndsWith("_merc", StringComparison.Ordinal)))
        {
            var expected = troop.Value >= 41 ? 3.0 : 2.0;
            if (!rows.TryGetValue(troop.Key, out var weight))
                problems.Add(troop.Key + " (level " + troop.Value + ") has no troop weight row, expected " + expected);
            else if (Math.Abs(weight - expected) > 0.0001)
                problems.Add(troop.Key + " (level " + troop.Value + ") weighs " + weight + ", expected " + expected);
        }

        Assert.AreEqual(0, problems.Count,
            "Troop weights follow the level rule at the top of troop_weights.xml (41 and above 3.0, below 2.0). " +
            "An unlisted elite weighs 1.0, so a Lothlorien army fills its cap with elites at a third of their cost:" +
            Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    [TestMethod]
    public void LothlorienVillager_IsItsOwnAndUpgradesIntoItsEntryTroop()
    {
        var culture = CultureBlock();
        Assert.AreEqual("NPCCharacter.villager_lothlorien", (string)culture.Attribute("villager"));
        Assert.AreEqual("PartyTemplate.villager_lothlorien_template", (string)culture.Attribute("villager_party_template"));

        var villagerStacks = TemplateStacks()["villager_lothlorien_template"];
        CollectionAssert.AreEqual(new[] { "villager_lothlorien" }, villagerStacks);

        var villager = XDocument.Load(Path.Combine(ModuleData(), "characters", "npcs_lothlorien.xml"))
            .Descendants("NPCCharacter").Single(n => (string)n.Attribute("id") == "villager_lothlorien");
        var targets = villager.Descendants("upgrade_target")
            .Select(t => StripPrefix((string)t.Attribute("id"), "NPCCharacter.")).ToList();
        CollectionAssert.AreEqual(new[] { StripPrefix((string)culture.Attribute("basic_troop"), "NPCCharacter.") }, targets,
            "A Lothlorien villager must promote into the culture's basic troop, not into Rivendell's imladris_recruit.");
        Assert.IsTrue(OwnTroopIds().Contains(targets[0]));
    }

    [TestMethod]
    public void LothlorienLordBattleTemplates_WearLorienArmourOnEveryArmourSlot()
    {
        var path = Path.Combine(ModuleData(), "equipmentsets", "taom_equipment_sets_lothlorien.xml");
        var rosters = XDocument.Load(path).Descendants("EquipmentRoster")
            .Where(r => ((string)r.Attribute("id") ?? "").StartsWith("lothlorien_bat_template_medium_", StringComparison.Ordinal))
            .ToList();
        Assert.IsTrue(rosters.Count >= 5, "Expected the five Lothlorien lord battle templates, found " + rosters.Count);

        var slots = new[] { "Head", "Body", "Cape", "Gloves", "Leg" };
        var foreign = rosters
            .SelectMany(r => r.Descendants("Equipment")
                .Where(e => slots.Contains((string)e.Attribute("slot"))
                            && !((string)e.Attribute("id") ?? "").StartsWith("Item.sk_ll_", StringComparison.Ordinal))
                .Select(e => (string)r.Attribute("id") + " " + (string)e.Attribute("slot") + "=" + (string)e.Attribute("id")))
            .ToList();

        Assert.AreEqual(0, foreign.Count,
            "Lothlorien lords wear the sk_ll_ kit; these slots still hold another culture's armour:" +
            Environment.NewLine + string.Join(Environment.NewLine, foreign));
    }

    private static Dictionary<string, int> LothlorienLevels()
    {
        var path = Path.Combine(ModuleData(), "troops", "troops_lothlorien.xml");
        var levels = XDocument.Load(path).Descendants("NPCCharacter")
            .Where(n => !string.IsNullOrEmpty((string)n.Attribute("id")) && n.Attribute("level") != null)
            .ToDictionary(n => (string)n.Attribute("id"), n => int.Parse((string)n.Attribute("level")), StringComparer.Ordinal);
        Assert.IsTrue(levels.Count > 0, "troops_lothlorien.xml defines no leveled troops, so nothing was checked");
        return levels;
    }

    private static Dictionary<string, double> TroopWeightRows()
    {
        var path = Path.Combine(ModuleData(), "TroopWeights", "troop_weights.xml");
        return XDocument.Load(path).Descendants("TroopWeight")
            .Where(r => !string.IsNullOrEmpty((string)r.Attribute("id")))
            .ToDictionary(
                r => (string)r.Attribute("id"),
                r => double.Parse((string)r.Attribute("weight"), System.Globalization.CultureInfo.InvariantCulture),
                StringComparer.Ordinal);
    }

    private static XElement CultureBlock()
    {
        var path = Path.Combine(ModuleData(), "taom_spcultures.xml");
        var block = XDocument.Load(path).Descendants("Culture")
            .FirstOrDefault(c => (string)c.Attribute("id") == Culture);
        Assert.IsNotNull(block, "taom_spcultures.xml has no Culture id=\"" + Culture + "\"");
        return block;
    }

    private static HashSet<string> OwnTroopIds()
    {
        var path = Path.Combine(ModuleData(), "troops", "troops_lothlorien.xml");
        var ids = XDocument.Load(path).Descendants("NPCCharacter")
            .Select(n => (string)n.Attribute("id"))
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();
        Assert.IsTrue(ids.Count > 0, "troops_lothlorien.xml defines no troops, so nothing was checked");
        return new HashSet<string>(ids, StringComparer.Ordinal);
    }

    private static Dictionary<string, List<string>> TemplateStacks()
    {
        var path = Path.Combine(ModuleData(), "taom_partyTemplates.xml");
        return XDocument.Load(path).Descendants("MBPartyTemplate")
            .Where(t => !string.IsNullOrEmpty((string)t.Attribute("id")))
            .GroupBy(t => (string)t.Attribute("id"), StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.SelectMany(t => t.Descendants("PartyTemplateStack"))
                    .Select(s => StripPrefix((string)s.Attribute("troop"), "NPCCharacter."))
                    .Where(id => !string.IsNullOrEmpty(id))
                    .ToList(),
                StringComparer.Ordinal);
    }

    private static string StripPrefix(string value, string prefix) =>
        value != null && value.StartsWith(prefix, StringComparison.Ordinal) ? value.Substring(prefix.Length) : value;

    private static string ModuleData()
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        for (int i = 0; i < 12 && dir != null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "Main", "_Module", "ModuleData");
            if (Directory.Exists(Path.Combine(candidate, "troops")))
                return candidate;
        }

        Assert.Fail("ModuleData/troops was not found within 12 parent directories of " +
                    AppDomain.CurrentDomain.BaseDirectory +
                    ". This suite gates shipped troop data, so it fails rather than skipping.");
        return null;
    }
}
