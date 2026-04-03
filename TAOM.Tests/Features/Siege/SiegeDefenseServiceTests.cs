using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Siege;
using TAOM.Features.Siege.Models;

namespace TAOM.Tests.Features.Siege;

[TestClass]
public class SiegeDefenseServiceTests
{
    private ISiegeDefenseConfigProvider _configProvider;
    private ISiegeDefenseSettingsProvider _settings;
    private IModLogger _logger;
    private SiegeDefenseService _sut;

    [TestInitialize]
    public void Setup()
    {
        _configProvider = Substitute.For<ISiegeDefenseConfigProvider>();
        _settings = Substitute.For<ISiegeDefenseSettingsProvider>();
        _logger = Substitute.For<IModLogger>();

        _settings.EnableSiegeDefenseEvents.Returns(true);
        _settings.SiegeDefenseResponseDays.Returns(3);

        var config = new SiegeDefenseConfig
        {
            WatchedFactionIds = new List<string> { "gondor", "rohan" },
            WatchedSettlementIds = new List<string> { "town_special" },
            ResponseWindowDays = 3,
            RewardRelation = 5,
            RewardInfluence = 10
        };
        _configProvider.LoadConfig().Returns(config);

        _sut = new SiegeDefenseService(_configProvider, _settings, _logger);
    }

    // --- IsWatchedSiege ---

