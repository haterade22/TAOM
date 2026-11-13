using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Realms.Domain.Config;
using TAOM.Features.Realms.Domain.Laws;
using TAOM.Features.Realms.Domain.Ledger;
using TAOM.Features.Realms.Domain.Persistence;
using TAOM.Features.Realms.Domain.Services;
using TAOM.Features.Realms.Domain.Succession;
using TAOM.Features.Realms.Domain.Titles;

namespace TAOM.Tests.Features.Realms;

/// <summary>Grants, revocations, forming and restoring titles, unions, vacant thrones, offices, the chronicle and weekly consequences.</summary>
[TestClass]
public class FeudalTests
{
    private sealed class FakeWorld : IOfficeWorld, IRealmLeaders
    {
        public Dictionary<string, string> Owners { get; } = new()
        {
            ["town_a"] = "king_n", ["town_b"] = "lord_b", ["town_c"] = "lord_c", ["castle_a"] = "lord_b",
            ["village_a1"] = "king_n", ["village_ca"] = "lord_b", ["town_s"] = "king_s",
        };

        public Dictionary<string, string> Clans { get; } = new()
        {
            ["king_n"] = "clan_n", ["prince"] = "clan_n", ["lord_b"] = "clan_b", ["lord_c"] = "clan_c",
            ["king_s"] = "clan_s", ["duke"] = "clan_d", ["heir"] = "clan_h",
        };

        public Dictionary<string, string> Kingdoms { get; } = new()
        {
            ["king_n"] = "north", ["prince"] = "north", ["lord_b"] = "north", ["lord_c"] = "north",
            ["king_s"] = "south", ["duke"] = "north", ["heir"] = "north",
        };

        public Dictionary<string, string> Rulers { get; } = new() { ["north"] = "king_n", ["south"] = "king_s" };
        public HashSet<string> Dead { get; } = new();

        public string? OwnerOf(string settlementId) => Owners.TryGetValue(settlementId, out var h) ? h : null;
        public string? KingdomOfSettlement(string settlementId) => OwnerOf(settlementId) is { } owner ? KingdomOfHero(owner) : null;
        public string? KingdomOfHero(string heroId) => Kingdoms.TryGetValue(heroId, out var k) ? k : null;
        public string? ClanOf(string heroId) => Clans.TryGetValue(heroId, out var c) ? c : null;
        public bool IsAlive(string heroId) => !Dead.Contains(heroId);
        public string? RulerOf(string kingdomId) => Rulers.TryGetValue(kingdomId, out var h) ? h : null;
        public Dictionary<string, string> Cultures { get; } = new();
        public string? CultureOf(string heroId) => Cultures.TryGetValue(heroId, out var c) ? c : null;
        public string? ClanLeaderOf(string clanId) => null;
        public bool ClanExists(string clanId) => true;
        public bool KingdomExists(string kingdomId) => Rulers.ContainsKey(kingdomId);
    }

    private static RealmsService Service(RealmsConfig? config = null)
    {
        var service = new RealmsService();
        service.Initialize(TestWorld.Build(), config ?? RealmsConfig.Defaults());
        return service;
    }

    private static string DuchyOf(RealmsService service, string titleId) => service.Catalog.Get(titleId)!.ParentId!;

    private static RealmsConfig WithVacantNorth()
    {
        var config = RealmsConfig.Defaults();
        config.VacantThrones.Add("north");
        return config;
    }

    private static RealmsConfig WithOffices()
    {
        var config = RealmsConfig.Defaults();
        config.Offices.Add(new OfficeConfig("steward", "north", "Steward", "Steward {NAME}", null, "the North", TitleRank.Kingdom, true, 5, null));
        config.Offices.Add(new OfficeConfig("marshal", "north", "Marshal", "Marshal {NAME}", null, null, TitleRank.Duchy, false, 2, "lord_c"));
        return config;
    }

    // Grants

    [TestMethod]
    public void A_title_granted_within_the_clan_takes_the_titles_below_it()
    {
        var service = Service();
        var world = new FakeWorld();

        Assert.AreEqual(GrantMode.WithVassals, service.QuoteGrant("king_n", "clan_n", "prince", "c_town_a", world).Mode);
        var events = service.Grant("king_n", "clan_n", "prince", "c_town_a", world);

        Assert.AreEqual("prince", service.Ledger.HolderOf("c_town_a"));
        Assert.AreEqual("prince", service.Ledger.HolderOf("l_village_a1"));
        Assert.IsTrue(events.All(e => e.Reason == TitleEventReason.Grant));
    }

