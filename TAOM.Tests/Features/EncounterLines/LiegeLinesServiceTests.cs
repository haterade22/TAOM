using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Features.EncounterLines;
using TAOM.Features.EncounterLines.Models;
using TAOM.Features.Execution;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.Messengers;

namespace TAOM.Tests.Features.EncounterLines;

/// <summary>
/// The liege conversations of Encounter Lines: leaving a realm's service, joining it as a vassal and the mercenary
/// contract. A realm's ruler speaks the realm's row where the beat has one, then every lord speaks the row of his speaker
/// group. Every gate that leaves vanilla's line has its own test (tests.md "Skip-Guard Exhaustion").
/// </summary>
[TestClass]
public class LiegeLinesServiceTests
{
    private const string Seam = "TAOM_ENC_LINE";

    private IEncounterLinesSettingsProvider _settings = null!;
    private IEncounterLinesAdapter _adapter = null!;
    private IDedicatedServerProvider _dedicated = null!;
    private IMessengerConversationState _messenger = null!;
    private EncounterLinesService _service = null!;

    private static LordFacts Lord(
        string culture = "mordor",
        string race = "orc",
        bool isLord = true,
        bool inPlayerClan = false,
        string ruledRealm = "") =>
        new(isLord, inPlayerClan, false, culture, race, new SideIds("empire_s", culture), false, false, "lord_1_1", ruledRealm);

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IEncounterLinesSettingsProvider>();
        _settings.Enabled.Returns(true);
        _adapter = Substitute.For<IEncounterLinesAdapter>();
        _adapter.Player().Returns(new PlayerFacts("gondor", "human", new SideIds("player_faction", "gondor")));
        _adapter.ConversationLord().Returns(Lord());
        _adapter.TrySetVariable(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _adapter.TrySetMercenaryPitch(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        var groups = Substitute.For<IEncounterGroupsConfigProvider>();
        groups.GetMap().Returns(EncounterLinesServiceTests.Map());
        var alignment = Substitute.For<IAlignmentService>();
        _dedicated = Substitute.For<IDedicatedServerProvider>();
        _messenger = Substitute.For<IMessengerConversationState>();
        _service = new EncounterLinesService(_settings, _adapter, groups, alignment, Substitute.For<IRandomSource>(), _dedicated, _messenger);
    }

    private void AssertNothingSet()
    {
        _adapter.DidNotReceiveWithAnyArgs().TrySetVariable(default!, default!, default!);
        _adapter.DidNotReceiveWithAnyArgs().TrySetMercenaryPitch(default!, default!, default!);
    }

    // --- The sentence table ---

    [TestMethod]
    public void TheTable_MapsEveryVanillaLiegeSentence_ToOneTaomText()
    {
        var expected = new[]
        {
            "1220", "1221", "1225", "player_leave_faction_accepted", "player_leaves_faction",
            "player_want_to_end_mercenary_service_response", "lord_ask_exit_service_confirm_final",
            "player_want_to_join_faction_as_mercenary_or_vassal_answer", "vassalage_offer_player_has_low_relation",
            "vassalage_offer_rejected", "vassalage_offer_accepted", "player_is_accepted_as_a_vassal", "1210",
            "player_want_to_join_faction_as_mercenary_lord_answer_3", "mercenary_player_accepts_lord_answer",
            "mercenary_player_rejects_lord_answer",
        };

        CollectionAssert.AreEquivalent(expected, LiegeLines.TextOfSentence.Keys.ToList());
        Assert.AreEqual(LiegeLines.TextOfSentence["1220"], LiegeLines.TextOfSentence["1221"], "both of vanilla's release questions share one TAOM text");
        Assert.AreEqual(15, LiegeLines.TextOfSentence.Values.Distinct().Count());
        foreach (var text in LiegeLines.TextOfSentence.Values)
            StringAssert.StartsWith(text, "str_taom_liege_");
    }

    [TestMethod]
    public void TheRealmBeats_AreTheReleaseQuestionAndAnswer_TheVassalAcceptance_AndTheMercenaryAcceptance()
    {
        CollectionAssert.AreEquivalent(
            new[] { "str_taom_liege_leave_ask", "str_taom_liege_leave_granted", "str_taom_liege_vassal_accept", "str_taom_liege_merc_accept" },
            LiegeLines.RealmTexts.ToList());
    }

    // --- Realm and group rows ---

    [TestMethod]
    public void ARuler_WithARealmRow_SpeaksTheRealmLine()
    {
        _adapter.ConversationLord().Returns(Lord(race: "sauron", ruledRealm: "empire_s"));

        Assert.IsTrue(_service.TrySetSentenceLine("1220"));

        _adapter.Received(1).TrySetVariable(Seam, "str_taom_liege_leave_ask", "realm_empire_s");
        _adapter.DidNotReceive().TrySetVariable(Seam, "str_taom_liege_leave_ask", "group_sauron");
    }

    [TestMethod]
    public void ARuler_WithNoRealmRow_FallsBackToTheGroupLine()
    {
        _adapter.ConversationLord().Returns(Lord(race: "sauron", ruledRealm: "empire_s"));
        _adapter.TrySetVariable(Seam, "str_taom_liege_leave_ask", "realm_empire_s").Returns(false);

        Assert.IsTrue(_service.TrySetSentenceLine("1220"));

        _adapter.Received(1).TrySetVariable(Seam, "str_taom_liege_leave_ask", "group_sauron");
    }

    [TestMethod]
    public void ARuler_OnABeatWithNoRealmRows_SpeaksTheGroupLine_AndNeverAsksForARealmRow()
    {
        _adapter.ConversationLord().Returns(Lord(culture: "gondor", race: "human", ruledRealm: "empire_w"));

        Assert.IsTrue(_service.TrySetSentenceLine("1225"));

        _adapter.Received(1).TrySetVariable(Seam, "str_taom_liege_leave_confirm", "group_west");
        _adapter.DidNotReceive().TrySetVariable(Seam, "str_taom_liege_leave_confirm", "realm_empire_w");
    }

    [TestMethod]
    public void ALordWhoRulesNoRealm_SpeaksTheGroupLine_AndNeverAsksForARealmRow()
    {
        Assert.IsTrue(_service.TrySetSentenceLine("mercenary_player_accepts_lord_answer"));

        _adapter.Received(1).TrySetVariable(Seam, "str_taom_liege_merc_accept", "group_orcs");
        _adapter.Received(1).TrySetVariable(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void InASettlement_TheLiegeLinesStillShow_BecauseALiegeHoldsCourtInHisHall()
    {
        _adapter.InSettlement.Returns(true);

        Assert.IsTrue(_service.TrySetSentenceLine("player_leave_faction_accepted"));
        _adapter.Received(1).TrySetVariable(Seam, "str_taom_liege_leave_granted", "group_orcs");
    }

    [TestMethod]
    public void TheMercenaryPitch_GoesThroughTheAdapterThatSetsTheAward()
    {
        Assert.IsTrue(_service.TrySetSentenceLine("player_want_to_join_faction_as_mercenary_lord_answer_3"));

        _adapter.Received(1).TrySetMercenaryPitch(Seam, "str_taom_liege_merc_pitch", "group_orcs");
        _adapter.DidNotReceiveWithAnyArgs().TrySetVariable(default!, default!, default!);
    }

    // --- Every gate that leaves vanilla ---

    [TestMethod]
    public void AMissingGroupRow_IsFalse()
    {
        _adapter.TrySetVariable(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        _adapter.TrySetMercenaryPitch(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        Assert.IsFalse(_service.TrySetSentenceLine("1220"));
        Assert.IsFalse(_service.TrySetSentenceLine("player_want_to_join_faction_as_mercenary_lord_answer_3"));
    }

    [TestMethod]
    public void NoConversationLord_IsFalse()
    {
        _adapter.ConversationLord().Returns((LordFacts?)null);
        Assert.IsFalse(_service.TrySetSentenceLine("1220"));
        AssertNothingSet();
    }

    [TestMethod]
    public void ANonLord_IsFalse()
    {
        _adapter.ConversationLord().Returns(Lord(isLord: false, ruledRealm: "empire_s"));
        Assert.IsFalse(_service.TrySetSentenceLine("1220"));
        AssertNothingSet();
    }

    [TestMethod]
    public void ALordOfThePlayersClan_IsFalse()
    {
        _adapter.ConversationLord().Returns(Lord(inPlayerClan: true, ruledRealm: "empire_s"));
        Assert.IsFalse(_service.TrySetSentenceLine("1220"));
        AssertNothingSet();
    }

    [TestMethod]
    public void AManOfMordor_WhoRulesNoRealm_IsFalse_NoGroupFitsHim()
    {
        _adapter.ConversationLord().Returns(Lord(race: "human"));
        Assert.IsFalse(_service.TrySetSentenceLine("1220"));
        AssertNothingSet();
    }

    [TestMethod]
    public void AManOfMordor_WhoRulesTheRealm_SpeaksTheRealmLine()
    {
        _adapter.ConversationLord().Returns(Lord(race: "human", ruledRealm: "empire_s"));

        Assert.IsTrue(_service.TrySetSentenceLine("1220"));
        _adapter.Received(1).TrySetVariable(Seam, "str_taom_liege_leave_ask", "realm_empire_s");
    }

    [TestMethod]
    public void ACultureWithoutAGroup_IsFalse()
    {
        _adapter.ConversationLord().Returns(Lord(culture: "looters"));
        Assert.IsFalse(_service.TrySetSentenceLine("1225"));
        AssertNothingSet();
    }

    [TestMethod]
    public void TheSwitchOff_IsFalse()
    {
        _settings.Enabled.Returns(false);
        Assert.IsFalse(_service.TrySetSentenceLine("1220"));
        AssertNothingSet();
    }

    [TestMethod]
    public void ADedicatedServer_IsFalse()
    {
        _dedicated.IsDedicatedServer.Returns(true);
        Assert.IsFalse(_service.TrySetSentenceLine("1220"));
        AssertNothingSet();
    }

    [TestMethod]
    public void AnOpenMessengerConversation_IsFalse()
    {
        _messenger.IsOpen.Returns(true);
        Assert.IsFalse(_service.TrySetSentenceLine("1220"));
        AssertNothingSet();
    }

    [DataTestMethod]
    [DataRow("lord_give_oath_2")]
    [DataRow("1224")]
    [DataRow("hero_main_options")]
    public void ASentenceOutsideTheTable_IsFalse_AndNeverReadsTheLord(string id)
    {
        Assert.IsFalse(_service.TrySetSentenceLine(id));
        _adapter.DidNotReceive().ConversationLord();
        AssertNothingSet();
    }
}
