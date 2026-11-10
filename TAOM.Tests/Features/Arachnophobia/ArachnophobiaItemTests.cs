using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Arachnophobia;

namespace TAOM.Tests.Features.Arachnophobia;

/// <summary>
/// The wargs and the harness live in the unversioned LOTRLOME_Armory, so a module reinstall can remove one. The mode
/// changes nothing when an item is missing, so this gate reports it first. Inconclusive on a machine without the Armory.
/// </summary>
[TestClass]
[TestCategory("LiveInstall")]
public class ArachnophobiaItemTests
{
    private const string DefaultGameDir = @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord";

    [TestMethod]
    public void EveryRequiredItem_ExistsInTheArmoryHorsesFile()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        string armory = Path.Combine(string.IsNullOrWhiteSpace(env) ? DefaultGameDir : env, "Modules", "LOTRLOME_Armory");
        if (!Directory.Exists(armory))
            Assert.Inconclusive("LOTRLOME_Armory not installed on this machine; the live items cannot be checked here.");

        string horses = Path.Combine(armory, "ModuleData", "LOTRLOME_items", "LOTRAOM_horses.xml");
        var ids = new HashSet<string?>(XDocument.Load(horses).Descendants("Item").Select(i => (string?)i.Attribute("id")));

        foreach (string id in ArachnophobiaRules.RequiredItemIds)
            Assert.IsTrue(ids.Contains(id), $"LOTRAOM_horses.xml must define {id}: Arachnophobia Mode swaps spider riders onto it");
    }
}