    [TestMethod]
    public void A_duchy_granted_to_another_clan_passes_alone_and_a_fief_title_needs_the_fief()
    {
        var service = Service();
        var world = new FakeWorld();
        var duchy = DuchyOf(service, "c_town_a");
        service.Ledger.SetHolder(duchy, "king_n");

        Assert.AreEqual(GrantMode.TitleOnly, service.QuoteGrant("king_n", "clan_n", "lord_c", duchy, world).Mode);
        service.Grant("king_n", "clan_n", "lord_c", duchy, world);
        Assert.AreEqual("lord_c", service.Ledger.HolderOf(duchy));
        Assert.AreEqual("king_n", service.Ledger.HolderOf("c_town_a"), "the duchy's lands stay with the giver");

        Assert.AreEqual(GrantMode.ByFief, service.QuoteGrant("king_n", "clan_n", "lord_b", "c_town_a", world).Mode);
        Assert.AreEqual(0, service.Grant("king_n", "clan_n", "lord_b", "c_town_a", world).Count, "the game's fief gift moves it");
        world.Owners["town_a"] = "lord_c";
        Assert.AreEqual(ClaimRefusal.NotOwned, service.QuoteGrant("king_n", "clan_n", "lord_b", "c_town_a", world).Refusal);
    }

    [TestMethod]
    public void Grants_are_refused_for_realms_other_holders_strangers_and_lone_lordships()
    {
        var service = Service();
        var world = new FakeWorld();

        Assert.AreEqual(ClaimRefusal.NotGrantable, service.QuoteGrant("king_n", "clan_n", "prince", "k_north", world).Refusal);
        Assert.AreEqual(ClaimRefusal.NotHolder, service.QuoteGrant("lord_b", "clan_b", "lord_c", "c_town_a", world).Refusal);
        Assert.AreEqual(ClaimRefusal.Recipient, service.QuoteGrant("king_n", "clan_n", "king_s", "c_town_a", world).Refusal);
        Assert.AreEqual(ClaimRefusal.Recipient, service.QuoteGrant("king_n", "clan_n", "king_n", "c_town_a", world).Refusal);
        Assert.AreEqual(ClaimRefusal.NotGrantable, service.QuoteGrant("king_n", "clan_n", "lord_b", "l_village_a1", world).Refusal);
    }

    // Revocations

    [TestMethod]
    public void A_liege_revokes_a_vassal_s_title_for_influence_with_its_village_lordships()
    {
        var service = Service();
        var world = new FakeWorld();
        var config = new FeudalConfig();

        var quote = service.QuoteRevoke("king_n", "clan_n", "b_castle_a", world, config);
        Assert.IsTrue(quote.Allowed, quote.Refusal.ToString());
        Assert.AreEqual(25, quote.Cost);

        service.Revoke("king_n", "clan_n", "b_castle_a", world, config);
        Assert.AreEqual("king_n", service.Ledger.HolderOf("b_castle_a"));
        Assert.AreEqual("king_n", service.Ledger.HolderOf("l_village_ca"));
        Assert.AreEqual(TitleEventReason.Revocation, service.Ledger.ChronicleOf("b_castle_a").Last().Reason);
    }

    [TestMethod]
    public void Revoking_is_free_within_the_clan_and_refused_without_a_liege_title_or_across_realms()
    {
        var service = Service();
        var world = new FakeWorld();
        var config = new FeudalConfig();
        service.Ledger.SetHolder("c_town_a", "prince");

        Assert.AreEqual(0, service.QuoteRevoke("king_n", "clan_n", "c_town_a", world, config).Cost);
        Assert.IsTrue(service.QuoteRevoke("king_n", "clan_n", "c_town_a", world, config).Allowed);
        Assert.AreEqual(ClaimRefusal.NotLiege, service.QuoteRevoke("king_s", "clan_s", "c_town_c", world, config).Refusal);
        world.Kingdoms["lord_c"] = "south";
        Assert.AreEqual(ClaimRefusal.OutsideRealm, service.QuoteRevoke("king_n", "clan_n", "c_town_c", world, config).Refusal);
        service.Ledger.SetHolder("c_town_c", null);
        Assert.AreEqual(ClaimRefusal.Vacant, service.QuoteRevoke("king_n", "clan_n", "c_town_c", world, config).Refusal);
        config.RevokeInfluence.Remove(TitleRank.Barony);
        Assert.AreEqual(ClaimRefusal.NotForSale, service.QuoteRevoke("king_n", "clan_n", "b_castle_a", world, config).Refusal);
    }

