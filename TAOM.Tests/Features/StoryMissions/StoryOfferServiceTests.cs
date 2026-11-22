using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.Execution;
using TAOM.Features.StoryMissions;
using TAOM.Features.StoryMissions.Config;
using TAOM.Features.StoryMissions.Domain;
using TAOM.Features.WarChronicle.Events;

namespace TAOM.Tests.Features.StoryMissions;

/// <summary>
/// The story offer (#1105 slice 4, docs/features/story-missions.md "The story offer"): which chronicle window the player is offered, when no
/// offer may show, and what a mission's verdict does to the campaign. A win holds the window, a loss gives the target to an attacker, and no
/// verdict leaves the window open and offers it again on the next day. The sides on offer follow the player's side of the war (#1326).
/// </summary>
[TestClass]
public class StoryOfferServiceTests
{
    private static readonly string Catalog = "{\"missions\":[" + Mission("helms_deep_defend", "Defend") + "," + Mission("isengard_attack", "Attack") + "]}";

    private static string Mission(string id, string side) =>
        "{\"id\":\"" + id + "\",\"name\":\"{=!}Helm's Deep\",\"scene\":\"scene_a\",\"side\":\"" + side + "\","
        + "\"defenderCulture\":\"vlandia\",\"attackerCulture\":\"isengard\",\"garrison\":[{\"troop\":\"g1\",\"class\":\"Infantry\",\"count\":10}],"
        + "\"roster\":{\"waves\":[{\"count\":110,\"mix\":[{\"troop\":\"a1\",\"class\":\"Infantry\",\"weight\":1}]}]}}";

    // The alignment table's rule, on a table of four: the faction id first, its culture when the id is not listed. A window's
    // kingdoms are sided by GetKingdomSide alone; a two-argument ResolveSide in the service would zip a kingdom with a mission
    // row's culture (SidePairScanTests, #957), so it throws here.
    private sealed class TableAlignment : IAlignmentService
    {
        private static readonly Dictionary<string, FactionSide> Sides = new()
        {
            ["vlandia"] = FactionSide.Free, ["isengard"] = FactionSide.Evil, ["empire"] = FactionSide.Evil, ["mordor"] = FactionSide.Evil,
        };

        public FactionSide GetKingdomSide(string kingdomId) => Sides.TryGetValue(kingdomId ?? string.Empty, out var side) ? side : FactionSide.Neutral;

        public FactionSide GetCultureSide(string cultureId) => GetKingdomSide(cultureId);

        public FactionSide ResolveSide(string kingdomId, string cultureId)
        {
            // Reached only through the SideIds extension, for the player's own pair.
            if (kingdomId != PlayerFactionId)
                throw new System.InvalidOperationException($"ResolveSide('{kingdomId}', '{cultureId}') is not the player's pair");
            var side = GetKingdomSide(kingdomId);
            return side != FactionSide.Neutral ? side : GetCultureSide(cultureId);
        }

        public string PlayerFactionId { get; set; } = string.Empty;

        public bool AreEnemyAlignments(string kingdomIdA, string kingdomIdB) => throw new System.NotSupportedException();

        public bool AreSameAlignment(string kingdomIdA, string kingdomIdB) => throw new System.NotSupportedException();

        public bool AreEnemyAlignments(FactionSide sideA, FactionSide sideB) => throw new System.NotSupportedException();

        public bool AreSameAlignment(FactionSide sideA, FactionSide sideB) => throw new System.NotSupportedException();
    }

    private readonly TableAlignment _alignment = new();
    private IChronicleStoryPort _chronicle = null!;
    private ICoopSessionProvider _coop = null!;
    private IDedicatedServerProvider _dedicated = null!;
    private IEnlistmentStateQuery _enlistment = null!;
    private IStoryCampaignAdapter _campaign = null!;
    private IModLogger _logger = null!;
    private IOutcomeLog _outcomes = null!;
    private List<ChronicleStoryOffer> _offers = null!;
    private StoryOfferService _sut = null!;

    private static ChronicleStoryOffer Offer(string mission = "helms_deep_defend", string id = "hornburg", string? target = "town_V2", string? attack = null,
        string defender = "vlandia", string[]? attackers = null) =>
        new(id, mission, "{=taom_wc_hornburg_title}The Battle of the Hornburg", target, attackers ?? new[] { "isengard", "empire" }, defender, attack);

