using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.CoopCompat.CareerSync;
using TAOM.CoopCompat.Messages;
using TAOM.CoopCompat.Transport;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CoopInterop;
using TAOM.Features.CoopInterop.CareerSync;

namespace TAOM.CoopCompat.Tests.CareerSync;

/// <summary>The career relay over a fake transport, with TAOM's real career sync client and server behind it.</summary>
[TestClass]
public class CareerSyncRelayServiceTests
{
    private const string Own = "Player1";
    private FakeCoopTransport _transport = null!;
    private CareerDataService _data = null!;
    private IModLogger _logger = null!;
    private CareerSyncRelayService _relay = null!;

    private static readonly CareerDefinition Ranger = new(
        id: "ranger", displayName: "Ranger", description: "", portraitSprite: "", abilityTemplateId: "volley",
        minClanTier: 0, rootChoiceId: "ranger_root", eligibleCultureIds: new List<string>(), choiceGroupIds: new List<string>());

    [TestInitialize]
    public void Setup()
    {
        _transport = new FakeCoopTransport();
        _data = new CareerDataService();
        _logger = Substitute.For<IModLogger>();
        var registry = Substitute.For<ICareerRegistry>();
        registry.GetCareer("ranger").Returns(Ranger);
        registry.GetMaxChoicesForHero(Ranger, Arg.Any<int>()).Returns(5);
        registry.IsEligible(Arg.Any<string>(), Arg.Any<ICareerHeroAdapter>()).Returns(true);
        var passives = Substitute.For<ICareerPassiveService>();
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.IsCoopClient.Returns(true);
        var player = Substitute.For<IPlayerPartyAdapter>();
        player.GetMainHeroId().Returns(Own);
        var heroes = Substitute.For<ICareerSyncHeroAdapter>();
        var level10 = Substitute.For<ICareerHeroAdapter>();
        level10.Level.Returns(10);
        heroes.Find(Arg.Any<string>()).Returns(level10);
        var quests = Substitute.For<ICareerQuestConfigProvider>();
        quests.LoadQuests().Returns(new List<CareerQuestDefinition>());

        var client = new CareerSyncClient(_data, registry, passives, coop, player, Substitute.For<ICareerSyncNotice>(), _logger);
        var host = new CareerSyncHost(_data, registry, passives, quests, heroes, new CoopSettingsProvider(), _logger);
        _relay = new CareerSyncRelayService(_transport, client, host, _logger);
    }

    private static CoopCompatCareerReport Report(string hero, long sequence = 1, bool join = false) => new()
    {
        ProtocolVersion = CoopCompatProtocol.ProtocolVersion,
        Sequence = sequence,
        JoinReport = join,
        HeroId = hero,
        CareerId = "ranger",
        Choices = new[] { "ranger_root" },
    };

    [TestMethod]
    public void ClientReady_SendsAJoinReportOfTheOwnHero_WithTheProtocolVersion()
    {
        _transport.CurrentRole = CoopRole.Client;
        _data.SetCareer(Own, "ranger");

        _transport.RaiseClientReady();

        var sent = _transport.CareerReportsSent[0];
        Assert.AreEqual(CoopCompatProtocol.ProtocolVersion, sent.ProtocolVersion);
        Assert.IsTrue(sent.JoinReport);
        Assert.AreEqual(Own, sent.HeroId);
        Assert.AreEqual("ranger", sent.CareerId);
    }

    [TestMethod]
    public void AChangeAfterTheJoin_GoesOutAtTheNextTick_Once()
    {
        _transport.CurrentRole = CoopRole.Client;
        _transport.RaiseClientReady();
        _transport.CareerReportsSent.Clear();

        _data.SetCareer(Own, "ranger");
        _data.TryAddChoice(Own, "ranger_root", 5);
        _relay.Tick();
        _relay.Tick();

        Assert.AreEqual(1, _transport.CareerReportsSent.Count);
        Assert.IsFalse(_transport.CareerReportsSent[0].JoinReport);
        CollectionAssert.AreEqual(new[] { "ranger_root" }, _transport.CareerReportsSent[0].Choices);
    }

    [TestMethod]
    public void AnAcceptedReport_IsStored_AndGoesToEveryoneButTheSender()
    {
        _transport.CurrentRole = CoopRole.Server;
        var peer = FakeCoopTransport.Peer();

        _transport.RaiseCareerReport(peer, Own, Report(Own, sequence: 4));

        Assert.AreEqual("ranger", _data.GetCareerStringId(Own));
        Assert.AreEqual(0, _transport.CareerUpdatesToPeer.Count);
        var (except, update) = _transport.CareerBroadcastsExcept[0];
        Assert.AreSame(peer, except);
        Assert.AreEqual(Own, update.HeroId);
        Assert.AreEqual(4, update.Sequence);
        Assert.IsTrue(update.Known);
        Assert.AreEqual(CoopCompatProtocol.ProtocolVersion, update.ProtocolVersion);
        Assert.AreEqual(0, _transport.CareerBroadcasts.Count, "storing a report raises no second broadcast");
    }

    [TestMethod]
    public void AReportForAnotherHero_IsDropped_TheConnectionDecides()
    {
        _transport.CurrentRole = CoopRole.Server;

        _transport.RaiseCareerReport(FakeCoopTransport.Peer(), "Player2", Report(Own));

        Assert.IsFalse(_data.HasCareer(Own));
        Assert.IsFalse(_data.HasCareer("Player2"));
        Assert.AreEqual(0, _transport.CareerBroadcastsExcept.Count);
        Assert.AreEqual(0, _transport.CareerUpdatesToPeer.Count);
    }

