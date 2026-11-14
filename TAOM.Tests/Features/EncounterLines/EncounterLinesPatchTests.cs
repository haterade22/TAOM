using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TaleWorlds.CampaignSystem.Conversation.Tags;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.EncounterLines;
using TAOM.Features.EncounterLines.Hooks;
using TAOM.Features.EncounterLines.Models;
using TAOM.Features.EncounterLines.Tags;
using TAOM.Features.Execution;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.Messengers;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.EncounterLines;

/// <summary>
/// The patch bodies and the tag gate of Encounter Lines (Patch149), with no campaign: each does nothing when vanilla's condition
/// said no, a stripped greeting prefix leaves the postfix idle, the gate answers false with no service and on a fault, and every
/// fault leaves vanilla's text and warns once. The text seam's postfix needs the engine types and is run in
/// EncounterSentenceTextPatchTests. The two GameText nodes are pinned because a file registered for the wrong game
/// types loads as "ERROR: Text with id" rows.
/// </summary>
[TestClass]
public class EncounterLinesPatchTests
{
    private IModLogger _logger = null!;
    private IEncounterLinesSettingsProvider _settings = null!;
    private IEncounterLinesAdapter _adapter = null!;
    private EncounterLinesService _service = null!;

    private static readonly LordFacts OrcLord =
        new(true, false, false, "mordor", "orc", new SideIds("empire_s", "mordor"), false, false, "lord_1_1");

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _settings = Substitute.For<IEncounterLinesSettingsProvider>();
        _settings.Enabled.Returns(true);
        _adapter = Substitute.For<IEncounterLinesAdapter>();
        _adapter.Player().Returns(new PlayerFacts("gondor", "human", new SideIds("player_faction", "gondor")));
        _adapter.ConversationParty().Returns(new PartyFacts("gondor", false, false, false));
        var groups = Substitute.For<IEncounterGroupsConfigProvider>();
        groups.GetMap().Returns(EncounterLinesServiceTests.Map());
        var random = Substitute.For<IRandomSource>();
        random.NextDouble().Returns(0.0);
        _service = new EncounterLinesService(_settings, _adapter, groups, Substitute.For<IAlignmentService>(), random,
            Substitute.For<IDedicatedServerProvider>(), Substitute.For<IMessengerConversationState>());
        CaravanGreetingPatch.Initialize(_service, _logger);
        VillagerGreetingPatch.Initialize(_service, _logger);
        RobberyLinesPatch.Initialize(_service, _logger);
        EncounterTagGate.Initialize(_service, _ => OrcLord, _logger);
    }

    [TestMethod]
    public void CaravanPrefix_ReadsTheFirstMeetingIntoState()
    {
        _adapter.IsFirstCaravanMeeting().Returns(true);

        CaravanGreetingPatch.Prefix(out var state);

        Assert.IsTrue(state);
    }

    [TestMethod]
    public void CaravanPostfix_WithoutAFirstMeeting_OrWhenVanillaSaidNo_SetsNothing()
    {
        CaravanGreetingPatch.Postfix(true, false);
        CaravanGreetingPatch.Postfix(false, true);

        _adapter.DidNotReceiveWithAnyArgs().TrySetVariable(default!, default!, default!);
    }

    [TestMethod]
    public void CaravanPostfix_OnAFirstMeeting_SetsTheGreeting()
    {
        CaravanGreetingPatch.Postfix(true, true);

        _adapter.Received(1).TrySetVariable("CARAVAN_GREETING", "str_taom_enc_caravan_greet", "west_kin");
    }

    [TestMethod]
    public void CaravanPrefixAndPostfix_WhenTheAdapterThrows_SwallowIt_AndWarnOnce()
    {
        _adapter.IsFirstCaravanMeeting().Throws(new InvalidOperationException("boom"));
        _adapter.ConversationParty().Throws(new InvalidOperationException("boom"));

        CaravanGreetingPatch.Prefix(out var state);
        CaravanGreetingPatch.Postfix(true, true);

        Assert.IsFalse(state);
        _logger.ReceivedWithAnyArgs(1).LogWarning(default!);
    }

    [TestMethod]
    public void VillagerPrefixAndPostfix_FollowTheSameShape()
    {
        _adapter.IsFirstVillagerMeeting().Returns(true);

        VillagerGreetingPatch.Prefix(out var state);
        VillagerGreetingPatch.Postfix(true, state);
        VillagerGreetingPatch.Postfix(true, false);

        Assert.IsTrue(state);
        _adapter.Received(1).TrySetVariable("VILLAGER_GREETING", "str_taom_enc_villager_greet", "west_kin");
    }

    [TestMethod]
    public void VillagerPostfix_WhenTheAdapterThrows_WarnsOnceOverTwoCalls()
    {
        _adapter.ConversationParty().Throws(new InvalidOperationException("boom"));

        VillagerGreetingPatch.Postfix(true, true);
        VillagerGreetingPatch.Postfix(true, true);

        _logger.ReceivedWithAnyArgs(1).LogWarning(default!);
    }

    [TestMethod]
    public void RobberyPostfix_WhenVanillaSaidNo_NeverReadsTheAdapter()
    {
        RobberyLinesPatch.Postfix(false);

        Assert.AreEqual(0, _adapter.ReceivedCalls().Count());
    }

    [TestMethod]
    public void RobberyPostfix_WhenTheAdapterThrows_WarnsOnceOverTwoCalls()
    {
        _adapter.EncounteredAtSea.Throws(new InvalidOperationException("boom"));

        RobberyLinesPatch.Postfix(true);
        RobberyLinesPatch.Postfix(true);

        _logger.ReceivedWithAnyArgs(1).LogWarning(default!);
    }

    [TestMethod]
    public void TagGate_AnswersThroughTheService()
    {
        Assert.IsTrue(EncounterTagGate.Speaker(null, "orcs"));
        Assert.IsFalse(EncounterTagGate.Speaker(null, "west"));
        Assert.IsFalse(EncounterTagGate.Bucket(null, "liege"));
    }

    [TestMethod]
    public void TagGate_WhenDescribingThrows_AnswersFalse_AndWarnsOnce()
    {
        EncounterTagGate.Initialize(_service, _ => throw new InvalidOperationException("boom"), _logger);

        Assert.IsFalse(EncounterTagGate.Speaker(null, "orcs"));
        Assert.IsFalse(EncounterTagGate.Bucket(null, "foe"));
        Assert.IsFalse(EncounterTagGate.AltLine(null));
        _logger.ReceivedWithAnyArgs(1).LogWarning(default!);
    }

    [TestMethod]
    public void TagGate_ForAHeroThatIsNoLord_AnswersFalse()
    {
        EncounterTagGate.Initialize(_service, _ => null, _logger);

        Assert.IsFalse(EncounterTagGate.Speaker(null, "orcs"));
        Assert.IsFalse(EncounterTagGate.AltLine(null));
    }

    [TestMethod]
    public void TheSeamText_IsTheBareIdWrapper()
        => Assert.AreEqual("{=!}{TAOM_ENC_LINE}", EncounterSentenceTextPatch.SeamText);

    [DataTestMethod]
    [DataRow("taom_encounter_lord_strings")]
    [DataRow("taom_encounter_strings")]
    public void TheStringsFiles_AreRegisteredAsGameText_ForCampaignAndCampaignStoryModeOnly(string path)
    {
        var subModule = XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "SubModule.xml"));
        var nodes = subModule.Descendants("XmlNode").Where(n => (string?)n.Element("XmlName")?.Attribute("path") == path).ToList();
        Assert.AreEqual(1, nodes.Count, path + " must be registered exactly once in SubModule.xml");

        var node = nodes.Single();
        Assert.AreEqual("GameText", (string?)node.Element("XmlName")?.Attribute("id"));
        var types = node.Element("IncludedGameTypes")!.Elements("GameType").Select(g => ((string?)g.Attribute("value") ?? "").Trim()).ToList();
        CollectionAssert.AreEquivalent(new[] { "Campaign", "CampaignStoryMode" }, types);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheTagClasses_CarryTheIdsTheRowsUse()
    {
        var expected = new Dictionary<Type, string>
        {
            [typeof(TaomSpeakerWestTag)] = "taom_speaker_west",
            [typeof(TaomSpeakerRohirrimTag)] = "taom_speaker_rohirrim",
            [typeof(TaomSpeakerNorthmenTag)] = "taom_speaker_northmen",
            [typeof(TaomSpeakerDunlendingsTag)] = "taom_speaker_dunlendings",
            [typeof(TaomSpeakerSouthronsTag)] = "taom_speaker_southrons",
            [typeof(TaomSpeakerEasterlingsTag)] = "taom_speaker_easterlings",
            [typeof(TaomSpeakerElvesTag)] = "taom_speaker_elves",
            [typeof(TaomSpeakerDwarvesTag)] = "taom_speaker_dwarves",
            [typeof(TaomSpeakerOrcsTag)] = "taom_speaker_orcs",
            [typeof(TaomPlayerLiegeTag)] = "taom_player_liege",
            [typeof(TaomPlayerAllyTag)] = "taom_player_ally",
            [typeof(TaomPlayerRivalTag)] = "taom_player_rival",
            [typeof(TaomPlayerFoeTag)] = "taom_player_foe",
            [typeof(TaomPlayerKinTag)] = "taom_player_kin",
            [typeof(TaomAltLineTag)] = "taom_alt_line",
        };
        foreach (var pair in expected)
            Assert.AreEqual(pair.Value, ((ConversationTag)Activator.CreateInstance(pair.Key)).StringId, pair.Key.Name);

        Assert.IsTrue(new TaomSpeakerOrcsTag().IsApplicableTo(null!), "the speaker tag asks the gate");
        Assert.IsFalse(new TaomSpeakerWestTag().IsApplicableTo(null!));
    }
}
