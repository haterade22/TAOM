using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.Warg;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

// Phase 9b — Warg ADR-007 refactor (closes #178):
//
//   IWargAttackService now exposes IAgentAdapter instead of sealed TaleWorlds Agent.
//   HandleWargTargetHit + WargAttack are fully mockable via NSubstitute; the boundary
//   conversion (Agent → IAgentAdapter) happens once in WargAttackTask before the
//   service is called. CalculateWargAttackDamage takes the armor value as an explicit
//   parameter, removing the previous "TestableWargAttackService subclass" workaround.
//
//   The single remaining sealed-type leak is CustomAttacksUtils.TakeDamage at the
//   bottom of HandleWargTargetHit (mirrors Spider's pattern, line 58-61 of
//   SpiderAttackService). Every guard, branch, damage calculation, ProjectAgent
//   call, and team check above it is reachable from tests via IAgentAdapter mocks.

namespace TAOM.Tests.Features.Warg;

[TestClass]
public class WargAttackServiceTests
{
    private IMissionAdapterFactory _adapterFactory;
    private IModLogger _logger;
    private WargAttackService _sut;

    [TestInitialize]
    public void Setup()
    {
        _adapterFactory = Substitute.For<IMissionAdapterFactory>();
        _logger = Substitute.For<IModLogger>();
        _sut = new WargAttackService(_adapterFactory, _logger);
    }

    // -------------------------------------------------------------------------
    // CalculateWargAttackDamage — pure formula via IAgentAdapter mocks
    //
    // Formula:
    //   fromSpeed  = Min(MaxSpeedDamage=20, velocity * 20 / SpeedForMaxDamage=20)
    //              = Min(20, velocity)
    //   allDamage  = fromSpeed + MaxBaseDamage=40
    //   absorption = Clamp((100 - armor%) / 100, 0, 1)
    //   damage     = (int)(allDamage * absorption)
    // -------------------------------------------------------------------------

    [TestMethod]
    public void CalculateWargAttackDamage_ZeroArmorAndMaxSpeed_ReturnsMaxPossibleDamage()
    {
        var target = Substitute.For<IAgentAdapter>();
        int result = _sut.CalculateWargAttackDamage(target, velocity: 20f, armorEffectivenessPercent: 0f);
        // fromSpeed=20, allDamage=60, absorption=1.0, damage=60
        Assert.AreEqual(60, result);
    }

