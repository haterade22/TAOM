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
        _context.Radius.Returns(50f);
    }

    [TestMethod]
    public void CareerId_ReturnsConstructorValue()
    {
        Assert.AreEqual("knight_of_belfalas", _sut.CareerId);
    }

    [TestMethod]
    public void Execute_CallsApplyAllyCavalryBuff_Once()
    {
        _sut.Execute(_context);

        _context.Received(1).ApplyAllyCavalryBuff(
            Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>(),
            Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Execute_UsesTuningMountSpeedBonus_DividedBy100()
    {
        _sut.Execute(_context);

        // 20 / 100 = 0.20 multiplier delta
        _context.Received(1).ApplyAllyCavalryBuff(
            0.20f, Arg.Any<float>(), Arg.Any<float>(),
            Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Execute_UsesTuningChargeDamageBonus_DividedBy100()
    {
        _sut.Execute(_context);

        // 25 / 100 = 0.25 multiplier delta
        _context.Received(1).ApplyAllyCavalryBuff(
            Arg.Any<float>(), 0.25f, Arg.Any<float>(),
            Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Execute_UsesTuningDamageBonus_DividedBy100()
    {
        _sut.Execute(_context);

        // 10 / 100 = 0.10 multiplier delta
        _context.Received(1).ApplyAllyCavalryBuff(
            Arg.Any<float>(), Arg.Any<float>(), 0.10f,
            Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Execute_UsesContextRadius()
    {
        _context.Radius.Returns(65f);

        _sut.Execute(_context);

        _context.Received(1).ApplyAllyCavalryBuff(
            Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>(),
            65f, Arg.Any<float>());
    }

    [TestMethod]
    public void Execute_UsesContextDuration()
    {
        _context.Duration.Returns(12f);

        _sut.Execute(_context);

        _context.Received(1).ApplyAllyCavalryBuff(
            Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>(),
            Arg.Any<float>(), 12f);
    }
}
