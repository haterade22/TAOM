using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CharacterCreation;

namespace TAOM.Tests.Features.CharacterCreation;

[TestClass]
public class NarrativeHorseGuardServiceTests
{
    private IEquipmentRosterProvider _rosterProvider;
    private NarrativeHorseGuardService _sut;

    [TestInitialize]
    public void Setup()
    {
        _rosterProvider = Substitute.For<IEquipmentRosterProvider>();
        _sut = new NarrativeHorseGuardService(_rosterProvider);
    }

    [TestMethod]
    public void TryBuildNoHorseArgs_RosterNotFound_ReturnsNull()
    {
        // Arrange
        _rosterProvider.HasHorse("player_char_creation_gondor_retainer_m").Returns((bool?)null);

        // Act
        var result = _sut.TryBuildNoHorseArgs("gondor", "retainer", isFemale: false, characterId: "player_youth_character", age: 17);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void TryBuildNoHorseArgs_RosterHasHorse_ReturnsNull()
    {
        // Arrange
        _rosterProvider.HasHorse("player_char_creation_mordor_warrior_f").Returns((bool?)true);

        // Act
        var result = _sut.TryBuildNoHorseArgs("mordor", "warrior", isFemale: true, characterId: "player_youth_character", age: 17);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void TryBuildNoHorseArgs_RosterHasNoHorse_ReturnsArgs()
    {
        // Arrange
        _rosterProvider.HasHorse("player_char_creation_erebor_smith_m").Returns((bool?)false);

        // Act
        var result = _sut.TryBuildNoHorseArgs("erebor", "smith", isFemale: false, characterId: "player_youth_character", age: 17);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("player_youth_character", result.CharacterId);
        Assert.AreEqual(17, result.Age);
        Assert.AreEqual("player_char_creation_erebor_smith_m", result.EquipmentId);
        Assert.AreEqual("act_childhood_schooled", result.AnimationId);
        Assert.AreEqual("spawnpoint_player_1", result.SpawnPointEntityId);
        Assert.IsTrue(result.IsHuman);
        Assert.IsFalse(result.IsFemale);
    }

    [TestMethod]
    public void TryBuildNoHorseArgs_RosterHasNoHorse_Female_SetsIsFemale()
    {
        // Arrange
        _rosterProvider.HasHorse("player_char_creation_mirkwood_ranger_f").Returns((bool?)false);

        // Act
        var result = _sut.TryBuildNoHorseArgs("mirkwood", "ranger", isFemale: true, characterId: "player_adulthood_character", age: 20);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("player_adulthood_character", result.CharacterId);
        Assert.AreEqual(20, result.Age);
        Assert.AreEqual("player_char_creation_mirkwood_ranger_f", result.EquipmentId);
        Assert.IsTrue(result.IsFemale);
    }

    [TestMethod]
    public void TryBuildNoHorseArgs_AgeSelectionStage_PassesStartingAge()
    {
        // Arrange
        _rosterProvider.HasHorse("player_char_creation_erebor_miner_m").Returns((bool?)false);

        // Act
        var result = _sut.TryBuildNoHorseArgs("erebor", "miner", isFemale: false, characterId: "player_age_selection_character", age: 25);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("player_age_selection_character", result.CharacterId);
        Assert.AreEqual(25, result.Age);
    }

    [TestMethod]
    public void TryBuildNoHorseArgs_CallsProviderWithCorrectRosterId()
    {
        // Arrange
        _rosterProvider.HasHorse(Arg.Any<string>()).Returns((bool?)null);

        // Act
        _sut.TryBuildNoHorseArgs("gondor", "knight", isFemale: false, characterId: "player_youth_character", age: 17);

        // Assert
        _rosterProvider.Received(1).HasHorse("player_char_creation_gondor_knight_m");
    }

    [TestMethod]
    public void TryBuildNoHorseArgs_CallsProviderWithCorrectRosterId_Female()
    {
        // Arrange
        _rosterProvider.HasHorse(Arg.Any<string>()).Returns((bool?)null);

        // Act
        _sut.TryBuildNoHorseArgs("rohan", "rider", isFemale: true, characterId: "player_youth_character", age: 17);

        // Assert
        _rosterProvider.Received(1).HasHorse("player_char_creation_rohan_rider_f");
    }
}
