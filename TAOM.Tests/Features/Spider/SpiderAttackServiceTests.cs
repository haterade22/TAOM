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

    [TestMethod]
    public void CalculateSpiderBiteDamage_ZeroVelocityZeroArmor_ReturnsBaseDamage()
    {
        var target = Substitute.For<IAgentAdapter>();
        int result = _sut.CalculateSpiderBiteDamage(target, velocity: 0f, armorEffectivenessPercent: 0f);
        // fromSpeed=0, allDamage=35, absorption=1.0, damage=35
        Assert.AreEqual(35, result);
    }

    [TestMethod]
    public void CalculateSpiderBiteDamage_MaxVelocityZeroArmor_ReturnsCappedDamage()
    {
        var target = Substitute.For<IAgentAdapter>();
        int result = _sut.CalculateSpiderBiteDamage(target, velocity: 15f, armorEffectivenessPercent: 0f);
        // fromSpeed=15, allDamage=50, absorption=1.0, damage=50
        Assert.AreEqual(50, result);
    }

    [TestMethod]
    public void CalculateSpiderBiteDamage_ExcessiveVelocity_ClampsSpeedDamageToMax()
    {
        var target = Substitute.For<IAgentAdapter>();
        int result = _sut.CalculateSpiderBiteDamage(target, velocity: 100f, armorEffectivenessPercent: 0f);
        // fromSpeed = Min(15, 100) = 15 (capped)
        Assert.AreEqual(50, result);
    }

    [TestMethod]
    public void CalculateSpiderBiteDamage_FullArmor_ReturnsZero()
    {
        var target = Substitute.For<IAgentAdapter>();
        int result = _sut.CalculateSpiderBiteDamage(target, velocity: 15f, armorEffectivenessPercent: 100f);
        // absorption = 0 → damage = 0
        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public void CalculateSpiderBiteDamage_HalfArmor_ReturnsHalvedDamage()
    {
        var target = Substitute.For<IAgentAdapter>();
        int result = _sut.CalculateSpiderBiteDamage(target, velocity: 5f, armorEffectivenessPercent: 50f);
        // fromSpeed=5, allDamage=40, absorption=0.5, damage=20
        Assert.AreEqual(20, result);
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
        _sut.SpiderAttack(spider: null);
    }

    [TestMethod]
    public void SpiderAttack_InactiveSpider_DoesNotInvokeCustomAttack()
    {
        var spider = Substitute.For<IAgentAdapter>();
        spider.IsActive().Returns(false);

        _sut.SpiderAttack(spider);

        spider.DidNotReceive().CustomAttack(
            Arg.Any<ActionIndexCache>(),
            Arg.Any<System.Collections.Generic.List<sbyte>>(),
            Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>(),
            Arg.Any<bool>(),
            Arg.Any<System.Action<IAgentAdapter, IAgentAdapter, sbyte>>(),
            Arg.Any<System.Action>());
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

        // damage = 35 (base, no speed, no armor) >= DamageToFall(30) -> Fall, projected from the RIDER.
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

        // riderless spider (rider died): the spider itself is the damager; 35 dmg -> Fall.
        target.Received(1).ProjectAgent(new Vec3(7f, 7f, 7f), DamageAnimation.Fall);
    }

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
