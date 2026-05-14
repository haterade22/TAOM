using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.MountAndBlade;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

// Phase 9b — extracts inline business logic out of TaomAgentStatCalculateModel /
// TaomAgentApplyDamageModel into ICareerAgentStatService (gamemodels.md rule 4 —
// no inline branching in override bodies). Closes deferred audit-issue #142.
//
// Note: this service intentionally calls the static CareerAbilityBuffTracker (pragmatic
// acceptance per the task brief). Tests reset the static state via TestInitialize so
// methods that read both passive-cache + buff-tracker can be exercised in isolation.
[TestClass]
public class CareerAgentStatServiceTests
{
    private CareerAgentStatService _sut = null!;
    private ICareerPassiveService _passives = null!;

    [TestInitialize]
    public void Setup()
    {
        _passives = Substitute.For<ICareerPassiveService>();
        _sut = new CareerAgentStatService(_passives);
        // Static state — pre-reset so prior tests don't leak buffs into ours.
        CareerAbilityBuffTracker.ClearAll();
    }

    [TestCleanup]
    public void Teardown()
    {
        // Static state — post-clean so we don't leak into other test classes that
        // construct buffs themselves (e.g., CareerAbilityServiceTests).
        CareerAbilityBuffTracker.ClearAll();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ApplyAgentStatModifiers — UpdateAgentStats logic
    // ──────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void ApplyAgentStatModifiers_NonHumanAgent_IsNoOp()
    {
        var props = new AgentDrivenProperties();
        props.SwingSpeedMultiplier = 1f;
        props.DamageMultiplierBonus = 0f;

        _sut.ApplyAgentStatModifiers(heroId: "hero1", agentIndex: 5, isHuman: false, isHero: false, props);

        Assert.AreEqual(1f, props.SwingSpeedMultiplier);
        Assert.AreEqual(0f, props.DamageMultiplierBonus);
        _passives.DidNotReceiveWithAnyArgs().GetPassiveMagnitude(default!, default);
    }

    [TestMethod]
    public void ApplyAgentStatModifiers_HumanNonHeroWithoutAllyBuff_IsNoOp()
    {
        var props = new AgentDrivenProperties();
        props.DamageMultiplierBonus = 0f;

        _sut.ApplyAgentStatModifiers(heroId: null, agentIndex: 5, isHuman: true, isHero: false, props);

        Assert.AreEqual(0f, props.DamageMultiplierBonus);
    }

    [TestMethod]
    public void ApplyAgentStatModifiers_HeroWithSwingSpeedPassive_AddsToProps()
    {
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.SwingSpeed).Returns(0.10f);

        var props = new AgentDrivenProperties();
        props.SwingSpeedMultiplier = 1.0f;

        _sut.ApplyAgentStatModifiers("hero1", agentIndex: 5, isHuman: true, isHero: true, props);

        Assert.AreEqual(1.10f, props.SwingSpeedMultiplier, 0.001f);
    }

