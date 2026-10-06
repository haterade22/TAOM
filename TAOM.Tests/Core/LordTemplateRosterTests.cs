using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Xsl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// Generated lords of the six renamed vanilla cultures must draw TAOM gear, not Calradic.
///
/// <para>
/// Vanilla <c>DefaultEquipmentSelectionModel.GetSuitableEquipmentSet</c> (v1.5.4) pools every loaded
/// <c>EquipmentRoster</c> whose culture is the hero's and whose <c>&lt;Flags&gt;</c> equal the requested
/// categories exactly, then picks one set at random. SandBoxCore's own <c>emp_*</c>, <c>vla_*</c>...
/// lord and ruler templates stay loaded, so before <c>lord_template_rosters.xslt</c> a generated
/// Dunland lord drew from 26 Empire sets and one Dunland set. The stylesheet removes the lord and
/// ruler flags from those vanilla rosters; child templates keep theirs, because TAOM ships child
/// templates for Khand only and an empty pool hands the hero null equipment.
/// </para>
///
/// <para>
/// Sentinel-stub shape (<see cref="CulturePartyTemplateTests"/>): the stylesheet runs over a synthetic
/// roster document, and the coverage test reads the repo's own rosters, so neither needs the install.
/// </para>
/// </summary>
[TestClass]
public class LordTemplateRosterTests
{
    private static readonly string[] XsltCultures = { "vlandia", "empire", "aserai", "khuzait", "sturgia", "battania" };

    private static string ModuleData() => CultureDataFixture.ModuleDataPath();

    private static XslCompiledTransform LoadTransform()
    {
        var transform = new XslCompiledTransform();
        transform.Load(Path.Combine(ModuleData(), "lord_template_rosters.xslt"));
        return transform;
    }

    private static XDocument Apply(XslCompiledTransform transform, XDocument input)
    {
        var output = new XDocument();
        using (var writer = output.CreateWriter())
            transform.Transform(input.CreateReader(), null, writer);
        return output;
    }

    private static XElement Roster(string id, string culture, params string[] flags) =>
        new XElement("EquipmentRoster",
            new XAttribute("id", id),
            new XAttribute("culture", "Culture." + culture),
            new XElement("EquipmentSet",
                new XElement("Equipment", new XAttribute("slot", "Body"), new XAttribute("id", "Item.SENTINEL_" + id))),
            new XElement("Flags", flags.Select(f => new XAttribute(f, "true"))));

    private static HashSet<string> FlagsOf(XDocument doc, string id) =>
        new HashSet<string>(
            doc.Descendants("EquipmentRoster").Single(r => (string)r.Attribute("id") == id)
               .Elements("Flags").Attributes().Where(a => a.Value == "true").Select(a => a.Name.LocalName),
            StringComparer.Ordinal);

    [TestMethod]
    public void Transform_VanillaLordAndRulerTemplatesOfXsltCultures_LoseTheirTemplateFlags()
    {
        var stub = new XDocument(new XElement("EquipmentRosters",
            XsltCultures.Select(c => Roster("vanilla_lord_" + c, c, "IsLordTemplate")),
            Roster("vanilla_lady", "empire", "IsLordTemplate", "IsFemaleTemplate"),
            Roster("vanilla_teen", "empire", "IsLordTemplate", "IsTeenagerEquipmentTemplate"),
            Roster("vanilla_ruler", "empire", "IsKingdomRulerTemplate", "IsFemaleTemplate")));

        var output = Apply(LoadTransform(), stub);

        foreach (var c in XsltCultures)
            Assert.AreEqual(0, FlagsOf(output, "vanilla_lord_" + c).Count,
                $"vanilla_lord_{c} still carries a template flag, so generated {c} lords can still roll it.");
        CollectionAssert.AreEquivalent(new[] { "IsFemaleTemplate" }, FlagsOf(output, "vanilla_lady").ToList());
        CollectionAssert.AreEquivalent(new[] { "IsTeenagerEquipmentTemplate" }, FlagsOf(output, "vanilla_teen").ToList());
        CollectionAssert.AreEquivalent(new[] { "IsFemaleTemplate" }, FlagsOf(output, "vanilla_ruler").ToList());
    }

