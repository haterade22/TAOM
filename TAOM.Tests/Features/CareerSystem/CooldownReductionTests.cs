using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

// Issue #104 Option B — CooldownReduction property on AbilityTemplateData + MinCooldownSeconds
// on GlobalTuning + AdjustCooldown plumbing on CareerAbility / CareerAbilityService. Replaces
// the 98 dead MaxCharge mutations that the cooldown rework left unread (#103). Designers who
// edit -20/-30 entries in taom_career_choices.xml now actually shorten the 30s global cooldown,
// floored at MinCooldownSeconds (default 5s).
[TestClass]
public class CooldownReductionTests
{
    [TestMethod]
    public void AbilityTemplateData_CooldownReduction_DefaultsToZero()
    {
        var data = new AbilityTemplateData();
        Assert.AreEqual(0f, data.CooldownReduction, 0.001f);
    }

    [TestMethod]
    public void AbilityTemplateData_CopyCtor_PreservesCooldownReduction()
    {
        var source = new AbilityTemplateData { Id = "x", CooldownReduction = 6.5f };
        var copy = new AbilityTemplateData(source);
        Assert.AreEqual(6.5f, copy.CooldownReduction, 0.001f);
    }

    [TestMethod]
    public void GlobalTuning_Default_MinCooldownIsFiveSeconds()
    {
        Assert.AreEqual(5f, GlobalTuning.Default.MinCooldownSeconds, 0.001f);
    }

    [TestMethod]
    public void GlobalTuning_Ctor_AcceptsMinCooldown()
    {
        var tuning = new GlobalTuning(30f, 5f);
        Assert.AreEqual(30f, tuning.CooldownSeconds, 0.001f);
        Assert.AreEqual(5f, tuning.MinCooldownSeconds, 0.001f);
    }

    [TestMethod]
    public void CareerAbility_AdjustCooldown_ShortensRemainingByReduction()
    {
        var ability = new CareerAbility("t", ChargeType.CooldownOnly, 0f, 30f);
        ability.Activate();   // CooldownRemaining = 30
        ability.AdjustCooldown(reductionSeconds: 9f, minCooldownSeconds: 5f);
        Assert.AreEqual(21f, ability.CooldownRemaining, 0.001f);
    }

    [TestMethod]
    public void CareerAbility_AdjustCooldown_FloorsAtMinCooldown()
    {
        var ability = new CareerAbility("t", ChargeType.CooldownOnly, 0f, 30f);
        ability.Activate();
        ability.AdjustCooldown(reductionSeconds: 100f, minCooldownSeconds: 5f);
        Assert.AreEqual(5f, ability.CooldownRemaining, 0.001f);
    }

    [TestMethod]
    public void CareerAbility_AdjustCooldown_ZeroReduction_NoChange()
    {
        var ability = new CareerAbility("t", ChargeType.CooldownOnly, 0f, 30f);
        ability.Activate();
        ability.AdjustCooldown(reductionSeconds: 0f, minCooldownSeconds: 5f);
        Assert.AreEqual(30f, ability.CooldownRemaining, 0.001f);
    }

    [TestMethod]
    public void CareerAbility_AdjustCooldown_NegativeReduction_ClampsToZero()
    {
        // Negative reduction = bonus extension. Disallowed (designers should not lengthen
        // cooldown via reduction; use a separate property if needed). Treat as zero.
        var ability = new CareerAbility("t", ChargeType.CooldownOnly, 0f, 30f);
        ability.Activate();
        ability.AdjustCooldown(reductionSeconds: -5f, minCooldownSeconds: 5f);
        Assert.AreEqual(30f, ability.CooldownRemaining, 0.001f);
    }

    [TestMethod]
    public void CareerAbility_AdjustCooldown_NaNReduction_NoChange()
    {
        var ability = new CareerAbility("t", ChargeType.CooldownOnly, 0f, 30f);
        ability.Activate();
        ability.AdjustCooldown(reductionSeconds: float.NaN, minCooldownSeconds: 5f);
        Assert.AreEqual(30f, ability.CooldownRemaining, 0.001f);
    }