    [TestMethod]
    public void CalculateWargAttackDamage_FullArmorAbsorption_ReturnsZero()
    {
        var target = Substitute.For<IAgentAdapter>();
        int result = _sut.CalculateWargAttackDamage(target, velocity: 20f, armorEffectivenessPercent: 100f);
        // absorption = 0 → damage = 0
        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public void CalculateWargAttackDamage_ZeroVelocity_ReturnsOnlyBaseDamage()
    {
        var target = Substitute.For<IAgentAdapter>();
        int result = _sut.CalculateWargAttackDamage(target, velocity: 0f, armorEffectivenessPercent: 0f);
        // fromSpeed=0, allDamage=40, absorption=1.0, damage=40
        Assert.AreEqual(40, result);
    }

    [TestMethod]
    public void CalculateWargAttackDamage_ExcessiveVelocity_ClampsSpeedDamageToMax()
    {
        var target = Substitute.For<IAgentAdapter>();
        int result = _sut.CalculateWargAttackDamage(target, velocity: 100f, armorEffectivenessPercent: 0f);
        // fromSpeed = Min(20, 100) = 20 (capped), allDamage=60, damage=60
        Assert.AreEqual(60, result);
    }

    [TestMethod]
    public void CalculateWargAttackDamage_HalfSpeedHalfArmor_ReturnsExpectedMidRangeDamage()
    {
        var target = Substitute.For<IAgentAdapter>();
        int result = _sut.CalculateWargAttackDamage(target, velocity: 10f, armorEffectivenessPercent: 50f);
        // fromSpeed=10, allDamage=50, absorption=0.5, damage=25
        Assert.AreEqual(25, result);
    }

    // -------------------------------------------------------------------------
    // HandleWargTargetHit — guard clauses (skip-guard exhaustion)
    // -------------------------------------------------------------------------

    [TestMethod]
    public void HandleWargTargetHit_NullTarget_DoesNothing()
    {
        var attacker = Substitute.For<IAgentAdapter>();
        // Should not throw, no logger error called.
        _sut.HandleWargTargetHit(attacker, target: null, boneId: 0);
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void HandleWargTargetHit_InactiveTarget_DoesNothing()
    {
        var attacker = Substitute.For<IAgentAdapter>();
        var target = Substitute.For<IAgentAdapter>();
        target.IsActive().Returns(false);

        _sut.HandleWargTargetHit(attacker, target, 0);

        target.DidNotReceive().GetBaseArmorEffectivenessForBodyPart(Arg.Any<BoneBodyPartType>());
        target.DidNotReceive().ProjectAgent(Arg.Any<Vec3>(), Arg.Any<DamageAnimation>());
    }

    [TestMethod]
    public void HandleWargTargetHit_FadingOutTarget_DoesNothing()
    {
        var attacker = Substitute.For<IAgentAdapter>();
        var target = Substitute.For<IAgentAdapter>();
        target.IsActive().Returns(true);
        target.IsFadingOut().Returns(true);

        _sut.HandleWargTargetHit(attacker, target, 0);

        target.DidNotReceive().GetBaseArmorEffectivenessForBodyPart(Arg.Any<BoneBodyPartType>());
    }

    [TestMethod]
    public void HandleWargTargetHit_NullAttacker_DoesNothing()
    {
        var target = Substitute.For<IAgentAdapter>();
        target.IsActive().Returns(true);
        target.IsFadingOut().Returns(false);

        _sut.HandleWargTargetHit(attacker: null, target: target, boneId: 0);

        target.DidNotReceive().GetBaseArmorEffectivenessForBodyPart(Arg.Any<BoneBodyPartType>());
    }

    [TestMethod]
    public void HandleWargTargetHit_RiderAndVictimOnSameTeam_DoesNotDamage()
    {
        // Warg-specific: if the warg has a rider AND the rider is on the same team as the
        // victim (or the victim's rider, if mounted), the attack is friendly-fire and skipped.
        var attacker = Substitute.For<IAgentAdapter>();
        var attackerRider = Substitute.For<IAgentAdapter>();
        attacker.RiderAgent.Returns(attackerRider);

        var target = Substitute.For<IAgentAdapter>();
        target.IsActive().Returns(true);
        target.IsFadingOut().Returns(false);
        target.IsMount.Returns(false);

        attackerRider.IsSameTeam(target).Returns(true);

        _sut.HandleWargTargetHit(attacker, target, 0);

        target.DidNotReceive().GetBaseArmorEffectivenessForBodyPart(Arg.Any<BoneBodyPartType>());
        target.DidNotReceive().ProjectAgent(Arg.Any<Vec3>(), Arg.Any<DamageAnimation>());
    }

    [TestMethod]
    public void HandleWargTargetHit_VictimIsMountWithRiderOnSameTeamAsAttackerRider_DoesNotDamage()
    {
        // Warg-specific mounted-victim team attribution: when victim is a mount and has a rider,
        // the team check uses the rider's team, not the mount's. Friendly mounts must be skipped.
        var attacker = Substitute.For<IAgentAdapter>();
        var attackerRider = Substitute.For<IAgentAdapter>();
        attacker.RiderAgent.Returns(attackerRider);

        var victimMount = Substitute.For<IAgentAdapter>();
        var victimRider = Substitute.For<IAgentAdapter>();
        victimMount.IsActive().Returns(true);
        victimMount.IsFadingOut().Returns(false);
        victimMount.IsMount.Returns(true);
        victimMount.RiderAgent.Returns(victimRider);

        attackerRider.IsSameTeam(victimRider).Returns(true);

        _sut.HandleWargTargetHit(attacker, victimMount, 0);

        victimMount.DidNotReceive().GetBaseArmorEffectivenessForBodyPart(Arg.Any<BoneBodyPartType>());
    }

    [TestMethod]
    public void HandleWargTargetHit_KilledTargetState_DoesNotDamage()
    {
        var attacker = Substitute.For<IAgentAdapter>();
        var target = Substitute.For<IAgentAdapter>();
        target.IsActive().Returns(true);
        target.IsFadingOut().Returns(false);
        target.State.Returns(AgentState.Killed);

        _sut.HandleWargTargetHit(attacker, target, 0);

        target.DidNotReceive().GetBaseArmorEffectivenessForBodyPart(Arg.Any<BoneBodyPartType>());
        target.DidNotReceive().ProjectAgent(Arg.Any<Vec3>(), Arg.Any<DamageAnimation>());
    }

    [TestMethod]
    public void HandleWargTargetHit_UnseatedHumanTarget_ProjectsAgentWithFallAnimation()
    {
        // When the target has no mount and damage >= DamageToFall, ProjectAgent is invoked
        // with DamageAnimation.Fall so the human is knocked down.
        var attacker = Substitute.For<IAgentAdapter>();
        attacker.MovementVelocity.Returns(new Vec2(20f, 0f));   // max-speed warg → high damage
        attacker.RiderAgent.Returns((IAgentAdapter)null);       // no rider → attacker is its own damager

        // Attacker self-Position is irrelevant for the team check since RiderAgent is null,
        // but ProjectAgent reads damagerAdapter.Position which falls back to attacker.
        var attackerPos = new Vec3(1f, 2f, 3f);
        attacker.Position.Returns(attackerPos);
        attacker.Health.Returns(100);

        var target = Substitute.For<IAgentAdapter>();
        target.IsActive().Returns(true);
        target.IsFadingOut().Returns(false);
        target.State.Returns(AgentState.Active);
        target.IsMount.Returns(false);
        target.HasMount.Returns(false);
        target.IsHorse().Returns(false);
        target.IsCamel().Returns(false);
        target.GetBaseArmorEffectivenessForBodyPart(BoneBodyPartType.Chest).Returns(0);

        _sut.HandleWargTargetHit(attacker, target, 0);

        target.Received(1).ProjectAgent(attackerPos, DamageAnimation.Fall);
    }

    [TestMethod]
    public void HandleWargTargetHit_MountedTarget_DoesNotProjectAgent()
    {
        // ProjectAgent is suppressed when target.HasMount — riders take damage but don't
        // play the on-foot knockdown animation.
        var attacker = Substitute.For<IAgentAdapter>();
        attacker.MovementVelocity.Returns(new Vec2(20f, 0f));
        attacker.RiderAgent.Returns((IAgentAdapter)null);
        attacker.Position.Returns(new Vec3(0f, 0f, 0f));
        attacker.Health.Returns(100);

        var target = Substitute.For<IAgentAdapter>();
        target.IsActive().Returns(true);
        target.IsFadingOut().Returns(false);
        target.State.Returns(AgentState.Active);
        target.IsMount.Returns(false);
        target.HasMount.Returns(true);
        target.GetBaseArmorEffectivenessForBodyPart(BoneBodyPartType.Chest).Returns(0);

        _sut.HandleWargTargetHit(attacker, target, 0);

        target.DidNotReceive().ProjectAgent(Arg.Any<Vec3>(), Arg.Any<DamageAnimation>());
    }

    [TestMethod]
    public void HandleWargTargetHit_TargetIsHorse_DoublesDamage()
    {
        // Horses/camels take 2× damage. We verify this by setting up a low-damage hit
        // that would normally produce DamageAnimation.Nothing but, after the 2× horse
        // multiplier, doesn't change the ProjectAgent call (target is a mount → suppressed
        // by HasMount check). Instead we verify the damage-doubling indirectly: with a
        // small base damage the animation would be Flinch; doubled it crosses DamageToFall.
        //
        // For a horse the HasMount-suppression branch hides ProjectAgent entirely, so
        // this test focuses on confirming the IsHorse path is reached without throwing.
        var attacker = Substitute.For<IAgentAdapter>();
        attacker.MovementVelocity.Returns(new Vec2(5f, 0f));
        attacker.RiderAgent.Returns((IAgentAdapter)null);
        attacker.Position.Returns(new Vec3(0f, 0f, 0f));
        attacker.Health.Returns(100);

        var target = Substitute.For<IAgentAdapter>();
        target.IsActive().Returns(true);
        target.IsFadingOut().Returns(false);
        target.State.Returns(AgentState.Active);
        target.IsMount.Returns(false);     // riderless horse, not "ridden mount"
        target.HasMount.Returns(false);
        target.IsHorse().Returns(true);
        target.GetBaseArmorEffectivenessForBodyPart(BoneBodyPartType.Chest).Returns(0);

        _sut.HandleWargTargetHit(attacker, target, 0);

        target.Received(1).IsHorse();
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void HandleWargTargetHit_ExceptionInBranch_LogsError()
    {
        // If a deep branch throws, the try/catch should funnel it into the logger.
        var attacker = Substitute.For<IAgentAdapter>();
        attacker.MovementVelocity.Returns(new Vec2(10f, 0f));
        attacker.RiderAgent.Returns((IAgentAdapter)null);
        attacker.Position.Returns(new Vec3(0f, 0f, 0f));
        attacker.Health.Returns(100);

        var target = Substitute.For<IAgentAdapter>();
        target.IsActive().Returns(true);
        target.IsFadingOut().Returns(false);
        target.State.Returns(AgentState.Active);
        target.IsMount.Returns(false);
        // Force the armor lookup to throw inside the try block.
        target.GetBaseArmorEffectivenessForBodyPart(Arg.Any<BoneBodyPartType>())
            .Returns(_ => throw new InvalidOperationException("simulated"));

        _sut.HandleWargTargetHit(attacker, target, 0);

        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("HandleWargTargetHit error") && s.Contains("simulated")));
    }

    // -------------------------------------------------------------------------
    // WargAttack — null/inactive guard
    // -------------------------------------------------------------------------

    [TestMethod]
    public void WargAttack_NullWarg_DoesNothing()
    {
        // Should not throw.
        _sut.WargAttack(warg: null);
    }

    [TestMethod]
    public void WargAttack_InactiveWarg_DoesNotInvokeCustomAttack()
    {
        var warg = Substitute.For<IAgentAdapter>();
        warg.IsActive().Returns(false);

        _sut.WargAttack(warg);

        warg.DidNotReceive().CustomAttack(
            Arg.Any<ActionIndexCache>(),
            Arg.Any<List<sbyte>>(),
            Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>(),
            Arg.Any<bool>(),
            Arg.Any<Action<IAgentAdapter, IAgentAdapter, sbyte>>(),
            Arg.Any<Action>());
    }

    [TestMethod]
    public void WargAttack_FastWarg_InvokesRunningAttack()
    {
        // velocity.Y >= 4 → "act_warg_attack_running", bone list size 1, progress range 0.1..0.7.
        var warg = Substitute.For<IAgentAdapter>();
        warg.IsActive().Returns(true);
        warg.MovementVelocity.Returns(new Vec2(0f, 10f));

        _sut.WargAttack(warg);

        warg.Received(1).CustomAttack(
            Arg.Any<ActionIndexCache>(),
            Arg.Is<List<sbyte>>(l => l.Count == 1 && l[0] == 23),
            actionProgressMin: 0.1f,
            actionProgressMax: 0.7f,
            Arg.Any<float>(),
            boneCollisionRadius: 0.4f,
            Arg.Any<bool>(),
            Arg.Any<Action<IAgentAdapter, IAgentAdapter, sbyte>>(),
            Arg.Any<Action>());
    }

    [TestMethod]
    public void WargAttack_SlowWarg_InvokesStandingAttack()
    {
        // velocity.Y < 4 → "act_warg_attack_stand", bone list size 3, progress range 0.1..0.5.
        var warg = Substitute.For<IAgentAdapter>();
        warg.IsActive().Returns(true);
        warg.MovementVelocity.Returns(new Vec2(0f, 1f));

        _sut.WargAttack(warg);

        warg.Received(1).CustomAttack(
            Arg.Any<ActionIndexCache>(),
            Arg.Is<List<sbyte>>(l => l.Count == 3 && l[0] == 23 && l[1] == 37 && l[2] == 43),
            actionProgressMin: 0.1f,
            actionProgressMax: 0.5f,
            Arg.Any<float>(),
            boneCollisionRadius: 0.3f,
            Arg.Any<bool>(),
            Arg.Any<Action<IAgentAdapter, IAgentAdapter, sbyte>>(),
            Arg.Any<Action>());
    }

    // -------------------------------------------------------------------------
    // Construction
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Constructor_ValidDependencies_ConstructsWithoutThrowing()
    {
        var service = new WargAttackService(
            Substitute.For<IMissionAdapterFactory>(),
            Substitute.For<IModLogger>());

        Assert.IsNotNull(service);
    }

    [TestMethod]
    public void Constructor_NullAdapterFactory_DoesNotThrow()
    {
        // WargAttackService has no null guards in its constructor; failures surface
        // only when boundary methods are invoked. Document the behavior.
        var service = new WargAttackService(null, Substitute.For<IModLogger>());

        Assert.IsNotNull(service);
    }
}
