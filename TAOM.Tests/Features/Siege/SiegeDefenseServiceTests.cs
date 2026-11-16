using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Siege;
using TAOM.Features.Siege.Models;
using TaleWorlds.Library;

namespace TAOM.Tests.Features.Siege;

[TestClass]
[TestCategory("RequiresGame")]
public class SiegeDefenseServiceTests
{
    // The defaults as the service must key them (#1385): taom_module_strings.xml registers each key.
    private const string DefaultTitle = "{=taom_siege_defense_default_title}{attacker} is besieging {settlement}!";
    private const string DefaultBody = "{=taom_siege_defense_default_body}Will you answer the call to defend? You have {days} days to reach the settlement.";
    private const string DefaultAcceptButton = "{=taom_siege_defense_default_accept_button}Help Defend";
    private const string DefaultAcceptMessage = "{=taom_siege_defense_default_accept_message}You have pledged to defend {settlement}. Ride now!";
    private const string DefaultRewardMessage = "{=taom_siege_defense_default_reward_message}You answered the call! +{influence} influence, +{relation} relation.";

    private ISiegeDefenseConfigProvider _configProvider;
    private ISiegeDefenseSettingsProvider _settings;
    private IPlayerContextAdapter _playerContext;
    private IModLogger _logger;
    private ICoopSessionProvider _coopSession;
    private ITextLocalizerAdapter _text;
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
            KingdomMessages = new Dictionary<string, KingdomSiegeMessages?>
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

        // Authority by default: every pre-existing assertion in this class describes
        // singleplayer / host behaviour. The client path is pinned separately.
        _coopSession = Substitute.For<ICoopSessionProvider>();
        _coopSession.IsAuthority.Returns(true);
        _coopSession.IsCoopClient.Returns(false);
        _text = Substitute.For<ITextLocalizerAdapter>();
        _sut = new SiegeDefenseService(_configProvider, _settings, _playerContext, _logger, _coopSession, _text);
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
    public void GetMessages_KnownFactionId_KeysEachConfigMessageByKingdomAndField()
    {
        // Act
        var msgs = _sut.GetMessages("gondor");

        // Assert
        Assert.AreEqual("{=taom_siege_defense_gondor_title}Gondor Calls For Aid!", msgs.Title);
        Assert.AreEqual("{=taom_siege_defense_gondor_body}{attacker} besieges {settlement}! You have {days} days.", msgs.Body);
        Assert.AreEqual("{=taom_siege_defense_gondor_accept_button}For Gondor!", msgs.AcceptButton);
        Assert.AreEqual("{=taom_siege_defense_gondor_accept_message}Ride to {settlement}!", msgs.AcceptMessage);
        Assert.AreEqual("{=taom_siege_defense_gondor_reward_message}Gondor remembers! +{influence} influence, +{relation} relation.", msgs.RewardMessage);
    }

    [TestMethod]
    public void GetMessages_UnknownFactionId_ReturnsDefaultMessages()
    {
        // Act
        var msgs = _sut.GetMessages("unknown_faction");

        // Assert
        Assert.AreEqual(DefaultTitle, msgs.Title);
        Assert.AreEqual(DefaultBody, msgs.Body);
        Assert.AreEqual(DefaultAcceptButton, msgs.AcceptButton);
        Assert.AreEqual(DefaultAcceptMessage, msgs.AcceptMessage);
        Assert.AreEqual(DefaultRewardMessage, msgs.RewardMessage);
    }

    [TestMethod]
    public void GetMessages_EmptyFactionId_ReturnsDefaultMessages()
    {
        // Act
        var msgs = _sut.GetMessages("");

        // Assert
        Assert.AreEqual(DefaultAcceptButton, msgs.AcceptButton);
    }

    // --- GetMessages: partial or null entries in siege_defense_config.json (#660) ---

    private SiegeDefenseService CreateSutFromJson(string json, out SiegeDefenseConfig config)
    {
        // Same deserializer SiegeDefenseConfigProvider uses, so a missing key arrives as null.
        config = JsonConvert.DeserializeObject<SiegeDefenseConfig>(json)
            ?? throw new AssertFailedException("test JSON deserialized to null");
        var provider = Substitute.For<ISiegeDefenseConfigProvider>();
        provider.LoadConfig().Returns(config);
        return new SiegeDefenseService(provider, _settings, _playerContext, _logger, _coopSession, _text);
    }

