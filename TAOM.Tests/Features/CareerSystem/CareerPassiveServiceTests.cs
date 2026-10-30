using System.Collections.Generic;
using System.Linq;
using AiPartySizeService = TAOM.Features.AiPartySize.AiPartySizeService;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

[TestClass]
[TestCategory("RequiresGame")]
public class CareerPassiveServiceTests
{
    private CareerPassiveService _service;
    private ICareerDataService _dataService;
    private ICareerRegistry _registry;
    private IModLogger _logger;
    private IPartySizeCacheInvalidator _invalidator;
    private List<List<string>> _invalidatedIds;

    private static readonly CareerDefinition WarbossCareer = new CareerDefinition(
        id: "warboss", displayName: "Warboss", description: "", portraitSprite: "",
        abilityTemplateId: "rally",
        minClanTier: 0, rootChoiceId: "wb_root",
        eligibleCultureIds: new List<string> { "mordor" },
        choiceGroupIds: new List<string>());

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _invalidator = Substitute.For<IPartySizeCacheInvalidator>();
        _invalidatedIds = new List<List<string>>();
        _invalidator.When(i => i.InvalidateLedBy(Arg.Any<ICollection<string>>()))
            .Do(call => _invalidatedIds.Add(call.Arg<ICollection<string>>().OrderBy(id => id).ToList()));
        _service = new CareerPassiveService(_logger, _invalidator);
        _dataService = new CareerDataService();
        _registry = Substitute.For<ICareerRegistry>();

