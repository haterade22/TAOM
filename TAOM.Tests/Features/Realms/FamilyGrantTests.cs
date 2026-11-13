using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Realms.Domain.Config;
using TAOM.Features.Realms.Domain.Laws;
using TAOM.Features.Realms.Domain.Services;
using TAOM.Features.Realms.Domain.Succession;
using TAOM.Features.Realms.Domain.World;
using static TAOM.Tests.Features.Realms.TestWorld;

namespace TAOM.Tests.Features.Realms;

/// <summary>Clan leaders granting lesser titles to their children, on a small Gondor: Anórien and Ithilien.</summary>
[TestClass]
public class FamilyGrantTests
{
    private static readonly string[] House = { "denethor", "boromir", "faramir" };

    private static RealmsService Gondor(TitleLaws? laws = null)
    {
        var world = new WorldSnapshot(
            new List<KingdomInfo> { new("gondor", "Gondor", "culture_gondor", "denethor") },
            new List<FiefInfo>
            {
                Town("town_mt", "gondor", "denethor", 0),
                Town("town_wo", "gondor", "denethor", 7),
                Town("town_eo", "gondor", "denethor", 20),
                TestWorld.Castle("castle_mr", "gondor", "denethor", -80),
                TestWorld.Castle("castle_ca", "gondor", "lord_ca", 25),
                Village("village_mt", "town_mt", "denethor"),
                Village("village_eo", "town_eo", "denethor"),
            });
        var config = RealmsConfig.Defaults();
        config.Duchies.Add(new DuchyConfig("anorien", "{=x}Anórien", "gondor", new[] { "town_mt", "town_wo" }, new[] { "castle_mr" }));
        config.Duchies.Add(new DuchyConfig("ithilien", "{=x}Ithilien", "gondor", new[] { "town_eo" }, new[] { "castle_ca" }));
        if (laws != null)
            config.KingdomLaws["gondor"] = laws;
        var service = new RealmsService();
        service.Initialize(world, config);
        return service;
    }

    private static FamilySnapshot Family(params HeroInfo[] children) => new(
        new[] { new HeroInfo("denethor", true, false, 89, "hurin", null, null, children.Select(c => c.Id).ToList()) }.Concat(children));

    private static HeroInfo Child(string id, float age, bool isFemale = false, string clan = "hurin") =>
        new(id, true, isFemale, age, clan, "denethor");

    private static readonly ClanHead[] Steward = { new("denethor", "town_mt") };

    [TestMethod]
    public void The_heir_gets_a_fief_by_the_seat_and_the_younger_son_the_next_duchy()
    {
        var service = Gondor();
        Assert.AreEqual("denethor", service.Ledger.HolderOf("d_ithilien"), "setup: the steward is duke of Ithilien");

        var events = service.GrantToChildren(Steward, Family(Child("boromir", 41), Child("faramir", 36)));

        Assert.AreEqual("boromir", service.Ledger.HolderOf("c_town_wo"));
        Assert.AreEqual("faramir", service.Ledger.HolderOf("d_ithilien"));
        Assert.AreEqual("faramir", service.Ledger.HolderOf("c_town_eo"));
        Assert.AreEqual("faramir", service.Ledger.HolderOf("l_village_eo"), "the lordships go with their county");
        Assert.AreEqual("lord_ca", service.Ledger.HolderOf("b_castle_ca"), "only the giver's own titles are given");
        CollectionAssert.AreEquivalent(new[] { "k_gondor", "d_anorien", "c_town_mt", "l_village_mt", "b_castle_mr" },
            service.Ledger.TitlesHeldBy("denethor").ToArray(), "the steward keeps his realm, seat and what is left");
        Assert.IsTrue(events.All(e => e.Reason == TitleEventReason.Grant && e.PreviousHolderId == "denethor"));
        Assert.AreEqual(4, events.Count);
        Assert.AreEqual("d_ithilien", service.PrimaryTitleOf("faramir")?.Id);
    }

    [TestMethod]
    public void The_realm_capital_is_kept_as_the_seat_ahead_of_the_clan_home()
    {
        // TAOM gives Gondor West Osgiliath as its home settlement; the capital keeps Minas Tirith with the Steward.
        var service = Gondor();
        var capitals = new Dictionary<string, string> { ["gondor"] = "town_mt" };

        service.GrantToChildren(new[] { new ClanHead("denethor", "town_wo") }, Family(Child("boromir", 41), Child("faramir", 36)), capitals);

        Assert.AreEqual("denethor", service.Ledger.HolderOf("c_town_mt"));
        Assert.AreEqual("denethor", service.Ledger.HolderOf("b_castle_mr"));
        Assert.AreEqual("boromir", service.Ledger.HolderOf("c_town_wo"));
        Assert.AreEqual("faramir", service.Ledger.HolderOf("d_ithilien"));
    }

