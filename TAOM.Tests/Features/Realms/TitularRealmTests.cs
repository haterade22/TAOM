using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Realms.Domain.Config;
using TAOM.Features.Realms.Domain.Laws;
using TAOM.Features.Realms.Domain.Ledger;
using TAOM.Features.Realms.Domain.Persistence;
using TAOM.Features.Realms.Domain.Services;
using TAOM.Features.Realms.Domain.Titles;

namespace TAOM.Tests.Features.Realms;

/// <summary>
/// Titular realms (Khazad-dûm over the orcs' Moria), formed or pressed over another realm's lands by its own people, and
/// realms named after the higher title their ruler holds (Erebor as Durin's Folk).
/// </summary>
[TestClass]
public class TitularRealmTests
{
    private sealed class FakeWorld : IClaimWorld
    {
        public Dictionary<string, string> Owners { get; } = new()
        {
            ["town_a"] = "king_n", ["town_b"] = "lord_b", ["town_c"] = "lord_c", ["castle_a"] = "lord_b",
            ["village_a1"] = "king_n", ["village_ca"] = "lord_b", ["town_s"] = "king_s",
        };

        public Dictionary<string, string> Clans { get; } = new()
        {
            ["king_n"] = "clan_n", ["lord_b"] = "clan_b", ["lord_c"] = "clan_c", ["king_s"] = "clan_s",
        };

        public Dictionary<string, string> Kingdoms { get; } = new()
        {
            ["king_n"] = "north", ["lord_b"] = "north", ["lord_c"] = "north", ["king_s"] = "south",
        };

        public Dictionary<string, string> Rulers { get; } = new() { ["north"] = "king_n", ["south"] = "king_s" };
        public Dictionary<string, string> Cultures { get; } = new() { ["king_s"] = "dwarf" };

        public string? OwnerOf(string settlementId) => Owners.TryGetValue(settlementId, out var h) ? h : null;
        public string? KingdomOfSettlement(string settlementId) => OwnerOf(settlementId) is { } owner ? KingdomOfHero(owner) : null;
        public string? KingdomOfHero(string heroId) => Kingdoms.TryGetValue(heroId, out var k) ? k : null;
        public string? ClanOf(string heroId) => Clans.TryGetValue(heroId, out var c) ? c : null;
        public string? RulerOf(string kingdomId) => Rulers.TryGetValue(kingdomId, out var h) ? h : null;
        public string? CultureOf(string heroId) => Cultures.TryGetValue(heroId, out var c) ? c : null;
    }

    private static TitularRealmConfig Deep(string? liege = "e_high") =>
        new("deep", "The Deep", "dwarf", liege, "k_north", new[] { "dwarf" }, null);

    /// <summary>North and South under one high title, and the Deep over the North's lands.</summary>
    private static RealmsService Service(bool titular = true)
    {
        var config = RealmsConfig.Defaults();
        config.HighTitles.Add(new HighTitleConfig("high", "High", new[] { "north", "south" }, null));
        if (titular)
            config.TitularRealms.Add(Deep());
        var service = new RealmsService();
        service.Initialize(TestWorld.Build(), config);
        return service;
    }

    private static IEnumerable<TitleDefinition> CountiesOfTheNorth(RealmsService service) =>
        service.DescendantsOf("k_north").Where(t => t.Rank == TitleRank.County);

    // The file