    private void PlayerIs(string factionId, string cultureId)
    {
        _campaign.PlayerSide.Returns(new SideIds(factionId, cultureId));
        _alignment.PlayerFactionId = factionId;
    }

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
        // A neutral clan outside every kingdom: it may take any side that has a mission.
        PlayerIs("clan_player", "battania");
        _logger = Substitute.For<IModLogger>();
        _outcomes = Substitute.For<IOutcomeLog>();
        _sut = new StoryOfferService(_chronicle, StoryMissionCatalog.FromText(Catalog, _logger), _coop, _dedicated, _enlistment, _campaign,
            _alignment, _logger, _outcomes);
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
    public void NextOffer_APlayerOfTheEvilHost_NoAttackMission_ReturnsNull()
    {
        // A lost defence would give the fortress to the player's own ruler, or to the player.
        PlayerIs("empire", "empire");

        Assert.IsNull(_sut.NextOffer());
    }

    [TestMethod]
    public void NextOffer_APlayerOfTheEvilHost_WithAnAttackMission_MayOnlyAttack()
    {
        _offers[0] = Offer(attack: "isengard_attack");
        PlayerIs("isengard", "isengard");

        var offer = _sut.NextOffer();

        Assert.IsNotNull(offer);
        Assert.IsFalse(offer!.Choices.Defend);
        Assert.IsTrue(offer.Choices.Attack);
    }

    [TestMethod]
    public void NextOffer_ANeutralRealmAmongTheAttackers_ItsPlayerMayOnlyAttack()
    {
        // Alignment leaves a neutral player both sides: only his tie to an attacking realm removes the defence.
        _offers[0] = Offer(attack: "isengard_attack", attackers: new[] { "isengard", "umbar" });
        PlayerIs("umbar", "umbar");

        var offer = _sut.NextOffer();

        Assert.IsFalse(offer!.Choices.Defend, "he never fights against his own realm");
        Assert.IsTrue(offer.Choices.Attack);
        Assert.AreEqual(FactionSide.Neutral, offer.PlayerSide);
    }

    [TestMethod]
    public void NextOffer_ANeutralRealmDefends_ItsPlayerMayOnlyDefend()
    {
        // Alignment leaves a neutral player both sides: only his tie to the defending realm removes the attack.
        _offers[0] = Offer(attack: "isengard_attack", defender: "umbar");
        PlayerIs("umbar", "umbar");

        var offer = _sut.NextOffer();

        Assert.IsTrue(offer!.Choices.Defend);
        Assert.IsFalse(offer.Choices.Attack, "he never fights against his own realm");
    }

    [TestMethod]
    public void NextOffer_APlayerOfTheFreeDefender_MayOnlyDefend()
    {
        _offers[0] = Offer(attack: "isengard_attack");
        PlayerIs("vlandia", "vlandia");

        var offer = _sut.NextOffer();

        Assert.IsTrue(offer!.Choices.Defend);
        Assert.IsFalse(offer.Choices.Attack);
        Assert.AreEqual(FactionSide.Free, offer.PlayerSide);
    }

    [TestMethod]
    public void NextOffer_AFreePlayerOfAnotherRealm_MayOnlyDefend()
    {
        _offers[0] = Offer(attack: "isengard_attack");
        PlayerIs("clan_player", "vlandia");

        var offer = _sut.NextOffer();

        Assert.IsTrue(offer!.Choices.Defend);
        Assert.IsFalse(offer.Choices.Attack, "a free player never storms a free fortress");
    }

    [TestMethod]
    public void NextOffer_AnEvilPlayer_NoAttackMission_ReturnsNull()
    {
        // Not a lone "Let fate decide": that is a saved decline, and it would close the window for him before an attack exists.
        PlayerIs("empire_s", "mordor");

        Assert.IsNull(_sut.NextOffer());
    }

    [TestMethod]
    public void NextOffer_AnEvilPlayer_WithAnAttackMission_MayOnlyAttack()
    {
        _offers[0] = Offer(attack: "isengard_attack");
        PlayerIs("empire_s", "mordor");

        var offer = _sut.NextOffer();

        Assert.IsFalse(offer!.Choices.Defend, "an evil player is not asked to hold Rohan's walls");
        Assert.IsTrue(offer.Choices.Attack);
        Assert.AreEqual(FactionSide.Evil, offer.PlayerSide);
    }