    [TestMethod]
    public void ApplyAgentStatModifiers_HeroWithDamageAndMovementSpeedPassives_AddsBoth()
    {
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.Damage).Returns(0.20f);
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.MovementSpeed).Returns(0.05f);

        var props = new AgentDrivenProperties();
        props.DamageMultiplierBonus = 0f;
        props.MaxSpeedMultiplier = 1f;

        _sut.ApplyAgentStatModifiers("hero1", agentIndex: 1, isHuman: true, isHero: true, props);

        Assert.AreEqual(0.20f, props.DamageMultiplierBonus, 0.001f);
        Assert.AreEqual(1.05f, props.MaxSpeedMultiplier, 0.001f);
    }

    [TestMethod]
    public void ApplyAgentStatModifiers_HeroWithActiveSelfBuff_AppliesAllBuffFields()
    {
        CareerAbilityBuffTracker.SetBuff("hero1", new ActiveBuffs
        {
            SpeedMultiplier = 0.1f,
            CombatSpeedMultiplier = 0.05f,
            DamageBonus = 0.15f,
            ArmorReduction = 0.20f,
            DrawSpeedBonus = 0.08f,
            MountSpeedBonus = 0.25f,
            ChargeDamageBonus = 0.30f,
        });

        var props = new AgentDrivenProperties();
        props.MaxSpeedMultiplier = 1f;
        props.CombatMaxSpeedMultiplier = 1f;
        props.DamageMultiplierBonus = 0f;
        props.ArmorEncumbrance = 1f;
        props.ThrustOrRangedReadySpeedMultiplier = 1f;
        props.MountSpeed = 1f;
        props.MountChargeDamage = 100f;

        _sut.ApplyAgentStatModifiers("hero1", agentIndex: 1, isHuman: true, isHero: true, props);

        Assert.AreEqual(1.1f, props.MaxSpeedMultiplier, 0.001f);
        Assert.AreEqual(1.05f, props.CombatMaxSpeedMultiplier, 0.001f);
        Assert.AreEqual(0.15f, props.DamageMultiplierBonus, 0.001f);
        Assert.AreEqual(0.8f, props.ArmorEncumbrance, 0.001f);
        Assert.AreEqual(1.08f, props.ThrustOrRangedReadySpeedMultiplier, 0.001f);
        Assert.AreEqual(1.25f, props.MountSpeed, 0.001f);
        Assert.AreEqual(130f, props.MountChargeDamage, 0.01f);
    }

    [TestMethod]
    public void ApplyAgentStatModifiers_HeroWithZeroMountSpeedBonus_DoesNotMutateMountSpeed()
    {
        // Mount stats only mutate when buff value is non-zero — preserve vanilla 0f.
        CareerAbilityBuffTracker.SetBuff("hero1", new ActiveBuffs
        {
            MountSpeedBonus = 0f,
            ChargeDamageBonus = 0f,
        });

        var props = new AgentDrivenProperties();
        props.MountSpeed = 7f;
        props.MountChargeDamage = 50f;

        _sut.ApplyAgentStatModifiers("hero1", agentIndex: 1, isHuman: true, isHero: true, props);

        Assert.AreEqual(7f, props.MountSpeed);
        Assert.AreEqual(50f, props.MountChargeDamage);
    }

    [TestMethod]
    public void ApplyAgentStatModifiers_HumanAgentWithAllyBuff_AppliesAllyFields()
    {
        // AoE ally buff — applied to ALL humans (including non-heroes) by Infantry ability.
        CareerAbilityBuffTracker.SetAllyBuff(42, new ActiveBuffs
        {
            DamageBonus = 0.10f,
            SpeedMultiplier = 0.05f,
            CombatSpeedMultiplier = 0.04f,
            DrawSpeedBonus = 0.02f,
            MountSpeedBonus = 0.15f,
            ChargeDamageBonus = 0.20f,
        });

        var props = new AgentDrivenProperties();
        props.DamageMultiplierBonus = 0f;
        props.MaxSpeedMultiplier = 1f;
        props.CombatMaxSpeedMultiplier = 1f;
        props.ThrustOrRangedReadySpeedMultiplier = 1f;
        props.MountSpeed = 1f;
        props.MountChargeDamage = 100f;

        _sut.ApplyAgentStatModifiers(heroId: null, agentIndex: 42, isHuman: true, isHero: false, props);

        Assert.AreEqual(0.10f, props.DamageMultiplierBonus, 0.001f);
        Assert.AreEqual(1.05f, props.MaxSpeedMultiplier, 0.001f);
        Assert.AreEqual(1.04f, props.CombatMaxSpeedMultiplier, 0.001f);
        Assert.AreEqual(1.02f, props.ThrustOrRangedReadySpeedMultiplier, 0.001f);
        Assert.AreEqual(1.15f, props.MountSpeed, 0.001f);
        Assert.AreEqual(120f, props.MountChargeDamage, 0.01f);
    }

    [TestMethod]
    public void ApplyAgentStatModifiers_HeroWithBothSelfBuffAndAllyBuff_StacksBoth()
    {
        // The original UpdateAgentStats applies hero self-buff THEN the AoE ally-buff
        // unconditionally (so a hero with an ally-buff entry stacks both).
        CareerAbilityBuffTracker.SetBuff("hero1", new ActiveBuffs { DamageBonus = 0.10f });
        CareerAbilityBuffTracker.SetAllyBuff(7, new ActiveBuffs { DamageBonus = 0.05f });

        var props = new AgentDrivenProperties();
        props.DamageMultiplierBonus = 0f;

        _sut.ApplyAgentStatModifiers("hero1", agentIndex: 7, isHuman: true, isHero: true, props);

        Assert.AreEqual(0.15f, props.DamageMultiplierBonus, 0.001f);
    }

    [TestMethod]
    public void ApplyAgentStatModifiers_HeroIdNullButIsHero_SkipsHeroBranchAppliesAllyOnly()
    {
        // Defensive: model's boundary only passes a non-null heroId if it successfully
        // extracted HeroObject.StringId — if null, hero passives should NOT fire.
        CareerAbilityBuffTracker.SetAllyBuff(3, new ActiveBuffs { DamageBonus = 0.25f });

        var props = new AgentDrivenProperties();
        props.DamageMultiplierBonus = 0f;

        _sut.ApplyAgentStatModifiers(heroId: null, agentIndex: 3, isHuman: true, isHero: true, props);

        Assert.AreEqual(0.25f, props.DamageMultiplierBonus, 0.001f);
        _passives.DidNotReceiveWithAnyArgs().GetPassiveMagnitude(default!, default);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // CalculateDamageAmplification — attacker armor-pen passive
    // ──────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void CalculateDamageAmplification_NullAttackerHeroId_ReturnsBaseUnchanged()
    {
        var result = _sut.CalculateDamageAmplification(attackerHeroId: null, baseResult: 100f);
        Assert.AreEqual(100f, result);
        _passives.DidNotReceiveWithAnyArgs().GetPassiveMagnitude(default!, default);
    }

    [TestMethod]
    public void CalculateDamageAmplification_ZeroArmorPen_ReturnsBaseUnchanged()
    {
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.ArmorPenetration).Returns(0f);
        var result = _sut.CalculateDamageAmplification("hero1", baseResult: 100f);
        Assert.AreEqual(100f, result);
    }

    [TestMethod]
    public void CalculateDamageAmplification_NonZeroArmorPen_MultipliesBase()
    {
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.ArmorPenetration).Returns(0.25f);
        var result = _sut.CalculateDamageAmplification("hero1", baseResult: 100f);
        Assert.AreEqual(125f, result, 0.01f);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // CalculateDamageReduction — victim resistance + self-buff + ally-buff
    // ──────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void CalculateDamageReduction_NoHeroAndNoVictim_ReturnsBaseUnchanged()
    {
        var result = _sut.CalculateDamageReduction(victimHeroId: null, victimAgentIndex: null, baseResult: 100f);
        Assert.AreEqual(100f, result);
    }

    [TestMethod]
    public void CalculateDamageReduction_HeroWithResistance_MultipliesByOneMinus()
    {
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.Resistance).Returns(0.30f);
        var result = _sut.CalculateDamageReduction("hero1", victimAgentIndex: null, baseResult: 100f);
        Assert.AreEqual(70f, result, 0.01f);
    }

    [TestMethod]
    public void CalculateDamageReduction_HeroWithSelfBuffReduction_StacksWithResistance()
    {
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.Resistance).Returns(0.10f);
        CareerAbilityBuffTracker.SetBuff("hero1", new ActiveBuffs { DamageReductionBonus = 0.20f });

        var result = _sut.CalculateDamageReduction("hero1", victimAgentIndex: null, baseResult: 100f);

        // 100 * (1-0.10) * (1-0.20) = 100 * 0.9 * 0.8 = 72
        Assert.AreEqual(72f, result, 0.01f);
    }

    [TestMethod]
    public void CalculateDamageReduction_NonHeroAgentWithAllyBuff_AppliesAllyReduction()
    {
        CareerAbilityBuffTracker.SetAllyBuff(99, new ActiveBuffs { DamageReductionBonus = 0.15f });

        var result = _sut.CalculateDamageReduction(victimHeroId: null, victimAgentIndex: 99, baseResult: 100f);

        Assert.AreEqual(85f, result, 0.01f);
    }

    [TestMethod]
    public void CalculateDamageReduction_HeroWithAllyBuffOnSameIndex_StacksBoth()
    {
        // A hero can also have an ally-buff entry (Infantry AoE puts hero in the ally map too).
        // The original code applies BOTH the hero self-buff and the ally-buff for the same agent.
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.Resistance).Returns(0f);
        CareerAbilityBuffTracker.SetBuff("hero1", new ActiveBuffs { DamageReductionBonus = 0.20f });
        CareerAbilityBuffTracker.SetAllyBuff(7, new ActiveBuffs { DamageReductionBonus = 0.10f });

        var result = _sut.CalculateDamageReduction("hero1", victimAgentIndex: 7, baseResult: 100f);

        // 100 * (1-0.20) * (1-0.10) = 100 * 0.8 * 0.9 = 72
        Assert.AreEqual(72f, result, 0.01f);
    }

    [TestMethod]
    public void CalculateDamageReduction_ZeroResistanceAndZeroBuffs_ReturnsBaseUnchanged()
    {
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.Resistance).Returns(0f);
        // No buff, no ally-buff entries.
        var result = _sut.CalculateDamageReduction("hero1", victimAgentIndex: 7, baseResult: 100f);
        Assert.AreEqual(100f, result);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ShouldShrugOffBlow — victim ShruggedOff passive
    // ──────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldShrugOffBlow_NullHeroId_ReturnsFalse()
    {
        Assert.IsFalse(_sut.ShouldShrugOffBlow(victimHeroId: null));
        _passives.DidNotReceiveWithAnyArgs().GetPassiveMagnitude(default!, default);
    }

    [TestMethod]
    public void ShouldShrugOffBlow_ZeroShruggedOffMagnitude_ReturnsFalse()
    {
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.ShruggedOff).Returns(0f);
        Assert.IsFalse(_sut.ShouldShrugOffBlow("hero1"));
    }

    [TestMethod]
    public void ShouldShrugOffBlow_NonZeroShruggedOffMagnitude_ReturnsTrue()
    {
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.ShruggedOff).Returns(0.5f);
        Assert.IsTrue(_sut.ShouldShrugOffBlow("hero1"));
    }

    [TestMethod]
    public void ShouldShrugOffBlow_NegativeMagnitude_ReturnsFalse()
    {
        // shrugOff > 0f gate — negative magnitude does NOT trigger shrug.
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.ShruggedOff).Returns(-0.5f);
        Assert.IsFalse(_sut.ShouldShrugOffBlow("hero1"));
    }
}
