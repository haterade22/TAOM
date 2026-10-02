using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Xsl;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Core;

namespace TAOM.Tests.Features.CustomBattles;

/// <summary>
/// The curated Custom Battle commanders that are vanilla SandBox lords rebuilt by lords.xslt (Sauron, Boromir,
/// Théoden and the rest) exist in a Custom Battle only through characters/custom_battle_lords.xml. SandBox
/// registers its lords.xml for Campaign only, so that file supplies a bare stub per id, and the engine applies
/// lords.xslt to it because MBObjectManager.CreateMergedXmlFile (v1.5.3, :962-976) runs each node's XSLT over
/// everything merged before it. These tests pin both halves: the load order, and what the transform makes of
/// each stub.
/// </summary>
[TestClass]
public class CustomBattleLordStubsTests
{
    private const string StubPath = "characters/custom_battle_lords";

    private static string ModuleData() => CultureDataFixture.ModuleDataPath();

    private static XDocument StubFile() =>
        XDocument.Load(Path.Combine(ModuleData(), "characters", "custom_battle_lords.xml"));

    private static List<string> StubIds() =>
        StubFile().Descendants("NPCCharacter").Select(c => (string)c.Attribute("id")).ToList();

    private static List<XElement> XmlNodes() =>
        XDocument.Load(Path.Combine(ModuleData(), "..", "SubModule.xml")).Descendants("XmlNode").ToList();

    private static string PathOf(XElement node) => (string)node.Element("XmlName")?.Attribute("path");

    private static List<string> GameTypes(XElement node) =>
        node.Descendants("GameType").Select(g => (string)g.Attribute("value")).ToList();

    [TestMethod]
    public void StubFile_RegisteredForCustomGameOnly_BeforeTheLordsXsltNode()
    {
        var nodes = XmlNodes();
        var stubIndex = nodes.FindIndex(n => PathOf(n) == StubPath);
        var lordsIndex = nodes.FindIndex(n => PathOf(n) == "lords");

        Assert.IsTrue(stubIndex >= 0, $"SubModule.xml does not register {StubPath}.");
        Assert.IsTrue(lordsIndex >= 0, "SubModule.xml no longer registers the lords node that applies lords.xslt.");
        Assert.IsTrue(stubIndex < lordsIndex,
            "The stub node must come before the lords node: the engine applies a node's XSLT only to what is merged before it.");
        Assert.AreEqual("NPCCharacters", (string)nodes[stubIndex].Element("XmlName")?.Attribute("id"));
        // Campaign loads the real vanilla lords from SandBox; a stub there would duplicate them.
        CollectionAssert.AreEqual(new[] { "CustomGame" }, GameTypes(nodes[stubIndex]));
    }

    [TestMethod]
    public void EveryStub_IsRebuiltByLordsXslt_IntoAFullCharacter()
    {
        var transform = new XslCompiledTransform();
        transform.Load(Path.Combine(ModuleData(), "lords.xslt"));
        var output = new XDocument();
        using (var writer = output.CreateWriter())
            transform.Transform(StubFile().CreateReader(), null, writer);

        var stubIds = StubIds();
        Assert.IsTrue(stubIds.Count > 0, "custom_battle_lords.xml has no stubs.");
        var problems = new List<string>();
        foreach (var id in stubIds)
        {
            var built = output.Descendants("NPCCharacter").SingleOrDefault(c => (string)c.Attribute("id") == id);
            if (built == null) { problems.Add($"{id}: missing from the transform output"); continue; }
            if (string.IsNullOrEmpty((string)built.Attribute("name"))) problems.Add($"{id}: no name (no lords.xslt template?)");
            if ((string)built.Attribute("is_hero") != "true") problems.Add($"{id}: not a hero");
            if (built.Element("face")?.Element("BodyProperties") == null) problems.Add($"{id}: no face");
            if (!built.Descendants("EquipmentSet").Any()) problems.Add($"{id}: no equipment");
        }

        Assert.AreEqual(0, problems.Count, string.Join("; ", problems));
    }

    [TestMethod]
    public void EveryStub_IsACuratedCommander_NotDefinedInLordsXml()
    {
        var config = File.ReadAllText(Path.Combine(ModuleData(), "custom_battle", "custom_battle_commanders.json"));
        var lordsXml = XDocument.Load(Path.Combine(ModuleData(), "characters", "lords.xml"));
        var native = new HashSet<string>(
            lordsXml.Descendants("NPCCharacter").Select(c => (string)c.Attribute("id")), StringComparer.OrdinalIgnoreCase);

        foreach (var id in StubIds())
        {
            // A stub loads a vanilla lord into every Custom Battle; only a curated commander needs one.
            StringAssert.Contains(config, $"\"{id}\"", $"Stub '{id}' is not a curated commander.");
            Assert.IsFalse(native.Contains(id), $"Stub '{id}' duplicates a TAOM lord in characters/lords.xml.");
        }
    }
}
