using System.Collections.Generic;
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
/// The choices of Encounter Lines (Patch149) with the adapter substituted: who speaks for a group, the bucket the player is
/// in, and which keyed line a caravan, villager party, bandit party or hideout boss gets. Every gate that leaves vanilla's
/// text has its own test (tests.md "Skip-Guard Exhaustion").
/// </summary>
[TestClass]
public class EncounterLinesServiceTests
{
    private IEncounterLinesSettingsProvider _settings = null!;
    private IEncounterLinesAdapter _adapter = null!;
    private IEncounterGroupsConfigProvider _groups = null!;
    private IAlignmentService _alignment = null!;
    private IRandomSource _random = null!;
    private IDedicatedServerProvider _dedicated = null!;
    private IMessengerConversationState _messenger = null!;
    private EncounterLinesService _service = null!;

    private static readonly Dictionary<string, FactionSide> Sides = new()
    {
        ["empire_s"] = FactionSide.Evil,
        ["empire_w"] = FactionSide.Free,
        ["vlandia"] = FactionSide.Free,
        ["player_faction"] = FactionSide.Free,
    };

    internal static EncounterGroupMap Map() => new(
        new Dictionary<string, string>
        {
            ["gondor"] = "west", ["vlandia"] = "rohirrim", ["mordor"] = "orcs", ["aserai"] = "southrons", ["erebor"] = "dwarves",
        },
        new Dictionary<string, string>
        {
            ["west"] = "man", ["rohirrim"] = "man", ["orcs"] = "orc", ["southrons"] = "man", ["dwarves"] = "dwarf",
        },
        new Dictionary<string, string>
        {
            ["human"] = "man", ["orc"] = "orc", ["uruk"] = "orc", ["dwarf"] = "dwarf", ["sauron"] = "none",
        },
        new Dictionary<string, BanditClan>
        {
            ["harad_raiders"] = new(FactionSide.Evil, "man", "southrons"),
            ["feral_orcs"] = new(FactionSide.Evil, "orc", null),
        });

