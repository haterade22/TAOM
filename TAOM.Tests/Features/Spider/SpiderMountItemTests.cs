using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Spider;

namespace TAOM.Tests.Features.Spider;

/// <summary>
/// The five spider mounts on KEYforce's whole meshes (sk_spiders_a_geo.tpac, 2026-09-29): the Dol Guldur and brood
/// forest tiers (the Giant, Great and Pale Spiders) and the goblin tree's two mountain spiders. The items and their
/// name rows live in the unversioned LOTRLOME_Armory, so this is the in-repo gate a module reinstall would trip: a
/// reverted item goes back to the retired split halves, loses the rename, or drops the mountain mounts that
/// troops_goblin.xml names. Inconclusive on a machine without the Armory.
/// </summary>
[TestClass]
[TestCategory("LiveInstall")]
public class SpiderMountItemTests
{
    private const string DefaultGameDir = @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord";
    private static readonly string[] Languages = { "BR", "CNs", "CNt", "DE", "FR", "IT", "JP", "KO", "PL", "RU", "SP", "TR" };

    private static string ArmoryModuleData()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        string armory = Path.Combine(string.IsNullOrWhiteSpace(env) ? DefaultGameDir : env, "Modules", "LOTRLOME_Armory");
        if (!Directory.Exists(armory))
            Assert.Inconclusive("LOTRLOME_Armory not installed on this machine; the live items cannot be checked here.");
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
    [DataRow("spider_mount_a", "sk_spider_forest_a1", "100", DisplayName = "Giant Spider")]
    [DataRow("spider_mount_brown", "sk_spider_forest_a1", "110", DisplayName = "Great Spider (was Brown)")]
    [DataRow("spider_mount_pale", "sk_spider_forest_a2", "125", DisplayName = "Pale Spider")]
    [DataRow("spider_mount_mountain_a1", "sk_spider_mountain_a1", "100", DisplayName = "Mountain Spider")]
    [DataRow("spider_mount_mountain_a2", "sk_spider_mountain_a2", "125", DisplayName = "Great Mountain Spider")]
    public void EachSpiderMount_RidesItsWholeMeshAtItsTierSize(string itemId, string mesh, string bodyLength)
    {
        XElement item = HorsesItem(itemId);
        XElement horse = item.Descendants("Horse").Single();

        Assert.AreEqual("Horse", (string?)item.Attribute("Type"));
        Assert.AreEqual(mesh, (string?)item.Attribute("mesh"));
        Assert.AreEqual("Monster." + SpiderConfig.SpiderMonsterId, (string?)horse.Attribute("monster"),
            "every spider seam keys on Monster.spider, so a spider item on another Monster loses its bite and mount lock");
        Assert.AreEqual(bodyLength, (string?)horse.Attribute("body_length"), "the tier size lives on body_length");
        Assert.AreEqual(0, item.Descendants("Mesh").Count(),
            "the whole meshes need no AdditionalMeshes half; one here means the retired split came back");
        StringAssert.StartsWith((string?)item.Attribute("name"), "{=" + itemId + "}");
    }

    [DataTestMethod]
    [DataRow("spider_mount_a")]
    [DataRow("spider_mount_brown")]
    [DataRow("spider_mount_pale")]
    [DataRow("spider_mount_mountain_a1")]
    [DataRow("spider_mount_mountain_a2")]
    public void EachSpiderMountName_HasARowInEnglishAndEveryLanguage(string itemId)
    {
        string languages = Path.Combine(ArmoryModuleData(), "Languages");
        foreach (string file in new[] { Path.Combine(languages, "loc_LOTRAOM_horses.xml") }
                     .Concat(Languages.Select(l => Path.Combine(languages, l, "loc_LOTRAOM_horses.xml"))))
        {
            var rows = XDocument.Load(file).Descendants("string").Where(s => (string?)s.Attribute("id") == itemId).ToList();
            Assert.AreEqual(1, rows.Count, $"{file}: one row for {itemId}");
            Assert.IsFalse(string.IsNullOrWhiteSpace((string?)rows[0].Attribute("text")), $"{file}: empty text");
        }
    }
}
