using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

[TestClass]
public class CareerRegistryTests
{
    private ICareerConfigProvider _config;
    private ICareerHeroAdapter _hero;
    private CareerRegistry _registry;

    private static readonly CareerDefinition WarbossCareer = new CareerDefinition(
        id: "warboss",
        displayName: "Warboss",
        description: "A brute.",
        portraitSprite: "wb_sprite",
        abilityTemplateId: "rally_horde",
        minClanTier: 1,
        rootChoiceId: "wb_root",
        eligibleCultureIds: new List<string> { "mordor" },
        choiceGroupIds: new List<string> { "wb_brutality" });

    private static readonly CareerChoiceDefinition RootChoice = new CareerChoiceDefinition(
        id: "wb_root", groupId: "", type: ChoiceType.Passive,
        description: "Root", iconSprite: "icon",
        passive: new PassiveEffect(PassiveEffectType.TroopDamage, 0.05f),
        mutations: null);

    private static readonly CareerChoiceDefinition KeystoneChoice = new CareerChoiceDefinition(
        id: "wb_brut_key", groupId: "wb_brutality", type: ChoiceType.Keystone,
        description: "Keystone", iconSprite: "icon", passive: null, mutations: null);

    private static readonly CareerChoiceGroupDefinition BrutalityGroup = new CareerChoiceGroupDefinition(
        id: "wb_brutality", careerId: "warboss", tier: 1,
        choiceIds: new List<string> { "wb_brut_key" });

    [TestInitialize]
    public void Setup()
    {
        _config = Substitute.For<ICareerConfigProvider>();
        _config.LoadCareers().Returns(new List<CareerDefinition> { WarbossCareer });
        _config.LoadChoiceGroups().Returns(new List<CareerChoiceGroupDefinition> { BrutalityGroup });
        _config.LoadChoices().Returns(new List<CareerChoiceDefinition> { RootChoice, KeystoneChoice });
        _config.GetMaxPerkPoints().Returns(30);

        _hero = Substitute.For<ICareerHeroAdapter>();
        _hero.CultureStringId.Returns("mordor");
        _hero.ClanTier.Returns(2);
        _hero.Level.Returns(5);

        _registry = new CareerRegistry(_config, Substitute.For<IModLogger>());
    }

    [TestMethod]
    public void GetCareer_ExistingId_ReturnsCareer()
    {
        var career = _registry.GetCareer("warboss");
        Assert.IsNotNull(career);
        Assert.AreEqual("warboss", career.Id);
    }

    [TestMethod]
    public void GetCareer_UnknownId_ReturnsNull()
    {
        Assert.IsNull(_registry.GetCareer("nonexistent"));
    }

    [TestMethod]
    public void GetAllCareers_ReturnsAllLoaded()
    {
        Assert.AreEqual(1, _registry.GetAllCareers().Count);
    }

    [TestMethod]
    public void GetChoice_ExistingId_ReturnsChoice()
    {
        var choice = _registry.GetChoice("wb_root");
        Assert.IsNotNull(choice);
        Assert.AreEqual(ChoiceType.Passive, choice.Type);
    }

    [TestMethod]
    public void GetGroup_ExistingId_ReturnsGroup()
    {
        var group = _registry.GetGroup("wb_brutality");
        Assert.IsNotNull(group);
        Assert.AreEqual(1, group.Tier);
    }

    [TestMethod]
    public void GetChoicesForGroup_ReturnsGroupChoices()
    {
        var choices = _registry.GetChoicesForGroup("wb_brutality");
        Assert.AreEqual(1, choices.Count);
        Assert.AreEqual("wb_brut_key", choices[0].Id);
    }

    [TestMethod]
    public void GetChoicesForGroup_UnknownGroup_ReturnsEmpty()
    {
        Assert.AreEqual(0, _registry.GetChoicesForGroup("unknown").Count);
    }

    [TestMethod]
    public void IsEligible_HeroMatchesCulture_ReturnsTrue()
    {
        Assert.IsTrue(_registry.IsEligible("warboss", _hero));
    }

    [TestMethod]
    public void IsEligible_HeroWrongCulture_ReturnsFalse()
    {
        _hero.CultureStringId.Returns("gondor");
        Assert.IsFalse(_registry.IsEligible("warboss", _hero));
    }

    [TestMethod]
    public void IsEligible_HeroBelowMinClanTier_ReturnsFalse()
    {
        _hero.ClanTier.Returns(0);
        Assert.IsFalse(_registry.IsEligible("warboss", _hero));
    }

    [TestMethod]
    public void IsEligible_UnknownCareer_ReturnsFalse()
    {
        Assert.IsFalse(_registry.IsEligible("nonexistent", _hero));
    }

    [TestMethod]
    public void GetMaxChoicesForHero_Level5_Returns6()
    {
        Assert.AreEqual(6, _registry.GetMaxChoicesForHero(5));
    }

    [TestMethod]
    public void GetMaxChoicesForHero_Level50_CapsAtMaxPerkPoints()
    {
        Assert.AreEqual(31, _registry.GetMaxChoicesForHero(50));
    }

    [TestMethod]
    public void IsTierAvailable_Tier1_AlwaysTrue()
    {
        Assert.IsTrue(_registry.IsTierAvailable(1, 1));
    }

    [TestMethod]
    public void IsTierAvailable_Tier2_RequiresLevel10()
    {
        Assert.IsFalse(_registry.IsTierAvailable(9, 2));
        Assert.IsTrue(_registry.IsTierAvailable(10, 2));
    }

    [TestMethod]
    public void IsTierAvailable_Tier3_RequiresLevel20()
    {
        Assert.IsFalse(_registry.IsTierAvailable(19, 3));
        Assert.IsTrue(_registry.IsTierAvailable(20, 3));
    }

    [TestMethod]
    public void GetTierUnlockLevel_Tier1_Returns1()
    {
        Assert.AreEqual(1, _registry.GetTierUnlockLevel(1));
    }

    [TestMethod]
    public void GetTierUnlockLevel_Tier2_Returns10()
    {
        Assert.AreEqual(10, _registry.GetTierUnlockLevel(2));
    }

    [TestMethod]
    public void GetTierUnlockLevel_Tier3_Returns20()
    {
        Assert.AreEqual(20, _registry.GetTierUnlockLevel(3));
    }

    [TestMethod]
    public void IsTierAvailable_ConsistentWithUnlockLevel()
    {
        for (int tier = 1; tier <= 3; tier++)
        {
            var lvl = _registry.GetTierUnlockLevel(tier);
            Assert.IsFalse(_registry.IsTierAvailable(lvl - 1, tier), $"tier {tier} should be locked below its unlock level");
            Assert.IsTrue(_registry.IsTierAvailable(lvl, tier), $"tier {tier} should unlock at its unlock level");
        }
    }
}
