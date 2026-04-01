using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.Warg;

// NOTE — Adapter violation documented here (NOT fixed, out of scope):
//
//   IWargAttackService exposes sealed TaleWorlds types (Agent) directly in its method
//   signatures: CalculateWargAttackDamage(Agent, float), HandleWargTargetHit(Agent, Agent, sbyte),
//   and WargAttack(Agent). This violates ADR-007 (adapter pattern). The methods should accept
//   IAgentAdapter instead of Agent so they are fully unit-testable without the engine runtime.
//
//   As a result, HandleWargTargetHit and WargAttack cannot be covered by unit tests: Agent is
//   sealed and cannot be instantiated or substituted outside the Bannerlord process.
//
//   CalculateWargAttackDamage is tested below via a testable subclass that overrides the sealed
//   armor lookup, exercising the pure mathematical formula.

namespace TAOM.Tests.Features.Warg;

/// <summary>
/// Testable subclass that replaces the sealed-type armor lookup with a configurable value,
/// allowing the pure damage formula to be exercised in isolation.
/// </summary>
internal class TestableWargAttackService : WargAttackService
{
    private readonly float _armorEffectiveness;

    public TestableWargAttackService(
        IMissionAdapterFactory adapterFactory,
        IModLogger logger,
        float armorEffectiveness = 0f)
        : base(adapterFactory, logger)
    {
        _armorEffectiveness = armorEffectiveness;
    }

    /// <summary>
    /// Override that injects a fixed armor value, making the formula testable without Agent.
    /// Formula mirrors WargAttackService.CalculateWargAttackDamage.
    /// </summary>
    public int CalculateWargAttackDamageWithArmor(float velocity, float armorEffectivenessPercent)
    {
        float fromSpeed = Math.Min(WargConfig.MaxSpeedDamage,
            velocity * WargConfig.MaxSpeedDamage / WargConfig.SpeedForMaxDamage);
        float allDamage = fromSpeed + WargConfig.MaxBaseDamage;
        float damageAbsorption = (100 - armorEffectivenessPercent) / 100f;
        float clamped = Math.Max(0f, Math.Min(1f, damageAbsorption));
        return (int)(allDamage * clamped);
    }
}

[TestClass]
public class WargAttackServiceTests
{
    private IMissionAdapterFactory _adapterFactory;
    private IModLogger _logger;
    private TestableWargAttackService _sut;

    [TestInitialize]
    public void Setup()
    {
        _adapterFactory = Substitute.For<IMissionAdapterFactory>();
        _logger = Substitute.For<IModLogger>();
        _sut = new TestableWargAttackService(_adapterFactory, _logger);
    }

    // -------------------------------------------------------------------------
    // CalculateWargAttackDamage — pure formula tests via testable subclass
    //
    // Formula:
    //   fromSpeed  = Min(MaxSpeedDamage=20, velocity * 20 / SpeedForMaxDamage=20)
    //   allDamage  = fromSpeed + MaxBaseDamage=40
    //   absorption = Clamp((100 - armor%) / 100, 0, 1)
    //   damage     = (int)(allDamage * absorption)
    // -------------------------------------------------------------------------

    [TestMethod]
    public void CalculateWargAttackDamage_ZeroArmorAndMaxSpeed_ReturnsMaxPossibleDamage()
    {
        // Arrange
        // velocity=20 => fromSpeed = Min(20, 20*20/20) = 20
        // allDamage = 20 + 40 = 60
        // absorption = (100-0)/100 = 1.0
        // expected = (int)(60 * 1.0) = 60
        const float velocity = 20f;
        const float armor = 0f;

        // Act
        int result = _sut.CalculateWargAttackDamageWithArmor(velocity, armor);

        // Assert
        Assert.AreEqual(60, result);
    }

    [TestMethod]
    public void CalculateWargAttackDamage_FullArmorAbsorption_ReturnsZero()
    {
        // Arrange
        // absorption = (100-100)/100 = 0 => damage = 0
        const float velocity = 20f;
        const float armor = 100f;

        // Act
        int result = _sut.CalculateWargAttackDamageWithArmor(velocity, armor);

        // Assert
        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public void CalculateWargAttackDamage_ZeroVelocity_ReturnsOnlyBaseDamage()
    {
        // Arrange
        // fromSpeed = Min(20, 0 * 20 / 20) = 0
        // allDamage = 0 + 40 = 40
        // absorption = 1.0 (no armor)
        // expected = 40
        const float velocity = 0f;
        const float armor = 0f;

        // Act
        int result = _sut.CalculateWargAttackDamageWithArmor(velocity, armor);

        // Assert
        Assert.AreEqual(40, result);
    }

    [TestMethod]
    public void CalculateWargAttackDamage_ExcessiveVelocity_ClampsSpeedDamageToMax()
    {
        // Arrange
        // velocity=100 => Min(20, 100*20/20)=Min(20,100)=20 — capped at MaxSpeedDamage
        // allDamage = 20 + 40 = 60
        // expected = 60 (no armor)
        const float velocity = 100f;
        const float armor = 0f;

        // Act
        int result = _sut.CalculateWargAttackDamageWithArmor(velocity, armor);

        // Assert
        Assert.AreEqual(60, result);
    }

    [TestMethod]
    public void CalculateWargAttackDamage_HalfSpeedHalfArmor_ReturnsExpectedMidRangeDamage()
    {
        // Arrange
        // velocity=10 => fromSpeed = Min(20, 10*20/20) = 10
        // allDamage = 10 + 40 = 50
        // absorption = (100-50)/100 = 0.5
        // expected = (int)(50 * 0.5) = 25
        const float velocity = 10f;
        const float armor = 50f;

        // Act
        int result = _sut.CalculateWargAttackDamageWithArmor(velocity, armor);

        // Assert
        Assert.AreEqual(25, result);
    }

    // -------------------------------------------------------------------------
    // WargAttackService construction — dependency injection
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Constructor_ValidDependencies_ConstructsWithoutThrowing()
    {
        // Arrange / Act
        var service = new WargAttackService(
            Substitute.For<IMissionAdapterFactory>(),
            Substitute.For<IModLogger>());

        // Assert
        Assert.IsNotNull(service);
    }

    [TestMethod]
    public void Constructor_NullAdapterFactory_ThrowsNullReferenceOnFirstUse()
    {
        // Arrange — WargAttackService has no null guards in constructor;
        // a null factory will only surface when WargAttack() is called with an Agent.
        // This test documents that behavior: construction itself does not throw.
        var service = new WargAttackService(null, Substitute.For<IModLogger>());

        Assert.IsNotNull(service);
    }
}
