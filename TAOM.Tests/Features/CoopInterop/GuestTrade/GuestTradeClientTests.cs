using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.CoopInterop.GuestTrade;
using TAOM.Features.SpecialResources;

namespace TAOM.Tests.Features.CoopInterop.GuestTrade;

[TestClass]
public class GuestTradeClientTests
{
    private ICoopSessionProvider _coop = null!;
    private IPlayerPartyAdapter _player = null!;
    private ISpecialResourceStorageService _storage = null!;
    private FakeLink _link = null!;
    private double _now;
    private GuestTradeClient _client = null!;
    private readonly List<GuestTradeReply> _answers = new();

    [TestInitialize]
    public void Setup()
    {
        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.ShouldDeferToHost.Returns(true);
        _player = Substitute.For<IPlayerPartyAdapter>();
        _player.GetMainHeroId().Returns("guest_hero");
        _storage = Substitute.For<ISpecialResourceStorageService>();
        _link = new FakeLink();
        _now = 100;
        _answers.Clear();
        _client = new GuestTradeClient(_coop, _player, _storage, Substitute.For<IModLogger>(), () => _now);
        _client.Attach(_link);
    }

    private static GuestTradeRequest Buy() => new(GuestTradeAction.EmissaryBuy, "town_A1", "elite", Quantity: 5);

    private GuestTradeSend Send(GuestTradeRequest request) => _client.Request(request, _answers.Add);

    [TestMethod]
    public void IsGuest_NeedsALinkAndAGuestSeat()
    {
        Assert.IsTrue(_client.IsGuest);
        Assert.IsFalse(_client.IsGuestWithoutLink);

        _coop.ShouldDeferToHost.Returns(false);
        Assert.IsFalse(_client.IsGuest, "the host and a solo player trade locally");
        Assert.IsFalse(_client.IsGuestWithoutLink);

        _coop.ShouldDeferToHost.Returns(true);
        _client.Attach(null);
        Assert.IsFalse(_client.IsGuest, "with no link a guest keeps TAOM's refusal");
        Assert.IsTrue(_client.IsGuestWithoutLink);

        _coop.ShouldDeferToHost.Returns(false);
        Assert.IsFalse(_client.IsGuestWithoutLink, "a host with no relay still trades locally");
    }

    [TestMethod]
    public void Request_SendsTheIntentWithAFreshId()
    {
        Assert.AreEqual(GuestTradeSend.Sent, Send(Buy()));

        var sent = _link.Sent.Single();
        Assert.AreEqual(GuestTradeAction.EmissaryBuy, sent.Action);
        Assert.AreEqual("elite", sent.Subject);
        Assert.AreEqual(5, sent.Quantity);
        Assert.IsFalse(string.IsNullOrEmpty(sent.RequestId));
    }

    [TestMethod]
    public void Request_WhileOneWaits_IsBusyAndSendsNothing()
    {
        Send(Buy());

        Assert.AreEqual(GuestTradeSend.Busy, Send(Buy()));
        Assert.AreEqual(1, _link.Sent.Count);
    }

    [TestMethod]
    public void Request_WithNoLinkOrAFailedSend_IsFailedAndLeavesNothingWaiting()
    {
        _link.Succeeds = false;
        Assert.AreEqual(GuestTradeSend.Failed, Send(Buy()));

        _link.Succeeds = true;
        Assert.AreEqual(GuestTradeSend.Sent, Send(Buy()), "a failed send must not block the next request");

        _client.Attach(null);
        Assert.AreEqual(GuestTradeSend.Failed, Send(Buy()), "with no link nothing can be sent");
    }

    [TestMethod]
    public void Request_WhenTheLinkThrows_IsFailedAndLeavesNothingWaiting()
    {
        _link.Throws = true;
        Assert.AreEqual(GuestTradeSend.Failed, Send(Buy()));

        _link.Throws = false;
        Assert.AreEqual(GuestTradeSend.Sent, Send(Buy()));
    }

