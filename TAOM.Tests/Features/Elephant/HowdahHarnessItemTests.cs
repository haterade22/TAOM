using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Elephant;

namespace TAOM.Tests.Features.Elephant;

/// <summary>
/// The war elephant's body and its six HorseHarness items, KEYforce's art of 2026-09-29 (#627 for the howdah): three
/// plain armours with no howdah (sk_elephant_armor_a, _heavy, _elite) and three howdahs on one deck placement
/// (sk_elephant_armor_howdah_med, _heavy, _elite), each of which gets the platform and its crew. The Armory items and
/// their name rows live outside git, so those checks are the in-repo gate a module reinstall would trip (tagged
/// LiveInstall; Inconclusive on a machine without the Armory). The troop binding reads only the repo, so it runs on CI.
/// </summary>
[TestClass]
public class HowdahHarnessItemTests
{
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

    private static XElement HorsesItem(string itemId)
    {
        string horses = Path.Combine(ArmoryModuleData(), "LOTRLOME_items", "LOTRAOM_horses.xml");
        var items = XDocument.Load(horses).Descendants("Item")
            .Where(i => (string?)i.Attribute("id") == itemId).ToList();
        Assert.AreEqual(1, items.Count, $"LOTRAOM_horses.xml must define {itemId} exactly once");
        return items[0];
    }

    [DataTestMethod]
    [TestCategory("LiveInstall")]
    [DataRow("sk_elephant_armor_a", "sk_hd_elep_armor_med_a", DisplayName = "plain medium armour, no howdah")]
    [DataRow("sk_elephant_armor_heavy", "sk_hd_elep_armor_heavy_a", DisplayName = "plain heavy armour, no howdah")]
    [DataRow("sk_elephant_armor_elite", "sk_hd_elep_armor_elite_a", DisplayName = "plain elite armour, no howdah")]
    [DataRow("sk_elephant_armor_howdah_med", "sk_hd_elep_armor_howdah_med_a", DisplayName = "medium howdah")]
    [DataRow("sk_elephant_armor_howdah_heavy", "sk_hd_elep_armor_howdah_heavy_a", DisplayName = "heavy howdah")]
    [DataRow("sk_elephant_armor_howdah_elite", "sk_hd_elep_armor_howdah_elite_a", DisplayName = "elite howdah")]
    public void TheArmory_DefinesEachHarness_OnceOnItsOwnMesh(string harnessId, string mesh)
    {
        XElement item = HorsesItem(harnessId);
        Assert.AreEqual("HorseHarness", (string?)item.Attribute("Type"));
        Assert.AreEqual(mesh, (string?)item.Attribute("mesh"));
        Assert.AreEqual("10", (string?)item.Descendants("Armor").Single().Attribute("family_type"),
            "family_type 10 is the war elephant's (lotr_monster_elephant.xml), or the harness will not fit the mount");
        StringAssert.StartsWith((string?)item.Attribute("name"), "{=aom_" + harnessId + "}");
    }

    [DataTestMethod]
    [TestCategory("LiveInstall")]
    [DataRow("sk_elephant_armor_a")]
    [DataRow("sk_elephant_armor_heavy")]
    [DataRow("sk_elephant_armor_elite")]
    [DataRow("sk_elephant_armor_howdah_med")]
    [DataRow("sk_elephant_armor_howdah_heavy")]
    [DataRow("sk_elephant_armor_howdah_elite")]
    public void TheHarnessName_HasARowInEnglishAndEveryLanguage(string harnessId)
    {
        string nameKey = "aom_" + harnessId;
        string languages = Path.Combine(ArmoryModuleData(), "Languages");
        foreach (string file in new[] { Path.Combine(languages, "loc_LOTRAOM_horses.xml") }
                     .Concat(Languages.Select(l => Path.Combine(languages, l, "loc_LOTRAOM_horses.xml"))))
        {
            var rows = XDocument.Load(file).Descendants("string").Where(s => (string?)s.Attribute("id") == nameKey).ToList();
            Assert.AreEqual(1, rows.Count, $"{file}: one row for {nameKey}");
            Assert.IsFalse(string.IsNullOrWhiteSpace((string?)rows[0].Attribute("text")), $"{file}: empty text");
        }
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void TheWarElephant_UsesItsBaseMesh()
    {
        Assert.AreEqual("sk_elephant_basemesh_a", (string?)HorsesItem("taom_war_elephant").Attribute("mesh"));
    }

    [TestMethod]
    public void TheHaradElephantRider_WearsExactlyTheThreeHowdahs()
    {
        string troops = Path.Combine(RepoRoot(), "Main", "_Module", "ModuleData", "troops", "troops_harad.xml");
        XElement rider = XDocument.Load(troops).Descendants("NPCCharacter")
            .Single(c => (string?)c.Attribute("id") == "harad_elephant_rider");
        var worn = rider.Descendants("equipment")
            .Where(e => (string?)e.Attribute("slot") == "HorseHarness")
            .Select(e => (string?)e.Attribute("id"))
            .Distinct()
            .ToList();

        CollectionAssert.AreEquivalent(ElephantConfig.HowdahHarnessStringIds.Select(id => "Item." + id).ToList(), worn,
            "the rider's rosters must carry exactly the three howdahs, each of which gets its crew");
    }
}
