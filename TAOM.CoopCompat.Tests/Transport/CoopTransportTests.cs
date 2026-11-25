using System;
using Autofac;
using Common;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Messages;
using GameInterface;
using GameInterface.Services.Players;
using LiteNetLib;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.CoopCompat.Messages;
using TAOM.CoopCompat.Transport;

namespace TAOM.CoopCompat.Tests.Transport;

/// <summary>
/// CoopTransport against Coop's real broker and container provider (process-wide statics, so the
/// tests run one at a time and put them back). The game-thread marshal is replaced by a direct call.
/// </summary>
[TestClass]
public class CoopTransportTests
{
    private CoopTransport _transport = null!;
    private int _marshalled;

    [TestInitialize]
    public void Setup()
    {
        ContainerProvider.Clear();
        ModInformation.IsServer = false;
        _marshalled = 0;
        _transport = new CoopTransport(a => { _marshalled++; a(); });
        _transport.Start();
    }

    [TestCleanup]
    public void Teardown()
    {
        ContainerProvider.Clear();
        ModInformation.IsServer = false;
    }

    private static void BeginSession(bool server)
    {
        ContainerProvider.SetContainer(new ContainerBuilder().Build());
        ModInformation.IsServer = server;
    }

    private static INetwork BeginSessionWithNetwork(bool server)
    {
        var network = Substitute.For<INetwork>();
        var builder = new ContainerBuilder();
        builder.RegisterInstance(network).As<INetwork>();
        ContainerProvider.SetContainer(builder.Build());
        ModInformation.IsServer = server;
        return network;
    }

    /// <summary>The client's hello, which carries the session token that the server echoes; returns that token.</summary>
    private static string SayHello(CoopTransport transport)
    {
        var hello = new CoopCompatHello();
        Assert.IsTrue(transport.SendToServer(hello));
        return hello.SessionToken;
    }

    private static NetPeer FakePeer() => (NetPeer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(NetPeer));

    [TestMethod]
    public void Role_WithNoContainer_IsNoSession_EvenWhenIsServerIsSet()
    {
        ModInformation.IsServer = true;

        Assert.AreEqual(CoopRole.NoSession, _transport.CurrentRole);
    }

    [TestMethod]
    public void Role_IsReadWhenAsked_NotCached()
    {
        BeginSession(server: false);
        Assert.AreEqual(CoopRole.Client, _transport.CurrentRole);

        ModInformation.IsServer = true;
        Assert.AreEqual(CoopRole.Server, _transport.CurrentRole);

        ContainerProvider.Clear();
        Assert.AreEqual(CoopRole.NoSession, _transport.CurrentRole);
    }

    [TestMethod]
    public void Hello_OnServer_RaisesWithThePeer_OnTheGameThread()
    {
        BeginSession(server: true);
        CoopPeer? got = null;
        CoopCompatHello? hello = null;
        _transport.HelloReceived += (p, h) => { got = p; hello = h; };
        var sent = new CoopCompatHello { ProtocolVersion = 1, TaomVersion = "v1" };

        MessageBroker.Instance.Publish(FakePeer(), sent);

        Assert.IsNotNull(got);
        Assert.AreEqual("v1", hello!.TaomVersion);
        Assert.AreEqual(1, _marshalled);
    }

    [TestMethod]
    public void Hello_OnClientOrWithoutSession_IsIgnored()
    {
        var raised = 0;
        _transport.HelloReceived += (_, _) => raised++;

        MessageBroker.Instance.Publish(FakePeer(), new CoopCompatHello());
        BeginSession(server: false);
        MessageBroker.Instance.Publish(FakePeer(), new CoopCompatHello());

        Assert.AreEqual(0, raised);
        Assert.AreEqual(0, _marshalled);
    }

    [TestMethod]
    public void Hello_WithoutANetPeerSender_IsIgnored()
    {
        BeginSession(server: true);
        var raised = 0;
        _transport.HelloReceived += (_, _) => raised++;

        MessageBroker.Instance.Publish("not a peer", new CoopCompatHello());

        Assert.AreEqual(0, raised);
    }

