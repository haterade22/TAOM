using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.MountReins;

/// <summary>
/// The premise of the mount reins guard (Patch153) in the installed data: the Armory's creature mounts that the player
/// can saddle have no rein skeleton, and vanilla harnesses with reins share their family_type, so the inventory lets a
/// player put one on. If the Armory ever gives these Monsters a rein skeleton, the first test fails and the guard's
/// doc needs a second look.
/// </summary>
[TestClass]
public class MountReinsLiveDataTests
{
    private static readonly string[] NoReinSkeletonMounts = { "warg", "fell_warg", "dire_wolf", "spider" };

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void LiveArmory_CreatureMounts_HaveNoReinSkeleton_AndFamilyType1()
    {
        var monsters = ArmoryMonsters();
        foreach (var id in NoReinSkeletonMounts)
        {
            Assert.IsTrue(monsters.TryGetValue(id, out var m), $"Monster '{id}' not found in the live Armory");
            Assert.IsNull(m.Attribute("rein_skeleton"), $"'{id}' now has a rein skeleton");
            Assert.IsNull(m.Attribute("base_monster"), $"'{id}' now inherits from another Monster");
            Assert.AreEqual("1", (string?)m.Attribute("family_type"), $"'{id}' left harness family 1");
        }
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    [DataRow("light_harness")]
    [DataRow("saddle_of_aeneas")]
    public void LiveSandBoxCore_HorseSaddle_HasReins_AndFitsFamily1(string id)
    {
        // light_harness: the 2026-11-15 player's harness. saddle_of_aeneas: the one Rivendell's troops ride with.
        var path = Path.Combine(GameDir(), "Modules", "SandBoxCore", "ModuleData", "items", "horses_and_others.xml");
        if (!File.Exists(path)) Assert.Inconclusive("SandBoxCore items not found on this machine.");
        var item = XDocument.Load(path).Descendants("Item").Single(i => (string?)i.Attribute("id") == id);
        var armor = item.Element("ItemComponent")!.Element("Armor")!;

        Assert.IsFalse(string.IsNullOrEmpty((string?)armor.Attribute("reins_mesh")), "the saddle carries reins");
        Assert.AreEqual("1", (string?)armor.Attribute("family_type"));
    }

    private static Dictionary<string, XElement> ArmoryMonsters()
    {
        var dir = Path.Combine(GameDir(), "Modules", "LOTRLOME_Armory", "ModuleData");
        if (!Directory.Exists(dir)) Assert.Inconclusive("LOTRLOME_Armory not installed on this machine; the live data cannot be checked here.");
        var result = new Dictionary<string, XElement>(StringComparer.Ordinal);
        var files = new List<string> { Path.Combine(dir, "monsters.xml") };
        var lotr = Path.Combine(dir, "Monsters", "LOTR");
        if (Directory.Exists(lotr)) files.AddRange(Directory.GetFiles(lotr, "*.xml"));
        foreach (var f in files.Where(File.Exists))
            foreach (var m in XDocument.Load(f).Descendants("Monster"))
                if ((string?)m.Attribute("id") is { } id)
                    result[id] = m;
        return result;
    }

    private static string GameDir()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        return string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env!;
    }
}
