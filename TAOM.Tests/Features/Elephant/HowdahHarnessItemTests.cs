using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Elephant;

namespace TAOM.Tests.Features.Elephant;

/// <summary>
/// The visible howdah (#627, Mike 2026-09-19): a HorseHarness item in the unversioned LOTRLOME_Armory binds the elite
/// howdah mesh the platform prefab is fitted to, and the Harad elephant rider wears it. The Armory item and its name
/// rows live outside git, so the live checks are the in-repo gate a module reinstall would trip (Inconclusive on a
/// machine without the Armory); the troop binding is in the repo and always checked.
/// </summary>
[TestClass]
public class HowdahHarnessItemTests
{
    private const string EliteHowdahMesh = "sk_hd_elep_armor_howdah_elite_a";
    private const string NameKey = "aom_" + ElephantConfig.HowdahHarnessStringId;
    private const string DefaultGameDir = @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord";
    private static readonly string[] Languages = { "BR", "CNs", "CNt", "DE", "FR", "IT", "JP", "KO", "PL", "RU", "SP", "TR" };

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new FileNotFoundException("TAOM.sln not found walking upward from cwd");
    }

    private static string ArmoryModuleData()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        string armory = Path.Combine(string.IsNullOrWhiteSpace(env) ? DefaultGameDir : env, "Modules", "LOTRLOME_Armory");
        if (!Directory.Exists(armory))
            Assert.Inconclusive("LOTRLOME_Armory not installed on this machine; the live item cannot be checked here.");
        return Path.Combine(armory, "ModuleData");
    }

    private static XElement HowdahItem()
    {
        string horses = Path.Combine(ArmoryModuleData(), "LOTRLOME_items", "LOTRAOM_horses.xml");
        var items = XDocument.Load(horses).Descendants("Item")
            .Where(i => (string?)i.Attribute("id") == ElephantConfig.HowdahHarnessStringId).ToList();
        Assert.AreEqual(1, items.Count, $"LOTRAOM_horses.xml must define {ElephantConfig.HowdahHarnessStringId} exactly once");
        return items[0];
    }

    [TestMethod]
    public void TheArmory_DefinesTheHowdahHarness_OnTheEliteHowdahMesh()
    {
        XElement item = HowdahItem();
        Assert.AreEqual("HorseHarness", (string?)item.Attribute("Type"));
        Assert.AreEqual(EliteHowdahMesh, (string?)item.Attribute("mesh"), "the platform prefab is fitted to this mesh's deck");
        Assert.AreEqual("10", (string?)item.Descendants("Armor").Single().Attribute("family_type"),
            "family_type 10 is the war elephant's (lotr_monster_elephant.xml), or the harness will not fit the mount");
        StringAssert.StartsWith((string?)item.Attribute("name"), "{=" + NameKey + "}");
    }

    [TestMethod]
    public void TheHowdahHarnessName_HasARowInEnglishAndEveryLanguage()
    {
        string languages = Path.Combine(ArmoryModuleData(), "Languages");
        foreach (string file in new[] { Path.Combine(languages, "loc_LOTRAOM_horses.xml") }
                     .Concat(Languages.Select(l => Path.Combine(languages, l, "loc_LOTRAOM_horses.xml"))))
        {
            var rows = XDocument.Load(file).Descendants("string").Where(s => (string?)s.Attribute("id") == NameKey).ToList();
            Assert.AreEqual(1, rows.Count, $"{file}: one row for {NameKey}");
            Assert.IsFalse(string.IsNullOrWhiteSpace((string?)rows[0].Attribute("text")), $"{file}: empty text");
        }
    }

    [TestMethod]
    public void TheHaradElephantRider_WearsTheHowdahHarness()
    {
        string troops = Path.Combine(RepoRoot(), "Main", "_Module", "ModuleData", "troops", "troops_harad.xml");
        XElement rider = XDocument.Load(troops).Descendants("NPCCharacter")
            .Single(c => (string?)c.Attribute("id") == "harad_elephant_rider");
        var harness = rider.Descendants("equipment").Where(e => (string?)e.Attribute("slot") == "HorseHarness").ToList();
        Assert.IsTrue(harness.Count > 0, "the rider has no HorseHarness slot");
        foreach (var slot in harness)
            Assert.AreEqual("Item." + ElephantConfig.HowdahHarnessStringId, (string?)slot.Attribute("id"),
                "the rider's elephant should carry the visible howdah (and its crew)");
    }
}
