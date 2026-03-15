using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation;
using TAOM.Features.CharacterCreation.Hooks;
using TAOM.Features.CharacterCreation.Models;

namespace TAOM.Tests.Features.CharacterCreation;

[TestClass]
public class GetRaceNamesHookTests
{
    private ICultureCreationDataProvider _dataProvider;
    private IModLogger _logger;
    private GetRaceNamesHook _sut;

    private string _selectedCultureId;
    private readonly string[] _allRaces = { "human", "elf", "dwarf", "uruk", "goblin", "orc", "uruk_hai", "berserker", "pale_uruk", "dg_uruk" };

    [TestInitialize]
    public void Setup()
    {
        _dataProvider = Substitute.For<ICultureCreationDataProvider>();
        _logger = Substitute.For<IModLogger>();
        _selectedCultureId = null;

        _sut = new GetRaceNamesHook(
            _dataProvider,
            _logger,
            () => _selectedCultureId);
    }

    [TestMethod]
    public void FilterRaceNames_NoCultureSelected_ReturnsAllRaces()
    {
        _selectedCultureId = null;

        var result = _sut.FilterRaceNames(_allRaces);

        CollectionAssert.AreEqual(_allRaces, result);
    }

    [TestMethod]
    public void FilterRaceNames_SingleRaceCulture_ReturnsOnlyThatRace()
    {
        _selectedCultureId = "erebor";
        _dataProvider.GetCultureData("erebor").Returns(new CultureCreationData
        {
            CultureId = "erebor",
            Races = new[] { "dwarf" }
        });

        var result = _sut.FilterRaceNames(_allRaces);

        CollectionAssert.AreEqual(new[] { "dwarf" }, result);
    }

    [TestMethod]
    public void FilterRaceNames_MultipleRaceCulture_ReturnsAllAllowedRaces()
    {
        _selectedCultureId = "mordor";
        _dataProvider.GetCultureData("mordor").Returns(new CultureCreationData
        {
            CultureId = "mordor",
            Races = new[] { "uruk", "goblin", "orc", "human" }
        });

        var result = _sut.FilterRaceNames(_allRaces);

        CollectionAssert.AreEqual(new[] { "human", "uruk", "goblin", "orc" }, result);
    }

    [TestMethod]
    public void FilterRaceNames_CultureWithNoRaceData_ReturnsAllRaces()
    {
        _selectedCultureId = "unknown";
        _dataProvider.GetCultureData("unknown").Returns((CultureCreationData)null);

        var result = _sut.FilterRaceNames(_allRaces);

        CollectionAssert.AreEqual(_allRaces, result);
    }

    [TestMethod]
    public void FilterRaceNames_CultureWithEmptyRaces_ReturnsAllRaces()
    {
        _selectedCultureId = "test";
        _dataProvider.GetCultureData("test").Returns(new CultureCreationData
        {
            CultureId = "test",
            Races = Array.Empty<string>()
        });

        var result = _sut.FilterRaceNames(_allRaces);

        CollectionAssert.AreEqual(_allRaces, result);
    }

    [TestMethod]
    public void FilterRaceNames_HumanOnlyCulture_ReturnsOnlyHuman()
    {
        _selectedCultureId = "gondor";
        _dataProvider.GetCultureData("gondor").Returns(new CultureCreationData
        {
            CultureId = "gondor",
            Races = new[] { "human" }
        });

        var result = _sut.FilterRaceNames(_allRaces);

        CollectionAssert.AreEqual(new[] { "human" }, result);
    }

    [TestMethod]
    public void FilterRaceNames_CaseInsensitiveMatching()
    {
        _selectedCultureId = "rivendell";
        _dataProvider.GetCultureData("rivendell").Returns(new CultureCreationData
        {
            CultureId = "rivendell",
            Races = new[] { "Elf", "Human" }
        });

        var result = _sut.FilterRaceNames(new[] { "human", "elf", "dwarf" });

        CollectionAssert.AreEqual(new[] { "human", "elf" }, result);
    }

    [TestMethod]
    public void FilteredIndex_MappedThroughRaceManager_ReturnsCorrectGlobalId()
    {
        _selectedCultureId = "erebor";
        _dataProvider.GetCultureData("erebor").Returns(new CultureCreationData
        {
            CultureId = "erebor",
            Races = new[] { "dwarf" }
        });

        var filtered = _sut.FilterRaceNames(_allRaces);
        var selectedFilteredIndex = 0;
        var selectedRaceName = filtered[selectedFilteredIndex];

        var faceGenAdapter = Substitute.For<IFaceGenAdapter>();
        faceGenAdapter.GetRaceNames().Returns(_allRaces);
        var raceManager = new RaceManager(Substitute.For<IModLogger>(), faceGenAdapter);

        var globalId = raceManager.GetRaceIdFromName(selectedRaceName);

        Assert.AreEqual("dwarf", selectedRaceName);
        Assert.AreEqual(2, globalId, "Filtered index 0 ('dwarf') should map to global race ID 2, not 0 ('human')");
    }
}