        _registry.GetCareer("warboss").Returns(WarbossCareer);
    }

    // #768: PartyBase.PartySizeLimit caches on MemberRoster.VersionNo, so a pick that moves the
    // PartySize passive is invisible to the engine until the cache is busted.
    [TestMethod]
    public void RefreshCache_InvalidatesOnlyTheHeroesWithPassives_AndNeverSweepsEveryParty()
    {
        GivenPartySizePassive(4f);

        Assert.AreEqual(1, _invalidatedIds.Count);
        CollectionAssert.AreEqual(new[] { "hero1" }, _invalidatedIds[0]);
        _invalidator.DidNotReceive().InvalidateAll();
    }

    [TestMethod]
    public void RefreshCache_HeroWhosePassivesWentAway_IsStillInvalidated()
    {
        // hero1 loses every passive, hero2 gains one: both parties' cached limits are stale.
        GivenPartySizePassive(4f);
        _invalidatedIds.Clear();
        _dataService.SetCareer("hero2", "warboss");
        _dataService.TryAddChoice("hero2", "wb_party_size", 10);
        var onlyHero2 = Substitute.For<ICareerDataService>();
        onlyHero2.GetAllData().Returns(new Dictionary<string, HeroCareerData>
        {
            ["hero2"] = _dataService.GetAllData()["hero2"],
        });

        _service.RefreshCache(onlyHero2, _registry);

        Assert.AreEqual(1, _invalidatedIds.Count);
        CollectionAssert.AreEqual(new[] { "hero1", "hero2" }, _invalidatedIds[0]);
    }

    [TestMethod]
    public void RefreshCache_NothingBeforeOrAfter_PassesAnEmptySet()
    {
        _service.RefreshCache(_dataService, _registry);

        Assert.AreEqual(1, _invalidatedIds.Count);
        Assert.AreEqual(0, _invalidatedIds[0].Count);
    }

    [TestMethod]
    public void RefreshCache_ThrowingInvalidator_KeepsNewCacheAndWarnsOnce()
    {
        _invalidator.When(i => i.InvalidateLedBy(Arg.Any<ICollection<string>>()))
            .Do(_ => throw new System.InvalidOperationException("sweep failed"));
        _dataService.SetCareer("hero1", "warboss");
        _dataService.TryAddChoice("hero1", "wb_brut_p1", 10);
        _registry.GetChoice("wb_brut_p1").Returns(new CareerChoiceDefinition(
            id: "wb_brut_p1", groupId: "wb_brutality", type: ChoiceType.Passive,
            description: "", iconSprite: "",
            passives: new[] { new PassiveEffect(PassiveEffectType.PartySize, 5f) },
            mutations: null));
        _registry.GetChoice("wb_root").Returns(new CareerChoiceDefinition(
            id: "wb_root", groupId: "", type: ChoiceType.Passive,
            description: "", iconSprite: "", passives: null, mutations: null));

        _service.RefreshCache(_dataService, _registry);

        Assert.AreEqual(5f, _service.GetPassiveMagnitude("hero1", PassiveEffectType.PartySize), 0.001f);
        _logger.Received(1).LogWarning(Arg.Any<string>());
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
            passives: new[] { new PassiveEffect(PassiveEffectType.Damage, 0.10f) },
            mutations: null));

        _registry.GetChoice("wb_root").Returns(new CareerChoiceDefinition(
            id: "wb_root", groupId: "", type: ChoiceType.Passive,
            description: "", iconSprite: "", passives: null, mutations: null));

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
            passives: new[] { new PassiveEffect(PassiveEffectType.Damage, 0.10f) },
            mutations: null));

        _registry.GetChoice("wb_brut_p2").Returns(new CareerChoiceDefinition(
            id: "wb_brut_p2", groupId: "wb_brutality", type: ChoiceType.Passive,
            description: "", iconSprite: "",
            passives: new[] { new PassiveEffect(PassiveEffectType.Damage, 0.05f) },
            mutations: null));

        _registry.GetChoice("wb_root").Returns(new CareerChoiceDefinition(
            id: "wb_root", groupId: "", type: ChoiceType.Passive,
            description: "", iconSprite: "", passives: null, mutations: null));

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
            passives: new[] { new PassiveEffect(PassiveEffectType.Damage, 0.10f) },
            mutations: null));

        _registry.GetChoice("wb_root").Returns(new CareerChoiceDefinition(
            id: "wb_root", groupId: "", type: ChoiceType.Passive,
            description: "", iconSprite: "", passives: null, mutations: null));

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
            passives: new[] { new PassiveEffect(PassiveEffectType.TroopDamage, 0.05f) },
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
            passives: new[] { new PassiveEffect(PassiveEffectType.TroopDamage, 0.05f) },
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
            passives: new[] { new PassiveEffect(PassiveEffectType.Damage, 0.10f) },
            mutations: null));
        _registry.GetChoice("wb_root").Returns(new CareerChoiceDefinition(
            id: "wb_root", groupId: "", type: ChoiceType.Passive,
            description: "", iconSprite: "", passives: null, mutations: null));
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
            passives: new[] { new PassiveEffect(PassiveEffectType.TroopMorale, 5f) },
            mutations: null));
        _registry.GetChoice("wb_root").Returns(new CareerChoiceDefinition(
            id: "wb_root", groupId: "", type: ChoiceType.Passive,
            description: "", iconSprite: "", passives: null, mutations: null));
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
            passives: new[] { new PassiveEffect(PassiveEffectType.PartySize, 2f) },
            mutations: null));
        _registry.GetChoice("wb_root").Returns(new CareerChoiceDefinition(
            id: "wb_root", groupId: "", type: ChoiceType.Passive,
            description: "", iconSprite: "", passives: null, mutations: null));
        _service.RefreshCache(_dataService, _registry);

        var flat = new ExplainedNumber(100f);
        _service.ApplyFlat("hero1", ref flat, PassiveEffectType.PartySize);
        Assert.AreEqual(102f, flat.ResultNumber, 0.01f, "PartySize must add a flat +2");

        // Contrast: the buggy path (ApplyFactor) triples the base on a whole-count magnitude.
        var factor = new ExplainedNumber(100f);
        _service.ApplyFactor("hero1", ref factor, PassiveEffectType.PartySize);
        Assert.AreEqual(300f, factor.ResultNumber, 0.01f, "ApplyFactor on a whole-count magnitude is the bug");
    }

    // Deep review 2026-08-18. The test above proves a flat count is not applied as a FACTOR. It does
    // not prove the count survives contact with factors added by someone else, because it runs on a
    // bare ExplainedNumber. ExplainedNumber resolves as BaseNumber * (1 + SumOfFactors), so a
    // base-frame Add is silently multiplied by every factor on the number. That was a rounding error
    // while only culture feats contributed; once AiPartySize started adding a large factor
    // (garrisons default to 3x) a "+4 party size" perk became worth +13 and the career screen's
    // promise stopped being true.
    private void GivenPartySizePassive(float magnitude)
    {
        _dataService.SetCareer("hero1", "warboss");
        _dataService.TryAddChoice("hero1", "wb_party_size", 10);
        _registry.GetChoice("wb_party_size").Returns(new CareerChoiceDefinition(
            id: "wb_party_size", groupId: "wb_command", type: ChoiceType.Passive,
            description: "", iconSprite: "",
            passives: new[] { new PassiveEffect(PassiveEffectType.PartySize, magnitude) },
            mutations: null));
        _registry.GetChoice("wb_root").Returns(new CareerChoiceDefinition(
            id: "wb_root", groupId: "", type: ChoiceType.Passive,
            description: "", iconSprite: "", passives: null, mutations: null));
        _service.RefreshCache(_dataService, _registry);
    }

    [TestMethod]
    public void ApplyFlat_UnderACultureFactor_IsStillWorthItsLiteralCount()
    {
        // A culture party-size feat is already in the frame when the career passive lands.
        GivenPartySizePassive(2f);
        var result = new ExplainedNumber(100f);
        result.AddFactor(0.20f);

        _service.ApplyFlat("hero1", ref result, PassiveEffectType.PartySize);

        Assert.AreEqual(122f, result.ResultNumber, 0.01f, "+2 must be worth exactly 2 bodies (120 + 2)");
        Assert.AreNotEqual(122.4f, result.ResultNumber, 0.01f, "a base-frame Add would be amplified to +2.4");
    }

    [TestMethod]
    public void ApplyFlat_UnderALargeAiPartySizeFactor_IsNotAmplified()
    {
        // A player-owned garrison: 3x garrison multiplier plus a 0.2 culture feat = scale 3.2.
        GivenPartySizePassive(4f);
        var result = new ExplainedNumber(100f);
        result.AddFactor(2.2f);

        _service.ApplyFlat("hero1", ref result, PassiveEffectType.PartySize);

        Assert.AreEqual(324f, result.ResultNumber, 0.01f, "+4 must be worth exactly 4 bodies (320 + 4)");
        Assert.AreNotEqual(332.8f, result.ResultNumber, 0.01f,
            "the pre-fix base-frame Add turned a '+4 party size' perk into +12.8");
    }

    [TestMethod]
    public void ApplyFlat_AfterTheAiScaling_IsWorthItsLiteralCount()
    {
        // 100 * (1 + 0.025 + 2) = 302.5; +4 divided by the 3.025 frame lands as exactly 4 bodies.
        // The order the model uses: every AddFactor first, ApplyFlat last (#768).
        GivenPartySizePassive(4f);
        var result = new ExplainedNumber(100f);
        result.AddFactor(0.025f);
        AiPartySizeService.ApplyPartySizeScaling(ref result, 3f, 0f);

        _service.ApplyFlat("hero1", ref result, PassiveEffectType.PartySize);

        Assert.AreEqual(306.5f, result.ResultNumber, 0.01f, "302.5 + 4 literal bodies");
    }

    [TestMethod]
    public void ApplyFlat_NoFactors_IsUnchangedByTheResultFrameConversion()
    {
        // The Health call site (TaomCharacterStatsModel) runs on a factor-free ExplainedNumber:
        // vanilla DefaultCharacterStatsModel.MaxHitpoints uses only Add, never AddFactor. The
        // conversion must therefore be a no-op there, which is why it was safe to change.
        GivenPartySizePassive(25f);
        var result = new ExplainedNumber(100f);

        _service.ApplyFlat("hero1", ref result, PassiveEffectType.PartySize);

        Assert.AreEqual(125f, result.ResultNumber, 0.01f);
    }

    [TestMethod]
    public void ApplyFlat_CancelledFactorFrame_IsSkippedRatherThanDividingByNearZero()
    {
        GivenPartySizePassive(4f);
        var result = new ExplainedNumber(100f);
        result.AddFactor(-1f);

        _service.ApplyFlat("hero1", ref result, PassiveEffectType.PartySize);

        Assert.AreEqual(0f, result.ResultNumber, 0.01f);
        Assert.IsFalse(float.IsNaN(result.ResultNumber), "a cancelled frame must not poison the number");
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
        description: "", iconSprite: "", passives: null, mutations: null);

    [TestMethod]
    public void GetMaskedMagnitude_MeleeMaskedDamage_AppliesToMeleeHitOnly()
    {
        _dataService.SetCareer("hero1", "warboss");
        _dataService.TryAddChoice("hero1", "wb_melee_dmg", 10);
        _registry.GetChoice("wb_melee_dmg").Returns(new CareerChoiceDefinition(
            id: "wb_melee_dmg", groupId: "g", type: ChoiceType.Passive, description: "", iconSprite: "",
            passives: new[] { new PassiveEffect(PassiveEffectType.Damage, 0.15f, attackTypeMask: AttackTypeMask.Melee) },
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
            passives: new[] { new PassiveEffect(PassiveEffectType.Damage, 0.10f, attackTypeMask: AttackTypeMask.All) },
            mutations: null));
        _registry.GetChoice("wb_root").Returns(NullRootChoice());
        _service.RefreshCache(_dataService, _registry);

        Assert.AreEqual(0.10f, _service.GetMaskedMagnitude("hero1", PassiveEffectType.Damage, AttackTypeMask.Melee), 0.001f);
        Assert.AreEqual(0.10f, _service.GetMaskedMagnitude("hero1", PassiveEffectType.Damage, AttackTypeMask.Ranged), 0.001f);
    }

    [TestMethod]
    public void GetMaskedMagnitude_BluntPip_AppliesToBluntHitsOfEitherDeliveryOnly()
    {
        // #613: a kind-masked pip matches on the kind axis whatever the delivery, and stays out of
        // a hit of another kind.
        _dataService.SetCareer("hero1", "warboss");
        _dataService.TryAddChoice("hero1", "wb_blunt", 10);
        _registry.GetChoice("wb_blunt").Returns(new CareerChoiceDefinition(
            id: "wb_blunt", groupId: "g", type: ChoiceType.Passive, description: "", iconSprite: "",
            passives: new[] { new PassiveEffect(PassiveEffectType.Resistance, 0.06f, attackTypeMask: AttackTypeMask.Blunt) },
            mutations: null));
        _registry.GetChoice("wb_root").Returns(NullRootChoice());
        _service.RefreshCache(_dataService, _registry);

        Assert.AreEqual(0.06f, _service.GetMaskedMagnitude("hero1", PassiveEffectType.Resistance, AttackTypeMask.Melee | AttackTypeMask.Blunt), 0.001f);
        Assert.AreEqual(0.06f, _service.GetMaskedMagnitude("hero1", PassiveEffectType.Resistance, AttackTypeMask.Ranged | AttackTypeMask.Blunt), 0.001f);
        Assert.AreEqual(0f, _service.GetMaskedMagnitude("hero1", PassiveEffectType.Resistance, AttackTypeMask.Melee | AttackTypeMask.Cut), 0.001f);
        Assert.AreEqual(0f, _service.GetMaskedMagnitude("hero1", PassiveEffectType.Resistance, AttackTypeMask.Melee), 0.001f);
    }

    [TestMethod]
    public void GetMaskedMagnitude_MeleeAndRangedPips_SumOnlyMatchingDeliveryType()
    {
        _dataService.SetCareer("hero1", "warboss");
        _dataService.TryAddChoice("hero1", "wb_melee", 10);
        _dataService.TryAddChoice("hero1", "wb_ranged", 10);
        _registry.GetChoice("wb_melee").Returns(new CareerChoiceDefinition(
            id: "wb_melee", groupId: "g", type: ChoiceType.Passive, description: "", iconSprite: "",
            passives: new[] { new PassiveEffect(PassiveEffectType.Damage, 0.12f, attackTypeMask: AttackTypeMask.Melee) },
            mutations: null));
        _registry.GetChoice("wb_ranged").Returns(new CareerChoiceDefinition(
            id: "wb_ranged", groupId: "g", type: ChoiceType.Passive, description: "", iconSprite: "",
            passives: new[] { new PassiveEffect(PassiveEffectType.Damage, 0.08f, attackTypeMask: AttackTypeMask.Ranged) },
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
            passives: new[] { new PassiveEffect(PassiveEffectType.Resistance, 0.10f, attackTypeMask: AttackTypeMask.All) },
            mutations: null));
        _registry.GetChoice("wb_melee").Returns(new CareerChoiceDefinition(
            id: "wb_melee", groupId: "g", type: ChoiceType.Passive, description: "", iconSprite: "",
            passives: new[] { new PassiveEffect(PassiveEffectType.Resistance, 0.05f, attackTypeMask: AttackTypeMask.Melee) },
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
            passives: new[] { new PassiveEffect(PassiveEffectType.Damage, 0.10f) },
            mutations: null));
        _registry.GetChoice("wb_root").Returns(new CareerChoiceDefinition(
            id: "wb_root", groupId: "", type: ChoiceType.Passive,
            description: "", iconSprite: "", passives: null, mutations: null));

        _service.RefreshCache(_dataService, _registry);
        Assert.AreEqual(0.10f, _service.GetPassiveMagnitude("hero1", PassiveEffectType.Damage), 0.001f);

        // Wipe the data service — second refresh should produce an empty cache.
        var emptyDataService = new CareerDataService();
        _service.RefreshCache(emptyDataService, _registry);
        Assert.AreEqual(0f, _service.GetPassiveMagnitude("hero1", PassiveEffectType.Damage));
        Assert.IsFalse(_service.HasActivePassive("hero1", PassiveEffectType.Damage));
    }

    // ── #769 E8 + E9: several effects per option, and the troop-class bucket ──

    private void GivenChoice(string id, params PassiveEffect[] passives)
    {
        _dataService.SetCareer("hero1", "warboss");
        _dataService.TryAddChoice("hero1", id, 10);
        _registry.GetChoice(id).Returns(new CareerChoiceDefinition(
            id: id, groupId: "g", type: ChoiceType.Passive, description: "", iconSprite: "",
            passives: passives, mutations: null));
        _registry.GetChoice("wb_root").Returns(NullRootChoice());
        _service.RefreshCache(_dataService, _registry);
    }

    private float Troop(PassiveEffectType type, TroopClassMask troopClass, AttackTypeMask? hit = null)
        => _service.GetTroopMagnitude("hero1", type, troopClass, hit);

    [TestMethod]
    public void RefreshCache_ChoiceWithTwoPassives_AccumulatesBoth()
    {
        GivenChoice("wb_two",
            new PassiveEffect(PassiveEffectType.Damage, 0.10f),
            new PassiveEffect(PassiveEffectType.Health, 25f));

        Assert.AreEqual(0.10f, _service.GetPassiveMagnitude("hero1", PassiveEffectType.Damage), 0.001f);
        Assert.AreEqual(25f, _service.GetPassiveMagnitude("hero1", PassiveEffectType.Health), 0.001f);
        Assert.IsTrue(_service.HasActivePassive("hero1", PassiveEffectType.Health));
    }

    [TestMethod]
    public void RefreshCache_TwoPassivesOfOneType_SumIntoTheSameTotal()
    {
        GivenChoice("wb_two",
            new PassiveEffect(PassiveEffectType.Damage, 0.10f, attackTypeMask: AttackTypeMask.Melee),
            new PassiveEffect(PassiveEffectType.Damage, 0.04f, attackTypeMask: AttackTypeMask.Ranged));

        Assert.AreEqual(0.14f, _service.GetPassiveMagnitude("hero1", PassiveEffectType.Damage), 0.001f);
        Assert.AreEqual(0.10f, _service.GetMaskedMagnitude("hero1", PassiveEffectType.Damage, AttackTypeMask.Melee), 0.001f);
        Assert.AreEqual(0.04f, _service.GetMaskedMagnitude("hero1", PassiveEffectType.Damage, AttackTypeMask.Ranged), 0.001f);
    }

    [TestMethod]
    public void RefreshCache_RootChoiceWithTwoPassives_AccumulatesBoth()
    {
        _dataService.SetCareer("hero1", "warboss");
        _registry.GetChoice("wb_root").Returns(new CareerChoiceDefinition(
            id: "wb_root", groupId: "", type: ChoiceType.Passive, description: "", iconSprite: "",
            passives: new[]
            {
                new PassiveEffect(PassiveEffectType.TroopDamage, 0.05f, troopClass: TroopClassMask.Infantry),
                new PassiveEffect(PassiveEffectType.TroopResistance, 0.03f),
            },
            mutations: null));
        _service.RefreshCache(_dataService, _registry);

        Assert.AreEqual(0.05f, _service.GetPassiveMagnitude("hero1", PassiveEffectType.TroopDamage), 0.001f);
        Assert.AreEqual(0.03f, _service.GetPassiveMagnitude("hero1", PassiveEffectType.TroopResistance), 0.001f);
        Assert.AreEqual(0.05f, Troop(PassiveEffectType.TroopDamage, TroopClassMask.Infantry), 0.001f);
        Assert.AreEqual(0f, Troop(PassiveEffectType.TroopDamage, TroopClassMask.Cavalry), 0.001f);
    }

    [TestMethod]
    public void GetTroopMagnitude_InfantryPip_AppliesToInfantryOnly()
    {
        GivenChoice("wb_inf", new PassiveEffect(PassiveEffectType.TroopDamage, 0.10f, troopClass: TroopClassMask.Infantry));

        Assert.AreEqual(0.10f, Troop(PassiveEffectType.TroopDamage, TroopClassMask.Infantry), 0.001f);
        Assert.AreEqual(0f, Troop(PassiveEffectType.TroopDamage, TroopClassMask.Ranged), 0.001f);
        Assert.AreEqual(0f, Troop(PassiveEffectType.TroopDamage, TroopClassMask.Cavalry), 0.001f);
    }

    [TestMethod]
    public void GetTroopMagnitude_AllClassPip_AppliesToEveryTroopIncludingOneWithNoClass()
    {
        GivenChoice("wb_all", new PassiveEffect(PassiveEffectType.TroopResistance, 0.08f));

        foreach (var troop in new[] { TroopClassMask.Infantry, TroopClassMask.Ranged, TroopClassMask.Cavalry, TroopClassMask.All, TroopClassMask.None })
            Assert.AreEqual(0.08f, Troop(PassiveEffectType.TroopResistance, troop), 0.001f, troop.ToString());
    }

    [TestMethod]
    public void GetTroopMagnitude_TroopWithNoClass_ReceivesOnlyAllClassEffects()
    {
        GivenChoice("wb_mix",
            new PassiveEffect(PassiveEffectType.TroopDamage, 0.10f, troopClass: TroopClassMask.Infantry),
            new PassiveEffect(PassiveEffectType.TroopDamage, 0.02f));

        Assert.AreEqual(0.02f, Troop(PassiveEffectType.TroopDamage, TroopClassMask.None), 0.001f);
    }

    [TestMethod]
    public void GetTroopMagnitude_AllAndClassPips_SumForTheMatchingClass()
    {
        GivenChoice("wb_mix",
            new PassiveEffect(PassiveEffectType.TroopDamage, 0.10f, troopClass: TroopClassMask.Infantry),
            new PassiveEffect(PassiveEffectType.TroopDamage, 0.02f));

        Assert.AreEqual(0.12f, Troop(PassiveEffectType.TroopDamage, TroopClassMask.Infantry), 0.001f);
        Assert.AreEqual(0.02f, Troop(PassiveEffectType.TroopDamage, TroopClassMask.Cavalry), 0.001f);
        // The unfiltered per-type total still sums both, whatever the class.
        Assert.AreEqual(0.12f, _service.GetPassiveMagnitude("hero1", PassiveEffectType.TroopDamage), 0.001f);
    }

    [TestMethod]
    public void GetTroopMagnitude_MultiClassTroop_MatchesAnyOfItsClasses()
    {
        // A horse archer is Ranged | Cavalry: a pip for either class reaches it, one for infantry does not.
        GivenChoice("wb_a", new PassiveEffect(PassiveEffectType.TroopDamage, 0.10f, troopClass: TroopClassMask.Cavalry));
        var horseArcher = TroopClassMask.Ranged | TroopClassMask.Cavalry;

        Assert.AreEqual(0.10f, Troop(PassiveEffectType.TroopDamage, horseArcher), 0.001f);
        Assert.AreEqual(0f, Troop(PassiveEffectType.TroopDamage, TroopClassMask.Infantry), 0.001f);
    }

    [TestMethod]
    public void GetTroopMagnitude_MultiClassPip_ReachesEachOfItsClassesOnce()
    {
        GivenChoice("wb_ir", new PassiveEffect(PassiveEffectType.TroopDamage, 0.10f, troopClass: TroopClassMask.Infantry | TroopClassMask.Ranged));
        var skirmisher = TroopClassMask.Infantry | TroopClassMask.Ranged;

        Assert.AreEqual(0.10f, Troop(PassiveEffectType.TroopDamage, TroopClassMask.Infantry), 0.001f);
        Assert.AreEqual(0.10f, Troop(PassiveEffectType.TroopDamage, TroopClassMask.Ranged), 0.001f);
        Assert.AreEqual(0.10f, Troop(PassiveEffectType.TroopDamage, skirmisher), 0.001f, "one bucket, counted once");
        Assert.AreEqual(0f, Troop(PassiveEffectType.TroopDamage, TroopClassMask.Cavalry), 0.001f);
    }

    [TestMethod]
    public void GetTroopMagnitude_NoHitMask_IgnoresTheAttackTypeMask()
    {
        GivenChoice("wb_ward", new PassiveEffect(PassiveEffectType.TroopResistance, 0.20f, attackTypeMask: AttackTypeMask.Ranged));

        Assert.AreEqual(0.20f, Troop(PassiveEffectType.TroopResistance, TroopClassMask.Infantry), 0.001f);
    }

    [TestMethod]
    public void GetTroopMagnitude_NoHitMask_StillSkipsAnUnparseableMaskPip()
    {
        // The loader lands an unknown attack_type_mask on None and warns "the pip is inert". No hit matches None,
        // so a reader with no hit (the raid speed, the console) must skip it too, or the pip is inert per blow
        // and live everywhere else.
        GivenChoice("wb_bad",
            new PassiveEffect(PassiveEffectType.TroopDamage, 0.10f, attackTypeMask: AttackTypeMask.None),
            new PassiveEffect(PassiveEffectType.TroopDamage, 0.02f));

        Assert.AreEqual(0.02f, Troop(PassiveEffectType.TroopDamage, TroopClassMask.Infantry), 0.001f);
        Assert.AreEqual(0.02f, Troop(PassiveEffectType.TroopDamage, TroopClassMask.None), 0.001f);
    }

    [TestMethod]
    public void GetTroopMagnitude_WithHitMask_HonoursTheAttackTypeMask_TheArrowWard()
    {
        // "Infantry shrug arrows": a Ranged-masked, Infantry-classed TroopResistance reaches an infantryman
        // struck by an arrow and nothing else.
        GivenChoice("wb_ward", new PassiveEffect(PassiveEffectType.TroopResistance, 0.20f,
            attackTypeMask: AttackTypeMask.Ranged, troopClass: TroopClassMask.Infantry));
        var arrow = AttackTypeMask.Ranged | AttackTypeMask.Pierce;
        var sword = AttackTypeMask.Melee | AttackTypeMask.Cut;

        Assert.AreEqual(0.20f, Troop(PassiveEffectType.TroopResistance, TroopClassMask.Infantry, arrow), 0.001f);
        Assert.AreEqual(0f, Troop(PassiveEffectType.TroopResistance, TroopClassMask.Infantry, sword), 0.001f);
        Assert.AreEqual(0f, Troop(PassiveEffectType.TroopResistance, TroopClassMask.Cavalry, arrow), 0.001f);
    }

    [TestMethod]
    public void GetTroopMagnitude_UnmaskedShippedStylePip_IsUnchangedByAHitMask()
    {
        // Shipped troop pips carry no attack_type_mask (All), so honouring the hit mask changes nothing for them.
        GivenChoice("wb_flat", new PassiveEffect(PassiveEffectType.TroopDamage, 0.07f));

        Assert.AreEqual(0.07f, Troop(PassiveEffectType.TroopDamage, TroopClassMask.Infantry, AttackTypeMask.Melee | AttackTypeMask.Cut), 0.001f);
        Assert.AreEqual(0.07f, Troop(PassiveEffectType.TroopDamage, TroopClassMask.Infantry, AttackTypeMask.Ranged | AttackTypeMask.Pierce), 0.001f);
    }

    [TestMethod]
    public void GetMaskedMagnitude_ClassFilteredPip_StillCountsForAnyClass()
    {
        // The hero-facing readers (Damage / Resistance) have no troop class; they keep their meaning.
        GivenChoice("wb_inf", new PassiveEffect(PassiveEffectType.Damage, 0.10f, troopClass: TroopClassMask.Infantry));

        Assert.AreEqual(0.10f, _service.GetMaskedMagnitude("hero1", PassiveEffectType.Damage, AttackTypeMask.Melee), 0.001f);
    }

    // ── army-wide readers of a class-filtered type (TaomRaidModel's raid speed, any future ApplyFactor/ApplyFlat) ──

    [TestMethod]
    public void ApplyFactor_ClassFilteredType_ReadsTheArmyWidePipsOnly()
    {
        // A class-only pip ("+10% infantry damage") must not speed a raid at full magnitude: a raid has no
        // troop class, so it reads what every troop gets, the All-class pips.
        GivenChoice("wb_mix",
            new PassiveEffect(PassiveEffectType.TroopDamage, 0.05f),
            new PassiveEffect(PassiveEffectType.TroopDamage, 0.10f, troopClass: TroopClassMask.Infantry));

        var result = new ExplainedNumber(100f);
        _service.ApplyFactor("hero1", ref result, PassiveEffectType.TroopDamage);

        Assert.AreEqual(105f, result.ResultNumber, 0.01f);
    }

    [TestMethod]
    public void ApplyFactor_ClassFilteredType_WithOnlyAClassPip_AddsNothing()
    {
        GivenChoice("wb_inf", new PassiveEffect(PassiveEffectType.TroopDamage, 0.10f, troopClass: TroopClassMask.Infantry));

        var result = new ExplainedNumber(100f);
        _service.ApplyFactor("hero1", ref result, PassiveEffectType.TroopDamage);

        Assert.AreEqual(100f, result.ResultNumber, 0.01f);
    }

    [TestMethod]
    public void ApplyFlat_ClassFilteredType_ReadsTheArmyWidePipsOnly()
    {
        GivenChoice("wb_mix",
            new PassiveEffect(PassiveEffectType.TroopResistance, 4f),
            new PassiveEffect(PassiveEffectType.TroopResistance, 9f, troopClass: TroopClassMask.Cavalry));

        var result = new ExplainedNumber(100f);
        _service.ApplyFlat("hero1", ref result, PassiveEffectType.TroopResistance);

        Assert.AreEqual(104f, result.ResultNumber, 0.01f);
    }

    [TestMethod]
    public void GetPassiveMagnitude_ClassFilteredType_StaysTheUnfilteredTotal()
    {
        // The unfiltered reader is unchanged: the career screen and taom.career_perks total every pip.
        GivenChoice("wb_mix",
            new PassiveEffect(PassiveEffectType.TroopDamage, 0.05f),
            new PassiveEffect(PassiveEffectType.TroopDamage, 0.10f, troopClass: TroopClassMask.Infantry));

        Assert.AreEqual(0.15f, _service.GetPassiveMagnitude("hero1", PassiveEffectType.TroopDamage), 0.001f);
    }

    [TestMethod]
    public void GetTroopMagnitude_UnknownHeroTypeOrNullId_ReturnsZero()
    {
        GivenChoice("wb_inf", new PassiveEffect(PassiveEffectType.TroopDamage, 0.10f));

        Assert.AreEqual(0f, _service.GetTroopMagnitude(null, PassiveEffectType.TroopDamage, TroopClassMask.Infantry));
        Assert.AreEqual(0f, _service.GetTroopMagnitude("", PassiveEffectType.TroopDamage, TroopClassMask.Infantry));
        Assert.AreEqual(0f, _service.GetTroopMagnitude("nobody", PassiveEffectType.TroopDamage, TroopClassMask.Infantry));
        Assert.AreEqual(0f, Troop(PassiveEffectType.TroopResistance, TroopClassMask.Infantry));
    }

    [TestMethod]
    public void GetTroopMagnitude_TakenChoiceAndRootBothAccumulate()
    {
        _dataService.SetCareer("hero1", "warboss");
        _dataService.TryAddChoice("hero1", "wb_inf", 10);
        _registry.GetChoice("wb_inf").Returns(new CareerChoiceDefinition(
            id: "wb_inf", groupId: "g", type: ChoiceType.Passive, description: "", iconSprite: "",
            passives: new[] { new PassiveEffect(PassiveEffectType.TroopDamage, 0.10f, troopClass: TroopClassMask.Infantry) },
            mutations: null));
        _registry.GetChoice("wb_root").Returns(new CareerChoiceDefinition(
            id: "wb_root", groupId: "", type: ChoiceType.Passive, description: "", iconSprite: "",
            passives: new[] { new PassiveEffect(PassiveEffectType.TroopDamage, 0.05f) },
            mutations: null));
        _service.RefreshCache(_dataService, _registry);

        Assert.AreEqual(0.15f, Troop(PassiveEffectType.TroopDamage, TroopClassMask.Infantry), 0.001f);
        Assert.AreEqual(0.05f, Troop(PassiveEffectType.TroopDamage, TroopClassMask.Cavalry), 0.001f);
    }

    // ── #769 E11: the campaign consumers' service rules ──

    private void GivenPassive(PassiveEffectType type, float magnitude, TroopClassMask troopClass = TroopClassMask.All)
        => GivenChoice("wb_e11", new PassiveEffect(type, magnitude, troopClass: troopClass));

    private int CareerPerkLines(string typeName)
        => _logger.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IModLogger.LogInfo)
            && c.GetArguments()[0] is string text && text.StartsWith("[CareerPerks] " + typeName));

    [DataTestMethod]
    [DataRow(PassiveEffectType.LootGold, 0.20f, 0.5f, 0.6f)]
    [DataRow(PassiveEffectType.RaidLoot, 0.50f, 1.0f, 1.5f)]
    [DataRow(PassiveEffectType.TradePrices, -0.25f, 0.2f, 0.15f)]
    public void ScaleByPassive_HeldPassive_ScalesTheValueByOnePlusTheMagnitude(PassiveEffectType type, float magnitude, float value, float expected)
    {
        GivenPassive(type, magnitude);

        Assert.AreEqual(expected, _service.ScaleByPassive("hero1", value, type), 0.0001f);
    }

    [TestMethod]
    public void ScaleByPassive_NoHeroNoPassiveOrAnotherType_ReturnsTheValueUntouched()
    {
        GivenPassive(PassiveEffectType.LootGold, 0.20f);

        Assert.AreEqual(0.5f, _service.ScaleByPassive(null, 0.5f, PassiveEffectType.LootGold));
        Assert.AreEqual(0.5f, _service.ScaleByPassive("", 0.5f, PassiveEffectType.LootGold));
        Assert.AreEqual(0.5f, _service.ScaleByPassive("nobody", 0.5f, PassiveEffectType.LootGold));
        Assert.AreEqual(0.5f, _service.ScaleByPassive("hero1", 0.5f, PassiveEffectType.RaidLoot));
    }

    [TestMethod]
    public void ScaleByPassive_MagnitudeThatCancelsTheValue_ReturnsZeroNotANegative()
    {
        // -100% is the floor: a trade penalty or a loot share of exactly nothing, never a sign flip.
        GivenPassive(PassiveEffectType.TradePrices, -1f);

        Assert.AreEqual(0f, _service.ScaleByPassive("hero1", 0.2f, PassiveEffectType.TradePrices));
    }

    [DataTestMethod]
    [DataRow(-1.01f)]
    [DataRow(-1.5f)]
    [DataRow(-3f)]
    public void ScaleByPassive_MagnitudeBelowMinusOne_FloorsAtZeroNotADiscountBelowBase(float magnitude)
    {
        // A scale below zero would turn a trade penalty into a discount below the base price (buy under base,
        // sell over it). There is no cliff at -1: a total of -1.01 is the same full cut as -1, never the full
        // penalty back. The loader rejects a NaN or infinite magnitude (CareerConfigProviderTests), so the
        // service never sees one.
        GivenPassive(PassiveEffectType.TradePrices, magnitude);

        var scaled = _service.ScaleByPassive("hero1", 0.2f, PassiveEffectType.TradePrices);

        Assert.AreEqual(0f, scaled);
    }

    [TestMethod]
    public void ScaleByPassive_EventScopedType_LogsOneLineWithTheValueBeforeAndAfter()
    {
        GivenPassive(PassiveEffectType.LootGold, 0.20f);

        _service.ScaleByPassive("hero1", 0.5f, PassiveEffectType.LootGold);

        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.StartsWith("[CareerPerks] LootGold +20% for 'hero1': 0.5 -> 0.6")));
    }

    [TestMethod]
    public void ScaleByPassive_ValueThatDoesNotMove_LogsNoZeroToZeroLine()
    {
        GivenPassive(PassiveEffectType.LootGold, 0.20f);

        _service.ScaleByPassive("hero1", 0f, PassiveEffectType.LootGold);

        Assert.AreEqual(0, CareerPerkLines("LootGold"));
    }

    [TestMethod]
    public void ScaleByPassive_PerTickType_NeverLogs()
    {
        // The raid step and every price quote call this many times a second.
        GivenPassive(PassiveEffectType.RaidLoot, 0.50f);
        GivenChoice("wb_e11b", new PassiveEffect(PassiveEffectType.TradePrices, -0.20f));

        _service.ScaleByPassive("hero1", 1f, PassiveEffectType.RaidLoot);
        _service.ScaleByPassive("hero1", 0.2f, PassiveEffectType.TradePrices);

        Assert.AreEqual(0, CareerPerkLines("RaidLoot"));
        Assert.AreEqual(0, CareerPerkLines("TradePrices"));
    }

    [TestMethod]
    public void ApplyFactor_EventScopedTypeOverAZeroBase_LogsNoZeroToZeroLine()
    {
        // Vanilla pays no influence outside a kingdom, so a factor on it moves nothing: no "0 -> 0" in the log.
        GivenPassive(PassiveEffectType.InfluenceGain, 0.20f);

        var none = new ExplainedNumber(0f);
        _service.ApplyFactor("hero1", ref none, PassiveEffectType.InfluenceGain);

        Assert.AreEqual(0f, none.ResultNumber);
        Assert.AreEqual(0, CareerPerkLines("InfluenceGain"));
    }

    [TestMethod]
    public void ApplyFactor_EventScopedTypeOverARealBase_LogsOneLine()
    {
        GivenPassive(PassiveEffectType.InfluenceGain, 0.20f);

        var gain = new ExplainedNumber(5f);
        _service.ApplyFactor("hero1", ref gain, PassiveEffectType.InfluenceGain);

        Assert.AreEqual(6f, gain.ResultNumber, 0.001f);
        Assert.AreEqual(1, CareerPerkLines("InfluenceGain"));
    }

    [TestMethod]
    public void ApplyFlat_ClassPip_AddsOnlyForATroopOfThatClass()
    {
        GivenChoice("wb_train",
            new PassiveEffect(PassiveEffectType.TroopTraining, 4f),
            new PassiveEffect(PassiveEffectType.TroopTraining, 6f, troopClass: TroopClassMask.Infantry));

        var infantry = new ExplainedNumber(0f);
        _service.ApplyFlat("hero1", ref infantry, PassiveEffectType.TroopTraining, TroopClassMask.Infantry);
        var cavalry = new ExplainedNumber(0f);
        _service.ApplyFlat("hero1", ref cavalry, PassiveEffectType.TroopTraining, TroopClassMask.Cavalry);
        var classless = new ExplainedNumber(0f);
        _service.ApplyFlat("hero1", ref classless, PassiveEffectType.TroopTraining, TroopClassMask.None);

        Assert.AreEqual(10f, infantry.ResultNumber, 0.001f, "the All pip plus the infantry pip");
        Assert.AreEqual(4f, cavalry.ResultNumber, 0.001f, "only the All pip");
        Assert.AreEqual(4f, classless.ResultNumber, 0.001f, "a troop with no class gets the All pip and nothing narrower");
    }

    [TestMethod]
    public void ApplyFlat_ClassFilteredTypeAfterAFactor_IsStillWorthItsLiteralCount()
    {
        // Same frame rule as ApplyFlat: the vanilla training perks add factors, and a "+5 experience" pip is worth 5.
        GivenPassive(PassiveEffectType.TroopTraining, 5f);
        var result = new ExplainedNumber(10f);
        result.AddFactor(1f);

        _service.ApplyFlat("hero1", ref result, PassiveEffectType.TroopTraining, TroopClassMask.Infantry);

        Assert.AreEqual(25f, result.ResultNumber, 0.001f);
    }

    [TestMethod]
    public void ApplyFlat_ClassFilteredTypeForNoHeroNoPassiveOrAnotherHero_IsNoOp()
    {
        GivenPassive(PassiveEffectType.TroopTraining, 5f);
        var result = new ExplainedNumber(3f);

        _service.ApplyFlat(null, ref result, PassiveEffectType.TroopTraining, TroopClassMask.Infantry);
        _service.ApplyFlat("", ref result, PassiveEffectType.TroopTraining, TroopClassMask.Infantry);
        _service.ApplyFlat("nobody", ref result, PassiveEffectType.TroopTraining, TroopClassMask.Infantry);
        _service.ApplyFlat("hero1", ref result, PassiveEffectType.DailyGold, TroopClassMask.Infantry);

        Assert.AreEqual(3f, result.ResultNumber, 0.001f);
    }

    [TestMethod]
    public void ApplyWoundRecovery_PositiveHealing_IsRaisedByTheFactor()
    {
        GivenPassive(PassiveEffectType.WoundRecovery, 0.25f);
        var healing = new ExplainedNumber(20f);

        _service.ApplyWoundRecovery("hero1", isPrisoner: false, ref healing);

        Assert.AreEqual(25f, healing.ResultNumber, 0.001f);
    }

    [TestMethod]
    public void ApplyWoundRecovery_StarvingOrRaftNegativeValue_IsNotMadeWorse()
    {
        // Vanilla returns a NEGATIVE value for a starving party (-regulars/4) and a raft; a plus factor on it
        // would wound the troops faster. The factor only ever speeds up a recovery.
        GivenPassive(PassiveEffectType.WoundRecovery, 0.25f);
        var starving = new ExplainedNumber(0f);
        starving.Add(-12f);
        var zero = new ExplainedNumber(0f);

        _service.ApplyWoundRecovery("hero1", isPrisoner: false, ref starving);
        _service.ApplyWoundRecovery("hero1", isPrisoner: false, ref zero);

        Assert.AreEqual(-12f, starving.ResultNumber, 0.001f);
        Assert.AreEqual(0f, zero.ResultNumber, 0.001f);
    }

    [TestMethod]
    public void ApplyWoundRecovery_PrisonerPath_IsNeverTouched()
    {
        // isPrisoner true is the CAPTOR healing its prisoners (vanilla's constant +1): not the hero's own troops.
        GivenPassive(PassiveEffectType.WoundRecovery, 0.50f);
        var healing = new ExplainedNumber(1f);

        _service.ApplyWoundRecovery("hero1", isPrisoner: true, ref healing);

        Assert.AreEqual(1f, healing.ResultNumber, 0.001f);
    }

    [TestMethod]
    public void ApplyWoundRecovery_NoHeroOrNoPassive_IsNoOp()
    {
        GivenPassive(PassiveEffectType.WoundRecovery, 0.50f);
        var healing = new ExplainedNumber(10f);

        _service.ApplyWoundRecovery(null, isPrisoner: false, ref healing);
        _service.ApplyWoundRecovery("nobody", isPrisoner: false, ref healing);

        Assert.AreEqual(10f, healing.ResultNumber, 0.001f);
    }

    [DataTestMethod]
    [DataRow(-1f, 0f)]
    [DataRow(-1.5f, 0f)]
    [DataRow(-3f, 0f)]
    [DataRow(-0.5f, 10f)]
    public void ApplyWoundRecovery_TotalOfMinusOneOrLess_NeverTurnsPositiveHealingNegative(float magnitude, float expected)
    {
        // A fraction total is not range-checked at the loader, and vanilla puts no lower limit on the number: a
        // total below -1 would turn a positive recovery into a daily wound. The factor is floored at -1 (no recovery).
        GivenPassive(PassiveEffectType.WoundRecovery, magnitude);
        var healing = new ExplainedNumber(20f);

        _service.ApplyWoundRecovery("hero1", isPrisoner: false, ref healing);

        Assert.AreEqual(expected, healing.ResultNumber, 0.001f);
    }

    [TestMethod]
    public void ApplyWoundRecovery_NaNHealing_AddsNoFactor()
    {
        // The gate is written positive so NaN fails it: a factor on a NaN base would only spread the NaN.
        GivenPassive(PassiveEffectType.WoundRecovery, 0.50f);
        var healing = new ExplainedNumber(float.NaN);

        _service.ApplyWoundRecovery("hero1", isPrisoner: false, ref healing);

        Assert.AreEqual(0f, healing.SumOfFactors);
    }

    [TestMethod]
    public void ApplyFlat_ClassWithANaNFactorFrame_AddsNothing()
    {
        // The divisor 1 + SumOfFactors is NaN, which the positive gate rejects: the base is left as it was.
        GivenPassive(PassiveEffectType.TroopTraining, 5f);
        var result = new ExplainedNumber(10f);
        result.AddFactor(float.NaN);

        _service.ApplyFlat("hero1", ref result, PassiveEffectType.TroopTraining, TroopClassMask.Infantry);

        Assert.AreEqual(10f, result.BaseNumber);
    }

    [TestMethod]
    public void ApplyFlat_DailyGold_AddsTheCountAfterTheFactors()
    {
        // The clan finance models call ApplyFlat last, outside the enlistment wage line's applyWithdrawals return.
        GivenPassive(PassiveEffectType.DailyGold, 50f);
        var gold = new ExplainedNumber(100f);
        gold.AddFactor(0.5f);

        _service.ApplyFlat("hero1", ref gold, PassiveEffectType.DailyGold);

        Assert.AreEqual(200f, gold.ResultNumber, 0.001f, "100 * 1.5 = 150, plus exactly 50");
    }
}