    [TestMethod]
    public void GetMessages_EntryWithMissingKeys_FallsBackPerFieldToDefaults()
    {
        // Arrange
        var sut = CreateSutFromJson(
            "{\"KingdomMessages\":{\"rohan\":{\"Title\":\"Rohan Rides!\",\"Body\":\"The horns of {settlement}.\"}}}",
            out _);

        // Act
        var msgs = sut.GetMessages("rohan");

        // Assert
        Assert.AreEqual("{=taom_siege_defense_rohan_title}Rohan Rides!", msgs.Title);
        Assert.AreEqual("{=taom_siege_defense_rohan_body}The horns of {settlement}.", msgs.Body);
        Assert.AreEqual(DefaultAcceptButton, msgs.AcceptButton);
        Assert.AreEqual(DefaultAcceptMessage, msgs.AcceptMessage);
        Assert.AreEqual(DefaultRewardMessage, msgs.RewardMessage);
    }

    [TestMethod]
    public void GetMessages_EntryWithEmptyStrings_FallsBackPerFieldToDefaults()
    {
        // Arrange
        var sut = CreateSutFromJson(
            "{\"KingdomMessages\":{\"rohan\":{\"Title\":\"\",\"Body\":\"\",\"AcceptButton\":\"\"," +
            "\"AcceptMessage\":\"\",\"RewardMessage\":\"Rohan remembers.\"}}}",
            out _);

        // Act
        var msgs = sut.GetMessages("rohan");

        // Assert
        Assert.AreEqual(DefaultTitle, msgs.Title);
        Assert.AreEqual(DefaultBody, msgs.Body);
        Assert.AreEqual(DefaultAcceptButton, msgs.AcceptButton);
        Assert.AreEqual(DefaultAcceptMessage, msgs.AcceptMessage);
        Assert.AreEqual("{=taom_siege_defense_rohan_reward_message}Rohan remembers.", msgs.RewardMessage);
    }

    [TestMethod]
    public void GetMessages_EntryIsJsonNull_ReturnsDefaults()
    {
        // Arrange
        var sut = CreateSutFromJson("{\"KingdomMessages\":{\"rohan\":null}}", out _);

        // Act
        var msgs = sut.GetMessages("rohan");

        // Assert
        Assert.IsNotNull(msgs);
        Assert.AreEqual(DefaultTitle, msgs.Title);
        Assert.AreEqual(DefaultAcceptButton, msgs.AcceptButton);
        Assert.AreEqual(DefaultRewardMessage, msgs.RewardMessage);
    }

    [TestMethod]
    public void GetMessages_PartialEntryResultMutatedByCaller_DefaultsAndConfigEntryUnchanged()
    {
        // Arrange
        var sut = CreateSutFromJson("{\"KingdomMessages\":{\"rohan\":{\"Title\":\"Rohan Rides!\"}}}", out var config);

        // Act
        var merged = sut.GetMessages("rohan");
        merged.AcceptButton = "mutated by a caller";
        var defaults = sut.GetMessages("unknown_faction");
        var again = sut.GetMessages("rohan");

        // Assert
        Assert.AreEqual(DefaultTitle, defaults.Title);
        Assert.AreEqual(DefaultAcceptButton, defaults.AcceptButton);
        Assert.AreEqual(DefaultAcceptButton, again.AcceptButton);
        var entry = config.KingdomMessages["rohan"];
        Assert.IsNotNull(entry);
        Assert.IsNull(entry.AcceptButton);
    }

    [TestMethod]
    public void GetMessages_DefaultsResultMutatedByCaller_LaterLookupsStillGetDefaults()
    {
        // Arrange: an unknown kingdom, a JSON-null entry and an empty id all take the defaults-only path
        var sut = CreateSutFromJson("{\"KingdomMessages\":{\"rohan\":null}}", out _);

        // Act
        var unknown = sut.GetMessages("unknown_faction");
        unknown.Title = "mutated by a caller";
        unknown.AcceptButton = "mutated by a caller";
        var nullEntry = sut.GetMessages("rohan");
        nullEntry.RewardMessage = "mutated by a caller";
        var unknownAgain = sut.GetMessages("unknown_faction");
        var nullEntryAgain = sut.GetMessages("rohan");
        var emptyId = _sut.GetMessages("");
        emptyId.AcceptButton = "mutated by a caller";
        var otherService = _sut.GetMessages("");

        // Assert
        Assert.AreNotSame(unknown, unknownAgain);
        Assert.AreEqual(DefaultTitle, unknownAgain.Title);
        Assert.AreEqual(DefaultAcceptButton, unknownAgain.AcceptButton);
        Assert.AreEqual(DefaultAcceptButton, nullEntryAgain.AcceptButton);
        Assert.AreEqual(DefaultRewardMessage, nullEntryAgain.RewardMessage);
        Assert.AreNotSame(emptyId, otherService);
        Assert.AreEqual(DefaultAcceptButton, otherService.AcceptButton);
    }

