using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

/// <summary>
/// Tests the auto-assign logic extracted from CareerCampaignBehavior.OnSessionLaunched.
/// Since the behavior uses Hero.MainHero (sealed BL type), we test the logic via
/// CareerCreationHandler which the behavior delegates to.
///
/// The auto-assign algorithm: iterate GetAllCareers(), find first career where
/// any EligibleCultureId matches hero's culture (case-insensitive), call
/// OnCareerSelected. First match wins.
/// </summary>
[TestClass]
public class CareerAutoAssignTests
{
    private CareerDataService _dataService;
    private ICareerRegistry _registry;
    private ICareerPassiveService _passiveService;
    private ICareerCreationHandler _creationHandler;
    private IModLogger _logger;

    private static readonly CareerDefinition WarbossCareer = new CareerDefinition(
        id: "warboss", displayName: "Warboss", description: "",
        portraitSprite: "", abilityTemplateId: "rally",
        minClanTier: 0, rootChoiceId: "wb_root",
        eligibleCultureIds: new List<string> { "mordor" },
        choiceGroupIds: new List<string>());

    private static readonly CareerDefinition RangerCareer = new CareerDefinition(
        id: "ranger", displayName: "Ranger", description: "",
        portraitSprite: "", abilityTemplateId: "ambush",
        minClanTier: 0, rootChoiceId: "ranger_root",
        eligibleCultureIds: new List<string> { "gondor", "sturgia" },
        choiceGroupIds: new List<string>());

    [TestInitialize]
    public void Setup()
    {
        _dataService = new CareerDataService();
        _registry = Substitute.For<ICareerRegistry>();
        _passiveService = Substitute.For<ICareerPassiveService>();
        _creationHandler = Substitute.For<ICareerCreationHandler>();
        _logger = Substitute.For<IModLogger>();

        _registry.GetAllCareers().Returns(new List<CareerDefinition> { WarbossCareer, RangerCareer });
    }

    [TestMethod]
    public void AutoAssign_MordorCulture_AssignsWarboss()
    {
        var result = TryAutoAssign("hero1", "mordor");

        Assert.IsTrue(result);
        _creationHandler.Received(1).OnCareerSelected("hero1", "warboss");
    }

    [TestMethod]
    public void AutoAssign_GondorCulture_AssignsRanger()
    {
        var result = TryAutoAssign("hero1", "gondor");

        Assert.IsTrue(result);
        _creationHandler.Received(1).OnCareerSelected("hero1", "ranger");
    }

    [TestMethod]
    public void AutoAssign_NoMatchingCulture_DoesNotAssign()
    {
        var result = TryAutoAssign("hero1", "erebor");

        Assert.IsFalse(result);
        _creationHandler.DidNotReceive().OnCareerSelected(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void AutoAssign_AlreadyHasCareer_DoesNotOverride()
    {
        _dataService.SetCareer("hero1", "warboss");

        var result = TryAutoAssign("hero1", "mordor");

        Assert.IsFalse(result);
        _creationHandler.DidNotReceive().OnCareerSelected(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void AutoAssign_NullCulture_DoesNotAssign()
    {
        var result = TryAutoAssign("hero1", null);

        Assert.IsFalse(result);
        _creationHandler.DidNotReceive().OnCareerSelected(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void AutoAssign_EmptyRegistry_DoesNotAssign()
    {
        _registry.GetAllCareers().Returns(new List<CareerDefinition>());

        var result = TryAutoAssign("hero1", "mordor");

        Assert.IsFalse(result);
        _creationHandler.DidNotReceive().OnCareerSelected(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void AutoAssign_CaseInsensitiveCultureMatch()
    {
        var result = TryAutoAssign("hero1", "Mordor");

        Assert.IsTrue(result);
        _creationHandler.Received(1).OnCareerSelected("hero1", "warboss");
    }

    /// <summary>
    /// Calls the REAL fallback. It used to be a copy of the algorithm pasted into this file,
    /// which is precisely why the "should it run at all on a new campaign" defect could ship: the
    /// copy had no notion of WHEN the behavior invokes it.
    /// </summary>
    private bool TryAutoAssign(string heroStringId, string cultureId)
    {
        var service = new CareerLifecycleService(_dataService, _registry, _creationHandler, _logger);
        return service.AssignFallbackCareerIfMissing(heroStringId, cultureId);
    }
}
