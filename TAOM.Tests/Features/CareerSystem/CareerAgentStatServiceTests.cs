using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
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
[TestCategory("RequiresGame")]
public class CareerAgentStatServiceTests
{
    private CareerAgentStatService _sut = null!;
    private ICareerPassiveService _passives = null!;
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _passives = Substitute.For<ICareerPassiveService>();
        _logger = Substitute.For<IModLogger>();
        _sut = new CareerAgentStatService(_passives, _logger);
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
    public void ApplyAgentStatModifiers_DamagePassive_NotAppliedAsFlatMultiplier()
    {
        // Damage moved to the per-hit amplification path (attack_type_mask honored), so it must
        // NOT be applied as a flat DamageMultiplierBonus during UpdateAgentStats. MovementSpeed
        // still is (it is not attack-typed).
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.Damage).Returns(0.20f);
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.MovementSpeed).Returns(0.05f);

        var props = new AgentDrivenProperties();
        props.DamageMultiplierBonus = 0f;
        props.MaxSpeedMultiplier = 1f;

        _sut.ApplyAgentStatModifiers("hero1", agentIndex: 1, isHuman: true, isHero: true, props);

        Assert.AreEqual(0f, props.DamageMultiplierBonus, 0.001f);
        Assert.AreEqual(1.05f, props.MaxSpeedMultiplier, 0.001f);
    }

    [TestMethod]
    public void ApplyAgentStatModifiers_Human_NeverTouchesTheMountProperties()
    {
        // #611: MountChargeDamage and MountSpeed live on the MOUNT agent's properties (the base
        // writes them only in UpdateHorseStats and the engine reads them off the horse). A rider-side
        // multiply scales a value nobody wrote and nobody reads; it moved to ApplyMountStatModifiers.
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.MountChargeDamage).Returns(0.15f);
        CareerAbilityBuffTracker.SetBuff("hero1", new ActiveBuffs { MountSpeedBonus = 0.25f, ChargeDamageBonus = 0.30f });
        CareerAbilityBuffTracker.SetAllyBuff(1, new ActiveBuffs { MountSpeedBonus = 0.15f, ChargeDamageBonus = 0.20f });

        var props = new AgentDrivenProperties();
        props.MountSpeed = 1f;
        props.MountChargeDamage = 100f;

        _sut.ApplyAgentStatModifiers("hero1", agentIndex: 1, isHuman: true, isHero: true, props);

        Assert.AreEqual(1f, props.MountSpeed, 0.001f);
        Assert.AreEqual(100f, props.MountChargeDamage, 0.01f);
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
        // The two mount fields of the same buff apply on the MOUNT (#611), not here.
        Assert.AreEqual(1f, props.MountSpeed, 0.001f);
        Assert.AreEqual(100f, props.MountChargeDamage, 0.01f);
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
        // The two mount fields of the same buff apply on the MOUNT (#611), not here.
        Assert.AreEqual(1f, props.MountSpeed, 0.001f);
        Assert.AreEqual(100f, props.MountChargeDamage, 0.01f);
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
    // [CareerPerks] logging (#613): the hero's own stat application logs once per distinct set of
    // applied values, the per-hit paths log at DEBUG when a passive moved the number.
    // ──────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void ApplyAgentStatModifiers_HeroWithPassives_LogsOncePerDistinctSetOfValues()
    {
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.SwingSpeed).Returns(0.05f);

        var props = new AgentDrivenProperties();
        _sut.ApplyAgentStatModifiers("hero1", agentIndex: 1, isHuman: true, isHero: true, props);
        _sut.ApplyAgentStatModifiers("hero1", agentIndex: 1, isHuman: true, isHero: true, new AgentDrivenProperties());

        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.StartsWith("[CareerPerks]") && s.Contains("hero1") && s.Contains("SwingSpeed +5%")));
    }

    [TestMethod]
    public void ApplyAgentStatModifiers_ValuesChange_LogsAgain()
    {
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.SwingSpeed).Returns(0.05f);
        _sut.ApplyAgentStatModifiers("hero1", agentIndex: 1, isHuman: true, isHero: true, new AgentDrivenProperties());

        CareerAbilityBuffTracker.SetBuff("hero1", new ActiveBuffs { DamageBonus = 0.15f });
        _sut.ApplyAgentStatModifiers("hero1", agentIndex: 1, isHuman: true, isHero: true, new AgentDrivenProperties());

        _logger.Received(2).LogInfo(Arg.Is<string>(s => s.StartsWith("[CareerPerks]")));
        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("self buff") && s.Contains("dmg +15%")));
    }

    [TestMethod]
    public void ResetDiagnostics_ClearsTheDedupe_SoTheNextSpawnLogsAgain()
    {
        // The service is a singleton; without a reset a second battle with the same values would
        // log nothing at spawn, which is the re-test loop the feature doc recommends.
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.SwingSpeed).Returns(0.05f);
        _sut.ApplyAgentStatModifiers("hero1", agentIndex: 1, isHuman: true, isHero: true, new AgentDrivenProperties());
        _sut.ApplyMountStatModifiers("hero1", riderAgentIndex: 1, new AgentDrivenProperties());

        _sut.ResetDiagnostics();
        _sut.ApplyAgentStatModifiers("hero1", agentIndex: 1, isHuman: true, isHero: true, new AgentDrivenProperties());
        _sut.ApplyMountStatModifiers("hero1", riderAgentIndex: 1, new AgentDrivenProperties());

        _logger.Received(2).LogInfo(Arg.Is<string>(s => s.Contains("agent stats for 'hero1'")));
        _logger.Received(2).LogInfo(Arg.Is<string>(s => s.Contains("mount stats for rider 'hero1'")));
    }

    [TestMethod]
    public void ApplyAgentStatModifiers_NonHeroSoldier_NeverLogs()
    {
        // Every soldier recomputes stats on spawn and on formation orders; only the career hero's
        // own agent is worth a line.
        CareerAbilityBuffTracker.SetAllyBuff(42, new ActiveBuffs { DamageBonus = 0.10f });

        _sut.ApplyAgentStatModifiers(heroId: null, agentIndex: 42, isHuman: true, isHero: false, new AgentDrivenProperties());

        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void ApplyMountStatModifiers_HeroRider_LogsOncePerDistinctSetOfValues()
    {
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.MountChargeDamage).Returns(0.15f);

        _sut.ApplyMountStatModifiers("hero1", riderAgentIndex: 1, new AgentDrivenProperties { MountChargeDamage = 1f });
        _sut.ApplyMountStatModifiers("hero1", riderAgentIndex: 1, new AgentDrivenProperties { MountChargeDamage = 1f });

        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.StartsWith("[CareerPerks]") && s.Contains("mount") && s.Contains("MountChargeDamage +15%")));
    }

    [TestMethod]
    public void CalculateDamageAmplification_PassiveMovedTheNumber_LogsDebugWithBaseAndResult()
    {
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.ArmorPenetration).Returns(0.10f);

        var result = _sut.CalculateDamageAmplification("hero1", null, AttackTypeMask.Melee | AttackTypeMask.Cut, 50f);

        Assert.AreEqual(55f, result, 0.01f);
        _logger.Received(1).LogDebug(Arg.Is<string>(s => s.StartsWith("[CareerPerks]") && s.Contains("hero1") && s.Contains("50.0") && s.Contains("55.0") && s.Contains("ArmorPenetration")));
    }

    [TestMethod]
    public void CalculateDamageReduction_PassiveMovedTheNumber_LogsDebug()
    {
        _passives.GetMaskedMagnitude("hero1", PassiveEffectType.Resistance, AttackTypeMask.Melee | AttackTypeMask.Blunt).Returns(0.10f);

        var result = _sut.CalculateDamageReduction("hero1", null, null, AttackTypeMask.Melee | AttackTypeMask.Blunt, 40f);

        Assert.AreEqual(36f, result, 0.01f);
        _logger.Received(1).LogDebug(Arg.Is<string>(s => s.StartsWith("[CareerPerks]") && s.Contains("Resistance") && s.Contains("Blunt")));
    }

    [TestMethod]
    public void CalculateDamageAmplification_NothingApplied_DoesNotLog()
    {
        _sut.CalculateDamageAmplification(null, null, AttackTypeMask.Melee, 50f);
        _sut.CalculateDamageAmplification("hero1", null, AttackTypeMask.Melee, 50f);

        _logger.DidNotReceive().LogDebug(Arg.Any<string>());
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ApplyMountStatModifiers (#611): the rider's MountChargeDamage passive and the Cavalry
    // ability's two mount fields, applied on the MOUNT's properties via the rider's ids.
    // ──────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void ApplyMountStatModifiers_RiderHeroWithMountChargeDamagePassive_ScalesTheMountsChargeDamage()
    {
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.MountChargeDamage).Returns(0.15f);

        var mount = new AgentDrivenProperties();
        mount.MountChargeDamage = 100f;
        mount.MountSpeed = 1f;

        _sut.ApplyMountStatModifiers(riderHeroId: "hero1", riderAgentIndex: 1, mount);

        Assert.AreEqual(115f, mount.MountChargeDamage, 0.01f);
        Assert.AreEqual(1f, mount.MountSpeed, 0.001f);
    }

    [TestMethod]
    public void ApplyMountStatModifiers_RiderHeroWithZeroPassiveAndNoBuff_LeavesTheMountUntouched()
    {
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.MountChargeDamage).Returns(0f);

        var mount = new AgentDrivenProperties();
        mount.MountChargeDamage = 80f;
        mount.MountSpeed = 7f;

        _sut.ApplyMountStatModifiers(riderHeroId: "hero1", riderAgentIndex: 1, mount);

        Assert.AreEqual(80f, mount.MountChargeDamage, 0.01f);
        Assert.AreEqual(7f, mount.MountSpeed, 0.001f);
    }

    [TestMethod]
    public void ApplyMountStatModifiers_RiderSelfBuff_ScalesMountSpeedAndChargeMultiplicatively()
    {
        CareerAbilityBuffTracker.SetBuff("hero1", new ActiveBuffs { MountSpeedBonus = 0.25f, ChargeDamageBonus = 0.30f });

        var mount = new AgentDrivenProperties();
        mount.MountSpeed = 1f;
        mount.MountChargeDamage = 100f;

        _sut.ApplyMountStatModifiers(riderHeroId: "hero1", riderAgentIndex: 1, mount);

        Assert.AreEqual(1.25f, mount.MountSpeed, 0.001f);
        Assert.AreEqual(130f, mount.MountChargeDamage, 0.01f);
    }

    [TestMethod]
    public void ApplyMountStatModifiers_NonHeroRiderWithAllyBuff_AppliesTheAllyFieldsByRiderIndex()
    {
        CareerAbilityBuffTracker.SetAllyBuff(42, new ActiveBuffs { MountSpeedBonus = 0.15f, ChargeDamageBonus = 0.20f });

        var mount = new AgentDrivenProperties();
        mount.MountSpeed = 1f;
        mount.MountChargeDamage = 100f;

        _sut.ApplyMountStatModifiers(riderHeroId: null, riderAgentIndex: 42, mount);

        Assert.AreEqual(1.15f, mount.MountSpeed, 0.001f);
        Assert.AreEqual(120f, mount.MountChargeDamage, 0.01f);
        _passives.DidNotReceiveWithAnyArgs().GetPassiveMagnitude(default!, default);
    }

    [TestMethod]
    public void ApplyMountStatModifiers_PassiveSelfBuffAndAllyBuff_StackMultiplicatively()
    {
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.MountChargeDamage).Returns(0.10f);
        CareerAbilityBuffTracker.SetBuff("hero1", new ActiveBuffs { ChargeDamageBonus = 0.30f });
        CareerAbilityBuffTracker.SetAllyBuff(7, new ActiveBuffs { ChargeDamageBonus = 0.20f });

        var mount = new AgentDrivenProperties();
        mount.MountChargeDamage = 100f;

        _sut.ApplyMountStatModifiers(riderHeroId: "hero1", riderAgentIndex: 7, mount);

        // 100 x 1.10 x 1.30 x 1.20
        Assert.AreEqual(171.6f, mount.MountChargeDamage, 0.01f);
    }

    [TestMethod]
    public void ApplyMountStatModifiers_HeroRiderWithoutIndex_AppliesPassiveAndSelfBuffOnly()
    {
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.MountChargeDamage).Returns(0.10f);
        CareerAbilityBuffTracker.SetBuff("hero1", new ActiveBuffs { ChargeDamageBonus = 0.30f });
        CareerAbilityBuffTracker.SetAllyBuff(7, new ActiveBuffs { ChargeDamageBonus = 0.20f });

        var mount = new AgentDrivenProperties();
        mount.MountChargeDamage = 100f;

        _sut.ApplyMountStatModifiers(riderHeroId: "hero1", riderAgentIndex: null, mount);

        // 100 x 1.10 x 1.30; the ally entry under index 7 is never consulted without an index.
        Assert.AreEqual(143f, mount.MountChargeDamage, 0.01f);
    }

    [TestMethod]
    public void ApplyMountStatModifiers_NegativeBonus_ScalesDown()
    {
        // A malus is a legal tuning value: 1 + (-0.25) = 0.75.
        CareerAbilityBuffTracker.SetBuff("hero1", new ActiveBuffs { MountSpeedBonus = -0.25f, ChargeDamageBonus = -0.5f });

        var mount = new AgentDrivenProperties();
        mount.MountSpeed = 4f;
        mount.MountChargeDamage = 100f;

        _sut.ApplyMountStatModifiers(riderHeroId: "hero1", riderAgentIndex: null, mount);

        Assert.AreEqual(3f, mount.MountSpeed, 0.001f);
        Assert.AreEqual(50f, mount.MountChargeDamage, 0.01f);
    }

    [TestMethod]
    public void ApplyMountStatModifiers_NoRider_IsNoOpAndDoesNotQuery()
    {
        // A riderless horse, or a human agent (the model passes null for both ids off a non-mount).
        CareerAbilityBuffTracker.SetBuff("hero1", new ActiveBuffs { ChargeDamageBonus = 0.30f });

        var mount = new AgentDrivenProperties();
        mount.MountSpeed = 3f;
        mount.MountChargeDamage = 100f;

        _sut.ApplyMountStatModifiers(riderHeroId: null, riderAgentIndex: null, mount);

        Assert.AreEqual(3f, mount.MountSpeed, 0.001f);
        Assert.AreEqual(100f, mount.MountChargeDamage, 0.01f);
        _passives.DidNotReceiveWithAnyArgs().GetPassiveMagnitude(default!, default);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // MountChargeMultiplier: the rider's charge product as a query. The elk's antler charge reads it when it
    // fires (#636, Mike 2026-09-23: "scale the antler blows too"); ApplyMountStatModifiers applies the same
    // product to the mount's MountChargeDamage, so the body charge and the antler blow agree for any product the
    // antler accepts, (0, 10].
    // ──────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void MountChargeMultiplier_NoRider_IsOneAndDoesNotQuery()
    {
        CareerAbilityBuffTracker.SetBuff("hero1", new ActiveBuffs { ChargeDamageBonus = 0.30f });

        Assert.AreEqual(1f, _sut.MountChargeMultiplier(riderHeroId: null, riderAgentIndex: null), 1e-6f);
        _passives.DidNotReceiveWithAnyArgs().GetPassiveMagnitude(default!, default);
    }

    [TestMethod]
    public void MountChargeMultiplier_HeroRiderWithPassive_IsOnePlusThePassive()
    {
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.MountChargeDamage).Returns(0.15f);

        Assert.AreEqual(1.15f, _sut.MountChargeMultiplier(riderHeroId: "hero1", riderAgentIndex: 1), 1e-5f);
    }

    [TestMethod]
    public void MountChargeMultiplier_HeroRiderWithSelfBuff_TakesTheChargeBonusOnly()
    {
        // Antler Crash: +25% charge and +20% mount speed; only the charge half scales a charge.
        CareerAbilityBuffTracker.SetBuff("hero1", new ActiveBuffs { ChargeDamageBonus = 0.25f, MountSpeedBonus = 0.20f });

        Assert.AreEqual(1.25f, _sut.MountChargeMultiplier(riderHeroId: "hero1", riderAgentIndex: 1), 1e-5f);
    }

    [TestMethod]
    public void MountChargeMultiplier_NonHeroRiderWithAllyBuff_ReadsTheBuffByRiderIndex()
    {
        CareerAbilityBuffTracker.SetAllyBuff(42, new ActiveBuffs { ChargeDamageBonus = 0.20f });

        Assert.AreEqual(1.20f, _sut.MountChargeMultiplier(riderHeroId: null, riderAgentIndex: 42), 1e-5f);
        _passives.DidNotReceiveWithAnyArgs().GetPassiveMagnitude(default!, default);
    }

    [TestMethod]
    public void MountChargeMultiplier_PassiveSelfBuffAndAllyBuff_StackMultiplicatively()
    {
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.MountChargeDamage).Returns(0.10f);
        CareerAbilityBuffTracker.SetBuff("hero1", new ActiveBuffs { ChargeDamageBonus = 0.30f });
        CareerAbilityBuffTracker.SetAllyBuff(7, new ActiveBuffs { ChargeDamageBonus = 0.20f });

        // 1.10 x 1.30 x 1.20
        Assert.AreEqual(1.716f, _sut.MountChargeMultiplier(riderHeroId: "hero1", riderAgentIndex: 7), 1e-5f);
    }

    [TestMethod]
    public void MountChargeMultiplier_HeroRiderWithoutIndex_SkipsTheAllyBuff()
    {
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.MountChargeDamage).Returns(0.10f);
        CareerAbilityBuffTracker.SetBuff("hero1", new ActiveBuffs { ChargeDamageBonus = 0.30f });
        CareerAbilityBuffTracker.SetAllyBuff(7, new ActiveBuffs { ChargeDamageBonus = 0.20f });

        // 1.10 x 1.30; the ally entry under index 7 is never consulted without an index.
        Assert.AreEqual(1.43f, _sut.MountChargeMultiplier(riderHeroId: "hero1", riderAgentIndex: null), 1e-5f);
    }

    [TestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void MountChargeMultiplier_NonFinitePassive_IsOne(float passive)
    {
        // The loader rejects NaN and infinity, but a huge finite product can overflow. A non-finite product must
        // poison neither the mount's charge nor the elk's antler blow; it scales nothing.
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.MountChargeDamage).Returns(passive);

        Assert.AreEqual(1f, _sut.MountChargeMultiplier(riderHeroId: "hero1", riderAgentIndex: 1), 1e-6f);
    }

    [TestMethod]
    public void ApplyMountStatModifiers_NaNPassive_LeavesTheMountsChargeUnscaled()
    {
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.MountChargeDamage).Returns(float.NaN);
        var mount = new AgentDrivenProperties { MountChargeDamage = 100f };

        _sut.ApplyMountStatModifiers(riderHeroId: "hero1", riderAgentIndex: 1, mount);

        Assert.AreEqual(100f, mount.MountChargeDamage, 0.01f);
    }

    [TestMethod]
    public void MountChargeMultiplier_IsExactlyWhatApplyMountStatModifiersAppliesToTheMount()
    {
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.MountChargeDamage).Returns(0.10f);
        CareerAbilityBuffTracker.SetBuff("hero1", new ActiveBuffs { ChargeDamageBonus = 0.25f });
        CareerAbilityBuffTracker.SetAllyBuff(7, new ActiveBuffs { ChargeDamageBonus = 0.20f });
        var mount = new AgentDrivenProperties { MountChargeDamage = 1f };

        _sut.ApplyMountStatModifiers(riderHeroId: "hero1", riderAgentIndex: 7, mount);

        Assert.AreEqual(mount.MountChargeDamage, _sut.MountChargeMultiplier(riderHeroId: "hero1", riderAgentIndex: 7), 1e-6f);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // AmmoBonus (#613): the magnitude the model hands CareerAmmoApplier at InitializeMissionEquipment.
    // ──────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void AmmoBonus_HeroWithPassive_ReturnsTheMagnitude()
    {
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.Ammo).Returns(0.15f);

        Assert.AreEqual(0.15f, _sut.AmmoBonus("hero1"), 0.0001f);
    }

    [TestMethod]
    public void AmmoBonus_NullHeroOrNegativeMagnitude_ReturnsZeroAndNeverQueriesForNull()
    {
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.Ammo).Returns(-0.2f);

        Assert.AreEqual(0f, _sut.AmmoBonus("hero1"), 0.0001f);
        Assert.AreEqual(0f, _sut.AmmoBonus(null), 0.0001f);
        _passives.DidNotReceive().GetPassiveMagnitude(null!, PassiveEffectType.Ammo);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ApplyMountHealthPassives — mount MountHealth only (multiplicative).
    // The hero `Health` passive lives on TaomCharacterStatsModel.MaxHitpoints (#394); see the
    // double-count pin at the end of this block for why it must never come back here.
    // ──────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void ApplyMountHealthPassives_MountWithHeroRider_ScalesMountHealthMultiplicatively()
    {
        _passives.GetPassiveMagnitude("rider1", PassiveEffectType.MountHealth).Returns(0.15f);

        var result = _sut.ApplyMountHealthPassives(mountRiderHeroId: "rider1", baseHealth: 200f);

        Assert.AreEqual(230f, result, 0.01f);
    }

    [TestMethod]
    public void ApplyMountHealthPassives_MountWithZeroMountHealth_ReturnsBase()
    {
        _passives.GetPassiveMagnitude("rider1", PassiveEffectType.MountHealth).Returns(0f);

        var result = _sut.ApplyMountHealthPassives(mountRiderHeroId: "rider1", baseHealth: 200f);

        Assert.AreEqual(200f, result, 0.01f);
    }

    [TestMethod]
    public void ApplyMountHealthPassives_NoMountRider_ReturnsBaseAndDoesNotQuery()
    {
        var result = _sut.ApplyMountHealthPassives(mountRiderHeroId: null, baseHealth: 100f);

        Assert.AreEqual(100f, result, 0.01f);
        _passives.DidNotReceiveWithAnyArgs().GetPassiveMagnitude(default!, default);
    }

    /// <summary>
    /// #394 double-count pin. SandboxAgentStatCalculateModel.GetEffectiveMaxHealth opens with
    /// `if (agent.IsHero) return agent.Character.MaxHitPoints();`, and CharacterObject.MaxHitPoints()
    /// resolves through TaomCharacterStatsModel — which already adds the Health passive. If this
    /// service ever reads Health again, a +75 pip becomes +150 in battle (100 → 175 → 250).
    /// </summary>
    [TestMethod]
    public void ApplyMountHealthPassives_NeverReadsHeroHealthPassive_SoBattleHealthIsNotDoubleCounted()
    {
        _passives.GetPassiveMagnitude(Arg.Any<string>(), PassiveEffectType.Health).Returns(75f);

        var result = _sut.ApplyMountHealthPassives(mountRiderHeroId: "rider1", baseHealth: 200f);

        Assert.AreEqual(200f, result, 0.01f,
            "Health leaked into the agent-stat path — it is already applied by TaomCharacterStatsModel.");
        _passives.DidNotReceive().GetPassiveMagnitude(Arg.Any<string>(), PassiveEffectType.Health);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // CalculateDamageAmplification — attacker armor-pen + mask-gated Damage passive
    // ──────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void CalculateDamageAmplification_NoAttackerHeroAndNoTroopLeader_ReturnsBaseUnchanged()
    {
        var result = _sut.CalculateDamageAmplification(
            attackerHeroId: null, attackerTroopLeaderHeroId: null, AttackTypeMask.Melee, baseResult: 100f);
        Assert.AreEqual(100f, result);
        _passives.DidNotReceiveWithAnyArgs().GetPassiveMagnitude(default!, default);
        _passives.DidNotReceiveWithAnyArgs().GetMaskedMagnitude(default!, default, default);
    }

    // ── TroopDamage — the offensive mirror of TroopResistance (#395) ──────────
    // The passive belongs to the party LEADER but applies to their non-hero troops' hits. Until
    // #395 the only consumer was TaomRaidModel.CalculateHitDamage — settlement raid progress — so
    // 105 pips promising "+N% troop damage" did nothing in any battle.

    [TestMethod]
    public void CalculateDamageAmplification_NonHeroTroopWhoseLeaderHasTroopDamage_MultipliesBase()
    {
        _passives.GetPassiveMagnitude("leader1", PassiveEffectType.TroopDamage).Returns(0.05f);

        var result = _sut.CalculateDamageAmplification(
            attackerHeroId: null, attackerTroopLeaderHeroId: "leader1", AttackTypeMask.Melee, baseResult: 100f);

        Assert.AreEqual(105f, result, 0.01f);
    }

    [TestMethod]
    public void CalculateDamageAmplification_TroopDamageIsNotMaskGated_AppliesOnRangedHitToo()
    {
        // Unlike the hero Damage passive, TroopDamage carries no attack_type_mask in the shipped
        // XML — it is a flat army-wide multiplier, so a ranged hit gets it just like a melee one.
        _passives.GetPassiveMagnitude("leader1", PassiveEffectType.TroopDamage).Returns(0.08f);

        var result = _sut.CalculateDamageAmplification(
            attackerHeroId: null, attackerTroopLeaderHeroId: "leader1", AttackTypeMask.Ranged, baseResult: 100f);

        Assert.AreEqual(108f, result, 0.01f);
    }

    [TestMethod]
    public void CalculateDamageAmplification_ZeroTroopDamage_ReturnsBaseUnchanged()
    {
        _passives.GetPassiveMagnitude("leader1", PassiveEffectType.TroopDamage).Returns(0f);

        var result = _sut.CalculateDamageAmplification(
            attackerHeroId: null, attackerTroopLeaderHeroId: "leader1", AttackTypeMask.Melee, baseResult: 100f);

        Assert.AreEqual(100f, result, 0.01f);
    }

    [TestMethod]
    public void CalculateDamageAmplification_HeroAttacker_DoesNotAlsoTakeTroopDamage()
    {
        // The boundary returns null for a hero attacker's troop-leader id (a hero's own hits are
        // covered by the Damage pip), so the two never stack on one blow. Pinned because the
        // mirror-image bug — a leader's own swings getting both — would be invisible in play.
        _passives.GetMaskedMagnitude("hero1", PassiveEffectType.Damage, AttackTypeMask.Melee).Returns(0.10f);
        _passives.GetPassiveMagnitude(Arg.Any<string>(), PassiveEffectType.TroopDamage).Returns(0.05f);

        var result = _sut.CalculateDamageAmplification(
            attackerHeroId: "hero1", attackerTroopLeaderHeroId: null, AttackTypeMask.Melee, baseResult: 100f);

        Assert.AreEqual(110f, result, 0.01f);
        _passives.DidNotReceive().GetPassiveMagnitude(Arg.Any<string>(), PassiveEffectType.TroopDamage);
    }

    [TestMethod]
    public void CalculateDamageAmplification_ZeroArmorPenAndZeroDamage_ReturnsBaseUnchanged()
    {
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.ArmorPenetration).Returns(0f);
        _passives.GetMaskedMagnitude("hero1", PassiveEffectType.Damage, AttackTypeMask.Melee).Returns(0f);
        var result = _sut.CalculateDamageAmplification("hero1", null, AttackTypeMask.Melee, baseResult: 100f);
        Assert.AreEqual(100f, result);
    }

    [TestMethod]
    public void CalculateDamageAmplification_NonZeroArmorPen_MultipliesBase()
    {
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.ArmorPenetration).Returns(0.25f);
        var result = _sut.CalculateDamageAmplification("hero1", null, AttackTypeMask.Melee, baseResult: 100f);
        Assert.AreEqual(125f, result, 0.01f);
    }

    [TestMethod]
    public void CalculateDamageAmplification_MeleeDamagePassiveOnMeleeHit_MultipliesBase()
    {
        _passives.GetMaskedMagnitude("hero1", PassiveEffectType.Damage, AttackTypeMask.Melee).Returns(0.15f);
        var result = _sut.CalculateDamageAmplification("hero1", null, AttackTypeMask.Melee, baseResult: 100f);
        Assert.AreEqual(115f, result, 0.01f);
    }

    [TestMethod]
    public void CalculateDamageAmplification_RangedHit_OnlyAppliesRangedMaskedDamage()
    {
        // The masked lookup returns 0 for a ranged hit when only a melee Damage pip is held; verify
        // the service forwards the hit mask and does not boost the wrong delivery type.
        _passives.GetMaskedMagnitude("hero1", PassiveEffectType.Damage, AttackTypeMask.Ranged).Returns(0f);
        var result = _sut.CalculateDamageAmplification("hero1", null, AttackTypeMask.Ranged, baseResult: 100f);
        Assert.AreEqual(100f, result, 0.01f);
    }

    [TestMethod]
    public void CalculateDamageAmplification_ArmorPenAndDamageStack_MultiplyTogether()
    {
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.ArmorPenetration).Returns(0.20f);
        _passives.GetMaskedMagnitude("hero1", PassiveEffectType.Damage, AttackTypeMask.Melee).Returns(0.10f);
        var result = _sut.CalculateDamageAmplification("hero1", null, AttackTypeMask.Melee, baseResult: 100f);
        // 100 * 1.20 * 1.10 = 132
        Assert.AreEqual(132f, result, 0.01f);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // CalculateDamageReduction — victim resistance + self-buff + ally-buff
    // ──────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void CalculateDamageReduction_NoHeroAndNoVictim_ReturnsBaseUnchanged()
    {
        var result = _sut.CalculateDamageReduction(victimHeroId: null, victimAgentIndex: null, troopLeaderHeroId: null, AttackTypeMask.Melee, baseResult: 100f);
        Assert.AreEqual(100f, result);
    }

    [TestMethod]
    public void CalculateDamageReduction_HeroWithResistance_MultipliesByOneMinus()
    {
        _passives.GetMaskedMagnitude("hero1", PassiveEffectType.Resistance, AttackTypeMask.Melee).Returns(0.30f);
        var result = _sut.CalculateDamageReduction("hero1", victimAgentIndex: null, troopLeaderHeroId: null, AttackTypeMask.Melee, baseResult: 100f);
        Assert.AreEqual(70f, result, 0.01f);
    }

    [TestMethod]
    public void CalculateDamageReduction_RangedHit_DoesNotApplyMeleeOnlyResistance()
    {
        // A melee-masked Resistance pip must not reduce a ranged hit; the masked lookup keyed on the
        // ranged hit returns 0, so the base damage passes through unchanged.
        _passives.GetMaskedMagnitude("hero1", PassiveEffectType.Resistance, AttackTypeMask.Ranged).Returns(0f);
        var result = _sut.CalculateDamageReduction("hero1", victimAgentIndex: null, troopLeaderHeroId: null, AttackTypeMask.Ranged, baseResult: 100f);
        Assert.AreEqual(100f, result, 0.01f);
    }

    [TestMethod]
    public void CalculateDamageReduction_HeroWithSelfBuffReduction_StacksWithResistance()
    {
        _passives.GetMaskedMagnitude("hero1", PassiveEffectType.Resistance, AttackTypeMask.Melee).Returns(0.10f);
        CareerAbilityBuffTracker.SetBuff("hero1", new ActiveBuffs { DamageReductionBonus = 0.20f });

        var result = _sut.CalculateDamageReduction("hero1", victimAgentIndex: null, troopLeaderHeroId: null, AttackTypeMask.Melee, baseResult: 100f);

        // 100 * (1-0.10) * (1-0.20) = 100 * 0.9 * 0.8 = 72
        Assert.AreEqual(72f, result, 0.01f);
    }

    [TestMethod]
    public void CalculateDamageReduction_NonHeroAgentWithAllyBuff_AppliesAllyReduction()
    {
        CareerAbilityBuffTracker.SetAllyBuff(99, new ActiveBuffs { DamageReductionBonus = 0.15f });

        var result = _sut.CalculateDamageReduction(victimHeroId: null, victimAgentIndex: 99, troopLeaderHeroId: null, AttackTypeMask.Melee, baseResult: 100f);

        Assert.AreEqual(85f, result, 0.01f);
    }

    [TestMethod]
    public void CalculateDamageReduction_HeroWithAllyBuffOnSameIndex_StacksBoth()
    {
        // A hero can also have an ally-buff entry (Infantry AoE puts hero in the ally map too).
        // The original code applies BOTH the hero self-buff and the ally-buff for the same agent.
        _passives.GetMaskedMagnitude("hero1", PassiveEffectType.Resistance, AttackTypeMask.Melee).Returns(0f);
        CareerAbilityBuffTracker.SetBuff("hero1", new ActiveBuffs { DamageReductionBonus = 0.20f });
        CareerAbilityBuffTracker.SetAllyBuff(7, new ActiveBuffs { DamageReductionBonus = 0.10f });

        var result = _sut.CalculateDamageReduction("hero1", victimAgentIndex: 7, troopLeaderHeroId: null, AttackTypeMask.Melee, baseResult: 100f);

        // 100 * (1-0.20) * (1-0.10) = 100 * 0.8 * 0.9 = 72
        Assert.AreEqual(72f, result, 0.01f);
    }

    [TestMethod]
    public void CalculateDamageReduction_ZeroResistanceAndZeroBuffs_ReturnsBaseUnchanged()
    {
        // No buff, no ally-buff entries; masked Resistance defaults to 0.
        var result = _sut.CalculateDamageReduction("hero1", victimAgentIndex: 7, troopLeaderHeroId: null, AttackTypeMask.Melee, baseResult: 100f);
        Assert.AreEqual(100f, result);
    }

    [TestMethod]
    public void CalculateDamageReduction_TroopLeaderWithTroopResistance_MultipliesByOneMinus()
    {
        // A non-hero troop victim: victimHeroId is null, troopLeaderHeroId is the party leader.
        // TroopResistance is not attack-typed, so it uses the mask-agnostic GetPassiveMagnitude.
        _passives.GetPassiveMagnitude("leader1", PassiveEffectType.TroopResistance).Returns(0.20f);

        var result = _sut.CalculateDamageReduction(victimHeroId: null, victimAgentIndex: null, troopLeaderHeroId: "leader1", AttackTypeMask.Melee, baseResult: 100f);

        Assert.AreEqual(80f, result, 0.01f);
    }

    [TestMethod]
    public void CalculateDamageReduction_ZeroTroopResistance_ReturnsBaseUnchanged()
    {
        _passives.GetPassiveMagnitude("leader1", PassiveEffectType.TroopResistance).Returns(0f);

        var result = _sut.CalculateDamageReduction(victimHeroId: null, victimAgentIndex: null, troopLeaderHeroId: "leader1", AttackTypeMask.Melee, baseResult: 100f);

        Assert.AreEqual(100f, result, 0.01f);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ShouldShrugOffBlow — victim ShrugOff passive
    // ──────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void ShouldShrugOffBlow_NullHeroId_ReturnsFalse()
    {
        Assert.IsFalse(_sut.ShouldShrugOffBlow(victimHeroId: null));
        _passives.DidNotReceiveWithAnyArgs().GetPassiveMagnitude(default!, default);
    }

    [TestMethod]
    public void ShouldShrugOffBlow_ZeroShrugOffMagnitude_ReturnsFalse()
    {
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.ShrugOff).Returns(0f);
        Assert.IsFalse(_sut.ShouldShrugOffBlow("hero1"));
    }

    [TestMethod]
    public void ShouldShrugOffBlow_NonZeroShrugOffMagnitude_ReturnsTrue()
    {
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.ShrugOff).Returns(0.5f);
        Assert.IsTrue(_sut.ShouldShrugOffBlow("hero1"));
    }

    [TestMethod]
    public void ShouldShrugOffBlow_NegativeMagnitude_ReturnsFalse()
    {
        // shrugOff > 0f gate — negative magnitude does NOT trigger shrug.
        _passives.GetPassiveMagnitude("hero1", PassiveEffectType.ShrugOff).Returns(-0.5f);
        Assert.IsFalse(_sut.ShouldShrugOffBlow("hero1"));
    }
}
