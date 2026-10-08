using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// lords.xslt retags every wanderer template merged before TAOM's `lords` NPCCharacters entry (SandBox's
/// 67 Calradian wanderers in spspecialcharacters.xml) to occupation NotAssigned, so the engine stops
/// spawning them in Middle-earth (#758).
///
/// Why: CompanionsCampaignBehavior.InitializeCompanionTemplateList (v1.5.4 :344-353) takes every loaded
/// CharacterObject with IsTemplate and Occupation.Wanderer, from every module, not the culture template
/// lists spcultures.xslt rewrites. A 2026-10-07 session created 11 vanilla wanderers out of 50.
///
/// Retag, never delete: a hero cloned from a vanilla template saves a reference to it
/// (CharacterObject._originCharacter), and InitializeHeroCharacterOnAfterLoad dereferences it unguarded.
/// Such a hero keeps its saved occupation (Hero.Occupation), so an existing save keeps the vanilla
/// wanderers it already has; the spawner no longer knows their template, so it never culls them and does
/// not count them. New campaigns get none. TAOM's own wanderers merge after lords.xslt runs, which the
/// load-order test below pins.
/// </summary>
[TestClass]
public class VanillaWandererTemplateTransformTests
{
    private const string DefaultGameDir = @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord";

    private static string LordsXslt => Path.Combine(CultureDataFixture.ModuleDataPath(), "lords.xslt");

    /// <summary>Applies the stylesheet the way MBObjectManager.ApplyXslt does.</summary>
    private static XDocument Transform(XmlReader input)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
        var xslt = new XslCompiledTransform();
        using (var sheet = XmlReader.Create(LordsXslt, settings))
            xslt.Load(sheet, new XsltSettings(false, false), null);

        var result = new XDocument();
        using (var output = result.CreateWriter())
            xslt.Transform(input, output);
        return result;
    }

    private static XElement Character(XDocument doc, string id) =>
        doc.Descendants("NPCCharacter").Single(c => (string)c.Attribute("id") == id);

    [TestMethod]
    public void LordsXslt_VanillaWandererTemplate_IsRetaggedNotAssigned()
    {
        const string stub = @"<NPCCharacters>
  <NPCCharacter id=""spc_wanderer_khuzait_0"" is_template=""true"" occupation=""Wanderer"" culture=""Culture.khuzait"" voice=""curt"" />
  <NPCCharacter id=""spc_notable_khuzait_0"" is_template=""true"" occupation=""Merchant"" culture=""Culture.khuzait"" />
  <NPCCharacter id=""some_hero_wanderer"" occupation=""Wanderer"" culture=""Culture.khuzait"" />
</NPCCharacters>";

        using var reader = XmlReader.Create(new StringReader(stub));
        var doc = Transform(reader);

        var wanderer = Character(doc, "spc_wanderer_khuzait_0");
        Assert.AreEqual("NotAssigned", (string)wanderer.Attribute("occupation"), "vanilla wanderer template still spawnable");
        Assert.AreEqual("true", (string)wanderer.Attribute("is_template"), "the template must stay a template (saves reference it)");
        Assert.AreEqual("curt", (string)wanderer.Attribute("voice"), "other attributes must pass through");
        Assert.AreEqual("Merchant", (string)Character(doc, "spc_notable_khuzait_0").Attribute("occupation"), "a notable template is not a wanderer");
        Assert.AreEqual("Wanderer", (string)Character(doc, "some_hero_wanderer").Attribute("occupation"), "a non-template character is not in the spawn pool");
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void LordsXslt_InstalledSandBoxSpecialCharacters_LeavesNoWandererTemplate()
    {
        var root = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        var file = new[] { root, DefaultGameDir }
            .Where(r => !string.IsNullOrEmpty(r))
            .Select(r => Path.Combine(r!, "Modules", "SandBox", "ModuleData", "spspecialcharacters.xml"))
            .FirstOrDefault(File.Exists);
        if (file == null)
            Assert.Inconclusive("SandBox spspecialcharacters.xml not found; needs the game install.");

        static bool IsSpawnable(XElement c) =>
            string.Equals((string)c.Attribute("is_template"), "true", StringComparison.OrdinalIgnoreCase)
            && (string)c.Attribute("occupation") == "Wanderer";

        var before = XDocument.Load(file!).Descendants("NPCCharacter").Count(IsSpawnable);
        Assert.IsTrue(before > 0, "the vanilla file has no wanderer templates: the check tested nothing");

        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
        using var reader = XmlReader.Create(file!, settings);
        var after = Transform(reader).Descendants("NPCCharacter").Where(IsSpawnable)
            .Select(c => (string)c.Attribute("id")).ToList();

        Assert.AreEqual(0, after.Count, $"{after.Count} of {before} vanilla wanderer templates still spawnable:\n" + string.Join("\n", after));
    }

    [TestMethod]
    public void SubModule_NpcCharactersEntriesBeforeLordsXslt_HoldNoWanderer()
    {
        // The engine applies lords.xslt to every NPCCharacters entry merged before it (MBObjectManager
        // CreateMergedXmlFile), so a TAOM wanderer file registered above `lords` would be retagged too and
        // no TAOM wanderer would ever spawn, silently.
        var moduleData = CultureDataFixture.ModuleDataPath();
        var entries = XDocument.Load(Path.Combine(moduleData, "..", "SubModule.xml"))
            .Descendants("XmlName")
            .Where(x => (string)x.Attribute("id") == "NPCCharacters")
            .Select(x => (string)x.Attribute("path"))
            .ToList();

        var lords = entries.IndexOf("lords");
        Assert.IsTrue(lords >= 0, "no NPCCharacters entry with path=\"lords\": the stylesheet is not registered");
        Assert.IsTrue(entries.IndexOf("taom_wanderers") > lords, "taom_wanderers must be registered after the lords stylesheet");

        var early = entries.Take(lords)
            .Select(p => Path.Combine(moduleData, p + ".xml"))
            .Where(File.Exists)
            .SelectMany(f => XDocument.Load(f).Descendants("NPCCharacter")
                .Where(c => (string)c.Attribute("occupation") == "Wanderer")
                .Select(c => $"{(string)c.Attribute("id")} ({Path.GetFileName(f)})"))
            .ToList();

        Assert.AreEqual(0, early.Count, "wanderers registered before lords.xslt would be retagged:\n" + string.Join("\n", early));
    }
}