    // Forming and restoring

    [TestMethod]
    public void A_vacant_realm_is_formed_by_holding_most_of_its_counties()
    {
        var service = Service();
        service.Ledger.SetHolder("k_north", null);
        var claims = new ClaimsConfig();
        // Nobody stands behind king_n: he rules nothing and the other lords serve elsewhere.
        var world = new FakeWorld();
        world.Rulers.Remove("north");
        world.Kingdoms["lord_b"] = world.Kingdoms["lord_c"] = "south";

        Assert.AreEqual(ClaimRefusal.TooFewVassals, service.QuoteForm("king_n", "k_north", world, claims).Refusal);
        service.Ledger.SetHolder("c_town_b", "king_n");
        var quote = service.QuoteForm("king_n", "k_north", world, claims);
        Assert.IsTrue(quote.Allowed, quote.Refusal.ToString());
        Assert.AreEqual(claims.UsurpCost(TitleRank.Kingdom), quote.Cost);

        service.Form("king_n", "k_north", world, claims);
        Assert.AreEqual("king_n", service.Ledger.HolderOf("k_north"));
        Assert.AreEqual(TitleEventReason.Formation, service.Ledger.ChronicleOf("k_north").Last().Reason);
        Assert.AreEqual(ClaimRefusal.NotVacant, service.QuoteForm("king_n", "k_north", world, claims).Refusal);
    }

    [TestMethod]
    public void A_ruler_forms_a_vacant_title_from_the_counties_his_lords_hold()
    {
        var service = Service();
        service.Ledger.SetHolder("k_north", null);
        service.Ledger.SetHolder("c_town_a", "lord_b");

        Assert.IsTrue(service.QuoteForm("king_n", "k_north", new FakeWorld(), new ClaimsConfig()).Allowed,
            "king_n rules the north, and its lords hold every county");
    }

    [TestMethod]
    public void A_vacant_throne_starts_empty_is_never_formed_and_is_restored_by_an_ancestral_claimant()
    {
        var service = Service(WithVacantNorth());
        var world = new FakeWorld();
        var claims = new ClaimsConfig();
        service.Ledger.SetHolder("c_town_b", "king_n");

        Assert.IsNull(service.Ledger.HolderOf("k_north"));
        Assert.AreEqual(ClaimRefusal.RestoreOnly, service.QuoteForm("king_n", "k_north", world, claims).Refusal);
        Assert.AreEqual(ClaimRefusal.NoClaim, service.QuoteRestore("heir", "k_north", world, claims).Refusal);

        service.Ledger.AddClaim(new TitleClaim("k_north", "heir", ClaimKind.Ancestral, 0));
        world.Kingdoms["heir"] = "south";
        Assert.AreEqual(ClaimRefusal.NotServing, service.QuoteRestore("heir", "k_north", world, claims).Refusal);
        world.Kingdoms["heir"] = "north";
        Assert.IsTrue(service.QuoteRestore("heir", "k_north", world, claims).Allowed);

        service.Restore("heir", "k_north", world, claims);
        Assert.AreEqual("heir", service.Ledger.HolderOf("k_north"));
        Assert.AreEqual(TitleEventReason.Restoration, service.Ledger.ChronicleOf("k_north").Last().Reason);
    }

    // Coronations

    [TestMethod]
    public void A_coronation_sets_the_price_of_its_restore_for_its_claimant_only()
    {
        var config = WithVacantNorth();
        var service = Service(config);
        var world = new FakeWorld();
        var claims = new ClaimsConfig();
        service.Ledger.AddClaim(new TitleClaim("k_north", "heir", ClaimKind.Ancestral, 0));
        service.Ledger.AddClaim(new TitleClaim("k_north", "prince", ClaimKind.Ancestral, 0));
        var usual = service.QuoteRestore("heir", "k_north", world, claims).Cost;

        config.Coronations.Add(new CoronationConfig("k_north", "heir", "House", null, null, new string[0], new string[0],
            null, 4, 33, 0, 0, influence: 1000));

        Assert.AreEqual(1000, service.QuoteRestore("heir", "k_north", world, claims).Cost);
        Assert.AreEqual(usual, service.QuoteRestore("prince", "k_north", world, claims).Cost);

        // The MCM's Title Influence Cost scales the coronation's own price as well.
        claims.InfluencePercent = 250;
        Assert.AreEqual(2500, service.QuoteRestore("heir", "k_north", world, claims).Cost);
    }

