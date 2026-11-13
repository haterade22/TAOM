using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Realms;
using TAOM.Features.Realms.Domain.Config;
using TAOM.Features.Realms.Domain.Ledger;
using TAOM.Features.Realms.Domain.Services;
using TAOM.Features.Realms.Domain.Titles;
using TAOM.Features.Realms.Hooks;

namespace TAOM.Tests.Features.Realms;

/// <summary>
/// What keeps the weekly AI sweep (titles formed, claims pressed and bought) from freezing the map: the shortcuts give
/// the same answers as the long way round.
/// </summary>
[TestClass]
public class WeeklySweepTests
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
            ["king_n"] = "clan_n", ["lord_b"] = "clan_b", ["lord_c"] = "clan_c", ["king_s"] = "clan_s", ["heir"] = "clan_h",
        };

        public Dictionary<string, string> Kingdoms { get; } = new()
        {
            ["king_n"] = "north", ["lord_b"] = "north", ["lord_c"] = "north", ["king_s"] = "south", ["heir"] = "north",
        };

        public Dictionary<string, string> Rulers { get; } = new() { ["north"] = "king_n", ["south"] = "king_s" };

        public string? OwnerOf(string settlementId) => Owners.TryGetValue(settlementId, out var h) ? h : null;
        public string? KingdomOfSettlement(string settlementId) => OwnerOf(settlementId) is { } owner ? KingdomOfHero(owner) : null;
        public string? KingdomOfHero(string heroId) => Kingdoms.TryGetValue(heroId, out var k) ? k : null;
        public string? ClanOf(string heroId) => Clans.TryGetValue(heroId, out var c) ? c : null;
        public string? RulerOf(string kingdomId) => Rulers.TryGetValue(kingdomId, out var h) ? h : null;
        public string? CultureOf(string heroId) => null;
    }

    private static RealmsService Service(RealmsConfig? config = null)
    {
        var service = new RealmsService();
        service.Initialize(TestWorld.Build(), config ?? RealmsConfig.Defaults());
        return service;
    }

    // The holder index

    [TestMethod]
    public void Titles_held_by_a_hero_follow_every_change_of_holder()
    {
        var service = Service();
        var ledger = service.Ledger;
        var ids = service.Catalog.All.Select(t => t.Id).ToList();
        var heroes = new[] { "king_n", "lord_b", "lord_c", "king_s", "heir" };

        for (var i = 0; i < ids.Count * 3; i++)
        {
            var title = ids[(i * 7) % ids.Count];
            ledger.SetHolder(title, i % 5 == 4 ? null : heroes[(i * 3) % heroes.Length]);
            if (i % 4 == 0)
                ledger.SetHolder(title, ledger.HolderOf(title)); // the same holder again changes nothing
        }

        foreach (var hero in heroes.Concat(new[] { "nobody" }))
            CollectionAssert.AreEqual(
                ledger.Holders.Where(p => p.Value == hero).Select(p => p.Key).OrderBy(id => id).ToList(),
                ledger.TitlesHeldBy(hero).ToList(), hero);
    }

    // Who is not quoted

    [TestMethod]
    public void The_cheapest_usurpation_is_the_lowest_usurp_cost_at_the_options_percentage()
    {
        var claims = new ClaimsConfig();
        Assert.AreEqual(claims.UsurpInfluence.Values.Min(), RealmsService.CheapestUsurpCost(claims));

        claims.InfluencePercent = 50;
        Assert.AreEqual(claims.UsurpInfluence.Keys.Select(claims.UsurpCost).Min(), RealmsService.CheapestUsurpCost(claims));

        claims.UsurpInfluence.Clear();
        Assert.IsNull(RealmsService.CheapestUsurpCost(claims), "nothing can be usurped");
    }

    [TestMethod]
    public void The_cheapest_formation_counts_duchies_and_higher_and_the_hero_s_own_coronation()
    {
        var config = RealmsConfig.Defaults();
        var service = Service(config);
        var claims = new ClaimsConfig();
        Assert.AreEqual(claims.UsurpCost(TitleRank.Duchy), service.CheapestFormCost("heir", claims), "a lordship or county is never formed");

        config.Coronations.Add(new CoronationConfig("k_north", "heir", "House", null, null, new string[0], new string[0],
            null, 4, 33, 0, 0, influence: 30));
        Assert.AreEqual(30, service.CheapestFormCost("heir", claims));
        Assert.AreEqual(claims.UsurpCost(TitleRank.Duchy), service.CheapestFormCost("lord_b", claims), "another hero's coronation");

        foreach (var rank in new[] { TitleRank.Duchy, TitleRank.Kingdom, TitleRank.Empire, TitleRank.Union })
            claims.UsurpInfluence.Remove(rank);
        Assert.AreEqual(30, service.CheapestFormCost("heir", claims));
        Assert.IsNull(service.CheapestFormCost("lord_b", claims), "nothing can be formed");
    }

    [TestMethod]
    public void Every_formation_offer_costs_at_least_the_cheapest_formation()
    {
        var config = RealmsConfig.Defaults();
        config.VacantThrones.Add("north");
        var service = Service(config);
        var world = new FakeWorld();
        var claims = new ClaimsConfig();
        config.Coronations.Add(new CoronationConfig("k_north", "heir", "House", null, null, new string[0], new string[0],
            null, 4, 33, 0, 0, influence: 300));
        service.Ledger.AddClaim(new TitleClaim("k_north", "heir", ClaimKind.Ancestral, 0));
        foreach (var duchy in service.Catalog.All.Where(t => t.Rank == TitleRank.Duchy))
            service.Ledger.SetHolder(duchy.Id, null);

        foreach (var hero in new[] { "king_n", "heir" })
        {
            var offers = service.FormOffers(hero, world, claims);
            Assert.IsTrue(offers.Count > 0, hero);
            Assert.IsTrue(offers.All(o => o.Cost >= service.CheapestFormCost(hero, claims)), hero);
        }
    }

    // One side per offer list

    [TestMethod]
    public void Usurp_offers_quote_each_claim_as_a_single_quote_does()
    {
        var service = Service();
        var world = new FakeWorld();
        var claims = new ClaimsConfig();
        var duchy = service.Catalog.Get("c_town_b")!.ParentId!;
        // lord_b's side holds every land of the duchy, held by a southern lord: pressing it is allowed.
        foreach (var land in service.Catalog.ChildrenOf(duchy))
            service.Ledger.SetHolder(land.Id, "lord_b");
        service.Ledger.SetHolder(duchy, "king_s");
        foreach (var title in new[] { duchy, "k_south", "c_town_s", "c_town_c" })
            service.Ledger.AddClaim(new TitleClaim(title, "lord_b", ClaimKind.Fabricated, 0));

        var expected = service.Ledger.ClaimsOf("lord_b")
            .Select(c => (c.TitleId, Quote: service.QuoteUsurp("lord_b", "clan_b", c.TitleId, world, claims)))
            .Where(o => o.Quote.Allowed).Select(o => (o.TitleId, o.Quote.Cost)).ToList();

        CollectionAssert.Contains(expected.Select(o => o.TitleId).ToList(), duchy);
        CollectionAssert.AreEqual(expected, service.UsurpOffers("lord_b", "clan_b", world, claims).ToList());
        Assert.AreEqual(0, service.UsurpOffers("king_s", "clan_s", world, claims).Count, "no claims, no offers");
    }

    // The sweep's hero table and the party names

    [TestMethod]
    public void The_hero_table_keeps_the_first_hero_of_each_id_as_a_search_would()
    {
        var heroes = new[] { ("a", "dead a"), ((string?)null, "no id"), ("b", "b"), ("a", "living a") };

        var table = ClaimWorld.IndexFirst(heroes, h => h.Item1);

        Assert.AreEqual(2, table.Count);
        Assert.AreEqual("dead a", table["a"].Item2, "the first of an id, as Hero.Find returns it");
        Assert.AreEqual("b", table["b"].Item2);
    }

    [TestMethod]
    public void Party_names_are_built_again_for_new_and_former_holders_but_not_for_claimants()
    {
        var events = new[]
        {
            new TitleEvent(TitleEventKind.HolderChanged, TitleEventReason.Usurpation, "c_town_a", "lord_b", "king_n"),
            new TitleEvent(TitleEventKind.HolderChanged, TitleEventReason.Formation, "k_north", "lord_c", null),
            new TitleEvent(TitleEventKind.OfficeChanged, TitleEventReason.Appointment, "steward", "heir", "duke"),
            new TitleEvent(TitleEventKind.ClaimGained, TitleEventReason.Purchase, "c_town_s", "buyer", "king_s"),
        };

        var owners = PartyNames.OwnersAffectedBy(events);

        CollectionAssert.AreEquivalent(new[] { "lord_b", "king_n", "lord_c", "heir", "duke" }, owners.ToList());
    }
}
