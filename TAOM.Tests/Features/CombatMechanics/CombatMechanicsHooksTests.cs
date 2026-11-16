using System;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CombatMechanics;
using TAOM.Features.CombatMechanics.Domain;
using TAOM.Features.CombatMechanics.Hooks;
using TAOM.Tests.Infrastructure;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Features.CombatMechanics;

/// <summary>
/// The boundary the campaign and Custom Battle damage models hand their combat seams to (#737, #788). A live <c>Agent</c> cannot be built outside the
/// game, so these pin what each seam gives its service when the engine hands it no agent, and that the service's answer,
/// or the vanilla value when it has none, comes back. The collision and blow carry a non-default value for each field a seam
/// maps from them, so a dropped or swapped field fails. With no agents, both sides of a context builder read the same
/// neutral defaults, so these cannot see two agents swapped inside a builder; the builders moved byte-identical from HEAD. Which agent reaches which seam is pinned on
/// the model's source (<c>CombatMechanicsModelInvariantsTests</c>); the decisions are the services' own tests. Reading an
/// engine struct's getter runs engine code, hence the category.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class CombatMechanicsHooksTests
{
    private ICrushThroughService _crush = null!;
    private IChargeKnockdownService _charge = null!;
    private ICreatureCombatService _creature = null!;
    private IShieldPenetrationService _shield = null!;
    private IShieldBashKnockdownService _bash = null!;
    private ICareerAgentStatService _career = null!;
    private ICombatMechanicsSettingsProvider _settings = null!;
    private CombatMechanicsHooks _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _crush = Substitute.For<ICrushThroughService>();
        _charge = Substitute.For<IChargeKnockdownService>();
        _creature = Substitute.For<ICreatureCombatService>();
        _shield = Substitute.For<IShieldPenetrationService>();
        _bash = Substitute.For<IShieldBashKnockdownService>();
        _career = Substitute.For<ICareerAgentStatService>();
        _settings = Substitute.For<ICombatMechanicsSettingsProvider>();
        _sut = new CombatMechanicsHooks(_crush, _charge, _creature, _shield, _bash, _career, _settings);
        _savedPierce = CombatMechanicsHooks.ShooterPiercesShields;
        CombatMechanicsHooks.ShooterPiercesShields = null;
    }

    private Func<Agent, bool>? _savedPierce;

    [TestCleanup]
    public void Cleanup() => CombatMechanicsHooks.ShooterPiercesShields = _savedPierce;

    // No native side: only the grant delegate sees it, by reference.
    private static Agent BareAgent() => (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));

    [TestMethod]
    public void CrushThrough_NoAgents_AsksWithTheSwingAndNeutralFighters()
    {
        CrushThroughContext seen = default;
        _crush.DecideCrushThrough(Arg.Do<CrushThroughContext>(c => seen = c)).Returns(true);

        var verdict = _sut.CrushThrough(null!, null!, 42f, Agent.UsageDirection.AttackUp, StrikeType.Swing, null!, isPassiveUsageHit: true);

        Assert.AreEqual(true, verdict);
        Assert.AreEqual(42f, seen.TotalAttackEnergy);
        Assert.IsTrue(seen.IsSwing);
        Assert.IsTrue(seen.IsOverhead);
        Assert.IsTrue(seen.IsPassiveUsage);
        Assert.IsFalse(seen.HasMeleeWeapon);
        Assert.IsFalse(seen.DefendItemIsShield);
        Assert.AreEqual(0, seen.AttackerWeaponSkill);
        Assert.AreEqual(0, seen.DefenderWeaponSkill);
        Assert.IsNull(seen.AttackerRaceId);
        Assert.IsNull(seen.DefenderRaceId);
        Assert.IsNull(seen.AttackerMonsterId);
        Assert.IsFalse(seen.IsAiControlled);
        Assert.IsTrue(seen.RandomRoll >= 0f && seen.RandomRoll <= 1f, $"roll {seen.RandomRoll}");
    }

    [TestMethod]
    public void CrushThrough_ThrustFromBelowAndServiceHasNoOpinion_AsksWithNoSwingAndReturnsNull()
    {
        CrushThroughContext seen = default;
        _crush.DecideCrushThrough(Arg.Do<CrushThroughContext>(c => seen = c)).Returns((bool?)null);

        Assert.IsNull(_sut.CrushThrough(null!, null!, 1f, Agent.UsageDirection.AttackDown, StrikeType.Thrust, null!, false));
        Assert.IsFalse(seen.IsSwing);
        Assert.IsFalse(seen.IsOverhead);
        Assert.IsFalse(seen.IsPassiveUsage);
    }

    [TestMethod]
    public void CleaveMomentum_NoAttacker_AsksWithoutAMonsterId()
    {
        _creature.CalculateCleaveMomentum(null!, 5f, true).Returns(2f);

        Assert.AreEqual(2f, _sut.CleaveMomentum(null!, 5f, CollisionDataFixture.With(isColliderAgent: true)));
    }

    [TestMethod]
    public void CleaveMomentum_ServiceHasNoOpinion_ReturnsNull()
    {
        _creature.CalculateCleaveMomentum(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<bool>()).Returns((float?)null);

        Assert.IsNull(_sut.CleaveMomentum(null!, 5f, default));
    }

    [TestMethod]
    public void CollisionReaction_ServiceForcesTheCleave_SlicesThrough()
    {
        _creature.ShouldForceSliceThrough(null!, 3f, true, 25).Returns(true);

        Assert.AreEqual(MeleeCollisionReaction.SlicedThrough,
            _sut.CollisionReaction(null!, 3f, CollisionDataFixture.With(inflictedDamage: 25, isColliderAgent: true), MeleeCollisionReaction.Bounced));
    }

    [TestMethod]
    public void CollisionReaction_ServiceDeclines_KeepsTheVanillaReaction()
    {
        _creature.ShouldForceSliceThrough(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<bool>(), Arg.Any<int>()).Returns(false);

        Assert.AreEqual(MeleeCollisionReaction.Bounced, _sut.CollisionReaction(null!, 3f, default, MeleeCollisionReaction.Bounced));
    }

    [TestMethod]
    public void IsUnstoppable_NoVictim_AsksWithoutAMonsterIdAndWithTheDamage()
    {
        _creature.IsUnstoppable(null!, 12).Returns(true);

        Assert.IsTrue(_sut.IsUnstoppable(null!, CollisionDataFixture.With(inflictedDamage: 12)));
    }

    [TestMethod]
    public void StaggerThreshold_NoDefender_AsksWithNoRace()
    {
        _creature.ApplyStaggerThresholdMultiplier(null, 7f).Returns(10.5f);

        Assert.AreEqual(10.5f, _sut.StaggerThreshold(null!, 7f));
    }

    [TestMethod]
    public void ChargeKnockdown_HorseChargeWithoutAgents_AsksWithTheChargeAndNeutralWeightsAndResistance()
    {
        ChargeKnockdownContext seen = default;
        _charge.DecideChargeKnockdown(Arg.Do<ChargeKnockdownContext>(c => seen = c)).Returns(false);
        var blow = default(Blow);
        blow.BlowFlag = BlowFlags.ShrugOff;

        var verdict = _sut.ChargeKnockdown(null!, null!, CollisionDataFixture.With(chargeVelocity: 6f, inflictedDamage: 30), blow);

        Assert.AreEqual(false, verdict);
        Assert.IsTrue(seen.IsHorseCharge);
        Assert.AreEqual(6f, seen.ChargeVelocity);
        Assert.AreEqual(30f, seen.InflictedDamage);
        Assert.IsTrue(seen.HasShrugOffFlag);
        Assert.IsFalse(seen.HasKnockBackFlag);
        Assert.AreEqual(1, seen.ChargerWeight);
        Assert.AreEqual(0, seen.RiderWeight);
        Assert.AreEqual(1, seen.VictimWeight);
        Assert.IsNull(seen.VictimRaceId);
        Assert.AreEqual(1f, seen.VictimMaxHealth);
        Assert.AreEqual(1f, seen.VictimKnockDownResistance, "a null victim reads no stat model");
        Assert.AreEqual(float.MaxValue, seen.ChargerSpeedLimitForCharge);
    }

    // The service's Branch B needs the knock-back flag, so a dropped mapping would switch it off without a sound.
    [TestMethod]
    public void ChargeKnockdown_KnockBackBlow_CarriesTheKnockBackFlagOnly()
    {
        ChargeKnockdownContext seen = default;
        _charge.DecideChargeKnockdown(Arg.Do<ChargeKnockdownContext>(c => seen = c)).Returns(true);
        var blow = default(Blow);
        blow.BlowFlag = BlowFlags.KnockBack;

        _sut.ChargeKnockdown(null!, null!, CollisionDataFixture.With(chargeVelocity: 6f), blow);

        Assert.IsTrue(seen.HasKnockBackFlag);
        Assert.IsFalse(seen.HasShrugOffFlag);
    }

    [TestMethod]
    public void ChargeKnockdown_ServiceHasNoOpinion_ReturnsNull()
    {
        _charge.DecideChargeKnockdown(Arg.Any<ChargeKnockdownContext>()).Returns((bool?)null);

        Assert.IsNull(_sut.ChargeKnockdown(null!, null!, CollisionDataFixture.With(chargeVelocity: 6f), default));
    }

    // Not a charge: no context is built, so neither its stat-model resistance read nor the service runs.
    [TestMethod]
    public void ChargeKnockdown_NotAHorseCharge_NeverAsks()
    {
        Assert.IsNull(_sut.ChargeKnockdown(null!, null!, CollisionDataFixture.With(inflictedDamage: 30), default));
        _charge.DidNotReceiveWithAnyArgs().DecideChargeKnockdown(default);
    }

    // IsHorseCharge is ChargeVelocity > 0f, so a NaN velocity is no charge, as the model routed it before #737.
    [TestMethod]
    public void ChargeKnockdown_NaNChargeVelocity_IsNoChargeAndNeverAsks()
    {
        Assert.IsNull(_sut.ChargeKnockdown(null!, null!, CollisionDataFixture.With(chargeVelocity: float.NaN), default));
        _charge.DidNotReceiveWithAnyArgs().DecideChargeKnockdown(default);
    }

    // Hammerhand (#895). A live Agent cannot be built here, so the context mapping (the Athletics reads, the immunity sources, the
    // race, the roll) is not reachable; ShieldBashWiringTests pins the facade and builder on the source. A bash that arrives
    // without fighters (unreachable in the engine) declines instead of reading a skill off null.
    [TestMethod]
    public void ShieldBashKnockdown_KickWithoutAgents_DeclinesWithoutAsking()
    {
        _bash.Decide(default).ReturnsForAnyArgs(true);

        Assert.IsNull(_sut.ShieldBashKnockdown(null!, null!, CollisionDataFixture.With(isAlternativeAttack: true), null!));
        _bash.DidNotReceiveWithAnyArgs().Decide(default);
    }

    [DataTestMethod]
    [DataRow(WeaponClass.SmallShield, true)]
    [DataRow(WeaponClass.LargeShield, true)]
    [DataRow(WeaponClass.OneHandedSword, false)]
    [DataRow(WeaponClass.Mace, false)]
    [DataRow(WeaponClass.Undefined, false)]
    public void IsShieldClass_OnlyTheTwoShieldClassesPass(WeaponClass weaponClass, bool expected)
    {
        var weapon = new WeaponComponentData(null!, weaponClass, 0);

        Assert.AreEqual(expected, CombatMechanicsHooks.IsShieldClass(weapon));
    }

    [TestMethod]
    public void PenetrationFlags_EmptyMissile_AsksWithNoItemAndHandsBackTheServiceFlags()
    {
        _shield.ApplyPenetrationFlags(null!, null!, (ulong)WeaponFlags.MeleeWeapon)
            .Returns((ulong)(WeaponFlags.MeleeWeapon | WeaponFlags.CanPenetrateShield));

        var flags = _sut.PenetrationFlags(null, default, WeaponFlags.MeleeWeapon);

        Assert.AreEqual(WeaponFlags.MeleeWeapon | WeaponFlags.CanPenetrateShield, flags);
    }

    // The hero grant (#1361, decision R7) rides after the service and outside its MCM switch. A shooter the delegate
    // does not name keeps exactly the service's flags.
    [TestMethod]
    public void PenetrationFlags_NullShooter_KeepsServiceFlags()
    {
        CombatMechanicsHooks.ShooterPiercesShields = _ => true;
        _shield.ApplyPenetrationFlags(null!, null!, (ulong)WeaponFlags.MeleeWeapon).Returns((ulong)WeaponFlags.MeleeWeapon);

        Assert.AreEqual(WeaponFlags.MeleeWeapon, _sut.PenetrationFlags(null, default, WeaponFlags.MeleeWeapon));
    }

    [TestMethod]
    public void PenetrationFlags_NoDelegate_KeepsServiceFlags()
    {
        CombatMechanicsHooks.ShooterPiercesShields = null;
        _shield.ApplyPenetrationFlags(null!, null!, (ulong)WeaponFlags.MeleeWeapon).Returns((ulong)WeaponFlags.MeleeWeapon);

        Assert.AreEqual(WeaponFlags.MeleeWeapon, _sut.PenetrationFlags(BareAgent(), default, WeaponFlags.MeleeWeapon));
    }

    [TestMethod]
    public void PenetrationFlags_DelegateDeclines_KeepsServiceFlags()
    {
        CombatMechanicsHooks.ShooterPiercesShields = _ => false;
        _shield.ApplyPenetrationFlags(null!, null!, (ulong)WeaponFlags.MeleeWeapon).Returns((ulong)WeaponFlags.MeleeWeapon);

        Assert.AreEqual(WeaponFlags.MeleeWeapon, _sut.PenetrationFlags(BareAgent(), default, WeaponFlags.MeleeWeapon));
    }

    [TestMethod]
    public void PenetrationFlags_GrantedShooter_AddsCanPenetrateShieldOnly_AfterTheService()
    {
        var hero = BareAgent();
        CombatMechanicsHooks.ShooterPiercesShields = a => ReferenceEquals(a, hero);
        _shield.ApplyPenetrationFlags(null!, null!, (ulong)WeaponFlags.MeleeWeapon).Returns((ulong)WeaponFlags.MeleeWeapon);

        var flags = _sut.PenetrationFlags(hero, default, WeaponFlags.MeleeWeapon);

        Assert.AreEqual(WeaponFlags.MeleeWeapon | WeaponFlags.CanPenetrateShield, flags);
        Assert.AreEqual((WeaponFlags)0, flags & WeaponFlags.MultiplePenetration, "one shield, never a second body");
    }

    [DataTestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void ShieldPierceFloor_NotBlockedOrNotMissile_ReturnsInput(bool isMissile, bool blocked)
    {
        CombatMechanicsHooks.ShooterPiercesShields = _ => true;
        var info = new AttackInformation { AttackerAgent = BareAgent() };
        var collision = CollisionDataFixture.With(isMissile: isMissile, attackBlockedWithShield: blocked);

        Assert.AreEqual(12f, _sut.ShieldPierceFloor(in info, in collision, 12f));
    }

    [TestMethod]
    public void ShieldPierceFloor_NoAttacker_ReturnsInput()
    {
        CombatMechanicsHooks.ShooterPiercesShields = _ => true;
        var info = new AttackInformation();
        var collision = CollisionDataFixture.With(isMissile: true, attackBlockedWithShield: true);

        Assert.AreEqual(12f, _sut.ShieldPierceFloor(in info, in collision, 12f));
    }

    [TestMethod]
    public void ShieldPierceFloor_NoDelegateOrDeclined_ReturnsInput()
    {
        var info = new AttackInformation { AttackerAgent = BareAgent() };
        var collision = CollisionDataFixture.With(isMissile: true, attackBlockedWithShield: true);

        CombatMechanicsHooks.ShooterPiercesShields = null;
        Assert.AreEqual(12f, _sut.ShieldPierceFloor(in info, in collision, 12f));

        CombatMechanicsHooks.ShooterPiercesShields = _ => false;
        Assert.AreEqual(12f, _sut.ShieldPierceFloor(in info, in collision, 12f));
    }

    // A granted shooter whose victim holds no shield in the offhand (AttackInformation.cs:187-194 leaves it Invalid).
    [TestMethod]
    public void ShieldPierceFloor_GrantedButNoVictimShield_ReturnsInput()
    {
        CombatMechanicsHooks.ShooterPiercesShields = _ => true;
        var info = new AttackInformation { AttackerAgent = BareAgent(), VictimShield = MissionWeapon.Invalid };
        var collision = CollisionDataFixture.With(isMissile: true, attackBlockedWithShield: true);

        Assert.AreEqual(12f, _sut.ShieldPierceFloor(in info, in collision, 12f));
    }

    [TestMethod]
    public void ShieldDamage_UnarmedAttacker_AsksWithNoItemAndNoStaticFlag()
    {
        _shield.ApplyRuntimeFlagCorrection(null!, null!, false, 12f).Returns(6f);

        Assert.AreEqual(6f, _sut.ShieldDamage(default, 12f));
    }

    [TestMethod]
    public void HorseChargePenetration_FeatureOn_IsTheSetting()
    {
        _settings.ChargeKnockdownEnabled.Returns(true);
        _settings.ChargeHorsePenetration.Returns(0.55f);

        Assert.AreEqual(0.55f, _sut.HorseChargePenetration());
    }

    [TestMethod]
    public void HorseChargePenetration_FeatureOff_IsNullSoTheModelKeepsVanilla()
    {
        _settings.ChargeKnockdownEnabled.Returns(false);
        _settings.ChargeHorsePenetration.Returns(0.55f);

        Assert.IsNull(_sut.HorseChargePenetration());
    }
}
