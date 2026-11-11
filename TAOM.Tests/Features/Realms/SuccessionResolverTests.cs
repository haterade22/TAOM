using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Realms.Domain.Laws;
using TAOM.Features.Realms.Domain.Succession;

namespace TAOM.Tests.Features.Realms;

[TestClass]
public class SuccessionResolverTests
{
    // lord (dead) has: daughter (40), son_elder (35, dead, with grandson 10), son_younger (30).
    // lord's father grandpa (dead) also had uncle... no: lord's sibling is brother (45). clan also has cousin (60).
    private static FamilySnapshot Family(bool elderSonHasChild = true, bool withSons = true) => new(new[]
    {
        new HeroInfo("lord", false, false, 65, "clan", "grandpa", null,
            withSons ? new[] { "daughter", "son_elder", "son_younger" } : new[] { "daughter" }),
        new HeroInfo("daughter", true, true, 40, "clan", "lord"),
        new HeroInfo("son_elder", false, false, 35, "clan", "lord", null, elderSonHasChild ? new[] { "grandson" } : null),
        new HeroInfo("grandson", true, false, 10, "clan", "son_elder"),
        new HeroInfo("son_younger", true, false, 30, "clan", "lord"),
        new HeroInfo("grandpa", false, false, 90, "clan", null, null, new[] { "lord", "brother" }),
        new HeroInfo("brother", true, false, 45, "clan", "grandpa"),
        new HeroInfo("cousin", true, false, 60, "clan"),
    });

    private static SuccessionOutcome Resolve(FamilySnapshot family, SuccessionLaw law, GenderLaw gender, bool realm = false) =>
        SuccessionResolver.Resolve("lord", family, new TitleLaws(law, gender), realm);

    [TestMethod]
    public void Male_preference_primogeniture_follows_the_dead_eldest_sons_line_first()
    {
        var outcome = Resolve(Family(), SuccessionLaw.Primogeniture, GenderLaw.AgnaticCognatic);

        Assert.AreEqual(SuccessionRoute.Descendant, outcome.Route);
        Assert.AreEqual("grandson", outcome.HeirId);
    }

    [TestMethod]
    public void Without_that_line_the_next_son_inherits_before_an_older_daughter()
    {
        var outcome = Resolve(Family(elderSonHasChild: false), SuccessionLaw.Primogeniture, GenderLaw.AgnaticCognatic);

        Assert.AreEqual("son_younger", outcome.HeirId);
    }

    [TestMethod]
    public void Absolute_primogeniture_takes_the_eldest_child_of_either_sex()
    {
        Assert.AreEqual("daughter", Resolve(Family(), SuccessionLaw.Primogeniture, GenderLaw.Absolute).HeirId);
    }

    [TestMethod]
    public void Agnatic_law_skips_daughters_and_falls_to_the_brother()
    {
        var outcome = Resolve(Family(withSons: false), SuccessionLaw.Primogeniture, GenderLaw.Agnatic);

        Assert.AreEqual(SuccessionRoute.Sibling, outcome.Route);
        Assert.AreEqual("brother", outcome.HeirId);
    }

    [TestMethod]
    public void Male_preference_lets_a_daughter_inherit_when_there_are_no_sons()
    {
        Assert.AreEqual("daughter", Resolve(Family(withSons: false), SuccessionLaw.Primogeniture, GenderLaw.AgnaticCognatic).HeirId);
    }

    [TestMethod]
    public void Ultimogeniture_takes_the_youngest_line()
    {
        Assert.AreEqual("son_younger", Resolve(Family(), SuccessionLaw.Ultimogeniture, GenderLaw.AgnaticCognatic).HeirId);
    }

    [TestMethod]
    public void Seniority_takes_the_eldest_adult_of_the_clan()
    {
        var outcome = Resolve(Family(), SuccessionLaw.Seniority, GenderLaw.AgnaticCognatic);

        Assert.AreEqual(SuccessionRoute.Clan, outcome.Route);
        Assert.AreEqual("cousin", outcome.HeirId);
    }

    [TestMethod]
    public void Clan_leader_law_defers_to_the_game()
    {
        Assert.AreEqual(SuccessionRoute.DeferredToClan, Resolve(Family(), SuccessionLaw.ClanLeader, GenderLaw.Absolute).Route);
    }

    [TestMethod]
    public void Elective_defers_to_the_realm_for_realm_titles_and_to_the_clan_below()
    {
        Assert.AreEqual(SuccessionRoute.DeferredToRealm, Resolve(Family(), SuccessionLaw.Elective, GenderLaw.Absolute, realm: true).Route);
        Assert.AreEqual(SuccessionRoute.DeferredToClan, Resolve(Family(), SuccessionLaw.Elective, GenderLaw.Absolute).Route);
    }

    [TestMethod]
    public void Nobody_eligible_means_no_heir()
    {
        var family = new FamilySnapshot(new[] { new HeroInfo("lord", false, false, 50, null) });

        Assert.AreEqual(SuccessionRoute.None, Resolve(family, SuccessionLaw.Primogeniture, GenderLaw.Absolute).Route);
    }
}
