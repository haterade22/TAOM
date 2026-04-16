using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Abilities.Executors;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem.Abilities;

[TestClass]
public class RangedAbilityExecutorTests
{
    private RangedAbilityExecutor _sut;
    private IAbilityExecutionContext _context;
    private ICareerConfigProvider _configProvider;

    [TestInitialize]
    public void SetUp()
    {
        _configProvider = Substitute.For<ICareerConfigProvider>();
        _configProvider.GetAbilityTuning().Returns(new AbilityTuningConfig(
            InfantryTuning.Default,
            new RangedTuning(15f, 20f, 20f),
            CavalryTuning.Default));

        _sut = new RangedAbilityExecutor("ranger_of_ithilien", _configProvider);

        _context = Substitute.For<IAbilityExecutionContext>();
        _context.Duration.Returns(8f);
    }

    [TestMethod]
    public void CareerId_ReturnsConstructorValue()
    {
        Assert.AreEqual("ranger_of_ithilien", _sut.CareerId);
    }

    [TestMethod]
    public void Execute_CallsApplySpeedBuff()
    {
        _sut.Execute(_context);

        _context.Received(1).ApplySpeedBuff(Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Execute_CallsApplyDamageBuff()
    {
        _sut.Execute(_context);

        _context.Received(1).ApplyDamageBuff(Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Execute_CallsApplyDrawSpeedBuff()
    {
        _sut.Execute(_context);

        _context.Received(1).ApplyDrawSpeedBuff(Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Execute_UsesDurationFromContext()
    {
        _context.Duration.Returns(12f);

        _sut.Execute(_context);

        _context.Received(1).ApplySpeedBuff(Arg.Any<float>(), 12f);
        _context.Received(1).ApplyDamageBuff(Arg.Any<float>(), 12f);
        _context.Received(1).ApplyDrawSpeedBuff(Arg.Any<float>(), 12f);
    }

    [TestMethod]
    public void Execute_UsesTuningSpeedBonus_ConvertedToMultiplier()
    {
        float captured = 0f;
        _context.When(c => c.ApplySpeedBuff(Arg.Any<float>(), Arg.Any<float>()))
            .Do(ci => captured = (float)ci[0]);

        _sut.Execute(_context);

        // 15 / 100 = 0.15; multiplier = 1 + 0.15 = 1.15
        Assert.AreEqual(1.15f, captured, 0.001f);
    }

    [TestMethod]
    public void Execute_UsesTuningRangedDamageBonus_ConvertedToMultiplier()
    {
        float captured = 0f;
        _context.When(c => c.ApplyDamageBuff(Arg.Any<float>(), Arg.Any<float>()))
            .Do(ci => captured = (float)ci[0]);

        _sut.Execute(_context);

        // 20 / 100 = 0.20; multiplier = 1 + 0.20 = 1.20
        Assert.AreEqual(1.20f, captured, 0.001f);
    }

    [TestMethod]
    public void Execute_UsesTuningDrawSpeedBonus_ConvertedToMultiplierDelta()
    {
        _sut.Execute(_context);

        // 20 / 100 = 0.20 as multiplier delta
        _context.Received(1).ApplyDrawSpeedBuff(0.20f, Arg.Any<float>());
    }
}