    [TestMethod]
    public void ReplyAndSnapshot_OnClient_Raise_OnServer_AreIgnored()
    {
        var replies = 0;
        var snapshots = 0;
        _transport.HelloReplyReceived += _ => replies++;
        _transport.SnapshotReceived += _ => snapshots++;

        BeginSessionWithNetwork(server: false);
        var token = SayHello(_transport);
        MessageBroker.Instance.Publish(FakePeer(), new CoopCompatHelloReply { SessionToken = token });
        MessageBroker.Instance.Publish(FakePeer(), new CoopCompatMomentumSnapshot { SessionToken = token });
        Assert.AreEqual(1, replies);
        Assert.AreEqual(1, snapshots);

        ModInformation.IsServer = true;
        MessageBroker.Instance.Publish(FakePeer(), new CoopCompatHelloReply());
        MessageBroker.Instance.Publish(FakePeer(), new CoopCompatMomentumSnapshot());
        Assert.AreEqual(1, replies);
        Assert.AreEqual(1, snapshots);
    }

    [TestMethod]
    public void ClientCampaignReady_RaisesClientReady_OnlyOnAClient()
    {
        var raised = 0;
        _transport.ClientReady += () => raised++;

        BeginSession(server: true);
        MessageBroker.Instance.Publish(this, new ClientCampaignReady());
        Assert.AreEqual(0, raised);

        ModInformation.IsServer = false;
        MessageBroker.Instance.Publish(this, new ClientCampaignReady());
        Assert.AreEqual(1, raised);
    }

    [TestMethod]
    public void Start_CalledTwice_SubscribesOnce()
    {
        BeginSession(server: false);
        var raised = 0;
        _transport.ClientReady += () => raised++;
        _transport.Start();

        MessageBroker.Instance.Publish(this, new ClientCampaignReady());

        Assert.AreEqual(1, raised);
    }

    [TestMethod]
    public void Subscriptions_SurviveAGarbageCollection_WhileTheTransportIsHeld()
    {
        BeginSession(server: false);
        var raised = 0;
        _transport.ClientReady += () => raised++;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        MessageBroker.Instance.Publish(this, new ClientCampaignReady());

        Assert.AreEqual(1, raised);
    }

    [TestMethod]
    public void Sends_WithTheWrongRoleOrNoSession_ReturnFalse()
    {
        var peer = new CoopPeer(FakePeer());

        Assert.IsFalse(_transport.SendToServer(new CoopCompatHello()));
        Assert.IsFalse(_transport.Broadcast(new CoopCompatMomentumSnapshot()));
        Assert.IsFalse(_transport.SendToPeer(peer, new CoopCompatHelloReply()));
        Assert.IsFalse(_transport.SendToPeer(peer, new CoopCompatMomentumSnapshot()));

        BeginSession(server: true);
        Assert.IsFalse(_transport.SendToServer(new CoopCompatHello()));

        ModInformation.IsServer = false;
        Assert.IsFalse(_transport.Broadcast(new CoopCompatMomentumSnapshot()));
        Assert.IsFalse(_transport.SendToPeer(peer, new CoopCompatHelloReply()));
    }

    [TestMethod]
    public void Sends_WhenTheContainerHoldsNoNetwork_ReturnFalse()
    {
        BeginSession(server: false);

        Assert.IsFalse(_transport.SendToServer(new CoopCompatHello()));

        ModInformation.IsServer = true;
        Assert.IsFalse(_transport.Broadcast(new CoopCompatMomentumSnapshot()));
    }

    [TestMethod]
    public void TradeRequest_OnServer_RaisesWithTheConnectionsHero_OnTheGameThread()
    {
        var peerSeen = default(NetPeer);
        var transport = new CoopTransport(a => { _marshalled++; a(); }, p => { peerSeen = p; return "hero_1"; });
        transport.Start();
        BeginSession(server: true);
        string? heroId = null;
        CoopCompatTradeRequest? got = null;
        transport.TradeRequestReceived += (_, h, r) => { heroId = h; got = r; };
        var sender = FakePeer();

        MessageBroker.Instance.Publish(sender, new CoopCompatTradeRequest { RequestId = "r1" });

        Assert.AreEqual("hero_1", heroId);
        Assert.AreEqual("r1", got!.RequestId);
        Assert.AreSame(sender, peerSeen);
        Assert.IsTrue(_marshalled >= 1);
    }

