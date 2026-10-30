using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CareerSystem.UI;

namespace TAOM.Tests.Features.CareerSystem;

[TestClass]
[TestCategory("RequiresGame")]
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
        description: "Keystone", iconSprite: "icon", passives: null, mutations: null);

    private static readonly CareerChoiceDefinition PassiveChoice = new CareerChoiceDefinition(
        id: "wb_brut_p1", groupId: "wb_brutality", type: ChoiceType.Passive,
        description: "Passive", iconSprite: "icon",
        passives: new[] { new PassiveEffect(PassiveEffectType.Damage, 0.1f) },
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
        StubMaxChoices(6);
        // Issue #379 — the VM reads unspent points through the registry's single-source
        // helper; mirror the real formula so per-test GetMaxChoicesForHero overrides flow
        // through without each test re-stubbing the derived method.
        _registry.GetUnspentPoints(Arg.Any<CareerDefinition>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(ci => System.Math.Max(0,
                _registry.GetMaxChoicesForHero(ci.ArgAt<CareerDefinition>(0), ci.ArgAt<int>(1)) - ci.ArgAt<int>(2)));
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
    public void ToggleChoice_ValidChoice_AddsAndRefreshes()
    {
        SetupHeroWithCareer();
        StubMaxChoices(10);
        var vm = CreateVM();

        Take(vm, "wb_brut_key");

        Assert.IsTrue(_dataService.GetOrCreateData("hero1").HasChoice("wb_brut_key"));
        _passiveService.Received().RefreshCache(_dataService, _registry);
    }

    // ── Preventive: Tier Gating (root cause: missing integration test for composed selection flow) ──

    [TestMethod]
    public void ToggleChoice_Tier2ChoiceAtLevel5_Rejected()
    {
        // Tier 2 requires level 10. Hero is level 5 → IsTierAvailable(5, 2) returns false.
        SetupHeroWithCareer();
        AddGroup("wb_scavenger", tier: 2, maxPicks: 0, Passive("wb_scav_p1", "wb_scavenger"));
        StubMaxChoices(10);

        var vm = CreateVM();
        Take(vm, "wb_scav_p1");

        Assert.IsFalse(_dataService.GetOrCreateData("hero1").HasChoice("wb_scav_p1"),
            "Tier 2 choice should be rejected when hero level is below threshold");
    }

    [TestMethod]
    public void LockReasonText_TierLockedChoice_ShowsTheRequiredLevel()
    {
        SetupHeroWithCareer();
        AddGroup("wb_scavenger", tier: 2, maxPicks: 0, Passive("wb_scav_p1", "wb_scavenger"));
        _registry.GetTierUnlockLevel(2).Returns(10);
        StubMaxChoices(10);

        var vm = CreateVM();

        var choice = FindChoiceVM(vm, "wb_scav_p1");
        Assert.IsTrue(choice.HasLockReason);
        Assert.AreEqual("Requires Level 10", choice.LockReasonText);
        Assert.IsFalse(choice.IsForkLocked);
    }

    [TestMethod]
    public void ToggleChoice_SecondKeystoneInSameTier_Rejected()
    {
        // Set up career with 2 groups in tier 1, each with a keystone.
        // Selecting the first keystone should succeed, selecting the second should be rejected.
        SetupHeroWithCareer();

        var dominionGroup = new CareerChoiceGroupDefinition(
            id: "wb_dominion", careerId: "warboss", tier: 1,
            choiceIds: new List<string> { "wb_dom_key" });
        var dominionKeystone = new CareerChoiceDefinition(
            id: "wb_dom_key", groupId: "wb_dominion", type: ChoiceType.Keystone,
            description: "Dominion keystone", iconSprite: "icon", passives: null, mutations: null);

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
        StubMaxChoices(10);

        var vm = CreateVM();

        // First keystone succeeds
        Take(vm, "wb_brut_key");
        Assert.IsTrue(_dataService.GetOrCreateData("hero1").HasChoice("wb_brut_key"),
            "First keystone in tier should be accepted");

        // Second keystone in same tier rejected
        Take(vm, "wb_dom_key");
        Assert.IsFalse(_dataService.GetOrCreateData("hero1").HasChoice("wb_dom_key"),
            "Second keystone in same tier should be rejected (mutual exclusion)");
        Assert.AreEqual("You took another keystone in this tier.", FindChoiceVM(vm, "wb_dom_key").LockReasonText);
    }

    [TestMethod]
    public void ToggleChoice_PassiveInSameTierAsExistingKeystone_Allowed()
    {
        // Passives should still be selectable even after a keystone in the same tier
        SetupHeroWithCareer();
        StubMaxChoices(10);
        _dataService.TryAddChoice("hero1", "wb_brut_key", 10);

        var vm = CreateVM();
        Take(vm, "wb_brut_p1");

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

    [TestMethod]
    public void ToggleChoice_RebuildsGroupsOnce_NotTwice()
    {
        // Keystone already taken so the click lands on the passive choice -- keystone
        // selection also scans GetChoicesForGroup for the mutual-exclusion check, which
        // would muddy the rebuild count this test pins down.
        SetupHeroWithCareer();
        StubMaxChoices(10);
        _dataService.TryAddChoice("hero1", "wb_brut_key", 10);
        var vm = CreateVM();
        _registry.ClearReceivedCalls();

        FindChoiceVM(vm, "wb_brut_p1").ExecuteToggleChoice();

        // One successful selection = one RefreshValues = one rebuild. The count is the same one
        // the old group-level click asserted: CanTake runs no registry scan for a passive, and the
        // rebuild's per-choice rule (CanTake or CanRefund) reuses the group's single
        // GetChoicesForGroup walk.
        //
        // Still exactly one call after #388: the Active Effects panel is accumulated from
        // THIS walk rather than a second pass of its own, precisely so this guard keeps
        // meaning "one rebuild".
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

    // ── Issue #388 — Active Effects panel (right-hand summary on the diamond screen) ──
    // Keystone lines are the taken keystones' descriptions; passive lines are the passives of the
    // root and of every taken choice, summed per effect type and troop class, so two +5% damage
    // picks read as one "+10% damage".

    [TestMethod]
    public void ActiveEffects_NoChoicesTakenAndNoRoot_BothListsEmpty()
    {
        SetupHeroWithCareer();
        var vm = CreateVM();

        Assert.AreEqual(0, vm.KeystoneEffectLines.Count);
        Assert.AreEqual(0, vm.PassiveEffectLines.Count);
    }

    [TestMethod]
    public void ActiveEffects_TakenPassive_ListsFormattedTotal()
    {
        SetupHeroWithCareer();
        StubMaxChoices(10);
        var vm = CreateVM();

        Take(vm, "wb_brut_p1"); // Damage 0.1

        Assert.AreEqual(1, vm.PassiveEffectLines.Count);
        Assert.AreEqual("+10% damage", vm.PassiveEffectLines[0].LineText);
    }

    [TestMethod]
    public void ActiveEffects_TakenKeystone_ListsItsDescription()
    {
        SetupHeroWithCareer();
        StubMaxChoices(10);
        var vm = CreateVM();

        Take(vm, "wb_brut_key");

        Assert.AreEqual(1, vm.KeystoneEffectLines.Count);
        Assert.AreEqual("Keystone", vm.KeystoneEffectLines[0].LineText);
    }

    [TestMethod]
    public void ActiveEffects_SameEffectTakenTwice_SumsIntoOneLine()
    {
        // Two separate +10% Damage passives must read as a single "+20% damage", not two lines.
        var second = new CareerChoiceDefinition(
            id: "wb_brut_p2", groupId: "wb_brutality", type: ChoiceType.Passive,
            description: "Passive2", iconSprite: "icon",
            passives: new[] { new PassiveEffect(PassiveEffectType.Damage, 0.1f) }, mutations: null);
        _registry.GetChoice("wb_brut_p2").Returns(second);
        _registry.GetChoicesForGroup("wb_brutality").Returns(
            new List<CareerChoiceDefinition> { KeystoneChoice, PassiveChoice, second });

        SetupHeroWithCareer();
        StubMaxChoices(10);
        var vm = CreateVM();

        Take(vm, "wb_brut_p1");
        Take(vm, "wb_brut_p2");

        Assert.AreEqual(1, vm.PassiveEffectLines.Count);
        Assert.AreEqual("+20% damage", vm.PassiveEffectLines[0].LineText);
    }

    private void GivenExtraChoices(params CareerChoiceDefinition[] extra)
    {
        foreach (var choice in extra)
            _registry.GetChoice(choice.Id).Returns(choice);
        var all = new List<CareerChoiceDefinition> { KeystoneChoice, PassiveChoice };
        all.AddRange(extra);
        _registry.GetChoicesForGroup("wb_brutality").Returns(all);
    }

    private static CareerChoiceDefinition Passives(string id, params PassiveEffect[] passives) => new CareerChoiceDefinition(
        id: id, groupId: "wb_brutality", type: ChoiceType.Passive, description: id, iconSprite: "icon",
        passives: passives, mutations: null);

    [TestMethod]
    public void ActiveEffects_ChoiceWithTwoPassives_ListsEachEffect()
    {
        // #769 E8: both effects of one option read in the panel, not just the first.
        GivenExtraChoices(Passives("wb_two",
            new PassiveEffect(PassiveEffectType.Health, 25f),
            new PassiveEffect(PassiveEffectType.Resistance, 0.05f)));
        SetupHeroWithCareer();
        StubMaxChoices(10);
        var vm = CreateVM();

        Take(vm, "wb_two");

        CollectionAssert.AreEquivalent(
            new[] { "+25 max health", "+5% resistance" },
            vm.PassiveEffectLines.Select(l => l.LineText).ToList());
    }

    [TestMethod]
    public void ActiveEffects_ClassFilteredAndUnfilteredOfOneType_StayOnSeparateLines()
    {
        // "+10% troop damage for infantry" and an All-class "+5% troop damage" are different promises: one
        // summed line would tell the player the whole army gets +15%.
        GivenExtraChoices(
            Passives("wb_inf", new PassiveEffect(PassiveEffectType.TroopDamage, 0.10f, troopClass: TroopClassMask.Infantry)),
            Passives("wb_all", new PassiveEffect(PassiveEffectType.TroopDamage, 0.05f)));
        SetupHeroWithCareer();
        StubMaxChoices(10);
        var vm = CreateVM();

        Take(vm, "wb_inf");
        Take(vm, "wb_all");

        CollectionAssert.AreEquivalent(
            new[] { "+10% troop damage for infantry", "+5% troop damage" },
            vm.PassiveEffectLines.Select(l => l.LineText).ToList());
    }

    [TestMethod]
    public void ActiveEffects_TwoPicksOfTheSameClassAndType_SumIntoOneLine()
    {
        GivenExtraChoices(
            Passives("wb_inf1", new PassiveEffect(PassiveEffectType.TroopDamage, 0.10f, troopClass: TroopClassMask.Infantry)),
            Passives("wb_inf2", new PassiveEffect(PassiveEffectType.TroopDamage, 0.05f, troopClass: TroopClassMask.Infantry)));
        SetupHeroWithCareer();
        StubMaxChoices(10);
        var vm = CreateVM();

        Take(vm, "wb_inf1");
        Take(vm, "wb_inf2");

        Assert.AreEqual(1, vm.PassiveEffectLines.Count);
        Assert.AreEqual("+15% troop damage for infantry", vm.PassiveEffectLines[0].LineText);
    }

    [TestMethod]
    public void ToggleChoice_UntakenThenTaken_SelectsThenRefunds()
    {
        // #388 — the diamond is the whole click target, so one command serves both
        // directions. Losing this would strand every taken choice as unrefundable.
        SetupHeroWithCareer();
        StubMaxChoices(10);
        var vm = CreateVM();
        var choice = vm.ChoiceGroupsTier1[0].Choices.First(c => c.ChoiceId == "wb_brut_p1");

        choice.ExecuteToggleChoice();
        Assert.AreEqual(1, vm.PassiveEffectLines.Count, "first click takes the choice");

        var taken = vm.ChoiceGroupsTier1[0].Choices.First(c => c.ChoiceId == "wb_brut_p1");
        taken.ExecuteToggleChoice();
        Assert.AreEqual(0, vm.PassiveEffectLines.Count, "second click refunds it");
    }

    [TestMethod]
    public void ActiveEffects_Deselect_RemovesTheLine()
    {
        SetupHeroWithCareer();
        StubMaxChoices(10);
        var vm = CreateVM();
        Take(vm, "wb_brut_p1");

        // The real user path: the choice VM's "−" callback into TryDeselectChoice.
        var taken = vm.ChoiceGroupsTier1[0].Choices.First(c => c.ChoiceId == "wb_brut_p1");
        taken.DeSelectChoice();

        Assert.AreEqual(0, vm.PassiveEffectLines.Count);
    }

    // -- #766 click path: the REAL parser feeds the REAL registry, and the click goes through
    // CareerChoiceObjectVM.ExecuteToggleChoice. Hand-built definitions (everything above) carry a
    // GroupId the parser never stamped, so they could not see the tier gate and the keystone rule
    // going inert. --

    private const string ClickPathCareersXml = @"<?xml version='1.0'?>
<Careers max_perk_points=""30"">
  <Career id=""tester"" display_name=""Tester"" description="""" portrait_sprite="""" ability_template_id=""t_ability""
          min_clan_tier=""0"" root_choice_id=""t_root"">
    <EligibleCultures><Culture id=""mordor"" /></EligibleCultures>
    <ChoiceGroups>
      <Group id=""t_g1"" /><Group id=""t_g2"" /><Group id=""t_g3"" /><Group id=""t_g4"" />
    </ChoiceGroups>
  </Career>
</Careers>";

    private const string ClickPathChoicesXml = @"<?xml version='1.0'?>
<CareerChoices>
  <Choice id=""t_root"" type=""Passive"" description=""r"" icon_sprite=""i"" />
  <ChoiceGroup id=""t_g1"" career_id=""tester"" tier=""1"">
    <Choice id=""t_g1_key"" type=""Keystone"" description=""k1"" icon_sprite=""i"" />
    <Choice id=""t_g1_p1"" type=""Passive"" description=""p1"" icon_sprite=""i"">
      <PassiveEffect type=""Damage"" magnitude=""0.1"" />
    </Choice>
  </ChoiceGroup>
  <ChoiceGroup id=""t_g2"" career_id=""tester"" tier=""1"">
    <Choice id=""t_g2_key"" type=""Keystone"" description=""k2"" icon_sprite=""i"" />
  </ChoiceGroup>
  <ChoiceGroup id=""t_g4"" career_id=""tester"" tier=""1"" max_picks=""1"">
    <Choice id=""t_g4_a"" type=""Passive"" description=""f1"" icon_sprite=""i"">
      <PassiveEffect type=""Damage"" magnitude=""0.1"" />
    </Choice>
    <Choice id=""t_g4_b"" type=""Passive"" description=""f2"" icon_sprite=""i"">
      <PassiveEffect type=""Damage"" magnitude=""0.1"" />
    </Choice>
  </ChoiceGroup>
  <ChoiceGroup id=""t_g3"" career_id=""tester"" tier=""3"">
    <Choice id=""t_g3_p1"" type=""Passive"" description=""p3"" icon_sprite=""i"">
      <PassiveEffect type=""Damage"" magnitude=""0.1"" />
    </Choice>
    <Choice id=""t_g3_p2"" type=""Passive"" description=""p3b"" icon_sprite=""i"">
      <PassiveEffect type=""Damage"" magnitude=""0.1"" />
    </Choice>
  </ChoiceGroup>
</CareerChoices>";

    private (CareerScreenVM Vm, CareerRegistry Registry, string Dir) CreateParsedVM(int heroLevel)
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "taom_test_" + System.IO.Path.GetRandomFileName());
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dir, "career_system"));
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "career_system", "taom_careers.xml"), ClickPathCareersXml);
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "career_system", "taom_career_choices.xml"), ClickPathChoicesXml);

        var pathService = Substitute.For<TAOM.Core.Infrastructure.IPathService>();
        pathService.ModuleDataPath.Returns(dir);
        var provider = new CareerConfigProvider(pathService, _logger);
        var registry = new CareerRegistry(provider, _logger);

        _dataService.SetCareer("hero1", "tester");
        var vm = new CareerScreenVM(_dataService, registry, _passiveService, provider, _logger,
            "hero1", heroLevel, () => _closeCalled = true);
        return (vm, registry, dir);
    }

    private static CareerChoiceObjectVM FindChoiceVM(CareerScreenVM vm, string choiceId)
        => vm.ChoiceGroupsTier1.Concat(vm.ChoiceGroupsTier2).Concat(vm.ChoiceGroupsTier3)
            .SelectMany(g => g.Choices).First(c => c.ChoiceId == choiceId);

    [TestMethod]
    public void ExecuteToggleChoice_ParsedTier3ChoiceAtLevel1_IsRefused()
    {
        var (vm, registry, dir) = CreateParsedVM(heroLevel: 1);
        try
        {
            Assert.AreEqual("t_g3", registry.GetChoice("t_g3_p1").GroupId);
            Assert.IsTrue(vm.FreeCareerPoints > 0, "points must not be the refuser");

            FindChoiceVM(vm, "t_g3_p1").ExecuteToggleChoice();

            Assert.IsFalse(_dataService.GetOrCreateData("hero1").HasChoice("t_g3_p1"), "tier 3 is locked at level 1");
            Assert.IsFalse(FindChoiceVM(vm, "t_g3_p1").IsTaken);

            // Positive control: a tier-1 passive in the same VM is accepted.
            FindChoiceVM(vm, "t_g1_p1").ExecuteToggleChoice();
            Assert.IsTrue(_dataService.GetOrCreateData("hero1").HasChoice("t_g1_p1"));
        }
        finally
        {
            System.IO.Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void ExecuteToggleChoice_ParsedSecondKeystoneInSameTier_IsRefused()
    {
        var (vm, registry, dir) = CreateParsedVM(heroLevel: 5);
        try
        {
            Assert.AreEqual("t_g1", registry.GetChoice("t_g1_key").GroupId);
            Assert.AreEqual("t_g2", registry.GetChoice("t_g2_key").GroupId);

            FindChoiceVM(vm, "t_g1_key").ExecuteToggleChoice();
            Assert.IsTrue(_dataService.GetOrCreateData("hero1").HasChoice("t_g1_key"), "the first keystone is taken");
            Assert.IsTrue(vm.FreeCareerPoints > 0, "points must not be the refuser");

            // RefreshValues rebuilt every choice VM, so fetch the second one fresh.
            FindChoiceVM(vm, "t_g2_key").ExecuteToggleChoice();

            Assert.IsFalse(_dataService.GetOrCreateData("hero1").HasChoice("t_g2_key"), "one keystone per tier");
            Assert.IsFalse(FindChoiceVM(vm, "t_g2_key").IsTaken);
        }
        finally
        {
            System.IO.Directory.Delete(dir, true);
        }
    }

    // ── #769 E4/E5: forks, lock on leave, lock reasons, per-career pacing ──

    [TestMethod]
    public void ToggleChoice_ForkGroupWithOnePick_RefusesTheSiblingWithTheForkText()
    {
        SetupHeroWithCareer();
        AddGroup("wb_fork", tier: 1, maxPicks: 1, Passive("wb_fork_a", "wb_fork"), Passive("wb_fork_b", "wb_fork"));
        StubMaxChoices(10);
        var vm = CreateVM();

        Take(vm, "wb_fork_a");
        Take(vm, "wb_fork_b");

        var data = _dataService.GetOrCreateData("hero1");
        Assert.IsTrue(data.HasChoice("wb_fork_a"));
        Assert.IsFalse(data.HasChoice("wb_fork_b"), "the fork holds one pick");
        Assert.IsTrue(vm.FreeCareerPoints > 0, "points must not be the refuser");
        var sibling = FindChoiceVM(vm, "wb_fork_b");
        Assert.IsTrue(sibling.IsForkLocked);
        Assert.IsTrue(sibling.HasLockReason);
        Assert.AreEqual("You took another choice on this path.", sibling.LockReasonText);
    }

    [TestMethod]
    public void ToggleChoice_ForkPickRefundedInTheSameVisit_ReopensTheFork()
    {
        SetupHeroWithCareer();
        AddGroup("wb_fork", tier: 1, maxPicks: 1, Passive("wb_fork_a", "wb_fork"), Passive("wb_fork_b", "wb_fork"));
        StubMaxChoices(10);
        var vm = CreateVM();
        Take(vm, "wb_fork_a");

        Take(vm, "wb_fork_a");

        Assert.IsFalse(_dataService.GetOrCreateData("hero1").HasChoice("wb_fork_a"), "free to refund the same visit");
        Assert.IsFalse(FindChoiceVM(vm, "wb_fork_b").IsForkLocked);
        Take(vm, "wb_fork_b");
        Assert.IsTrue(_dataService.GetOrCreateData("hero1").HasChoice("wb_fork_b"), "the other path is now open");
    }

    [TestMethod]
    public void ToggleChoice_ForkPickAfterReopeningTheScreen_IsLockedIn()
    {
        SetupHeroWithCareer();
        AddGroup("wb_fork", tier: 1, maxPicks: 1, Passive("wb_fork_a", "wb_fork"), Passive("wb_fork_b", "wb_fork"));
        StubMaxChoices(10);
        Take(CreateVM(), "wb_fork_a");

        var reopened = CreateVM();
        var taken = FindChoiceVM(reopened, "wb_fork_a");
        Assert.IsTrue(taken.IsTaken);
        Assert.AreEqual("Locked in. Retrain to change this choice.", taken.LockReasonText);
        Assert.IsFalse(taken.IsForkLocked, "a taken choice is not shown as a refused fork sibling");

        taken.ExecuteToggleChoice();

        Assert.IsTrue(_dataService.GetOrCreateData("hero1").HasChoice("wb_fork_a"), "the refund is refused");
        Assert.IsTrue(FindChoiceVM(reopened, "wb_fork_a").IsTaken);
    }

    [TestMethod]
    public void ToggleChoice_UnlimitedGroupAfterReopeningTheScreen_StillRefundsFreely()
    {
        SetupHeroWithCareer();
        StubMaxChoices(10);
        Take(CreateVM(), "wb_brut_p1");

        var reopened = CreateVM();
        var taken = FindChoiceVM(reopened, "wb_brut_p1");
        Assert.IsFalse(taken.HasLockReason);

        taken.ExecuteToggleChoice();

        Assert.IsFalse(_dataService.GetOrCreateData("hero1").HasChoice("wb_brut_p1"));
    }

    [TestMethod]
    public void LockReasonText_NoFreePoints_ExplainsTheMissingPoint()
    {
        SetupHeroWithCareer();
        _dataService.TryAddChoice("hero1", "wb_root", 10);
        StubMaxChoices(1);

        var vm = CreateVM();

        Assert.AreEqual(0, vm.FreeCareerPoints);
        Assert.AreEqual("No free career points.", FindChoiceVM(vm, "wb_brut_p1").LockReasonText);
    }

    [TestMethod]
    public void LockReasonText_TakeableChoice_IsEmpty()
    {
        SetupHeroWithCareer();
        StubMaxChoices(10);

        var vm = CreateVM();

        var choice = FindChoiceVM(vm, "wb_brut_p1");
        Assert.AreEqual("", choice.LockReasonText);
        Assert.IsFalse(choice.HasLockReason);
        Assert.IsTrue(choice.IsFreeToTake);
    }

    [TestMethod]
    public void NextPointText_CareerWithAnUpcomingPointLevel_NamesIt()
    {
        SetupHeroWithCareer();
        _registry.GetNextPointLevel(Arg.Any<CareerDefinition>(), 5).Returns(10);

        var vm = CreateVM();

        Assert.IsTrue(vm.HasNextPoint);
        Assert.AreEqual("Next point at level 10", vm.NextPointText);
    }

    [TestMethod]
    public void NextPointText_NoUpcomingPointLevel_IsEmpty()
    {
        SetupHeroWithCareer();
        _registry.GetNextPointLevel(Arg.Any<CareerDefinition>(), 5).Returns((int?)null);

        var vm = CreateVM();

        Assert.IsFalse(vm.HasNextPoint);
        Assert.AreEqual("", vm.NextPointText);
    }

    // ── #769 E7: paid retrain button ──

    private void AddForkAB()
        => AddGroup("wb_fork", tier: 1, maxPicks: 1, Passive("wb_fork_a", "wb_fork"), Passive("wb_fork_b", "wb_fork"));

    private CareerScreenVM CreateRetrainVM(Action<int> onRetrainRequested)
        => new CareerScreenVM(_dataService, _registry, _passiveService, _configProvider, _logger,
            "hero1", 5, () => _closeCalled = true, onRetrainRequested: onRetrainRequested);

    [TestMethod]
    public void ShowRetrain_CareerWithAForkGroup_IsTrue()
    {
        SetupHeroWithCareer();
        AddForkAB();

        Assert.IsTrue(CreateVM().ShowRetrain);
    }

    [TestMethod]
    public void ShowRetrain_CareerWithOnlyUnlimitedGroups_IsFalse()
    {
        SetupHeroWithCareer();

        var vm = CreateVM();

        Assert.IsFalse(vm.ShowRetrain);
        Assert.IsFalse(vm.CanRetrain);
    }

    [TestMethod]
    public void CanRetrain_ForkPickFromAnEarlierVisit_IsTrue()
    {
        SetupHeroWithCareer();
        AddForkAB();
        StubMaxChoices(10);
        Take(CreateVM(), "wb_fork_a");

        Assert.IsTrue(CreateVM().CanRetrain);
    }

    [TestMethod]
    public void CanRetrain_ForkPickTakenThisVisit_IsFalse()
    {
        SetupHeroWithCareer();
        AddForkAB();
        StubMaxChoices(10);
        var vm = CreateVM();

        Take(vm, "wb_fork_a");

        Assert.IsFalse(vm.CanRetrain, "a pick taken this visit refunds free, so retraining is not offered");
    }

    [TestMethod]
    public void CanRetrain_OnePickThisVisitAndOneFromAnEarlierVisit_IsTrue()
    {
        // A pick taken now must not hide the earlier one that still needs the paid retrain (this
        // replaced a rule test when the retrain state moved into the screen's own walk).
        SetupHeroWithCareer();
        AddForkAB();
        AddGroup("wb_fork2", tier: 1, maxPicks: 1, Passive("wb_fork2_c", "wb_fork2"), Passive("wb_fork2_d", "wb_fork2"));
        StubMaxChoices(10);
        Take(CreateVM(), "wb_fork_a"); // an earlier visit
        var vm = CreateVM();

        Take(vm, "wb_fork2_c"); // this visit

        Assert.IsTrue(vm.CanRetrain);
    }

    [TestMethod]
    public void CanRetrain_NoForkPick_IsFalse()
    {
        SetupHeroWithCareer();
        AddForkAB();

        Assert.IsFalse(CreateVM().CanRetrain);
    }

    [TestMethod]
    public void RetrainText_ShowsTheCostInGold()
    {
        SetupHeroWithCareer();
        AddForkAB();
        _registry.GetRetrainCost(5).Returns(2250);

        Assert.AreEqual("Retrain (2250 gold)", CreateVM().RetrainText);
    }

    [TestMethod]
    public void ExecuteRetrain_WhenRetrainable_InvokesTheCallbackWithTheCost()
    {
        SetupHeroWithCareer();
        AddForkAB();
        StubMaxChoices(10);
        _registry.GetRetrainCost(5).Returns(2250);
        Take(CreateVM(), "wb_fork_a");
        int? requested = null;
        var vm = CreateRetrainVM(cost => requested = cost);

        vm.ExecuteRetrain();

        Assert.AreEqual(2250, requested);
    }

    [TestMethod]
    public void ExecuteRetrain_WhenNotRetrainable_DoesNotInvokeTheCallback()
    {
        SetupHeroWithCareer();
        AddForkAB();
        StubMaxChoices(10);
        var calls = 0;
        var vm = CreateRetrainVM(_ => calls++);
        Take(vm, "wb_fork_a"); // taken this visit: free to refund, so not retrainable

        vm.ExecuteRetrain();

        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    public void ExecuteRetrain_WithoutACallback_DoesNotThrow()
    {
        SetupHeroWithCareer();
        AddForkAB();
        StubMaxChoices(10);
        Take(CreateVM(), "wb_fork_a");

        CreateVM().ExecuteRetrain();
    }

    [TestMethod]
    public void OnRetrained_RebuildsWithNothingLockedAndClearsTheVisitSet()
    {
        SetupHeroWithCareer();
        AddForkAB();
        StubMaxChoices(10);
        var vm = CreateVM();
        Take(vm, "wb_fork_a"); // now in the visit set

        // The lifecycle service has refunded the picks; the screen is told afterwards.
        _dataService.RemoveChoice("hero1", "wb_fork_a");
        vm.OnRetrained();

        Assert.IsFalse(FindChoiceVM(vm, "wb_fork_a").IsTaken);
        Assert.IsFalse(FindChoiceVM(vm, "wb_fork_b").IsForkLocked, "nothing is re-locked after a retrain");
        Assert.IsFalse(vm.CanRetrain);

        // The visit set was cleared: a pick that reappears is no longer "taken this visit".
        _dataService.TryAddChoice("hero1", "wb_fork_a", 10);
        vm.RefreshValues();
        Assert.AreEqual("Locked in. Retrain to change this choice.", FindChoiceVM(vm, "wb_fork_a").LockReasonText);
        Assert.IsTrue(vm.CanRetrain);
    }

    // ── #769: the root perk is the career's always-held identity perk. CareerPassiveService.RefreshCache
    // applies it (taken or not) plus every stored choice, keystones included; the panel sums by the same
    // rule. The root description is keyed, as every shipped one is, so the VM must resolve it.

    private static CareerChoiceDefinition RootChoice(float damage = 0.05f) => new CareerChoiceDefinition(
        id: "wb_root", groupId: "", type: ChoiceType.Passive,
        description: "{=taom_test_root}Root perk text", iconSprite: "icon",
        passives: new[] { new PassiveEffect(PassiveEffectType.Damage, damage) }, mutations: null);

    [TestMethod]
    public void ActiveEffects_RootChoiceNeverTaken_ListsItsPassive()
    {
        _registry.GetChoice("wb_root").Returns(RootChoice());
        SetupHeroWithCareer();

        var vm = CreateVM();

        Assert.AreEqual(1, vm.PassiveEffectLines.Count);
        Assert.AreEqual("+5% damage", vm.PassiveEffectLines[0].LineText);
    }

    [TestMethod]
    public void ActiveEffects_RootChoiceHeldInTheHeroData_IsCountedOnce()
    {
        // The lifecycle service stores the root in ChoiceIds; RefreshCache then skips its second add.
        _registry.GetChoice("wb_root").Returns(RootChoice());
        SetupHeroWithCareer();
        _dataService.TryAddChoice("hero1", "wb_root", 10);

        var vm = CreateVM();

        Assert.AreEqual(1, vm.PassiveEffectLines.Count);
        Assert.AreEqual("+5% damage", vm.PassiveEffectLines[0].LineText);
    }

    [TestMethod]
    public void ActiveEffects_RootChoiceAlsoListedInAGroup_IsCountedOnce()
    {
        var root = RootChoice();
        _registry.GetChoice("wb_root").Returns(root);
        _registry.GetChoicesForGroup("wb_brutality").Returns(
            new List<CareerChoiceDefinition> { KeystoneChoice, PassiveChoice, root });
        SetupHeroWithCareer();
        _dataService.TryAddChoice("hero1", "wb_root", 10);

        var vm = CreateVM();

        Assert.AreEqual(1, vm.PassiveEffectLines.Count);
        Assert.AreEqual("+5% damage", vm.PassiveEffectLines[0].LineText);
    }

    [TestMethod]
    public void ActiveEffects_RootAndATakenPassiveOfOneType_SumIntoOneLine()
    {
        _registry.GetChoice("wb_root").Returns(RootChoice());
        SetupHeroWithCareer();
        StubMaxChoices(10);
        var vm = CreateVM();

        Take(vm, "wb_brut_p1"); // Damage 0.1

        Assert.AreEqual(1, vm.PassiveEffectLines.Count);
        Assert.AreEqual("+15% damage", vm.PassiveEffectLines[0].LineText);
    }

    [TestMethod]
    public void ActiveEffects_StoredChoiceInNoGroupOfTheCareer_StillCountsLikeRefreshCache()
    {
        // RefreshCache applies every stored id the registry resolves, and RepairForeignChoices keeps an
        // id whose owner it cannot prove, so such a choice is applied: the panel must not hide it.
        _registry.GetChoice("wb_stray").Returns(
            Passives("wb_stray", new PassiveEffect(PassiveEffectType.Damage, 0.1f)));
        SetupHeroWithCareer();
        _dataService.TryAddChoice("hero1", "wb_stray", 10);

        var vm = CreateVM();

        Assert.AreEqual(1, vm.PassiveEffectLines.Count);
        Assert.AreEqual("+10% damage", vm.PassiveEffectLines[0].LineText);
    }

    [TestMethod]
    public void ActiveEffects_GroupListedTwiceByTheCareer_CountsATakenPassiveOnce()
    {
        _registry.GetCareer("warboss").Returns(new CareerDefinition(
            id: "warboss", displayName: "Warboss", description: "A brute.",
            portraitSprite: "wb_sprite", abilityTemplateId: "rally_horde",
            minClanTier: 0, rootChoiceId: "wb_root",
            eligibleCultureIds: new List<string> { "mordor" },
            choiceGroupIds: new List<string> { "wb_brutality", "wb_brutality" }));
        SetupHeroWithCareer();
        StubMaxChoices(10);
        var vm = CreateVM();

        Take(vm, "wb_brut_p1"); // Damage 0.1

        Assert.AreEqual(1, vm.PassiveEffectLines.Count);
        Assert.AreEqual("+10% damage", vm.PassiveEffectLines[0].LineText);
    }

    [TestMethod]
    public void ActiveEffects_TakenKeystoneWithAPassive_ListsBothTheDescriptionAndThePassive()
    {
        var keystone = new CareerChoiceDefinition(
            id: "wb_brut_key", groupId: "wb_brutality", type: ChoiceType.Keystone,
            description: "Keystone", iconSprite: "icon",
            passives: new[] { new PassiveEffect(PassiveEffectType.Damage, 0.07f) }, mutations: null);
        _registry.GetChoice("wb_brut_key").Returns(keystone);
        _registry.GetChoicesForGroup("wb_brutality").Returns(
            new List<CareerChoiceDefinition> { keystone, PassiveChoice });
        SetupHeroWithCareer();
        StubMaxChoices(10);
        var vm = CreateVM();

        Take(vm, "wb_brut_key");

        Assert.AreEqual(1, vm.KeystoneEffectLines.Count);
        Assert.AreEqual("Keystone", vm.KeystoneEffectLines[0].LineText);
        Assert.AreEqual(1, vm.PassiveEffectLines.Count);
        Assert.AreEqual("+7% damage", vm.PassiveEffectLines[0].LineText);
    }

    [TestMethod]
    public void ActiveEffects_TakenKeystonePassiveAndRoot_SumIntoOneLine()
    {
        var keystone = new CareerChoiceDefinition(
            id: "wb_brut_key", groupId: "wb_brutality", type: ChoiceType.Keystone,
            description: "Keystone", iconSprite: "icon",
            passives: new[] { new PassiveEffect(PassiveEffectType.Damage, 0.07f) }, mutations: null);
        _registry.GetChoice("wb_brut_key").Returns(keystone);
        _registry.GetChoice("wb_root").Returns(RootChoice());
        _registry.GetChoicesForGroup("wb_brutality").Returns(
            new List<CareerChoiceDefinition> { keystone, PassiveChoice });
        SetupHeroWithCareer();
        StubMaxChoices(10);
        var vm = CreateVM();

        Take(vm, "wb_brut_key");

        Assert.AreEqual(1, vm.PassiveEffectLines.Count);
        Assert.AreEqual("+12% damage", vm.PassiveEffectLines[0].LineText);
    }

    [TestMethod]
    public void RootPerkDescription_CareerWithARootChoice_ShowsItsText()
    {
        _registry.GetChoice("wb_root").Returns(RootChoice());
        SetupHeroWithCareer();

        var vm = CreateVM();

        Assert.IsTrue(vm.HasRootPerk);
        Assert.AreEqual("Root perk text", vm.RootPerkDescription);
    }

    [TestMethod]
    public void RootPerkDescription_RootChoiceNotInTheRegistry_IsEmptyAndHidden()
    {
        SetupHeroWithCareer();

        var vm = CreateVM();

        Assert.IsFalse(vm.HasRootPerk);
        Assert.AreEqual("", vm.RootPerkDescription);
    }

    [TestMethod]
    public void RootPerkDescription_RootChoiceWithoutADescription_IsHidden()
    {
        _registry.GetChoice("wb_root").Returns(new CareerChoiceDefinition(
            id: "wb_root", groupId: "", type: ChoiceType.Passive, description: "", iconSprite: "icon",
            passives: null, mutations: null));
        SetupHeroWithCareer();

        var vm = CreateVM();

        Assert.IsFalse(vm.HasRootPerk);
        Assert.AreEqual("", vm.RootPerkDescription);
    }

    private void StubMaxChoices(int max)
        => _registry.GetMaxChoicesForHero(Arg.Any<CareerDefinition>(), 5).Returns(max);

    private static void Take(CareerScreenVM vm, string choiceId)
        => FindChoiceVM(vm, choiceId).ExecuteToggleChoice();

    private static CareerChoiceDefinition Passive(string id, string groupId)
        => new CareerChoiceDefinition(id, groupId, ChoiceType.Passive, "Passive", "icon",
            new[] { new PassiveEffect(PassiveEffectType.Damage, 0.1f) }, null);

    private readonly List<string> _extraGroupIds = new List<string>();

    // Adds a group to the Warboss career on top of the fixture's wb_brutality group.
    private void AddGroup(string groupId, int tier, int maxPicks, params CareerChoiceDefinition[] choices)
    {
        _extraGroupIds.Add(groupId);
        var ids = new List<string>();
        foreach (var c in choices)
        {
            ids.Add(c.Id);
            _registry.GetChoice(c.Id).Returns(c);
        }
        _registry.GetGroup(groupId).Returns(new CareerChoiceGroupDefinition(
            groupId, "warboss", tier, ids, maxPicks: maxPicks));
        _registry.GetChoicesForGroup(groupId).Returns(new List<CareerChoiceDefinition>(choices));

        var groupIds = new List<string> { "wb_brutality" };
        groupIds.AddRange(_extraGroupIds);
        _registry.GetCareer("warboss").Returns(new CareerDefinition(
            id: "warboss", displayName: "Warboss", description: "A brute.",
            portraitSprite: "wb_sprite", abilityTemplateId: "rally_horde",
            minClanTier: 0, rootChoiceId: "wb_root",
            eligibleCultureIds: new List<string> { "mordor" },
            choiceGroupIds: groupIds));
    }

    [TestMethod]
    public void ExecuteToggleChoice_ParsedForkGroup_RefusesTheSiblingAndLocksInOnReopen()
    {
        var (vm, registry, dir) = CreateParsedVM(heroLevel: 5);
        string reopenedDir = null;
        try
        {
            Assert.AreEqual(1, registry.GetGroup("t_g4").MaxPicks);

            FindChoiceVM(vm, "t_g4_a").ExecuteToggleChoice();
            FindChoiceVM(vm, "t_g4_b").ExecuteToggleChoice();

            var data = _dataService.GetOrCreateData("hero1");
            Assert.IsTrue(data.HasChoice("t_g4_a"));
            Assert.IsFalse(data.HasChoice("t_g4_b"), "the parsed fork holds one pick");
            Assert.IsTrue(FindChoiceVM(vm, "t_g4_b").IsForkLocked);

            CareerScreenVM reopened;
            (reopened, _, reopenedDir) = CreateParsedVM(heroLevel: 5);
            FindChoiceVM(reopened, "t_g4_a").ExecuteToggleChoice();

            Assert.IsTrue(data.HasChoice("t_g4_a"), "locked in once the screen is reopened");
            Assert.AreEqual("Locked in. Retrain to change this choice.", FindChoiceVM(reopened, "t_g4_a").LockReasonText);
        }
        finally
        {
            System.IO.Directory.Delete(dir, true);
            if (reopenedDir != null) System.IO.Directory.Delete(reopenedDir, true);
        }
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
