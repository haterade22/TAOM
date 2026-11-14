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
/// The vanilla SandBox lords rebuilt by lords.xslt (Sauron, Boromir, Théoden and the rest) exist in a Custom Battle
/// only through characters/custom_battle_lords.xml. SandBox
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
        // The lords node has no lords.xml, only lords.xslt, so it can look like dead config; without CustomGame
        // the stubs would load bare (no name, no face: toddlers) and still fill the curated lists.
        CollectionAssert.Contains(GameTypes(nodes[lordsIndex]), "CustomGame",
            "The lords node must load for CustomGame, or lords.xslt never rebuilds the stubs.");
    }

    [TestMethod]
    public void EveryStub_IsRebuiltByLordsXslt_IntoAFullCharacter()
    {
        var transform = CompiledXslt.Get(Path.Combine(ModuleData(), "lords.xslt"), XsltLoadStyle.FrameworkDefaults);
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
    public void EveryStub_HasAWholeLordTemplate_AndNoLordsXmlRow()
    {
        // Since #648 a vanilla-id lord has no characters/lords.xml row, so a stub is its only way into a Custom Battle.
        var templates = WholeLordTemplateIds(XDocument.Load(Path.Combine(ModuleData(), "lords.xslt")));
        Assert.IsTrue(templates.Count > 300, $"only {templates.Count} whole-lord templates found; the reader no longer matches lords.xslt");
        var lordsXmlIds = XDocument.Load(Path.Combine(ModuleData(), "characters", "lords.xml"))
            .Descendants("NPCCharacter").Select(c => (string)c.Attribute("id")).ToList();

        var problems = StubRuleViolations(StubIds(), templates, lordsXmlIds);

        Assert.AreEqual(0, problems.Count, string.Join("; ", problems));
    }

    [TestMethod]
    public void StubRule_StubWithNoTemplate_IsAViolation()
    {
        var problems = StubRuleViolations(new[] { "lord_X_1" }, new HashSet<string> { "lord_Y_1" }, new string[0]);

        Assert.AreEqual(1, problems.Count);
        StringAssert.Contains(problems[0], "lord_X_1");
    }

    [TestMethod]
    public void StubRule_StubWithALordsXmlRow_IsAViolation()
    {
        var problems = StubRuleViolations(new[] { "lord_X_1" }, new HashSet<string> { "lord_X_1" }, new[] { "lord_X_1" });

        Assert.AreEqual(1, problems.Count);
        StringAssert.Contains(problems[0], "characters/lords.xml");
    }

    [TestMethod]
    public void StubRule_StubWithATemplateAndNoRow_IsAllowed()
    {
        var problems = StubRuleViolations(new[] { "lord_X_1" }, new HashSet<string> { "lord_X_1" }, new[] { "lord_Y_1" });

        Assert.AreEqual(0, problems.Count, string.Join("; ", problems));
    }

    [TestMethod]
    public void WholeLordTemplateIds_TemplatePassingVanillaAttributesThrough_IsNotAWholeLord()
    {
        // A template that keeps the vanilla attributes (@*) only patches a lord, so on a bare stub it would build none.
        const string xsl = "http://www.w3.org/1999/XSL/Transform";
        var x = XNamespace.Get(xsl);
        var doc = new XDocument(new XElement(x + "stylesheet",
            new XElement(x + "template", new XAttribute("match", "NPCCharacter[@id='lord_whole']"),
                new XElement(x + "copy", new XElement(x + "attribute", new XAttribute("name", "id"), "lord_whole"))),
            new XElement(x + "template", new XAttribute("match", "NPCCharacter[@id='lord_patch']"),
                new XElement(x + "copy", new XElement(x + "apply-templates", new XAttribute("select", "@*|node()"))))));

        CollectionAssert.AreEquivalent(new[] { "lord_whole" }, WholeLordTemplateIds(doc).ToList());
    }

    /// <summary>
    /// The ids lords.xslt rebuilds as a whole lord: a <c>NPCCharacter[@id='x']</c> template whose xsl:copy writes the
    /// id itself and passes no vanilla attribute through, so it turns a bare stub into the full character.
    /// </summary>
    internal static HashSet<string> WholeLordTemplateIds(XDocument stylesheet)
    {
        XNamespace x = "http://www.w3.org/1999/XSL/Transform";
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var template in stylesheet.Descendants(x + "template"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                (string)template.Attribute("match") ?? "", @"^NPCCharacter\[@id='([^']+)'\]$");
            var copy = template.Element(x + "copy");
            if (!match.Success || copy == null) continue;
            var writesId = copy.Elements(x + "attribute").Any(a => (string)a.Attribute("name") == "id");
            var keepsVanillaAttributes = copy.Descendants(x + "apply-templates")
                .Any(a => ((string)a.Attribute("select") ?? "").Contains("@*"));
            if (writesId && !keepsVanillaAttributes) ids.Add(match.Groups[1].Value);
        }
        return ids;
    }

    /// <summary>
    /// The stub rule: an id may have a stub only when lords.xslt rebuilds it as a whole lord (else the stub loads bare)
    /// and characters/lords.xml has no row for it (else the id is a TAOM lord that already loads, or a double definition).
    /// </summary>
    internal static List<string> StubRuleViolations(
        IEnumerable<string> stubIds, ISet<string> wholeLordTemplateIds, IEnumerable<string> lordsXmlIds)
    {
        var rows = new HashSet<string>(lordsXmlIds, StringComparer.OrdinalIgnoreCase);
        var problems = new List<string>();
        foreach (var id in stubIds)
        {
            if (!wholeLordTemplateIds.Contains(id))
                problems.Add($"stub '{id}' has no whole-lord template in lords.xslt, so it would load bare");
            if (rows.Contains(id))
                problems.Add($"stub '{id}' also has a row in characters/lords.xml");
        }
        return problems;
    }
}
