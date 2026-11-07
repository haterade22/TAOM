using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Xsl;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Core;

namespace TAOM.Tests.Features.NazgulFamily;

/// <summary>
/// lord_1_48_1/2/3 are vanilla lord ids, so lords.xslt alone defines them. A characters/lords.xml row
/// is a second definition: on v1.4.8 it wins each attribute it states and its equipment sets union with
/// the template's (MBObjectManager.MergeElements), and Hero.SetInitialValuesFromCharacter picks a kit at
/// random, so each campaign dressed these three in a Nazgul or a generic Mordor kit. The race and ages
/// came only from the rows, so the templates must carry them.
/// </summary>
[TestClass]
public class LesserNazgulDefinitionTests
{
    private static readonly string[] TheThree = { "lord_1_48_1", "lord_1_48_2", "lord_1_48_3" };

    [TestMethod]
    public void LordsXml_DefinesNoneOfTheThree()
    {
        var lords = XDocument.Load(Path.Combine(CultureDataFixture.ModuleDataPath(), "characters", "lords.xml"));

        var duplicates = lords.Descendants("NPCCharacter")
            .Select(e => (string?)e.Attribute("id"))
            .Where(id => TheThree.Contains(id))
            .ToArray();

        Assert.AreEqual(0, duplicates.Length,
            "characters/lords.xml defines a lord lords.xslt already defines: " + string.Join(", ", duplicates));
    }

    [TestMethod]
    public void LordsXslt_TheThree_CarryTheirRaceAgeAndOneNazgulKit()
    {
        var transform = new XslCompiledTransform();
        transform.Load(Path.Combine(CultureDataFixture.ModuleDataPath(), "lords.xslt"));
        var stub = new XDocument(new XElement("NPCCharacters",
            TheThree.Select(id => new XElement("NPCCharacter", new XAttribute("id", id)))));
        var output = new XDocument();
        using (var writer = output.CreateWriter())
            transform.Transform(stub.CreateReader(), null, writer);

        foreach (var id in TheThree)
        {
            var lord = output.Descendants("NPCCharacter").Single(e => (string?)e.Attribute("id") == id);
            Assert.AreEqual("uruk", (string?)lord.Attribute("race"), $"{id} race");
            Assert.AreEqual("20", (string?)lord.Attribute("age"), $"{id} age");
            Assert.AreEqual("22.19", (string?)lord.Element("face")?.Element("BodyProperties")?.Attribute("age"), $"{id} face age");

            var kit = lord.Element("Equipments")!.Elements("EquipmentSet").Select(s => (string?)s.Attribute("id")).ToArray();
            Assert.AreEqual(2, kit.Length, $"{id}: one battle and one civilian set");
            Assert.IsTrue(kit.All(k => k != null && k.StartsWith("nazgul_")), $"{id} kit: {string.Join(", ", kit)}");
        }
    }
}
