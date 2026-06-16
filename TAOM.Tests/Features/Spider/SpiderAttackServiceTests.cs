using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.Spider;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Features.Spider;

[TestClass]
public class SpiderAttackServiceTests
{
    private IMissionAdapterFactory _adapterFactory;
    private IModLogger _logger;
    private SpiderAttackService _sut;

    [TestInitialize]
    public void Setup()
    {
        _adapterFactory = Substitute.For<IMissionAdapterFactory>();
        _logger = Substitute.For<IModLogger>();
        _sut = new SpiderAttackService(_adapterFactory, _logger);
    }

    // ---------------------------------------------------------------------
    // CalculateSpiderBiteDamage — pure formula
    //   fromSpeed  = Min(MaxSpeedDamage=15, velocity * 15 / SpeedForMaxDamage=15) = Min(15, velocity)
    //   allDamage  = fromSpeed + MaxBaseDamage=35
    //   absorption = Clamp((100 - armor%) / 100, 0, 1)
    //   damage     = (int)(allDamage * absorption)
    // ---------------------------------------------------------------------

    // critRoll 1f = no crit (CritChance 0.2) unless a test states otherwise.

    [TestMethod]
    public void CalculateSpiderBiteDamage_ZeroVelocityZeroArmor_ReturnsBaseDamage()
    {
        var target = Substitute.For<IAgentAdapter>();
        // fromSpeed=0, allDamage=75 (MaxBaseDamage), absorption=1.0, damage=75
        int result = _sut.CalculateSpiderBiteDamage(target, velocity: 0f, armorEffectivenessPercent: 0f, critRoll: 1f);
        Assert.AreEqual(75, result);
    }

    [TestMethod]
    public void CalculateSpiderBiteDamage_MaxVelocityZeroArmor_ReturnsCappedDamage()
    {
        var target = Substitute.For<IAgentAdapter>();
        // fromSpeed = Min(25, 15×25/15=25) = 25, allDamage=100, absorption=1.0, damage=100
        int result = _sut.CalculateSpiderBiteDamage(target, velocity: 15f, armorEffectivenessPercent: 0f, critRoll: 1f);
        Assert.AreEqual(100, result);
    }

    [TestMethod]
    public void CalculateSpiderBiteDamage_ExcessiveVelocity_ClampsSpeedDamageToMax()
    {
        var target = Substitute.For<IAgentAdapter>();
        // fromSpeed = Min(25, 100×25/15=166) = 25 (capped at MaxSpeedDamage), allDamage=100, damage=100
        int result = _sut.CalculateSpiderBiteDamage(target, velocity: 100f, armorEffectivenessPercent: 0f, critRoll: 1f);
        Assert.AreEqual(100, result);
    }

    [TestMethod]
    public void CalculateSpiderBiteDamage_MaxArmor_FlooredByMinPassthrough()
    {
        var target = Substitute.For<IAgentAdapter>();
        // absorption = (100 − 100×1.1)/100 = −0.1, clamped UP to MinArmorPassthrough 0.2 → 100×0.2 = 20.
        // Even max-armor plate always takes a bite (no full immunity).
        int result = _sut.CalculateSpiderBiteDamage(target, velocity: 15f, armorEffectivenessPercent: 100f, critRoll: 1f);
        Assert.AreEqual(20, result);
    }

    [TestMethod]
    public void CalculateSpiderBiteDamage_MediumArmorAtCharge_TwoHitKillBand()
    {
        var target = Substitute.For<IAgentAdapter>();
        // Medium line trooper (~35 torso armor), charge velY 8: fromSpeed=Min(25,8×25/15=13.3)=13.3,
        // allDamage=88.3, absorption=(100−35×1.1)/100=0.615, damage=54 → ~2 hits on 100 HP.
        int result = _sut.CalculateSpiderBiteDamage(target, velocity: 8f, armorEffectivenessPercent: 35f, critRoll: 1f);
        Assert.AreEqual(54, result);
    }

    [TestMethod]
    public void CalculateSpiderBiteDamage_HeavyArmorAtCharge_ThreeHitKillBand()
    {
        var target = Substitute.For<IAgentAdapter>();
        // Heavy elite (~55 torso armor), charge velY 8: allDamage=88.3, absorption=(100−55×1.1)/100=0.395, damage=34 → ~3 hits.
        int result = _sut.CalculateSpiderBiteDamage(target, velocity: 8f, armorEffectivenessPercent: 55f, critRoll: 1f);
        Assert.AreEqual(34, result);
    }

