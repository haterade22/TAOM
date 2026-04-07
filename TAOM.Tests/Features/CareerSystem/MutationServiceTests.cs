using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CareerSystem.Mutations;

namespace TAOM.Tests.Features.CareerSystem;

[TestClass]
public class MutationServiceTests
{
    private MutationService _service;
    private IMutationCalculatorRegistry _calcRegistry;
    private ICareerDataService _dataService;
    private ICareerRegistry _registry;
    private ICareerHeroAdapter _hero;
    private IModLogger _logger;

    private static readonly AbilityTemplateData TestTemplate = new AbilityTemplateData
    {
        Id = "rally_horde",
        Duration = 8f,
        Radius = 10f,
        MaxCharge = 100f
    };

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _calcRegistry = new MutationCalculatorRegistry(_logger);
        BuiltInCalculators.RegisterAll(_calcRegistry);

        _service = new MutationService(_calcRegistry, _logger);
        _dataService = new CareerDataService();
        _registry = Substitute.For<ICareerRegistry>();

        _hero = Substitute.For<ICareerHeroAdapter>();
        _hero.StringId.Returns("hero1");
        _hero.Level.Returns(10);
        _hero.GetSkillValue("Leadership").Returns(120);
    }

    [TestMethod]
    public void MutateAbility_NoMutations_ReturnsSameValues()
    {
        _dataService.SetCareer("hero1", "warboss");
        _registry.GetCareer("warboss").Returns(new CareerDefinition(
            "warboss", "", "", "", "rally_horde", ChargeType.Kills, 100, 0, "wb_root",
            new List<string>(), new List<string>()));
        _registry.GetChoice("wb_root").Returns(new CareerChoiceDefinition(
            "wb_root", "", ChoiceType.Passive, "", "", null, new List<MutationDefinition>()));

        var result = _service.MutateAbility(TestTemplate, _hero, _dataService, _registry);

        Assert.AreEqual(8f, result.Duration, 0.001f);
        Assert.AreEqual(10f, result.Radius, 0.001f);
    }

    [TestMethod]
    public void MutateAbility_FlatMutation_ModifiesProperty()
    {
        _dataService.SetCareer("hero1", "warboss");
        _dataService.TryAddChoice("hero1", "choice1", 10);

        _registry.GetCareer("warboss").Returns(new CareerDefinition(
            "warboss", "", "", "", "rally_horde", ChargeType.Kills, 100, 0, "wb_root",
            new List<string>(), new List<string>()));
        _registry.GetChoice("wb_root").Returns(new CareerChoiceDefinition(
            "wb_root", "", ChoiceType.Passive, "", "", null, new List<MutationDefinition>()));

        _registry.GetChoice("choice1").Returns(new CareerChoiceDefinition(
            "choice1", "g1", ChoiceType.Keystone, "", "", null,
            new List<MutationDefinition>
            {
                new MutationDefinition("rally_horde", "Radius", "flat", OperationType.Add,
                    new Dictionary<string, string> { ["value"] = "3" })
            }));

        var result = _service.MutateAbility(TestTemplate, _hero, _dataService, _registry);

        Assert.AreEqual(13f, result.Radius, 0.001f);
    }

    [TestMethod]
    public void MutateAbility_SkillScaling_ScalesByHeroSkill()
    {
        _dataService.SetCareer("hero1", "warboss");

        _registry.GetCareer("warboss").Returns(new CareerDefinition(
            "warboss", "", "", "", "rally_horde", ChargeType.Kills, 100, 0, "wb_root",
            new List<string>(), new List<string>()));

        _registry.GetChoice("wb_root").Returns(new CareerChoiceDefinition(
            "wb_root", "", ChoiceType.Passive, "", "", null,
            new List<MutationDefinition>
            {
                new MutationDefinition("rally_horde", "Duration", "skill_scaling", OperationType.Add,
                    new Dictionary<string, string> { ["skill"] = "Leadership", ["factor"] = "0.02" })
            }));

        var result = _service.MutateAbility(TestTemplate, _hero, _dataService, _registry);

        // 8f + (120 * 0.02) = 8 + 2.4 = 10.4
        Assert.AreEqual(10.4f, result.Duration, 0.001f);
    }

    [TestMethod]
    public void MutateAbility_DoesNotModifyOriginal()
    {
        _dataService.SetCareer("hero1", "warboss");
        _dataService.TryAddChoice("hero1", "choice1", 10);

        _registry.GetCareer("warboss").Returns(new CareerDefinition(
            "warboss", "", "", "", "rally_horde", ChargeType.Kills, 100, 0, "wb_root",
            new List<string>(), new List<string>()));
        _registry.GetChoice("wb_root").Returns(new CareerChoiceDefinition(
            "wb_root", "", ChoiceType.Passive, "", "", null, new List<MutationDefinition>()));
        _registry.GetChoice("choice1").Returns(new CareerChoiceDefinition(
            "choice1", "g1", ChoiceType.Keystone, "", "", null,
            new List<MutationDefinition>
            {
                new MutationDefinition("rally_horde", "Duration", "flat", OperationType.Add,
                    new Dictionary<string, string> { ["value"] = "5" })
            }));

        _service.MutateAbility(TestTemplate, _hero, _dataService, _registry);

        Assert.AreEqual(8f, TestTemplate.Duration, 0.001f);
    }

    [TestMethod]
    public void MutateAbility_NullTemplate_ReturnsNull()
    {
        Assert.IsNull(_service.MutateAbility(null, _hero, _dataService, _registry));
    }
}