    [TestMethod]
    public void A_crowned_king_takes_his_seat_with_its_lordships_from_the_former_house_only()
    {
        var config = WithVacantNorth();
        var service = Service(config);
        var duchy = DuchyOf(service, "c_town_a");
        var coronation = new CoronationConfig("k_north", "heir", "House", null, null, new[] { "town_a", "town_b" },
            new[] { duchy }, null, 4, 33, 0, 0);
        config.Coronations.Add(coronation);
        service.Ledger.SetHolder("c_town_a", "king_n");
        service.Ledger.SetHolder("l_village_a1", "prince");
        service.Ledger.SetHolder("c_town_b", "lord_b");
        service.Ledger.SetHolder(duchy, "king_n");
        var formerHouse = new HashSet<string> { "king_n", "prince" };

        Assert.AreSame(coronation, service.CoronationFor("k_north", "heir"));
        Assert.IsNull(service.CoronationFor("k_north", "prince"));
        var events = service.SeatKing(coronation, "heir", id => id != null && formerHouse.Contains(id));

        Assert.AreEqual("heir", service.Ledger.HolderOf("c_town_a"));
        Assert.AreEqual("heir", service.Ledger.HolderOf("l_village_a1"));
        Assert.AreEqual("heir", service.Ledger.HolderOf(duchy));
        Assert.AreEqual("lord_b", service.Ledger.HolderOf("c_town_b"), "a lord of another house keeps his title");
        Assert.AreEqual(3, events.Count);
        Assert.AreEqual(0, service.SeatKing(coronation, "heir", id => id != null && formerHouse.Contains(id)).Count);
    }

    [TestMethod]
    public void The_steward_keeps_his_office_when_the_restored_king_comes_to_rule()
    {
        var config = WithOffices();
        config.VacantThrones.Add("north");
        var service = Service(config);
        var world = new FakeWorld();
        service.EnsureOffices(world);
        Assert.AreEqual("king_n", service.Ledger.OfficeHolderOf("steward"), "the ruling steward holds it while the throne is empty");

        service.Ledger.SetHolder("k_north", "heir");
        world.Rulers["north"] = "heir";
        service.UpdateOffices(world, null);

        Assert.AreEqual("king_n", service.Ledger.OfficeHolderOf("steward"));
    }

    [TestMethod]
    public void A_new_ruler_of_an_elective_realm_does_not_take_up_a_vacant_throne()
    {
        var config = WithVacantNorth();
        config.KingdomLaws["north"] = new TitleLaws(SuccessionLaw.Elective, GenderLaw.Absolute);
        var service = Service(config);

        Assert.AreEqual(0, service.OnRealmRulerChanged("north", "steward_2").Count);
        Assert.IsNull(service.Ledger.HolderOf("k_north"));
    }

    [TestMethod]
    public void An_older_save_vacates_a_vacant_throne_its_ruler_holds()
    {
        var service = Service();
        service.Config = WithVacantNorth();
        var world = new FakeWorld();

        Assert.AreEqual(0, service.Migrate(2, world).Count, "a current save is left alone");
        var events = service.Migrate(1, world);

        Assert.AreEqual(TitleEventReason.Vacated, events.Single().Reason);
        Assert.IsNull(service.Ledger.HolderOf("k_north"));
        Assert.AreEqual("king_s", service.Ledger.HolderOf("k_south"));
    }

    // Unions

    [TestMethod]
    public void A_union_sits_above_its_realms_and_is_formed_by_holding_them()
    {
        var config = RealmsConfig.Defaults();
        config.Unions.Add(new UnionConfig("both", "Both", new[] { "k_north", "k_south" }));
        var service = Service(config);

        var union = service.Catalog.Get("u_both")!;
        Assert.AreEqual(TitleRank.Union, union.Rank);
        Assert.AreEqual("u_both", service.Catalog.Get("k_north")!.ParentId);
        Assert.IsNull(service.Ledger.HolderOf("u_both"), "two rulers hold its realms");

        service.Ledger.SetHolder("k_south", "king_n");
        Assert.IsTrue(service.QuoteForm("king_n", "u_both", new FakeWorld(), config.Claims).Allowed);
    }

