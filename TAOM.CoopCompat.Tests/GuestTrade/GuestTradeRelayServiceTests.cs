using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.CoopCompat.GuestTrade;
using TAOM.CoopCompat.Messages;
using TAOM.CoopCompat.Transport;
using TAOM.Features.CoopInterop;
using TAOM.Features.ArmourAcquisition.Domain;
using TAOM.Features.CoopInterop.GuestTrade;
using TAOM.Features.EliteEmissary;
using TAOM.Features.EliteEmissary.Domain;
using TAOM.Features.SpecialResources;
using TAOM.Features.SpecialResources.Domain;

namespace TAOM.CoopCompat.Tests.GuestTrade;

[TestClass]
public class GuestTradeRelayServiceTests
{
    private FakeCoopTransport _transport = null!;
    private ISpecialResourceStorageService _storage = null!;
    private IGuestTradeHeroAdapter _heroes = null!;
    private GuestTradeClient _client = null!;
    private readonly List<GuestTradeReply> _answers = new();

    [TestInitialize]
    public void Setup()
    {
        _transport = new FakeCoopTransport();
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.ShouldDeferToHost.Returns(true);
        var player = Substitute.For<IPlayerPartyAdapter>();
        player.GetMainHeroId().Returns("guest_hero");
        _storage = Substitute.For<ISpecialResourceStorageService>();
        _heroes = Substitute.For<IGuestTradeHeroAdapter>();
        var logger = Substitute.For<IModLogger>();
        _client = new GuestTradeClient(coop, player, _storage, logger);
        var host = new GuestTradeHost(_heroes, Substitute.For<IGuestTradeShops>(), Substitute.For<ISpecialResourceService>(), logger);
        _answers.Clear();
        _ = new GuestTradeRelayService(_transport, _client, host, logger);
    }

    [TestMethod]
    public void TheRelay_AttachesItselfToTheClient()
    {
        Assert.IsTrue(_client.IsGuest);
    }

    [TestMethod]
    public void AGuestRequest_GoesToTheServer_WithTheProtocolVersion()
    {
        _client.Request(new GuestTradeRequest(GuestTradeAction.ArmouryUpgrade, "town_A1", "mail", null, "plate"), _answers.Add);

        var sent = _transport.TradeRequestsSent[0];
        Assert.AreEqual(CoopCompatProtocol.ProtocolVersion, sent.ProtocolVersion);
        Assert.AreEqual((int)GuestTradeAction.ArmouryUpgrade, sent.Action);
        Assert.AreEqual("town_A1", sent.SettlementId);
        Assert.AreEqual("mail", sent.Subject);
        Assert.AreEqual("", sent.Modifier);
        Assert.AreEqual("plate", sent.Target);
        Assert.IsFalse(string.IsNullOrEmpty(sent.RequestId));
    }

    [TestMethod]
    public void ARequestOnTheServer_IsHandledForTheConnectionsHero_AndAnsweredToThatPeer()
    {
        var peer = FakeCoopTransport.Peer();

        _transport.RaiseTradeRequest(peer, "guest_hero", new CoopCompatTradeRequest
        {
            ProtocolVersion = CoopCompatProtocol.ProtocolVersion, RequestId = "r1", Action = (int)GuestTradeAction.EmissaryBalance,
            SettlementId = "town_A1",
        });

        _heroes.Received(1).Find("guest_hero");
        var (to, reply) = _transport.TradeRepliesSent[0];
        Assert.AreSame(peer, to);
        Assert.AreEqual("r1", reply.RequestId);
        Assert.AreEqual((int)GuestTradeRefusal.NotFreeLeader, reply.Refusal);
        Assert.AreEqual(CoopCompatProtocol.ProtocolVersion, reply.ProtocolVersion);
        Assert.IsFalse(reply.HasBalance);
    }

