using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Realms.Domain.Config;
using TAOM.Features.Realms.Domain.Laws;
using TAOM.Features.Realms.Domain.Titles;

namespace TAOM.Tests.Features.Realms;

[TestClass]
public class ConfigXmlTests
{
    private static ConfigReadResult Parse(string xml) => RealmsConfigXml.Parse(XDocument.Parse(xml));

    [TestMethod]
    public void Reads_prefixes_with_their_conditions_in_file_order()
    {
        var result = Parse(@"<Realms>
  <Prefixes>
    <Prefix name='Fief of {NAME}' kingdom='empire_w' rank='County' />
    <Prefix name='Princedom of {NAME}' settlement='town_EW5' rank='county barony' priority='10'
            title='c_a c_b' under='d_a' culture='gondor dolamroth' />
  </Prefixes>
</Realms>");

        Assert.AreEqual(0, result.Warnings.Count, string.Join("; ", result.Warnings));
        var prefixes = result.Config.RankNames.Prefixes;
        Assert.AreEqual(2, prefixes.Count);
        Assert.AreEqual("Fief of {NAME}", prefixes[0].Template);
        Assert.AreEqual(0, prefixes[0].Priority);
        CollectionAssert.AreEqual(new[] { "empire_w" }, prefixes[0].KingdomIds.ToArray());
        CollectionAssert.AreEqual(new[] { TitleRank.County }, prefixes[0].Ranks.ToArray());
        Assert.AreEqual(10, prefixes[1].Priority);
        CollectionAssert.AreEquivalent(new[] { TitleRank.County, TitleRank.Barony }, prefixes[1].Ranks.ToArray());
        CollectionAssert.AreEquivalent(new[] { "c_a", "c_b" }, prefixes[1].TitleIds.ToArray());
        CollectionAssert.AreEquivalent(new[] { "town_EW5" }, prefixes[1].SettlementIds.ToArray());
        CollectionAssert.AreEquivalent(new[] { "d_a" }, prefixes[1].UnderIds.ToArray());
        CollectionAssert.AreEquivalent(new[] { "gondor", "dolamroth" }, prefixes[1].CultureIds.ToArray());
    }

    [TestMethod]
    public void Known_over_needs_a_holder_style_and_a_rank()
    {
        var result = Parse(@"<Realms>
  <Prefixes>
    <Prefix name='A {NAME}' settlement='s1' holder='Prince {NAME}' known_over='Duchy' />
    <Prefix name='B {NAME}' settlement='s2' known_over='Duchy' />
    <Prefix name='C {NAME}' settlement='s3' holder='Prince {NAME}' known_over='Emperor' />
  </Prefixes>
</Realms>");

        var prefixes = result.Config.RankNames.Prefixes;
        Assert.AreEqual(3, prefixes.Count);
        Assert.AreEqual(TitleRank.Duchy, prefixes[0].KnownOver);
        Assert.IsNull(prefixes[1].KnownOver);
        Assert.IsNull(prefixes[2].KnownOver);
        Assert.AreEqual(2, result.Warnings.Count, string.Join("; ", result.Warnings));
    }

    [TestMethod]
    public void Bad_prefixes_are_reported_and_skipped()
    {
        var result = Parse(@"<Realms>
  <Prefixes>
    <Prefix kingdom='empire_w' />
    <Prefix name='A {NAME}' kingdom='empire_w' priority='high' />
    <Prefix name='B {NAME}' rank='Castle' />
    <Prefix name='C {NAME}' />
    <Prefix name='D {NAME}' culture='gondor' />
  </Prefixes>
</Realms>");

        Assert.AreEqual(4, result.Warnings.Count, string.Join("; ", result.Warnings));
        Assert.AreEqual(1, result.Config.RankNames.Prefixes.Count);
        Assert.AreEqual("D {NAME}", result.Config.RankNames.Prefixes[0].Template);
    }

    [TestMethod]
    public void Reads_generation_laws_names_high_titles_and_duchies()
    {
        var result = Parse(@"<Realms>
  <Generation include_villages='false' counties_per_duchy='4' />
  <DefaultLaws succession='Seniority' gender='Absolute' />
  <RankNames>
    <Rank rank='County' name='{=a}Shire of {NAME}' />
    <Culture id='erebor'><Rank rank='Barony' name='{=b}Hold of {NAME}' /></Culture>
  </RankNames>
  <Kingdoms><Kingdom id='erebor' succession='Primogeniture' gender='Agnatic' /></Kingdoms>
  <HighTitles><HighTitle id='arnor' name='{=c}Arnor' succession='Primogeniture' gender='Agnatic'><Member kingdom='arthedain' /><Member kingdom='cardolan' /></HighTitle></HighTitles>
  <Duchies><Duchy id='west' name='{=d}West' kingdom='erebor'><County settlement='town_E1' /></Duchy></Duchies>
</Realms>");
        var config = result.Config;

        Assert.AreEqual(0, result.Warnings.Count, string.Join("; ", result.Warnings));
        Assert.IsFalse(config.IncludeVillages);
        Assert.AreEqual(4, config.CountiesPerDuchy);
        Assert.AreEqual(new TitleLaws(SuccessionLaw.Seniority, GenderLaw.Absolute), config.DefaultLaws);
        Assert.AreEqual("{=a}Shire of {NAME}", config.RankNames.Template(TitleRank.County, "rohan"));
        Assert.AreEqual("{=b}Hold of {NAME}", config.RankNames.Template(TitleRank.Barony, "erebor"));
        Assert.AreEqual("{=realms_rank_barony}Barony of {NAME}", config.RankNames.Template(TitleRank.Barony, "rohan"));
        Assert.AreEqual(GenderLaw.Agnatic, config.KingdomLaws["erebor"].Gender);
        CollectionAssert.AreEqual(new[] { "arthedain", "cardolan" }, config.HighTitles.Single().MemberKingdomIds.ToArray());
        Assert.AreEqual("town_E1", config.Duchies.Single().CountySettlementIds.Single());
        Assert.AreEqual(0, config.Duchies.Single().BaronySettlementIds.Count);
    }

    [TestMethod]
    public void Reads_duchy_baronies_and_reports_members_without_a_settlement()
    {
        var result = Parse(@"<Realms>
  <Duchies>
    <Duchy id='fells' name='{=d}Fells' kingdom='north'>
      <Barony settlement='castle_A' />
      <County settlement='town_A' />
      <Barony settlement='castle_B' />
      <Barony />
    </Duchy>
  </Duchies>
</Realms>");

        Assert.AreEqual(1, result.Warnings.Count, string.Join("; ", result.Warnings));
        var duchy = result.Config.Duchies.Single();
        CollectionAssert.AreEqual(new[] { "town_A" }, duchy.CountySettlementIds.ToArray());
        CollectionAssert.AreEqual(new[] { "castle_A", "castle_B" }, duchy.BaronySettlementIds.ToArray());
    }

    [TestMethod]
    public void Reads_holder_styles_by_rank_culture_and_gender()
    {
        var result = Parse(@"<Realms>
  <RankNames>
    <Holder rank='Kingdom' male='{=k}King {NAME}' female='{=q}Queen {NAME}' />
    <Holder rank='County' male='{=c}Count {NAME}' female='{=cs}Countess {NAME}' />
    <Culture id='lindon'>
      <Holder rank='Kingdom' male='{=a}Aran {NAME}' female='{=b}Bereth {NAME}' />
    </Culture>
    <Culture id='erebor'>
      <Holder rank='Kingdom' male='{=u}Uzbad {NAME}' />
    </Culture>
  </RankNames>
</Realms>");
        var names = result.Config.RankNames;

        Assert.AreEqual(0, result.Warnings.Count, string.Join("; ", result.Warnings));
        Assert.AreEqual("{=a}Aran {NAME}", names.HolderTemplate(TitleRank.Kingdom, "lindon", isFemale: false));
        Assert.AreEqual("{=b}Bereth {NAME}", names.HolderTemplate(TitleRank.Kingdom, "lindon", isFemale: true));
        Assert.AreEqual("{=u}Uzbad {NAME}", names.HolderTemplate(TitleRank.Kingdom, "erebor", isFemale: false));
        // The culture's entry replaces the default as a whole: no female form means no style, not the default's.
        Assert.IsNull(names.HolderTemplate(TitleRank.Kingdom, "erebor", isFemale: true));
        Assert.AreEqual("{=q}Queen {NAME}", names.HolderTemplate(TitleRank.Kingdom, "rohan", isFemale: true));
        Assert.AreEqual("{=c}Count {NAME}", names.HolderTemplate(TitleRank.County, "lindon", isFemale: false));
        Assert.AreEqual("{=k}King {NAME}", names.HolderTemplate(TitleRank.Kingdom, null, isFemale: false));
        Assert.IsNull(names.HolderTemplate(TitleRank.Duchy, "lindon", isFemale: false));
    }

    [TestMethod]
    public void Holder_styles_need_a_known_rank_and_a_male_style()
    {
        var result = Parse(@"<Realms>
  <RankNames>
    <Holder rank='Kingdom' female='{=q}Queen {NAME}' />
    <Culture id='lindon'><Holder rank='Realm' male='{=a}Aran {NAME}' /></Culture>
    <Culture><Holder rank='County' male='{=h}Hir {NAME}' /></Culture>
  </RankNames>
</Realms>");

        Assert.AreEqual(3, result.Warnings.Count, string.Join("; ", result.Warnings));
        Assert.IsNull(result.Config.RankNames.HolderTemplate(TitleRank.Kingdom, "lindon", isFemale: true));
        Assert.IsNull(result.Config.RankNames.HolderTemplate(TitleRank.County, "lindon", isFemale: false));
    }

    [TestMethod]
    public void The_shipped_file_styles_holders_by_culture_and_leaves_orcs_plain()
    {
        var path = TAOM.Tests.Infrastructure.RepoPaths.RepoPath("Main", "_Module", "ModuleData", "realms_titles.xml");
        var config = RealmsConfigXml.Parse(XDocument.Load(path)).Config;
        var names = config.RankNames;

        StringAssert.EndsWith(names.HolderTemplate(TitleRank.Kingdom, "lindon", isFemale: false) ?? "", "Aran {NAME}");
        StringAssert.EndsWith(names.HolderTemplate(TitleRank.County, "lothlorien", isFemale: true) ?? "", "Híril {NAME}");
        StringAssert.EndsWith(names.HolderTemplate(TitleRank.Kingdom, "erebor", isFemale: false) ?? "", "Uzbad {NAME}");
        StringAssert.EndsWith(names.Template(TitleRank.Duchy, "erebor"), "Hold of {NAME}");
        StringAssert.EndsWith(names.Template(TitleRank.Barony, "eredluin"), "Delving of {NAME}");
        StringAssert.EndsWith(names.Template(TitleRank.County, "rivendell"), "Refuge of {NAME}");
        StringAssert.EndsWith(names.HolderTemplate(TitleRank.Kingdom, "gondor", isFemale: false) ?? "", "King {NAME}");
        StringAssert.EndsWith(names.HolderTemplate(TitleRank.County, "gondor", isFemale: false) ?? "", "Lord {NAME}");
        StringAssert.EndsWith(names.HolderTemplate(TitleRank.Kingdom, "vlandia", isFemale: true) ?? "", "Queen {NAME}");
        Assert.IsNull(names.HolderTemplate(TitleRank.Kingdom, "mordor", isFemale: false));
        Assert.IsNull(names.HolderTemplate(TitleRank.County, "gundabad", isFemale: false));

        var dolAmroth = new TitleDefinition("c_town_EW5", TitleRank.County, "Dol Amroth", settlementId: "town_EW5", kingdomId: "empire_w", cultureId: "gondor");
        StringAssert.EndsWith(names.HolderTemplate(dolAmroth, null, "gondor", isFemale: false) ?? "", "Prince {NAME}");
        StringAssert.EndsWith(names.HolderTemplate(dolAmroth, null, "gondor", isFemale: true) ?? "", "Princess {NAME}");
        var gondor = new TitleDefinition("k_empire_w", TitleRank.Kingdom, "Gondor", kingdomId: "empire_w", cultureId: "gondor");
        // Imrahil is known as Prince of Dol Amroth though he also holds the Duchy of Belfalas, unless he wears a crown.
        var belfalas = new TitleDefinition("d_belfalas", TitleRank.Duchy, "Belfalas", kingdomId: "empire_w", cultureId: "gondor");
        Assert.AreSame(dolAmroth, names.KnownBy(new[] { belfalas, dolAmroth }, null, "gondor"));
        Assert.AreSame(gondor, names.KnownBy(new[] { belfalas, dolAmroth, gondor }, null, "gondor"));
        Assert.AreSame(belfalas, names.KnownBy(new[] { belfalas, dolAmroth }, null, "mordor"));
        Assert.IsNull(names.KnownBy(new TitleDefinition[0], null, "gondor"));
        // The crown styles its holder King; the Ruling Steward's style comes from the office, which follows the ruler.
        StringAssert.EndsWith(names.HolderTemplate(gondor, null, "gondor", isFemale: false) ?? "", "King {NAME}");
        var steward = config.Offices.Single(o => o.Id == "steward_gondor");
        StringAssert.EndsWith(steward.HolderMale ?? "", "Steward {NAME}");
        Assert.AreEqual("empire_w", steward.KingdomId);
        Assert.IsTrue(steward.FollowsRuler);
        StringAssert.EndsWith(names.Template(gondor, null), "Kingdom of {NAME}");

        // Elessar's union is simply "Reunited Kingdom", by a prefix that also renames saves made with the older name.
        var reunited = new TitleDefinition("u_reunited", TitleRank.Union, "Gondor and Arnor");
        StringAssert.EndsWith(names.Template(reunited, null), "Reunited Kingdom");

        // Aragorn restoring Gondor's crown founds the House of Telcontar under the Húrinionath banner, with Minas Tirith.
        var coronation = config.Coronations.Single();
        Assert.AreEqual("k_empire_w", coronation.TitleId);
        Assert.AreEqual("named_companion_aragorn", coronation.HeroId);
        StringAssert.EndsWith(coronation.ClanName, "House of Telcontar");
        Assert.AreEqual("clan_empire_west_1", coronation.BannerOfClanId);
        CollectionAssert.AreEqual(new[] { "town_EW1" }, coronation.FiefIds.ToArray());
        CollectionAssert.AreEqual(new[] { "d_anorien" }, coronation.TitleIds.ToArray());
        Assert.AreEqual("steward_gondor", coronation.OfficeId);
        Assert.AreEqual(2500, coronation.Influence);
        Assert.AreEqual("11.149.149.1528.1528.764.764.1.0.0.31006.172.2000.580.580.764.798.0.0.0", coronation.Banner);
        StringAssert.StartsWith(coronation.FormerBanner, "11.172.149.");
        StringAssert.EndsWith(coronation.Event.Heading, "The Return of the King");
        StringAssert.EndsWith(coronation.Event.Button, "Long live the King!");
        StringAssert.Contains(coronation.Event.Text, "{HERO}");
        Assert.IsNull(coronation.Event.Image);
    }

    [TestMethod]
    public void A_coronation_reads_its_lists_and_defaults()
    {
        var xml = XDocument.Parse(
            "<Realms><Coronations>" +
            "<Coronation title=\"k_a\" hero=\"h\" clan=\"House\" fiefs=\"town_1, castle_2\" titles=\"d_x\" />" +
            "<Coronation title=\"k_a\" hero=\"h\" clan=\"Again\" />" +
            "<Coronation title=\"k_b\" hero=\"h\" />" +
            "</Coronations></Realms>");
        var result = RealmsConfigXml.Parse(xml);
        var coronation = result.Config.Coronations.Single();

        CollectionAssert.AreEqual(new[] { "town_1", "castle_2" }, coronation.FiefIds.ToArray());
        CollectionAssert.AreEqual(new[] { "d_x" }, coronation.TitleIds.ToArray());
        Assert.IsNull(coronation.BannerOfClanId);
        Assert.AreEqual(4, coronation.Tier);
        Assert.AreEqual(33, coronation.Troops);
        Assert.AreEqual(20000, coronation.Gold);
        Assert.AreEqual(50, coronation.Relation);
        Assert.IsNull(coronation.Influence);
        Assert.IsNull(coronation.FormerBanner);
        Assert.IsNull(coronation.Banner);
        Assert.IsNull(coronation.Event.Heading);
        Assert.IsNull(coronation.Event.Image);
        Assert.AreEqual(2, result.Warnings.Count, "the duplicate and the one without a clan");
    }

    [TestMethod]
    public void The_holder_is_styled_by_their_own_culture_and_a_title_style_needs_its_culture()
    {
        var path = TAOM.Tests.Infrastructure.RepoPaths.RepoPath("Main", "_Module", "ModuleData", "realms_titles.xml");
        var names = RealmsConfigXml.Parse(XDocument.Load(path)).Config.RankNames;
        var castle = new TitleDefinition("b_castle_EW4", TitleRank.Barony, "Cair Andros", settlementId: "castle_EW4", kingdomId: "empire_w", cultureId: "gondor");
        var dolAmroth = new TitleDefinition("c_town_EW5", TitleRank.County, "Dol Amroth", settlementId: "town_EW5", kingdomId: "empire_w", cultureId: "gondor");
        var gondor = new TitleDefinition("k_empire_w", TitleRank.Kingdom, "Gondor", kingdomId: "empire_w", cultureId: "gondor");

        Assert.IsNull(names.HolderTemplate(castle, null, "mordor", isFemale: false), "an orc keeps his own name");
        StringAssert.EndsWith(names.HolderTemplate(castle, null, "mirkwood", isFemale: false) ?? "", "Hîr {NAME}");
        StringAssert.EndsWith(names.HolderTemplate(castle, null, "gondor", isFemale: false) ?? "", "Lord {NAME}");
        StringAssert.EndsWith(names.HolderTemplate(dolAmroth, null, "vlandia", isFemale: false) ?? "", "Lord {NAME}");
        StringAssert.EndsWith(names.HolderTemplate(gondor, null, "vlandia", isFemale: false) ?? "", "King {NAME}");
        Assert.IsNull(names.HolderTemplate(gondor, null, "isengard", isFemale: false));
        StringAssert.EndsWith(names.HolderTemplate(castle, null, null, isFemale: false) ?? "", "Lord {NAME}");
    }

    [TestMethod]
    public void Prefix_holder_styles_win_and_a_prefix_without_one_falls_through()
    {
        var result = Parse(@"<Realms>
  <RankNames>
    <Holder rank='County' male='{=l}Lord {NAME}' female='{=y}Lady {NAME}' />
    <Culture id='orc' holders='plain' />
  </RankNames>
  <Prefixes>
    <Prefix name='Fief of {NAME}' kingdom='empire_w' rank='County' />
    <Prefix name='Princedom of {NAME}' settlement='town_EW5' priority='10' holder='{=p}Prince {NAME}' />
    <Prefix name='Bad {NAME}' settlement='town_x' holder_female='{=q}Queen {NAME}' />
  </Prefixes>
</Realms>");

        Assert.AreEqual(1, result.Warnings.Count, string.Join("; ", result.Warnings));
        var names = result.Config.RankNames;
        var prince = new TitleDefinition("c_town_EW5", TitleRank.County, "Dol Amroth", settlementId: "town_EW5", kingdomId: "empire_w");
        var fief = new TitleDefinition("c_town_EW1", TitleRank.County, "Lossarnach", settlementId: "town_EW1", kingdomId: "empire_w");
        var orc = new TitleDefinition("c_town_O1", TitleRank.County, "Carn Dûm", settlementId: "town_O1", cultureId: "orc");

        Assert.AreEqual("{=p}Prince {NAME}", names.HolderTemplate(prince, null, null, isFemale: false));
        Assert.IsNull(names.HolderTemplate(prince, null, null, isFemale: true));
        Assert.AreEqual("{=y}Lady {NAME}", names.HolderTemplate(fief, null, null, isFemale: true));
        Assert.IsNull(names.HolderTemplate(orc, null, null, isFemale: false));
        Assert.IsNull(names.HolderTemplate(fief, null, "orc", isFemale: false));
    }

    [TestMethod]
    public void An_unknown_holders_value_is_reported()
    {
        var result = Parse("<Realms><RankNames><Culture id='orc' holders='none' /></RankNames></Realms>");

        Assert.AreEqual(1, result.Warnings.Count, string.Join("; ", result.Warnings));
    }

    [TestMethod]
    public void Bad_values_are_reported_and_defaults_kept()
    {
        var result = Parse(@"<Realms>
  <Generation include_villages='maybe' counties_per_duchy='-1' />
  <Kingdoms><Kingdom id='x' succession='Tanistry' /><Kingdom succession='Seniority' /></Kingdoms>
  <RankNames><Rank rank='Archduchy' name='{NAME}' /></RankNames>
</Realms>");

        Assert.AreEqual(5, result.Warnings.Count, string.Join("; ", result.Warnings));
        Assert.IsTrue(result.Config.IncludeVillages);
        Assert.AreEqual(3, result.Config.CountiesPerDuchy);
        Assert.AreEqual(SuccessionLaw.Primogeniture, result.Config.KingdomLaws["x"].Succession);
    }

    [TestMethod]
    public void Wrong_root_falls_back_to_defaults()
    {
        var result = Parse("<Titles />");

        Assert.AreEqual(1, result.Warnings.Count);
        Assert.IsTrue(result.Config.IncludeVillages);
    }

    [TestMethod]
    public void The_shipped_realms_titles_xml_parses_without_warnings()
    {
        var path = TAOM.Tests.Infrastructure.RepoPaths.RepoPath("Main", "_Module", "ModuleData", "realms_titles.xml");

        var result = RealmsConfigXml.Parse(XDocument.Load(path));

        Assert.AreEqual(0, result.Warnings.Count, string.Join("; ", result.Warnings));
        Assert.IsTrue(result.Config.HighTitles.Count > 0);
    }

    [TestMethod]
    public void Reads_a_kingdom_capital_with_or_without_laws()
    {
        var result = RealmsConfigXml.Parse(XDocument.Parse(
            "<Realms><Kingdoms><Kingdom id=\"a\" succession=\"Seniority\" capital=\"town_a\" /><Kingdom id=\"b\" capital=\"town_b\" /></Kingdoms></Realms>"));

        Assert.AreEqual("town_a", result.Config.KingdomCapitals["a"]);
        Assert.AreEqual("town_b", result.Config.KingdomCapitals["b"]);
        Assert.IsFalse(result.Config.KingdomLaws.ContainsKey("b"));
        Assert.AreEqual(0, result.Warnings.Count);
    }

    [TestMethod]
    public void The_shipped_file_makes_Minas_Tirith_the_capital_of_Gondor()
    {
        var path = TAOM.Tests.Infrastructure.RepoPaths.RepoPath("Main", "_Module", "ModuleData", "realms_titles.xml");

        Assert.AreEqual("town_EW1", RealmsConfigXml.Parse(XDocument.Load(path)).Config.KingdomCapitals["empire_w"]);
    }
}