    [TestMethod]
    public void A_union_missing_from_an_older_save_is_added_once()
    {
        var service = Service();
        service.Config.Unions.Add(new UnionConfig("both", "Both", new[] { "k_north", "k_south", "k_nowhere" }));
        var warnings = new List<string>();

        Assert.AreEqual(1, service.AddMissingTitles(warnings));
        Assert.AreEqual("u_both", service.Catalog.Get("k_south")!.ParentId);
        Assert.AreEqual(TitleEventReason.Start, service.Ledger.ChronicleOf("u_both").Single().Reason);
        Assert.AreEqual(0, service.AddMissingTitles(warnings));
        Assert.IsTrue(warnings.Any(w => w.Contains("both")), "the missing member is reported");
    }

    // Claims through blood and at the start

    [TestMethod]
    public void Children_of_a_dead_lord_gain_ancestral_claims_and_keep_their_father_s()
    {
        var service = Service();
        service.Clock = () => 40;
        service.Ledger.AddClaim(new TitleClaim("k_south", "king_n", ClaimKind.Ancestral, 1));
        var family = new FamilySnapshot(new[]
        {
            new HeroInfo("king_n", false, false, 70, "royals", null, null, new[] { "prince", "princess" }),
            new HeroInfo("prince", true, false, 30, "royals", "king_n"),
            new HeroInfo("princess", true, true, 28, "other_house", "king_n"),
        });

        var events = service.OnHeroDied("king_n", family);

        Assert.AreEqual("prince", service.Ledger.HolderOf("k_north"));
        Assert.IsFalse(service.Ledger.ClaimsOf("prince").Any(c => c.TitleId == "k_north"), "the heir holds it");
        Assert.IsTrue(service.Ledger.ClaimsOf("princess").Any(c => c.TitleId == "k_north" && c.Kind == ClaimKind.Ancestral && c.CampaignDay == 40));
        Assert.IsTrue(service.Ledger.ClaimsOf("princess").Any(c => c.TitleId == "k_south"), "a claim of the father passes on");
        Assert.IsFalse(service.Ledger.ClaimsOf("princess").Any(c => c.TitleId == "l_village_a1"), "below the ancestral rank");
        Assert.IsTrue(events.Any(e => e.Kind == TitleEventKind.ClaimGained && e.Reason == TitleEventReason.Ancestry));
    }

    [TestMethod]
    public void Starting_claims_are_given_once_to_living_heroes()
    {
        var service = Service();
        service.Config.StartingClaims.Add(new StartingClaimConfig("aragorn", "k_north", ClaimKind.Ancestral));
        service.Config.StartingClaims.Add(new StartingClaimConfig("ghost", "k_north", ClaimKind.Ancestral));
        service.Config.StartingClaims.Add(new StartingClaimConfig("aragorn", "k_nowhere", ClaimKind.Ancestral));

        Assert.AreEqual(1, service.ApplyStartingClaims(h => h != "ghost"));
        Assert.AreEqual(0, service.ApplyStartingClaims(h => h != "ghost"));
        Assert.AreEqual(ClaimKind.Ancestral, service.Ledger.ClaimsOf("aragorn").Single().Kind);
    }

    // Chronicle

    [TestMethod]
    public void The_chronicle_records_each_change_and_keeps_the_latest()
    {
        var config = RealmsConfig.Defaults();
        config.Feudal.ChronicleEntries = 2;
        var service = Service(config);
        service.Clock = () => 7;

        Assert.AreEqual(TitleEventReason.Start, service.Ledger.ChronicleOf("c_town_b").Single().Reason);
        service.OnFiefOwnerChanged("town_b", "lord_c", "lord_b", TransferKind.Transfer, 7);
        service.OnFiefOwnerChanged("town_b", "king_n", "lord_c", TransferKind.Transfer, 7);

        var chronicle = service.Ledger.ChronicleOf("c_town_b");
        Assert.AreEqual(2, chronicle.Count);
        Assert.AreEqual("lord_c", chronicle[0].HeroId);
        Assert.AreEqual("king_n", chronicle[1].HeroId);
        Assert.AreEqual("lord_c", chronicle[1].PreviousHolderId);
        Assert.AreEqual(7, chronicle[1].CampaignDay);
    }

