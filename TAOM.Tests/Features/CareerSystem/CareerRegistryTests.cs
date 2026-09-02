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

    // ── Issue #379 — unspent-points computation (single source for the badge + screen) ──

    [TestMethod]
    public void GetUnspentPoints_TakenBelowMax_ReturnsDifference()
    {
        // Level 5 → max 6; 4 taken → 2 free.
        Assert.AreEqual(2, _registry.GetUnspentPoints(5, 4));
    }

    [TestMethod]
    public void GetUnspentPoints_TakenEqualsMax_ReturnsZero()
    {
        Assert.AreEqual(0, _registry.GetUnspentPoints(5, 6));
    }

    [TestMethod]
    public void GetUnspentPoints_OverTaken_ClampsToZero()
    {
        // A save from a higher max (e.g. after a rebalance) can hold more choices than the
        // current budget — the badge must clamp, not go negative.
        Assert.AreEqual(0, _registry.GetUnspentPoints(5, 10));
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

    // ── GetEligibleSwitchTargets ──
    //
    // Used by the "I wish to discuss my career path" dialogue + the career-switch screen. Must
    // return only the careers a hero could legally switch INTO -- which means eligible by culture
    // and clan tier, AND distinct from the hero's current career.

    [TestMethod]
    public void GetEligibleSwitchTargets_ExcludesCurrentCareer()
    {
        // Two Mordor careers; hero is currently in "warboss" -- target list must contain only the other.
        var skirmisher = new CareerDefinition(
            id: "skirmisher", displayName: "Skirmisher", description: "", portraitSprite: "",
            abilityTemplateId: "harry", minClanTier: 0, rootChoiceId: "sk_root",
            eligibleCultureIds: new List<string> { "mordor" },
            choiceGroupIds: new List<string>());
        _config.LoadCareers().Returns(new List<CareerDefinition> { WarbossCareer, skirmisher });
        _registry = new CareerRegistry(_config, Substitute.For<IModLogger>());

        var targets = _registry.GetEligibleSwitchTargets(currentCareerId: "warboss", hero: _hero);

        Assert.AreEqual(1, targets.Count);
        Assert.AreEqual("skirmisher", targets[0].Id);
    }

    [TestMethod]
    public void GetEligibleSwitchTargets_NoEligible_ReturnsEmpty()
    {
        // Only career in the registry is "warboss" and the hero is already in it -- no alternatives.
        var targets = _registry.GetEligibleSwitchTargets(currentCareerId: "warboss", hero: _hero);

        Assert.AreEqual(0, targets.Count);
    }

    [TestMethod]
    public void GetEligibleSwitchTargets_IneligibleByTier_Excluded()
    {
        // A career requiring clan tier 5 must not appear for a tier-2 hero.
        var hightier = new CareerDefinition(
            id: "hightier", displayName: "Lord", description: "", portraitSprite: "",
            abilityTemplateId: "h_ab", minClanTier: 5, rootChoiceId: "h_root",
            eligibleCultureIds: new List<string> { "mordor" },
            choiceGroupIds: new List<string>());
        _config.LoadCareers().Returns(new List<CareerDefinition> { WarbossCareer, hightier });
        _registry = new CareerRegistry(_config, Substitute.For<IModLogger>());

        // Hero is currently "warboss" (excluded) and tier 2 < 5 (excludes hightier).
        var targets = _registry.GetEligibleSwitchTargets(currentCareerId: "warboss", hero: _hero);

        Assert.AreEqual(0, targets.Count);
    }

    [TestMethod]
    public void GetEligibleSwitchTargets_IneligibleByCulture_Excluded()
    {
        // A career eligible only for Gondor must not appear for a Mordor hero.
        var gondorCareer = new CareerDefinition(
            id: "knight", displayName: "Knight", description: "", portraitSprite: "",
            abilityTemplateId: "charge", minClanTier: 0, rootChoiceId: "k_root",
            eligibleCultureIds: new List<string> { "gondor" },
            choiceGroupIds: new List<string>());
        _config.LoadCareers().Returns(new List<CareerDefinition> { WarbossCareer, gondorCareer });
        _registry = new CareerRegistry(_config, Substitute.For<IModLogger>());

        var targets = _registry.GetEligibleSwitchTargets(currentCareerId: "warboss", hero: _hero);

        Assert.AreEqual(0, targets.Count);
    }

    [TestMethod]
    public void GetEligibleSwitchTargets_NullHero_ReturnsEmpty()
    {
        var targets = _registry.GetEligibleSwitchTargets(currentCareerId: "warboss", hero: null);

        Assert.AreEqual(0, targets.Count);
    }

    [TestMethod]
    public void GetEligibleSwitchTargets_NullCurrentCareerId_ReturnsAllEligible()
    {
        // Hero in valid state but with no current career (corrupted save, freshly-cleared career,
        // or static-screen entry point that bypasses the dialogue gate). The picker should still
        // show every eligible career rather than silently failing.
        var skirmisher = new CareerDefinition(
            id: "skirmisher", displayName: "Skirmisher", description: "", portraitSprite: "",
            abilityTemplateId: "harry", minClanTier: 0, rootChoiceId: "sk_root",
            eligibleCultureIds: new List<string> { "mordor" },
            choiceGroupIds: new List<string>());
        _config.LoadCareers().Returns(new List<CareerDefinition> { WarbossCareer, skirmisher });
        _registry = new CareerRegistry(_config, Substitute.For<IModLogger>());

        var targets = _registry.GetEligibleSwitchTargets(currentCareerId: null, hero: _hero);

        Assert.AreEqual(2, targets.Count);
    }
    // -- GetOwningCareerId: the choice -> career resolution the repair pass deletes on --

    [TestMethod]
    public void GetOwningCareerId_RootChoiceWithEmptyGroupId_ResolvesToItsCareer()
    {
        // Every root choice in taom_career_choices.xml carries group_id="", so the group index
        // cannot see them. If this returns null, a ghost root from another career reads as
        // "owner unknown", survives the repair pass, and the career-point bug is NOT fixed --
        // which is exactly what a stubbed registry hid during review.
        Assert.AreEqual("warboss", _registry.GetOwningCareerId("wb_root"));
    }

    [TestMethod]
    public void GetOwningCareerId_ChoiceInACareersGroup_ResolvesToThatCareer()
    {
        Assert.AreEqual("warboss", _registry.GetOwningCareerId("wb_brut_key"));
    }

    [TestMethod]
    public void GetOwningCareerId_UnknownChoice_ReturnsNull()
    {
        Assert.IsNull(_registry.GetOwningCareerId("not_a_real_choice"));
    }

    [TestMethod]
    public void GetOwningCareerId_NullOrEmpty_ReturnsNull()
    {
        Assert.IsNull(_registry.GetOwningCareerId(null));
        Assert.IsNull(_registry.GetOwningCareerId(""));
    }

    [TestMethod]
    public void GetOwningCareerId_ChoicesFileFailedToLoad_StillResolvesRootsAndNothingElse()
    {
        // CareerConfigProvider loads taom_careers.xml and taom_career_choices.xml under separate
        // try/catch blocks, so careers can resolve while every group and choice is empty. Roots
        // must still resolve (so a ghost is cleanable) and everything else must read as unknown
        // (so the player's real tree is never deleted).
        var config = Substitute.For<ICareerConfigProvider>();
        config.GetMaxPerkPoints().Returns(30);
        config.LoadCareers().Returns(new List<CareerDefinition> { WarbossCareer });
        config.LoadChoiceGroups().Returns(new List<CareerChoiceGroupDefinition>());
        config.LoadChoices().Returns(new List<CareerChoiceDefinition>());
        var registry = new CareerRegistry(config, Substitute.For<IModLogger>());

        Assert.AreEqual("warboss", registry.GetOwningCareerId("wb_root"));
        Assert.IsNull(registry.GetOwningCareerId("wb_brut_key"));
    }
}
