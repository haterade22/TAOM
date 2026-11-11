using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Realms.Domain.Config;
using TAOM.Features.Realms.Domain.Laws;
using TAOM.Features.Realms.Domain.Services;
using TAOM.Features.Realms.Domain.Succession;

namespace TAOM.Tests.Features.Realms;

[TestClass]
public class RealmsServiceTests
{
    private sealed class FakeLeaders : IRealmLeaders
    {
        public Dictionary<string, string?> Clans { get; } = new();
        public Dictionary<string, string?> Rulers { get; } = new();
        public string? ClanLeaderOf(string clanId) => Clans.TryGetValue(clanId, out var h) ? h : null;
        public bool ClanExists(string clanId) => Clans.ContainsKey(clanId);
        public string? RulerOf(string kingdomId) => Rulers.TryGetValue(kingdomId, out var h) ? h : null;
        public bool KingdomExists(string kingdomId) => Rulers.ContainsKey(kingdomId);
    }

    private static RealmsService Service(RealmsConfig? config = null)
    {
        var service = new RealmsService();
        service.Initialize(TestWorld.Build(), config ?? RealmsConfig.Defaults());
        return service;
    }

    private static FamilySnapshot KingWithSon() => new(new[]
    {
        new HeroInfo("king_n", false, false, 70, "royals", null, null, new[] { "prince" }),
        new HeroInfo("prince", true, false, 30, "royals", "king_n"),
    });

    [TestMethod]
    public void Laws_are_inherited_from_the_nearest_ancestor_with_laws()
    {
        var config = RealmsConfig.Defaults();
        var elective = new TitleLaws(SuccessionLaw.Elective, GenderLaw.Absolute);
        config.KingdomLaws["north"] = elective;
        var service = Service(config);

        Assert.AreEqual(elective, service.EffectiveLaws("l_village_a1"));
        Assert.AreEqual(TitleLaws.Default, service.EffectiveLaws("c_town_s"));
    }

    [TestMethod]
    public void The_primary_title_is_the_highest_held_then_the_first_by_id()
    {
        var service = Service();

        Assert.AreEqual("k_north", service.PrimaryTitleOf("king_n")?.Id);
        service.Ledger.SetHolder("c_town_s", "squire");
        service.Ledger.SetHolder("c_town_a", "squire");
        Assert.AreEqual("c_town_a", service.PrimaryTitleOf("squire")?.Id);
        Assert.IsNull(service.PrimaryTitleOf("nobody"));
    }

    [TestMethod]
    public void Death_passes_every_title_to_the_heir_and_reports_it()
    {
        var service = Service();

        var events = service.OnHeroDied("king_n", KingWithSon());

        Assert.AreEqual("prince", service.Ledger.HolderOf("k_north"));
        Assert.AreEqual("prince", service.Ledger.HolderOf("c_town_a"));
        Assert.AreEqual(0, service.Ledger.TitlesHeldBy("king_n").Count);
        Assert.IsTrue(events.All(e => e.Reason == TitleEventReason.Inheritance && e.HeroId == "prince"));
        Assert.AreEqual("k_north", events[0].TitleId, "highest title is reported first");
    }

    [TestMethod]
    public void Elective_realm_title_waits_for_the_realm_ruler()
    {
        var config = RealmsConfig.Defaults();
        config.KingdomLaws["north"] = new TitleLaws(SuccessionLaw.Elective, GenderLaw.Absolute);
        var service = Service(config);
        var leaders = new FakeLeaders();
        leaders.Clans["royals"] = "king_n";
        leaders.Rulers["north"] = "king_n";

        service.OnHeroDied("king_n", KingWithSon());
        Assert.AreEqual(0, service.ResolvePending(leaders).Count, "the dead king is still listed as leader and ruler");

        leaders.Clans["royals"] = "prince";
        leaders.Rulers["north"] = "elected";
        var events = service.ResolvePending(leaders);

        Assert.AreEqual("elected", service.Ledger.HolderOf("k_north"));
        Assert.AreEqual("prince", service.Ledger.HolderOf("c_town_a"), "fiefs under an elective law follow the clan");
        Assert.AreEqual(0, service.Ledger.Pending.Count);
        Assert.IsTrue(events.Any(e => e.TitleId == "k_north" && e.Reason == TitleEventReason.RealmSuccession));
    }