    [TestMethod]
    public void AnotherProtocolVersion_IsDroppedBothWays()
    {
        _transport.RaiseTradeRequest(FakeCoopTransport.Peer(), "guest_hero", new CoopCompatTradeRequest
        {
            ProtocolVersion = CoopCompatProtocol.ProtocolVersion + 1, RequestId = "r1", Action = 1, SettlementId = "town_A1",
        });
        Assert.AreEqual(0, _transport.TradeRepliesSent.Count);

        _client.Request(new GuestTradeRequest(GuestTradeAction.EmissaryBalance, "town_A1"), _answers.Add);
        _transport.RaiseTradeReply(new CoopCompatTradeReply
        {
            ProtocolVersion = CoopCompatProtocol.ProtocolVersion + 1, RequestId = _transport.TradeRequestsSent[0].RequestId,
        });
        Assert.AreEqual(0, _answers.Count);
    }

    [TestMethod]
    public void AReply_ReachesTheClient_WithTheBalanceOnlyWhenTheHostSentOne()
    {
        _client.Request(new GuestTradeRequest(GuestTradeAction.EmissaryBalance, "town_A1"), _answers.Add);
        _transport.RaiseTradeReply(new CoopCompatTradeReply
        {
            ProtocolVersion = CoopCompatProtocol.ProtocolVersion, RequestId = _transport.TradeRequestsSent[0].RequestId,
            Action = (int)GuestTradeAction.EmissaryBalance, ResourceId = "horses", ResourceName = "Horses", HasBalance = true, Balance = 30f,
            HasArmouryLevel = true, ArmouryLevel = 3,
        });

        Assert.AreEqual(1, _answers.Count);
        Assert.AreEqual(30f, _answers[0].Balance);
        Assert.AreEqual(3, _answers[0].ArmouryLevel);
        _storage.Received(1).Set("guest_hero", "horses", 30f);

        _client.Request(new GuestTradeRequest(GuestTradeAction.EmissaryBalance, "town_A1"), _answers.Add);
        _transport.RaiseTradeReply(new CoopCompatTradeReply
        {
            ProtocolVersion = CoopCompatProtocol.ProtocolVersion, RequestId = _transport.TradeRequestsSent[1].RequestId,
            ResourceId = "", HasBalance = false,
        });

        Assert.AreEqual(2, _answers.Count);
        Assert.IsNull(_answers[1].Balance);
        Assert.IsNull(_answers[1].ResourceId);
        Assert.IsNull(_answers[1].ArmouryLevel);
    }

    [TestMethod]
    public void AnUpgradeRequest_MapsEveryFieldIn_AndEveryReplyFieldOut()
    {
        var (transport, shops, _) = ReadyHost();
        var offer = new UpgradeOffer("mail", "fine", "plate", ArmourClass.Heavy, 100, System.Array.Empty<UpgradeMaterial>(), 0f, 1,
            UpgradeBlock.NotEnoughGold);
        shops.ArmouryOffers(Arg.Any<GuestTradeHero>(), 2).Returns(new[] { offer });
        var peer = FakeCoopTransport.Peer();

        transport.RaiseTradeRequest(peer, "guest_hero", new CoopCompatTradeRequest
        {
            ProtocolVersion = CoopCompatProtocol.ProtocolVersion, RequestId = "r9", Action = (int)GuestTradeAction.ArmouryUpgrade,
            SettlementId = "town_A1", Subject = "mail", Modifier = "fine", Target = "plate",
        });

        var (to, reply) = transport.TradeRepliesSent.Single();
        Assert.AreSame(peer, to);
        Assert.AreEqual("r9", reply.RequestId);
        Assert.AreEqual((int)GuestTradeAction.ArmouryUpgrade, reply.Action);
        Assert.AreEqual((int)GuestTradeRefusal.None, reply.Refusal);
        Assert.AreEqual((int)UpgradeBlock.NotEnoughGold, reply.Status, "the offer matched on source, modifier and target");
        Assert.AreEqual("silver", reply.ResourceId);
        Assert.IsTrue(reply.HasBalance);
        Assert.AreEqual(12f, reply.Balance);
        Assert.IsTrue(reply.HasArmouryLevel);
        Assert.AreEqual(2, reply.ArmouryLevel);
    }