    [TestMethod]
    public void The_chronicle_and_offices_survive_a_save_and_the_version_is_read()
    {
        var service = Service(WithOffices());
        var world = new FakeWorld();
        service.EnsureOffices(world);
        service.Clock = () => 3.25;
        service.OnFiefOwnerChanged("town_b", "lord_c", "lord_b", TransferKind.Transfer, 3.25);

        var payload = RealmsSaveFormat.Write(service.Catalog, service.Ledger);
        var read = RealmsSaveFormat.Read(payload);

        Assert.IsTrue(read.Succeeded, read.Error);
        Assert.AreEqual(0, read.Warnings.Count, string.Join("; ", read.Warnings));
        Assert.AreEqual(RealmsSaveFormat.Version, read.Version);
        var entry = read.Ledger!.ChronicleOf("c_town_b").Last();
        Assert.AreEqual(3.25, entry.CampaignDay);
        Assert.AreEqual(TitleEventReason.Transfer, entry.Reason);
        Assert.AreEqual("lord_c", read.Ledger.OfficeHolderOf("marshal"));
        Assert.IsNull(read.Ledger.OfficeHolderOf("steward"), "the ruler holds the crown, so the stewardship is vacant");
        Assert.IsTrue(read.Ledger.KnownOffices.Contains("steward"));
        Assert.AreEqual(1, RealmsSaveFormat.Read(payload.Replace(RealmsSaveFormat.Header + "\t2", RealmsSaveFormat.Header + "\t1")).Version);
    }

    // Offices

    [TestMethod]
    public void Offices_start_with_their_initial_holder_and_a_ruling_steward_while_the_throne_is_vacant()
    {
        var config = WithOffices();
        config.VacantThrones.Add("north");
        var service = Service(config);
        var world = new FakeWorld();

        var events = service.EnsureOffices(world);

        Assert.AreEqual("lord_c", service.Ledger.OfficeHolderOf("marshal"));
        Assert.AreEqual("king_n", service.Ledger.OfficeHolderOf("steward"));
        Assert.IsTrue(events.All(e => e.Kind == TitleEventKind.OfficeChanged));
        Assert.AreEqual("steward", service.PrimaryOfficeOf("king_n")?.Id);
        Assert.AreEqual(0, service.EnsureOffices(world).Count, "nothing changes a second time");
        Assert.IsFalse(service.CanAppointTo("king_n", service.OfficeById("steward")!, world), "the ruling steward keeps it");
    }

    [TestMethod]
    public void A_crowned_ruler_gives_up_the_stewardship_and_an_ai_ruler_fills_it()
    {
        var service = Service(WithOffices());
        var world = new FakeWorld();
        service.Ledger.SetHolder("k_north", null);
        service.EnsureOffices(world);
        Assert.AreEqual("king_n", service.Ledger.OfficeHolderOf("steward"));

        service.Ledger.SetHolder("k_north", "king_n");
        var candidates = new Dictionary<string, IReadOnlyList<string>> { ["north"] = new[] { "king_n", "lord_c", "lord_b" } };
        service.UpdateOffices(world, candidates);

        Assert.AreEqual("lord_b", service.Ledger.OfficeHolderOf("steward"), "the ruler and the marshal are passed over");
        Assert.AreEqual(TitleEventReason.Appointment, service.Ledger.ChronicleOf("steward").Last().Reason);
    }

    [TestMethod]
    public void Offices_fall_vacant_on_death_or_leaving_and_only_the_ruler_appoints()
    {
        var service = Service(WithOffices());
        var world = new FakeWorld();
        service.EnsureOffices(world);

        var died = service.OnHeroDied("lord_c", new FamilySnapshot(new[] { new HeroInfo("lord_c", false, false, 50, "clan_c") }));
        Assert.IsNull(service.Ledger.OfficeHolderOf("marshal"));
        Assert.IsTrue(died.Any(e => e.Kind == TitleEventKind.OfficeChanged && e.TitleId == "marshal"));

        Assert.AreEqual(0, service.Appoint("lord_b", "marshal", "duke", world).Count, "lord_b does not rule");
        Assert.AreEqual(1, service.Appoint("king_n", "marshal", "duke", world).Count);
        Assert.AreEqual("duke", service.Ledger.OfficeHolderOf("marshal"));
        Assert.AreEqual(0, service.Appoint("king_n", "marshal", "king_n", world).Count, "the ruler cannot hold it");

        world.Kingdoms["duke"] = "south";
        service.UpdateOffices(world, null);
        Assert.IsNull(service.Ledger.OfficeHolderOf("marshal"));

        service.Appoint("king_n", "marshal", "lord_b", world);
        Assert.AreEqual(1, service.Dismiss("king_n", "marshal", world).Count);
        Assert.IsNull(service.Ledger.OfficeHolderOf("marshal"));
    }