    [TestMethod]
    public void Transform_TaomRostersChildTemplatesAndOtherCultures_KeepTheirFlags()
    {
        var stub = new XDocument(new XElement("EquipmentRosters",
            Roster("taom_empire_lord_battle_male", "empire", "IsLordTemplate"),
            Roster("taom_empire_ruler_battle_female", "empire", "IsKingdomRulerTemplate", "IsFemaleTemplate"),
            Roster("vanilla_child", "empire", "IsLordTemplate", "IsChildEquipmentTemplate"),
            Roster("vanilla_nord_lord", "nord", "IsLordTemplate")));

        var output = Apply(LoadTransform(), stub);

        CollectionAssert.AreEquivalent(new[] { "IsLordTemplate" }, FlagsOf(output, "taom_empire_lord_battle_male").ToList());
        CollectionAssert.AreEquivalent(new[] { "IsKingdomRulerTemplate", "IsFemaleTemplate" },
            FlagsOf(output, "taom_empire_ruler_battle_female").ToList());
        CollectionAssert.AreEquivalent(new[] { "IsLordTemplate", "IsChildEquipmentTemplate" },
            FlagsOf(output, "vanilla_child").ToList(),
            "Child templates must keep their flags: TAOM ships none for five of the six cultures.");
        CollectionAssert.AreEquivalent(new[] { "IsLordTemplate" }, FlagsOf(output, "vanilla_nord_lord").ToList());
        Assert.AreEqual("Item.SENTINEL_vanilla_child",
            (string)output.Descendants("Equipment").Single(e => ((string)e.Attribute("id")).EndsWith("vanilla_child")).Attribute("id"),
            "The stylesheet must copy roster contents through untouched.");
    }

    [TestMethod]
    public void SubModule_LordTemplateStylesheet_IsTheLastEquipmentRostersNode()
    {
        // MBObjectManager.CreateMergedXmlFile applies a node's stylesheet only to the entries merged BEFORE it,
        // so a roster node registered below this one would keep vanilla-style template flags unseen.
        var subModule = XDocument.Load(Path.Combine(ModuleData(), "..", "SubModule.xml"));
        var rosterNodes = subModule.Descendants("XmlName")
            .Where(x => (string)x.Attribute("id") == "EquipmentRosters")
            .Select(x => (string)x.Attribute("path"))
            .ToList();

        Assert.AreEqual("lord_template_rosters", rosterNodes.LastOrDefault(),
            "lord_template_rosters must stay the last EquipmentRosters node in SubModule.xml. Nodes after it: "
            + string.Join(", ", rosterNodes.SkipWhile(p => p != "lord_template_rosters").Skip(1)));
    }

    [TestMethod]
    public void TaomLords_NoNamedRosterSharesALordId()
    {
        // LordKitDonorAdapter reads a lord's own kit as the roster BasicCharacterObject.Deserialize registers under
        // the lord's id. A named roster with that id would make the engine rename the lord's roster, and the lookup
        // would return the named one instead.
        var rosterIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(Path.Combine(ModuleData(), "equipmentsets"), "*.xml"))
            foreach (var roster in XDocument.Load(file).Descendants("EquipmentRoster"))
                rosterIds.Add((string)roster.Attribute("id") ?? string.Empty);

        var clashes = XDocument.Load(Path.Combine(ModuleData(), "characters", "lords.xml"))
            .Descendants("NPCCharacter")
            .Select(c => (string)c.Attribute("id"))
            .Where(id => id != null && rosterIds.Contains(id))
            .ToList();

        Assert.AreEqual(0, clashes.Count, "Lord ids that a named EquipmentRoster also uses: " + string.Join(", ", clashes));
    }

    [TestMethod]
    public void TaomRosters_AfterTransform_FillEveryPoolTheEngineRequests()
    {
        // The requests DefaultEquipmentSelectionModel makes for a lord, teenager or ruler; female adds IsFemaleTemplate.
        var requests = new List<(string Flags, string SetType)>();
        foreach (var female in new[] { "", "+IsFemaleTemplate" })
        {
            requests.Add(("IsLordTemplate" + female, "Battle"));
            requests.Add(("IsLordTemplate" + female, "Civilian"));
            requests.Add(("IsLordTemplate+IsTeenagerEquipmentTemplate" + female, "Civilian"));
            requests.Add(("IsKingdomRulerTemplate" + female, "Battle"));
            requests.Add(("IsKingdomRulerTemplate" + female, "Civilian"));
        }
        static string Key(IEnumerable<string> flags) => string.Join("+", flags.OrderBy(f => f, StringComparer.Ordinal));

        var transform = LoadTransform();
        var pool = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(Path.Combine(ModuleData(), "equipmentsets"), "*.xml"))
        {
            foreach (var roster in Apply(transform, XDocument.Load(file)).Descendants("EquipmentRoster"))
            {
                var flags = Key(roster.Elements("Flags").Attributes().Where(a => a.Value == "true").Select(a => a.Name.LocalName));
                foreach (var set in roster.Elements("EquipmentSet"))
                    pool.Add($"{(string)roster.Attribute("culture")}|{flags}|{(string)set.Attribute("equipmentType") ?? "Battle"}");
            }
        }

        var empty = (from c in XsltCultures
                     from r in requests
                     let key = $"Culture.{c}|{Key(r.Flags.Split('+'))}|{r.SetType}"
                     where !pool.Contains(key)
                     select key).ToList();

        Assert.AreEqual(0, empty.Count,
            "With vanilla's templates stripped, these pools hold no TAOM set, so the engine would hand the hero null equipment:"
            + Environment.NewLine + string.Join(Environment.NewLine, empty));
    }
}