    [TestMethod]
    public void ARefusedReport_AnswersTheSenderOnly()
    {
        _transport.CurrentRole = CoopRole.Server;
        var peer = FakeCoopTransport.Peer();
        var bad = Report(Own, sequence: 2);
        bad.CareerId = "no_such_career";

        _transport.RaiseCareerReport(peer, Own, bad);

        var (to, update) = _transport.CareerUpdatesToPeer[0];
        Assert.AreSame(peer, to);
        Assert.IsTrue(update.Refused);
        Assert.IsFalse(update.Known);
        Assert.AreEqual(2, update.Sequence);
        Assert.AreEqual(0, _transport.CareerBroadcastsExcept.Count);
    }

    [TestMethod]
    public void AnUpdateOnAClient_IsWritten()
    {
        _transport.CurrentRole = CoopRole.Client;

        _transport.RaiseCareerUpdate(new CoopCompatCareerUpdate
        {
            ProtocolVersion = CoopCompatProtocol.ProtocolVersion, HeroId = "Player2", CareerId = "ranger",
            Choices = new[] { "ranger_root" }, Known = true,
        });

        Assert.AreEqual("ranger", _data.GetCareerStringId("Player2"));
    }

    [TestMethod]
    public void AChangeTheServerMakesItself_IsBroadcast_OnTheServerOnly()
    {
        _transport.CurrentRole = CoopRole.Client;
        _data.SetCareer("Player2", "ranger");
        Assert.AreEqual(0, _transport.CareerBroadcasts.Count, "a client never broadcasts");

        _transport.CurrentRole = CoopRole.Server;
        _data.ClearCareer("Player2");

        Assert.AreEqual(1, _transport.CareerBroadcasts.Count);
        Assert.AreEqual("Player2", _transport.CareerBroadcasts[0].HeroId);
        Assert.AreEqual("", _transport.CareerBroadcasts[0].CareerId);
    }

    [TestMethod]
    public void AThrowingSend_OnTheFrameTick_StaysInTheRelay_AndIsLoggedOnce()
    {
        // Tick runs every frame outside Coop's GameThread.RunSafe, so a throw would reach the engine's tick.
        _transport.CurrentRole = CoopRole.Client;
        _transport.RaiseClientReady();
        _transport.CareerSendsThrow = true;

        foreach (var career in new[] { "ranger", "warden" })
        {
            _data.SetCareer(Own, career);
            try { _relay.Tick(); }
            catch (Exception e) { Assert.Fail("the tick let a send failure escape: " + e.Message); }
        }

        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("could not send a career report")));
    }

    [TestMethod]
    public void AThrowingBroadcast_OfAServerChange_StaysInTheRelay_AndIsLoggedOnce()
    {
        // The server's own change arrives inside the career book's mutator (here ClearCareer, as from a death).
        _transport.CurrentRole = CoopRole.Server;
        _data.SetCareer("Player2", "ranger");
        _data.SetCareer("Player3", "ranger");
        _transport.CareerSendsThrow = true;

        try
        {
            _data.ClearCareer("Player2");
            _data.ClearCareer("Player3");
        }
        catch (Exception e) { Assert.Fail("a broadcast failure escaped into the career book: " + e.Message); }

        Assert.IsFalse(_data.HasCareer("Player2"), "the book's own change still happened");
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("could not pass on a change")));
    }

    [TestMethod]
    public void AnotherProtocol_IsDropped_AndLoggedOnce()
    {
        _transport.CurrentRole = CoopRole.Server;
        var old = Report(Own);
        old.ProtocolVersion = CoopCompatProtocol.ProtocolVersion + 1;

        _transport.RaiseCareerReport(FakeCoopTransport.Peer(), Own, old);
        _transport.RaiseCareerUpdate(new CoopCompatCareerUpdate { ProtocolVersion = 0, HeroId = "Player2", CareerId = "ranger", Known = true });

        Assert.IsFalse(_data.HasCareer(Own));
        Assert.IsFalse(_data.HasCareer("Player2"));
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("dropped a career message")));
    }

    [TestMethod]
    public void AnUpdateWithNoCareerId_IsAClearedCareer()
    {
        _transport.CurrentRole = CoopRole.Client;
        _data.SetCareer("Player2", "ranger");

        _transport.RaiseCareerUpdate(new CoopCompatCareerUpdate { ProtocolVersion = CoopCompatProtocol.ProtocolVersion, HeroId = "Player2", Known = true });

        Assert.IsFalse(_data.HasCareer("Player2"));
        Assert.IsNull(_data.GetCareerStringId("Player2"));
    }

    [TestMethod]
    public void Hello_OnTheServer_SendsThatPlayerTheStoredCareerOfEveryReportedHero()
    {
        _transport.CurrentRole = CoopRole.Server;
        _transport.RaiseCareerReport(FakeCoopTransport.Peer("1"), Own, Report(Own, sequence: 2));
        var joiner = FakeCoopTransport.Peer("2");

        _transport.RaiseHello(joiner, new CoopCompatHello());

        var sent = _transport.CareerUpdatesToPeer.Where(x => ReferenceEquals(x.Peer, joiner)).Select(x => x.Update).ToList();
        Assert.AreEqual(1, sent.Count);
        Assert.AreEqual(Own, sent[0].HeroId);
        Assert.AreEqual("ranger", sent[0].CareerId);
        Assert.AreEqual(0, sent[0].Sequence);
        Assert.IsTrue(sent[0].Known);
        Assert.AreEqual(CoopCompatProtocol.ProtocolVersion, sent[0].ProtocolVersion);
    }

    [TestMethod]
    public void Hello_OnAClient_SendsNoBaseline()
    {
        _transport.CurrentRole = CoopRole.Client;

        _transport.RaiseHello(FakeCoopTransport.Peer(), new CoopCompatHello());

        Assert.AreEqual(0, _transport.CareerUpdatesToPeer.Count);
    }
}