    // Weekly consequences

    [TestMethod]
    public void A_fief_held_without_its_title_by_another_clan_is_a_gap()
    {
        var service = Service();
        var world = new FakeWorld();
        Assert.AreEqual(0, service.TitleGaps(world).Count);

        world.Owners["town_c"] = "lord_b";
        var gap = service.TitleGaps(world).Single();
        Assert.AreEqual("c_town_c", gap.TitleId);
        Assert.AreEqual("lord_c", gap.HolderId);
        Assert.AreEqual("lord_b", gap.OwnerId);

        world.Clans["lord_b"] = "clan_c";
        Assert.AreEqual(0, service.TitleGaps(world).Count, "kin may hold each other's fiefs");
    }

    [TestMethod]
    public void Lieges_pull_vassals_and_vassals_pay_tribute_within_the_realm()
    {
        var service = Service();
        var world = new FakeWorld();
        var config = new FeudalConfig();
        var duchy = DuchyOf(service, "c_town_c");
        service.Ledger.SetHolder(duchy, "duke");
        service.Ledger.SetHolder("b_castle_a", "knight");

        var bonds = service.LiegeBonds(world, config);
        CollectionAssert.Contains(bonds.ToList(), ("duke", "lord_c"));
        CollectionAssert.Contains(bonds.ToList(), ("king_n", "duke"));
        Assert.IsFalse(bonds.Contains(("lord_b", "knight")), "a county is below the liege rank");

        var tributes = service.TributeBonds(world, config);
        CollectionAssert.Contains(tributes.ToList(), ("clan_c", "clan_d"));
        CollectionAssert.Contains(tributes.ToList(), ("clan_d", "clan_n"));
        Assert.IsFalse(tributes.Any(t => t.PayerClanId == t.PayeeClanId), "kin owe each other nothing");
        Assert.AreEqual(tributes.Count, tributes.Distinct().Count(), "one bond per pair of clans");

        world.Kingdoms["lord_c"] = "south";
        Assert.IsFalse(service.TributeBonds(world, config).Any(t => t.PayerClanId == "clan_c"), "no tribute across realms");

        // A duchy held from the south (an enemy fief taken and usurped): its lord neither pays the northern king above it
        // nor receives from the northern count below it.
        world.Kingdoms["lord_c"] = "north";
        world.Kingdoms["duke"] = "south";
        tributes = service.TributeBonds(world, config);
        Assert.IsFalse(tributes.Any(t => t.PayerClanId == "clan_d" || t.PayeeClanId == "clan_d"), "no tribute to or from another realm");
        world.Kingdoms["duke"] = "north";
        config.TributeMinRank = TitleRank.Duchy;
        Assert.IsFalse(service.TributeBonds(world, config).Any(t => t.PayerClanId == "clan_c"), "a county is below the tribute rank");
    }

    [TestMethod]
    public void A_foreign_duchy_taken_whole_and_a_castle_given_to_a_lord_of_the_own_realm_pays_its_new_duke_alone()
    {
        // Cirith Ungol under the Morgul Vale: the southern king takes the duchy and gives the castle to a southern lord.
        // The kingdom above the duchy stays with the north.
        var service = Service();
        var world = new FakeWorld();
        var config = new FeudalConfig();
        var duchy = DuchyOf(service, "b_castle_a");
        var realm = service.Catalog.Get(duchy)!.ParentId!;
        world.Clans["lord_s"] = "clan_s2";
        world.Kingdoms["lord_s"] = "south";
        service.Ledger.SetHolder("b_castle_a", "lord_s");
        service.Ledger.SetHolder(realm, "king_n");

        service.Ledger.SetHolder(duchy, "duke"); // the duchy still with a northern duke
        Assert.IsFalse(service.TributeBonds(world, config).Any(t => t.PayerClanId == "clan_s2" || t.PayerClanId == "clan_s"),
            "a castle or town held from a foreign duke pays nothing");

        service.Ledger.SetHolder(duchy, "king_s"); // the duchy usurped as well
        var tributes = service.TributeBonds(world, config);
        CollectionAssert.Contains(tributes.ToList(), ("clan_s2", "clan_s"), "the castle's lord pays the duke of their own realm");
        Assert.IsFalse(tributes.Any(t => t.PayerClanId == "clan_s"), "the duke pays nothing to the foreign king above");
    }

