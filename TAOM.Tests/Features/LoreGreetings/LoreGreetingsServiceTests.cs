using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.LoreGreetings;
using TAOM.Features.LoreGreetings.Domain;

namespace TAOM.Tests.Features.LoreGreetings;

/// <summary>
/// The pair choice and the two patch flows of Lore Greetings (docs/features/lore-greetings.md "Rules"): who is matched to a
/// person, which listener wins, which line a pair gives at peace, when hostile and on a first meeting, and when the meeting
/// prefix marks a speaker met.
/// </summary>
[TestClass]
public class LoreGreetingsServiceTests
{
    private static readonly SideIds Mirkwood = new("mirkwood_kingdom", "mirkwood");
    private static readonly SideIds Erebor = new("erebor_kingdom", "erebor");
    private static readonly SideIds Mordor = new("mordor_kingdom", "mordor");
    private static readonly SideIds Khand = new("khand_kingdom", "khand");

    private IAlignmentService _alignment = null!;
    private ILoreGreetingsConfigProvider _config = null!;
    private ILoreGreetingsAdapter _adapter = null!;
    private ICoopSessionProvider _coop = null!;
    private IOutcomeLog _outcomes = null!;
    private LoreGreetingsService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _alignment = Substitute.For<IAlignmentService>();
        _alignment.ResolveSide(Arg.Any<string>(), Arg.Any<string>()).Returns(FactionSide.Free);
        _alignment.ResolveSide(Mordor.FactionId, Mordor.CultureId).Returns(FactionSide.Evil);
        _alignment.ResolveSide(Khand.FactionId, Khand.CultureId).Returns(FactionSide.Neutral);

