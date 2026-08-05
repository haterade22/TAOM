using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
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
    private ICareerConfigProvider _configProvider;
    private IModLogger _logger;
    private bool _closeCalled;

    private static readonly CareerDefinition WarbossCareer = new CareerDefinition(
        id: "warboss", displayName: "Warboss", description: "A brute.",
        portraitSprite: "wb_sprite", abilityTemplateId: "rally_horde",
        minClanTier: 0,
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
        _configProvider = Substitute.For<ICareerConfigProvider>();
        _logger = Substitute.For<IModLogger>();
        _closeCalled = false;

        _registry.GetCareer("warboss").Returns(WarbossCareer);
        _registry.GetGroup("wb_brutality").Returns(BrutalityGroup);
        _registry.GetChoice("wb_brut_key").Returns(KeystoneChoice);
        _registry.GetChoice("wb_brut_p1").Returns(PassiveChoice);
        _registry.GetChoicesForGroup("wb_brutality").Returns(new List<CareerChoiceDefinition> { KeystoneChoice, PassiveChoice });
        _registry.GetMaxChoicesForHero(5).Returns(6);
        // Issue #379 — the VM reads unspent points through the registry's single-source
        // helper; mirror the real formula so per-test GetMaxChoicesForHero overrides flow
        // through without each test re-stubbing the derived method.
        _registry.GetUnspentPoints(Arg.Any<int>(), Arg.Any<int>())
            .Returns(ci => System.Math.Max(0, _registry.GetMaxChoicesForHero(ci.ArgAt<int>(0)) - ci.ArgAt<int>(1)));
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

    // ── Preventive: Tier Gating (root cause: missing integration test for composed selection flow) ──

    [TestMethod]
    public void ExecuteSelectChoice_Tier2ChoiceAtLevel5_Rejected()
    {
        // Tier 2 requires level 10. Hero is level 5 → IsTierAvailable(5, 2) returns false.
        SetupHeroWithCareer();

        var tier2Group = new CareerChoiceGroupDefinition(
            id: "wb_scavenger", careerId: "warboss", tier: 2,
            choiceIds: new List<string> { "wb_scav_key" });
        var tier2Keystone = new CareerChoiceDefinition(
            id: "wb_scav_key", groupId: "wb_scavenger", type: ChoiceType.Keystone,
            description: "Tier 2 keystone", iconSprite: "icon", passive: null, mutations: null);

        _registry.GetGroup("wb_scavenger").Returns(tier2Group);
        _registry.GetChoice("wb_scav_key").Returns(tier2Keystone);
        _registry.GetMaxChoicesForHero(5).Returns(10);

        var vm = CreateVM();
        vm.ExecuteSelectChoice("wb_scav_key");

        Assert.IsFalse(_dataService.GetOrCreateData("hero1").HasChoice("wb_scav_key"),
            "Tier 2 choice should be rejected when hero level is below threshold");
    }

    [TestMethod]
    public void ExecuteSelectChoice_SecondKeystoneInSameTier_Rejected()
    {
        // Set up career with 2 groups in tier 1, each with a keystone.
        // Selecting the first keystone should succeed, selecting the second should be rejected.
        SetupHeroWithCareer();

        var dominionGroup = new CareerChoiceGroupDefinition(
            id: "wb_dominion", careerId: "warboss", tier: 1,
            choiceIds: new List<string> { "wb_dom_key" });
        var dominionKeystone = new CareerChoiceDefinition(
            id: "wb_dom_key", groupId: "wb_dominion", type: ChoiceType.Keystone,
            description: "Dominion keystone", iconSprite: "icon", passive: null, mutations: null);

        // Expand career to have both groups
        var expandedCareer = new CareerDefinition(
            id: "warboss", displayName: "Warboss", description: "A brute.",
            portraitSprite: "wb_sprite", abilityTemplateId: "rally_horde",
            minClanTier: 0,
            rootChoiceId: "wb_root",
            eligibleCultureIds: new List<string> { "mordor" },
            choiceGroupIds: new List<string> { "wb_brutality", "wb_dominion" });
        _registry.GetCareer("warboss").Returns(expandedCareer);

        _registry.GetGroup("wb_dominion").Returns(dominionGroup);
        _registry.GetChoice("wb_dom_key").Returns(dominionKeystone);
        _registry.GetChoicesForGroup("wb_dominion").Returns(new List<CareerChoiceDefinition> { dominionKeystone });
        _registry.GetMaxChoicesForHero(5).Returns(10);

        var vm = CreateVM();

        // First keystone succeeds
        vm.ExecuteSelectChoice("wb_brut_key");
        Assert.IsTrue(_dataService.GetOrCreateData("hero1").HasChoice("wb_brut_key"),
            "First keystone in tier should be accepted");

        // Second keystone in same tier rejected
        vm.ExecuteSelectChoice("wb_dom_key");
        Assert.IsFalse(_dataService.GetOrCreateData("hero1").HasChoice("wb_dom_key"),
            "Second keystone in same tier should be rejected (mutual exclusion)");
    }

    [TestMethod]
    public void ExecuteSelectChoice_PassiveInSameTierAsExistingKeystone_Allowed()
    {
        // Passives should still be selectable even after a keystone in the same tier
        SetupHeroWithCareer();
        _registry.GetMaxChoicesForHero(5).Returns(10);
        _dataService.TryAddChoice("hero1", "wb_brut_key", 10);

        var vm = CreateVM();
        vm.ExecuteSelectChoice("wb_brut_p1");

        Assert.IsTrue(_dataService.GetOrCreateData("hero1").HasChoice("wb_brut_p1"),
            "Passive choices in same tier should still be selectable");
    }

    // ── Preventive: Serialization Safety (root cause: vanilla API not researched) ──
    // These tests are in CareerPersistenceTests below.

    // ── Switch mode (career-switch picker from "I wish to discuss my career path" dialogue) ──

    [TestMethod]
    public void SwitchMode_PopulatesEligibleSwitchTargets()
    {
        // Two eligible alternative careers; the picker must list both.
        var ranger = new CareerDefinition(
            id: "ranger", displayName: "Ranger", description: "scout",
            portraitSprite: "r_sprite", abilityTemplateId: "ambush",
            minClanTier: 0, rootChoiceId: "ranger_root",
            eligibleCultureIds: new List<string> { "mordor" },
            choiceGroupIds: new List<string>());
        var skirmisher = new CareerDefinition(
            id: "skirmisher", displayName: "Skirmisher", description: "flank",
            portraitSprite: "sk_sprite", abilityTemplateId: "harry",
            minClanTier: 0, rootChoiceId: "sk_root",
            eligibleCultureIds: new List<string> { "mordor" },
            choiceGroupIds: new List<string>());
        var hero = Substitute.For<ICareerHeroAdapter>();
        _registry.GetEligibleSwitchTargets("warboss", hero)
            .Returns(new List<CareerDefinition> { ranger, skirmisher });
        SetupHeroWithCareer();

        var vm = CreateSwitchModeVM(hero, _ => { });

        Assert.AreEqual(2, vm.EligibleSwitchTargets.Count);
        Assert.AreEqual("ranger", vm.EligibleSwitchTargets[0].CareerId);
        Assert.AreEqual("skirmisher", vm.EligibleSwitchTargets[1].CareerId);
    }

    [TestMethod]
    public void SwitchMode_DoesNotLoadChoiceGroups()
    {
        // Switch mode must NOT also render the normal-mode tier panels.
        SetupHeroWithCareer();
        var hero = Substitute.For<ICareerHeroAdapter>();
        _registry.GetEligibleSwitchTargets("warboss", hero).Returns(new List<CareerDefinition>());

        var vm = CreateSwitchModeVM(hero, _ => { });

        Assert.AreEqual(0, vm.ChoiceGroupsTier1.Count);
        Assert.AreEqual(0, vm.ChoiceGroupsTier2.Count);
        Assert.AreEqual(0, vm.ChoiceGroupsTier3.Count);
    }

    [TestMethod]
    public void SwitchMode_IsSwitchModeTrue_IsNormalModeFalse()
    {
        SetupHeroWithCareer();
        var hero = Substitute.For<ICareerHeroAdapter>();
        _registry.GetEligibleSwitchTargets("warboss", hero).Returns(new List<CareerDefinition>());

        var vm = CreateSwitchModeVM(hero, _ => { });

        Assert.IsTrue(vm.IsSwitchMode);
        Assert.IsFalse(vm.IsNormalMode);
    }

    [TestMethod]
    public void SwitchMode_NoEligibleTargets_IsBrowsingTargetsFalse()
    {
        // Empty target list -- the picker panel should hide via @IsBrowsingTargets.
        SetupHeroWithCareer();
        var hero = Substitute.For<ICareerHeroAdapter>();
        _registry.GetEligibleSwitchTargets("warboss", hero).Returns(new List<CareerDefinition>());

        var vm = CreateSwitchModeVM(hero, _ => { });

        Assert.IsFalse(vm.IsBrowsingTargets);
    }

    [TestMethod]
    public void SwitchMode_TargetVMExecuteChoose_InvokesSwitchCallback()
    {
        // Selecting a target must invoke the screen's switch callback with the target career id.
        var ranger = new CareerDefinition(
            id: "ranger", displayName: "Ranger", description: "",
            portraitSprite: "", abilityTemplateId: "ambush",
            minClanTier: 0, rootChoiceId: "ranger_root",
            eligibleCultureIds: new List<string> { "mordor" },
            choiceGroupIds: new List<string>());
        var hero = Substitute.For<ICareerHeroAdapter>();
        _registry.GetEligibleSwitchTargets("warboss", hero).Returns(new List<CareerDefinition> { ranger });
        SetupHeroWithCareer();

        string chosenCareerId = null;
        var vm = CreateSwitchModeVM(hero, id => chosenCareerId = id);

        vm.EligibleSwitchTargets[0].ExecuteChoose();

        Assert.AreEqual("ranger", chosenCareerId);
    }

    // ── Collapse on level-up: taking or refunding a choice refreshes the screen, which rebuilds
    // every group VM. Fresh VMs start with ButtonsVisible=false and Gauntlet fires no new
    // HoverBegin for a widget recreated under a stationary cursor, so the +/- strip vanished on
    // every click. Rebuilds must carry the open state over to the replacement VMs. ──

    [TestMethod]
    public void ClickIncrease_HoveredGroup_StaysOpenAfterRebuild()
    {
        SetupHeroWithCareer();
        _registry.GetMaxChoicesForHero(5).Returns(10);
        var vm = CreateVM();
        var group = vm.ChoiceGroupsTier1[0];
        group.ExecuteBeginHover();

        group.ExecuteClickIncrease();

        Assert.IsTrue(_dataService.GetOrCreateData("hero1").HasChoice("wb_brut_key"),
            "sanity: the click should take the first untaken choice");
        Assert.IsTrue(vm.ChoiceGroupsTier1[0].ButtonsVisible,
            "the rebuilt group VM under the cursor should stay open");
    }

    [TestMethod]
    public void ClickDecrease_HoveredGroup_StaysOpenAfterRebuild()
    {
        SetupHeroWithCareer();
        _registry.GetMaxChoicesForHero(5).Returns(10);
        _dataService.TryAddChoice("hero1", "wb_brut_key", 10);
        var vm = CreateVM();
        var group = vm.ChoiceGroupsTier1[0];
        group.ExecuteBeginHover();

        group.ExecuteClickDecrease();

        Assert.IsFalse(_dataService.GetOrCreateData("hero1").HasChoice("wb_brut_key"),
            "sanity: the click should refund the last taken choice");
        Assert.IsTrue(vm.ChoiceGroupsTier1[0].ButtonsVisible,
            "the rebuilt group VM under the cursor should stay open");
    }

    [TestMethod]
    public void RefreshValues_NoGroupHovered_GroupsStayClosed()
    {
        SetupHeroWithCareer();
        var vm = CreateVM();

        vm.RefreshValues();

        Assert.IsFalse(vm.ChoiceGroupsTier1[0].ButtonsVisible,
            "carry-over must not open groups that were closed before the rebuild");
    }

    [TestMethod]
    public void ClickIncrease_RebuildsGroupsOnce_NotTwice()
    {
        // Keystone already taken so the click lands on the passive choice -- keystone
        // selection also scans GetChoicesForGroup for the mutual-exclusion check, which
        // would muddy the rebuild count this test pins down.
        SetupHeroWithCareer();
        _registry.GetMaxChoicesForHero(5).Returns(10);
        _dataService.TryAddChoice("hero1", "wb_brut_key", 10);
        var vm = CreateVM();
        _registry.ClearReceivedCalls();

        vm.ChoiceGroupsTier1[0].ExecuteClickIncrease();

        // One successful selection = one RefreshValues = one rebuild. The group VM's
        // choiceChangedAction fired a second, redundant rebuild after TrySelectChoice
        // had already refreshed the screen.
        _registry.Received(1).GetChoicesForGroup("wb_brutality");
    }

    [TestMethod]
    public void NormalMode_IsSwitchModeFalse_IsNormalModeTrue()
    {
        // Confirm the default (existing) ctor path still produces normal mode.
        SetupHeroWithCareer();

        var vm = CreateVM();

        Assert.IsFalse(vm.IsSwitchMode);
        Assert.IsTrue(vm.IsNormalMode);
    }

    private void SetupHeroWithCareer()
    {
        _dataService.SetCareer("hero1", "warboss");
    }

    private CareerScreenVM CreateVM()
    {
        return new CareerScreenVM(_dataService, _registry, _passiveService, _configProvider, _logger, "hero1", 5, () => _closeCalled = true);
    }

    private CareerScreenVM CreateSwitchModeVM(ICareerHeroAdapter hero, Action<string> onChoose)
    {
        return new CareerScreenVM(
            _dataService, _registry, _passiveService, _configProvider, _logger,
            "hero1", 5,
            () => _closeCalled = true,
            questService: null,
            isSwitchMode: true,
            heroAdapter: hero,
            onChooseSwitchTarget: onChoose);
    }
}
