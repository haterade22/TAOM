using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Abilities.Executors;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem.Abilities;

[TestClass]
public class InfantryAbilityExecutorTests
{
    private InfantryAbilityExecutor _sut;
    private IAbilityExecutionContext _context;
    private ICareerConfigProvider _configProvider;

    [TestInitialize]
    public void SetUp()
    {
        _configProvider = Substitute.For<ICareerConfigProvider>();
        _configProvider.GetAbilityTuning().Returns(new AbilityTuningConfig(
            GlobalTuning.Default,
            new InfantryTuning(15f, 10f, 50f),
            RangedTuning.Default,
            CavalryTuning.Default));

        _sut = new InfantryAbilityExecutor("captain_of_osgiliath", _configProvider);

        _context = Substitute.For<IAbilityExecutionContext>();
        _context.Duration.Returns(8f);
        _context.Radius.Returns(50f);
    }

    [TestMethod]
    public void CareerId_ReturnsConstructorValue()
    {
        Assert.AreEqual("captain_of_osgiliath", _sut.CareerId);
    }

    [TestMethod]
    public void Execute_CallsApplyAllyBuff()
    {
        _sut.Execute(_context);

        _context.Received(1).ApplyAllyBuff(
            Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Execute_UsesDurationFromContext()
    {
        _context.Duration.Returns(12f);

        _sut.Execute(_context);

        _context.Received(1).ApplyAllyBuff(
            Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>(), 12f);
    }

    [TestMethod]
    public void Execute_UsesTuningDamageBonus_ConvertedToMultiplierDelta()
    {
        _sut.Execute(_context);

        // 15f / 100f = 0.15f (multiplier delta matching stat model convention)
        _context.Received(1).ApplyAllyBuff(
            0.15f, Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Execute_UsesTuningDamageReduction_ConvertedToMultiplierDelta()
    {
        _sut.Execute(_context);

        // 10f / 100f = 0.10f (multiplier delta matching stat model convention)
        _context.Received(1).ApplyAllyBuff(
            Arg.Any<float>(), 0.10f, Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Execute_UsesContextRadius_NotTuningRadius()
    {
        // context.Radius comes from the mutated ability template, respecting choice-tree upgrades
        _context.Radius.Returns(65f);

        _sut.Execute(_context);

        _context.Received(1).ApplyAllyBuff(
            Arg.Any<float>(), Arg.Any<float>(), 65f, Arg.Any<float>());
    }

    [TestMethod]
    public void Execute_DifferentCareerId_StillWorks()
    {
        var sut = new InfantryAbilityExecutor("ironguard", _configProvider);

        Assert.AreEqual("ironguard", sut.CareerId);

        sut.Execute(_context);
        _context.Received(1).ApplyAllyBuff(
            Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>());
    }
}
