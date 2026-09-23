using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Xsl;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.NazgulFamily;
using TAOM.Tests.Core;

namespace TAOM.Tests.Features.NazgulFamily;

/// <summary>
/// The Nine are race nazghul (#644), each defined exactly once.
///
/// All nine are vanilla lord ids, so they live in lords.xslt templates (Mike, 2026-09-23: "If the
/// lords are vanilla they should be in the xslt. If they are created new, they should be in
/// lords.xml"). The templates rebuild the vanilla lord from explicit <c>&lt;xsl:attribute&gt;</c>
/// lines with no <c>@*</c> passthrough, so a template without a race line loads the wraith as race
/// 0, human. The transform is run for real over a sentinel stub (the CulturePartyTemplateTests
/// technique): reading the markup cannot tell an emitted attribute from one the template never
/// reaches.
///
/// Until #644, lord_1_48_1/2/3 also had characters/lords.xml rows. The engine merges a second
/// definition per attribute (the later file wins) and UNIONS equipment sets with different ids, so
/// each campaign dressed those three in the Nazgul kit or a generic Mordor lord kit at random, and
/// their age of 20 came only from the row. The rows are gone; the values they carried moved into
/// the templates.
/// </summary>
[TestClass]
public class NazgulRaceDataTests
{
    private const string NazghulRace = "nazghul";
    private const string SentinelRace = "SENTINEL_RACE";

    private static readonly string[] TheNine =
    {
        "lord_1_15", "lord_1_155", "lord_1_16", "lord_1_28", "lord_1_38", "lord_1_48",
        "lord_1_48_1", "lord_1_48_2", "lord_1_48_3",
    };

    /// <summary>The three that had a characters/lords.xml row until #644.</summary>
    private static readonly string[] FormerlyDuplicated = { "lord_1_48_1", "lord_1_48_2", "lord_1_48_3" };

    private static XDocument TransformSentinelStub()
    {
        var transform = new XslCompiledTransform();
        transform.Load(Path.Combine(CultureDataFixture.ModuleDataPath(), "lords.xslt"));

        var stub = new XElement("NPCCharacters",
            TheNine.Select(id => new XElement("NPCCharacter",
                new XAttribute("id", id), new XAttribute("race", SentinelRace))));

        var output = new XDocument();
        using (var writer = output.CreateWriter())
            transform.Transform(new XDocument(stub).CreateReader(), null, writer);
        return output;
    }

    private static XElement? Lord(XElement? root, string id)
        => root?.Elements("NPCCharacter").SingleOrDefault(e => (string?)e.Attribute("id") == id);

    [TestMethod]
    public void LordsXslt_EveryWraith_EmitsTheNazghulRace()
    {
        var output = TransformSentinelStub();

        var offenders = TheNine
            .Select(id => (id, race: Lord(output.Root, id)?.Attribute("race")?.Value))
            .Where(x => x.race != NazghulRace)
            .Select(x => $"{x.id}: race={x.race ?? "(absent, so human)"}")
            .ToArray();

        Assert.AreEqual(0, offenders.Length,
            "A Ringwraith template in lords.xslt does not emit race=\"nazghul\":\n  " +
            string.Join("\n  ", offenders) +
            "\nSENTINEL means no template matched the id; absent means the template has no race line.");
    }

    [TestMethod]
    public void LordsXml_DefinesNoneOfTheNine()
    {
        // A second definition would win per attribute and union its equipment sets with the
        // template's: the random-kit bug these three carried until #644.
        var lords = XDocument.Load(Path.Combine(CultureDataFixture.ModuleDataPath(), "characters", "lords.xml"));

        var duplicates = TheNine.Where(id => Lord(lords.Root, id) != null).ToArray();

        Assert.AreEqual(0, duplicates.Length,
            "characters/lords.xml defines a Ringwraith that lords.xslt already defines (vanilla lords " +
            "live in the XSLT): " + string.Join(", ", duplicates));
    }

    [TestMethod]
    public void LordsXslt_FormerlyDuplicatedWraiths_KeepTheValuesTheirRowsCarried()
    {
        // Without the moved values the vanilla ages come back (31, 9 and 11: Alympia's family).
        var output = TransformSentinelStub();

        foreach (var id in FormerlyDuplicated)
        {
            var lord = Lord(output.Root, id);
            Assert.IsNotNull(lord, id);
            Assert.AreEqual("20", lord!.Attribute("age")?.Value, $"{id} age");
            Assert.AreEqual("22.19", lord.Element("face")?.Element("BodyProperties")?.Attribute("age")?.Value, $"{id} face age");
            Assert.AreEqual("false", lord.Attribute("is_female")?.Value, $"{id} is_female");

            var kit = lord.Element("Equipments")!.Elements("EquipmentSet").Select(s => (string?)s.Attribute("id")).ToArray();
            Assert.AreEqual(2, kit.Length, $"{id}: one battle and one civilian set");
            Assert.IsTrue(kit.All(k => k != null && k.StartsWith("nazgul_")), $"{id} wears the Nazgul kit: {string.Join(", ", kit)}");
        }
    }

    [TestMethod]
    public void PinnedWraithIds_AreTheNineTheRegistryKnows()
    {
        var registry = new NazgulRegistry();

        Assert.AreEqual(9, TheNine.Distinct().Count(), "the Nine are nine distinct ids");
        foreach (var id in TheNine)
            Assert.IsTrue(registry.IsWraith(id), $"{id} is pinned here but NazgulRegistry does not know it");
    }
}
