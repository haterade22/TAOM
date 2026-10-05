using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

// Mike, 2026-10-04: the Mouth of Sauron's helm gives 50 head armour. The item lives in the UNVERSIONED
// LOTRLOME_Armory, so an Armory reinstall from any package built before 2026-10-04 brings back 55, and nothing else
// reads the value (docs/reference/lotrlome-armory-snapshot/README.md, "APPLIED EDIT: the Mouth of Sauron's helm").
// This is a change detector by design: when the helm is retuned, change the expected value here in the same change.
// The live test is Inconclusive where the Armory is not installed.

namespace TAOM.Tests.Features.ArmourAcquisition;

[TestClass]
public class MouthOfSauronHelmDataTests
{
    private const string HelmId = "sk_mordor_mouth_of_sauron_helm";

    private static string LiveHeadArmorsXml()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        string game = string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env;
        string path = Path.Combine(game, "Modules", "LOTRLOME_Armory", "ModuleData", "LOTRLOME_items", "mordor",
            "head_armors.xml");
        if (!File.Exists(path))
            Assert.Inconclusive("LOTRLOME_Armory not installed on this machine; the live item cannot be checked here.");
        return path;
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void LiveHelm_AfterAnArmoryUpdate_KeepsArmour50AndItsFlags()
    {
        var item = XDocument.Load(LiveHeadArmorsXml()).Descendants("Item")
            .SingleOrDefault(i => (string?)i.Attribute("id") == HelmId);

        Assert.IsNotNull(item, $"{HelmId} is missing from the live Armory: both of lord_1_14's rosters name it");
        var armor = item!.Element("ItemComponent")?.Element("Armor");
        Assert.IsNotNull(armor, $"{HelmId} has no <Armor> component");
        Assert.AreEqual("50", (string?)armor!.Attribute("head_armor"),
            "the helm's head armour reverted: an Armory package from before 2026-10-04 carries 55");
        Assert.AreEqual("true", (string?)armor.Attribute("covers_head"), "covers_head hides the head the helm replaces");
        Assert.AreEqual("false", (string?)item.Attribute("is_merchandise"), "the helm is never sold");
        Assert.AreEqual("true", (string?)item.Element("Flags")?.Attribute("Civilian"), "the Civilian flag is gone");
    }

    [TestMethod]
    public void ClassTable_MouthOfSauronHelm_IsNamed()
    {
        string path = RepoPaths.RepoPath("Main", "_Module", "ModuleData", "armour_acquisition", "armour_classes.xml");
        var row = XDocument.Load(path).Descendants("Item").SingleOrDefault(i => (string?)i.Attribute("id") == HelmId);

        Assert.IsNotNull(row, $"armour_classes.xml has no row for {HelmId}: re-run tools/generate_armour_classes.py --apply");
        Assert.AreEqual("named", (string?)row!.Attribute("class"),
            "the helm must stay named (never sold, looted or awarded)");
    }
}