    [TestMethod]
    public void NextOffer_ANeutralPlayer_WithAnAttackMission_MayTakeEitherSide()
    {
        _offers[0] = Offer(attack: "isengard_attack");

        var offer = _sut.NextOffer();

        Assert.IsTrue(offer!.Choices.Defend);
        Assert.IsTrue(offer.Choices.Attack);
        Assert.AreEqual(FactionSide.Neutral, offer.PlayerSide);
    }

    [TestMethod]
    public void NextOffer_ANeutralPlayer_NoAttackMission_MayOnlyDefend()
    {
        var offer = _sut.NextOffer();

        Assert.IsTrue(offer!.Choices.Defend);
        Assert.IsFalse(offer.Choices.Attack);
    }

    [TestMethod]
    public void NextOffer_AnAttackIdThatIsNotAnAttack_DropsTheAttackAndWarnsOnce()
    {
        _offers[0] = Offer(attack: "helms_deep_defend");

        var offer = _sut.NextOffer();
        _sut.NextOffer();

        Assert.IsTrue(offer!.Choices.Defend);
        Assert.IsFalse(offer.Choices.Attack);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("helms_deep_defend") && s.Contains("attack")));
    }

    [TestMethod]
    public void NextOffer_OnlyTheAttackMissionLoads_StillOffersTheAttack()
    {
        _offers[0] = Offer("nope", attack: "isengard_attack");
        PlayerIs("empire_s", "mordor");

        var offer = _sut.NextOffer();

        Assert.IsFalse(offer!.Choices.Defend);
        Assert.IsTrue(offer.Choices.Attack);
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
        _chronicle.ResolveStoryHeld("hornburg", Arg.Any<string>()).Returns(true);

        Assert.AreEqual(StoryResult.Held, _sut.Apply(Offer(), StorySide.Defending, StoryOutcome.Won));

        _campaign.DidNotReceiveWithAnyArgs().GiveSettlement(default!, default!, out _);
        _outcomes.Received(1).Pass(StoryOfferService.ResultId, "game1/hornburg/Won", Arg.Is<string>(d => d.Contains("verdict=Won") && d.Contains("result=Held")));
    }

    [TestMethod]
    public void Apply_WonOnAWindowAlreadyResolved_ChangesNothing()
    {
        _chronicle.ResolveStoryHeld("hornburg", Arg.Any<string>()).Returns(false);

        Assert.AreEqual(StoryResult.Unchanged, _sut.Apply(Offer(), StorySide.Defending, StoryOutcome.Won));
    }

    [TestMethod]
    public void Apply_Lost_GivesTheTargetToTheAttackers()
    {
        _campaign.GiveSettlement("town_V2", Arg.Any<IReadOnlyList<string>>(), out Arg.Any<string>()).Returns(c =>
        {
            c[2] = "isengard";
            return true;
        });

        Assert.AreEqual(StoryResult.Fell, _sut.Apply(Offer(), StorySide.Defending, StoryOutcome.Lost));

        _chronicle.Received(1).ConfirmStoryWindow("hornburg");
        _campaign.Received(1).GiveSettlement("town_V2", Arg.Is<IReadOnlyList<string>>(a => a[0] == "isengard" && a[1] == "empire"), out Arg.Any<string>());
        _chronicle.DidNotReceiveWithAnyArgs().ResolveStoryHeld(default, default!);
        _outcomes.Received(1).Pass(StoryOfferService.ResultId, "game1/hornburg/Lost", Arg.Is<string>(d => d.Contains("verdict=Lost") && d.Contains("taker=isengard")));
    }

    [TestMethod]
    public void NextOffer_AnAttackingMission_IsNotOffered()
    {
        _offers[0] = Offer("isengard_attack");

        Assert.IsNull(_sut.NextOffer(), "the window's defence must be a Defend row: an attack is offered only through attackMission");
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
        _sut.NoteShown(_sut.NextOffer()!);

        _outcomes.Received(1).Pass(StoryOfferService.ShownId, "game1/hornburg", "mission=helms_deep_defend target=town_V2 choices=Defend side=Neutral");
    }

    [TestMethod]
    public void Apply_AWinAndALossInOneProcess_WriteTwoResultKeys()
    {
        // OutcomeLog latches one line per key for the whole process: a reload that plays the other result must still write its line.
        _chronicle.ResolveStoryHeld("hornburg", Arg.Any<string>()).Returns(true);
        _campaign.GiveSettlement("town_V2", Arg.Any<IReadOnlyList<string>>(), out Arg.Any<string>()).Returns(c =>
        {
            c[2] = "isengard";
            return true;
        });

        _sut.Apply(Offer(), StorySide.Defending, StoryOutcome.Won);
        _sut.Apply(Offer(), StorySide.Defending, StoryOutcome.Lost);

        _outcomes.Received(1).Pass(StoryOfferService.ResultId, "game1/hornburg/Won", Arg.Any<string>());
        _outcomes.Received(1).Pass(StoryOfferService.ResultId, "game1/hornburg/Lost", Arg.Any<string>());
    }

    [TestMethod]
    public void Apply_LostWithNoTarget_FailsAndGivesNothingAway()
    {
        Assert.AreEqual(StoryResult.Failed, _sut.Apply(Offer(target: null), StorySide.Defending, StoryOutcome.Lost));

        _campaign.DidNotReceiveWithAnyArgs().GiveSettlement(default!, default!, out _);
    }

    [TestMethod]
    public void Apply_LostOnAWindowTheChronicleNoLongerConfirms_GivesNothingAway()
    {
        // The chronicle reconciles the live owner first (a clan that defects with its fiefs raises no owner-change event).
        _chronicle.ConfirmStoryWindow("hornburg").Returns(false);

        Assert.AreEqual(StoryResult.Unchanged, _sut.Apply(Offer(), StorySide.Defending, StoryOutcome.Lost));

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

        Assert.AreEqual(StoryResult.Failed, _sut.Apply(Offer(), StorySide.Defending, StoryOutcome.Lost));

        Assert.IsNull(_sut.NextOffer(), "snoozed for today");
        _outcomes.Received(1).Fail(StoryOfferService.ResultId, "game1/hornburg/Lost", Arg.Any<string>());
    }

    [TestMethod]
    public void Apply_AttackWon_GivesTheTargetToTheAttackers()
    {
        _campaign.GiveSettlement("town_V2", Arg.Any<IReadOnlyList<string>>(), out Arg.Any<string>()).Returns(c =>
        {
            c[2] = "isengard";
            return true;
        });

        Assert.AreEqual(StoryResult.Fell, _sut.Apply(Offer(), StorySide.Attacking, StoryOutcome.Won));

        _chronicle.Received(1).ConfirmStoryWindow("hornburg");
        _chronicle.DidNotReceiveWithAnyArgs().ResolveStoryHeld(default, default!);
        _outcomes.Received(1).Pass(StoryOfferService.ResultId, "game1/hornburg/AttackWon", "side=Attacking verdict=Won result=Fell taker=isengard");
    }

    [TestMethod]
    public void Apply_AttackLost_ResolvesTheWindowHeld()
    {
        // The canon ending. A window left open would let the player storm the walls again every day.
        _chronicle.ResolveStoryHeld("hornburg", Arg.Any<string>()).Returns(true);

        Assert.AreEqual(StoryResult.Held, _sut.Apply(Offer(), StorySide.Attacking, StoryOutcome.Lost));

        _chronicle.Received(1).ResolveStoryHeld("hornburg", Arg.Is<string>(why => why.Contains("thrown back")));
        _campaign.DidNotReceiveWithAnyArgs().GiveSettlement(default!, default!, out _);
        _outcomes.Received(1).Pass(StoryOfferService.ResultId, "game1/hornburg/AttackLost", "side=Attacking verdict=Lost result=Held");
    }

    [TestMethod]
    public void Apply_AttackUndecided_LeavesTheWindowOpenAndSnoozes()
    {
        Assert.AreEqual(StoryResult.Undecided, _sut.Apply(Offer(), StorySide.Attacking, StoryOutcome.Undecided));

        _chronicle.DidNotReceiveWithAnyArgs().ResolveStoryHeld(default, default!);
        _campaign.DidNotReceiveWithAnyArgs().GiveSettlement(default!, default!, out _);
        Assert.IsNull(_sut.NextOffer(), "snoozed for today");
    }

    [TestMethod]
    public void Apply_Undecided_LeavesTheWindowOpenAndOffersAgainTomorrow()
    {
        Assert.AreEqual(StoryResult.Undecided, _sut.Apply(Offer(), StorySide.Defending, StoryOutcome.Undecided));

        _chronicle.DidNotReceiveWithAnyArgs().ResolveStoryHeld(default, default!);
        _campaign.DidNotReceiveWithAnyArgs().GiveSettlement(default!, default!, out _);
        Assert.IsNull(_sut.NextOffer());
        _sut.NewDay();
        Assert.IsNotNull(_sut.NextOffer());
    }

    // #1105 slice 3: a story whose result is a hero's life (the Breaking of the Fellowship and Boromir).
    private static ChronicleStoryOffer HeroOffer() =>
        new("breaking", "helms_deep_defend", "{=taom_wc_breaking_title}The Breaking of the Fellowship", null, new[] { "isengard" }, "vlandia", "isengard_attack", "lord_1_75");

    [TestMethod]
    public void NextOffer_AHeroStory_OffersBothSidesToANeutralPlayer()
    {
        _offers.Clear();
        _offers.Add(HeroOffer());

        var offer = _sut.NextOffer();

        Assert.AreEqual("breaking", offer?.EventId);
        Assert.IsTrue(offer!.Choices.Defend && offer.Choices.Attack);
    }

    [DataTestMethod]
    [DataRow(false, true, true)]
    [DataRow(false, false, false)]
    [DataRow(true, true, false)]
    [DataRow(true, false, true)]
    public void Apply_AHeroStory_TheHeroLivesWhenHisSideWins(bool playerAttacks, bool playerWon, bool lives)
    {
        var side = playerAttacks ? StorySide.Attacking : StorySide.Defending;
        var verdict = playerWon ? StoryOutcome.Won : StoryOutcome.Lost;
        _chronicle.ResolveHeroStory(Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<string>()).Returns(true);

        var result = _sut.Apply(HeroOffer(), side, verdict);

        Assert.AreEqual(lives ? StoryResult.HeroLives : StoryResult.HeroDies, result);
        _chronicle.Received(1).ResolveHeroStory("breaking", lives, Arg.Any<string>());
        _outcomes.Received(1).Pass(StoryOfferService.ResultId, Arg.Any<string>(),
            Arg.Is<string>(d => d.Contains("verdict=" + verdict) && d.EndsWith(lives ? "result=Averted hero=lord_1_75" : "result=Occurred hero=lord_1_75")));
    }

    [TestMethod]
    public void Apply_AHeroStory_NeverTouchesAFortress()
    {
        _chronicle.ResolveHeroStory(Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<string>()).Returns(true);

        _sut.Apply(HeroOffer(), StorySide.Defending, StoryOutcome.Lost);
        _sut.Apply(HeroOffer(), StorySide.Defending, StoryOutcome.Won);

        _chronicle.DidNotReceiveWithAnyArgs().ResolveStoryHeld(default, default!);
        _chronicle.DidNotReceiveWithAnyArgs().ConfirmStoryWindow(default);
        _campaign.DidNotReceiveWithAnyArgs().GiveSettlement(default!, default!, out _);
    }

    [TestMethod]
    public void Apply_AHeroStoryWhoseWindowHasClosed_ChangesNothing()
    {
        _chronicle.ResolveHeroStory(Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<string>()).Returns(false);

        Assert.AreEqual(StoryResult.Unchanged, _sut.Apply(HeroOffer(), StorySide.Defending, StoryOutcome.Won));
        _outcomes.DidNotReceiveWithAnyArgs().Pass(default!, default!, default!);
    }

    [TestMethod]
    public void Apply_AHeroStoryWithNoVerdict_AsksNothingOfTheChronicle_AndSnoozes()
    {
        Assert.AreEqual(StoryResult.Undecided, _sut.Apply(HeroOffer(), StorySide.Defending, StoryOutcome.Undecided));

        _chronicle.DidNotReceiveWithAnyArgs().ResolveHeroStory(default, default, default!);
        _offers.Clear();
        _offers.Add(HeroOffer());
        Assert.IsNull(_sut.NextOffer(), "snoozed for the day");
    }
}
