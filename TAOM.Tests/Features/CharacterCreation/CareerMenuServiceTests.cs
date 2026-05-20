using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CharacterCreation;
using TAOM.Features.CharacterCreation.Models;

namespace TAOM.Tests.Features.CharacterCreation;

[TestClass]
public class CareerMenuServiceTests
{
    private ICareerRegistry _registry;
    private ICareerMenuDataProvider _dataProvider;
    private ICareerArchetypeService _archetypeService;
    private IEquipmentRosterProvider _equipmentRosterProvider;
    private IPlayerEquipmentService _playerEquipmentService;
    private ICareerStartingEquipmentService _careerStartingEquipmentService;
    private IModLogger _logger;
    private CareerMenuService _sut;

    [TestInitialize]
    public void Setup()
    {
        _registry = Substitute.For<ICareerRegistry>();
        _dataProvider = Substitute.For<ICareerMenuDataProvider>();
        _archetypeService = Substitute.For<ICareerArchetypeService>();
        _equipmentRosterProvider = Substitute.For<IEquipmentRosterProvider>();
        _playerEquipmentService = Substitute.For<IPlayerEquipmentService>();
        _careerStartingEquipmentService = Substitute.For<ICareerStartingEquipmentService>();
        _logger = Substitute.For<IModLogger>();
        _sut = new CareerMenuService(_registry, _dataProvider, _archetypeService, _equipmentRosterProvider, _playerEquipmentService, _careerStartingEquipmentService, _logger);
    }

    [TestMethod]
    public void SelectedCareerStringId_InitiallyNull()
    {
        Assert.IsNull(_sut.SelectedCareerStringId);
    }

    [TestMethod]
    public void BuildCareerMenuOptions_CreatesOneOptionPerCareerPlusFallback()
    {
        SetupCareers(
            MakeCareer("ranger_of_ithilien", "Ranger of Ithilien", "An elite scout.", "gondor", "empire_w"),
            MakeCareer("captain_of_osgiliath", "Captain of Osgiliath", "A mariner.", "gondor", "empire_w"),
            MakeCareer("black_uruk_captain", "Black Uruk Captain", "An iron champion.", "mordor", "empire_s"));
        SetupOptionData("ranger_of_ithilien", new[] { "Bow", "Scouting" }, "Cunning");
        SetupOptionData("captain_of_osgiliath", new[] { "Leadership", "OneHanded" }, "Social");
        SetupOptionData("black_uruk_captain", new[] { "TwoHanded", "Athletics" }, "Vigor");

        var options = _sut.BuildCareerMenuOptions();

        // 3 career options + 1 fallback "No specialization"
        Assert.AreEqual(4, options.Count);
        Assert.AreEqual("taom_career_none", options[3].StringId);
    }

