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
            Troop("dunland_recruit", "empire"),
            Troop("sturgia_recruit", "sturgia"),
            Troop("my_eleftheroi_tier_1", "sturgia")));

        Assert.IsNull(Obsolete(doc, "dunland_recruit"));
        Assert.IsNull(Obsolete(doc, "sturgia_recruit"));
        Assert.IsNull(Obsolete(doc, "my_eleftheroi_tier_1"));
    }

    // Vanilla soldiers of the cultures TAOM reuses (empire Dunland, sturgia Dale, vlandia Rohan, khuzait Rhun) were
    // offered next to the TAOM troops in those Custom Battle slots: the main lines, militia and the vanilla minor
    // clans' tier troops. Measured on v1.5.7 by merging SandBoxCore, CustomBattle and TAOM NPCCharacters for
    // CustomGame: 88 vanilla soldiers there, every one matched by these id rules.
    [TestMethod]
    [DataRow("imperial_recruit", "empire")]
    [DataRow("imperial_militia_spearman", "empire")]
    [DataRow("bucellarii", "empire")]
    [DataRow("hidden_hand_tier_2", "empire")]
    [DataRow("embers_of_flame_tier_3", "empire")]
    [DataRow("sturgian_warrior", "sturgia")]
    [DataRow("varyag", "sturgia")]
    [DataRow("varyag_veteran", "sturgia")]
    [DataRow("druzhinnik", "sturgia")]
    [DataRow("druzhinnik_champion", "sturgia")]
    [DataRow("lakepike_tier_1", "sturgia")]
    [DataRow("vlandian_knight", "vlandia")]
    [DataRow("vlandian_militia_archer", "vlandia")]
    [DataRow("khuzait_militia_spearman", "khuzait")]
    [DataRow("karakhuzaits_tier_1", "khuzait")]
    public void VanillaSoldiersOfReusedCultures_AreObsoleteInCustomBattle(string id, string culture)
    {
        var doc = Run(Doc(Troop(id, culture)));

        Assert.AreEqual("true", Obsolete(doc, id));
    }

    // Training dummies and training-yard opponents are Soldier-occupation characters in most TAOM cultures
    // (gear_practice_dummy_<culture>, gear_dummy_<culture>, weapon_practice_stage_N_<culture>), so they were offered
    // as troops. Culture data and tournaments read them by id in a campaign, which never loads this filter.
    [TestMethod]
    [DataRow("gear_practice_dummy_rohan", "vlandia")]
    [DataRow("gear_dummy_gondor", "gondor")]
    [DataRow("gear_practice_dummy_vlandia", "vlandia")]
    [DataRow("weapon_dummy_archer", "khuzait")]
    [DataRow("weapon_practice_stage_2_dale", "sturgia")]
    [DataRow("gear_dummy_khand", "battania")]
    [DataRow("weapon_practice_stage_1_khand", "battania")]
    public void TrainingDummies_AreObsoleteInCustomBattle(string id, string culture)
    {
        var doc = Run(Doc(Troop(id, culture)));

        Assert.AreEqual("true", Obsolete(doc, id));
    }

    [TestMethod]
    public void TaomTroopsOfTheReusedCultures_AreLeftAlone()
    {
        var doc = Run(Doc(
            Troop("rohan_rider", "vlandia"),
            Troop("guard_rohan", "vlandia"),
            Troop("dale_guardsman", "sturgia"),
            Troop("rhun_spearman", "khuzait"),
            Troop("khand_warrior", "battania"),
            Troop("varyag_rider", "sturgia")));

        Assert.IsNull(Obsolete(doc, "rohan_rider"));
        Assert.IsNull(Obsolete(doc, "guard_rohan"));
        Assert.IsNull(Obsolete(doc, "dale_guardsman"));
        Assert.IsNull(Obsolete(doc, "rhun_spearman"));
        Assert.IsNull(Obsolete(doc, "khand_warrior"));
        Assert.IsNull(Obsolete(doc, "varyag_rider"));
    }

    // TAOM has no Khand (Culture.battania) soldiers of its own, so the vanilla Battanian line is the whole Khand
    // Custom Battle roster; hiding it would empty the Khand ranged and cavalry slots. No vanilla-line rule names
    // battanian_ or wolfskins_, which is what keeps them: a rule that did would fail these rows.
    [TestMethod]
    [DataRow("battanian_fian")]
    [DataRow("battanian_horseman")]
    [DataRow("wolfskins_tier_3")]
    public void TheVanillaBattanianLine_StaysForKhand(string id)
    {
        var doc = Run(Doc(Troop(id, "battania")));

        Assert.IsNull(Obsolete(doc, id));
    }

    // The vanilla CustomBattle module's Calradian commanders are heroes of reused cultures, so the muster screen
    // offered them as commanders next to TAOM's lords. Dunland (empire) and Khand (battania) have TAOM lords in
    // Custom Battle too (custom_battle_lords.xml), so theirs are hidden like the rest.
    [TestMethod]
    [DataRow("commander_2", "vlandia")]
    [DataRow("commander_7", "khuzait")]
    [DataRow("commander_6", "aserai")]
    [DataRow("commander_24", "sturgia")]
    [DataRow("commander_1", "empire")]
    [DataRow("commander_23", "empire")]
    [DataRow("commander_4", "battania")]
    [DataRow("commander_16", "battania")]
    public void VanillaCustomBattleCommanders_AreObsoleteInCustomBattle(string id, string culture)
    {
        var doc = Run(Doc($"<NPCCharacter id=\"{id}\" culture=\"Culture.{culture}\" is_hero=\"true\"/>"));

        var attrs = doc.Descendants("NPCCharacter").Single().Attributes().Where(a => a.Name == "is_obsolete").ToList();
        Assert.AreEqual(1, attrs.Count);
        Assert.AreEqual("true", attrs[0].Value);
    }

    // Vanilla's trader hero of Dunland is offered by the muster as a commander too (SandBoxCore spnpccharacters.xml:
    // merchant_empire, is_hero=true, occupation GoodsTrader). The other merchant_* are not heroes.
    [TestMethod]
    public void TheDunlandTraderHero_IsObsoleteInCustomBattle()
    {
        var doc = Run(Doc(
            "<NPCCharacter id=\"merchant_empire\" culture=\"Culture.empire\" is_hero=\"true\" occupation=\"GoodsTrader\"/>",
            "<NPCCharacter id=\"merchant_vlandia\" culture=\"Culture.vlandia\" is_hero=\"false\" occupation=\"GoodsTrader\"/>"));

        Assert.AreEqual("true", Obsolete(doc, "merchant_empire"));
        Assert.IsNull(Obsolete(doc, "merchant_vlandia"));
    }

    // Other commander-like ids are not vanilla's.
    [TestMethod]
    public void OtherCommanderLikeIds_AreLeftAlone()
    {
        var doc = Run(Doc(
            "<NPCCharacter id=\"commander_aragorn\" culture=\"Culture.gondor\" is_hero=\"true\"/>",
            "<NPCCharacter id=\"commander_\" culture=\"Culture.gondor\" is_hero=\"true\"/>",
            Troop("commander_7", "khuzait")));

        Assert.IsNull(Obsolete(doc, "commander_aragorn"));
        Assert.IsNull(Obsolete(doc, "commander_"));
        Assert.IsNull(Obsolete(doc, "commander_7"));
    }

    [TestMethod]
    public void VanillaHeroesWithAVanillaPrefix_AreLeftAlone()
    {
        var doc = Run(Doc(
            "<NPCCharacter id=\"vlandian_lord_template\" culture=\"Culture.vlandia\" is_hero=\"true\"/>",
            "<NPCCharacter id=\"imperial_wanderer\" culture=\"Culture.empire\" is_hero=\"true\"/>"));

        Assert.IsNull(Obsolete(doc, "vlandian_lord_template"));
        Assert.IsNull(Obsolete(doc, "imperial_wanderer"));
    }

    [TestMethod]
    public void TheHaradDummy_IsStillObsoleteOnce()
    {
        var doc = Run(Doc(Troop("gear_practice_dummy_harad", "aserai")));

        var attrs = doc.Descendants("NPCCharacter").Single().Attributes().Where(a => a.Name == "is_obsolete").ToList();
        Assert.AreEqual(1, attrs.Count);
        Assert.AreEqual("true", attrs[0].Value);
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