    [TestMethod]
    public void A_capital_the_leader_does_not_hold_falls_back_to_the_clan_home()
    {
        var service = Gondor();
        var capitals = new Dictionary<string, string> { ["gondor"] = "castle_ca", ["elsewhere"] = "town_mt" };

        service.GrantToChildren(new[] { new ClanHead("denethor", "town_wo") }, Family(Child("boromir", 41)), capitals);

        Assert.AreEqual("denethor", service.Ledger.HolderOf("c_town_wo"), "the home is the seat");
        Assert.AreEqual("boromir", service.Ledger.HolderOf("c_town_mt"));
    }

    [TestMethod]
    public void Granting_again_changes_nothing_until_a_child_comes_of_age()
    {
        var service = Gondor();
        service.GrantToChildren(Steward, Family(Child("boromir", 41), Child("faramir", 16)));
        Assert.AreEqual("boromir", service.Ledger.HolderOf("c_town_wo"));
        Assert.AreEqual("denethor", service.Ledger.HolderOf("d_ithilien"), "a minor gets nothing");

        Assert.AreEqual(0, service.GrantToChildren(Steward, Family(Child("boromir", 41), Child("faramir", 17))).Count);

        service.GrantToChildren(Steward, Family(Child("boromir", 42), Child("faramir", 18)));
        Assert.AreEqual("faramir", service.Ledger.HolderOf("d_ithilien"));
        Assert.AreEqual("boromir", service.Ledger.HolderOf("c_town_wo"), "a child keeps one grant");
    }

    [TestMethod]
    public void Children_outside_the_clan_or_the_gender_law_get_nothing()
    {
        var service = Gondor(new TitleLaws(SuccessionLaw.Primogeniture, GenderLaw.Agnatic));

        var events = service.GrantToChildren(Steward,
            Family(Child("daughter", 40, isFemale: true), Child("married", 38, clan: "other"), Child("faramir", 36)));

        Assert.AreEqual("faramir", service.Ledger.HolderOf("c_town_wo"), "the only eligible child is the first");
        Assert.AreEqual(0, service.Ledger.TitlesHeldBy("daughter").Count);
        Assert.AreEqual(0, service.Ledger.TitlesHeldBy("married").Count);
        Assert.IsTrue(events.All(e => e.HeroId == "faramir"));
    }

    [TestMethod]
    public void Under_absolute_law_the_eldest_daughter_comes_first()
    {
        var service = Gondor(new TitleLaws(SuccessionLaw.Primogeniture, GenderLaw.Absolute));

        service.GrantToChildren(Steward, Family(Child("son", 30), Child("daughter", 40, isFemale: true)));

        Assert.AreEqual("daughter", service.Ledger.HolderOf("c_town_wo"));
        Assert.AreEqual("son", service.Ledger.HolderOf("d_ithilien"));
    }

    [TestMethod]
    public void Without_a_held_home_the_seat_is_the_highest_county()
    {
        var service = Gondor();

        service.GrantToChildren(new[] { new ClanHead("denethor", "town_unknown") }, Family(Child("boromir", 41)));

        Assert.AreEqual("denethor", service.Ledger.HolderOf("c_town_eo"), "c_town_eo is first by id, so it is the seat");
        Assert.AreEqual("denethor", service.Ledger.HolderOf("d_ithilien"), "and its duchy stays");
        Assert.AreEqual("boromir", service.Ledger.HolderOf("d_anorien"), "the heir takes the highest grant");
        Assert.AreEqual("boromir", service.Ledger.HolderOf("c_town_mt"));
    }

    [TestMethod]
    public void A_lawful_hand_over_moves_a_title_a_clan_member_holds_and_a_conquest_does_not()
    {
        var service = Gondor();
        service.GrantToChildren(Steward, Family(Child("boromir", 41), Child("faramir", 36)));

        var taken = service.OnFiefOwnerChanged("town_wo", "haradrim", "denethor", TransferKind.Conquest, 3, House);
        Assert.AreEqual("boromir", service.Ledger.HolderOf("c_town_wo"));
        Assert.AreEqual(TitleEventKind.ClaimGained, taken.Single().Kind);

        var given = service.OnFiefOwnerChanged("town_eo", "lord_ca", "denethor", TransferKind.Transfer, 4, House);
        Assert.AreEqual("lord_ca", service.Ledger.HolderOf("c_town_eo"));
        Assert.AreEqual("lord_ca", service.Ledger.HolderOf("l_village_eo"));
        Assert.AreEqual("faramir", service.Ledger.HolderOf("d_ithilien"), "the duchy is no fief and stays");
        Assert.IsTrue(given.All(e => e.Reason == TitleEventReason.Transfer && e.PreviousHolderId == "faramir"));

        service.OnFiefOwnerChanged("town_mt", "lord_ca", "denethor", TransferKind.Transfer, 5);
        Assert.AreEqual("lord_ca", service.Ledger.HolderOf("c_town_mt"), "the giver's own title moves as before");
    }
}