    [TestMethod]
    public void BuildCareerMenuOptions_SkipsCareersWithoutCCData()
    {
        SetupCareers(
            MakeCareer("ranger_of_ithilien", "Ranger of Ithilien", "An elite scout.", "gondor"),
            MakeCareer("unknown_career", "Unknown", "No CC data.", "gondor"));
        SetupOptionData("ranger_of_ithilien", new[] { "Bow" }, "Cunning");
        // No CC data for "unknown_career"

        var options = _sut.BuildCareerMenuOptions();

        // 1 career option + 1 fallback
        Assert.AreEqual(2, options.Count);
        Assert.AreEqual("taom_career_ranger_of_ithilien", options[0].StringId);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("unknown_career")));
    }

    [TestMethod]
    public void BuildCareerMenuOptions_SetsCorrectStringId()
    {
        SetupCareers(MakeCareer("ranger_of_ithilien", "Ranger of Ithilien", "Desc.", "gondor"));
        SetupOptionData("ranger_of_ithilien", new[] { "Bow" }, "Cunning");

        var options = _sut.BuildCareerMenuOptions();

        Assert.AreEqual("taom_career_ranger_of_ithilien", options[0].StringId);
    }

    [TestMethod]
    public void BuildCareerMenuOptions_UsesDisplayNameFromRegistry()
    {
        SetupCareers(MakeCareer("ranger_of_ithilien", "Ranger of Ithilien", "An elite scout.", "gondor"));
        SetupOptionData("ranger_of_ithilien", new[] { "Bow" }, "Cunning");

        var options = _sut.BuildCareerMenuOptions();

        Assert.AreEqual("Ranger of Ithilien", options[0].Text.ToString());
    }

    [TestMethod]
    public void BuildCareerMenuOptions_UsesDescriptionFromRegistry()
    {
        SetupCareers(MakeCareer("ranger_of_ithilien", "Ranger of Ithilien", "An elite scout.", "gondor"));
        SetupOptionData("ranger_of_ithilien", new[] { "Bow" }, "Cunning");

        var options = _sut.BuildCareerMenuOptions();

        Assert.AreEqual("An elite scout.", options[0].DescriptionText.ToString());
    }

    [TestMethod]
    public void OnCareerOptionSelected_StoresCareerStringId()
    {
        _sut.OnCareerOptionSelected("ranger_of_ithilien", manager: null);

        Assert.AreEqual("ranger_of_ithilien", _sut.SelectedCareerStringId);
    }

    [TestMethod]
    public void OnCareerOptionSelected_OverwritesPreviousSelection()
    {
        _sut.OnCareerOptionSelected("ranger_of_ithilien", manager: null);
        _sut.OnCareerOptionSelected("captain_of_osgiliath", manager: null);

        Assert.AreEqual("captain_of_osgiliath", _sut.SelectedCareerStringId);
    }

    [TestMethod]
    public void BuildCareerMenuOptions_NoCareersInRegistry_ReturnsFallbackOnly()
    {
        _registry.GetAllCareers().Returns(new List<CareerDefinition>());

        var options = _sut.BuildCareerMenuOptions();

        // Even with no careers, the fallback option is always present
        Assert.AreEqual(1, options.Count);
        Assert.AreEqual("taom_career_none", options[0].StringId);
    }

    [TestMethod]
    public void GetEligibleCultureIds_ReturnsCareerCultures()
    {
        var career = MakeCareer("ranger_of_ithilien", "Ranger", "Desc.", "gondor", "empire_w");

        var cultures = _sut.GetEligibleCultureIds(career);

        Assert.AreEqual(2, cultures.Count);
        Assert.IsTrue(cultures.Contains("gondor"));
        Assert.IsTrue(cultures.Contains("empire_w"));
    }

    [TestMethod]
    public void FallbackOption_SetsSelectedCareerToNull()
    {
        // Simulate selecting a career then switching to fallback
        _sut.OnCareerOptionSelected("ranger_of_ithilien", manager: null);
        Assert.AreEqual("ranger_of_ithilien", _sut.SelectedCareerStringId);

        // Fallback clears the selection
        _sut.OnCareerOptionSelected(null, manager: null);
        Assert.IsNull(_sut.SelectedCareerStringId);
    }

    [TestMethod]
    public void FreshService_HasNullSelectedCareer()
    {
        var freshService = new CareerMenuService(_registry, _dataProvider, _archetypeService, _equipmentRosterProvider, _playerEquipmentService, _careerStartingEquipmentService, _logger);
        Assert.IsNull(freshService.SelectedCareerStringId);
    }

    // --- Live equipment-preview update on selection ---
    //
    // The happy path requires a non-null CharacterCreationManager with a populated
    // CurrentMenu — both are sealed TaleWorlds types that can't be unit-mocked. Those
    // paths are covered by in-game smoke testing. Below are the unit-testable branches.

    [TestMethod]
    public void OnCareerOptionSelected_NullManager_StoresIdButSkipsPreviewLookups()
    {
        _sut.OnCareerOptionSelected("ranger_of_ithilien", manager: null);

        Assert.AreEqual("ranger_of_ithilien", _sut.SelectedCareerStringId);
        _archetypeService.DidNotReceiveWithAnyArgs().TryGetArchetype(default, out _);
        _equipmentRosterProvider.DidNotReceiveWithAnyArgs().GetRoster(default);
        _playerEquipmentService.DidNotReceiveWithAnyArgs().ApplyPlayerStartingEquipment(default, default, default, default);
        _careerStartingEquipmentService.DidNotReceiveWithAnyArgs().ApplyCareerStartingEquipment(default, default, default, default);
    }

    [TestMethod]
    public void OnCareerOptionSelected_NullManager_DoesNotThrow()
    {
        // Defensive: short-circuit at manager?.CurrentMenu == null guard.
        _sut.OnCareerOptionSelected("knight_of_belfalas", manager: null);
        // No assertion needed beyond "did not throw".
    }

    // --- Helpers ---

    private void SetupCareers(params CareerDefinition[] careers)
    {
        _registry.GetAllCareers().Returns(new List<CareerDefinition>(careers));
    }

    private void SetupOptionData(string careerId, string[] skills, string attribute)
    {
        var data = new CareerMenuOptionDefinition
        {
            CareerStringId = careerId,
            Skills = skills,
            Attribute = attribute,
            FocusToAdd = 1,
            SkillLevelToAdd = 10,
            AttributeLevelToAdd = 1
        };
        _dataProvider.GetOptionForCareer(careerId).Returns(data);
    }

    private static CareerDefinition MakeCareer(string id, string displayName, string description, params string[] cultureIds)
    {
        return new CareerDefinition(
            id,
            displayName,
            description,
            $"career_{id}_portrait",
            $"{id}_ability",
            0,
            $"{id}_root",
            new List<string>(cultureIds),
            new List<string>());
    }
}
