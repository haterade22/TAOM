using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation;
using TAOM.Features.CharacterCreation.Models;
using TAOM.Features.StartupResources;

namespace TAOM.Tests.Features.CharacterCreation;

[TestClass]
public class CharacterCreationContentServiceTests
{
    private ICultureCreationDataProvider _dataProvider;
    private INarrativeDataProvider _narrativeDataProvider;
    private IRaceManager _raceManager;
    private IHeroRosterAdapter _heroRosterAdapter;
    private IEquipmentRosterProvider _equipmentRosterProvider;
    private ICareerMenuService _careerMenuService;
    private IPlayerStartupGoldService _playerStartupGoldService;
    private IPlayerEquipmentService _playerEquipmentService;
    private IModLogger _logger;
    private CharacterCreationContentService _sut;

    [TestInitialize]
    public void Setup()
    {
        _dataProvider = Substitute.For<ICultureCreationDataProvider>();
        _narrativeDataProvider = Substitute.For<INarrativeDataProvider>();
        _raceManager = Substitute.For<IRaceManager>();
        _heroRosterAdapter = Substitute.For<IHeroRosterAdapter>();
        _equipmentRosterProvider = Substitute.For<IEquipmentRosterProvider>();
        _careerMenuService = Substitute.For<ICareerMenuService>();
        _playerStartupGoldService = Substitute.For<IPlayerStartupGoldService>();
        _playerEquipmentService = Substitute.For<IPlayerEquipmentService>();
        _logger = Substitute.For<IModLogger>();

        _sut = new CharacterCreationContentService(
            _dataProvider,
            _narrativeDataProvider,
            _raceManager,
            _heroRosterAdapter,
            _equipmentRosterProvider,
            _careerMenuService,
            _playerStartupGoldService,
            _playerEquipmentService,
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

    [TestMethod]
    [DataRow("empire")]
    [DataRow("vlandia")]
    [DataRow("sturgia")]
    [DataRow("aserai")]
    [DataRow("battania")]
    [DataRow("khuzait")]
    public void SetPlayerRace_VanillaCulture_SetsHuman(string cultureId)
    {
        var cultureData = new CultureCreationData
        {
            CultureId = cultureId,
            Races = new[] { "human" }
        };
        _raceManager.GetRaceIdFromName("human").Returns(0);

        _sut.SetPlayerRace(cultureData, "hero_id");

        _heroRosterAdapter.Received(1).SetHeroRace("hero_id", 0);
    }

    [TestMethod]
    public void SetPlayerRace_NullHeroId_LogsWarningAndSkips()
    {
        var cultureData = new CultureCreationData
        {
            CultureId = "gondor",
            Races = new[] { "human" }
        };

        _sut.SetPlayerRace(cultureData, null);

        _heroRosterAdapter.DidNotReceive().SetHeroRace(Arg.Any<string>(), Arg.Any<int>());
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("null")));
    }

    [TestMethod]
    public void SetPlayerRace_FaceGenChoiceInAllowedList_PreservesChoice()
    {
        var cultureData = new CultureCreationData
        {
            CultureId = "mordor",
            Races = new[] { "uruk", "orc", "human" }
        };
        _heroRosterAdapter.GetHeroRace("main_hero").Returns(7);
        _raceManager.IsValidRaceId(7).Returns(true);
        _raceManager.GetRaceNameFromId(7).Returns("orc");

        _sut.SetPlayerRace(cultureData, "main_hero");

        _heroRosterAdapter.Received(1).SetHeroRace("main_hero", 7);
        _raceManager.DidNotReceive().GetRaceIdFromName(Arg.Any<string>());
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("preserved FaceGen selection")));
    }

    [TestMethod]
    public void SetPlayerRace_FaceGenChoiceNotAllowed_FallsBackToCultureDefault()
    {
        var cultureData = new CultureCreationData
        {
            CultureId = "erebor",
            Races = new[] { "dwarf" }
        };
        _heroRosterAdapter.GetHeroRace("main_hero").Returns(0);
        _raceManager.IsValidRaceId(0).Returns(true);
        _raceManager.GetRaceNameFromId(0).Returns("human");
        _raceManager.GetRaceIdFromName("dwarf").Returns(5);

        _sut.SetPlayerRace(cultureData, "main_hero");

        _heroRosterAdapter.Received(1).SetHeroRace("main_hero", 5);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("fell back to culture default")));
    }

    [TestMethod]
    public void SetPlayerRace_FaceGenChoiceCaseInsensitive_PreservesChoice()
    {
        var cultureData = new CultureCreationData
        {
            CultureId = "mirkwood",
            Races = new[] { "elf", "human" }
        };
        _heroRosterAdapter.GetHeroRace("h").Returns(11);
        _raceManager.IsValidRaceId(11).Returns(true);
        _raceManager.GetRaceNameFromId(11).Returns("ELF");

        _sut.SetPlayerRace(cultureData, "h");

        _heroRosterAdapter.Received(1).SetHeroRace("h", 11);
    }

    [TestMethod]
    public void SetPlayerRace_InvalidFaceGenRaceId_DoesNotPreserve_FallsBackToCultureDefault()
    {
        // Codex review #N (2026-05-06) regression: an invalid race ID would be silently coerced
        // by GetRaceNameFromId to "human", and if the culture allowed "human", the system would
        // preserve a value the player never picked. IsValidRaceId must gate the FaceGen branch.
        var cultureData = new CultureCreationData
        {
            CultureId = "mordor",
            Races = new[] { "uruk", "orc", "human" }
        };
        _heroRosterAdapter.GetHeroRace("h").Returns(9999);
        _raceManager.IsValidRaceId(9999).Returns(false);
        _raceManager.GetRaceIdFromName("uruk").Returns(3);

        _sut.SetPlayerRace(cultureData, "h");

        // Must fall back to Races[0] = "uruk" (id 3), NOT preserve the coerced "human".
        _heroRosterAdapter.Received(1).SetHeroRace("h", 3);
        _raceManager.DidNotReceive().GetRaceNameFromId(9999);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("fell back to culture default")));
    }
}
