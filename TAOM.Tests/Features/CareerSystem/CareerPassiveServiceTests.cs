using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

[TestClass]
public class CareerPassiveServiceTests
{
    private CareerPassiveService _service;
    private ICareerDataService _dataService;
    private ICareerRegistry _registry;

    private static readonly CareerDefinition WarbossCareer = new CareerDefinition(
        id: "warboss", displayName: "Warboss", description: "", portraitSprite: "",
        abilityTemplateId: "rally",
        minClanTier: 0, rootChoiceId: "wb_root",
        eligibleCultureIds: new List<string> { "mordor" },
        choiceGroupIds: new List<string>());

    [TestInitialize]
    public void Setup()
    {
        _service = new CareerPassiveService(Substitute.For<IModLogger>());
        _dataService = new CareerDataService();
        _registry = Substitute.For<ICareerRegistry>();

        _registry.GetCareer("warboss").Returns(WarbossCareer);
    }

    [TestMethod]
    public void GetPassiveMagnitude_NoCareers_ReturnsZero()
    {
        _service.RefreshCache(_dataService, _registry);
        Assert.AreEqual(0f, _service.GetPassiveMagnitude("hero1", PassiveEffectType.Damage));
    }

    [TestMethod]
    public void GetPassiveMagnitude_SingleChoice_ReturnsMagnitude()
    {
        _dataService.SetCareer("hero1", "warboss");
        _dataService.TryAddChoice("hero1", "wb_brut_p1", 10);

        _registry.GetChoice("wb_brut_p1").Returns(new CareerChoiceDefinition(
            id: "wb_brut_p1", groupId: "wb_brutality", type: ChoiceType.Passive,
            description: "", iconSprite: "",
            passive: new PassiveEffect(PassiveEffectType.Damage, 0.10f),
            mutations: null));

        _registry.GetChoice("wb_root").Returns(new CareerChoiceDefinition(
            id: "wb_root", groupId: "", type: ChoiceType.Passive,
            description: "", iconSprite: "", passive: null, mutations: null));

        _service.RefreshCache(_dataService, _registry);

        Assert.AreEqual(0.10f, _service.GetPassiveMagnitude("hero1", PassiveEffectType.Damage), 0.001f);
    }

    [TestMethod]
    public void GetPassiveMagnitude_MultipleChoicesSameType_SumsMagnitudes()
    {
        _dataService.SetCareer("hero1", "warboss");
        _dataService.TryAddChoice("hero1", "wb_brut_p1", 10);
        _dataService.TryAddChoice("hero1", "wb_brut_p2", 10);

        _registry.GetChoice("wb_brut_p1").Returns(new CareerChoiceDefinition(
            id: "wb_brut_p1", groupId: "wb_brutality", type: ChoiceType.Passive,
            description: "", iconSprite: "",
            passive: new PassiveEffect(PassiveEffectType.Damage, 0.10f),
            mutations: null));

        _registry.GetChoice("wb_brut_p2").Returns(new CareerChoiceDefinition(
            id: "wb_brut_p2", groupId: "wb_brutality", type: ChoiceType.Passive,
            description: "", iconSprite: "",
            passive: new PassiveEffect(PassiveEffectType.Damage, 0.05f),
            mutations: null));

        _registry.GetChoice("wb_root").Returns(new CareerChoiceDefinition(
            id: "wb_root", groupId: "", type: ChoiceType.Passive,
            description: "", iconSprite: "", passive: null, mutations: null));

        _service.RefreshCache(_dataService, _registry);

        Assert.AreEqual(0.15f, _service.GetPassiveMagnitude("hero1", PassiveEffectType.Damage), 0.001f);
    }

    [TestMethod]
    public void HasActivePassive_WithPassive_ReturnsTrue()
    {
        _dataService.SetCareer("hero1", "warboss");
        _dataService.TryAddChoice("hero1", "wb_brut_p1", 10);

        _registry.GetChoice("wb_brut_p1").Returns(new CareerChoiceDefinition(
            id: "wb_brut_p1", groupId: "wb_brutality", type: ChoiceType.Passive,
            description: "", iconSprite: "",
            passive: new PassiveEffect(PassiveEffectType.Damage, 0.10f),
            mutations: null));

        _registry.GetChoice("wb_root").Returns(new CareerChoiceDefinition(
            id: "wb_root", groupId: "", type: ChoiceType.Passive,
            description: "", iconSprite: "", passive: null, mutations: null));

        _service.RefreshCache(_dataService, _registry);

        Assert.IsTrue(_service.HasActivePassive("hero1", PassiveEffectType.Damage));
    }

    [TestMethod]
    public void HasActivePassive_NoPassive_ReturnsFalse()
    {
        _service.RefreshCache(_dataService, _registry);
        Assert.IsFalse(_service.HasActivePassive("hero1", PassiveEffectType.Damage));
    }

    [TestMethod]
    public void RefreshCache_IncludesRootChoicePassive()
    {
        _dataService.SetCareer("hero1", "warboss");

        _registry.GetChoice("wb_root").Returns(new CareerChoiceDefinition(
            id: "wb_root", groupId: "", type: ChoiceType.Passive,
            description: "", iconSprite: "",
            passive: new PassiveEffect(PassiveEffectType.TroopDamage, 0.05f),
            mutations: null));

        _service.RefreshCache(_dataService, _registry);

        Assert.AreEqual(0.05f, _service.GetPassiveMagnitude("hero1", PassiveEffectType.TroopDamage), 0.001f);
    }

    [TestMethod]
    public void RefreshCache_DoesNotDoubleCountRootIfInChoices()
    {
        _dataService.SetCareer("hero1", "warboss");
        _dataService.TryAddChoice("hero1", "wb_root", 10);

        _registry.GetChoice("wb_root").Returns(new CareerChoiceDefinition(
            id: "wb_root", groupId: "", type: ChoiceType.Passive,
            description: "", iconSprite: "",
            passive: new PassiveEffect(PassiveEffectType.TroopDamage, 0.05f),
            mutations: null));

        _service.RefreshCache(_dataService, _registry);

        Assert.AreEqual(0.05f, _service.GetPassiveMagnitude("hero1", PassiveEffectType.TroopDamage), 0.001f);
    }
}