    // --- The popup's text goes through the localizer under its key (#1385) ---

    private static ISiegeEventAdapter Siege(string defenderFactionId, string settlementId)
    {
        var siege = Substitute.For<ISiegeEventAdapter>();
        siege.DefenderFactionId.Returns(defenderFactionId);
        siege.SettlementId.Returns(settlementId);
        siege.SettlementName.Returns("Minas Tirith");
        siege.AttackerName.Returns("Mordor");
        siege.IsTown.Returns(true);
        return siege;
    }

    /// <summary>The popup OnSiegeStarted shows; the fake localizer marks each string it was handed with "L:".</summary>
    private InquiryData? ShowPopup(ISiegeEventAdapter siege)
    {
        _text.Localize(Arg.Any<string>()).Returns(call => "L:" + call.ArgAt<string>(0));
        _text.Localize(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns(call => "L:" + call.ArgAt<string>(0));
        InquiryData? shown = null;
        Action<InquiryData, bool, bool> capture = (data, _, _) => shown = data;
        InformationManager.OnShowInquiry += capture;
        try
        {
            _sut.OnSiegeStarted(siege);
        }
        finally
        {
            InformationManager.OnShowInquiry -= capture;
        }
        return shown;
    }

    [TestMethod]
    public void OnSiegeStarted_ConfiguredKingdom_ShowsEachStringLocalizedUnderItsKingdomKey()
    {
        var popup = ShowPopup(Siege("gondor", "town_gondor_1"));

        Assert.IsNotNull(popup, "OnSiegeStarted showed no popup.");
        Assert.AreEqual("L:{=taom_siege_defense_gondor_title}Gondor Calls For Aid!", popup.TitleText);
        Assert.AreEqual("L:{=taom_siege_defense_gondor_body}{attacker} besieges {settlement}! You have {days} days.", popup.Text);
        Assert.AreEqual("L:{=taom_siege_defense_gondor_accept_button}For Gondor!", popup.AffirmativeText);
        Assert.AreEqual("L:{=taom_siege_defense_ignore}Ignore", popup.NegativeText);
    }

    [TestMethod]
    public void OnSiegeStarted_KingdomWithNoEntry_ShowsTheDefaultKeys()
    {
        var popup = ShowPopup(Siege("unknown_faction", "town_special"));

        Assert.IsNotNull(popup, "OnSiegeStarted showed no popup.");
        Assert.AreEqual("L:" + DefaultTitle, popup.TitleText);
        Assert.AreEqual("L:" + DefaultBody, popup.Text);
        Assert.AreEqual("L:" + DefaultAcceptButton, popup.AffirmativeText);
        Assert.AreEqual("L:{=taom_siege_defense_ignore}Ignore", popup.NegativeText);
    }

    [TestMethod]
    public void OnSiegeStarted_GivesTheSiegeToTheTextAsVariables()
    {
        ShowPopup(Siege("gondor", "town_gondor_1"));

        _text.Received(1).Localize("{=taom_siege_defense_gondor_title}Gondor Calls For Aid!",
            Arg.Is<IReadOnlyDictionary<string, string>>(v =>
                v["settlement"] == "Minas Tirith" && v["attacker"] == "Mordor" && v["days"] == "3"));
        _text.Received(1).Localize("{=taom_siege_defense_gondor_accept_message}Ride to {settlement}!",
            Arg.Is<IReadOnlyDictionary<string, string>>(v => v["settlement"] == "Minas Tirith"));
    }

    [TestMethod]
    public void Resolve_RewardValues_AreTheInfluenceAndRelationVariables()
    {
        _sut.Resolve(DefaultRewardMessage, "town_gondor_1", "", 0, 10, 5);

        _text.Received(1).Localize(DefaultRewardMessage,
            Arg.Is<IReadOnlyDictionary<string, string>>(v =>
                v["influence"] == "10" && v["relation"] == "5" && v["attacker"] == "" && v["days"] == "0"));
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