    [TestMethod]
    public void TradeRequest_FromAConnectionThatStandsForNoHero_IsRaisedWithNoHero_SoTheHostAnswersIt()
    {
        var transport = new CoopTransport(a => a(), _ => null);
        transport.Start();
        BeginSession(server: true);
        var heroes = new System.Collections.Generic.List<string>();
        transport.TradeRequestReceived += (_, h, _) => heroes.Add(h);

        MessageBroker.Instance.Publish(FakePeer(), new CoopCompatTradeRequest());
        MessageBroker.Instance.Publish("not a peer", new CoopCompatTradeRequest());

        CollectionAssert.AreEqual(new[] { "" }, heroes, "a peer gets a refusal; a sender that is no peer cannot be answered");
    }

    [TestMethod]
    public void HeroOfCurrentController_IsTheHerosStringId_NotCoopsObjectKey()
    {
        // Coop keys a player's hero as "Hero_" + StringId (Player.HeroId); CampaignObjectManager.Find matches StringId only.
        var peer = FakePeer();
        var hero = (Hero)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Hero));
        hero.StringId = "Player1";
        var players = Substitute.For<IPlayerManager>();
        players.TryGetCurrentController(Arg.Any<NetPeer>(), out Arg.Any<string>()).Returns(x => { x[1] = "controller"; return true; });
        players.TryGetSenderHero(Arg.Any<object>(), out Arg.Any<Hero>()).Returns(x => { x[1] = hero; return true; });

        Assert.AreEqual("Player1", CoopTransport.HeroOfCurrentController(players, peer));
    }

    [TestMethod]
    public void HeroOfCurrentController_IsNull_ForAConnectionNoLongerCurrent_OrWithNoHero()
    {
        var peer = FakePeer();
        var hero = (Hero)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Hero));
        hero.StringId = "Player1";
        var players = Substitute.For<IPlayerManager>();
        players.TryGetSenderHero(Arg.Any<object>(), out Arg.Any<Hero>()).Returns(x => { x[1] = hero; return true; });
        players.TryGetCurrentController(Arg.Any<NetPeer>(), out Arg.Any<string>()).Returns(false);

        Assert.IsNull(CoopTransport.HeroOfCurrentController(players, peer), "a reconnect leaves the old peer standing for nobody");

        players.TryGetCurrentController(Arg.Any<NetPeer>(), out Arg.Any<string>()).Returns(x => { x[1] = "controller"; return true; });
        players.TryGetSenderHero(Arg.Any<object>(), out Arg.Any<Hero>()).Returns(false);

        Assert.IsNull(CoopTransport.HeroOfCurrentController(players, peer));
    }

    [TestMethod]
    public void TradeRequest_OnAClient_IsIgnored_AndTradeReply_OnlyOnAClient()
    {
        var transport = new CoopTransport(a => a(), _ => "hero_1");
        transport.Start();
        var requests = 0;
        var replies = 0;
        transport.TradeRequestReceived += (_, _, _) => requests++;
        transport.TradeReplyReceived += _ => replies++;

        BeginSessionWithNetwork(server: false);
        var token = SayHello(transport);
        MessageBroker.Instance.Publish(FakePeer(), new CoopCompatTradeRequest());
        MessageBroker.Instance.Publish(FakePeer(), new CoopCompatTradeReply { SessionToken = token });
        Assert.AreEqual(0, requests);
        Assert.AreEqual(1, replies);

        ModInformation.IsServer = true;
        MessageBroker.Instance.Publish(FakePeer(), new CoopCompatTradeReply());
        Assert.AreEqual(1, replies);
    }

    [TestMethod]
    public void TradeSends_WithTheWrongRole_ReturnFalse()
    {
        var peer = new CoopPeer(FakePeer());
        BeginSession(server: true);
        Assert.IsFalse(_transport.SendToServer(new CoopCompatTradeRequest()));

        ModInformation.IsServer = false;
        Assert.IsFalse(_transport.SendToPeer(peer, new CoopCompatTradeReply()));
    }

    [TestMethod]
    public void CareerReport_OnServer_CarriesTheConnectionsHero_NotTheReportsHero()
    {
        var transport = new CoopTransport(a => { _marshalled++; a(); }, _ => "Player1");
        transport.Start();
        BeginSession(server: true);
        string? heroId = null;
        CoopCompatCareerReport? got = null;
        transport.CareerReportReceived += (_, h, r) => { heroId = h; got = r; };

        MessageBroker.Instance.Publish(FakePeer(), new CoopCompatCareerReport { HeroId = "Player2", Sequence = 5 });
        MessageBroker.Instance.Publish("not a peer", new CoopCompatCareerReport());

        Assert.AreEqual("Player1", heroId, "the server names the hero from the connection; the report's id is only compared");
        Assert.AreEqual("Player2", got!.HeroId);
        Assert.AreEqual(5, got.Sequence);
        Assert.IsTrue(_marshalled >= 1);
    }

    [TestMethod]
    public void CareerReport_OnAClient_IsIgnored_AndCareerUpdate_OnlyOnAClient()
    {
        var transport = new CoopTransport(a => a(), _ => "Player1");
        transport.Start();
        var reports = 0;
        var updates = 0;
        transport.CareerReportReceived += (_, _, _) => reports++;
        transport.CareerUpdateReceived += _ => updates++;

        BeginSessionWithNetwork(server: false);
        var token = SayHello(transport);
        MessageBroker.Instance.Publish(FakePeer(), new CoopCompatCareerReport());
        MessageBroker.Instance.Publish(FakePeer(), new CoopCompatCareerUpdate { SessionToken = token });
        Assert.AreEqual(0, reports);
        Assert.AreEqual(1, updates);

        ModInformation.IsServer = true;
        MessageBroker.Instance.Publish(FakePeer(), new CoopCompatCareerUpdate());
        Assert.AreEqual(1, updates);
    }

    [TestMethod]
    public void CareerSends_WithTheWrongRole_ReturnFalse()
    {
        var peer = new CoopPeer(FakePeer());
        BeginSession(server: true);
        Assert.IsFalse(_transport.SendToServer(new CoopCompatCareerReport()));

        ModInformation.IsServer = false;
        Assert.IsFalse(_transport.SendToPeer(peer, new CoopCompatCareerUpdate()));
        Assert.IsFalse(_transport.Broadcast(new CoopCompatCareerUpdate()));
        Assert.IsFalse(_transport.BroadcastExcept(peer, new CoopCompatCareerUpdate()));
    }

    // --- Session token (Codex P1 on #1471): a mission peer can reach a client's broker directly or through Coop's relay ---

    [TestMethod]
    public void ServerMessages_OnAClient_WithAnotherTokenOrNone_AreDropped()
    {
        var transport = new CoopTransport(a => a());
        transport.Start();
        var raised = 0;
        transport.HelloReplyReceived += _ => raised++;
        transport.SnapshotReceived += _ => raised++;
        transport.TradeReplyReceived += _ => raised++;
        transport.CareerUpdateReceived += _ => raised++;
        BeginSessionWithNetwork(server: false);
        SayHello(transport);

        foreach (var token in new[] { "forged", "" })
        {
            MessageBroker.Instance.Publish(FakePeer(), new CoopCompatHelloReply { SessionToken = token });
            MessageBroker.Instance.Publish(FakePeer(), new CoopCompatMomentumSnapshot { SessionToken = token });
            MessageBroker.Instance.Publish(FakePeer(), new CoopCompatTradeReply { SessionToken = token });
            MessageBroker.Instance.Publish(FakePeer(), new CoopCompatCareerUpdate { SessionToken = token, Known = false });
        }

        Assert.AreEqual(0, raised);
    }

    [TestMethod]
    public void ServerMessages_BeforeTheClientsHello_AreDropped()
    {
        var transport = new CoopTransport(a => a());
        transport.Start();
        var raised = 0;
        transport.CareerUpdateReceived += _ => raised++;
        BeginSessionWithNetwork(server: false);

        MessageBroker.Instance.Publish(FakePeer(), new CoopCompatCareerUpdate { SessionToken = "" });

        Assert.AreEqual(0, raised);
    }

    [TestMethod]
    public void HelloReply_WithNoToken_IsRaised_OnlyAsABuildMismatch()
    {
        var transport = new CoopTransport(a => a());
        transport.Start();
        var replies = new System.Collections.Generic.List<CoopCompatHelloReply>();
        transport.HelloReplyReceived += replies.Add;
        BeginSessionWithNetwork(server: false);
        SayHello(transport);

        MessageBroker.Instance.Publish(FakePeer(), new CoopCompatHelloReply { BuildMismatch = false });
        MessageBroker.Instance.Publish(FakePeer(), new CoopCompatHelloReply
        {
            ProtocolVersion = 1, BuildMismatch = true, Verdict = 2, DivergentGroups = new[] { "forged" },
            LoadOrderDiffers = true, LoadOrderHostFirst = "a", LoadOrderHostSecond = "b",
        });

        Assert.AreEqual(1, replies.Count, "a host built before the token sends none, and a newer client always differs from it");
        Assert.IsTrue(replies[0].BuildMismatch);
        Assert.AreEqual(0, replies[0].ProtocolVersion, "an unknown protocol makes the handshake inconclusive, never a match");
        Assert.AreEqual(0, replies[0].DivergentGroups.Length, "no group list or load order comes from a reply with no token");
        Assert.IsFalse(replies[0].LoadOrderDiffers);
    }

    [TestMethod]
    public void AHelloInTheSameSession_KeepsTheToken_SoAnAnswerOnTheWayStillCounts()
    {
        var transport = new CoopTransport(a => a());
        transport.Start();
        var raised = 0;
        transport.CareerUpdateReceived += _ => raised++;
        BeginSessionWithNetwork(server: false);

        var first = SayHello(transport);
        var second = SayHello(transport);
        MessageBroker.Instance.Publish(FakePeer(), new CoopCompatCareerUpdate { SessionToken = first });

        Assert.IsTrue(first.Length >= 16);
        Assert.AreEqual(first, second, "Coop raises ClientCampaignReady after every mission, so the hello comes again");
        Assert.AreEqual(1, raised);
    }

    [TestMethod]
    public void ANewSession_GetsANewToken_AndTheOldOneStopsCounting_BeforeTheNewHello()
    {
        var transport = new CoopTransport(a => a());
        transport.Start();
        var raised = 0;
        transport.CareerUpdateReceived += _ => raised++;
        BeginSessionWithNetwork(server: false);
        var first = SayHello(transport);

        BeginSessionWithNetwork(server: false);
        MessageBroker.Instance.Publish(FakePeer(), new CoopCompatCareerUpdate { SessionToken = first });
        var second = SayHello(transport);

        Assert.AreEqual(0, raised);
        Assert.AreNotEqual(first, second);
    }

    [TestMethod]
    public void ADroppedServerMessage_IsLoggedOncePerSession()
    {
        var warnings = new System.Collections.Generic.List<string>();
        var transport = new CoopTransport(a => a(), null, null, warnings.Add);
        transport.Start();
        BeginSessionWithNetwork(server: false);
        SayHello(transport);

        MessageBroker.Instance.Publish(FakePeer(), new CoopCompatCareerUpdate { SessionToken = "forged" });
        MessageBroker.Instance.Publish(FakePeer(), new CoopCompatTradeReply { SessionToken = "forged" });
        Assert.AreEqual(1, warnings.Count);

        BeginSessionWithNetwork(server: false);
        MessageBroker.Instance.Publish(FakePeer(), new CoopCompatMomentumSnapshot { SessionToken = "forged" });
        Assert.AreEqual(2, warnings.Count);
    }

    [TestMethod]
    public void AHello_ForgetsClosedConnections()
    {
        var a = FakePeer();
        var b = FakePeer();
        var aClosed = false;
        var transport = new CoopTransport(x => x(), _ => null, p => !(aClosed && ReferenceEquals(p, a)));
        transport.Start();
        BeginSessionWithNetwork(server: true);
        MessageBroker.Instance.Publish(a, new CoopCompatHello { SessionToken = "tA" });
        aClosed = true;

        MessageBroker.Instance.Publish(b, new CoopCompatHello { SessionToken = "tB" });

        Assert.AreEqual(1, transport.KnownConnections);
    }

    [TestMethod]
    public void SendToPeer_OnServer_StampsThatPeersOwnToken()
    {
        var transport = new CoopTransport(a => a(), _ => null, _ => true);
        transport.Start();
        var network = BeginSessionWithNetwork(server: true);
        var known = FakePeer();
        var stranger = FakePeer();
        var stamps = new System.Collections.Generic.List<string>();
        // An uninitialized NetPeer cannot be formatted or compared by NSubstitute, so the calls are captured by hand.
        network.When(n => n.Send(Arg.Any<NetPeer>(), Arg.Any<IMessage>())).Do(c => stamps.Add(((ISessionStamped)c.ArgAt<IMessage>(1)).SessionToken));
        MessageBroker.Instance.Publish(known, new CoopCompatHello { SessionToken = "tokenA" });

        Assert.IsTrue(transport.SendToPeer(new CoopPeer(known), new CoopCompatCareerUpdate()));
        Assert.IsTrue(transport.SendToPeer(new CoopPeer(known), new CoopCompatTradeReply()));
        Assert.IsTrue(transport.SendToPeer(new CoopPeer(stranger), new CoopCompatCareerUpdate()));

        CollectionAssert.AreEqual(new[] { "tokenA", "tokenA", "" }, stamps);
    }

    [TestMethod]
    public void HelloReply_IsStamped_WithTheTokenOfTheHelloItAnswers()
    {
        var transport = new CoopTransport(a => a(), _ => null, _ => true);
        transport.Start();
        var network = BeginSessionWithNetwork(server: true);
        var peer = FakePeer();
        string? stamped = null;
        network.When(n => n.Send(Arg.Any<NetPeer>(), Arg.Any<IMessage>())).Do(c => stamped = ((ISessionStamped)c.ArgAt<IMessage>(1)).SessionToken);
        void Answer(CoopPeer p, CoopCompatHello _) => transport.SendToPeer(p, new CoopCompatHelloReply());
        transport.HelloReceived += Answer;

        MessageBroker.Instance.Publish(peer, new CoopCompatHello { SessionToken = "tokenA" });
        transport.HelloReceived -= Answer;

        Assert.AreEqual("tokenA", stamped, "the token is known before the hello is raised");
    }

    [TestMethod]
    public void Broadcasts_OnServer_GoToEachLivePeerThatSaidHello_WithItsOwnToken()
    {
        var a = FakePeer();
        var b = FakePeer();
        var gone = FakePeer();
        var transport = new CoopTransport(x => x(), _ => null, p => !ReferenceEquals(p, gone));
        transport.Start();
        var network = BeginSessionWithNetwork(server: true);
        var sent = new System.Collections.Generic.List<(NetPeer Peer, string Token)>();
        network.When(n => n.Send(Arg.Any<NetPeer>(), Arg.Any<IMessage>()))
            .Do(c => sent.Add((c.ArgAt<NetPeer>(0), ((ISessionStamped)c.ArgAt<IMessage>(1)).SessionToken)));
        MessageBroker.Instance.Publish(a, new CoopCompatHello { SessionToken = "tA" });
        MessageBroker.Instance.Publish(b, new CoopCompatHello { SessionToken = "tB" });
        MessageBroker.Instance.Publish(gone, new CoopCompatHello { SessionToken = "tG" });

        Assert.IsTrue(transport.Broadcast(new CoopCompatMomentumSnapshot()));
        Assert.IsTrue(transport.Broadcast(new CoopCompatCareerUpdate()));
        Assert.IsTrue(transport.BroadcastExcept(new CoopPeer(a), new CoopCompatCareerUpdate()));

        Assert.AreEqual(5, sent.Count);
        Assert.IsTrue(ReferenceEquals(a, sent[0].Peer) && sent[0].Token == "tA");
        Assert.IsTrue(ReferenceEquals(b, sent[1].Peer) && sent[1].Token == "tB");
        Assert.IsTrue(ReferenceEquals(a, sent[2].Peer) && sent[2].Token == "tA");
        Assert.IsTrue(ReferenceEquals(b, sent[3].Peer) && sent[3].Token == "tB");
        Assert.IsTrue(ReferenceEquals(b, sent[4].Peer) && sent[4].Token == "tB", "BroadcastExcept skips the excluded peer");
        network.DidNotReceive().SendAll(Arg.Any<IMessage>());
        network.DidNotReceive().SendAllBut(Arg.Any<NetPeer>(), Arg.Any<IMessage>());
    }

    [TestMethod]
    public void Hello_WithAnOversizeToken_IsStoredAsNone()
    {
        var transport = new CoopTransport(a => a(), _ => null, _ => true);
        transport.Start();
        var network = BeginSessionWithNetwork(server: true);
        var peer = FakePeer();
        string? stamped = null;
        network.When(n => n.Send(Arg.Any<NetPeer>(), Arg.Any<IMessage>())).Do(c => stamped = ((ISessionStamped)c.ArgAt<IMessage>(1)).SessionToken);
        MessageBroker.Instance.Publish(peer, new CoopCompatHello { SessionToken = new string('x', 65) });

        transport.SendToPeer(new CoopPeer(peer), new CoopCompatCareerUpdate());

        Assert.AreEqual("", stamped);
    }
}
