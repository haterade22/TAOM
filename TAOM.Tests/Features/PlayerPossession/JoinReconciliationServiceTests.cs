using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.PlayerPossession;
using TAOM.Features.SpecialResources;
using TAOM.Features.SpecialResources.Domain;
using TAOM.Features.StartupResources;

namespace TAOM.Tests.Features.PlayerPossession;

[TestClass]
public class JoinReconciliationServiceTests
{
    private IHeroRosterAdapter _heroRoster;
    private IRaceManager _raceManager;
    private IPlayerStartupGoldService _startupGold;
    private ICareerCreationHandler _careerHandler;
    private ISpecialResourceService _specialResources;
    private IModLogger _logger;
    private JoinReconciliationService _sut;

    [TestInitialize]
    public void Setup()
    {
        _heroRoster = Substitute.For<IHeroRosterAdapter>();
        _raceManager = Substitute.For<IRaceManager>();
        _startupGold = Substitute.For<IPlayerStartupGoldService>();
        _careerHandler = Substitute.For<ICareerCreationHandler>();
        _specialResources = Substitute.For<ISpecialResourceService>();
        _logger = Substitute.For<IModLogger>();

        _raceManager.IsValidRaceId(Arg.Any<int>()).Returns(true);
        _heroRoster.GetHeroRace(Arg.Any<string>()).Returns(0);
        _specialResources.ResolveResource(Arg.Any<string>(), Arg.Any<string>())
            .Returns(new SpecialResource("mirkwood_boon", new[] { "woodland_realm" }, new[] { "mirkwood" },
                "Elven Wine", "icon", 400f, 20f, 0.5f, 6f, 3f, 15f, 1f));

        _sut = new JoinReconciliationService(
            _heroRoster, _raceManager, _startupGold, _careerHandler, _specialResources, _logger);
    }

    private static PlayerCharacterCreationChoices Choices(int raceId = 3, string careerId = "ranger") =>
        new("cc_hero", "mirkwood", raceId, careerId);

    [TestMethod]
    public void Reapply_AppliesEveryGrantToTheNewHero()
    {
        var applied = _sut.ReapplyCharacterCreationPackage(Choices(), "joined_hero", "woodland_realm");

        Assert.IsTrue(applied);
        _heroRoster.Received(1).SetHeroRace("joined_hero", 3);
        _startupGold.Received(1).GrantPlayerStartupGold("mirkwood", "joined_hero");
        _careerHandler.Received(1).OnCareerSelected("joined_hero", "ranger");
        _specialResources.Received(1).InitializeHero("joined_hero", "woodland_realm", "mirkwood");
    }

    [TestMethod]
    public void Reapply_UsesTheChosenCultureNotTheHostsKingdomCulture()
    {
        // The player picked Mirkwood and earned Mirkwood's package. Arriving under the host's
        // culture is the bug being fixed, so the CC choice — not the live hero — drives the grants.
        _sut.ReapplyCharacterCreationPackage(Choices(), "joined_hero", "gondor_kingdom");

        _startupGold.Received(1).GrantPlayerStartupGold("mirkwood", "joined_hero");
        _startupGold.DidNotReceive().GrantPlayerStartupGold("gondor", Arg.Any<string>());
    }

    [TestMethod]
    public void Reapply_OneGrantThrows_TheOthersStillApply()
    {
        // A joiner losing their career because the gold grant threw would be worse than the bug
        // this fixes, so each grant is independently guarded.
        _startupGold
            .When(s => s.GrantPlayerStartupGold(Arg.Any<string>(), Arg.Any<string>()))
            .Throw(new System.InvalidOperationException("boom"));

        var applied = _sut.ReapplyCharacterCreationPackage(Choices(), "joined_hero", "woodland_realm");

        Assert.IsTrue(applied);
        _heroRoster.Received(1).SetHeroRace("joined_hero", 3);
        _careerHandler.Received(1).OnCareerSelected("joined_hero", "ranger");
        _specialResources.Received(1).InitializeHero("joined_hero", "woodland_realm", "mirkwood");
        _logger.Received().LogError(Arg.Is<string>(m => m.Contains("startup gold")));
    }

    [TestMethod]
    public void Reapply_RaceAlreadyCorrect_DoesNotSetItAgain()
    {
        _heroRoster.GetHeroRace("joined_hero").Returns(3);

        _sut.ReapplyCharacterCreationPackage(Choices(), "joined_hero", "woodland_realm");

        _heroRoster.DidNotReceive().SetHeroRace(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Reapply_RaceIdInvalidOnThisClient_SkipsRaceButKeepsOtherGrants()
    {
        // Validate BEFORE the set: GetRaceNameFromId coerces unknown ids to "human", so an id from a
        // module set this client lacks would be written as a valid-looking race and cached for the
        // session (csharp-architecture.md, validate-before-lookup).
        _raceManager.IsValidRaceId(99).Returns(false);

        var applied = _sut.ReapplyCharacterCreationPackage(Choices(raceId: 99), "joined_hero", "woodland_realm");

        Assert.IsTrue(applied);
        _heroRoster.DidNotReceive().SetHeroRace(Arg.Any<string>(), Arg.Any<int>());
        _startupGold.Received(1).GrantPlayerStartupGold("mirkwood", "joined_hero");
    }

    [TestMethod]
    public void Reapply_RaceIdUnset_SkipsRace()
    {
        // -1 is what the snapshot records when the CC race stage never resolved one.
        _sut.ReapplyCharacterCreationPackage(Choices(raceId: -1), "joined_hero", "woodland_realm");

        _heroRoster.DidNotReceive().SetHeroRace(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Reapply_HumanRaceZero_IsAppliedNotTreatedAsUnset()
    {
        _raceManager.IsValidRaceId(0).Returns(false); // human is valid by definition, never looked up
        _heroRoster.GetHeroRace("joined_hero").Returns(5);

        _sut.ReapplyCharacterCreationPackage(Choices(raceId: 0), "joined_hero", "woodland_realm");

        _heroRoster.Received(1).SetHeroRace("joined_hero", 0);
    }

    [TestMethod]
    public void Reapply_NoCareerPicked_SkipsCareerGrant()
    {
        _sut.ReapplyCharacterCreationPackage(Choices(careerId: null), "joined_hero", "woodland_realm");

        _careerHandler.DidNotReceive().OnCareerSelected(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void Reapply_CultureHasNoSpecialResource_SkipsTheSeed()
    {
        _specialResources.ResolveResource(Arg.Any<string>(), Arg.Any<string>()).Returns((SpecialResource)null);

        _sut.ReapplyCharacterCreationPackage(Choices(), "joined_hero", "woodland_realm");

        _specialResources.DidNotReceive().InitializeHero(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void Reapply_NullChoices_IsANoOp()
    {
        Assert.IsFalse(_sut.ReapplyCharacterCreationPackage(null, "joined_hero", "woodland_realm"));
        _heroRoster.DidNotReceive().SetHeroRace(Arg.Any<string>(), Arg.Any<int>());
        _startupGold.DidNotReceive().GrantPlayerStartupGold(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void Reapply_EmptyHeroId_IsANoOp()
    {
        Assert.IsFalse(_sut.ReapplyCharacterCreationPackage(Choices(), string.Empty, "woodland_realm"));
        _startupGold.DidNotReceive().GrantPlayerStartupGold(Arg.Any<string>(), Arg.Any<string>());
    }
}
