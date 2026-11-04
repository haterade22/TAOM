using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Core;

namespace TAOM.Tests.Features.Warg;

/// <summary>
/// Pins the dire wolf rider lines in the shipped data (#1081): the free branch off the archer line,
/// a wolf and its saddle in every battle roster, and War Spoils from level 26 at the lowest band.
/// </summary>
[TestClass]
public class DireWolfTroopDataTests
{
    private static readonly string[] GoblinLine = { "goblin_wolf_runner", "goblin_dire_wolf_rider", "goblin_wolf_lord" };
    private static readonly string[] GundabadLine = { "gundabad_wolf_runner", "gundabad_dire_wolf_rider", "gundabad_wolf_lord" };
    private static readonly string[] GoblinCultures = { "goblin", "mistymountainorcs", "bluecraig" };

    private static string DataPath(params string[] parts) =>
        Path.Combine(new[] { CultureDataFixture.ModuleDataPath() }.Concat(parts).ToArray());

    private static Dictionary<string, XElement> Troops(string file) =>
        XDocument.Load(DataPath("troops", file)).Root.Elements("NPCCharacter")
            .ToDictionary(c => (string)c.Attribute("id"));

    private static List<string> Targets(XElement troop) =>
        troop.Elements("upgrade_targets").Elements("upgrade_target")
            .Select(t => ((string)t.Attribute("id")).Replace("NPCCharacter.", ""))
            .ToList();

    private static void AssertLine(Dictionary<string, XElement> troops, string entry, string[] line, int[] levels)
    {
        CollectionAssert.Contains(Targets(troops[entry]), line[0], entry + " does not branch into the wolf line");
        for (var i = 0; i < line.Length; i++)
        {
            Assert.IsTrue(troops.ContainsKey(line[i]), line[i] + " is not defined");
            Assert.AreEqual(levels[i], (int)troops[line[i]].Attribute("level"), line[i]);
            Assert.AreEqual("Cavalry", (string)troops[line[i]].Attribute("default_group"), line[i]);
            var expected = i + 1 < line.Length ? new List<string> { line[i + 1] } : new List<string>();
            CollectionAssert.AreEqual(expected, Targets(troops[line[i]]), line[i]);
        }
    }

    private static void AssertRiders(Dictionary<string, XElement> troops, string[] line)
    {
        foreach (var id in line)
        {
            var rosters = troops[id].Elements("Equipments").Elements("EquipmentRoster").ToList();
            Assert.IsTrue(rosters.Count > 0, id + " has no battle roster");
            foreach (var roster in rosters)
            {
                var slots = roster.Elements("equipment")
                    .ToDictionary(e => (string)e.Attribute("slot"), e => (string)e.Attribute("id"));
                StringAssert.StartsWith(slots["Horse"], "Item.dire_wolf_", id);
                Assert.AreEqual("Item.dire_wolf_saddle", slots["HorseHarness"], id);
            }
        }
    }

    [TestMethod]
    public void GoblinLine_BranchesFromHunter_AndRidesAWolfInEveryRoster()
    {
        var troops = Troops("troops_goblin.xml");
        AssertLine(troops, "goblin_hunter", GoblinLine, new[] { 16, 21, 26 });
        AssertRiders(troops, GoblinLine);
    }

    [TestMethod]
    public void GundabadLine_BranchesFromLurker_AndRidesAWolfInEveryRoster()
    {
        var troops = Troops("troops_gundabad.xml");
        AssertLine(troops, "gundabad_lurker", GundabadLine, new[] { 21, 26, 31 });
        AssertRiders(troops, GundabadLine);
    }

    [TestMethod]
    public void RidersFromLevel26_PayTheLowestWarSpoilsBand_AndNoLowerRiderPays()
    {
        var rows = XDocument.Load(DataPath("special_resources", "troop_resource_costs.xml")).Root.Elements("Troop")
            .ToDictionary(t => (string)t.Attribute("id"));

        foreach (var id in new[] { "goblin_wolf_lord", "gundabad_dire_wolf_rider", "gundabad_wolf_lord" })
        {
            Assert.IsTrue(rows.ContainsKey(id), id + " has no cost row");
            Assert.AreEqual("war_spoils", (string)rows[id].Attribute("resource_id"), id);
            Assert.AreEqual("1", (string)rows[id].Attribute("upgrade_cost"), id);
            Assert.AreEqual("1", (string)rows[id].Attribute("recruit_cost"), id + ": a notable must not sell it free");
            Assert.AreEqual("0.05", (string)rows[id].Attribute("daily_upkeep"), id);
        }
        foreach (var id in new[] { "goblin_wolf_runner", "goblin_dire_wolf_rider", "gundabad_wolf_runner" })
            Assert.IsFalse(rows.ContainsKey(id), id + " is below level 26 and must stay free");
    }

    [TestMethod]
    public void WarSpoils_ListsTheThreeGoblinCultures_AsCultureAndKingdom()
    {
        var spoils = XDocument.Load(DataPath("special_resources", "special_resources_config.xml")).Root
            .Elements("Resource").Single(r => (string)r.Attribute("id") == "war_spoils");

        foreach (var id in GoblinCultures)
        {
            Assert.IsTrue(spoils.Elements("Culture").Any(c => (string)c.Attribute("id") == id), "Culture " + id);
            Assert.IsTrue(spoils.Elements("Kingdom").Any(k => (string)k.Attribute("id") == id), "Kingdom " + id);
        }
    }

    [TestMethod]
    public void Marketplace_StocksEachWolfAndTheSaddle_InItsCultures()
    {
        var routed = XDocument.Load(DataPath("culture_marketplace", "culture_marketplace_config.xml")).Root
            .Element("Routing").Elements("Item")
            .ToDictionary(i => (string)i.Attribute("id"), i => ((string)i.Attribute("cultures")).Split(','));

        foreach (var id in new[] { "dire_wolf_grey", "dire_wolf_white" })
            CollectionAssert.Contains(routed[id], "gundabad", id);
        foreach (var id in new[] { "dire_wolf_black", "dire_wolf_brown" })
            foreach (var culture in GoblinCultures)
                CollectionAssert.Contains(routed[id], culture, id);
        foreach (var culture in GoblinCultures.Concat(new[] { "gundabad" }))
            CollectionAssert.Contains(routed["dire_wolf_saddle"], culture, "dire_wolf_saddle");
    }
}
