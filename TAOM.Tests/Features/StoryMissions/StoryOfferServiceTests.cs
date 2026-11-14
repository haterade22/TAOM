using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.StoryMissions;
using TAOM.Features.StoryMissions.Config;
using TAOM.Features.StoryMissions.Domain;
using TAOM.Features.WarChronicle.Events;

namespace TAOM.Tests.Features.StoryMissions;

/// <summary>
/// The story offer (#1105 slice 4, docs/features/story-missions.md "The story offer"): which chronicle window the player is offered, when no
/// offer may show, and what a mission's verdict does to the campaign. A win holds the window, a loss gives the target to an attacker, and no
/// verdict leaves the window open and offers it again on the next day.
/// </summary>
[TestClass]
public class StoryOfferServiceTests
{
    private static readonly string Catalog = "{\"missions\":[" + Mission("helms_deep_defend", "Defend") + "," + Mission("isengard_attack", "Attack") + "]}";

    private static string Mission(string id, string side) =>
        "{\"id\":\"" + id + "\",\"name\":\"{=!}Helm's Deep\",\"scene\":\"scene_a\",\"side\":\"" + side + "\","
        + "\"defenderCulture\":\"vlandia\",\"attackerCulture\":\"isengard\",\"garrison\":[{\"troop\":\"g1\",\"class\":\"Infantry\",\"count\":10}],"
        + "\"roster\":{\"waves\":[{\"count\":110,\"mix\":[{\"troop\":\"a1\",\"class\":\"Infantry\",\"weight\":1}]}]}}";

    private IChronicleStoryPort _chronicle = null!;
    private ICoopSessionProvider _coop = null!;
    private IDedicatedServerProvider _dedicated = null!;
    private IEnlistmentStateQuery _enlistment = null!;
    private IStoryCampaignAdapter _campaign = null!;
    private IModLogger _logger = null!;
    private IOutcomeLog _outcomes = null!;
    private List<ChronicleStoryOffer> _offers = null!;
    private StoryOfferService _sut = null!;

    private static ChronicleStoryOffer Offer(string mission = "helms_deep_defend", string id = "hornburg", string? target = "town_V2") =>
        new(id, mission, "{=taom_wc_hornburg_title}The Battle of the Hornburg", target, new[] { "isengard", "empire" });

    [TestInitialize]
    public void Setup()
    {
        _offers = new List<ChronicleStoryOffer> { Offer() };
        _chronicle = Substitute.For<IChronicleStoryPort>();
        _chronicle.GetStoryOffers().Returns(_ => _offers);
        _chronicle.ConfirmStoryWindow(Arg.Any<string?>()).Returns(true);
        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.IsAuthority.Returns(true);
        _dedicated = Substitute.For<IDedicatedServerProvider>();
        _enlistment = Substitute.For<IEnlistmentStateQuery>();
        _campaign = Substitute.For<IStoryCampaignAdapter>();
        _campaign.CampaignId.Returns("game1");
        _logger = Substitute.For<IModLogger>();
        _outcomes = Substitute.For<IOutcomeLog>();
        _sut = new StoryOfferService(_chronicle, StoryMissionCatalog.FromText(Catalog, _logger), _coop, _dedicated, _enlistment, _campaign, _logger, _outcomes);
    }

    [TestMethod]
    public void NextOffer_AnOpenStoryWindow_ReturnsIt()
    {
        Assert.AreEqual("hornburg", _sut.NextOffer()?.EventId);
    }

    [TestMethod]
    public void NextOffer_NoOpenWindow_ReturnsNull()
    {
        _offers.Clear();

        Assert.IsNull(_sut.NextOffer());
    }

    [TestMethod]
    public void NextOffer_ACoopSession_ReturnsNull()
    {
        _coop.IsSessionActive.Returns(true);

        Assert.IsNull(_sut.NextOffer());
    }

    [TestMethod]
    public void NextOffer_ADedicatedServer_ReturnsNull()
    {
        _dedicated.IsDedicatedServer.Returns(true);

        Assert.IsNull(_sut.NextOffer());
    }

    [TestMethod]
    public void NextOffer_Enlisted_ReturnsNull()
    {
        _enlistment.IsEnlisted.Returns(true);

        Assert.IsNull(_sut.NextOffer());
    }

    [TestMethod]
    public void NextOffer_APrisoner_ReturnsNull()
    {
        _campaign.IsMainHeroPrisoner.Returns(true);

        Assert.IsNull(_sut.NextOffer());
    }

