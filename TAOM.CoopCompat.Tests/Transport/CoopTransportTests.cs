using System;
using Autofac;
using Common;
using Common.Messaging;
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

        BeginSession(server: false);
        MessageBroker.Instance.Publish(FakePeer(), new CoopCompatHelloReply());
        MessageBroker.Instance.Publish(FakePeer(), new CoopCompatMomentumSnapshot());
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

        BeginSession(server: false);
        MessageBroker.Instance.Publish(FakePeer(), new CoopCompatTradeRequest());
        MessageBroker.Instance.Publish(FakePeer(), new CoopCompatTradeReply());
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
}