    [TestMethod]
    public void Waiting_title_falls_vacant_when_the_clan_is_gone()
    {
        var config = RealmsConfig.Defaults();
        config.DefaultLaws = new TitleLaws(SuccessionLaw.ClanLeader, GenderLaw.Absolute);
        var service = Service(config);
        service.OnHeroDied("king_n", new FamilySnapshot(new[] { new HeroInfo("king_n", false, false, 70, "royals") }));

        var events = service.ResolvePending(new FakeLeaders());

        Assert.IsNull(service.Ledger.HolderOf("c_town_a"));
        Assert.IsTrue(events.All(e => e.Reason == TitleEventReason.Vacated));
    }

    [TestMethod]
    public void A_son_s_blood_claim_takes_a_waiting_title_when_the_clan_is_gone()
    {
        var config = RealmsConfig.Defaults();
        config.DefaultLaws = new TitleLaws(SuccessionLaw.ClanLeader, GenderLaw.Absolute);
        var service = Service(config);
        service.OnHeroDied("king_n", KingWithSon());
        Assert.IsTrue(service.Ledger.ClaimsOf("prince").Any(c => c.TitleId == "c_town_a" && c.Kind == global::TAOM.Features.Realms.Domain.Ledger.ClaimKind.Ancestral));

        var events = service.ResolvePending(new FakeLeaders());

        Assert.AreEqual("prince", service.Ledger.HolderOf("c_town_a"));
        Assert.IsTrue(events.Any(e => e.TitleId == "c_town_a" && e.Reason == TitleEventReason.Claim));
    }

    [TestMethod]
    public void New_ruler_of_an_elective_realm_takes_the_realm_title()
    {
        var config = RealmsConfig.Defaults();
        config.KingdomLaws["south"] = new TitleLaws(SuccessionLaw.Elective, GenderLaw.Absolute);
        var service = Service(config);

        service.OnRealmRulerChanged("south", "usurper");
        service.OnRealmRulerChanged("north", "usurper");

        Assert.AreEqual("usurper", service.Ledger.HolderOf("k_south"));
        Assert.AreEqual("king_n", service.Ledger.HolderOf("k_north"), "a hereditary realm keeps its de jure king");
    }

    [TestMethod]
    public void Conquest_keeps_the_title_and_gives_the_taker_a_claim()
    {
        var service = Service();

        var events = service.OnFiefOwnerChanged("town_b", "raider", "lord_b", TransferKind.Conquest, 12);

        Assert.AreEqual("lord_b", service.Ledger.HolderOf("c_town_b"));
        Assert.AreEqual("raider", service.Ledger.ClaimsOn("c_town_b").Single().HeroId);
        Assert.AreEqual(TitleEventKind.ClaimGained, events.Single().Kind);
    }

    [TestMethod]
    public void Lawful_transfer_moves_the_title_and_its_village_lordships()
    {
        var service = Service();

        service.OnFiefOwnerChanged("town_a", "lord_b", "king_n", TransferKind.Transfer, 5);

        Assert.AreEqual("lord_b", service.Ledger.HolderOf("c_town_a"));
        Assert.AreEqual("lord_b", service.Ledger.HolderOf("l_village_a1"));
    }

    [TestMethod]
    public void Transfer_by_someone_who_does_not_hold_the_title_does_not_move_it()
    {
        var service = Service();
        service.OnFiefOwnerChanged("town_b", "raider", "lord_b", TransferKind.Conquest, 1);

        service.OnFiefOwnerChanged("town_b", "buyer", "raider", TransferKind.Transfer, 2);

        Assert.AreEqual("lord_b", service.Ledger.HolderOf("c_town_b"));
    }

    [TestMethod]
    public void Vacant_title_is_taken_up_by_whoever_gains_the_settlement()
    {
        var service = Service();
        service.Ledger.SetHolder("c_town_c", null);

        var events = service.OnFiefOwnerChanged("town_c", "settler", null, TransferKind.Other, 3);

        Assert.AreEqual("settler", service.Ledger.HolderOf("c_town_c"));
        Assert.AreEqual(TitleEventReason.Possession, events.Single().Reason);
    }

    [TestMethod]
    public void Death_removes_the_heros_claims()
    {
        var service = Service();
        service.OnFiefOwnerChanged("town_b", "raider", "lord_b", TransferKind.Conquest, 1);

        service.OnHeroDied("raider", new FamilySnapshot(new[] { new HeroInfo("raider", false, false, 40, null) }));

        Assert.AreEqual(0, service.Ledger.ClaimsOn("c_town_b").Count);
    }
}
