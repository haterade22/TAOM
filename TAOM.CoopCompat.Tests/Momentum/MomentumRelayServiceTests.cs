using System;
using System.Collections.Generic;
using System.Linq;
using Common.Messaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.CoopCompat.Messages;
using TAOM.CoopCompat.Momentum;
using TAOM.CoopCompat.Transport;
using TAOM.Core.Logging;
using TAOM.Features.WarOfTheRingMomentum;

namespace TAOM.CoopCompat.Tests.Momentum;

[TestClass]
public class MomentumRelayServiceTests
{
    private FakeCoopTransport _transport = null!;
    private IMomentumStateStore _store = null!;
    private IModLogger _logger = null!;
    private IOutcomeLog _outcomes = null!;
    private MomentumRelayService _service = null!;
    private double _seconds;
    private double _hours;
    private Dictionary<string, string> _war = null!;

    [TestInitialize]
    public void Setup()
    {
        _transport = new FakeCoopTransport { CurrentRole = CoopRole.Server };
        _store = Substitute.For<IMomentumStateStore>();
        _logger = Substitute.For<IModLogger>();
        _outcomes = Substitute.For<IOutcomeLog>();
        _seconds = 1000;
        _hours = 100;
        _war = War(momentum: "5");
        _store.CaptureWarState().Returns(_ => new Dictionary<string, string>(_war));
        _store.ApplyWarState(Arg.Any<Dictionary<string, string>>()).Returns(true);
        _service = new MomentumRelayService(_transport, _store, _logger, _outcomes, () => _seconds, () => _hours);
    }

    private static Dictionary<string, string> War(string momentum, string endHours = "50.5") => new()
    {
        ["version"] = "1",
        ["free.momentum"] = momentum,
        ["free.ev.BattleWon.0"] = "10|" + endHours + "|a battle | with a pipe",
    };

    private void MarkChanged() => _store.MomentumChanged += Raise.Event<Action>();

    private void Tick(double secondsLater = 0, double hoursLater = 1)
    {
        _seconds += secondsLater;
        _hours += hoursLater;
        _service.OnHourlyTick();
    }

    private CoopCompatMomentumSnapshot SnapshotFor(long sequence, int protocol = CoopCompatProtocol.ProtocolVersion) => new()
    {
        ProtocolVersion = protocol,
        Sequence = sequence,
        Keys = new[] { "version", "free.momentum" },
        Values = new[] { "1", "9" },
    };

    private void ClientJoins()
    {
        _transport.CurrentRole = CoopRole.Client;
        _transport.RaiseClientReady();
    }

    // server

    [TestMethod]
    public void Hello_OnTheServer_AnswersThatPeerWithASnapshot()
    {
        _transport.RaiseHello(FakeCoopTransport.Peer("7"), new CoopCompatHello());

        var sent = _transport.SnapshotsToPeer.Single();
        Assert.AreEqual("7", sent.Peer.Label);
        Assert.AreEqual(CoopCompatProtocol.ProtocolVersion, sent.Snapshot.ProtocolVersion);
        Assert.AreEqual(1L, sent.Snapshot.Sequence);
        CollectionAssert.AreEquivalent(_war.Keys.ToList(), sent.Snapshot.Keys);
        Assert.AreEqual("10|50.5|a battle | with a pipe", sent.Snapshot.Values[Array.IndexOf(sent.Snapshot.Keys, "free.ev.BattleWon.0")]);
        Assert.AreEqual(0, _transport.Broadcasts.Count);
        _outcomes.Received().Pass("taom.coopcompat.momentum.server", "7", "sequence=1 entries=" + _war.Count);
    }

    [TestMethod]
    public void Hello_OnAClientOrWithoutASession_SendsNothing()
    {
        _transport.CurrentRole = CoopRole.Client;
        _transport.RaiseHello(FakeCoopTransport.Peer(), new CoopCompatHello());
        _transport.CurrentRole = CoopRole.NoSession;
        _transport.RaiseHello(FakeCoopTransport.Peer(), new CoopCompatHello());

        Assert.AreEqual(0, _transport.SnapshotsToPeer.Count);
    }