    [TestMethod]
    public void Receive_AReplyWithNoId_IsIgnored()
    {
        Send(Buy());

        _client.Receive(new GuestTradeReply("", GuestTradeAction.EmissaryBuy, GuestTradeRefusal.None));

        Assert.AreEqual(0, _answers.Count);
        Assert.AreEqual(GuestTradeSend.Busy, Send(Buy()), "the real request still waits");
    }

    [TestMethod]
    public void Receive_TheMatchingReply_RunsTheCallbackOnce()
    {
        Send(Buy());
        var reply = new GuestTradeReply(_link.Sent[0].RequestId, GuestTradeAction.EmissaryBuy, GuestTradeRefusal.None, Status: 0);

        _client.Receive(reply);
        _client.Receive(reply);

        Assert.AreEqual(1, _answers.Count);
        Assert.AreEqual(GuestTradeSend.Sent, Send(Buy()), "the answered request no longer blocks");
    }

    [TestMethod]
    public void Receive_AnUnknownId_RunsNothing()
    {
        Send(Buy());

        _client.Receive(new GuestTradeReply("someone_else", GuestTradeAction.EmissaryBuy, GuestTradeRefusal.None));

        Assert.AreEqual(0, _answers.Count);
    }

    [TestMethod]
    public void AnUnansweredRequest_PastTheTimeout_IsReportedOnce_ThenStopsBlocking_AndItsLateReplyStillRuns()
    {
        Send(Buy());
        var firstId = _link.Sent[0].RequestId;

        _now += GuestTradeClient.ReplyTimeoutSeconds - 1;
        Assert.AreEqual(GuestTradeSend.Busy, Send(Buy()));

        _now += 2;
        Assert.AreEqual(GuestTradeSend.NoAnswer, Send(Buy()), "the guest learns the host never answered");
        Assert.AreEqual(1, _link.Sent.Count, "the click that reports the silence sends nothing");
        Assert.AreEqual(GuestTradeSend.Sent, Send(Buy()));

        _client.Receive(new GuestTradeReply(firstId, GuestTradeAction.EmissaryBuy, GuestTradeRefusal.None));
        Assert.AreEqual(1, _answers.Count, "a late purchase still shows its result");
    }

    [TestMethod]
    public void Receive_ABalance_CopiesItToThisMachinesHero()
    {
        Send(Buy());

        _client.Receive(new GuestTradeReply(_link.Sent[0].RequestId, GuestTradeAction.EmissaryBuy, GuestTradeRefusal.None,
            ResourceId: "wine", Balance: 42f));

        _storage.Received(1).Set("guest_hero", "wine", 42f);
    }

    [TestMethod]
    public void Receive_ABadBalance_CopiesNothing()
    {
        foreach (var bad in new[] { float.NaN, float.PositiveInfinity, -1f })
        {
            Send(Buy());
            _client.Receive(new GuestTradeReply(_link.Sent.Last().RequestId, GuestTradeAction.EmissaryBuy, GuestTradeRefusal.None,
                ResourceId: "wine", Balance: bad));
        }
        Send(Buy());
        _client.Receive(new GuestTradeReply(_link.Sent.Last().RequestId, GuestTradeAction.EmissaryBuy, GuestTradeRefusal.None,
            ResourceId: null, Balance: 5f));

        _storage.DidNotReceiveWithAnyArgs().Set(default!, default!, default);
        Assert.AreEqual(4, _answers.Count, "the result still shows");
    }

    [TestMethod]
    public void Attach_AgainDropsWhatWaited()
    {
        Send(Buy());
        var id = _link.Sent[0].RequestId;

        _client.Attach(_link);
        _client.Receive(new GuestTradeReply(id, GuestTradeAction.EmissaryBuy, GuestTradeRefusal.None));

        Assert.AreEqual(0, _answers.Count);
    }

    private sealed class FakeLink : IGuestTradeLink
    {
        public bool Succeeds { get; set; } = true;
        public bool Throws { get; set; }
        public List<GuestTradeRequest> Sent { get; } = new();

        public bool SendToHost(GuestTradeRequest request)
        {
            if (Throws) throw new System.InvalidOperationException("link down");
            if (!Succeeds) return false;
            Sent.Add(request);
            return true;
        }
    }
}
