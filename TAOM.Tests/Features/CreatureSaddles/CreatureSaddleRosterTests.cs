using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CreatureSaddles;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.CreatureSaddles;

/// <summary>
/// After Patch154 every troop and hero roster in TAOM's data must still pair a creature mount with a harness of the same
/// family (or none), or the soldier would ride with a harness the inventory refuses. Reads TAOM's ModuleData from the
/// repo and the mount items and Monsters from the live Armory (LiveInstall).
/// </summary>
[TestClass]
public class CreatureSaddleRosterTests
{
    [TestMethod]
    [TestCategory("LiveInstall")]
    public void EveryRoster_PairsACreatureMountWithAHarnessOfItsFamily()
    {
        var armory = ArmoryModuleData();
        var mountMonster = new Dictionary<string, string>(StringComparer.Ordinal);
        var harnessFamily = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var f in Directory.GetFiles(Path.Combine(armory, "LOTRLOME_items"), "*.xml"))
            foreach (var item in XDocument.Load(f).Descendants("Item"))
            {
                var id = (string?)item.Attribute("id");
                if (id == null) continue;
                var horse = item.Element("ItemComponent")?.Element("Horse");
                if (horse != null)
                    mountMonster[id] = ((string?)horse.Attribute("monster") ?? "").Replace("Monster.", "");
                var armor = item.Element("ItemComponent")?.Element("Armor");
                if (armor != null && (string?)item.Attribute("Type") == "HorseHarness")
                    harnessFamily[id] = CreatureSaddleFamilies.HarnessFamily(id) ?? int.Parse((string?)armor.Attribute("family_type") ?? "0");
            }
        Assert.IsTrue(mountMonster.ContainsKey("dire_wolf_brown"), "the Armory's dire wolves were not found");

        var bad = new List<string>();
        int checkedPairs = 0;
        var data = RepoPaths.RepoPath("Main", "_Module", "ModuleData");
        foreach (var f in Directory.GetFiles(data, "*.xml", SearchOption.AllDirectories))
        {
            if (f.Contains(Path.DirectorySeparatorChar + "Languages" + Path.DirectorySeparatorChar)) continue;
            XDocument doc;
            try { doc = XDocument.Load(f); } catch { continue; }
            foreach (var set in doc.Descendants().Where(e => e.Name.LocalName is "EquipmentRoster" or "EquipmentSet"))
            {
                var slots = set.Elements("equipment").GroupBy(e => (string?)e.Attribute("slot") ?? "")
                    .ToDictionary(g => g.Key, g => ((string?)g.Last().Attribute("id") ?? "").Replace("Item.", ""));
                if (!slots.TryGetValue("Horse", out var mount) || !mountMonster.TryGetValue(mount, out var monster)) continue;
                var mountFamily = CreatureSaddleFamilies.MonsterFamily(monster);
                if (mountFamily == null || !slots.TryGetValue("HorseHarness", out var harness) || string.IsNullOrEmpty(harness)) continue;
                checkedPairs++;
                if (!harnessFamily.TryGetValue(harness, out var hf) || hf != mountFamily)
                    bad.Add($"{Path.GetFileName(f)}: {mount} ({monster}, family {mountFamily}) with {harness} (family {(harnessFamily.TryGetValue(harness, out var x) ? x : -1)})");
            }
        }

        Assert.IsTrue(checkedPairs > 50, $"only {checkedPairs} creature mount + harness pairs found: the reader missed the rosters");
        Assert.AreEqual(0, bad.Count, string.Join("\n", bad.Distinct()));
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void LiveArmory_StillGivesTheCreatureRigsFamily1_SoTheOverrideIsNeeded()
    {
        var dir = Path.Combine(ArmoryModuleData(), "Monsters", "LOTR");
        var monsters = Directory.GetFiles(dir, "*.xml").SelectMany(f => XDocument.Load(f).Descendants("Monster"))
            .ToDictionary(m => (string)m.Attribute("id")!, m => (string?)m.Attribute("family_type"));
        foreach (var id in CreatureSaddleFamilies.Monsters.Keys)
        {
            Assert.IsTrue(monsters.ContainsKey(id), $"Monster '{id}' not found in the live Armory");
            Assert.IsTrue(monsters[id] == "1" || monsters[id] == CreatureSaddleFamilies.Monsters[id].ToString(),
                $"'{id}' has family {monsters[id]} in the Armory: neither the horse family nor TAOM's");
        }
    }

    private static string ArmoryModuleData()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        string game = string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env!;
        string dir = Path.Combine(game, "Modules", "LOTRLOME_Armory", "ModuleData");
        if (!Directory.Exists(dir))
            Assert.Inconclusive("LOTRLOME_Armory not installed on this machine; the live data cannot be checked here.");
        return dir;
    }
}
