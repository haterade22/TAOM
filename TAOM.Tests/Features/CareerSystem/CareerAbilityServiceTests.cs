using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

[TestClass]
public class CareerAbilityServiceTests
{
    private const string HeroId = "hero_1";
    private const string CareerId = "warboss";

    private static readonly CareerDefinition Career = new CareerDefinition(
        id: CareerId,
        displayName: "Warboss",
        description: "",
        portraitSprite: "",
        abilityTemplateId: "rally_horde",
        minClanTier: 0,
        rootChoiceId: "",
        eligibleCultureIds: new List<string>(),
        choiceGroupIds: new List<string>());

    private ICareerConfigProvider _config;
    private ICareerRegistry _registry;
    private ICareerDataService _dataService;
    private CareerAbilityService _sut;

    [TestInitialize]
    public void Setup()
    {
        _config = Substitute.For<ICareerConfigProvider>();
        _config.GetAbilityTuning().Returns(AbilityTuningConfig.Default); // Global cooldown = 30s

        _registry = Substitute.For<ICareerRegistry>();
        _registry.GetCareer(CareerId).Returns(Career);

        _dataService = Substitute.For<ICareerDataService>();
        _dataService.GetCareerStringId(HeroId).Returns(CareerId);

        _sut = new CareerAbilityService(_config, Substitute.For<IModLogger>());
    }

    [TestMethod]
    public void GetOrCreateAbility_CreatesCooldownOnlyAbility()
    {
        var ability = _sut.GetOrCreateAbility(HeroId, _registry, _dataService);

        Assert.IsNotNull(ability);
        Assert.AreEqual(ChargeType.CooldownOnly, ability.ChargeType);
    }

    [TestMethod]
    public void GetOrCreateAbility_UsesConfiguredCooldownSeconds()
    {
        _config.GetAbilityTuning().Returns(new AbilityTuningConfig(
            new GlobalTuning(45f),
            InfantryTuning.Default,
            RangedTuning.Default,
            CavalryTuning.Default));

        var ability = _sut.GetOrCreateAbility(HeroId, _registry, _dataService);

        Assert.AreEqual(45f, ability.CooldownDuration, 0.001f);
    }

    [TestMethod]
    public void IsAbilityReady_FreshlyCreated_ReturnsTrue()
    {
        _sut.GetOrCreateAbility(HeroId, _registry, _dataService);

        Assert.IsTrue(_sut.IsAbilityReady(HeroId));
    }

    [TestMethod]
    public void IsAbilityReady_AfterActivate_ReturnsFalse()
    {
        _sut.GetOrCreateAbility(HeroId, _registry, _dataService);
        _sut.ActivateAbility(HeroId);

        Assert.IsFalse(_sut.IsAbilityReady(HeroId));
    }

    [TestMethod]
    public void IsAbilityReady_AfterCooldownTicksFully_ReturnsTrue()
    {
        _sut.GetOrCreateAbility(HeroId, _registry, _dataService);
        _sut.ActivateAbility(HeroId);

        // Default global cooldown = 30s
        for (var i = 0; i < 30; i++)
            _sut.Tick(HeroId, 1f);

        Assert.IsTrue(_sut.IsAbilityReady(HeroId));
    }

    [TestMethod]
    public void IsAbilityReady_BeforeCooldownExpires_ReturnsFalse()
    {
        _sut.GetOrCreateAbility(HeroId, _registry, _dataService);
        _sut.ActivateAbility(HeroId);

        for (var i = 0; i < 29; i++)
            _sut.Tick(HeroId, 1f);

        Assert.IsFalse(_sut.IsAbilityReady(HeroId));
    }

    [TestMethod]
    public void GetCooldownRemaining_FreshlyCreated_ReturnsZero()
    {
        _sut.GetOrCreateAbility(HeroId, _registry, _dataService);

        Assert.AreEqual(0f, _sut.GetCooldownRemaining(HeroId), 0.001f);
    }

    [TestMethod]
    public void GetCooldownRemaining_AfterActivate_ReturnsFullDuration()
    {
        _sut.GetOrCreateAbility(HeroId, _registry, _dataService);
        _sut.ActivateAbility(HeroId);

        Assert.AreEqual(30f, _sut.GetCooldownRemaining(HeroId), 0.001f);
    }

    [TestMethod]
    public void GetCooldownRemaining_AfterPartialTick_ReturnsRemaining()
    {
        _sut.GetOrCreateAbility(HeroId, _registry, _dataService);
        _sut.ActivateAbility(HeroId);
        _sut.Tick(HeroId, 10f);

        Assert.AreEqual(20f, _sut.GetCooldownRemaining(HeroId), 0.001f);
    }

    [TestMethod]
    public void GetCooldownRemaining_NoAbilityForHero_ReturnsZero()
    {
        // Never called GetOrCreateAbility — hero has no entry.
        Assert.AreEqual(0f, _sut.GetCooldownRemaining("unknown_hero"), 0.001f);
    }

    [TestMethod]
    public void Tick_LargeDt_DrainsFullElapsedTime()
    {
        // Regression for Codex Review #30 Finding 1: long frames must drain the full elapsed
        // time, not a fixed-interval bucket. A 2.5s frame must subtract 2.5s of cooldown.
        _sut.GetOrCreateAbility(HeroId, _registry, _dataService);
        _sut.ActivateAbility(HeroId);

        _sut.Tick(HeroId, 2.5f);

        Assert.AreEqual(27.5f, _sut.GetCooldownRemaining(HeroId), 0.001f);
    }

    [TestMethod]
    public void Tick_FractionalDt_AccumulatesAcrossFrames()
    {
        // Per-frame Tick(dt) at 60fps drains ~1s after 60 frames.
        _sut.GetOrCreateAbility(HeroId, _registry, _dataService);
        _sut.ActivateAbility(HeroId);

        for (var i = 0; i < 60; i++)
            _sut.Tick(HeroId, 1f / 60f);

        Assert.AreEqual(29f, _sut.GetCooldownRemaining(HeroId), 0.01f);
    }
}
