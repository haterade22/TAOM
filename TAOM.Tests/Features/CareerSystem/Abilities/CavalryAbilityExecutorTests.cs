using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Abilities.Executors;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem.Abilities;

[TestClass]
public class CavalryAbilityExecutorTests
{
    private CavalryAbilityExecutor _sut;
    private IAbilityExecutionContext _context;
    private ICareerConfigProvider _configProvider;

    [TestInitialize]
    public void SetUp()
    {
        _configProvider = Substitute.For<ICareerConfigProvider>();
        _configProvider.GetAbilityTuning().Returns(new AbilityTuningConfig(
            InfantryTuning.Default,
            RangedTuning.Default,
            new CavalryTuning(20f, 25f, 10f)));

        _sut = new CavalryAbilityExecutor("knight_of_belfalas", _configProvider);

        _context = Substitute.For<IAbilityExecutionContext>();
        _context.Duration.Returns(8f);
    }

    [TestMethod]
    public void CareerId_ReturnsConstructorValue()
    {
        Assert.AreEqual("knight_of_belfalas", _sut.CareerId);
    }

    [TestMethod]
    public void Execute_CallsApplyMountSpeedBuff()
    {
        _sut.Execute(_context);

        _context.Received(1).ApplyMountSpeedBuff(Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Execute_CallsApplyChargeDamageBuff()
    {
        _sut.Execute(_context);

        _context.Received(1).ApplyChargeDamageBuff(Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Execute_CallsApplyDamageBuff()
    {
        _sut.Execute(_context);

        _context.Received(1).ApplyDamageBuff(Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Execute_UsesDurationFromContext()
    {
        _context.Duration.Returns(12f);

        _sut.Execute(_context);

        _context.Received(1).ApplyMountSpeedBuff(Arg.Any<float>(), 12f);
        _context.Received(1).ApplyChargeDamageBuff(Arg.Any<float>(), 12f);
        _context.Received(1).ApplyDamageBuff(Arg.Any<float>(), 12f);
    }

    [TestMethod]
    public void Execute_UsesTuningMountSpeedBonus_ConvertedToMultiplierDelta()
    {
        _sut.Execute(_context);

        // 20 / 100 = 0.20 as multiplier delta
        _context.Received(1).ApplyMountSpeedBuff(0.20f, Arg.Any<float>());
    }

    [TestMethod]
    public void Execute_UsesTuningChargeDamageBonus_ConvertedToMultiplierDelta()
    {
        _sut.Execute(_context);

        // 25 / 100 = 0.25 as multiplier delta
        _context.Received(1).ApplyChargeDamageBuff(0.25f, Arg.Any<float>());
    }

    [TestMethod]
    public void Execute_UsesTuningDamageBonus()
    {
        float captured = 0f;
        _context.When(c => c.ApplyDamageBuff(Arg.Any<float>(), Arg.Any<float>()))
            .Do(ci => captured = (float)ci[0]);

        _sut.Execute(_context);

        // DamageBuff expects multiplier: 1 + damageBonus/100 = 1.10
        Assert.AreEqual(1.10f, captured, 0.001f);
    }
}
