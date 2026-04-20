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
        _context.Radius.Returns(50f);
    }

    [TestMethod]
    public void CareerId_ReturnsConstructorValue()
    {
        Assert.AreEqual("ranger_of_ithilien", _sut.CareerId);
    }

    [TestMethod]
    public void Execute_CallsApplyAllyRangedBuff_Once()
    {
        _sut.Execute(_context);

        _context.Received(1).ApplyAllyRangedBuff(
            Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>(),
            Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Execute_UsesTuningSpeedBonus_DividedBy100()
    {
        _sut.Execute(_context);

        // 15 / 100 = 0.15 multiplier delta
        _context.Received(1).ApplyAllyRangedBuff(
            0.15f, Arg.Any<float>(), Arg.Any<float>(),
            Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Execute_UsesTuningRangedDamageBonus_DividedBy100()
    {
        _sut.Execute(_context);

        // 20 / 100 = 0.20 multiplier delta
        _context.Received(1).ApplyAllyRangedBuff(
            Arg.Any<float>(), 0.20f, Arg.Any<float>(),
            Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Execute_UsesTuningDrawSpeedBonus_DividedBy100()
    {
        _sut.Execute(_context);

        // 20 / 100 = 0.20 multiplier delta
        _context.Received(1).ApplyAllyRangedBuff(
            Arg.Any<float>(), Arg.Any<float>(), 0.20f,
            Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Execute_UsesContextRadius()
    {
        _context.Radius.Returns(65f);

        _sut.Execute(_context);

        _context.Received(1).ApplyAllyRangedBuff(
            Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>(),
            65f, Arg.Any<float>());
    }

    [TestMethod]
    public void Execute_UsesContextDuration()
    {
        _context.Duration.Returns(12f);

        _sut.Execute(_context);

        _context.Received(1).ApplyAllyRangedBuff(
            Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>(),
            Arg.Any<float>(), 12f);
    }
}
