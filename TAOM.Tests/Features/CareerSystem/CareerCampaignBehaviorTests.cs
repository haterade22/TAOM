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
        var service = new CareerLifecycleService(_dataService, _registry, _creationHandler, Substitute.For<ICareerPassiveService>(), Substitute.For<TAOM.Adapters.ICareerPurseAdapter>(), _logger);
        return service.AssignFallbackCareerIfMissing(heroStringId, cultureId);
    }
}

/// <summary>
/// <c>CareerCampaignBehavior.OnSessionLaunched</c> runs on the engine's campaign event, so its order
/// cannot be exercised without a campaign. The order is load-bearing: the repair drops another
/// career's picks so the migration judges a clean list, and the cache refresh must come last so
/// the passives reflect both. Pinned against the source, like the other ordering gates.
/// </summary>
[TestClass]
public class CareerSessionLaunchOrderTests
{
    [TestMethod]
    public void OnSessionLaunched_RunsTheRepairThenTheMigrationThenTheCacheRefresh()
    {
        var src = TAOM.Tests.Infrastructure.RepoPaths.ReadSource(
            "Main/Features/CareerSystem/CareerCampaignBehavior.cs", stripComments: true);

        int start = src.IndexOf("void OnSessionLaunched(", System.StringComparison.Ordinal);
        int end = src.IndexOf("void OnGameLoaded(", System.StringComparison.Ordinal);
        Assert.IsTrue(start >= 0 && end > start, "OnSessionLaunched or OnGameLoaded was renamed: update this pin.");
        var body = src.Substring(start, end - start);

        int repair = body.IndexOf("_lifecycle.RepairForeignChoices", System.StringComparison.Ordinal);
        int migrate = body.IndexOf("_lifecycle.MigrateAllTreeVersions(", System.StringComparison.Ordinal);
        int refresh = body.IndexOf("_passiveService.RefreshCache", System.StringComparison.Ordinal);

        Assert.IsTrue(repair >= 0, "OnSessionLaunched must call RepairForeignChoices");
        Assert.IsTrue(migrate >= 0, "OnSessionLaunched must call MigrateAllTreeVersions (every hero, not only the player)");
        Assert.IsTrue(refresh >= 0, "OnSessionLaunched must call RefreshCache");
        Assert.IsTrue(repair < migrate, "the repair must run before the migration, so it judges a clean list");
        Assert.IsTrue(migrate < refresh, "the migration must run before the cache refresh, so the passives reflect the refund");
    }

    [TestMethod]
    public void OnGameLoaded_ClearsTheUnknownCareer_ThenAssignsTheFallback_ThenTellsThePlayer()
    {
        // #1046: the message says a new career was chosen, so it may show only after the fallback
        // reports that it assigned one (a culture with no eligible career leaves the hero without).
        var src = TAOM.Tests.Infrastructure.RepoPaths.ReadSource(
            "Main/Features/CareerSystem/CareerCampaignBehavior.cs", stripComments: true);

        int start = src.IndexOf("void OnGameLoaded(", System.StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "OnGameLoaded was renamed: update this pin.");
        int next = src.IndexOf("private void ", start + 1, System.StringComparison.Ordinal);
        var body = next > start ? src.Substring(start, next - start) : src.Substring(start);

        int clear = body.IndexOf("_lifecycle.ClearUnknownCareer(", System.StringComparison.Ordinal);
        int assign = body.IndexOf("_lifecycle.AssignFallbackCareerIfMissing(", System.StringComparison.Ordinal);
        int message = body.IndexOf("taom_career_unknown_reset", System.StringComparison.Ordinal);

        Assert.IsTrue(clear >= 0 && assign >= 0 && message >= 0, "OnGameLoaded must clear, assign and tell the player");
        Assert.IsTrue(clear < assign, "the clear must run before the fallback, so the fallback sees no career");
        Assert.IsTrue(assign < message, "the message must follow the fallback's result, never precede it");
    }
}
