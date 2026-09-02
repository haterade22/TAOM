using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

/// <summary>
/// Lifecycle half of CareerCampaignBehavior: WHEN the legacy-career fallback may run, and the
/// repair pass that clears the choices left behind by every session where it ran too early.
///
/// The fallback used to sit in OnSessionLaunched behind a `!HasCareer` gate. On a new campaign
/// that gate is always open, because OnSessionLaunched fires before character creation has even
/// started (Campaign.DoLoadingForGameType, NewCampaign branch: OnSessionStart at Campaign.cs:1695;
/// CC is only launched later, from SandBoxGameManager.OnLoadFinished). The player was handed a
/// career for the vanilla placeholder culture `battania` plus its root choice, and that ghost
/// choice then ate the level-1 career point for the rest of the campaign.
/// </summary>
[TestClass]
public class CareerLifecycleServiceTests
{
    private const string Hero = "main_hero";

    private CareerDataService _dataService;
    private ICareerRegistry _registry;
    private ICareerCreationHandler _creationHandler;
    private IModLogger _logger;
    private CareerLifecycleService _sut;

    private static readonly CareerChoiceDefinition RangerRoot = Choice("ranger_root");
    private static readonly CareerChoiceDefinition RangerT1 = Choice("ranger_t1_a");

    private static readonly CareerDefinition Ranger = new CareerDefinition(
        id: "ranger", displayName: "Ranger", description: "",
        portraitSprite: "", abilityTemplateId: "ambush",
        minClanTier: 0, rootChoiceId: "ranger_root",
        eligibleCultureIds: new List<string> { "gondor" },
        choiceGroupIds: new List<string> { "ranger_g1" });

    private static readonly CareerDefinition Blademaster = new CareerDefinition(
        id: "blademaster_of_ren", displayName: "Blademaster", description: "",
        portraitSprite: "", abilityTemplateId: "riposte",
        minClanTier: 0, rootChoiceId: "blademaster_of_ren_root",
        eligibleCultureIds: new List<string> { "battania" },
        choiceGroupIds: new List<string>());

    private static CareerChoiceDefinition Choice(string id) => new CareerChoiceDefinition(
        id: id, groupId: "ranger_g1", type: ChoiceType.Passive, description: id,
        iconSprite: "", passive: null, mutations: null);

    [TestInitialize]
    public void Setup()
    {
        _dataService = new CareerDataService();
        _registry = Substitute.For<ICareerRegistry>();
        _creationHandler = Substitute.For<ICareerCreationHandler>();
        _logger = Substitute.For<IModLogger>();

        _registry.GetAllCareers().Returns(new List<CareerDefinition> { Blademaster, Ranger });
        _registry.GetCareer("ranger").Returns(Ranger);
        _registry.GetCareer("blademaster_of_ren").Returns(Blademaster);
        // Choice -> owning career. NSubstitute returns null for anything unstubbed, which is
        // exactly the "owner unknown" case the service must treat as keep.
        _registry.GetOwningCareerId("ranger_root").Returns("ranger");
        _registry.GetOwningCareerId("ranger_t1_a").Returns("ranger");
        _registry.GetOwningCareerId("blademaster_of_ren_root").Returns("blademaster_of_ren");

        _sut = new CareerLifecycleService(_dataService, _registry, _creationHandler, _logger);
    }

    // -- the fallback itself (behaviour unchanged, now exercised through the real method) --

    [TestMethod]
    public void AssignFallbackCareerIfMissing_MatchingCulture_AssignsFirstEligibleCareer()
    {
        Assert.IsTrue(_sut.AssignFallbackCareerIfMissing(Hero, "gondor"));
        _creationHandler.Received(1).OnCareerSelected(Hero, "ranger");
    }

