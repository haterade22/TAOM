using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
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

    // Phase 9b #173 — new ApplyFactor/ApplyFlat methods replacing the static CareerPassiveHelper.

    [TestMethod]
    public void ApplyFactor_NonZeroMagnitude_AddsFactorToResult()
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

        var result = new ExplainedNumber(100f);
        _service.ApplyFactor("hero1", ref result, PassiveEffectType.Damage);

        // 100 * (1 + 0.10) = 110
        Assert.AreEqual(110f, result.ResultNumber, 0.01f);
    }

    [TestMethod]
    public void ApplyFlat_NonZeroMagnitude_AddsFlatToResult()
    {
        _dataService.SetCareer("hero1", "warboss");
        _dataService.TryAddChoice("hero1", "wb_morale_flat", 10);
        _registry.GetChoice("wb_morale_flat").Returns(new CareerChoiceDefinition(
            id: "wb_morale_flat", groupId: "wb_morale", type: ChoiceType.Passive,
            description: "", iconSprite: "",
            passive: new PassiveEffect(PassiveEffectType.TroopMorale, 5f),
            mutations: null));
        _registry.GetChoice("wb_root").Returns(new CareerChoiceDefinition(
            id: "wb_root", groupId: "", type: ChoiceType.Passive,
            description: "", iconSprite: "", passive: null, mutations: null));
        _service.RefreshCache(_dataService, _registry);

        var result = new ExplainedNumber(50f);
        _service.ApplyFlat("hero1", ref result, PassiveEffectType.TroopMorale);

        Assert.AreEqual(55f, result.ResultNumber, 0.01f);
    }

    [TestMethod]
    public void ApplyFlat_PartySizeWholeCountMagnitude_AddsFlatNotFactor()
    {
        // Regression guard for the "+2 party size -> +150" bug (issue: party-size math).
        // PartySize passives are authored as WHOLE COUNTS (magnitude="2", description "+2 party
        // size"), so TaomPartySizeModel MUST apply them via ApplyFlat (result.Add -> +2), NOT
        // ApplyFactor (result.AddFactor(2) -> x3 = +200%). This test documents both semantics so
        // a future change back to ApplyFactor is visibly wrong against the contrast assertion.
        _dataService.SetCareer("hero1", "warboss");
        _dataService.TryAddChoice("hero1", "wb_party_size", 10);
        _registry.GetChoice("wb_party_size").Returns(new CareerChoiceDefinition(
            id: "wb_party_size", groupId: "wb_command", type: ChoiceType.Passive,
            description: "", iconSprite: "",
            passive: new PassiveEffect(PassiveEffectType.PartySize, 2f),
            mutations: null));
        _registry.GetChoice("wb_root").Returns(new CareerChoiceDefinition(
            id: "wb_root", groupId: "", type: ChoiceType.Passive,
            description: "", iconSprite: "", passive: null, mutations: null));
        _service.RefreshCache(_dataService, _registry);

        var flat = new ExplainedNumber(100f);
        _service.ApplyFlat("hero1", ref flat, PassiveEffectType.PartySize);
        Assert.AreEqual(102f, flat.ResultNumber, 0.01f, "PartySize must add a flat +2");

        // Contrast: the buggy path (ApplyFactor) triples the base on a whole-count magnitude.
        var factor = new ExplainedNumber(100f);
        _service.ApplyFactor("hero1", ref factor, PassiveEffectType.PartySize);
        Assert.AreEqual(300f, factor.ResultNumber, 0.01f, "ApplyFactor on a whole-count magnitude is the bug");
    }

    [TestMethod]
    public void ApplyFactor_NullHeroId_IsNoOp()
    {
        var result = new ExplainedNumber(100f);
        _service.ApplyFactor(null, ref result, PassiveEffectType.Damage);
        Assert.AreEqual(100f, result.ResultNumber);
    }

    [TestMethod]
    public void ApplyFactor_EmptyHeroId_IsNoOp()
    {
        var result = new ExplainedNumber(100f);
        _service.ApplyFactor("", ref result, PassiveEffectType.Damage);
        Assert.AreEqual(100f, result.ResultNumber);
    }

    [TestMethod]
    public void ApplyFactor_ZeroMagnitudeOrMissingHero_IsNoOp()
    {
        // Hero has no career data → magnitude=0 → no AddFactor call → result unchanged.
        _service.RefreshCache(_dataService, _registry);
        var result = new ExplainedNumber(100f);
        _service.ApplyFactor("nonexistent_hero", ref result, PassiveEffectType.Damage);
        Assert.AreEqual(100f, result.ResultNumber);
    }

    [TestMethod]
    public void ApplyFlat_NullHeroId_IsNoOp()
    {
        var result = new ExplainedNumber(100f);
        _service.ApplyFlat(null, ref result, PassiveEffectType.TroopMorale);
        Assert.AreEqual(100f, result.ResultNumber);
    }

    [TestMethod]
    public void GetPassiveMagnitude_NullHeroId_ReturnsZero()
    {
        // Phase 9b #173 — explicit null/empty heroId guard added in the refactor.
        Assert.AreEqual(0f, _service.GetPassiveMagnitude(null, PassiveEffectType.Damage));
        Assert.AreEqual(0f, _service.GetPassiveMagnitude("", PassiveEffectType.Damage));
    }

    // ── GetMaskedMagnitude — attack_type_mask honoring for Damage/Resistance ──

    private static CareerChoiceDefinition NullRootChoice() => new CareerChoiceDefinition(
        id: "wb_root", groupId: "", type: ChoiceType.Passive,
        description: "", iconSprite: "", passive: null, mutations: null);

    [TestMethod]
    public void GetMaskedMagnitude_MeleeMaskedDamage_AppliesToMeleeHitOnly()
    {
        _dataService.SetCareer("hero1", "warboss");
        _dataService.TryAddChoice("hero1", "wb_melee_dmg", 10);
        _registry.GetChoice("wb_melee_dmg").Returns(new CareerChoiceDefinition(
            id: "wb_melee_dmg", groupId: "g", type: ChoiceType.Passive, description: "", iconSprite: "",
            passive: new PassiveEffect(PassiveEffectType.Damage, 0.15f, attackTypeMask: AttackTypeMask.Melee),
            mutations: null));
        _registry.GetChoice("wb_root").Returns(NullRootChoice());
        _service.RefreshCache(_dataService, _registry);

        Assert.AreEqual(0.15f, _service.GetMaskedMagnitude("hero1", PassiveEffectType.Damage, AttackTypeMask.Melee), 0.001f);
        Assert.AreEqual(0f, _service.GetMaskedMagnitude("hero1", PassiveEffectType.Damage, AttackTypeMask.Ranged), 0.001f);
    }

    [TestMethod]
    public void GetMaskedMagnitude_AllMaskedEntry_AppliesToBothDeliveryTypes()
    {
        _dataService.SetCareer("hero1", "warboss");
        _dataService.TryAddChoice("hero1", "wb_all_dmg", 10);
        _registry.GetChoice("wb_all_dmg").Returns(new CareerChoiceDefinition(
            id: "wb_all_dmg", groupId: "g", type: ChoiceType.Passive, description: "", iconSprite: "",
            passive: new PassiveEffect(PassiveEffectType.Damage, 0.10f, attackTypeMask: AttackTypeMask.All),
            mutations: null));
        _registry.GetChoice("wb_root").Returns(NullRootChoice());
        _service.RefreshCache(_dataService, _registry);

        Assert.AreEqual(0.10f, _service.GetMaskedMagnitude("hero1", PassiveEffectType.Damage, AttackTypeMask.Melee), 0.001f);
        Assert.AreEqual(0.10f, _service.GetMaskedMagnitude("hero1", PassiveEffectType.Damage, AttackTypeMask.Ranged), 0.001f);
    }

    [TestMethod]
    public void GetMaskedMagnitude_MeleeAndRangedPips_SumOnlyMatchingDeliveryType()
    {
        _dataService.SetCareer("hero1", "warboss");
        _dataService.TryAddChoice("hero1", "wb_melee", 10);
        _dataService.TryAddChoice("hero1", "wb_ranged", 10);
        _registry.GetChoice("wb_melee").Returns(new CareerChoiceDefinition(
            id: "wb_melee", groupId: "g", type: ChoiceType.Passive, description: "", iconSprite: "",
            passive: new PassiveEffect(PassiveEffectType.Damage, 0.12f, attackTypeMask: AttackTypeMask.Melee),
            mutations: null));
        _registry.GetChoice("wb_ranged").Returns(new CareerChoiceDefinition(
            id: "wb_ranged", groupId: "g", type: ChoiceType.Passive, description: "", iconSprite: "",
            passive: new PassiveEffect(PassiveEffectType.Damage, 0.08f, attackTypeMask: AttackTypeMask.Ranged),
            mutations: null));
        _registry.GetChoice("wb_root").Returns(NullRootChoice());
        _service.RefreshCache(_dataService, _registry);

        Assert.AreEqual(0.12f, _service.GetMaskedMagnitude("hero1", PassiveEffectType.Damage, AttackTypeMask.Melee), 0.001f);
        Assert.AreEqual(0.08f, _service.GetMaskedMagnitude("hero1", PassiveEffectType.Damage, AttackTypeMask.Ranged), 0.001f);
        // The mask-agnostic total still sums both (used by non-damage consumers).
        Assert.AreEqual(0.20f, _service.GetPassiveMagnitude("hero1", PassiveEffectType.Damage), 0.001f);
    }

    [TestMethod]
    public void GetMaskedMagnitude_AllPlusMeleePips_SumOnMeleeButOnlyAllOnRanged()
    {
        // The live black_uruk_captain case: an All-masked Resistance (e.g. a Blunt pip coarsened to
        // All) + a Melee-masked Resistance on one hero. On a melee hit BOTH buckets intersect and
        // sum; on a ranged hit only the All bucket applies.
        _dataService.SetCareer("hero1", "warboss");
        _dataService.TryAddChoice("hero1", "wb_all", 10);
        _dataService.TryAddChoice("hero1", "wb_melee", 10);
        _registry.GetChoice("wb_all").Returns(new CareerChoiceDefinition(
            id: "wb_all", groupId: "g", type: ChoiceType.Passive, description: "", iconSprite: "",
            passive: new PassiveEffect(PassiveEffectType.Resistance, 0.10f, attackTypeMask: AttackTypeMask.All),
            mutations: null));
        _registry.GetChoice("wb_melee").Returns(new CareerChoiceDefinition(
            id: "wb_melee", groupId: "g", type: ChoiceType.Passive, description: "", iconSprite: "",
            passive: new PassiveEffect(PassiveEffectType.Resistance, 0.05f, attackTypeMask: AttackTypeMask.Melee),
            mutations: null));
        _registry.GetChoice("wb_root").Returns(NullRootChoice());
        _service.RefreshCache(_dataService, _registry);

        Assert.AreEqual(0.15f, _service.GetMaskedMagnitude("hero1", PassiveEffectType.Resistance, AttackTypeMask.Melee), 0.001f);
        Assert.AreEqual(0.10f, _service.GetMaskedMagnitude("hero1", PassiveEffectType.Resistance, AttackTypeMask.Ranged), 0.001f);
    }

    [TestMethod]
    public void GetMaskedMagnitude_NullHeroOrMissingHero_ReturnsZero()
    {
        _service.RefreshCache(_dataService, _registry);
        Assert.AreEqual(0f, _service.GetMaskedMagnitude(null, PassiveEffectType.Damage, AttackTypeMask.Melee));
        Assert.AreEqual(0f, _service.GetMaskedMagnitude("nobody", PassiveEffectType.Damage, AttackTypeMask.Melee));
    }

    [TestMethod]
    public void RefreshCache_SecondRefresh_ReplacesPriorCache()
    {
        // Phase 9b #173 F2 — snapshot-swap pattern. Second RefreshCache builds a new dict +
        // atomically swaps the reference. Heroes present in the first refresh but absent in the
        // second should return zero magnitude after the second refresh.
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

        // Wipe the data service — second refresh should produce an empty cache.
        var emptyDataService = new CareerDataService();
        _service.RefreshCache(emptyDataService, _registry);
        Assert.AreEqual(0f, _service.GetPassiveMagnitude("hero1", PassiveEffectType.Damage));
        Assert.IsFalse(_service.HasActivePassive("hero1", PassiveEffectType.Damage));
    }
}