    [TestMethod]
    public void A_titular_realm_and_realm_names_are_read_from_the_file()
    {
        var result = RealmsConfigXml.Parse(XDocument.Parse(@"<Realms>
  <HighTitles><HighTitle id='high' name='High' realm_name='{=h}Highland'><Member kingdom='north' /></HighTitle></HighTitles>
  <Unions><Union id='both' name='Both' realm_name='{=b}Bothland'><Member title='k_north' /></Union></Unions>
  <TitularRealms>
    <Realm id='deep' name='{=d}The Deep' culture='dwarf' liege='e_high' lands='k_north' cultures='dwarf, hilldwarf' succession='Seniority' />
    <Realm id='deep' name='again' lands='k_south' />
    <Realm id='nowhere' />
  </TitularRealms>
</Realms>"));

        var deep = result.Config.TitularRealms.Single();
        Assert.AreEqual("k_deep", deep.TitleId);
        Assert.AreEqual("{=d}The Deep", deep.Name);
        Assert.AreEqual("dwarf", deep.CultureId);
        Assert.AreEqual("e_high", deep.LiegeTitleId);
        Assert.AreEqual("k_north", deep.LandsTitleId);
        CollectionAssert.AreEqual(new[] { "dwarf", "hilldwarf" }, deep.FormingCultureIds.ToList());
        Assert.AreEqual(SuccessionLaw.Seniority, deep.Laws!.Succession);
        Assert.AreEqual("{=h}Highland", result.Config.RealmNames["e_high"]);
        Assert.AreEqual("{=b}Bothland", result.Config.RealmNames["u_both"]);
        Assert.IsFalse(result.Config.RealmNames.ContainsKey("k_deep"), "no realm_name: the title's own name");
        Assert.AreEqual(2, result.Warnings.Count, string.Join("; ", result.Warnings));
    }

    [TestMethod]
    public void The_shipped_file_has_khazad_dum_over_moria_for_the_dwarves()
    {
        var path = TAOM.Tests.Infrastructure.RepoPaths.RepoPath("Main", "_Module", "ModuleData", "realms_titles.xml");
        var config = RealmsConfigXml.Parse(XDocument.Load(path)).Config;

        var khazadDum = config.TitularRealms.Single(r => r.Id == "khazad_dum");
        Assert.AreEqual("e_durins_folk", khazadDum.LiegeTitleId);
        Assert.AreEqual("k_mistymountainorcs", khazadDum.LandsTitleId);
        Assert.IsTrue(khazadDum.Allows("erebor") && khazadDum.Allows("eredluin"));
        Assert.IsFalse(khazadDum.Allows("mistymountainorcs"), "the orcs of Moria do not form it");
        Assert.IsFalse(khazadDum.Allows(null));
        StringAssert.EndsWith(config.RealmNames["u_reunited"], "Reunited Kingdom");
    }

    // The title

    [TestMethod]
    public void A_titular_realm_is_a_vacant_kingdom_title_of_no_realm_under_its_liege()
    {
        var service = Service();

        var deep = service.Catalog.Get("k_deep")!;
        Assert.AreEqual(TitleRank.Kingdom, deep.Rank);
        Assert.AreEqual("e_high", deep.ParentId);
        Assert.IsNull(deep.KingdomId);
        Assert.IsNull(deep.SettlementId);
        Assert.AreEqual("dwarf", deep.CultureId);
        Assert.IsNull(service.Ledger.HolderOf("k_deep"));
        Assert.AreEqual(0, service.Catalog.ChildrenOf("k_deep").Count, "it takes no lands from the realm it lies over");
        Assert.AreEqual("e_high", service.Catalog.Get("k_north")!.ParentId);
    }

    [TestMethod]
    public void A_titular_realm_without_its_lands_is_skipped_and_without_its_liege_stands_alone()
    {
        var config = RealmsConfig.Defaults();
        config.TitularRealms.Add(new TitularRealmConfig("lost", "Lost", null, null, "k_nowhere", new string[0], null));
        config.TitularRealms.Add(Deep(liege: "e_missing"));
        var service = new RealmsService();
        var warnings = service.Initialize(TestWorld.Build(), config);

        Assert.IsNull(service.Catalog.Get("k_lost"));
        Assert.IsNull(service.Catalog.Get("k_deep")!.ParentId);
        Assert.IsTrue(warnings.Any(w => w.Contains("lost")) && warnings.Any(w => w.Contains("e_missing")), string.Join("; ", warnings));
    }

    [TestMethod]
    public void A_titular_realm_missing_from_an_older_save_is_added_once()
    {
        var service = Service(titular: false);
        service.Config.TitularRealms.Add(Deep());
        var warnings = new List<string>();

        Assert.AreEqual(1, service.AddMissingTitles(warnings));
        Assert.AreEqual("e_high", service.Catalog.Get("k_deep")!.ParentId);
        Assert.AreEqual(TitleEventReason.Start, service.Ledger.ChronicleOf("k_deep").Single().Reason);
        Assert.AreEqual(0, service.AddMissingTitles(warnings));
    }

    // Forming and pressing it

    [TestMethod]
    public void Its_own_people_form_it_by_holding_more_than_half_of_the_lands_it_lies_over()
    {
        var service = Service();
        var world = new FakeWorld();
        var claims = new ClaimsConfig();
        var counties = CountiesOfTheNorth(service).ToList();
        Assert.AreEqual(3, counties.Count);

        service.Ledger.SetHolder(counties[0].Id, "king_s");
        Assert.AreEqual(ClaimRefusal.TooFewVassals, service.QuoteForm("king_s", "k_deep", world, claims).Refusal);

        service.Ledger.SetHolder(counties[1].Id, "king_s");
        Assert.IsTrue(service.QuoteForm("king_s", "k_deep", world, claims).Allowed);
        CollectionAssert.Contains(service.FormOffers("king_s", world, claims).Select(o => o.TitleId).ToList(), "k_deep");

        world.Cultures.Remove("king_s");
        Assert.AreEqual(ClaimRefusal.WrongCulture, service.QuoteForm("king_s", "k_deep", world, claims).Refusal);
        Assert.IsFalse(service.FormOffers("king_s", world, claims).Any(o => o.TitleId == "k_deep"));
        // The realm it lies over is no help to anyone else: its own king holds every land but is not a dwarf.
        Assert.AreEqual(ClaimRefusal.WrongCulture, service.QuoteForm("king_n", "k_deep", world, claims).Refusal);
    }

    [TestMethod]
    public void Forming_it_moves_the_lands_under_it_and_falling_vacant_gives_them_back()
    {
        var service = Service();
        var world = new FakeWorld();
        var claims = new ClaimsConfig();
        var lands = service.Catalog.ChildrenOf("k_north").Select(t => t.Id).ToList();
        Assert.IsTrue(lands.Count > 0);
        var counties = CountiesOfTheNorth(service).ToList();
        service.Ledger.SetHolder(counties[0].Id, "king_s");
        service.Ledger.SetHolder(counties[1].Id, "king_s");

        Assert.AreEqual(1, service.Form("king_s", "k_deep", world, claims).Count);

        CollectionAssert.AreEquivalent(lands, service.Catalog.ChildrenOf("k_deep").Select(t => t.Id).ToList());
        Assert.AreEqual(0, service.Catalog.ChildrenOf("k_north").Count);
        Assert.IsTrue(service.Catalog.AncestorsOf(counties[0].Id).Any(t => t.Id == "k_deep"));
        Assert.AreEqual(0, service.LandsShownUnder("k_deep").Count, "they are its own vassals now");
        // Pressed from its own lands now, not the (empty) realm it lay over.
        world.Cultures["lord_c"] = "dwarf";
        service.Ledger.AddClaim(new TitleClaim("k_deep", "lord_c", ClaimKind.Fabricated, 0));
        Assert.AreEqual(ClaimRefusal.TooFewVassals, service.QuoteUsurp("lord_c", "clan_c", "k_deep", world, claims).Refusal);
        service.Ledger.SetHolder(counties[0].Id, "lord_c");
        service.Ledger.SetHolder(counties[1].Id, "lord_c");
        Assert.IsTrue(service.QuoteUsurp("lord_c", "clan_c", "k_deep", world, claims).Allowed);
        Assert.AreEqual(0, service.SyncTitularLands(), "nothing more to move");

        service.Ledger.SetHolder("k_deep", null);
        Assert.AreEqual(lands.Count, service.SyncTitularLands());
        CollectionAssert.AreEquivalent(lands, service.Catalog.ChildrenOf("k_north").Select(t => t.Id).ToList());
        Assert.AreEqual(0, service.Catalog.ChildrenOf("k_deep").Count);
    }

    [TestMethod]
    public void A_vacant_titular_realm_shows_the_lands_it_lies_over_in_the_tree_dimmed()
    {
        var service = Service();
        var lands = service.Catalog.ChildrenOf("k_north").Select(t => t.Id).ToList();
        CollectionAssert.AreEquivalent(lands, service.LandsShownUnder("k_deep").Select(t => t.Id).ToList());
        Assert.AreEqual(0, service.LandsShownUnder("k_north").Count, "an ordinary realm shows its own vassals");

        var tree = TitleTree.Build(service.Catalog, "k_deep", service.LandsShownUnder)!;
        var deep = tree.Root.Children.Single(n => n.Title.Id == "k_deep");
        var north = tree.Root.Children.Single(n => n.Title.Id == "k_north");
        Assert.IsFalse(deep.IsLands);
        CollectionAssert.AreEquivalent(lands, deep.Children.Select(n => n.Title.Id).ToList());
        Assert.IsTrue(deep.Children.All(n => n.IsLands) && deep.Children.All(n => n.Children.All(c => c.IsLands)));
        Assert.IsFalse(north.IsLands);
        Assert.IsTrue(north.Children.All(n => !n.IsLands));
        Assert.AreEqual(0, TitleTree.Build(service.Catalog, "k_deep")!.Root.Children.Single(n => n.Title.Id == "k_deep").Children.Count);
    }

    [TestMethod]
    public void Its_claimant_presses_the_claim_over_the_lands_it_lies_over()
    {
        var service = Service();
        var world = new FakeWorld();
        var claims = new ClaimsConfig();
        service.Ledger.SetHolder("k_deep", "lord_c");
        service.Ledger.AddClaim(new TitleClaim("k_deep", "king_s", ClaimKind.Fabricated, 0));
        var counties = CountiesOfTheNorth(service).ToList();

        Assert.AreEqual(ClaimRefusal.TooFewVassals, service.QuoteUsurp("king_s", "clan_s", "k_deep", world, claims).Refusal);
        service.Ledger.SetHolder(counties[0].Id, "king_s");
        service.Ledger.SetHolder(counties[2].Id, "king_s");
        Assert.IsTrue(service.QuoteUsurp("king_s", "clan_s", "k_deep", world, claims).Allowed);

        world.Cultures["king_s"] = "orc";
        Assert.AreEqual(ClaimRefusal.WrongCulture, service.QuoteUsurp("king_s", "clan_s", "k_deep", world, claims).Refusal);
    }

    // Realm names

    [TestMethod]
    public void A_realm_is_named_after_the_highest_title_its_ruler_holds_above_its_own()
    {
        var service = Service();
        Assert.IsNull(service.RealmNameTitle("north", "king_n"), "the high title is vacant");

        service.Ledger.SetHolder("e_high", "king_n");
        Assert.AreEqual("e_high", service.RealmNameTitle("north", "king_n")?.Id);
        Assert.IsNull(service.RealmNameTitle("south", "king_s"), "its own ruler does not hold it");
        Assert.IsNull(service.RealmNameTitle("north", null));

        service.Ledger.SetHolder("k_deep", "king_s");
        Assert.IsNull(service.RealmNameTitle("south", "king_s"), "a title beside its own does not rename a realm");
    }

    [TestMethod]
    public void A_realm_made_in_the_campaign_takes_its_ruler_s_highest_title_of_no_other_realm()
    {
        var service = Service();
        service.Ledger.SetHolder("k_south", "rebel");
        Assert.IsNull(service.RealmNameTitle("rebels", "rebel"), "another realm's own title");

        service.Ledger.SetHolder("k_deep", "rebel");
        Assert.AreEqual("k_deep", service.RealmNameTitle("rebels", "rebel")?.Id);
        service.Ledger.SetHolder("e_high", "rebel");
        Assert.AreEqual("e_high", service.RealmNameTitle("rebels", "rebel")?.Id);
    }

    [TestMethod]
    public void A_realm_takes_the_title_s_realm_name_when_it_sets_one()
    {
        var service = Service();
        service.Config.RealmNames["e_high"] = "{=x}Highland";

        Assert.AreEqual("{=x}Highland", service.RealmNameFrom(service.Catalog.Get("e_high")!));
        Assert.AreEqual("The Deep", service.RealmNameFrom(service.Catalog.Get("k_deep")!));
    }

    [TestMethod]
    public void A_kingdom_s_own_names_survive_the_save()
    {
        var records = new Dictionary<string, KingdomNameRecord>
        {
            ["erebor"] = new("{=a}Erebor", "{=b}Erebor", "{=c}Kingdom of\tErebor"),
            ["empire_w"] = new("Gondor", "", "Realm of Gondor\nand more"),
        };

        var read = KingdomNameRecords.Read(KingdomNameRecords.Write(records));

        Assert.AreEqual(2, read.Count);
        Assert.AreEqual("{=c}Kingdom of\tErebor", read["erebor"].FormalName);
        Assert.AreEqual("", read["empire_w"].InformalName);
        Assert.AreEqual("Realm of Gondor\nand more", read["empire_w"].FormalName);
        Assert.AreEqual(0, KingdomNameRecords.Read(null).Count);
        Assert.AreEqual(0, KingdomNameRecords.Read("broken line").Count);
    }
}