    [TestMethod]
    public void IsWatchedSiege_WatchedFaction_ReturnsTrue()
    {
        // Arrange
        var siege = Substitute.For<ISiegeEventAdapter>();
        siege.DefenderFactionId.Returns("gondor");
        siege.SettlementId.Returns("town_gondor_1");
        siege.IsTown.Returns(true);

        // Act
        var result = _sut.IsWatchedSiege(siege);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsWatchedSiege_WatchedSettlement_ReturnsTrue()
    {
        // Arrange
        var siege = Substitute.For<ISiegeEventAdapter>();
        siege.DefenderFactionId.Returns("someother_faction");
        siege.SettlementId.Returns("town_special");
        siege.IsTown.Returns(true);

        // Act
        var result = _sut.IsWatchedSiege(siege);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsWatchedSiege_UnwatchedFactionAndSettlement_ReturnsFalse()
    {
        // Arrange
        var siege = Substitute.For<ISiegeEventAdapter>();
        siege.DefenderFactionId.Returns("mordor");
        siege.SettlementId.Returns("town_mordor_1");
        siege.IsTown.Returns(true);

        // Act
        var result = _sut.IsWatchedSiege(siege);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsWatchedSiege_NotATown_ReturnsFalse()
    {
        // Arrange
        var siege = Substitute.For<ISiegeEventAdapter>();
        siege.DefenderFactionId.Returns("gondor");
        siege.SettlementId.Returns("castle_gondor_1");
        siege.IsTown.Returns(false);

        // Act
        var result = _sut.IsWatchedSiege(siege);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsWatchedSiege_DisabledBySettings_ReturnsFalse()
    {
        // Arrange
        _settings.EnableSiegeDefenseEvents.Returns(false);
        var siege = Substitute.For<ISiegeEventAdapter>();
        siege.DefenderFactionId.Returns("gondor");
        siege.SettlementId.Returns("town_gondor_1");
        siege.IsTown.Returns(true);

        // Act
        var result = _sut.IsWatchedSiege(siege);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsWatchedSiege_AlreadyTracked_ReturnsFalse()
    {
        // Arrange
        var siege = Substitute.For<ISiegeEventAdapter>();
        siege.DefenderFactionId.Returns("gondor");
        siege.SettlementId.Returns("town_gondor_1");
        siege.SettlementName.Returns("Minas Tirith");
        siege.AttackerName.Returns("Mordor");
        siege.IsTown.Returns(true);

        // Act — first call adds to active events
        _sut.OnSiegeStarted(siege);
        var result = _sut.IsWatchedSiege(siege);

        // Assert — already tracked, so returns false to suppress re-fire
        Assert.IsFalse(result);
    }

    // --- OnSiegeStarted ---

    [TestMethod]
    public void OnSiegeStarted_WatchedFaction_AddsToActiveEvents()
    {
        // Arrange
        var siege = Substitute.For<ISiegeEventAdapter>();
        siege.DefenderFactionId.Returns("gondor");
        siege.SettlementId.Returns("town_gondor_1");
        siege.SettlementName.Returns("Minas Tirith");
        siege.AttackerName.Returns("Mordor");
        siege.IsTown.Returns(true);

        // Act
        _sut.OnSiegeStarted(siege);

        // Assert
        Assert.IsTrue(_sut.ActiveEvents.ContainsKey("town_gondor_1"));
    }

    [TestMethod]
    public void OnSiegeStarted_UnwatchedFaction_DoesNotAddToActiveEvents()
    {
        // Arrange
        var siege = Substitute.For<ISiegeEventAdapter>();
        siege.DefenderFactionId.Returns("mordor");
        siege.SettlementId.Returns("town_mordor_1");
        siege.SettlementName.Returns("Barad-dur");
        siege.AttackerName.Returns("Gondor");
        siege.IsTown.Returns(true);

        // Act
        _sut.OnSiegeStarted(siege);

        // Assert
        Assert.IsFalse(_sut.ActiveEvents.ContainsKey("town_mordor_1"));
    }

    [TestMethod]
    public void OnSiegeStarted_CalledTwiceSameSettlement_OnlyTrackedOnce()
    {
        // Arrange
        var siege = Substitute.For<ISiegeEventAdapter>();
        siege.DefenderFactionId.Returns("gondor");
        siege.SettlementId.Returns("town_gondor_1");
        siege.SettlementName.Returns("Minas Tirith");
        siege.AttackerName.Returns("Mordor");
        siege.IsTown.Returns(true);

        // Act
        _sut.OnSiegeStarted(siege);
        _sut.OnSiegeStarted(siege);

        // Assert
        Assert.AreEqual(1, _sut.ActiveEvents.Count);
    }

    // --- OnSiegeEnded ---

    [TestMethod]
    public void OnSiegeEnded_TrackedSettlement_RemovesFromActiveEvents()
    {
        // Arrange
        var siege = Substitute.For<ISiegeEventAdapter>();
        siege.DefenderFactionId.Returns("gondor");
        siege.SettlementId.Returns("town_gondor_1");
        siege.SettlementName.Returns("Minas Tirith");
        siege.AttackerName.Returns("Mordor");
        siege.IsTown.Returns(true);
        _sut.OnSiegeStarted(siege);

        // Act
        _sut.OnSiegeEnded("town_gondor_1");

        // Assert
        Assert.IsFalse(_sut.ActiveEvents.ContainsKey("town_gondor_1"));
    }

    [TestMethod]
    public void OnSiegeEnded_UntrackedSettlement_DoesNotThrow()
    {
        // Arrange — no sieges started

        // Act + Assert — should not throw
        _sut.OnSiegeEnded("town_unknown_99");
    }

    // --- Config loading ---

    [TestMethod]
    public void Constructor_LoadsConfigOnCreation()
    {
        // Assert — configProvider was called during construction
        _configProvider.Received(1).LoadConfig();
    }

    [TestMethod]
    public void OnSiegeStarted_ActiveEventHasCorrectDefenderFaction()
    {
        // Arrange
        var siege = Substitute.For<ISiegeEventAdapter>();
        siege.DefenderFactionId.Returns("rohan");
        siege.SettlementId.Returns("town_rohan_1");
        siege.SettlementName.Returns("Edoras");
        siege.AttackerName.Returns("Isengard");
        siege.IsTown.Returns(true);

        // Act
        _sut.OnSiegeStarted(siege);

        // Assert
        Assert.AreEqual("rohan", _sut.ActiveEvents["town_rohan_1"].DefenderFactionId);
    }

    [TestMethod]
    public void OnSiegeStarted_ActiveEventIsNotAcceptedByDefault()
    {
        // Arrange
        var siege = Substitute.For<ISiegeEventAdapter>();
        siege.DefenderFactionId.Returns("gondor");
        siege.SettlementId.Returns("town_gondor_1");
        siege.SettlementName.Returns("Minas Tirith");
        siege.AttackerName.Returns("Mordor");
        siege.IsTown.Returns(true);

        // Act
        _sut.OnSiegeStarted(siege);

        // Assert
        Assert.IsFalse(_sut.ActiveEvents["town_gondor_1"].PlayerAccepted);
    }

    [TestMethod]
    public void OnSiegeStarted_ActiveEventRewardNotClaimedByDefault()
    {
        // Arrange
        var siege = Substitute.For<ISiegeEventAdapter>();
        siege.DefenderFactionId.Returns("gondor");
        siege.SettlementId.Returns("town_gondor_1");
        siege.SettlementName.Returns("Minas Tirith");
        siege.AttackerName.Returns("Mordor");
        siege.IsTown.Returns(true);

        // Act
        _sut.OnSiegeStarted(siege);

        // Assert
        Assert.IsFalse(_sut.ActiveEvents["town_gondor_1"].RewardClaimed);
    }
}
