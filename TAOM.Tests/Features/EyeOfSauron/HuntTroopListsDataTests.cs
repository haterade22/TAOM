using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.EyeOfSauron;
using TAOM.Features.NazgulFamily;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.EyeOfSauron;

/// <summary>
/// #765 M8: the hunt's lore data against the shipped ModuleData. Every host troop must exist as an NPCCharacter in
/// Main/_Module/ModuleData (an unknown id would make the top-up silently add nothing), be a soldier of the host's
/// culture, and be high-tier; the hunter order must be exactly the Nine, Witch-king first and Khamul last.
/// </summary>
[TestClass]
public class HuntTroopListsDataTests
{
    private const int HighTierLevel = 36;

    private static readonly Lazy<Dictionary<string, XElement>> Characters = new(LoadCharacters);

    private static Dictionary<string, XElement> LoadCharacters()
    {
        var byId = new Dictionary<string, XElement>(StringComparer.Ordinal);
        // Languages holds string tables, never an NPCCharacter, so the scan skips it.
        var moduleData = RepoPaths.RepoPath("Main", "_Module", "ModuleData");
        var languages = Path.Combine(moduleData, "Languages") + Path.DirectorySeparatorChar;
        foreach (var file in Directory.EnumerateFiles(moduleData, "*.xml", SearchOption.AllDirectories)
                     .Where(f => !f.StartsWith(languages, StringComparison.OrdinalIgnoreCase)))
        {
            XDocument doc;
            try { doc = XDocument.Load(file); }
            catch (System.Xml.XmlException) { continue; }
            foreach (var el in doc.Descendants("NPCCharacter"))
            {
                var id = (string?)el.Attribute("id");
                // The first definition is kept unless it lacks a level; custom_battle_lords.xml carries id-and-culture stubs.
                if (id != null && (!byId.ContainsKey(id) || byId[id].Attribute("level") == null))
                    byId[id] = el;
            }
        }
        Assert.IsTrue(byId.Count > 1000, $"only {byId.Count} NPCCharacters parsed; the scan is broken");
        return byId;
    }

    public static IEnumerable<object[]> HostTroops() =>
        new[]
        {
            ("Mordor", HuntTroopLists.Mordor, "Culture.mordor"),
            ("DolGuldur", HuntTroopLists.DolGuldur, "Culture.dolguldur"),
            ("Rivendell", HuntTroopLists.Rivendell, "Culture.rivendell"),
        }.SelectMany(h => h.Item2.Select(t => new object[] { h.Item1, t.TroopId, h.Item3 }));

    [DataTestMethod]
    [DynamicData(nameof(HostTroops), DynamicDataSourceType.Method)]
    public void EveryHostTroop_ExistsAsAHighTierSoldierOfItsCulture(string host, string troopId, string culture)
    {
        Assert.IsTrue(Characters.Value.TryGetValue(troopId, out var el), $"{host}: {troopId} is not in ModuleData");
        Assert.AreEqual(culture, (string?)el!.Attribute("culture"), $"{host}: {troopId} culture");
        Assert.AreEqual("Soldier", (string?)el.Attribute("occupation"), $"{host}: {troopId} occupation");
        Assert.IsTrue(int.TryParse((string?)el.Attribute("level"), out int level) && level >= HighTierLevel,
            $"{host}: {troopId} is level {(string?)el.Attribute("level")}, under {HighTierLevel}");
    }

    [TestMethod]
    public void EveryHost_SharesSumToOne()
    {
        foreach (var host in new[] { HuntTroopLists.Mordor, HuntTroopLists.DolGuldur, HuntTroopLists.Rivendell })
            Assert.AreEqual(1f, host.Sum(t => t.Share), 1e-4f);
    }

    [TestMethod]
    public void WraithOrder_IsExactlyTheNine_WitchKingFirst_KhamulLast()
    {
        var registry = new NazgulRegistry();

        Assert.AreEqual(9, HuntTroopLists.WraithOrder.Count, "no hunter is listed twice");
        Assert.AreEqual(9, HuntTroopLists.WraithOrder.Distinct().Count());
        Assert.IsTrue(HuntTroopLists.WraithOrder.All(registry.IsWraith), "every hunter is one of the Nine");
        Assert.AreEqual("lord_1_15", HuntTroopLists.WraithOrder.First());
        Assert.AreEqual("lord_1_48", HuntTroopLists.WraithOrder.Last());
    }

    [TestMethod]
    public void Glorfindel_IsARivendellLord()
    {
        Assert.IsTrue(Characters.Value.TryGetValue(HuntTroopLists.GlorfindelId, out var el));
        Assert.AreEqual("Culture.rivendell", (string?)el!.Attribute("culture"));
        Assert.AreEqual("Lord", (string?)el.Attribute("occupation"));
        CollectionAssert.AreEqual(new[] { HuntTroopLists.GlorfindelId }, HuntTroopLists.ElvenOrder.ToArray());
    }

    [DataTestMethod]
    [DataRow("lord_R2_1", "rivendell", "Rivendell")]
    [DataRow("lord_1_48", "dolguldur", "DolGuldur")]
    [DataRow("lord_1_15", "empire_s", "Mordor")]
    [DataRow("lord_1_48_1", "dolguldur", "DolGuldur")]
    [DataRow("lord_1_48_2", "dolguldur", "DolGuldur")]
    [DataRow("lord_1_48_3", "dolguldur", "DolGuldur")]
    [DataRow("lord_1_155", "empire_s", "Mordor")]
    [DataRow("lord_1_155", null, "Mordor")]
    public void HostFor_PicksTheHostByHunterAndFaction(string heroId, string? faction, string expected)
    {
        var host = HuntTroopLists.HostFor(heroId, faction);
        var name = host == HuntTroopLists.Rivendell ? "Rivendell" : host == HuntTroopLists.DolGuldur ? "DolGuldur" : "Mordor";

        Assert.AreEqual(expected, name);
    }

    /// <summary>
    /// HostFor routes on the hunter's live kingdom id, so the id must stay the one ModuleData defines, ruled by Khamul;
    /// a rename would silently give every Dol Guldur wraith Mordor's host.
    /// </summary>
    [TestMethod]
    public void DolGuldurKingdomId_IsTheKingdomKhamulRules()
    {
        var kingdom = XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "taom_spkingdoms.xml"))
            .Descendants("Kingdom")
            .SingleOrDefault(k => (string?)k.Attribute("id") == HuntTroopLists.DolGuldurKingdomId);

        Assert.IsNotNull(kingdom, $"no Kingdom id=\"{HuntTroopLists.DolGuldurKingdomId}\" in taom_spkingdoms.xml");
        Assert.AreEqual("Hero." + HuntTroopLists.KhamulId, (string?)kingdom!.Attribute("owner"));
    }
}
