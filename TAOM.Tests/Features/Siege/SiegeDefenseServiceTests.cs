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
    private IPlayerContextAdapter _playerContext;
    private IModLogger _logger;
    private SiegeDefenseService _sut;

    [TestInitialize]
    public void Setup()
    {
        _configProvider = Substitute.For<ISiegeDefenseConfigProvider>();
        _settings = Substitute.For<ISiegeDefenseSettingsProvider>();
        _playerContext = Substitute.For<IPlayerContextAdapter>();
        _logger = Substitute.For<IModLogger>();

        _settings.EnableSiegeDefenseEvents.Returns(true);
        _settings.SiegeDefenseResponseDays.Returns(3);
        _playerContext.GetPlayerKingdomId().Returns("gondor");
        _playerContext.IsUnderMercenaryService().Returns(false);

        var config = new SiegeDefenseConfig
        {
            WatchedSettlementIds = new List<string> { "town_special" },
            KingdomMessages = new Dictionary<string, KingdomSiegeMessages>
            {
                ["gondor"] = new KingdomSiegeMessages
                {
                    Title = "Gondor Calls For Aid!",
                    Body = "{attacker} besieges {settlement}! You have {days} days.",
                    AcceptButton = "For Gondor!",
                    AcceptMessage = "Ride to {settlement}!",
                    RewardMessage = "Gondor remembers! +{influence} influence, +{relation} relation."
                }
            },
            ResponseWindowDays = 3,
            RewardRelation = 5,
            RewardInfluence = 10
        };
        _configProvider.LoadConfig().Returns(config);

        _sut = new SiegeDefenseService(_configProvider, _settings, _playerContext, _logger);
    }

    // --- IsWatchedSiege: player kingdom ---

    [TestMethod]
    public void IsWatchedSiege_PlayerKingdomUnderSiege_ReturnsTrue()
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
    public void IsWatchedSiege_DifferentKingdomUnderSiege_ReturnsFalse()
    {
        // Arrange
        var siege = Substitute.For<ISiegeEventAdapter>();
        siege.DefenderFactionId.Returns("rohan");
        siege.SettlementId.Returns("town_rohan_1");
        siege.IsTown.Returns(true);

        // Act
        var result = _sut.IsWatchedSiege(siege);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsWatchedSiege_PlayerHasNoKingdom_ReturnsFalse()
    {
        // Arrange
        _playerContext.GetPlayerKingdomId().Returns("");
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
    public void IsWatchedSiege_MercenaryKingdomUnderSiege_ReturnsTrue()
    {
        // Arrange — mercenary serving gondor; Clan.Kingdom is set to gondor in both cases
        _playerContext.GetPlayerKingdomId().Returns("gondor");
        _playerContext.IsUnderMercenaryService().Returns(true);
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
    public void IsWatchedSiege_WatchedSettlementOverride_ReturnsTrue()
    {
        // Arrange — player is in a different kingdom, but settlement is explicitly watched
        _playerContext.GetPlayerKingdomId().Returns("rohan");
        var siege = Substitute.For<ISiegeEventAdapter>();
        siege.DefenderFactionId.Returns("gondor");
        siege.SettlementId.Returns("town_special");
        siege.IsTown.Returns(true);

        // Act
        var result = _sut.IsWatchedSiege(siege);

        // Assert
        Assert.IsTrue(result);
    }

    // --- IsWatchedSiege: guards ---

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
    public void OnSiegeStarted_PlayerKingdomUnderSiege_AddsToActiveEvents()
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
        siege.DefenderFactionId.Returns("rohan");
        siege.SettlementId.Returns("town_rohan_1");
        siege.SettlementName.Returns("Edoras");
        siege.AttackerName.Returns("Isengard");
        siege.IsTown.Returns(true);

        // Act
        _sut.OnSiegeStarted(siege);

        // Assert
        Assert.IsFalse(_sut.ActiveEvents.ContainsKey("town_rohan_1"));
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
        siege.DefenderFactionId.Returns("gondor");
        siege.SettlementId.Returns("town_gondor_1");
        siege.SettlementName.Returns("Minas Tirith");
        siege.AttackerName.Returns("Mordor");
        siege.IsTown.Returns(true);

        // Act
        _sut.OnSiegeStarted(siege);

        // Assert
        Assert.AreEqual("gondor", _sut.ActiveEvents["town_gondor_1"].DefenderFactionId);
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

    // --- GetMessages ---

    [TestMethod]
    public void GetMessages_KnownFactionId_ReturnsConfigMessages()
    {
        // Act
        var msgs = _sut.GetMessages("gondor");

        // Assert
        Assert.AreEqual("Gondor Calls For Aid!", msgs.Title);
        Assert.AreEqual("For Gondor!", msgs.AcceptButton);
    }

    [TestMethod]
    public void GetMessages_UnknownFactionId_ReturnsDefaultMessages()
    {
        // Act
        var msgs = _sut.GetMessages("unknown_faction");

        // Assert
        Assert.IsTrue(msgs.Title.Contains("{attacker}"));
        Assert.AreEqual("Help Defend", msgs.AcceptButton);
    }

    [TestMethod]
    public void GetMessages_EmptyFactionId_ReturnsDefaultMessages()
    {
        // Act
        var msgs = _sut.GetMessages("");

        // Assert
        Assert.AreEqual("Help Defend", msgs.AcceptButton);
    }

    // Phase 9b #132 — Reset + Snapshot/Restore for save-load + R1 singleton reset

    [TestMethod]
    public void Reset_WithActiveEvents_ClearsAll()
    {
        // Arrange — populate active events via restore (Reset target)
        var snapshot = new Dictionary<string, string>
        {
            ["town_gondor_minas_tirith"] = "gondor|72.0|1|0"
        };
        _sut.RestoreFromSave(snapshot);
        Assert.AreEqual(1, _sut.ActiveEvents.Count);

        // Act
        _sut.Reset();

        // Assert — cleared, ready for fresh campaign
        Assert.AreEqual(0, _sut.ActiveEvents.Count);
    }

    [TestMethod]
    public void Reset_EmptyState_IsNoOp()
    {
        _sut.Reset();
        Assert.AreEqual(0, _sut.ActiveEvents.Count);
    }

    [TestMethod]
    public void RestoreFromSave_NullSnapshot_ClearsAndDoesNotThrow()
    {
        // Pre-populate to ensure Restore CLEARS even when input is null
        _sut.RestoreFromSave(new Dictionary<string, string> { ["x"] = "f|1|0|0" });
        Assert.AreEqual(1, _sut.ActiveEvents.Count);

        _sut.RestoreFromSave(null);

        Assert.AreEqual(0, _sut.ActiveEvents.Count);
    }

    [TestMethod]
    public void RestoreFromSave_MalformedEntry_SkipsWithoutThrowing()
    {
        var snapshot = new Dictionary<string, string>
        {
            ["valid"] = "gondor|24.0|1|0",
            ["too_few_parts"] = "gondor|24.0",
            ["bad_hours"] = "gondor|notanumber|1|0",
            ["null_value"] = null
        };
        _sut.RestoreFromSave(snapshot);

        // Only the valid entry survives
        Assert.AreEqual(1, _sut.ActiveEvents.Count);
        Assert.IsTrue(_sut.ActiveEvents.ContainsKey("valid"));
    }

    [TestMethod]
    public void RestoreFromSave_FlagsRoundTrip_PreservesAcceptedAndRewardClaimed()
    {
        var snapshot = new Dictionary<string, string>
        {
            ["accepted_not_claimed"] = "rohan|48.0|1|0",
            ["accepted_and_claimed"] = "rohan|48.0|1|1",
            ["not_accepted"] = "rohan|48.0|0|0"
        };
        _sut.RestoreFromSave(snapshot);

        Assert.AreEqual(3, _sut.ActiveEvents.Count);
        Assert.IsTrue(_sut.ActiveEvents["accepted_not_claimed"].PlayerAccepted);
        Assert.IsFalse(_sut.ActiveEvents["accepted_not_claimed"].RewardClaimed);
        Assert.IsTrue(_sut.ActiveEvents["accepted_and_claimed"].PlayerAccepted);
        Assert.IsTrue(_sut.ActiveEvents["accepted_and_claimed"].RewardClaimed);
        Assert.IsFalse(_sut.ActiveEvents["not_accepted"].PlayerAccepted);
    }

    [TestMethod]
    public void RestoreFromSave_DefenderFactionPreserved()
    {
        _sut.RestoreFromSave(new Dictionary<string, string>
        {
            ["t"] = "gondor|12.5|1|0"
        });

        Assert.AreEqual("gondor", _sut.ActiveEvents["t"].DefenderFactionId);
    }
}
