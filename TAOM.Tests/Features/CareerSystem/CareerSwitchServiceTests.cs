using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

[TestClass]
public class CareerSwitchServiceTests
{
    private CareerDataService _dataService;
    private ICareerRegistry _registry;
    private ICareerPassiveService _passiveService;
    private IModLogger _logger;
    private ICareerHeroAdapter _hero;
    private CareerSwitchService _service;

    [TestInitialize]
    public void Setup()
    {
        _dataService = new CareerDataService();
        _registry = Substitute.For<ICareerRegistry>();
        _passiveService = Substitute.For<ICareerPassiveService>();
        _logger = Substitute.For<IModLogger>();
        _hero = Substitute.For<ICareerHeroAdapter>();

        _hero.CultureStringId.Returns("mordor");
        _hero.ClanTier.Returns(2);
        _hero.Level.Returns(10);
        _hero.StringId.Returns("hero1");

        _registry.IsEligible("ranger", _hero).Returns(true);
        _registry.IsEligible("warboss", _hero).Returns(true);
        _registry.GetMaxChoicesForHero(10).Returns(11);

        _registry.GetCareer("warboss").Returns(new CareerDefinition(
            id: "warboss", displayName: "Warboss", description: "", portraitSprite: "",
            abilityTemplateId: "rally",
            minClanTier: 0, rootChoiceId: "wb_root",
            eligibleCultureIds: new List<string> { "mordor" },
            choiceGroupIds: new List<string>()));

        _registry.GetCareer("ranger").Returns(new CareerDefinition(
            id: "ranger", displayName: "Ranger", description: "", portraitSprite: "",
            abilityTemplateId: "ambush",
            minClanTier: 0, rootChoiceId: "ranger_root",
            eligibleCultureIds: new List<string> { "gondor", "mordor" },
            choiceGroupIds: new List<string>()));

        _service = new CareerSwitchService(_dataService, _registry, _passiveService, _logger);
    }

    [TestMethod]
    public void SwitchCareer_EligibleHero_ClearsOldAndSetsNew()
    {
        _dataService.SetCareer("hero1", "warboss");
        _dataService.TryAddChoice("hero1", "wb_root", 10);
        _dataService.TryAddChoice("hero1", "wb_brut_p1", 10);

        var result = _service.SwitchCareer("hero1", _hero, "ranger");

        Assert.IsTrue(result);
        Assert.AreEqual("ranger", _dataService.GetCareerStringId("hero1"));
        Assert.AreEqual(1, _dataService.GetChoiceCount("hero1")); // only root
        Assert.IsTrue(_dataService.GetOrCreateData("hero1").HasChoice("ranger_root"));
    }

    [TestMethod]
    public void SwitchCareer_IneligibleHero_ReturnsFalse()
    {
        _registry.IsEligible("knight", _hero).Returns(false);

        _dataService.SetCareer("hero1", "warboss");
        var result = _service.SwitchCareer("hero1", _hero, "knight");

        Assert.IsFalse(result);
        Assert.AreEqual("warboss", _dataService.GetCareerStringId("hero1"));
    }

    [TestMethod]
    public void SwitchCareer_RefreshesCache()
    {
        _dataService.SetCareer("hero1", "warboss");
        _service.SwitchCareer("hero1", _hero, "ranger");
        _passiveService.Received(1).RefreshCache(_dataService, _registry);
    }

    [TestMethod]
    public void CanSwitch_NullHero_ReturnsFalse()
    {
        Assert.IsFalse(_service.CanSwitch(null, "warboss"));
    }

    [TestMethod]
    public void CanSwitch_EligibleHero_ReturnsTrue()
    {
        Assert.IsTrue(_service.CanSwitch(_hero, "warboss"));
    }

    [TestMethod]
    public void CanSwitch_SameCareer_ReturnsFalse()
    {
        // Switching to your own current career is a no-op; the service must reject it so the
        // dialogue path doesn't waste a player click + the screen doesn't list the current career.
        // Uses hero.StringId to look up the current career via the data service.
        _dataService.SetCareer("hero1", "warboss");

        Assert.IsFalse(_service.CanSwitch(_hero, "warboss"));
    }

    [TestMethod]
    public void SwitchCareer_SameCareer_ReturnsFalse()
    {
        _dataService.SetCareer("hero1", "warboss");

        var result = _service.SwitchCareer("hero1", _hero, "warboss");

        Assert.IsFalse(result);
        Assert.AreEqual("warboss", _dataService.GetCareerStringId("hero1"));
        // Cache refresh must NOT fire on a rejected switch.
        _passiveService.DidNotReceive().RefreshCache(Arg.Any<ICareerDataService>(), Arg.Any<ICareerRegistry>());
    }

    [TestMethod]
    public void CanSwitch_EmptyHeroStringId_FallsThroughToEligibility()
    {
        // If a hero adapter returns empty StringId (mock state or test fixture mistake), the
        // same-career guard should silently skip and the call should fall through to IsEligible.
        // This guards the intent of the guard (defensive same-career rejection) from breaking
        // unrelated eligibility checks.
        _hero.StringId.Returns("");

        Assert.IsTrue(_service.CanSwitch(_hero, "warboss"));
    }
}