    [TestMethod]
    public void NextOffer_ThePlayersKingdomIsAnAttacker_ReturnsNull()
    {
        // A loss would give the fortress to the player's own ruler, or to the player.
        _campaign.PlayerKingdomId.Returns("empire");

        Assert.IsNull(_sut.NextOffer());
    }

    [TestMethod]
    public void NextOffer_ThePlayersKingdomIsNotAnAttacker_ReturnsTheOffer()
    {
        _campaign.PlayerKingdomId.Returns("vlandia");

        Assert.AreEqual("hornburg", _sut.NextOffer()?.EventId);
    }

    [TestMethod]
    public void MenuText_FollowsTheAppliedResult()
    {
        StringAssert.StartsWith(StoryOfferService.MenuText(false, null), "{=taom_story_offer_none}");
        StringAssert.StartsWith(StoryOfferService.MenuText(true, null), "{=taom_story_offer_menu}");
        StringAssert.StartsWith(StoryOfferService.MenuText(true, StoryResult.Held), "{=taom_story_offer_won}");
        StringAssert.StartsWith(StoryOfferService.MenuText(true, StoryResult.Fell), "{=taom_story_offer_lost}");
        StringAssert.StartsWith(StoryOfferService.MenuText(true, StoryResult.Failed), "{=taom_story_offer_undecided}", "a failed transfer gave nothing away");
        StringAssert.StartsWith(StoryOfferService.MenuText(true, StoryResult.Undecided), "{=taom_story_offer_undecided}");
        StringAssert.StartsWith(StoryOfferService.MenuText(true, StoryResult.Unchanged), "{=taom_story_offer_none}");
    }

    [TestMethod]
    public void NextOffer_InAMapEvent_ReturnsNull()
    {
        // The offer opens a menu, and only the battle menu may close the player's open battle (lessons, campaign-mechanics.md, #1099).
        _campaign.IsMainPartyInMapEvent.Returns(true);

        Assert.IsNull(_sut.NextOffer());
    }

