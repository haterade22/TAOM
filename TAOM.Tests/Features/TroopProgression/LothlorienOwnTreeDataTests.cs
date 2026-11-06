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
    public void LothlorienClans_DefaultPartyTemplates_AreBoundAndFieldOnlyLothlorienTroops()
    {
        var own = OwnTroopIds();
        var templates = TemplateStacks();
        var clans = XDocument.Load(Path.Combine(ModuleData(), "characters", "clans.xml")).Descendants("Faction")
            .Where(c => (string)c.Attribute("culture") == "Culture." + Culture)
            .ToList();

        Assert.IsTrue(clans.Count >= 3, "Expected the three Lothlorien clans in clans.xml, found " + clans.Count);

        var problems = new List<string>();
        foreach (var clan in clans)
        {
            var clanId = (string)clan.Attribute("id");
            var templateId = StripPrefix((string)clan.Attribute("default_party_template"), "PartyTemplate.");
            if (string.IsNullOrEmpty(templateId))
            {
                problems.Add(clanId + " binds no default_party_template");
                continue;
            }

            if (!templates.TryGetValue(templateId, out var stacks) || stacks.Count == 0)
            {
                problems.Add(clanId + " -> " + templateId + " (template missing or empty)");
                continue;
            }

            problems.AddRange(stacks.Where(t => !own.Contains(t)).Select(t => clanId + " -> " + templateId + " fields " + t));
        }

        Assert.AreEqual(0, problems.Count, string.Join(Environment.NewLine, problems));
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
        Assert.AreEqual(5, rosters.Count, "Expected the five Lothlorien lord battle templates, found " + rosters.Count);

        var problems = ArmourSlotProblems(rosters);

        Assert.AreEqual(0, problems.Count,
            "Lothlorien lords wear the sk_ll_ kit in every armour slot; these slots are missing or hold another culture's armour:" +
            Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    [TestMethod]
    public void LothlorienGeneratedLordAndRulerBattleRosters_WearLorienArmourOnEveryArmourSlot()
    {
        // taom_lord_template_equipment.xml is derived from the first battle set of each culture's equipment file.
        // The engine picks these rosters for a new ruler, a rebel lord and a woman who comes of age as a lord,
        // so they must follow lothlorien_bat_template_medium_a. Do not regenerate the file with
        // generate_lord_template_equipment.py --apply (issue #637: it deletes and re-equips other cultures'
        // rosters); copy the five armour slots by hand.
        var path = Path.Combine(ModuleData(), "equipmentsets", "taom_lord_template_equipment.xml");
        var rosters = XDocument.Load(path).Descendants("EquipmentRoster")
            .Where(r => System.Text.RegularExpressions.Regex.IsMatch(
                (string)r.Attribute("id") ?? "", "^taom_lothlorien_(lord|ruler)_battle_(male|female)$"))
            .ToList();
        Assert.AreEqual(4, rosters.Count, "Expected lord and ruler battle rosters for both sexes, found " + rosters.Count);

        var problems = ArmourSlotProblems(rosters);

        Assert.AreEqual(0, problems.Count,
            "Lothlorien lord and ruler battle rosters must wear the sk_ll_ kit; these slots still differ:" +
            Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    [TestMethod]
    public void LothlorienNamedHeroBattleSets_WearLorienArmourOnEveryArmourSlot()
    {
        // Galadriel, Celeborn and Thandirion have their own battle sets (the other eight lords share medium_a to _e).
        var path = Path.Combine(ModuleData(), "equipmentsets", "taom_equipment_sets_lothlorien.xml");
        var wanted = new[] { "galadriel_bat_equipment", "celeborn_bat_equipment", "thandirion_bat_equipment" };
        var rosters = XDocument.Load(path).Descendants("EquipmentRoster")
            .Where(r => wanted.Contains((string)r.Attribute("id")))
            .ToList();
        Assert.AreEqual(wanted.Length, rosters.Count, "Expected the three named hero battle sets, found " + rosters.Count);

        var problems = ArmourSlotProblems(rosters);

        Assert.AreEqual(0, problems.Count,
            "The named Lothlorien heroes wear the sk_ll_ kit in every armour slot; these slots are missing or differ:" +
            Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    [TestMethod]
    public void LothlorienEliteEmissary_OffersItsOwnEliteTroopsAtTheElvenWinePriceForTheirLevel()
    {
        // The price scale every other culture's offers follow: level 36 costs 12 to 14, 41 costs 18, 46 costs 28, 51 costs 45.
        var priceByLevel = new Dictionary<int, int> { { 41, 18 }, { 46, 28 } };

        var config = XDocument.Load(Path.Combine(ModuleData(), "elite_emissary", "elite_emissary_config.xml"));
        Assert.IsTrue(
            config.Descendants("KeySettlements").Descendants("Settlement").Any(s => (string)s.Attribute("id") == "town_L1"),
            "The emissary must appear in Caras Galadhon (town_L1), Lothlorien's capital.");
        var offers = config.Descendants("CultureOffers").Descendants("Culture")
            .SingleOrDefault(c => (string)c.Attribute("id") == Culture);
        Assert.IsNotNull(offers, "elite_emissary_config.xml has no Culture id=\"" + Culture + "\" offer list");

        var prices = XDocument.Load(Path.Combine(ModuleData(), "special_resources", "troop_resource_costs.xml"))
            .Descendants("Troop").Where(t => t.Attribute("merchant_cost") != null)
            .ToDictionary(t => (string)t.Attribute("id"), t => t, StringComparer.Ordinal);
        var own = OwnTroopIds();
        var levels = LothlorienLevels();

        var ids = offers.Descendants("Troop").Select(t => (string)t.Attribute("id")).ToList();
        Assert.IsTrue(ids.Count >= 6, "Expected at least six offers, found " + ids.Count);
        var problems = new List<string>();
        foreach (var id in ids)
        {
            if (!own.Contains(id)) { problems.Add(id + " is not a troop of troops_lothlorien.xml"); continue; }
            if (levels[id] < 41) { problems.Add(id + " is level " + levels[id] + ", below the 41 the elven offers start at"); continue; }
            if (!prices.TryGetValue(id, out var row)) { problems.Add(id + " has no merchant_cost row"); continue; }
            if ((string)row.Attribute("resource_id") != "elven_wine") problems.Add(id + " is priced in " + (string)row.Attribute("resource_id"));
            if (priceByLevel.TryGetValue(levels[id], out var want) && (int)row.Attribute("merchant_cost") != want)
                problems.Add(id + " (level " + levels[id] + ") costs " + (string)row.Attribute("merchant_cost") + ", expected " + want);
        }

        Assert.AreEqual(0, problems.Count, string.Join(Environment.NewLine, problems));

        var resource = XDocument.Load(Path.Combine(ModuleData(), "special_resources", "special_resources_config.xml"))
            .Descendants("Resource").Single(r => (string)r.Attribute("id") == "elven_wine");
        Assert.IsTrue(resource.Elements("Culture").Any(c => (string)c.Attribute("id") == Culture),
            "The emissary charges the owner culture's resource, so lothlorien must map to elven_wine.");
    }

    [TestMethod]
    public void LothlorienArmourLadder_UsesItsOwnMaterialWeaponKitAndQuestRows()
    {
        // The lord's gear ladder, the Deep Seam and the Armourer's Commission used to reach Rivendell's armour through
        // armour_from. Lothlorien owns armour now, so each one names Lorien items (Mike, 2026-11-06).
        var acquisition = XDocument.Load(Path.Combine(ModuleData(), "armour_acquisition", "armour_acquisition_config.xml"));
        var material = acquisition.Descendants("LordsMaterials").Descendants("Material")
            .SingleOrDefault(m => (string)m.Attribute("culture") == Culture);
        Assert.IsNotNull(material, "armour_acquisition_config.xml has no lord's material for lothlorien");
        var materialId = (string)material.Attribute("item");
        Assert.AreEqual("taom_lords_material_lothlorien", materialId);
        Assert.IsTrue(acquisition.Descendants("LadderWeapons").Descendants("Weapon").Any(w => (string)w.Attribute("culture") == Culture),
            "the weapon rung needs a Lorien pick");

        var items = XDocument.Load(Path.Combine(ModuleData(), "armour_acquisition", "taom_lords_materials.xml"));
        Assert.IsTrue(items.Descendants("Item").Any(i => (string)i.Attribute("id") == materialId),
            "taom_lords_materials.xml must define " + materialId);

        var marketplace = XDocument.Load(Path.Combine(ModuleData(), "culture_marketplace", "culture_marketplace_config.xml"));
        Assert.IsFalse(marketplace.Descendants("Culture").Any(c => (string)c.Attribute("id") == Culture
                                                                    && !string.IsNullOrEmpty((string)c.Attribute("armour_from"))),
            "Lothlorien owns armour, so it keeps no armour_from donor");

        var issues = XDocument.Load(Path.Combine(ModuleData(), "lotr_issues", "taom_lotr_issues.xml")).Descendants("LotrIssue").ToList();
        var servesLorien = issues.Where(i => ((string)i.Attribute("cultures") ?? "").Split(',').Select(c => c.Trim()).Contains(Culture)).ToList();
        var seam = servesLorien.Where(i => ((string)i.Attribute("id")).StartsWith("lotr_deep_seam_", StringComparison.Ordinal)).ToList();
        Assert.AreEqual(1, seam.Count, "exactly one Deep Seam row serves lothlorien");
        Assert.AreEqual(materialId, (string)seam[0].Attribute("reward_item"));
        var commission = servesLorien.Where(i => ((string)i.Attribute("id")).StartsWith("lotr_armourer_commission_", StringComparison.Ordinal)).ToList();
        Assert.AreEqual(1, commission.Count, "exactly one Armourer's Commission row serves lothlorien");
        StringAssert.StartsWith((string)commission[0].Attribute("reward_item"), "sk_ll_");

        // Every ladder rung needs a Lorien piece: its lord pieces, else its best elite, else its best heavy.
        var slotOfToken = new Dictionary<string, string>
        {
            { "_helmet", "head" }, { "_crown", "head" }, { "_chest", "body" }, { "_pauld", "shoulders" },
            { "_bracer", "hands" }, { "_grvs", "legs" }, { "_boots", "legs" },
        };
        var classes = XDocument.Load(Path.Combine(ModuleData(), "armour_acquisition", "armour_classes.xml")).Descendants("Item")
            .Where(i => ((string)i.Attribute("id") ?? "").StartsWith("sk_ll_", StringComparison.Ordinal)
                        && new[] { "lord", "elite", "heavy" }.Contains((string)i.Attribute("class")))
            .Select(i => slotOfToken.FirstOrDefault(kv => ((string)i.Attribute("id")).Contains(kv.Key)).Value)
            .ToList();
        foreach (var slot in new[] { "hands", "legs", "shoulders", "head", "body" })
            Assert.IsTrue(classes.Contains(slot), "no Lorien lord, elite or heavy piece for the " + slot + " rung");
    }

    private static readonly string[] ArmourSlots = { "Head", "Body", "Cape", "Gloves", "Leg" };

    private static List<string> ArmourSlotProblems(IEnumerable<XElement> rosters)
    {
        var problems = new List<string>();
        foreach (var roster in rosters)
        {
            var rosterId = (string)roster.Attribute("id");
            foreach (var slot in ArmourSlots)
            {
                var items = roster.Descendants("Equipment").Where(e => (string)e.Attribute("slot") == slot).ToList();
                if (items.Count != 1)
                    problems.Add(rosterId + " " + slot + " has " + items.Count + " items, expected 1");
                else if (!((string)items[0].Attribute("id") ?? "").StartsWith("Item.sk_ll_", StringComparison.Ordinal))
                    problems.Add(rosterId + " " + slot + "=" + (string)items[0].Attribute("id"));
            }
        }

        return problems;
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