    [TestMethod]
    public void AssignFallbackCareerIfMissing_HeroAlreadyHasCareer_DoesNothing()
    {
        _dataService.SetCareer(Hero, "ranger");

        Assert.IsFalse(_sut.AssignFallbackCareerIfMissing(Hero, "gondor"));
        _creationHandler.DidNotReceive().OnCareerSelected(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void AssignFallbackCareerIfMissing_NoCultureMatch_DoesNothing()
    {
        Assert.IsFalse(_sut.AssignFallbackCareerIfMissing(Hero, "erebor"));
        _creationHandler.DidNotReceive().OnCareerSelected(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void AssignFallbackCareerIfMissing_NullCulture_DoesNothing()
    {
        Assert.IsFalse(_sut.AssignFallbackCareerIfMissing(Hero, null));
        _creationHandler.DidNotReceive().OnCareerSelected(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void AssignFallbackCareerIfMissing_CultureCaseDiffers_StillMatches()
    {
        Assert.IsTrue(_sut.AssignFallbackCareerIfMissing(Hero, "GONDOR"));
        _creationHandler.Received(1).OnCareerSelected(Hero, "ranger");
    }

    [TestMethod]
    public void AssignFallbackCareerIfMissing_NullHeroId_DoesNothing()
    {
        Assert.IsFalse(_sut.AssignFallbackCareerIfMissing(null, "gondor"));
        _creationHandler.DidNotReceive().OnCareerSelected(Arg.Any<string>(), Arg.Any<string>());
    }

    // -- the repair pass --

    [TestMethod]
    public void RepairForeignChoices_GhostFromAnotherCareer_IsDropped()
    {
        _dataService.SetCareer(Hero, "ranger");
        _dataService.TryAddChoice(Hero, "blademaster_of_ren_root", 10);
        _dataService.TryAddChoice(Hero, "ranger_root", 10);

        Assert.AreEqual(1, _sut.RepairForeignChoices(Hero));
        CollectionAssert.AreEqual(new List<string> { "ranger_root" },
            new List<string>(_dataService.GetChoiceIds(Hero)));
    }

    [TestMethod]
    public void RepairForeignChoices_CleanSave_IsNoOp()
    {
        _dataService.SetCareer(Hero, "ranger");
        _dataService.TryAddChoice(Hero, "ranger_root", 10);
        _dataService.TryAddChoice(Hero, "ranger_t1_a", 10);

        Assert.AreEqual(0, _sut.RepairForeignChoices(Hero));
        Assert.AreEqual(2, _dataService.GetChoiceCount(Hero));
    }

    [TestMethod]
    public void RepairForeignChoices_ChoiceOwnedByTheHerosOwnCareer_IsKept()
    {
        // The root choice is auto-added at assignment and need not sit in any ChoiceGroup; it
        // still resolves to its own career, so ownership keeps it without a special case.
        _dataService.SetCareer(Hero, "blademaster_of_ren");
        _dataService.TryAddChoice(Hero, "blademaster_of_ren_root", 10);

        Assert.AreEqual(0, _sut.RepairForeignChoices(Hero));
        Assert.AreEqual(1, _dataService.GetChoiceCount(Hero));
    }

    [TestMethod]
    public void RepairForeignChoices_HeroHasNoCareer_KeepsEverything()
    {
        _dataService.TryAddChoice(Hero, "ranger_root", 10);

        Assert.AreEqual(0, _sut.RepairForeignChoices(Hero));
        Assert.AreEqual(1, _dataService.GetChoiceCount(Hero));
    }

    [TestMethod]
    public void RepairForeignChoices_CareerIdNotInRegistry_KeepsEverything()
    {
        // A career retired from XML must not cost the player their whole tree on the next load.
        _dataService.SetCareer(Hero, "retired_career");
        _dataService.TryAddChoice(Hero, "ranger_root", 10);

        Assert.AreEqual(0, _sut.RepairForeignChoices(Hero));
        Assert.AreEqual(1, _dataService.GetChoiceCount(Hero));
    }

    // -- the regression that ties this to the player report --

    [TestMethod]
    public void UnspentPoints_AfterRepairingAGhostChoice_MatchesHeroLevel()
    {
        // Level 1 budget is 2 (root plus one free point). The ghost took the free point, so the
        // player saw "Free Points: 0" from the first minute of the campaign.
        _registry.GetMaxChoicesForHero(1).Returns(2);
        _registry.GetUnspentPoints(Arg.Any<int>(), Arg.Any<int>())
            .Returns(ci => System.Math.Max(0, _registry.GetMaxChoicesForHero(ci.ArgAt<int>(0)) - ci.ArgAt<int>(1)));

        _dataService.SetCareer(Hero, "ranger");
        _dataService.TryAddChoice(Hero, "blademaster_of_ren_root", 10);
        _dataService.TryAddChoice(Hero, "ranger_root", 10);

        Assert.AreEqual(0, _registry.GetUnspentPoints(1, _dataService.GetChoiceCount(Hero)));

        _sut.RepairForeignChoices(Hero);

        Assert.AreEqual(1, _registry.GetUnspentPoints(1, _dataService.GetChoiceCount(Hero)));
    }
    [TestMethod]
    public void RepairForeignChoices_RegistryCannotResolveOwners_KeepsEverything()
    {
        // CareerConfigProvider.EnsureLoaded loads taom_careers.xml and taom_career_choices.xml
        // under SEPARATE try/catch blocks, so a malformed choices file leaves every career
        // resolvable and every choice ownerless. Deleting on "not proven to belong" would wipe the
        // player's entire tree here, permanently, at the next save.
        _registry.GetOwningCareerId(Arg.Any<string>()).Returns((string)null);

        _dataService.SetCareer(Hero, "ranger");
        _dataService.TryAddChoice(Hero, "ranger_root", 10);
        _dataService.TryAddChoice(Hero, "ranger_t1_a", 10);
        _dataService.TryAddChoice(Hero, "ranger_t2_a", 10);

        Assert.AreEqual(0, _sut.RepairForeignChoices(Hero));
        Assert.AreEqual(3, _dataService.GetChoiceCount(Hero));
    }

    [TestMethod]
    public void RepairForeignChoices_ChoiceWhoseOwnerIsUnknown_IsKeptWhileAKnownForeignOneGoes()
    {
        // A choice from a group later dropped from a live career resolves to no owner. It must
        // survive, while a positively-foreign choice in the same list is still removed.
        _dataService.SetCareer(Hero, "ranger");
        _dataService.TryAddChoice(Hero, "ranger_root", 10);
        _dataService.TryAddChoice(Hero, "retired_group_choice", 10);
        _dataService.TryAddChoice(Hero, "blademaster_of_ren_root", 10);

        Assert.AreEqual(1, _sut.RepairForeignChoices(Hero));
        CollectionAssert.AreEqual(new List<string> { "ranger_root", "retired_group_choice" },
            new List<string>(_dataService.GetChoiceIds(Hero)));
    }
}
