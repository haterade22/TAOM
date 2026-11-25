using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.CareerSystem;
using TAOM.Features.CoopInterop;
using TAOM.Features.CoopInterop.CareerSync;

namespace TAOM.Tests.Features.CoopInterop.CareerSync;

// Which career tiers count in the passive cache (docs/features/coop-interop.md, "Career sync"). A career quest
// unlocks a tier on the player's own machine, so only the co-op server, which cannot see the unlock, gates on level.
[TestClass]
public class CoopCareerTierTrustTests
{
    private const string Hero = "Player1";
    private ICoopSessionProvider _session = null!;
    private ICareerSyncHeroAdapter _heroes = null!;
    private ICareerRegistry _registry = null!;
    private CoopCareerTierTrust _trust = null!;

    [TestInitialize]
    public void Setup()
    {
        _session = Substitute.For<ICoopSessionProvider>();
        _heroes = Substitute.For<ICareerSyncHeroAdapter>();
        _registry = Substitute.For<ICareerRegistry>();
        _registry.IsTierAvailable(Arg.Any<int>(), Arg.Any<int>())
            .Returns(c => c.ArgAt<int>(1) >= 1 && c.ArgAt<int>(1) <= 3 && c.ArgAt<int>(0) >= c.ArgAt<int>(1) * 10 - 10);
        var hero = Substitute.For<ICareerHeroAdapter>();
        hero.Level.Returns(12);
        _heroes.Find(Hero).Returns(hero);
        _trust = new CoopCareerTierTrust(_session, _heroes, _registry);
    }

    private void Server() { _session.IsSessionActive.Returns(true); _session.IsAuthority.Returns(true); }

    [TestMethod]
    public void SinglePlayer_CountsEveryTier_AndReadsNoHero()
    {
        _session.IsAuthority.Returns(true);

        Assert.IsTrue(_trust.Counts(Hero, 3));
        Assert.IsTrue(_trust.Counts(Hero, 0));
        _heroes.DidNotReceiveWithAnyArgs().Find(default!);
    }

    [TestMethod]
    public void CoopClient_CountsEveryTier_BecauseItsQuestUnlocksRunThere()
    {
        _session.IsSessionActive.Returns(true);
        _session.IsCoopClient.Returns(true);

        Assert.IsTrue(_trust.Counts(Hero, 3));
        _heroes.DidNotReceiveWithAnyArgs().Find(default!);
    }

    [TestMethod]
    public void CoopServer_CountsATier_OnlyFromTheLevelThatOpensIt()
    {
        Server();

        Assert.IsTrue(_trust.Counts(Hero, 1));
        Assert.IsTrue(_trust.Counts(Hero, 2), "level 12 opens tier 2");
        Assert.IsFalse(_trust.Counts(Hero, 3), "a quest unlock the server cannot see does not count");
        Assert.IsFalse(_trust.Counts(Hero, 0), "a choice with no tier does not count");
    }

    [TestMethod]
    public void CoopServer_HeroItCannotFind_CountsNothing()
    {
        Server();
        _heroes.Find("gone").Returns((ICareerHeroAdapter?)null);

        Assert.IsFalse(_trust.Counts("gone", 1));
    }
}
