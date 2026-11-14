using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Xsl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// A vanilla lord is defined once, in its lords.xslt template (#648).
///
/// Mike, 2026-09-23: "If the lords are vanilla they should be in the xslt. If they are created new, they should be in
/// lords.xml." A characters/lords.xml row for an id the stylesheet already rebuilds is merged on top of the template:
/// the row wins per attribute, and its equipment sets with other ids are ADDED to the template's, so each campaign
/// picks one kit at random (Hero.SetInitialValuesFromCharacter). Seven of those rows gave Gondor and Mordor lords a
/// Dunland kit. Their rows are gone; the values the game took from them (age, skill template, face, skills) moved
/// into the templates, and the template's kit is now the only one. The other 169 followed the same way, so no id is
/// both a template and a row any more, and NoLordIsDefinedInBothLordsFiles keeps it so.
///
/// The transform runs for real over a stub (the NazgulRaceDataTests technique), so an id no template matches shows
/// up as missing.
/// </summary>
[TestClass]
public class LordDoubleDefinitionTests
{
    /// <summary>The seven that wore a Dunland kit in about half of all campaigns, with the kit prefix of their side.</summary>
    private static readonly (string Id, string KitPrefix)[] FormerlyCrossFactionKits =
    {
        ("lord_1_45_1", "gondor_"), ("lord_1_57_1", "gondor_"), ("lord_1_57_2", "gondor_"), ("lord_1_73_1", "gondor_"),
        ("lord_1_62_1", "gondor_"), ("lord_1_30_2", "mordor_"), ("lord_1_30_3", "mordor_"),
    };

    private static XElement? Lord(XElement? root, string id)
        => root?.Elements("NPCCharacter").SingleOrDefault(e => (string?)e.Attribute("id") == id);

    private static XDocument TransformStub(params string[] ids)
    {
        var transform = CompiledXslt.Get(Path.Combine(CultureDataFixture.ModuleDataPath(), "lords.xslt"), XsltLoadStyle.FrameworkDefaults);
        var stub = new XElement("NPCCharacters", ids.Select(id => new XElement("NPCCharacter", new XAttribute("id", id))));
        var output = new XDocument();
        using (var writer = output.CreateWriter())
            transform.Transform(new XDocument(stub).CreateReader(), null, writer);
        return output;
    }

    [TestMethod]
    public void NoLordIsDefinedInBothLordsFiles()
    {
        // The #648 gate. Template ids come from the match patterns the stylesheet rebuilds a whole lord with.
        var xslt = File.ReadAllText(Path.Combine(CultureDataFixture.ModuleDataPath(), "lords.xslt"));
        var templateIds = System.Text.RegularExpressions.Regex
            .Matches(xslt, @"<xsl:template match=""NPCCharacter\[@id='([^']+)'\]"">")
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
        Assert.IsTrue(templateIds.Count > 300, $"only {templateIds.Count} lord templates found; the pattern no longer matches lords.xslt");

        var lords = XDocument.Load(Path.Combine(CultureDataFixture.ModuleDataPath(), "characters", "lords.xml"));
        var both = lords.Root!.Elements("NPCCharacter")
            .Select(e => (string?)e.Attribute("id") ?? "")
            .Where(templateIds.Contains)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.AreEqual(0, both.Length,
            "These lords have a lords.xslt template and a characters/lords.xml row. Vanilla lords live in the XSLT only " +
            "(Mike, 2026-09-23): move the values into the template and delete the row (#648):\n  " + string.Join("\n  ", both));
    }

    [TestMethod]
    public void LordsXslt_FormerlyCrossFactionLords_WearOnlyTheirSidesKit()
    {
        var output = TransformStub(FormerlyCrossFactionKits.Select(x => x.Id).ToArray());

        var offenders = FormerlyCrossFactionKits
            .Select(x => (x.Id, x.KitPrefix, sets: Lord(output.Root, x.Id)?.Element("Equipments")?.Elements("EquipmentSet")
                .Select(s => (string?)s.Attribute("id") ?? "").ToArray() ?? Array.Empty<string>()))
            .Where(x => x.sets.Length == 0 || x.sets.Any(s => !s.StartsWith(x.KitPrefix, StringComparison.Ordinal)))
            .Select(x => $"{x.Id}: [{string.Join(", ", x.sets)}] (expected only {x.KitPrefix}*)")
            .ToArray();

        Assert.AreEqual(0, offenders.Length, "A template emits no kit or a kit of another side:\n  " + string.Join("\n  ", offenders));
    }

    [TestMethod]
    public void LordsXslt_FormerlyCrossFactionLords_KeepTheValuesTheirRowsCarried()
    {
        // Without these the template's older values come back (age 53 and 25, the template skill sets).
        var output = TransformStub(FormerlyCrossFactionKits.Select(x => x.Id).ToArray());

        Assert.AreEqual("40", Lord(output.Root, "lord_1_45_1")?.Attribute("age")?.Value, "lord_1_45_1 age");
        Assert.AreEqual("24", Lord(output.Root, "lord_1_57_1")?.Attribute("age")?.Value, "lord_1_57_1 age");
        Assert.AreEqual("SkillSet.spc_matriarch_skills_rookie", Lord(output.Root, "lord_1_57_2")?.Attribute("skill_template")?.Value);
        Assert.AreEqual("SkillSet.spc_chatelaine_skills_rookie", Lord(output.Root, "lord_1_30_2")?.Attribute("skill_template")?.Value);
        Assert.AreEqual("SkillSet.spc_knight_skills_rookie", Lord(output.Root, "lord_1_30_3")?.Attribute("skill_template")?.Value);
    }
}