    [TestMethod]
    public void CareerAbility_AdjustCooldown_InfinityReduction_FloorsAtMin()
    {
        var ability = new CareerAbility("t", ChargeType.CooldownOnly, 0f, 30f);
        ability.Activate();
        ability.AdjustCooldown(reductionSeconds: float.PositiveInfinity, minCooldownSeconds: 5f);
        Assert.AreEqual(30f, ability.CooldownRemaining, 0.001f);
    }

    [TestMethod]
    public void CareerAbility_AdjustCooldown_NaNMin_NoChange()
    {
        var ability = new CareerAbility("t", ChargeType.CooldownOnly, 0f, 30f);
        ability.Activate();
        ability.AdjustCooldown(reductionSeconds: 5f, minCooldownSeconds: float.NaN);
        Assert.AreEqual(30f, ability.CooldownRemaining, 0.001f);
    }

    [TestMethod]
    public void CareerAbility_AdjustCooldown_ChargeBasedAbility_NoOp()
    {
        var ability = new CareerAbility("t", ChargeType.Kills, 100f, 0f);
        ability.AdjustCooldown(reductionSeconds: 9f, minCooldownSeconds: 5f);
        Assert.AreEqual(0f, ability.CooldownRemaining, 0.001f);
    }

    [TestMethod]
    public void CareerAbilityService_ApplyCooldownAdjustment_UnknownHero_LogsWarning_NoThrow()
    {
        var config = Substitute.For<ICareerConfigProvider>();
        config.GetAbilityTuning().Returns(AbilityTuningConfig.Default);
        var logger = Substitute.For<IModLogger>();
        var sut = new CareerAbilityService(config, logger);

        sut.ApplyCooldownAdjustment("nobody", reductionSeconds: 6f, minCooldownSeconds: 5f);
        logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("nobody")));
    }

    [TestMethod]
    public void CareerAbilityService_ApplyCooldownAdjustment_ShortensActiveCooldown()
    {
        var heroId = "hero_1";
        var careerId = "captain_of_osgiliath";
        var career = new CareerDefinition(
            id: careerId, displayName: "Captain", description: "",
            portraitSprite: "", abilityTemplateId: "mithril_bastion",
            minClanTier: 0, rootChoiceId: "",
            eligibleCultureIds: new List<string>(), choiceGroupIds: new List<string>());

        var config = Substitute.For<ICareerConfigProvider>();
        config.GetAbilityTuning().Returns(AbilityTuningConfig.Default); // 30s, min 5s
        var registry = Substitute.For<ICareerRegistry>();
        registry.GetCareer(careerId).Returns(career);
        var dataService = Substitute.For<ICareerDataService>();
        dataService.GetCareerStringId(heroId).Returns(careerId);

        var sut = new CareerAbilityService(config, Substitute.For<IModLogger>());
        var ability = sut.GetOrCreateAbility(heroId, registry, dataService);
        sut.ActivateAbility(heroId);

        sut.ApplyCooldownAdjustment(heroId, reductionSeconds: 9f, minCooldownSeconds: 5f);

        Assert.AreEqual(21f, ability.CooldownRemaining, 0.001f);
    }

    [TestMethod]
    public void CareerAbilityService_ApplyCooldownAdjustment_FloorsAtMin()
    {
        var heroId = "hero_2";
        var careerId = "warden";
        var career = new CareerDefinition(
            id: careerId, displayName: "Warden", description: "",
            portraitSprite: "", abilityTemplateId: "starlight",
            minClanTier: 0, rootChoiceId: "",
            eligibleCultureIds: new List<string>(), choiceGroupIds: new List<string>());

        var config = Substitute.For<ICareerConfigProvider>();
        config.GetAbilityTuning().Returns(AbilityTuningConfig.Default);
        var registry = Substitute.For<ICareerRegistry>();
        registry.GetCareer(careerId).Returns(career);
        var dataService = Substitute.For<ICareerDataService>();
        dataService.GetCareerStringId(heroId).Returns(careerId);

        var sut = new CareerAbilityService(config, Substitute.For<IModLogger>());
        var ability = sut.GetOrCreateAbility(heroId, registry, dataService);
        sut.ActivateAbility(heroId);

        sut.ApplyCooldownAdjustment(heroId, reductionSeconds: 999f, minCooldownSeconds: 5f);

        Assert.AreEqual(5f, ability.CooldownRemaining, 0.001f);
    }
}