        _config = Substitute.For<ILoreGreetingsConfigProvider>();
        _config.GetTable().Returns(Table());
        _adapter = Substitute.For<ILoreGreetingsAdapter>();
        _coop = Substitute.For<ICoopSessionProvider>();
        _outcomes = Substitute.For<IOutcomeLog>();
        _sut = new LoreGreetingsService(_config, _alignment, _adapter, _coop, _outcomes);
    }

    private static LoreGreetingTable Table() => new(
        new[]
        {
            new LorePerson("thranduil", false, new[] { "lord_M1_1" }),
            new LorePerson("legolas", false, new[] { "taom_hero_legolas", "named_companion_legolas", "lord_M1_11" }),
            new LorePerson("gimli", false, new[] { "taom_hero_gimli", "named_companion_gimli" }),
            new LorePerson("dain", false, new[] { "lord_E1_1" }),
            new LorePerson("eowyn", true, new[] { "lord_4_3_2" }),
            new LorePerson("theoden", false, new[] { "lord_4_1" }),
        },
        new Dictionary<(string, string), LoreGreetingLines>
        {
            [("thranduil", "legolas")] = new("My son.", "My son, against me?", "Legolas! Home at last."),
            [("thranduil", "gimli")] = new("Son of Glóin.", null, null),
            [("dain", "gimli")] = new("Gimli, son of Glóin!", null, null),
            [("theoden", "eowyn")] = new("Éowyn, sister-daughter.", null, null),
            [("legolas", "gimli")] = new(null, "Gimli, under that banner?", null),
        });

    private static LoreSpeakerFacts Speaker(string id, bool female = false, bool hasMet = true, bool prisoner = false,
        bool inPlayerParty = false, SideIds? side = null) =>
        new(new[] { id }, female, hasMet, prisoner, inPlayerParty, side ?? Mirkwood);

    private static LoreListenerFacts Player(string id, bool female = false, bool atWar = false, SideIds? side = null) =>
        new(new[] { id }, female, true, atWar, side ?? Mirkwood);

    private static LoreListenerFacts Companion(string id, bool female = false, bool atWar = false, SideIds? side = null) =>
        new(new[] { id }, female, false, atWar, side ?? Mirkwood);

    private static LoreConversationFacts Talk(LoreSpeakerFacts speaker, params LoreListenerFacts[] listeners) => new(speaker, listeners);

    [TestMethod]
    public void Choose_PlayerIsTheListenersPerson_ReturnsThePairsLineAndKey()
    {
        var choice = _sut.Choose(Talk(Speaker("lord_M1_1"), Player("lord_M1_11")), firstMeeting: false);

        Assert.AreEqual(new LoreGreetingChoice("taom_loregreet_thranduil_legolas", "My son."), choice);
    }

    [TestMethod]
    public void Choose_PlayerMatchedByAnyOfHisIds_ReturnsTheLine()
    {
        var player = new LoreListenerFacts(new[] { "main_hero_x", "named_companion_legolas" }, false, true, false, Mirkwood);

        var choice = _sut.Choose(Talk(Speaker("lord_M1_1"), player), firstMeeting: false);

        Assert.AreEqual("My son.", choice?.Text);
    }

    [TestMethod]
    public void Choose_SpeakerIsNoPerson_ReturnsNull()
    {
        Assert.IsNull(_sut.Choose(Talk(Speaker("lord_nobody"), Player("lord_M1_11")), firstMeeting: false));
    }

    [TestMethod]
    public void Choose_ListenerIsNoPerson_ReturnsNull()
    {
        Assert.IsNull(_sut.Choose(Talk(Speaker("lord_M1_1"), Player("main_hero")), firstMeeting: false));
    }

    [TestMethod]
    public void Choose_TwoPersonsWithNoPair_ReturnsNull()
    {
        Assert.IsNull(_sut.Choose(Talk(Speaker("lord_E1_1"), Player("lord_M1_11")), firstMeeting: false));
    }

    [TestMethod]
    public void Choose_PairIsOneWay_TheOtherDirectionReturnsNull()
    {
        Assert.IsNull(_sut.Choose(Talk(Speaker("lord_M1_11"), Player("lord_M1_1")), firstMeeting: false));
    }

    [TestMethod]
    public void Choose_AtWar_ReturnsTheHostileLineAndKey()
    {
        var choice = _sut.Choose(Talk(Speaker("lord_M1_1"), Player("lord_M1_11", atWar: true)), firstMeeting: false);

        Assert.AreEqual(new LoreGreetingChoice("taom_loregreet_thranduil_legolas_hostile", "My son, against me?"), choice);
    }

    [TestMethod]
    public void Choose_FreeAgainstEvilAtPeace_ReturnsTheHostileLine()
    {
        var choice = _sut.Choose(Talk(Speaker("lord_M1_1"), Player("lord_M1_11", side: Mordor)), firstMeeting: false);

        Assert.AreEqual("My son, against me?", choice?.Text);
    }

    [TestMethod]
    public void Choose_NeutralAgainstFreeAtPeace_IsNotHostile()
    {
        var choice = _sut.Choose(Talk(Speaker("lord_M1_1"), Player("lord_M1_11", side: Khand)), firstMeeting: false);

        Assert.AreEqual("My son.", choice?.Text);
    }

    [TestMethod]
    public void Choose_HostileWithNoHostileLine_ReturnsNull()
    {
        Assert.IsNull(_sut.Choose(Talk(Speaker("lord_E1_1"), Player("taom_hero_gimli", atWar: true)), firstMeeting: false));
    }

    [TestMethod]
    public void Choose_AtPeaceWithOnlyAHostileLine_ReturnsNull()
    {
        Assert.IsNull(_sut.Choose(Talk(Speaker("lord_M1_11"), Player("taom_hero_gimli")), firstMeeting: false));
    }

    [TestMethod]
    public void Choose_FirstMeeting_ReturnsTheFirstLineAndKey()
    {
        var choice = _sut.Choose(Talk(Speaker("lord_M1_1"), Player("lord_M1_11")), firstMeeting: true);

        Assert.AreEqual(new LoreGreetingChoice("taom_loregreet_thranduil_legolas_first", "Legolas! Home at last."), choice);
    }

    [TestMethod]
    public void Choose_FirstMeetingWithNoFirstLine_FallsBackToTheLine()
    {
        var choice = _sut.Choose(Talk(Speaker("lord_E1_1"), Player("taom_hero_gimli")), firstMeeting: true);

        Assert.AreEqual(new LoreGreetingChoice("taom_loregreet_dain_gimli", "Gimli, son of Glóin!"), choice);
    }

    [TestMethod]
    public void Choose_FirstMeetingWhileHostile_ReturnsTheHostileLine()
    {
        var choice = _sut.Choose(Talk(Speaker("lord_M1_1"), Player("lord_M1_11", atWar: true)), firstMeeting: true);

        Assert.AreEqual("My son, against me?", choice?.Text);
    }

    [TestMethod]
    public void Choose_FirstMeeting_AppliesToThePlayerOnly_NotToACompanion()
    {
        var choice = _sut.Choose(Talk(Speaker("lord_M1_1"), Player("main_hero"), Companion("named_companion_legolas")), firstMeeting: true);

        Assert.AreEqual("My son.", choice?.Text);
    }

    [TestMethod]
    public void Choose_PlayerHasNoPair_ACompanionHas_ReturnsTheCompanionsLine()
    {
        var choice = _sut.Choose(Talk(Speaker("lord_E1_1"), Player("main_hero"), Companion("named_companion_gimli")), firstMeeting: false);

        Assert.AreEqual(new LoreGreetingChoice("taom_loregreet_dain_gimli", "Gimli, son of Glóin!"), choice);
    }

    [TestMethod]
    public void Choose_PlayerAndCompanionBothHaveAPair_ThePlayerWins()
    {
        var choice = _sut.Choose(Talk(Speaker("lord_M1_1"), Player("lord_M1_11"), Companion("named_companion_gimli")), firstMeeting: false);

        Assert.AreEqual("My son.", choice?.Text);
    }

    [TestMethod]
    public void Choose_FirstCompanionHasNoLine_TheNextCompanionsLineIsTaken()
    {
        var choice = _sut.Choose(Talk(Speaker("lord_M1_1"), Player("main_hero"), Companion("lord_4_3_2", female: true),
            Companion("named_companion_gimli")), firstMeeting: false);

        Assert.AreEqual("Son of Glóin.", choice?.Text);
    }

    [TestMethod]
    public void Choose_SpeakerRidesInThePlayersParty_CompanionsAreSkipped()
    {
        var choice = _sut.Choose(Talk(Speaker("lord_E1_1", inPlayerParty: true), Player("main_hero"), Companion("named_companion_gimli")),
            firstMeeting: false);

        Assert.IsNull(choice);
    }

    [TestMethod]
    public void Choose_SpeakerRidesInThePlayersParty_ThePlayerIsStillGreeted()
    {
        var choice = _sut.Choose(Talk(Speaker("lord_E1_1", inPlayerParty: true), Player("taom_hero_gimli")), firstMeeting: false);

        Assert.AreEqual("Gimli, son of Glóin!", choice?.Text);
    }

    [TestMethod]
    public void Choose_CompanionAtWarWithTheSpeaker_GetsTheHostileLine()
    {
        var choice = _sut.Choose(Talk(Speaker("lord_M1_1"), Player("main_hero"), Companion("named_companion_legolas", atWar: true)),
            firstMeeting: false);

        Assert.AreEqual("My son, against me?", choice?.Text);
    }

    [TestMethod]
    public void Choose_ListenersGenderDiffersFromThePersons_IsSkipped()
    {
        Assert.IsNull(_sut.Choose(Talk(Speaker("lord_4_1"), Player("lord_4_3_2", female: false)), firstMeeting: false));
    }

    [TestMethod]
    public void Choose_ListenersGenderMatchesAFemalePerson_ReturnsTheLine()
    {
        var choice = _sut.Choose(Talk(Speaker("lord_4_1"), Player("lord_4_3_2", female: true)), firstMeeting: false);

        Assert.AreEqual("Éowyn, sister-daughter.", choice?.Text);
    }

    [TestMethod]
    public void Choose_SpeakersGenderDiffersFromThePersons_ReturnsNull()
    {
        Assert.IsNull(_sut.Choose(Talk(Speaker("lord_M1_1", female: true), Player("lord_M1_11")), firstMeeting: false));
    }

    [TestMethod]
    public void Choose_SpeakerIsAPrisoner_ReturnsNull()
    {
        Assert.IsNull(_sut.Choose(Talk(Speaker("lord_M1_1", prisoner: true), Player("lord_M1_11")), firstMeeting: false));
    }

    [TestMethod]
    public void Choose_ListenerIsTheSpeakersOwnPerson_IsSkipped()
    {
        var table = new LoreGreetingTable(
            new[] { new LorePerson("legolas", false, new[] { "lord_M1_11", "named_companion_legolas" }) },
            new Dictionary<(string, string), LoreGreetingLines> { [("legolas", "legolas")] = new("Me.", null, null) });
        _config.GetTable().Returns(table);

        Assert.IsNull(_sut.Choose(Talk(Speaker("lord_M1_11"), Player("main_hero"), Companion("named_companion_legolas")), firstMeeting: false));
    }

    [TestMethod]
    public void Key_IsThePrefixTheTwoPersonsAndTheVariantSuffix()
    {
        Assert.AreEqual("taom_loregreet_dain_gimli", LoreGreetingsService.Key("dain", "gimli", LoreGreetingVariant.Text));
        Assert.AreEqual("taom_loregreet_dain_gimli_hostile", LoreGreetingsService.Key("dain", "gimli", LoreGreetingVariant.Hostile));
        Assert.AreEqual("taom_loregreet_dain_gimli_first", LoreGreetingsService.Key("dain", "gimli", LoreGreetingVariant.First));
    }

    [TestMethod]
    public void OnMeetingCheck_UnmetSpeakerWhoKnowsThePlayersPerson_IsMarkedMet()
    {
        _adapter.Describe().Returns(Talk(Speaker("lord_M1_1", hasMet: false), Player("lord_M1_11")));

        _sut.OnMeetingCheck();

        _adapter.Received(1).MeetSpeaker();
    }

    [TestMethod]
    public void OnMeetingCheck_HostilePairWithNoHostileLine_StillMarksMet()
    {
        _adapter.Describe().Returns(Talk(Speaker("lord_E1_1", hasMet: false), Player("taom_hero_gimli", atWar: true)));

        _sut.OnMeetingCheck();

        _adapter.Received(1).MeetSpeaker();
    }

    [TestMethod]
    public void OnMeetingCheck_SpeakerAlreadyMet_DoesNothing()
    {
        _adapter.Describe().Returns(Talk(Speaker("lord_M1_1", hasMet: true), Player("lord_M1_11")));

        _sut.OnMeetingCheck();

        _adapter.DidNotReceive().MeetSpeaker();
    }

    [TestMethod]
    public void OnMeetingCheck_OnlyACompanionIsKnown_DoesNotMarkMet()
    {
        _adapter.Describe().Returns(Talk(Speaker("lord_E1_1", hasMet: false), Player("main_hero"), Companion("named_companion_gimli")));

        _sut.OnMeetingCheck();

        _adapter.DidNotReceive().MeetSpeaker();
    }

    [TestMethod]
    public void OnMeetingCheck_PlayersGenderDiffersFromThePersons_DoesNotMarkMet()
    {
        _adapter.Describe().Returns(Talk(Speaker("lord_4_1", hasMet: false), Player("lord_4_3_2", female: false)));

        _sut.OnMeetingCheck();

        _adapter.DidNotReceive().MeetSpeaker();
    }

    [TestMethod]
    public void OnMeetingCheck_OnACoopClient_DoesNotMarkMet()
    {
        _coop.IsCoopClient.Returns(true);
        _adapter.Describe().Returns(Talk(Speaker("lord_M1_1", hasMet: false), Player("lord_M1_11")));

        _sut.OnMeetingCheck();

        _adapter.DidNotReceive().MeetSpeaker();
    }

    [TestMethod]
    public void OnMeetingCheck_SpeakerIsAPrisoner_DoesNotMarkMet()
    {
        _adapter.Describe().Returns(Talk(Speaker("lord_M1_1", hasMet: false, prisoner: true), Player("lord_M1_11")));

        _sut.OnMeetingCheck();

        _adapter.DidNotReceive().MeetSpeaker();
    }

    [TestMethod]
    public void OnMeetingCheck_NoConversation_DoesNothing()
    {
        _adapter.Describe().Returns((LoreConversationFacts?)null);

        _sut.OnMeetingCheck();

        _adapter.DidNotReceive().MeetSpeaker();
    }

    [TestMethod]
    public void OnGreeting_AfterTheSpeakerWasMarkedMet_SetsTheFirstLine()
    {
        _adapter.Describe().Returns(Talk(Speaker("lord_M1_1", hasMet: false), Player("lord_M1_11")));
        _adapter.MeetSpeaker().Returns(true);
        _sut.OnMeetingCheck();
        _adapter.Describe().Returns(Talk(Speaker("lord_M1_1", hasMet: true), Player("lord_M1_11")));

        _sut.OnGreeting();

        _adapter.Received(1).SetGreeting("taom_loregreet_thranduil_legolas_first", "Legolas! Home at last.");
    }

    [TestMethod]
    public void OnGreeting_WhenTheMeetingWriteFailed_SetsTheOrdinaryLine()
    {
        _adapter.Describe().Returns(Talk(Speaker("lord_M1_1", hasMet: false), Player("lord_M1_11")));
        _adapter.MeetSpeaker().Returns(false);
        _sut.OnMeetingCheck();

        _sut.OnGreeting();

        _adapter.Received(1).SetGreeting("taom_loregreet_thranduil_legolas", "My son.");
    }

    [TestMethod]
    public void OnGreeting_WithNoMeetingThisConversation_SetsTheOrdinaryLine()
    {
        _adapter.Describe().Returns(Talk(Speaker("lord_M1_1"), Player("lord_M1_11")));

        _sut.OnGreeting();

        _adapter.Received(1).SetGreeting("taom_loregreet_thranduil_legolas", "My son.");
    }

    [TestMethod]
    public void OnGreeting_TheFirstMeetingIsUsedOnce()
    {
        _adapter.Describe().Returns(Talk(Speaker("lord_M1_1", hasMet: false), Player("lord_M1_11")));
        _adapter.MeetSpeaker().Returns(true);
        _sut.OnMeetingCheck();
        _adapter.Describe().Returns(Talk(Speaker("lord_M1_1"), Player("lord_M1_11")));
        _sut.OnGreeting();
        _adapter.ClearReceivedCalls();

        _sut.OnGreeting();

        _adapter.Received(1).SetGreeting("taom_loregreet_thranduil_legolas", "My son.");
    }

    [TestMethod]
    public void OnGreeting_TheMeetingOfAnotherSpeaker_DoesNotMakeThisOneFirst()
    {
        _adapter.Describe().Returns(Talk(Speaker("lord_E1_1", hasMet: false), Player("taom_hero_gimli")));
        _adapter.MeetSpeaker().Returns(true);
        _sut.OnMeetingCheck();
        _adapter.Describe().Returns(Talk(Speaker("lord_M1_1"), Player("lord_M1_11")));

        _sut.OnGreeting();

        _adapter.Received(1).SetGreeting("taom_loregreet_thranduil_legolas", "My son.");
    }

    [TestMethod]
    public void OnGreeting_NoPairApplies_SetsNothing()
    {
        _adapter.Describe().Returns(Talk(Speaker("lord_E1_1"), Player("main_hero")));

        _sut.OnGreeting();

        _adapter.DidNotReceive().SetGreeting(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void OnGreeting_NoConversation_SetsNothing()
    {
        _adapter.Describe().Returns((LoreConversationFacts?)null);

        _sut.OnGreeting();

        _adapter.DidNotReceive().SetGreeting(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void OnGreeting_SetsALine_WritesAPassCheckForThePair()
    {
        _adapter.Describe().Returns(Talk(Speaker("lord_M1_1"), Player("lord_M1_11")));

        _sut.OnGreeting();

        _outcomes.Received(1).Pass(LoreGreetingsService.GreetingCheckId, "taom_loregreet_thranduil_legolas", Arg.Any<string>());
    }

    [TestMethod]
    public void OnMeetingCheck_MarksMet_WritesAPassCheckForTheSpeaker()
    {
        _adapter.Describe().Returns(Talk(Speaker("lord_M1_1", hasMet: false), Player("lord_M1_11")));
        _adapter.MeetSpeaker().Returns(true);

        _sut.OnMeetingCheck();

        _outcomes.Received(1).Pass(LoreGreetingsService.MeetingCheckId, "thranduil", Arg.Any<string>());
    }
}