    [TestMethod]
    public void Usurping_from_a_kinsman_is_refused()
    {
        var service = Service();
        var world = new FakeWorld();
        service.Ledger.SetHolder("c_town_a", "prince");
        service.Ledger.AddClaim(new TitleClaim("c_town_a", "king_n", ClaimKind.Conquest, 0));

        Assert.AreEqual(ClaimRefusal.OwnClan, service.QuoteUsurp("king_n", "clan_n", "c_town_a", world, new ClaimsConfig()).Refusal);
    }

    // Config

    [TestMethod]
    public void The_new_sections_are_read_from_xml()
    {
        var xml = XDocument.Parse(@"<Realms>
  <Claims ancestral_min_rank=""Duchy""/>
  <Kingdoms><Kingdom id=""empire_w"" throne=""vacant""/></Kingdoms>
  <Grants relation=""5"" revoke_relation=""-40""><Rank rank=""Duchy"" revoke=""150""/><Rank rank=""Kingdom""/></Grants>
  <Chronicle entries=""12""/>
  <Consequences resentment=""-2"" loyalty=""-3"" liege_relation=""2"" liege_cap=""40"" liege_rank=""Kingdom"" army_discount=""0.25"" tribute_rank=""County"">
    <Rank rank=""County"" tribute=""60""/>
  </Consequences>
  <Unions><Union id=""reunited"" name=""Gondor and Arnor""><Member title=""k_empire_w""/><Member title=""e_arnor""/></Union></Unions>
  <StartingClaims><Claim hero=""named_companion_aragorn"" title=""k_empire_w"" kind=""Ancestral""/></StartingClaims>
  <Offices>
    <Office id=""steward"" kingdom=""empire_w"" name=""Steward of Gondor"" holder=""Steward {NAME}"" of=""Gondor"" rank=""Kingdom"" follows=""ruler"" influence=""3""/>
    <Office id=""marshal"" kingdom=""vlandia"" name=""First Marshal"" rank=""Duchy"" initial=""lord_4_3_1""/>
  </Offices>
</Realms>");

        var result = RealmsConfigXml.Parse(xml);
        var config = result.Config;

        Assert.AreEqual(0, result.Warnings.Count, string.Join("; ", result.Warnings));
        Assert.AreEqual(TitleRank.Duchy, config.Claims.AncestralMinRank);
        Assert.IsTrue(config.VacantThrones.Contains("empire_w"));
        Assert.AreEqual(5, config.Feudal.GrantRelation);
        Assert.AreEqual(-40, config.Feudal.RevokeRelation);
        Assert.AreEqual(150, config.Feudal.RevokeCost(TitleRank.Duchy));
        Assert.IsNull(config.Feudal.RevokeCost(TitleRank.Kingdom));
        Assert.AreEqual(12, config.Feudal.ChronicleEntries);
        Assert.AreEqual(-3, config.Feudal.ResentmentLoyalty);
        Assert.AreEqual(40, config.Feudal.LiegeRelationCap);
        Assert.AreEqual(TitleRank.Kingdom, config.Feudal.LiegeMinRank);
        Assert.AreEqual(0.25f, config.Feudal.ArmyDiscount);
        Assert.AreEqual(TitleRank.County, config.Feudal.TributeMinRank);
        CollectionAssert.AreEqual(new[] { "k_empire_w", "e_arnor" }, config.Unions.Single().MemberTitleIds.ToArray());
        Assert.AreEqual("named_companion_aragorn", config.StartingClaims.Single().HeroId);
        var steward = config.Offices[0];
        Assert.IsTrue(steward.FollowsRuler);
        Assert.AreEqual(3, steward.Influence);
        Assert.AreEqual("Gondor", steward.Of);
        Assert.AreEqual(TitleRank.Kingdom, steward.Rank);
        Assert.AreEqual("lord_4_3_1", config.Offices[1].InitialHolderId);
        Assert.IsNull(config.Offices[1].HolderMale);
    }
}