    [TestMethod]
    public void CalculateSpiderBiteDamage_CritRollBelowChance_AppliesMultiplier()
    {
        var target = Substitute.For<IAgentAdapter>();
        // critRoll 0.1 < CritChance 0.2 → crit. allDamage=75 (vel 0, no armor) × CritMultiplier 1.75 = 131.
        int result = _sut.CalculateSpiderBiteDamage(target, velocity: 0f, armorEffectivenessPercent: 0f, critRoll: 0.1f);
        Assert.AreEqual(131, result);
    }

    [TestMethod]
    public void CalculateSpiderBiteDamage_CritRollAtChance_NoCrit()
    {
        var target = Substitute.For<IAgentAdapter>();
        // critRoll 0.2 is NOT < CritChance 0.2 (strict) → no crit → 75.
        int result = _sut.CalculateSpiderBiteDamage(target, velocity: 0f, armorEffectivenessPercent: 0f, critRoll: 0.2f);
        Assert.AreEqual(75, result);
    }

    // ---------------------------------------------------------------------
    // HandleSpiderTargetHit — guard clauses (skip-guard exhaustion)
    // ---------------------------------------------------------------------

    [TestMethod]
    public void HandleSpiderTargetHit_NullTarget_DoesNothing()
    {
        var attacker = Substitute.For<IAgentAdapter>();
        // Should not throw, no logger error called.
        _sut.HandleSpiderTargetHit(attacker, target: null, boneId: 0);
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void HandleSpiderTargetHit_InactiveTarget_DoesNothing()
    {
        var attacker = Substitute.For<IAgentAdapter>();
        var target = Substitute.For<IAgentAdapter>();
        target.IsActive().Returns(false);

        _sut.HandleSpiderTargetHit(attacker, target, 0);

        target.DidNotReceive().ProjectAgent(Arg.Any<Vec3>(), Arg.Any<DamageAnimation>());
        target.DidNotReceive().GetBaseArmorEffectivenessForBodyPart(Arg.Any<BoneBodyPartType>());
    }

    [TestMethod]
    public void HandleSpiderTargetHit_FadingOutTarget_DoesNothing()
    {
        var attacker = Substitute.For<IAgentAdapter>();
        var target = Substitute.For<IAgentAdapter>();
        target.IsActive().Returns(true);
        target.IsFadingOut().Returns(true);

        _sut.HandleSpiderTargetHit(attacker, target, 0);

        target.DidNotReceive().GetBaseArmorEffectivenessForBodyPart(Arg.Any<BoneBodyPartType>());
    }

    [TestMethod]
    public void HandleSpiderTargetHit_NullAttacker_DoesNothing()
    {
        var target = Substitute.For<IAgentAdapter>();
        target.IsActive().Returns(true);
        target.IsFadingOut().Returns(false);

        _sut.HandleSpiderTargetHit(attacker: null, target: target, boneId: 0);

        target.DidNotReceive().GetBaseArmorEffectivenessForBodyPart(Arg.Any<BoneBodyPartType>());
    }

    [TestMethod]
    public void HandleSpiderTargetHit_SameTeam_DoesNotDamage()
    {
        var attacker = Substitute.For<IAgentAdapter>();
        var target = Substitute.For<IAgentAdapter>();
        target.IsActive().Returns(true);
        target.IsFadingOut().Returns(false);
        attacker.IsSameTeam(target).Returns(true);

        _sut.HandleSpiderTargetHit(attacker, target, 0);

        target.DidNotReceive().GetBaseArmorEffectivenessForBodyPart(Arg.Any<BoneBodyPartType>());
        target.DidNotReceive().ProjectAgent(Arg.Any<Vec3>(), Arg.Any<DamageAnimation>());
    }

    [TestMethod]
    public void HandleSpiderTargetHit_KilledTargetState_DoesNotDamage()
    {
        var attacker = Substitute.For<IAgentAdapter>();
        var target = Substitute.For<IAgentAdapter>();
        target.IsActive().Returns(true);
        target.IsFadingOut().Returns(false);
        attacker.IsSameTeam(target).Returns(false);
        target.State.Returns(AgentState.Killed);

        _sut.HandleSpiderTargetHit(attacker, target, 0);

        target.DidNotReceive().GetBaseArmorEffectivenessForBodyPart(Arg.Any<BoneBodyPartType>());
    }

    // ---------------------------------------------------------------------
    // SpiderAttack — null/inactive guard
    // ---------------------------------------------------------------------

    [TestMethod]
    public void SpiderAttack_NullSpider_DoesNothing()
    {
        // Should not throw.
        _sut.SpiderAttack(spider: null, SpiderAttackKind.Pounce, bearing: 0f);
    }

    [TestMethod]
    public void SpiderAttack_InactiveSpider_DoesNotStrike()
    {
        var spider = Substitute.For<IAgentAdapter>();
        spider.IsActive().Returns(false);

        _sut.SpiderAttack(spider, SpiderAttackKind.Pounce, bearing: 0f);

        spider.DidNotReceive().RadialStrike(
            Arg.Any<ActionIndexCache>(),
            Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>(),
            Arg.Any<System.Action<IAgentAdapter, IAgentAdapter, sbyte>>());
    }

    // ---------------------------------------------------------------------
    // IsSpiderMonster — BT-attach + mount-lock key (mirrors IElephantAttackService.IsElephantMonster)
    // ---------------------------------------------------------------------

    [TestMethod]
    public void IsSpiderMonster_SpiderId_ReturnsTrue()
        => Assert.IsTrue(_sut.IsSpiderMonster(SpiderConfig.SpiderMonsterId));

    [TestMethod]
    public void IsSpiderMonster_OtherId_ReturnsFalse()
        => Assert.IsFalse(_sut.IsSpiderMonster("warg"));

    [TestMethod]
    public void IsSpiderMonster_Null_ReturnsFalse()
        => Assert.IsFalse(_sut.IsSpiderMonster(null));

    // ---------------------------------------------------------------------
    // HandleSpiderTargetHit — RIDDEN-mount attribution (warg pattern):
    //   victimTeamSource = target.IsMount && target.RiderAgent != null ? target.RiderAgent : target
    //   friendly skip    = (attacker.RiderAgent ?? attacker).IsSameTeam(victimTeamSource)
    //   damager          = attacker.RiderAgent ?? attacker; dead/null damager -> self-fallback 20 dmg
    // Observable: ProjectAgent(damager.Position, anim) + armor-read suppression on skips.
    // ---------------------------------------------------------------------

    private static IAgentAdapter EnemyTarget()
    {
        var target = Substitute.For<IAgentAdapter>();
        target.IsActive().Returns(true);
        target.IsFadingOut().Returns(false);
        target.State.Returns(AgentState.Active);
        target.HasMount.Returns(false);
        target.IsMount.Returns(false);
        target.IsHorse().Returns(false);
        target.IsCamel().Returns(false);
        target.GetBaseArmorEffectivenessForBodyPart(Arg.Any<BoneBodyPartType>()).Returns(0);
        return target;
    }

    [TestMethod]
    public void HandleSpiderTargetHit_RiderSameTeamAsVictim_DoesNotDamage()
    {
        var attacker = Substitute.For<IAgentAdapter>();
        var rider = Substitute.For<IAgentAdapter>();
        var target = EnemyTarget();
        attacker.RiderAgent.Returns(rider);
        rider.IsSameTeam(target).Returns(true);     // the RIDER's team decides
        attacker.IsSameTeam(target).Returns(false); // spider's own team must NOT be consulted

        _sut.HandleSpiderTargetHit(attacker, target, 0);

        target.DidNotReceive().GetBaseArmorEffectivenessForBodyPart(Arg.Any<BoneBodyPartType>());
        target.DidNotReceive().ProjectAgent(Arg.Any<Vec3>(), Arg.Any<DamageAnimation>());
    }

    [TestMethod]
    public void HandleSpiderTargetHit_VictimIsMountWithRider_UsesVictimRiderForTeamCheck()
    {
        var attacker = Substitute.For<IAgentAdapter>();
        var rider = Substitute.For<IAgentAdapter>();
        var victimRider = Substitute.For<IAgentAdapter>();
        var target = EnemyTarget();
        target.IsMount.Returns(true);
        target.RiderAgent.Returns(victimRider);
        attacker.RiderAgent.Returns(rider);
        rider.IsSameTeam(victimRider).Returns(true);  // friendly via the victim's RIDER
        rider.IsSameTeam(target).Returns(false);

        _sut.HandleSpiderTargetHit(attacker, target, 0);

        target.DidNotReceive().GetBaseArmorEffectivenessForBodyPart(Arg.Any<BoneBodyPartType>());
    }

    [TestMethod]
    public void HandleSpiderTargetHit_LiveRider_RiderIsDamager_ProjectsFromRiderPosition()
    {
        var attacker = Substitute.For<IAgentAdapter>();
        var rider = Substitute.For<IAgentAdapter>();
        var target = EnemyTarget();
        attacker.RiderAgent.Returns(rider);
        rider.IsSameTeam(target).Returns(false);
        rider.Health.Returns(50);
        rider.Position.Returns(new Vec3(1f, 2f, 3f));
        attacker.Position.Returns(new Vec3(9f, 9f, 9f));
        attacker.MovementVelocity.Returns(new Vec2(0f, 0f));

        _sut.HandleSpiderTargetHit(attacker, target, 0);

        // damage = 75 (MaxBaseDamage, no speed, no armor) >= DamageToFall(30) -> Fall, projected from the RIDER
        // (a crit would only raise it; still Fall).
        target.Received(1).ProjectAgent(new Vec3(1f, 2f, 3f), DamageAnimation.Fall);
    }

    [TestMethod]
    public void HandleSpiderTargetHit_DeadRider_SelfFallback20Damage()
    {
        var attacker = Substitute.For<IAgentAdapter>();
        var rider = Substitute.For<IAgentAdapter>();
        var target = EnemyTarget();
        attacker.RiderAgent.Returns(rider);
        rider.IsSameTeam(target).Returns(false);
        rider.Health.Returns(0);                     // dead rider -> self-fallback, damage forced to 20
        target.Position.Returns(new Vec3(4f, 4f, 4f));
        attacker.MovementVelocity.Returns(new Vec2(0f, 15f)); // would be 50 dmg without the fallback

        _sut.HandleSpiderTargetHit(attacker, target, 0);

        // fallback: damager = target itself, damage = 20 -> Flinch (8 <= 20 < 30), projected from target.
        target.Received(1).ProjectAgent(new Vec3(4f, 4f, 4f), DamageAnimation.Flinch);
    }

    [TestMethod]
    public void HandleSpiderTargetHit_NoRider_SpiderSelfIsDamager()
    {
        var attacker = Substitute.For<IAgentAdapter>();
        var target = EnemyTarget();
        attacker.RiderAgent.Returns((IAgentAdapter)null);
        attacker.IsSameTeam(target).Returns(false);
        attacker.Health.Returns(120);
        attacker.Position.Returns(new Vec3(7f, 7f, 7f));
        attacker.MovementVelocity.Returns(new Vec2(0f, 0f));

        _sut.HandleSpiderTargetHit(attacker, target, 0);

        // riderless spider (rider died): the spider itself is the damager; 75 dmg -> Fall.
        target.Received(1).ProjectAgent(new Vec3(7f, 7f, 7f), DamageAnimation.Fall);
    }

    // ---------------------------------------------------------------------
    // SelectActionName — directional clip resolution (pure; one test per (kind, branch) cell)
    //   Pounce:     vel.Y >= ChargeVelocityThreshold(4) -> charge,  else front
    //   SideAttack: bearing >= 0 -> left (LEFT),         else right
    // ---------------------------------------------------------------------

    [TestMethod]
    public void SelectActionName_PounceLowVelocity_ReturnsFront()
        => Assert.AreEqual(SpiderConfig.PounceFrontActionName,
            _sut.SelectActionName(SpiderAttackKind.Pounce, velocityY: 0f, bearing: 0f));

    [TestMethod]
    public void SelectActionName_PounceAtChargeThreshold_ReturnsCharge()
        // Inclusive >= threshold — at exactly the charge speed it uses the running lunge.
        => Assert.AreEqual(SpiderConfig.PounceChargeActionName,
            _sut.SelectActionName(SpiderAttackKind.Pounce, velocityY: SpiderConfig.ChargeVelocityThreshold, bearing: 0f));

    [TestMethod]
    public void SelectActionName_PounceHighVelocity_ReturnsCharge()
        => Assert.AreEqual(SpiderConfig.PounceChargeActionName,
            _sut.SelectActionName(SpiderAttackKind.Pounce, velocityY: 10f, bearing: 0f));

    [TestMethod]
    public void SelectActionName_SideAttackPositiveBearing_ReturnsLeft()
        // Positive bearing = enemy on the LEFT; bearing is ignored for the velocity here.
        => Assert.AreEqual(SpiderConfig.SwingLeftActionName,
            _sut.SelectActionName(SpiderAttackKind.SideAttack, velocityY: 10f, bearing: 0.5f));

    [TestMethod]
    public void SelectActionName_SideAttackZeroBearing_ReturnsLeft()
        // Boundary: >= 0 resolves LEFT.
        => Assert.AreEqual(SpiderConfig.SwingLeftActionName,
            _sut.SelectActionName(SpiderAttackKind.SideAttack, velocityY: 0f, bearing: 0f));

    [TestMethod]
    public void SelectActionName_SideAttackNegativeBearing_ReturnsRight()
        => Assert.AreEqual(SpiderConfig.SwingRightActionName,
            _sut.SelectActionName(SpiderAttackKind.SideAttack, velocityY: 0f, bearing: -0.5f));

    // ---------------------------------------------------------------------
    // SelectArc — radial-damage arc per (kind, bearing) (pure)
    //   Pounce → forward cone (center 0, ±PounceArcHalfAngleDeg)
    //   SideAttack → forward flank: center +SideArcCenterDeg (bearing≥0=LEFT) or its negation, ±SideArcHalfAngleDeg
    // ---------------------------------------------------------------------

    [TestMethod]
    public void SelectArc_Pounce_ReturnsForwardCone()
    {
        var (center, half) = _sut.SelectArc(SpiderAttackKind.Pounce, bearing: 0.5f);
        Assert.AreEqual(0f, center);
        Assert.AreEqual(SpiderConfig.PounceArcHalfAngleDeg, half);
    }

    [TestMethod]
    public void SelectArc_PounceIgnoresBearing_AlwaysForward()
    {
        var (center, _) = _sut.SelectArc(SpiderAttackKind.Pounce, bearing: -0.9f);
        Assert.AreEqual(0f, center);
    }

    [TestMethod]
    public void SelectArc_SideAttackPositiveBearing_ReturnsLeftFlank()
    {
        var (center, half) = _sut.SelectArc(SpiderAttackKind.SideAttack, bearing: 1f);
        Assert.AreEqual(SpiderConfig.SideArcCenterDeg, center);    // + = LEFT
        Assert.AreEqual(SpiderConfig.SideArcHalfAngleDeg, half);
    }

    [TestMethod]
    public void SelectArc_SideAttackZeroBearing_ReturnsLeftFlank()
        // Boundary: >= 0 resolves LEFT (matches SelectActionName's left/right split).
        => Assert.AreEqual(SpiderConfig.SideArcCenterDeg,
            _sut.SelectArc(SpiderAttackKind.SideAttack, bearing: 0f).centerDeg);

    [TestMethod]
    public void SelectArc_SideAttackNegativeBearing_ReturnsRightFlank()
    {
        var (center, half) = _sut.SelectArc(SpiderAttackKind.SideAttack, bearing: -1f);
        Assert.AreEqual(-SpiderConfig.SideArcCenterDeg, center);   // - = RIGHT
        Assert.AreEqual(SpiderConfig.SideArcHalfAngleDeg, half);
    }

    // ---------------------------------------------------------------------
    // IsOffCooldown — pure cooldown gate (mirror ElephantAttackServiceTests)
    // ---------------------------------------------------------------------

    private static readonly DateTime Now = new(2026, 6, 15, 12, 0, 0);

    [TestMethod]
    public void IsOffCooldown_NeverFired_ReturnsTrue()
        => Assert.IsTrue(_sut.IsOffCooldown(lastFired: null, now: Now, cooldownSeconds: 5.0));

    [TestMethod]
    public void IsOffCooldown_JustFired_ReturnsFalse()
        => Assert.IsFalse(_sut.IsOffCooldown(lastFired: Now, now: Now, cooldownSeconds: 5.0));

    [TestMethod]
    public void IsOffCooldown_PartiallyElapsed_ReturnsFalse()
        => Assert.IsFalse(_sut.IsOffCooldown(lastFired: Now.AddSeconds(-2), now: Now, cooldownSeconds: 5.0));

    [TestMethod]
    public void IsOffCooldown_ExactlyAtCooldown_ReturnsTrue()
        // Inclusive ">= cooldown" — available the moment the window closes.
        => Assert.IsTrue(_sut.IsOffCooldown(lastFired: Now.AddSeconds(-5), now: Now, cooldownSeconds: 5.0));

    [TestMethod]
    public void IsOffCooldown_FullyElapsed_ReturnsTrue()
        => Assert.IsTrue(_sut.IsOffCooldown(lastFired: Now.AddSeconds(-30), now: Now, cooldownSeconds: 5.0));

    [TestMethod]
    public void IsOffCooldown_LastFiredInFuture_ReturnsFalse()
        // Clock skew / bad stamp: a future lastFired reads as ON cooldown, not off.
        => Assert.IsFalse(_sut.IsOffCooldown(lastFired: Now.AddSeconds(10), now: Now, cooldownSeconds: 5.0));

    // ---------------------------------------------------------------------
    // Construction
    // ---------------------------------------------------------------------

    [TestMethod]
    public void Constructor_ValidDependencies_DoesNotThrow()
    {
        var service = new SpiderAttackService(
            Substitute.For<IMissionAdapterFactory>(),
            Substitute.For<IModLogger>());
        Assert.IsNotNull(service);
    }
}
