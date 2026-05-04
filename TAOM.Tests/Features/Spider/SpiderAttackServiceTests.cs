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