    private static LordFacts Lord(
        string culture = "mordor",
        string race = "orc",
        bool isLord = true,
        bool inPlayerClan = false,
        bool underCommand = false,
        bool atWar = false,
        bool rulesRealm = false,
        string faction = "empire_s") =>
        new(isLord, inPlayerClan, underCommand, culture, race, new SideIds(faction, culture), atWar, rulesRealm, "lord_1_1");

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IEncounterLinesSettingsProvider>();
        _settings.Enabled.Returns(true);
        _adapter = Substitute.For<IEncounterLinesAdapter>();
        _adapter.Player().Returns(new PlayerFacts("gondor", "human", new SideIds("player_faction", "gondor")));
        _adapter.ConversationParty().Returns(new PartyFacts("gondor", false, false, false));
        _adapter.TrySetVariable(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _adapter.HasText(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _adapter.EncounteredFactionId.Returns("harad_raiders");
        _groups = Substitute.For<IEncounterGroupsConfigProvider>();
        _groups.GetMap().Returns(Map());
        _alignment = Substitute.For<IAlignmentService>();
        _alignment.ResolveSide(Arg.Any<string>(), Arg.Any<string>())
            .Returns(ci => Sides.TryGetValue((string)ci[0], out var side) ? side : FactionSide.Neutral);
        _random = Substitute.For<IRandomSource>();
        _random.NextDouble().Returns(0.0);
        _dedicated = Substitute.For<IDedicatedServerProvider>();
        _messenger = Substitute.For<IMessengerConversationState>();
        _service = new EncounterLinesService(_settings, _adapter, _groups, _alignment, _random, _dedicated, _messenger);
    }

    private void AssertNothingSet() => _adapter.DidNotReceiveWithAnyArgs().TrySetVariable(default!, default!, default!);

    // --- Speaker ---

    [TestMethod]
    public void IsSpeaker_AnOrcLordOfMordor_SpeaksForOrcs_AndNoOtherGroup()
    {
        Assert.IsTrue(_service.IsSpeaker(Lord(), "orcs"));
        Assert.IsFalse(_service.IsSpeaker(Lord(), "west"));
    }

    [TestMethod]
    public void IsSpeaker_ANonLord_IsFalse() => Assert.IsFalse(_service.IsSpeaker(Lord(isLord: false), "orcs"));

    [TestMethod]
    public void IsSpeaker_ALordOfThePlayersClan_IsFalse() => Assert.IsFalse(_service.IsSpeaker(Lord(inPlayerClan: true), "orcs"));

    [TestMethod]
    public void IsSpeaker_ALordUnderThePlayersCommand_IsFalse() => Assert.IsFalse(_service.IsSpeaker(Lord(underCommand: true), "orcs"));

    [TestMethod]
    public void IsSpeaker_AVassalOfTheRealmThePlayerRules_IsTrue_SoTheLiegeRowsCanShow()
        => Assert.IsTrue(_service.IsSpeaker(Lord(underCommand: true, rulesRealm: true), "orcs"));

    [TestMethod]
    public void IsSpeaker_InASettlement_IsFalse()
    {
        _adapter.InSettlement.Returns(true);
        Assert.IsFalse(_service.IsSpeaker(Lord(), "orcs"));
    }

    [TestMethod]
    public void IsSpeaker_WithAMessengerOpen_IsFalse()
    {
        _messenger.IsOpen.Returns(true);
        Assert.IsFalse(_service.IsSpeaker(Lord(), "orcs"));
    }

    [TestMethod]
    public void IsSpeaker_OnADedicatedServer_IsFalse()
    {
        _dedicated.IsDedicatedServer.Returns(true);
        Assert.IsFalse(_service.IsSpeaker(Lord(), "orcs"));
    }

    [TestMethod]
    public void IsSpeaker_WithTheSwitchOff_IsFalse()
    {
        _settings.Enabled.Returns(false);
        Assert.IsFalse(_service.IsSpeaker(Lord(), "orcs"));
    }

    [TestMethod]
    public void IsSpeaker_SauronInMordor_IsFalse_TheFamilyDoesNotMatch()
        => Assert.IsFalse(_service.IsSpeaker(Lord(race: "sauron"), "orcs"));

    [TestMethod]
    public void IsSpeaker_AManOfMordor_IsFalse()
        => Assert.IsFalse(_service.IsSpeaker(Lord(race: "human"), "orcs"));

    [TestMethod]
    public void IsSpeaker_AnUnknownRace_IsFalse()
        => Assert.IsFalse(_service.IsSpeaker(Lord(race: ""), "orcs"));

    [TestMethod]
    public void IsSpeaker_ACultureWithoutAGroup_IsFalse()
        => Assert.IsFalse(_service.IsSpeaker(Lord(culture: "looters"), "orcs"));

    [TestMethod]
    public void IsSpeaker_NoLordFacts_IsFalse() => Assert.IsFalse(_service.IsSpeaker(null, "orcs"));

    // --- Lord bucket ---

    [TestMethod]
    public void IsLordBucket_AnEvilLordAndAFreePlayerAtPeace_IsFoe()
    {
        Assert.IsTrue(_service.IsLordBucket(Lord(), "foe"));
        Assert.IsFalse(_service.IsLordBucket(Lord(), "ally"));
    }

    [TestMethod]
    public void IsLordBucket_AGondorLordToTheGondorPlayer_IsKin()
        => Assert.IsTrue(_service.IsLordBucket(Lord(culture: "gondor", race: "human", faction: "empire_w"), "kin"));

    [TestMethod]
    public void IsLordBucket_ARohanLordToTheGondorPlayer_IsAlly()
        => Assert.IsTrue(_service.IsLordBucket(Lord(culture: "vlandia", race: "human", faction: "vlandia"), "ally"));

    [TestMethod]
    public void IsLordBucket_TheRealmThePlayerRules_IsLiege()
        => Assert.IsTrue(_service.IsLordBucket(Lord(culture: "gondor", race: "human", faction: "empire_w", rulesRealm: true), "liege"));

    [TestMethod]
    public void IsLordBucket_APlayerWithNoSide_AtPeace_IsRival()
    {
        _adapter.Player().Returns(new PlayerFacts("gondor", "human", SideIds.None));
        Assert.IsTrue(_service.IsLordBucket(Lord(culture: "vlandia", race: "human", faction: "vlandia"), "rival"));
    }

    [TestMethod]
    public void IsLordBucket_NoPlayer_IsFalseForEveryBucket()
    {
        _adapter.Player().Returns((PlayerFacts?)null);
        foreach (var bucket in new[] { "liege", "ally", "rival", "foe", "kin" })
            Assert.IsFalse(_service.IsLordBucket(Lord(), bucket), bucket);
    }

    [TestMethod]
    public void IsLordBucket_WithTheSwitchOff_IsFalse()
    {
        _settings.Enabled.Returns(false);
        Assert.IsFalse(_service.IsLordBucket(Lord(), "foe"));
    }

    [TestMethod]
    public void IsAltLine_FollowsTheDay()
    {
        _adapter.DayOfYear.Returns(10);
        var today = _service.IsAltLine(Lord());
        _adapter.DayOfYear.Returns(11);

        Assert.AreNotEqual(today, _service.IsAltLine(Lord()));
        Assert.AreEqual(EncounterBuckets.IsAltLine("lord_1_1", 11), _service.IsAltLine(Lord()));
    }

    // --- Caravan and villager greetings ---

    [TestMethod]
    public void TryCaravanGreeting_FirstMeeting_LowRoll_SetsTheGroupBucketLine()
    {
        Assert.IsTrue(_service.TryCaravanGreeting(true));

        _adapter.Received(1).TrySetVariable("CARAVAN_GREETING", "str_taom_enc_caravan_greet", "west_kin");
    }

    [TestMethod]
    public void TryCaravanGreeting_AtWar_IsTheFoeLine()
    {
        _adapter.ConversationParty().Returns(new PartyFacts("gondor", true, false, false));

        _service.TryCaravanGreeting(true);

        _adapter.Received(1).TrySetVariable("CARAVAN_GREETING", "str_taom_enc_caravan_greet", "west_foe");
    }

    [TestMethod]
    public void TryCaravanGreeting_AnotherGroupsCaravan_IsTheFriendLine()
    {
        _adapter.ConversationParty().Returns(new PartyFacts("aserai", false, false, false));

        _service.TryCaravanGreeting(true);

        _adapter.Received(1).TrySetVariable("CARAVAN_GREETING", "str_taom_enc_caravan_greet", "southrons_friend");
    }

    [DataTestMethod]
    [DataRow(0.5)]
    [DataRow(0.9)]
    public void TryCaravanGreeting_RollAtOrAboveHalf_LeavesVanilla(double roll)
    {
        _random.NextDouble().Returns(roll);

        Assert.IsFalse(_service.TryCaravanGreeting(true));
        AssertNothingSet();
    }

    [TestMethod]
    public void TryCaravanGreeting_ARepeatMeeting_LeavesVanilla_AndNeverRolls()
    {
        Assert.IsFalse(_service.TryCaravanGreeting(false));
        AssertNothingSet();
        _random.DidNotReceive().NextDouble();
    }

    [TestMethod]
    public void TryCaravanGreeting_AtSea_LeavesVanilla()
    {
        _adapter.ConversationParty().Returns(new PartyFacts("gondor", false, true, false));
        Assert.IsFalse(_service.TryCaravanGreeting(true));
        AssertNothingSet();
    }

    [TestMethod]
    public void TryCaravanGreeting_ThePlayersOwnCaravan_LeavesVanilla()
    {
        _adapter.ConversationParty().Returns(new PartyFacts("gondor", false, false, true));
        Assert.IsFalse(_service.TryCaravanGreeting(true));
        AssertNothingSet();
    }

    [TestMethod]
    public void TryCaravanGreeting_AMissingRow_AnswersFalse()
    {
        _adapter.TrySetVariable(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        Assert.IsFalse(_service.TryCaravanGreeting(true));
    }

    [TestMethod]
    public void TryCaravanGreeting_AHomeCultureWithoutAGroup_LeavesVanilla()
    {
        _adapter.ConversationParty().Returns(new PartyFacts("looters", false, false, false));
        Assert.IsFalse(_service.TryCaravanGreeting(true));
        AssertNothingSet();
    }

    [TestMethod]
    public void TryCaravanGreeting_SwitchOff_MessengerOrServer_LeaveVanilla()
    {
        _settings.Enabled.Returns(false);
        Assert.IsFalse(_service.TryCaravanGreeting(true));
        _settings.Enabled.Returns(true);
        _messenger.IsOpen.Returns(true);
        Assert.IsFalse(_service.TryCaravanGreeting(true));
        _messenger.IsOpen.Returns(false);
        _dedicated.IsDedicatedServer.Returns(true);
        Assert.IsFalse(_service.TryCaravanGreeting(true));
        AssertNothingSet();
    }

    [TestMethod]
    public void TryVillagerGreeting_FirstMeeting_LowRoll_SetsTheVillagerLine()
    {
        Assert.IsTrue(_service.TryVillagerGreeting(true));

        _adapter.Received(1).TrySetVariable("VILLAGER_GREETING", "str_taom_enc_villager_greet", "west_kin");
    }

    [TestMethod]
    public void TryVillagerGreeting_RepeatMeetingOrHighRoll_LeavesVanilla()
    {
        Assert.IsFalse(_service.TryVillagerGreeting(false));
        _random.NextDouble().Returns(0.5);
        Assert.IsFalse(_service.TryVillagerGreeting(true));
        AssertNothingSet();
    }

    [TestMethod]
    public void TryVillagerGreeting_AtSea_LeavesVanilla()
    {
        _adapter.ConversationParty().Returns(new PartyFacts("gondor", false, true, false));
        Assert.IsFalse(_service.TryVillagerGreeting(true));
        AssertNothingSet();
    }

    [TestMethod]
    public void IsFirstMeeting_ReadsTheAdapter_OnlyWithTheFeatureOn()
    {
        _adapter.IsFirstCaravanMeeting().Returns(true);
        _adapter.IsFirstVillagerMeeting().Returns(true);
        Assert.IsTrue(_service.IsFirstCaravanMeeting());
        Assert.IsTrue(_service.IsFirstVillagerMeeting());

        _settings.Enabled.Returns(false);
        Assert.IsFalse(_service.IsFirstCaravanMeeting());
        Assert.IsFalse(_service.IsFirstVillagerMeeting());
    }

    // --- Robbery ---

    [TestMethod]
    public void TryRobberySet_SetsAllFour_FromOneVariation()
    {
        Assert.IsTrue(_service.TryRobberySet());

        _adapter.Received(1).TrySetVariable("ROBBERY_THREAT", "str_robbery_threat", "harad_raiders_foe_1");
        _adapter.Received(1).TrySetVariable("ROBBERY_PAY_AGREEMENT", "str_robbery_pay_agreement", "harad_raiders_foe_1");
        _adapter.Received(1).TrySetVariable("ROBBERY_CONCLUSION", "str_robbery_conclusion", "harad_raiders_foe_1");
        _adapter.Received(1).TrySetVariable("ROBBERY_START_FIGHT", "str_robbery_start_fight", "harad_raiders_foe_1");
        _adapter.Received(4).TrySetVariable(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow("str_robbery_threat")]
    [DataRow("str_robbery_pay_agreement")]
    [DataRow("str_robbery_conclusion")]
    [DataRow("str_robbery_start_fight")]
    public void TryRobberySet_AnyOneOfFourMissing_SetsNothing(string missing)
    {
        _adapter.HasText(missing, "harad_raiders_foe_1").Returns(false);

        Assert.IsFalse(_service.TryRobberySet());
        AssertNothingSet();
    }

    [DataTestMethod]
    [DataRow(0.0, true, "_1")]
    [DataRow(0.2499, true, "_1")]
    [DataRow(0.25, true, "_2")]
    [DataRow(0.4999, true, "_2")]
    [DataRow(0.5, false, null)]
    public void TryRobberySet_AClanWithLegacyRows_UsesHalfTheRolls(double roll, bool set, string? suffix)
    {
        _random.NextDouble().Returns(roll);

        Assert.AreEqual(set, _service.TryRobberySet());
        if (set)
            _adapter.Received(1).TrySetVariable("ROBBERY_THREAT", "str_robbery_threat", "harad_raiders_foe" + suffix);
        else
            AssertNothingSet();
    }

    [DataTestMethod]
    [DataRow(0.3999, true, "_1")]
    [DataRow(0.4, true, "_2")]
    [DataRow(0.7999, true, "_2")]
    [DataRow(0.8, false, null)]
    public void TryRobberySet_AClanWithoutLegacyRows_UsesEightTenthsOfTheRolls(double roll, bool set, string? suffix)
    {
        _adapter.EncounteredFactionId.Returns("feral_orcs");
        _adapter.HasText("str_robbery_threat", "feral_orcs_1").Returns(false);
        _random.NextDouble().Returns(roll);

        Assert.AreEqual(set, _service.TryRobberySet());
        if (set)
            _adapter.Received(1).TrySetVariable("ROBBERY_THREAT", "str_robbery_threat", "feral_orcs_foe" + suffix);
        else
            AssertNothingSet();
    }

    [TestMethod]
    public void TryRobberySet_AtSea_LeavesVanilla_AndNeverRolls()
    {
        _adapter.EncounteredAtSea.Returns(true);

        Assert.IsFalse(_service.TryRobberySet());
        AssertNothingSet();
        _random.DidNotReceive().NextDouble();
    }

    [TestMethod]
    public void TryRobberySet_AClanTheDataDoesNotList_LeavesVanilla_AndNeverRolls()
    {
        _adapter.EncounteredFactionId.Returns("mirkwood_spiders");

        Assert.IsFalse(_service.TryRobberySet());
        AssertNothingSet();
        _random.DidNotReceive().NextDouble();
    }

    [TestMethod]
    public void TryRobberySet_SwitchOff_LeavesVanilla()
    {
        _settings.Enabled.Returns(false);
        Assert.IsFalse(_service.TryRobberySet());
        AssertNothingSet();
    }

    [TestMethod]
    public void BanditBucket_AHaradPlayer_IsKinToHaradRaiders()
    {
        _adapter.Player().Returns(new PlayerFacts("aserai", "human", new SideIds("aserai", "aserai")));
        Assert.AreEqual("kin", _service.BanditBucket("harad_raiders"));
    }

    [TestMethod]
    public void BanditBucket_UnknownClan_NoPlayer_OrSwitchOff_IsNull()
    {
        Assert.IsNull(_service.BanditBucket("wild_trolls"));
        Assert.IsNull(_service.BanditBucket(null));
        _adapter.Player().Returns((PlayerFacts?)null);
        Assert.IsNull(_service.BanditBucket("harad_raiders"));
        _adapter.Player().Returns(new PlayerFacts("gondor", "human", new SideIds("player_faction", "gondor")));
        _settings.Enabled.Returns(false);
        Assert.IsNull(_service.BanditBucket("harad_raiders"));
    }

    // --- Text seam ---

    [TestMethod]
    public void TrySetSentenceLine_CaravanSurrender_SetsTheDefyLine()
    {
        _adapter.ConversationParty().Returns(new PartyFacts("gondor", true, false, false));

        Assert.IsTrue(_service.TrySetSentenceLine("caravan_accepted_to_give_everything"));
        _adapter.Received(1).TrySetVariable("TAOM_ENC_LINE", "str_taom_enc_caravan_defy", "west_foe");
        _random.DidNotReceive().NextDouble();
    }

    [TestMethod]
    public void TrySetSentenceLine_VillagerSurrender_SetsTheDefyLine()
    {
        Assert.IsTrue(_service.TrySetSentenceLine("village_farmer_accepted_to_give_everything"));
        _adapter.Received(1).TrySetVariable("TAOM_ENC_LINE", "str_taom_enc_villager_defy", "west_kin");
    }

    [TestMethod]
    public void TrySetSentenceLine_HideoutBoss_SetsTheClanBucketLine()
    {
        Assert.IsTrue(_service.TrySetSentenceLine("bandit_hideout_start_defender"));
        _adapter.Received(1).TrySetVariable("TAOM_ENC_LINE", "str_taom_enc_hideout_boss", "harad_raiders_foe");
    }

    [TestMethod]
    public void TrySetSentenceLine_HideoutBoss_SpeaksForTheClanOfHisOwnParty_NotTheHideoutsFaction()
    {
        _adapter.HideoutBossClanId.Returns("feral_orcs");

        Assert.IsTrue(_service.TrySetSentenceLine("bandit_hideout_start_defender"));
        _adapter.Received(1).TrySetVariable("TAOM_ENC_LINE", "str_taom_enc_hideout_boss", "feral_orcs_foe");
        _adapter.DidNotReceive().TrySetVariable("TAOM_ENC_LINE", "str_taom_enc_hideout_boss", "harad_raiders_foe");
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void TrySetSentenceLine_HideoutBoss_WithNoClanOfHisOwn_FallsBackToTheHideoutsFaction(string? bossClan)
    {
        _adapter.HideoutBossClanId.Returns(bossClan);

        Assert.IsTrue(_service.TrySetSentenceLine("bandit_hideout_start_defender"));
        _adapter.Received(1).TrySetVariable("TAOM_ENC_LINE", "str_taom_enc_hideout_boss", "harad_raiders_foe");
    }

    [DataTestMethod]
    [DataRow("caravan_hero_leader_talk_start")]
    [DataRow("bandit_start_defender")]
    [DataRow("")]
    [DataRow(null)]
    public void TrySetSentenceLine_AnyOtherSentence_IsFalse(string? id)
    {
        Assert.IsFalse(_service.TrySetSentenceLine(id));
        AssertNothingSet();
    }

    [TestMethod]
    public void TrySetSentenceLine_AMissingRow_IsFalse()
    {
        _adapter.TrySetVariable(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        Assert.IsFalse(_service.TrySetSentenceLine("caravan_accepted_to_give_everything"));
        Assert.IsFalse(_service.TrySetSentenceLine("bandit_hideout_start_defender"));
    }

    [TestMethod]
    public void TrySetSentenceLine_AHideoutOfAClanTheDataDoesNotList_IsFalse()
    {
        _adapter.EncounteredFactionId.Returns("mirkwood_spiders");
        Assert.IsFalse(_service.TrySetSentenceLine("bandit_hideout_start_defender"));
        AssertNothingSet();
    }

    [TestMethod]
    public void TrySetSentenceLine_SwitchOff_IsFalse()
    {
        _settings.Enabled.Returns(false);
        Assert.IsFalse(_service.TrySetSentenceLine("caravan_accepted_to_give_everything"));
        Assert.IsFalse(_service.TrySetSentenceLine("bandit_hideout_start_defender"));
        AssertNothingSet();
    }
}