    [TestMethod]
    public void ABuyRequest_MapsSubjectAndQuantityIn_AndTheResultOut()
    {
        var (transport, _, emissary) = ReadyHost();
        emissary.Purchase("guest_hero", "rohan", "sturgia", "elite", 7)
            .Returns(EmissaryPurchaseResult.Of(EmissaryPurchaseStatus.Success, "elite", 7, 70, "Horses"));

        transport.RaiseTradeRequest(FakeCoopTransport.Peer(), "guest_hero", new CoopCompatTradeRequest
        {
            ProtocolVersion = CoopCompatProtocol.ProtocolVersion, RequestId = "r8", Action = (int)GuestTradeAction.EmissaryBuy,
            SettlementId = "town_A1", Subject = "elite", Quantity = 7,
        });

        var reply = transport.TradeRepliesSent.Single().Item2;
        Assert.AreEqual((int)GuestTradeRefusal.None, reply.Refusal);
        Assert.AreEqual((int)EmissaryPurchaseStatus.Success, reply.Status);
        Assert.AreEqual(7, reply.Quantity);
        Assert.AreEqual(70, reply.TotalCost);
        Assert.AreEqual("Horses", reply.ResourceName);
        Assert.IsFalse(reply.HasArmouryLevel);
    }

    private static (FakeCoopTransport, IGuestTradeShops, IEliteEmissaryService) ReadyHost()
    {
        var transport = new FakeCoopTransport();
        var player = Substitute.For<IArmouryPlayerAdapter>();
        player.KingdomId.Returns("gondor");
        player.CultureId.Returns("vlandia");
        var hero = new GuestTradeHero("guest_hero", true, "town_A1", false, false, new SettlementOwnerInfo("town_A1", "rohan", "sturgia"),
            player, Substitute.For<IPlayerPartyAdapter>());
        var heroes = Substitute.For<IGuestTradeHeroAdapter>();
        heroes.Find("guest_hero").Returns(hero);
        var shops = Substitute.For<IGuestTradeShops>();
        shops.HasArmoury("town_A1").Returns(true);
        shops.ArmouryLevel("town_A1").Returns(2);
        var emissary = Substitute.For<IEliteEmissaryService>();
        emissary.IsEnabled.Returns(true);
        emissary.IsKeySettlement("town_A1").Returns(true);
        emissary.HasPurchasableOffers("rohan", "sturgia").Returns(true);
        shops.Emissary(Arg.Any<GuestTradeHero>()).Returns(emissary);
        var resources = Substitute.For<ISpecialResourceService>();
        resources.ResolveResource("gondor", "vlandia").Returns(new SpecialResource("silver", new[] { "k" }, new[] { "c" }, "Silver", "icon",
            100f, 0f, 0f, 0f, 0f, 0f, 0f));
        resources.GetCurrentAmount("guest_hero", "gondor", "vlandia").Returns(12f);
        var logger = Substitute.For<IModLogger>();
        var coop = Substitute.For<ICoopSessionProvider>();
        var client = new GuestTradeClient(coop, Substitute.For<IPlayerPartyAdapter>(), Substitute.For<ISpecialResourceStorageService>(), logger);
        _ = new GuestTradeRelayService(transport, client, new GuestTradeHost(heroes, shops, resources, logger), logger);
        return (transport, shops, emissary);
    }

    [TestMethod]
    public void WithNoNetwork_TheClientReportsFailed()
    {
        _transport.SendsSucceed = false;

        Assert.AreEqual(GuestTradeSend.Failed, _client.Request(new GuestTradeRequest(GuestTradeAction.EmissaryBalance, "town_A1"), _answers.Add));
    }
}
