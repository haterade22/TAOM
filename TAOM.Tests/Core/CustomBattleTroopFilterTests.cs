using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Core;

/// <summary>
/// custom_battle_harad_troop_filter.xslt is registered for CustomGame only and hides characters from the Custom Battle
/// troop slots. The #957 troop retag put the minor clans' troops on Arthedain, Umbar, Sturgia and Dunland, so the filter
/// also hides those four clans' troops there (a campaign never loads the file). It runs for real over a synthetic
/// document, the sentinel-stub shape from .claude/rules/tests.md.
/// </summary>
[TestClass]
public class CustomBattleTroopFilterTests
{
    private const string Sheet = "Main/_Module/ModuleData/custom_battle/custom_battle_harad_troop_filter.xslt";

    private static XDocument Run(string npcXml)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
        var xslt = CompiledXslt.Get(RepoPaths.RepoPath(Sheet.Split('/')), XsltLoadStyle.IgnoreDtdNoResolver);

        using var input = XmlReader.Create(new StringReader(npcXml), settings);
        using var buffer = new StringWriter();
        using (var output = XmlWriter.Create(buffer, new XmlWriterSettings { ConformanceLevel = ConformanceLevel.Auto }))
            xslt.Transform(input, output);
        return XDocument.Parse(buffer.ToString());
    }

    private static string Doc(params string[] characters)
        => "<NPCCharacters>" + string.Concat(characters) + "</NPCCharacters>";

    private static string Troop(string id, string culture, string extra = "")
        => $"<NPCCharacter id=\"{id}\" culture=\"Culture.{culture}\" occupation=\"Soldier\" {extra}><skills/></NPCCharacter>";

    private static string? Obsolete(XDocument doc, string id)
        => doc.Descendants("NPCCharacter").Single(e => (string?)e.Attribute("id") == id).Attribute("is_obsolete")?.Value;

    [TestMethod]
    [DataRow("legion_of_the_betrayed_tier_1", "arthedain")]
    [DataRow("legion_of_the_betrayed_tier_4", "arthedain")]
    [DataRow("company_of_the_boar_tier_2", "umbar")]
    [DataRow("eleftheroi_tier_3", "sturgia")]
    [DataRow("brotherhood_of_woods_tier_1", "empire")]
    public void TheFourMinorClansTroops_AreObsoleteInCustomBattle(string id, string culture)
    {
        var doc = Run(Doc(Troop(id, culture)));

        Assert.AreEqual("true", Obsolete(doc, id));
    }

    [TestMethod]
    public void AnExistingIsObsoleteFalse_IsReplacedNotDuplicated()
    {
        var doc = Run(Doc(Troop("eleftheroi_tier_1", "sturgia", "is_obsolete=\"false\"")));

        var attrs = doc.Descendants("NPCCharacter").Single().Attributes().Where(a => a.Name == "is_obsolete").ToList();
        Assert.AreEqual(1, attrs.Count);
        Assert.AreEqual("true", attrs[0].Value);
    }

    [TestMethod]
    public void TheOtherAttributesAndChildren_PassThrough()
    {
        var doc = Run(Doc(Troop("company_of_the_boar_tier_1", "umbar")));

        var e = doc.Descendants("NPCCharacter").Single();
        Assert.AreEqual("Soldier", (string?)e.Attribute("occupation"));
        Assert.AreEqual("Culture.umbar", (string?)e.Attribute("culture"));
        Assert.IsNotNull(e.Element("skills"));
    }

    [TestMethod]
    public void TheLeaderTemplatesAndClones_AreLeftAlone()
    {
        var doc = Run(Doc(
            "<NPCCharacter id=\"spc_legion_of_the_betrayed_leader_0\" culture=\"Culture.empire\" is_hero=\"true\"/>",
            "<NPCCharacter id=\"taom_spc_legion_of_the_betrayed_leader_0\" culture=\"Culture.arthedain\" is_hero=\"true\"/>"));

        Assert.IsNull(Obsolete(doc, "spc_legion_of_the_betrayed_leader_0"));
        Assert.IsNull(Obsolete(doc, "taom_spc_legion_of_the_betrayed_leader_0"));
    }

    [TestMethod]
    public void AnUnrelatedTroopOfTheSameCulture_IsLeftAlone()
    {
        var doc = Run(Doc(
            Troop("imperial_recruit", "empire"),
            Troop("sturgia_recruit", "sturgia"),
            Troop("my_eleftheroi_tier_1", "sturgia")));

        Assert.IsNull(Obsolete(doc, "imperial_recruit"));
        Assert.IsNull(Obsolete(doc, "sturgia_recruit"));
        Assert.IsNull(Obsolete(doc, "my_eleftheroi_tier_1"));
    }

    [TestMethod]
    public void TheHaradRule_IsUnchanged()
    {
        var doc = Run(Doc(
            Troop("harad_regular", "aserai"),
            Troop("jawwal_tier_1", "aserai"),
            Troop("shaghana_militia", "shaghana"),
            "<NPCCharacter id=\"an_aserai_hero\" culture=\"Culture.aserai\" is_hero=\"true\"/>"));

        Assert.IsNull(Obsolete(doc, "harad_regular"));
        Assert.AreEqual("true", Obsolete(doc, "jawwal_tier_1"));
        Assert.AreEqual("true", Obsolete(doc, "shaghana_militia"));
        Assert.IsNull(Obsolete(doc, "an_aserai_hero"));
    }

    [TestMethod]
    public void SubModuleXml_RegistersTheFilterForCustomGameOnly()
    {
        var xml = RepoPaths.ReadSource("Main/_Module/SubModule.xml");
        var node = Regex.Match(xml,
            @"<XmlNode>\s*<XmlName id=""NPCCharacters"" path=""custom_battle/custom_battle_harad_troop_filter""\s*/>(.*?)</XmlNode>",
            RegexOptions.Singleline);

        Assert.IsTrue(node.Success, "the filter registration moved; update this test");
        Assert.IsTrue(node.Groups[1].Value.Contains("<GameType value=\"CustomGame\"/>"));
        Assert.AreEqual(1, Regex.Matches(node.Groups[1].Value, "<GameType ").Count, "a campaign must never load the filter");
    }
}