    [TestMethod]
    public void Sequence_RisesOnEverySnapshot_ForPeersAndBroadcastsAlike()
    {
        _transport.RaiseHello(FakeCoopTransport.Peer(), new CoopCompatHello());
        MarkChanged();
        Tick();
        _transport.RaiseHello(FakeCoopTransport.Peer(), new CoopCompatHello());

        Assert.AreEqual(1L, _transport.SnapshotsToPeer[0].Snapshot.Sequence);
        Assert.AreEqual(2L, _transport.Broadcasts.Single().Sequence);
        Assert.AreEqual(3L, _transport.SnapshotsToPeer[1].Snapshot.Sequence);
    }

    [TestMethod]
    public void Tick_WhenNothingChanged_SendsNothing()
    {
        Tick();
        Tick(secondsLater: 600);

        Assert.AreEqual(0, _transport.Broadcasts.Count);
    }

    [TestMethod]
    public void Tick_AfterAChange_Broadcasts_ThenStaysQuiet()
    {
        _war = War("6");
        MarkChanged();
        Tick();
        Tick(secondsLater: 600);

        Assert.AreEqual(1, _transport.Broadcasts.Count);
        // A broadcast can go out with no client connected, so only the answer to a hello writes the check line.
        _outcomes.DidNotReceive().Pass("taom.coopcompat.momentum.server", Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void Tick_InsideTheSixtySecondFloor_KeepsTheChangeForALaterTick_WithoutSerializing()
    {
        MarkChanged();
        Tick();
        _store.ClearReceivedCalls();
        _war = War("7");
        MarkChanged();

        Tick(secondsLater: 59);
        Assert.AreEqual(1, _transport.Broadcasts.Count);
        _store.DidNotReceive().CaptureWarState();

        Tick(secondsLater: 1);
        Assert.AreEqual(2, _transport.Broadcasts.Count);
        Assert.AreEqual("7", _transport.Broadcasts[1].Values[Array.IndexOf(_transport.Broadcasts[1].Keys, "free.momentum")]);
    }

    [TestMethod]
    public void Tick_WhenAnEventExpiryTimeChanged_Sends()
    {
        MarkChanged();
        Tick();
        _war = War("5", endHours: "99.25");
        MarkChanged();

        Tick(secondsLater: 120);

        Assert.AreEqual(2, _transport.Broadcasts.Count);
    }

    [TestMethod]
    public void Tick_WhenAnEventValueChangedBesidesItsExpiry_Sends()
    {
        MarkChanged();
        Tick();
        _war = War("5", endHours: "99.25");
        _war["free.ev.BattleWon.0"] = "11|99.25|a battle | with a pipe";
        MarkChanged();

        Tick(secondsLater: 120);

        Assert.AreEqual(2, _transport.Broadcasts.Count);
    }

    [TestMethod]
    public void Tick_SendsAKeepAliveEvery24GameHours_EvenWhenNothingChanged()
    {
        Tick(secondsLater: 100, hoursLater: 0);
        Tick(secondsLater: 100, hoursLater: 23);
        Assert.AreEqual(0, _transport.Broadcasts.Count);

        Tick(secondsLater: 100, hoursLater: 1);
        Assert.AreEqual(1, _transport.Broadcasts.Count);

        Tick(secondsLater: 100, hoursLater: 23);
        Assert.AreEqual(1, _transport.Broadcasts.Count);

        Tick(secondsLater: 100, hoursLater: 1);
        Assert.AreEqual(2, _transport.Broadcasts.Count);
    }

    [TestMethod]
    public void Tick_OnAClientOrWithoutASession_SendsNothing()
    {
        MarkChanged();
        _transport.CurrentRole = CoopRole.Client;
        Tick(secondsLater: 600);
        _transport.CurrentRole = CoopRole.NoSession;
        Tick(secondsLater: 600);

        Assert.AreEqual(0, _transport.Broadcasts.Count);
    }

    [TestMethod]
    public void Tick_WhenTheBroadcastFails_KeepsTheChangePending()
    {
        MarkChanged();
        _transport.SendsSucceed = false;
        Tick(secondsLater: 100);

        _transport.SendsSucceed = true;
        Tick(secondsLater: 100);

        Assert.AreEqual(1, _transport.Broadcasts.Count);
    }

    // client

    [TestMethod]
    public void Snapshot_OnAClientAfterReady_IsApplied()
    {
        ClientJoins();

        _transport.RaiseSnapshot(SnapshotFor(1));

        _store.Received(1).ApplyWarState(Arg.Is<Dictionary<string, string>>(d => d["free.momentum"] == "9" && d["version"] == "1" && d.Count == 2));
        _outcomes.Received().Pass("taom.coopcompat.momentum.client", "", Arg.Any<string>());
    }

    [TestMethod]
    public void Snapshot_WithAnotherProtocol_IsDropped()
    {
        ClientJoins();

        _transport.RaiseSnapshot(SnapshotFor(1, protocol: 99));

        _store.DidNotReceive().ApplyWarState(Arg.Any<Dictionary<string, string>>());
    }

    [TestMethod]
    public void Snapshot_OnTheServer_IsDropped()
    {
        _transport.CurrentRole = CoopRole.Server;

        _transport.RaiseSnapshot(SnapshotFor(1));

        _store.DidNotReceive().ApplyWarState(Arg.Any<Dictionary<string, string>>());
    }

    [TestMethod]
    public void Snapshot_BeforeClientReady_IsDropped()
    {
        _transport.CurrentRole = CoopRole.Client;

        _transport.RaiseSnapshot(SnapshotFor(1));

        _store.DidNotReceive().ApplyWarState(Arg.Any<Dictionary<string, string>>());
    }

    [TestMethod]
    public void Snapshot_WithASequenceNotNewer_IsDropped()
    {
        ClientJoins();
        _transport.RaiseSnapshot(SnapshotFor(5));

        _transport.RaiseSnapshot(SnapshotFor(5));
        _transport.RaiseSnapshot(SnapshotFor(4));
        _transport.RaiseSnapshot(SnapshotFor(6));

        _store.Received(2).ApplyWarState(Arg.Any<Dictionary<string, string>>());
    }

    [TestMethod]
    public void ClientReady_AgainAfterARejoin_AcceptsAnEarlierSequenceFromANewServer()
    {
        ClientJoins();
        _transport.RaiseSnapshot(SnapshotFor(50));

        ClientJoins();
        _transport.RaiseSnapshot(SnapshotFor(1));

        _store.Received(2).ApplyWarState(Arg.Any<Dictionary<string, string>>());
    }

    [TestMethod]
    public void Snapshot_TheStoreRejects_IsLoggedAndDoesNotAdvanceTheSequence()
    {
        ClientJoins();
        _store.ApplyWarState(Arg.Any<Dictionary<string, string>>()).Returns(false);

        _transport.RaiseSnapshot(SnapshotFor(3));
        _store.ApplyWarState(Arg.Any<Dictionary<string, string>>()).Returns(true);
        _transport.RaiseSnapshot(SnapshotFor(3));

        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("[CoopCompat]") && m.Contains("rejected") && m.Contains("sequence=3")));
        _outcomes.Received().Fail("taom.coopcompat.momentum.client", "", Arg.Any<string>());
        _store.Received(2).ApplyWarState(Arg.Any<Dictionary<string, string>>());
    }

    [TestMethod]
    public void Snapshot_WithMismatchedKeyAndValueCounts_IsRejectedWithoutTouchingTheStore()
    {
        ClientJoins();
        var bad = SnapshotFor(1);
        bad.Values = new[] { "1" };

        _transport.RaiseSnapshot(bad);

        _store.DidNotReceive().ApplyWarState(Arg.Any<Dictionary<string, string>>());
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("rejected")));
    }
}
