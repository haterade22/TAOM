using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation;
using TAOM.Features.CharacterCreation.Models;

namespace TAOM.Tests.Features.CharacterCreation;

[TestClass]
public class CharacterCreationContentServiceTests
{
    private ICultureCreationDataProvider _dataProvider;
    private INarrativeDataProvider _narrativeDataProvider;
    private IRaceManager _raceManager;
    private IHeroRosterAdapter _heroRosterAdapter;
    private IModLogger _logger;
    private CharacterCreationContentService _sut;

    [TestInitialize]
    public void Setup()
    {
        _dataProvider = Substitute.For<ICultureCreationDataProvider>();
        _narrativeDataProvider = Substitute.For<INarrativeDataProvider>();
        _raceManager = Substitute.For<IRaceManager>();
        _heroRosterAdapter = Substitute.For<IHeroRosterAdapter>();
        _logger = Substitute.For<IModLogger>();

        _sut = new CharacterCreationContentService(
            _dataProvider,
            _narrativeDataProvider,
            _raceManager,
            _heroRosterAdapter,
            _logger);
    }

    [TestMethod]
    public void SetPlayerRace_UsesFirstRaceFromCulture()
    {
        var cultureData = new CultureCreationData
        {
            CultureId = "mordor",
            Races = new[] { "uruk", "goblin", "orc", "human" }
        };
        _raceManager.GetRaceIdFromName("uruk").Returns(3);

        _sut.SetPlayerRace(cultureData, "main_hero_id");

        _heroRosterAdapter.Received(1).SetHeroRace("main_hero_id", 3);
    }

    [TestMethod]
    public void SetPlayerRace_SingleRace_SetsCorrectly()
    {
        var cultureData = new CultureCreationData
        {
            CultureId = "gondor",
            Races = new[] { "human" }
        };
        _raceManager.GetRaceIdFromName("human").Returns(0);

        _sut.SetPlayerRace(cultureData, "player_hero");

        _heroRosterAdapter.Received(1).SetHeroRace("player_hero", 0);
    }

    [TestMethod]
    public void SetPlayerRace_EmptyRaces_DefaultsToHuman()
    {
        var cultureData = new CultureCreationData
        {
            CultureId = "gondor",
            Races = System.Array.Empty<string>()
        };
        _raceManager.GetRaceIdFromName("human").Returns(0);

        _sut.SetPlayerRace(cultureData, "hero_id");

        _heroRosterAdapter.Received(1).SetHeroRace("hero_id", 0);
    }

    [TestMethod]
    public void SetPlayerRace_NullRaces_DefaultsToHuman()
    {
        var cultureData = new CultureCreationData
        {
            CultureId = "gondor",
            Races = null
        };
        _raceManager.GetRaceIdFromName("human").Returns(0);

        _sut.SetPlayerRace(cultureData, "hero_id");

        _heroRosterAdapter.Received(1).SetHeroRace("hero_id", 0);
    }

    [TestMethod]
    public void SetPlayerRace_LogsRaceAssignment()
    {
        var cultureData = new CultureCreationData
        {
            CultureId = "erebor",
            Races = new[] { "dwarf" }
        };
        _raceManager.GetRaceIdFromName("dwarf").Returns(5);

        _sut.SetPlayerRace(cultureData, "hero_id");

        _logger.Received().LogInfo(Arg.Is<string>(s =>
            s.Contains("dwarf") && s.Contains("5")));
    }
}
