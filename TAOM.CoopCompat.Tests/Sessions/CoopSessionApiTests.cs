using System.Collections.Generic;
using Common.Messaging;
using GameInterface.Services.Sessions.Messages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.CoopCompat.Messages;
using TAOM.CoopCompat.Sessions;
using TAOM.Features.LongDefeat.Coop;

namespace TAOM.CoopCompat.Tests.Sessions;

/// <summary>
/// Checks against Coop's session API itself; the project builds this file only when CoopBinDir has that API. The transport runs
/// against Coop's real broker, with the game-thread marshal replaced by a counted direct call.
/// </summary>
[TestClass]
public class CoopSessionApiTests
{
    private CoopSessionTransport _transport = null!;
    private int _marshalled;
    private readonly List<string> _raised = new();

    [TestInitialize]
    public void Setup()
    {
        _marshalled = 0;
        _transport = new CoopSessionTransport(a => { _marshalled++; a(); });
        _transport.MissionOpenRequested += (id, mod, payload) => _raised.Add($"open:{id}:{mod}:{payload.Length}");
        _transport.HostChanged += (id, epoch, local) => _raised.Add($"host:{id}:{epoch}:{local}");
        _transport.MissionEndRequested += id => _raised.Add($"end:{id}");
        _transport.RunEndedReceived += m => _raised.Add($"ended:{m.SessionId}:{m.Kills}");
        _transport.Start();
    }

    [TestMethod]
    public void TaomsPayloadLimit_IsCoops()
    {
        Assert.AreEqual(AttemptHostSession.MaxPayloadBytes, LongDefeatCoopContract.MaxPayloadBytes);
    }

    // Coop raises its session events on its own threads; each must reach TAOM through the game-thread marshal.
    [TestMethod]
    public void EverySessionEvent_GoesThroughTheGameThreadMarshal()
    {
        var broker = MessageBroker.Instance;

        broker.Publish(this, new SessionMissionOpenRequested("cs-test-1", LongDefeatCoopContract.ModId, new byte[] { 1, 2, 3 }));
        broker.Publish(this, new SessionHostChanged("cs-test-1", "controller-1", 2, true));
        broker.Publish(this, new SessionMissionEndRequested("cs-test-1"));
        broker.Publish(this, new CoopCompatLdRunEnded { ProtocolVersion = CoopCompatProtocol.ProtocolVersion, SessionId = "cs-test-1", Kills = 9 });

        CollectionAssert.AreEqual(new[]
        {
            "open:cs-test-1:" + LongDefeatCoopContract.ModId + ":3", "host:cs-test-1:2:True", "end:cs-test-1", "ended:cs-test-1:9",
        }, _raised);
        Assert.AreEqual(4, _marshalled);
    }

    [TestMethod]
    public void Start_Twice_SubscribesOnce()
    {
        _transport.Start();

        MessageBroker.Instance.Publish(this, new SessionMissionEndRequested("cs-test-2"));

        CollectionAssert.AreEqual(new[] { "end:cs-test-2" }, _raised);
    }
}
