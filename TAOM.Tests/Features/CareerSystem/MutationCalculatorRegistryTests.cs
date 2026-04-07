using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Mutations;

namespace TAOM.Tests.Features.CareerSystem;

[TestClass]
public class MutationCalculatorRegistryTests
{
    private MutationCalculatorRegistry _registry;
    private ICareerHeroAdapter _hero;
    private IModLogger _logger;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _registry = new MutationCalculatorRegistry(_logger);
        BuiltInCalculators.RegisterAll(_registry);

        _hero = Substitute.For<ICareerHeroAdapter>();
        _hero.Level.Returns(15);
        _hero.GetSkillValue("Leadership").Returns(120);
    }

    [TestMethod]
    public void FlatCalculator_AddsValue()
    {
        var p = new MutationParams(new Dictionary<string, string> { ["value"] = "5" });
        var result = _registry.Calculate("flat", 10f, _hero, p);
        Assert.AreEqual(15f, result, 0.001f);
    }

    [TestMethod]
    public void SkillScaling_ScalesBySkillValue()
    {
        var p = new MutationParams(new Dictionary<string, string>
        {
            ["skill"] = "Leadership",
            ["factor"] = "0.02"
        });
        var result = _registry.Calculate("skill_scaling", 8f, _hero, p);
        Assert.AreEqual(8f + 120 * 0.02f, result, 0.001f);
    }

    [TestMethod]
    public void LevelScaling_ScalesByHeroLevel()
    {
        var p = new MutationParams(new Dictionary<string, string> { ["factor"] = "0.5" });
        var result = _registry.Calculate("level_scaling", 10f, _hero, p);
        Assert.AreEqual(10f + 15 * 0.5f, result, 0.001f);
    }

    [TestMethod]
    public void ReplaceCalculator_ReplacesValue()
    {
        var p = new MutationParams(new Dictionary<string, string> { ["value"] = "99" });
        var result = _registry.Calculate("replace", 10f, _hero, p);
        Assert.AreEqual(99f, result, 0.001f);
    }

    [TestMethod]
    public void MultiplyCalculator_MultipliesValue()
    {
        var p = new MutationParams(new Dictionary<string, string> { ["factor"] = "2.5" });
        var result = _registry.Calculate("multiply", 10f, _hero, p);
        Assert.AreEqual(25f, result, 0.001f);
    }

    [TestMethod]
    public void UnknownCalculator_ReturnsBaseValue()
    {
        var p = new MutationParams(new Dictionary<string, string>());
        var result = _registry.Calculate("nonexistent", 42f, _hero, p);
        Assert.AreEqual(42f, result, 0.001f);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("nonexistent")));
    }

    [TestMethod]
    public void HasCalculator_Registered_ReturnsTrue()
    {
        Assert.IsTrue(_registry.HasCalculator("flat"));
    }

    [TestMethod]
    public void HasCalculator_Unregistered_ReturnsFalse()
    {
        Assert.IsFalse(_registry.HasCalculator("nonexistent"));
    }
}
