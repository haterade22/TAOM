using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Realms.Domain.Config;
using TAOM.Features.Realms.Domain.Titles;

namespace TAOM.Tests.Features.Realms;

[TestClass]
public class TitlePrefixTests
{
    // k_g (Gondor) > d_a > c_town_EW5 (Dol Amroth) > b_castle ; c_town_EW1 under d_a ; k_r (Rohan) > c_town_R1
    private static readonly TitleDefinition Gondor = new("k_g", TitleRank.Kingdom, "Gondor", kingdomId: "empire_w", cultureId: "gondor");
    private static readonly TitleDefinition Anorien = new("d_a", TitleRank.Duchy, "Anorien", "k_g", kingdomId: "empire_w", cultureId: "gondor");
    private static readonly TitleDefinition DolAmroth = new("c_town_EW5", TitleRank.County, "Dol Amroth", "d_a", "town_EW5", "empire_w", "gondor");
    private static readonly TitleDefinition MinasTirith = new("c_town_EW1", TitleRank.County, "Minas Tirith", "d_a", "town_EW1", "empire_w", "gondor");
    private static readonly TitleDefinition Castle = new("b_castle", TitleRank.Barony, "Castle", "c_town_EW5", "castle", "empire_w", "gondor");
    private static readonly TitleDefinition Rohan = new("k_r", TitleRank.Kingdom, "Rohan", kingdomId: "vlandia", cultureId: "vlandia");
    private static readonly TitleDefinition Edoras = new("c_town_R1", TitleRank.County, "Edoras", "k_r", "town_R1", "vlandia", "vlandia");

    private static readonly TitleCatalog Catalog = new(new[] { Gondor, Anorien, DolAmroth, MinasTirith, Castle, Rohan, Edoras });

    private static RankNameTable Table(params TitlePrefixRule[] rules)
    {
        var table = RankNameTable.CreateDefault();
        foreach (var rule in rules)
            table.AddPrefix(rule);
        return table;
    }

    [TestMethod]
    public void Without_a_matching_prefix_the_rank_name_is_used()
    {
        var table = Table(new TitlePrefixRule("Fief of {NAME}", kingdomIds: new[] { "empire_w" }, ranks: new[] { TitleRank.County }));
        table.SetForCulture("vlandia", TitleRank.County, "Mark of {NAME}");

        Assert.AreEqual("Mark of {NAME}", table.Template(Edoras, Catalog));
        Assert.AreEqual("{=realms_rank_barony}Barony of {NAME}", table.Template(Castle, Catalog));
        Assert.AreEqual("{=realms_rank_kingdom}Kingdom of {NAME}", table.Template(Gondor, Catalog));
    }

    [TestMethod]
    public void Kingdom_and_rank_name_a_realms_vassals()
    {
        var table = Table(new TitlePrefixRule("Fief of {NAME}", kingdomIds: new[] { "empire_w" }, ranks: new[] { TitleRank.County }));

        Assert.AreEqual("Fief of {NAME}", table.Template(DolAmroth, Catalog));
        Assert.AreEqual("Fief of {NAME}", table.Template(MinasTirith, Catalog));
    }

    [TestMethod]
    public void Kingdom_without_rank_also_names_the_realm_title()
    {
        var table = Table(new TitlePrefixRule("Land of {NAME}", kingdomIds: new[] { "empire_w" }));

        Assert.AreEqual("Land of {NAME}", table.Template(Gondor, Catalog));
        Assert.AreEqual("Land of {NAME}", table.Template(Castle, Catalog));
        Assert.AreEqual("{=realms_rank_kingdom}Kingdom of {NAME}", table.Template(Rohan, Catalog));
    }

    [TestMethod]
    public void Higher_priority_wins_regardless_of_order()
    {
        var table = Table(
            new TitlePrefixRule("Princedom of {NAME}", 10, settlementIds: new[] { "town_EW5" }),
            new TitlePrefixRule("Fief of {NAME}", kingdomIds: new[] { "empire_w" }, ranks: new[] { TitleRank.County }));

        Assert.AreEqual("Princedom of {NAME}", table.Template(DolAmroth, Catalog));
        Assert.AreEqual("Fief of {NAME}", table.Template(MinasTirith, Catalog));
    }

    [TestMethod]
    public void On_equal_priority_the_later_prefix_wins()
    {
        var table = Table(
            new TitlePrefixRule("First of {NAME}", kingdomIds: new[] { "empire_w" }),
            new TitlePrefixRule("Second of {NAME}", cultureIds: new[] { "gondor" }));

        Assert.AreEqual("Second of {NAME}", table.Template(DolAmroth, Catalog));
    }

    [TestMethod]
    public void Under_matches_any_de_jure_liege_and_needs_the_catalog()
    {
        var table = Table(new TitlePrefixRule("Ward of {NAME}", underIds: new[] { "d_a" }));

        Assert.AreEqual("Ward of {NAME}", table.Template(MinasTirith, Catalog));
        Assert.AreEqual("Ward of {NAME}", table.Template(Castle, Catalog));
        Assert.AreEqual("{=realms_rank_duchy}Duchy of {NAME}", table.Template(Anorien, Catalog));
        Assert.AreEqual("{=realms_rank_county}County of {NAME}", table.Template(MinasTirith, null));
    }

    [TestMethod]
    public void Title_rank_lists_and_all_conditions_must_match()
    {
        var table = Table(
            new TitlePrefixRule("Seat of {NAME}", titleIds: new[] { "c_town_EW1" }),
            new TitlePrefixRule("Hold of {NAME}", ranks: new[] { TitleRank.Barony, TitleRank.Duchy }, cultureIds: new[] { "gondor" }),
            new TitlePrefixRule("Never {NAME}", kingdomIds: new[] { "vlandia" }, cultureIds: new[] { "gondor" }));

        Assert.AreEqual("Seat of {NAME}", table.Template(MinasTirith, Catalog));
        Assert.AreEqual("Hold of {NAME}", table.Template(Castle, Catalog));
        Assert.AreEqual("Hold of {NAME}", table.Template(Anorien, Catalog));
        Assert.AreEqual("{=realms_rank_county}County of {NAME}", table.Template(Edoras, Catalog));
    }

    [TestMethod]
    public void A_condition_on_a_missing_value_does_not_match()
    {
        var table = Table(new TitlePrefixRule("Place of {NAME}", settlementIds: new[] { "town_EW5" }));

        Assert.AreEqual("{=realms_rank_kingdom}Kingdom of {NAME}", table.Template(Gondor, Catalog));
    }
}
