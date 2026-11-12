using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Realms.Domain.Config;
using TAOM.Features.Realms.Domain.Laws;
using TAOM.Features.Realms.Domain.Ledger;
using TAOM.Features.Realms.Domain.Services;
using TAOM.Features.Realms.Domain.Succession;
using TAOM.Features.Realms.Domain.Titles;

namespace TAOM.Tests.Features.Realms;

[TestClass]
public class ClaimTests
{
    /// <summary>The test map's owners, clans and realms, editable per test.</summary>
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

        public string? OwnerOf(string settlementId) => Owners.TryGetValue(settlementId, out var h) ? h : null;
        public string? KingdomOfSettlement(string settlementId) => OwnerOf(settlementId) is { } owner ? KingdomOfHero(owner) : null;
        public string? KingdomOfHero(string heroId) => Kingdoms.TryGetValue(heroId, out var k) ? k : null;
        public string? ClanOf(string heroId) => Clans.TryGetValue(heroId, out var c) ? c : null;

        public Dictionary<string, string> Rulers { get; } = new() { ["north"] = "king_n", ["south"] = "king_s" };
        public string? RulerOf(string kingdomId) => Rulers.TryGetValue(kingdomId, out var h) ? h : null;
        public string? CultureOf(string heroId) => null;
    }

    private static readonly ClaimsConfig Prices = new();

    private static RealmsService Service()
    {
        var service = new RealmsService();
        service.Initialize(TestWorld.Build(), RealmsConfig.Defaults());
        return service;
    }

    private static FiefNeighbors Neighbors(int count) => FiefNeighbors.Build(
        TestWorld.Build().Fiefs.Where(f => f.Kind != global::TAOM.Features.Realms.Domain.World.FiefKind.Village).Select(f => (f.Id, f.X, f.Y)), count);

    /// <summary>The southern king, whose only town borders town_c alone.</summary>
    private static ClaimBuyer SouthKing() => new("king_s", "clan_s", "south", new[] { "town_s" });

    [TestMethod]
    public void Neighbors_are_the_nearest_towns_and_castles()
    {
        var neighbors = Neighbors(2);

        CollectionAssert.AreEqual(new[] { "castle_a", "town_a" }, neighbors.Of("town_b").ToArray());
        CollectionAssert.AreEqual(new[] { "town_c" }, neighbors.Of("town_s").Take(1).ToArray());
        Assert.AreEqual(0, neighbors.Of("nowhere").Count);
    }

    [TestMethod]
    public void A_claim_can_be_bought_on_a_bordering_title_and_the_titles_above_it_one_step_at_a_time()
    {
        var service = Service();
        var world = new FakeWorld();
        var neighbors = Neighbors(1);
        var duchy = service.Catalog.Get("c_town_c")!.ParentId!;
        Assert.AreEqual(TitleRank.Duchy, service.Catalog.Get(duchy)!.Rank);
        Assert.AreEqual("k_north", service.Catalog.Get(duchy)!.ParentId);

        var county = service.QuoteClaim(SouthKing(), "c_town_c", world, neighbors, Prices);
        Assert.IsTrue(county.Allowed, county.Refusal.ToString());
        Assert.AreEqual(80, county.Cost);
        Assert.AreEqual(ClaimRefusal.TooFar, service.QuoteClaim(SouthKing(), "c_town_a", world, neighbors, Prices).Refusal);
        Assert.AreEqual(ClaimRefusal.TooFar, service.QuoteClaim(SouthKing(), "b_castle_a", world, neighbors, Prices).Refusal);

        // Bordering a realm's town is not enough to claim the duchy or the realm above it.
        Assert.AreEqual(ClaimRefusal.NoFoothold, service.QuoteClaim(SouthKing(), duchy, world, neighbors, Prices).Refusal);
        Assert.AreEqual(ClaimRefusal.NoFoothold, service.QuoteClaim(SouthKing(), "k_north", world, neighbors, Prices).Refusal);

        world.Owners["town_c"] = "king_s"; // the town first: its county is a foothold in the duchy
        var onDuchy = service.QuoteClaim(SouthKing(), duchy, world, neighbors, Prices);
        Assert.IsTrue(onDuchy.Allowed, onDuchy.Refusal.ToString());
        Assert.AreEqual(Prices.BuyPrice(TitleRank.Duchy), onDuchy.Cost);
        Assert.AreEqual(ClaimRefusal.NoFoothold, service.QuoteClaim(SouthKing(), "k_north", world, neighbors, Prices).Refusal,
            "a county is no foothold in the realm: the duchy between them is");

        service.Ledger.SetHolder(duchy, "king_s"); // then the duchy: a foothold in the realm
        var onRealm = service.QuoteClaim(SouthKing(), "k_north", world, neighbors, Prices);
        Assert.IsTrue(onRealm.Allowed, onRealm.Refusal.ToString());
        Assert.AreEqual(500, onRealm.Cost);
    }

    [TestMethod]
    public void A_vassals_title_is_a_foothold_too()
    {
        var service = Service();
        var world = new FakeWorld();
        var duchy = service.Catalog.Get("c_town_c")!.ParentId!;
        service.Ledger.SetHolder("c_town_s", "knight"); // the south king's vassal: c_town_s lies under k_south
        service.Ledger.SetHolder("c_town_c", "knight"); // and holds the duchy's county by right
        world.Clans["knight"] = "clan_k";
        world.Kingdoms["knight"] = "south";
        world.Rulers["south"] = "nobody"; // so king_s counts only his own vassals, not every lord of the realm
        service.Ledger.SetHolder("k_south", null);
        Assert.AreEqual(ClaimRefusal.NoFoothold, service.QuoteClaim(SouthKing(), duchy, world, Neighbors(1), Prices).Refusal,
            "the knight is nobody's vassal while k_south is vacant");

        service.Ledger.SetHolder("k_south", "king_s");
        var quote = service.QuoteClaim(SouthKing(), duchy, world, Neighbors(1), Prices);
        Assert.IsTrue(quote.Allowed, "the knight is king_s's vassal: " + quote.Refusal);
    }

    [TestMethod]
    public void Lordships_high_titles_and_unknown_titles_are_not_for_sale()
    {
        var service = Service();
        var buyer = new ClaimBuyer("king_s", "clan_s", "south", new[] { "town_s", "town_a" });

        Assert.AreEqual(ClaimRefusal.NotForSale, service.QuoteClaim(buyer, "l_village_a1", new FakeWorld(), Neighbors(5), Prices).Refusal);
        Assert.AreEqual(ClaimRefusal.NotForSale, service.QuoteClaim(buyer, "nothing", new FakeWorld(), Neighbors(5), Prices).Refusal);
    }

    [TestMethod]
    public void No_claims_inside_the_own_realm_or_clan()
    {
        var service = Service();
        var world = new FakeWorld();
        var lordB = new ClaimBuyer("lord_b", "clan_b", "north", new[] { "town_b", "castle_a" });

        Assert.AreEqual(ClaimRefusal.OwnRealm, service.QuoteClaim(lordB, "c_town_a", world, Neighbors(5), Prices).Refusal);
        Assert.AreEqual(ClaimRefusal.Held, service.QuoteClaim(lordB, "c_town_b", world, Neighbors(5), Prices).Refusal);

        world.Clans["lord_c"] = "clan_s";
        Assert.AreEqual(ClaimRefusal.OwnClan, service.QuoteClaim(SouthKing(), "c_town_c", world, Neighbors(1), Prices).Refusal);
    }

    [TestMethod]
    public void A_fief_of_another_clan_of_the_own_realm_cannot_be_claimed_even_when_its_holder_by_right_is_foreign()
    {
        var service = Service();
        var world = new FakeWorld();
        // Taken by the south and given to another southern clan; lord_c of the north still holds the title.
        world.Owners["town_c"] = "lord_s";
        world.Clans["lord_s"] = "clan_s2";
        world.Kingdoms["lord_s"] = "south";

        Assert.AreEqual(ClaimRefusal.OwnRealm, service.QuoteClaim(SouthKing(), "c_town_c", world, Neighbors(1), Prices).Refusal);
    }

    [TestMethod]
    public void A_fief_the_own_clan_owns_can_be_claimed_and_usurped_inside_the_own_realm()
    {
        var service = Service();
        var world = new FakeWorld();
        // Taken by the south, or granted on to the southern king; lord_c of the north still holds the title.
        world.Owners["town_c"] = "king_s";
        var buyer = new ClaimBuyer("king_s", "clan_s", "south", new[] { "town_s", "town_c" });

        var quote = service.QuoteClaim(buyer, "c_town_c", world, Neighbors(1), Prices);
        Assert.IsTrue(quote.Allowed, quote.Refusal.ToString());
        CollectionAssert.Contains(service.ClaimOffers(buyer, world, Neighbors(1), Prices).Select(o => o.Title.Id).ToList(), "c_town_c");

        service.BuyClaim(buyer, "c_town_c", 1, world, Neighbors(1), Prices);
        Assert.IsTrue(service.QuoteUsurp("king_s", "clan_s", "c_town_c", world, Prices).Allowed);
        service.Usurp("king_s", "clan_s", "c_town_c", world, Prices);
        Assert.AreEqual("king_s", service.Ledger.HolderOf("c_town_c"));
    }

    [TestMethod]
    public void A_fief_the_own_clan_owns_can_be_claimed_from_a_holder_of_the_same_realm_but_not_of_the_same_clan()
    {
        var service = Service();
        var world = new FakeWorld();
        world.Owners["town_c"] = "king_s"; // granted to the king's clan while lord_c, now of the south, kept the title
        world.Kingdoms["lord_c"] = "south";
        var buyer = new ClaimBuyer("king_s", "clan_s", "south", new[] { "town_s", "town_c" });

        Assert.IsTrue(service.QuoteClaim(buyer, "c_town_c", world, Neighbors(1), Prices).Allowed);

        world.Clans["lord_c"] = "clan_s";
        Assert.AreEqual(ClaimRefusal.OwnClan, service.QuoteClaim(buyer, "c_town_c", world, Neighbors(1), Prices).Refusal);
    }

    [TestMethod]
    public void Buying_records_a_fabricated_claim_once()
    {
        var service = Service();
        var world = new FakeWorld();

        var events = service.BuyClaim(SouthKing(), "c_town_c", 7, world, Neighbors(1), Prices);

        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(TitleEventKind.ClaimGained, events[0].Kind);
        Assert.AreEqual(TitleEventReason.Purchase, events[0].Reason);
        Assert.AreEqual("lord_c", events[0].PreviousHolderId);
        var claim = service.Ledger.ClaimsOn("c_town_c").Single();
        Assert.AreEqual(ClaimKind.Fabricated, claim.Kind);
        Assert.AreEqual(7, claim.CampaignDay);
        Assert.AreEqual(ClaimRefusal.Claimed, service.QuoteClaim(SouthKing(), "c_town_c", world, Neighbors(1), Prices).Refusal);
        Assert.AreEqual(0, service.BuyClaim(SouthKing(), "c_town_c", 8, world, Neighbors(1), Prices).Count);
    }

    [TestMethod]
    public void Claim_offers_list_what_can_be_bought_cheapest_first()
    {
        var service = Service();

        var world = new FakeWorld();

        var offers = service.ClaimOffers(SouthKing(), world, Neighbors(1), Prices);
        Assert.AreEqual("c_town_c", offers[0].Title.Id);
        Assert.IsFalse(offers.Any(o => o.Title.SettlementId == null), "no duchy or realm without a foothold below it");
        Assert.IsFalse(offers.Any(o => o.Title.KingdomId == "south"), "the buyer's own realm is never offered");

        service.Ledger.SetHolder(service.Catalog.Get("c_town_c")!.ParentId!, "king_s");
        offers = service.ClaimOffers(SouthKing(), world, Neighbors(1), Prices);
        Assert.AreEqual("c_town_c", offers[0].Title.Id);
        Assert.AreEqual("k_north", offers[offers.Count - 1].Title.Id);
    }

    [TestMethod]
    public void Usurping_needs_a_claim_and_the_fief()
    {
        var service = Service();
        var world = new FakeWorld();
        Assert.AreEqual(ClaimRefusal.NoClaim, service.QuoteUsurp("king_s", "clan_s", "c_town_c", world, Prices).Refusal);

        service.BuyClaim(SouthKing(), "c_town_c", 1, world, Neighbors(1), Prices);
        Assert.AreEqual(ClaimRefusal.NotOwned, service.QuoteUsurp("king_s", "clan_s", "c_town_c", world, Prices).Refusal);
        Assert.AreEqual(0, service.Usurp("king_s", "clan_s", "c_town_c", world, Prices).Count);

        world.Owners["town_c"] = "king_s";
        var quote = service.QuoteUsurp("king_s", "clan_s", "c_town_c", world, Prices);
        Assert.IsTrue(quote.Allowed, quote.Refusal.ToString());
        Assert.AreEqual(100, quote.Cost);

        var events = service.Usurp("king_s", "clan_s", "c_town_c", world, Prices);
        Assert.AreEqual(TitleEventReason.Usurpation, events.Single().Reason);
        Assert.AreEqual("lord_c", events[0].PreviousHolderId);
        Assert.AreEqual("king_s", service.Ledger.HolderOf("c_town_c"));
        Assert.AreEqual(0, service.Ledger.ClaimsOn("c_town_c").Count, "the claim is spent");
    }

    [TestMethod]
    public void A_clan_members_fief_counts_and_the_old_holders_village_lordships_go_along()
    {
        var service = Service();
        var world = new FakeWorld();
        service.OnFiefOwnerChanged("castle_a", "rebel", "lord_b", TransferKind.Conquest, 3);
        world.Owners["castle_a"] = world.Owners["village_ca"] = "rebel_leader";
        world.Clans["rebel"] = world.Clans["rebel_leader"] = "clan_r";

        var events = service.Usurp("rebel", "clan_r", "b_castle_a", world, Prices);

        CollectionAssert.AreEquivalent(new[] { "b_castle_a", "l_village_ca" }, events.Select(e => e.TitleId).ToArray());
        Assert.AreEqual("rebel", service.Ledger.HolderOf("l_village_ca"));
    }

    [TestMethod]
    public void A_duchy_is_usurped_by_holding_more_than_half_of_its_vassal_titles()
    {
        var service = Service();
        var world = new FakeWorld();
        var duchy = service.Catalog.Get("c_town_a")!.ParentId!;
        var vassals = service.Catalog.ChildrenOf(duchy);
        service.Ledger.AddClaim(new TitleClaim(duchy, "pretender", ClaimKind.Fabricated, 1));
        Assert.AreEqual(ClaimRefusal.TooFewVassals, service.QuoteUsurp("pretender", null, duchy, world, Prices).Refusal);

        foreach (var vassal in vassals.Take(vassals.Count / 2 + 1))
            service.Ledger.SetHolder(vassal.Id, "pretender");

        Assert.IsTrue(service.QuoteUsurp("pretender", null, duchy, world, Prices).Allowed);
        service.Usurp("pretender", null, duchy, world, Prices);
        Assert.AreEqual("pretender", service.Ledger.HolderOf(duchy));
    }

    /// <summary>Gives every vassal title of the duchy to the hero, so a test can take titles away one at a time.</summary>
    private static (RealmsService Service, FakeWorld World, string Duchy, List<TitleDefinition> Vassals) DuchyClaimedBy(string heroId)
    {
        var service = Service();
        var duchy = service.Catalog.Get("c_town_a")!.ParentId!;
        service.Ledger.AddClaim(new TitleClaim(duchy, heroId, ClaimKind.Fabricated, 1));
        return (service, new FakeWorld(), duchy, service.Catalog.ChildrenOf(duchy).ToList());
    }

    [TestMethod]
    public void A_ruler_counts_the_titles_and_fiefs_of_every_lord_serving_the_realm()
    {
        var (service, world, duchy, vassals) = DuchyClaimedBy("king_s");
        foreach (var vassal in vassals)
            service.Ledger.SetHolder(vassal.Id, "vassal_s");
        world.Clans["vassal_s"] = "clan_v";
        world.Kingdoms["vassal_s"] = "north";
        Assert.AreEqual(ClaimRefusal.TooFewVassals, service.QuoteUsurp("king_s", "clan_s", duchy, world, Prices).Refusal, "a foreign lord's titles");

        world.Kingdoms["vassal_s"] = "south";
        Assert.IsTrue(service.QuoteUsurp("king_s", "clan_s", duchy, world, Prices).Allowed, "the vassal clan joined the south");

        world.Rulers["south"] = "someone_else";
        Assert.AreEqual(ClaimRefusal.TooFewVassals, service.QuoteUsurp("king_s", "clan_s", duchy, world, Prices).Refusal,
            "a lord who does not rule counts only his own vassals");
    }

    [TestMethod]
    public void A_fief_given_to_a_vassal_clan_counts_before_its_title_is_pressed()
    {
        var (service, world, duchy, vassals) = DuchyClaimedBy("king_s");
        world.Clans["vassal_s"] = "clan_v";
        world.Kingdoms["vassal_s"] = "south";
        Assert.IsTrue(vassals.All(v => v.SettlementId != null), "the test duchy's vassals are fief titles");
        Assert.AreEqual(ClaimRefusal.TooFewVassals, service.QuoteUsurp("king_s", "clan_s", duchy, world, Prices).Refusal);

        foreach (var vassal in vassals)
            world.Owners[vassal.SettlementId!] = "vassal_s"; // the north's lords still hold the titles by right

        Assert.IsTrue(service.QuoteUsurp("king_s", "clan_s", duchy, world, Prices).Allowed);
    }

    [TestMethod]
    public void A_lord_counts_his_clan_and_his_vassals_and_their_vassals()
    {
        var (service, world, duchy, vassals) = DuchyClaimedBy("duke");
        world.Clans["duke"] = "clan_d";
        world.Kingdoms["duke"] = "north";
        foreach (var vassal in vassals)
            service.Ledger.SetHolder(vassal.Id, "squire");
        world.Clans["squire"] = "clan_q";
        world.Kingdoms["squire"] = "north";
        Assert.AreEqual(ClaimRefusal.TooFewVassals, service.QuoteUsurp("duke", "clan_d", duchy, world, Prices).Refusal);

        // The duke holds the south's realm title and the knight a county below it, so the knight is his vassal. The knight
        // also holds one of the duchy's counties, and the squire the barony below that county: the squire is the knight's
        // vassal. All of them serve the north.
        Assert.IsTrue(vassals.Any(v => v.Id == "c_town_b"), "c_town_b is one of the duchy's vassal titles");
        Assert.AreEqual("k_south", service.Catalog.Get("c_town_s")!.ParentId);
        Assert.AreEqual("c_town_b", service.Catalog.Get("b_castle_a")!.ParentId);
        service.Ledger.SetHolder("k_south", "duke");
        service.Ledger.SetHolder("c_town_s", "knight");
        service.Ledger.SetHolder("c_town_b", "knight");
        service.Ledger.SetHolder("b_castle_a", "squire");
        world.Clans["knight"] = "clan_k";
        world.Kingdoms["knight"] = "north";

        Assert.IsTrue(service.QuoteUsurp("duke", "clan_d", duchy, world, Prices).Allowed, "a vassal's vassal counts");

        world.Kingdoms["squire"] = "south";
        Assert.AreEqual(ClaimRefusal.TooFewVassals, service.QuoteUsurp("duke", "clan_d", duchy, world, Prices).Refusal,
            "a lord who left the realm is nobody's vassal");

        world.Clans["squire"] = "clan_d";
        Assert.IsTrue(service.QuoteUsurp("duke", "clan_d", duchy, world, Prices).Allowed, "the duke's own clan counts wherever it serves");
    }

    [TestMethod]
    public void A_title_without_an_heir_goes_to_its_strongest_claimant()
    {
        var service = Service();
        service.Ledger.AddClaim(new TitleClaim("c_town_c", "schemer", ClaimKind.Fabricated, 1));
        service.Ledger.AddClaim(new TitleClaim("c_town_c", "late_conqueror", ClaimKind.Conquest, 5));
        service.Ledger.AddClaim(new TitleClaim("c_town_c", "conqueror", ClaimKind.Conquest, 2));

        var events = service.OnHeroDied("lord_c", new FamilySnapshot(new[] { new HeroInfo("lord_c", false, false, 60, null) }));

        var e = events.Single(x => x.TitleId == "c_town_c");
        Assert.AreEqual(TitleEventReason.Claim, e.Reason);
        Assert.AreEqual("conqueror", service.Ledger.HolderOf("c_town_c"));
        Assert.AreEqual(2, service.Ledger.ClaimsOn("c_town_c").Count, "only the winner's claim is spent");
    }

    [TestMethod]
    public void A_bought_claim_takes_up_a_vacated_duchy_only_when_its_claimant_could_press_it()
    {
        var (service, world, duchy, vassals) = DuchyClaimedBy("pretender");
        var family = new FamilySnapshot(new[] { new HeroInfo("old_duke", false, false, 60, null) });
        service.Ledger.SetHolder(duchy, "old_duke");

        service.OnHeroDied("old_duke", family, world);
        Assert.IsNull(service.Ledger.HolderOf(duchy), "a claim bought from far below does not inherit the duchy");
        Assert.AreEqual(1, service.Ledger.ClaimsOn(duchy).Count, "the claim stays, to be pressed later");

        foreach (var vassal in vassals.Take(vassals.Count / 2 + 1))
            service.Ledger.SetHolder(vassal.Id, "pretender");
        service.Ledger.SetHolder(duchy, "old_duke");
        Assert.IsNull(service.StrongestClaimant(duchy), "without the campaign a bought claim on a duchy never counts");

        var events = service.OnHeroDied("old_duke", family, world);
        Assert.AreEqual("pretender", service.Ledger.HolderOf(duchy));
        Assert.AreEqual(TitleEventReason.Claim, events.Single(e => e.TitleId == duchy).Reason);
    }

    [TestMethod]
    public void A_blood_claim_takes_up_a_vacated_duchy_without_its_lands()
    {
        var (service, world, duchy, _) = DuchyClaimedBy("pretender");
        service.Ledger.AddClaim(new TitleClaim(duchy, "grandson", ClaimKind.Ancestral, 2));
        service.Ledger.SetHolder(duchy, "old_duke");

        service.OnHeroDied("old_duke", new FamilySnapshot(new[] { new HeroInfo("old_duke", false, false, 60, null) }), world);

        Assert.AreEqual("grandson", service.Ledger.HolderOf(duchy));
    }

    [TestMethod]
    public void A_waiting_title_whose_clan_is_gone_goes_to_a_claimant()
    {
        var config = RealmsConfig.Defaults();
        config.DefaultLaws = new TitleLaws(SuccessionLaw.ClanLeader, GenderLaw.Absolute);
        var service = new RealmsService();
        service.Initialize(TestWorld.Build(), config);
        service.Ledger.AddClaim(new TitleClaim("c_town_a", "schemer", ClaimKind.Fabricated, 1));
        service.OnHeroDied("king_n", new FamilySnapshot(new[] { new HeroInfo("king_n", false, false, 70, "royals") }));

        var events = service.ResolvePending(new NoLeaders());

        Assert.AreEqual("schemer", service.Ledger.HolderOf("c_town_a"));
        Assert.AreEqual(TitleEventReason.Claim, events.Single(x => x.TitleId == "c_town_a").Reason);
        Assert.IsNull(service.Ledger.HolderOf("k_north"), "a waiting title nobody claims still falls vacant");
    }

    private sealed class NoLeaders : IRealmLeaders
    {
        public string? ClanLeaderOf(string clanId) => null;
        public bool ClanExists(string clanId) => false;
        public string? RulerOf(string kingdomId) => null;
        public bool KingdomExists(string kingdomId) => false;
    }

    [TestMethod]
    public void Reads_claim_prices_and_settings()
    {
        var result = RealmsConfigXml.Parse(XDocument.Parse(@"<Realms>
  <Claims neighbors='3' relation='-5' usurp_relation='-15' ai='false' ai_chance='0.2' ai_gold_per_point='50' ai_reserve='1.5'>
    <Rank rank='Barony' buy='30' usurp='40' />
    <Rank rank='Kingdom' usurp='300' />
  </Claims>
</Realms>"));

        Assert.AreEqual(0, result.Warnings.Count, string.Join("; ", result.Warnings));
        var claims = result.Config.Claims;
        Assert.AreEqual(3, claims.Neighbors);
        Assert.AreEqual(-5, claims.BuyRelation);
        Assert.AreEqual(-15, claims.UsurpRelation);
        Assert.IsFalse(claims.AiEnabled);
        Assert.AreEqual(0.2f, claims.AiChance);
        Assert.AreEqual(50, claims.AiGoldPerPoint);
        Assert.AreEqual(1.5f, claims.AiReserve);
        Assert.AreEqual(30, claims.BuyPrice(TitleRank.Barony));
        Assert.AreEqual(40, claims.UsurpCost(TitleRank.Barony));
        Assert.IsNull(claims.BuyPrice(TitleRank.Kingdom), "a listed rank without buy cannot be bought");
        Assert.AreEqual(300, claims.UsurpCost(TitleRank.Kingdom));
        Assert.AreEqual(80, claims.BuyPrice(TitleRank.County), "unlisted ranks keep their defaults");
    }

    [TestMethod]
    public void The_shipped_file_keeps_the_agreed_prices()
    {
        var path = TAOM.Tests.Infrastructure.RepoPaths.RepoPath("Main", "_Module", "ModuleData", "realms_titles.xml");
        var claims = RealmsConfigXml.Parse(XDocument.Load(path)).Config.Claims;

        Assert.AreEqual(40, claims.BuyPrice(TitleRank.Barony));
        Assert.AreEqual(80, claims.BuyPrice(TitleRank.County));
        Assert.AreEqual(200, claims.BuyPrice(TitleRank.Duchy));
        Assert.AreEqual(500, claims.BuyPrice(TitleRank.Kingdom));
        Assert.IsNull(claims.BuyPrice(TitleRank.Lordship));
    }
}