    [TestMethod]
    public void NextOffer_AMissionTheCatalogLacks_IsNotOfferedAndWarnsOnce()
    {
        _offers[0] = Offer("nope");

        Assert.IsNull(_sut.NextOffer());
        Assert.IsNull(_sut.NextOffer());

        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("nope")));
    }

    [TestMethod]
    public void Snooze_HidesTheOfferUntilTheNextDay()
    {
        _sut.Snooze("hornburg");

        Assert.IsNull(_sut.NextOffer());
        _sut.NewDay();
        Assert.AreEqual("hornburg", _sut.NextOffer()?.EventId);
    }

    [TestMethod]
    public void ResetForNewSession_ClearsTheSnooze()
    {
        _sut.Snooze("hornburg");

        _sut.ResetForNewSession();

        Assert.IsNotNull(_sut.NextOffer());
    }

    [TestMethod]
    public void Decline_TellsTheChronicle()
    {
        _chronicle.DeclineStory("hornburg").Returns(true);

        Assert.IsTrue(_sut.Decline(Offer()));

        _chronicle.Received(1).DeclineStory("hornburg");
    }

    [TestMethod]
    public void Apply_Won_ResolvesTheWindowHeldAndPasses()
    {
        _chronicle.ResolveStoryWon("hornburg").Returns(true);

        Assert.AreEqual(StoryResult.Held, _sut.Apply(Offer(), StoryOutcome.Won));

        _campaign.DidNotReceiveWithAnyArgs().GiveSettlement(default!, default!, out _);
        _outcomes.Received(1).Pass(StoryOfferService.ResultId, "game1/hornburg/Won", Arg.Is<string>(d => d.Contains("verdict=Won") && d.Contains("result=Held")));
    }

    [TestMethod]
    public void Apply_WonOnAWindowAlreadyResolved_ChangesNothing()
    {
        _chronicle.ResolveStoryWon("hornburg").Returns(false);

        Assert.AreEqual(StoryResult.Unchanged, _sut.Apply(Offer(), StoryOutcome.Won));
    }

    [TestMethod]
    public void Apply_Lost_GivesTheTargetToTheAttackers()
    {
        _campaign.GiveSettlement("town_V2", Arg.Any<IReadOnlyList<string>>(), out Arg.Any<string>()).Returns(c =>
        {
            c[2] = "isengard";
            return true;
        });

        Assert.AreEqual(StoryResult.Fell, _sut.Apply(Offer(), StoryOutcome.Lost));

        _chronicle.Received(1).ConfirmStoryWindow("hornburg");
        _campaign.Received(1).GiveSettlement("town_V2", Arg.Is<IReadOnlyList<string>>(a => a[0] == "isengard" && a[1] == "empire"), out Arg.Any<string>());
        _chronicle.DidNotReceiveWithAnyArgs().ResolveStoryWon(default);
        _outcomes.Received(1).Pass(StoryOfferService.ResultId, "game1/hornburg/Lost", Arg.Is<string>(d => d.Contains("verdict=Lost") && d.Contains("taker=isengard")));
    }

    [TestMethod]
    public void NextOffer_AnAttackingMission_IsNotOffered()
    {
        _offers[0] = Offer("isengard_attack");

        Assert.IsNull(_sut.NextOffer(), "the player can take only the defending side");
    }

    [TestMethod]
    public void NextOffer_ASnoozedFirstWindow_ReturnsTheNextOne()
    {
        _offers.Add(Offer(id: "isengard"));
        _sut.Snooze("hornburg");

        Assert.AreEqual("isengard", _sut.NextOffer()?.EventId);
    }

    [TestMethod]
    public void NoteShown_KeysTheLineByCampaignAndWindow()
    {
        _sut.NoteShown(Offer());

        _outcomes.Received(1).Pass(StoryOfferService.ShownId, "game1/hornburg", Arg.Any<string>());
    }

    [TestMethod]
    public void Apply_AWinAndALossInOneProcess_WriteTwoResultKeys()
    {
        // OutcomeLog latches one line per key for the whole process: a reload that plays the other result must still write its line.
        _chronicle.ResolveStoryWon("hornburg").Returns(true);
        _campaign.GiveSettlement("town_V2", Arg.Any<IReadOnlyList<string>>(), out Arg.Any<string>()).Returns(c =>
        {
            c[2] = "isengard";
            return true;
        });

        _sut.Apply(Offer(), StoryOutcome.Won);
        _sut.Apply(Offer(), StoryOutcome.Lost);

        _outcomes.Received(1).Pass(StoryOfferService.ResultId, "game1/hornburg/Won", Arg.Any<string>());
        _outcomes.Received(1).Pass(StoryOfferService.ResultId, "game1/hornburg/Lost", Arg.Any<string>());
    }

    [TestMethod]
    public void Apply_LostWithNoTarget_FailsAndGivesNothingAway()
    {
        Assert.AreEqual(StoryResult.Failed, _sut.Apply(Offer(target: null), StoryOutcome.Lost));

        _campaign.DidNotReceiveWithAnyArgs().GiveSettlement(default!, default!, out _);
    }

    [TestMethod]
    public void Apply_LostOnAWindowTheChronicleNoLongerConfirms_GivesNothingAway()
    {
        // The chronicle reconciles the live owner first (a clan that defects with its fiefs raises no owner-change event).
        _chronicle.ConfirmStoryWindow("hornburg").Returns(false);

        Assert.AreEqual(StoryResult.Unchanged, _sut.Apply(Offer(), StoryOutcome.Lost));

        _campaign.DidNotReceiveWithAnyArgs().GiveSettlement(default!, default!, out _);
        _outcomes.DidNotReceiveWithAnyArgs().Pass(default!, default!, default!);
        _outcomes.DidNotReceiveWithAnyArgs().Fail(default!, default!, default!);
    }

    [TestMethod]
    public void Apply_LostButNoAttackerCanTakeIt_FailsAndOffersAgainTomorrow()
    {
        _campaign.GiveSettlement("town_V2", Arg.Any<IReadOnlyList<string>>(), out Arg.Any<string>()).Returns(c =>
        {
            c[2] = "no attacker has a ruler";
            return false;
        });

        Assert.AreEqual(StoryResult.Failed, _sut.Apply(Offer(), StoryOutcome.Lost));

        Assert.IsNull(_sut.NextOffer(), "snoozed for today");
        _outcomes.Received(1).Fail(StoryOfferService.ResultId, "game1/hornburg/Lost", Arg.Any<string>());
    }

    [TestMethod]
    public void Apply_Undecided_LeavesTheWindowOpenAndOffersAgainTomorrow()
    {
        Assert.AreEqual(StoryResult.Undecided, _sut.Apply(Offer(), StoryOutcome.Undecided));

        _chronicle.DidNotReceiveWithAnyArgs().ResolveStoryWon(default);
        _campaign.DidNotReceiveWithAnyArgs().GiveSettlement(default!, default!, out _);
        Assert.IsNull(_sut.NextOffer());
        _sut.NewDay();
        Assert.IsNotNull(_sut.NextOffer());
    }
}
