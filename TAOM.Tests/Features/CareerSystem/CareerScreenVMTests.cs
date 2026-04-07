using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CareerSystem.UI;

namespace TAOM.Tests.Features.CareerSystem;

[TestClass]
public class CareerScreenVMTests
{
    private CareerDataService _dataService;
    private ICareerRegistry _registry;
    private ICareerPassiveService _passiveService;
    private bool _closeCalled;

    private static readonly CareerDefinition WarbossCareer = new CareerDefinition(
        id: "warboss", displayName: "Warboss", description: "A brute.",
        portraitSprite: "wb_sprite", abilityTemplateId: "rally_horde",
        chargeType: ChargeType.Kills, maxCharge: 100, minClanTier: 0,
        rootChoiceId: "wb_root",
        eligibleCultureIds: new List<string> { "mordor" },
        choiceGroupIds: new List<string> { "wb_brutality" });

    private static readonly CareerChoiceGroupDefinition BrutalityGroup = new CareerChoiceGroupDefinition(
        id: "wb_brutality", careerId: "warboss", tier: 1,
        choiceIds: new List<string> { "wb_brut_key", "wb_brut_p1" });

    private static readonly CareerChoiceDefinition KeystoneChoice = new CareerChoiceDefinition(
        id: "wb_brut_key", groupId: "wb_brutality", type: ChoiceType.Keystone,
        description: "Keystone", iconSprite: "icon", passive: null, mutations: null);

    private static readonly CareerChoiceDefinition PassiveChoice = new CareerChoiceDefinition(
        id: "wb_brut_p1", groupId: "wb_brutality", type: ChoiceType.Passive,
        description: "Passive", iconSprite: "icon",
        passive: new PassiveEffect(PassiveEffectType.Damage, 0.1f),
        mutations: null);

    [TestInitialize]
    public void Setup()
    {
        _dataService = new CareerDataService();
        _registry = Substitute.For<ICareerRegistry>();
        _passiveService = Substitute.For<ICareerPassiveService>();
        _closeCalled = false;

        _registry.GetCareer("warboss").Returns(WarbossCareer);
        _registry.GetGroup("wb_brutality").Returns(BrutalityGroup);
        _registry.GetChoicesForGroup("wb_brutality").Returns(new List<CareerChoiceDefinition> { KeystoneChoice, PassiveChoice });
        _registry.GetMaxChoicesForHero(5).Returns(6);
        _registry.IsTierAvailable(5, 1).Returns(true);
        _registry.IsTierAvailable(5, 2).Returns(false);
        _registry.IsTierAvailable(5, 3).Returns(false);
    }

    [TestMethod]
    public void HasCareer_NoCareerSet_ReturnsFalse()
    {
        _dataService.GetOrCreateData("hero1");
        var vm = CreateVM();
        Assert.IsFalse(vm.HasCareer);
    }

    [TestMethod]
    public void HasCareer_CareerSet_ReturnsTrue()
    {
        SetupHeroWithCareer();
        var vm = CreateVM();
        Assert.IsTrue(vm.HasCareer);
    }

    [TestMethod]
    public void FreeCareerPoints_TwoChoicesFromLevel5_Returns4()
    {
        SetupHeroWithCareer();
        _dataService.TryAddChoice("hero1", "wb_root", 10);
        _dataService.TryAddChoice("hero1", "wb_brut_key", 10);

        var vm = CreateVM();
        Assert.AreEqual(4, vm.FreeCareerPoints); // 6 max - 2 taken = 4
    }

    [TestMethod]
    public void ChoiceGroupsTier1_HasGroups()
    {
        SetupHeroWithCareer();
        var vm = CreateVM();
        Assert.AreEqual(1, vm.ChoiceGroupsTier1.Count);
        Assert.AreEqual(2, vm.ChoiceGroupsTier1[0].Choices.Count);
    }

    [TestMethod]
    public void ExecuteClose_CallsCloseAction()
    {
        SetupHeroWithCareer();
        var vm = CreateVM();
        vm.ExecuteClose();
        Assert.IsTrue(_closeCalled);
    }

    [TestMethod]
    public void ExecuteSelectChoice_ValidChoice_AddsAndRefreshes()
    {
        SetupHeroWithCareer();
        _registry.GetMaxChoicesForHero(5).Returns(10);
        var vm = CreateVM();

        vm.ExecuteSelectChoice("wb_brut_key");

        Assert.IsTrue(_dataService.GetOrCreateData("hero1").HasChoice("wb_brut_key"));
        _passiveService.Received().RefreshCache(_dataService, _registry);
    }

    private void SetupHeroWithCareer()
    {
        _dataService.SetCareer("hero1", "warboss");
    }

    private CareerScreenVM CreateVM()
    {
        return new CareerScreenVM(_dataService, _registry, _passiveService, "hero1", 5, () => _closeCalled = true);
    }
}
