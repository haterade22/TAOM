using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.MountAndBlade;
using TAOM.CoopCompat.Messages;
using TAOM.CoopCompat.Sessions;
using TAOM.Features.LongDefeat.Coop;

namespace TAOM.CoopCompat.Tests.Sessions;

/// <summary>The link between TAOM's Long Defeat port and Coop's session API (#1280): it forwards only what is TAOM's.</summary>
[TestClass]
public class LongDefeatSessionLinkTests
{
    private readonly FakeSessionTransport _transport = new();
    private readonly RecordingPort _port = new();

    private LongDefeatSessionLink NewLink() => new(_transport, _port);

    [TestMethod]
    public void AnOpenForTaom_ReachesThePort()
    {
        NewLink();

        _transport.RaiseOpen("s1", LongDefeatCoopContract.ModId, new byte[] { 1, 2 });

        Assert.AreEqual("open:s1:2", _port.Calls.Single());
    }

    [TestMethod]
    public void AnOpenForAnotherMod_IsLeftAlone()
    {
        NewLink();

        _transport.RaiseOpen("s1", "SomeOtherMod", new byte[] { 1 });

        Assert.AreEqual(0, _port.Calls.Count);
    }

    [TestMethod]
    public void HostChangedAndEnd_ReachThePort()
    {
        NewLink();

        _transport.RaiseHostChanged("s1", 2, true);
        _transport.RaiseEnd("s1");

        CollectionAssert.AreEqual(new[] { "host:s1:2:True", "end:s1" }, _port.Calls);
    }

    [TestMethod]
    public void Host_UsesTaomsModId()
    {
        var link = NewLink();

        link.Host(new byte[] { 9 });

        Assert.AreEqual(LongDefeatCoopContract.ModId, _transport.HostedModId);
    }

    [TestMethod]
    public void SendRunEnded_StampsTheProtocolVersion()
    {
        var link = NewLink();

        Assert.IsTrue(link.SendRunEnded("s1", 3, 2, 7, 6, 412));

        var sent = _transport.Sent.Single();
        Assert.AreEqual(CoopCompatProtocol.ProtocolVersion, sent.ProtocolVersion);
        Assert.AreEqual("s1|3|2|7|6|412", $"{sent.SessionId}|{sent.Epoch}|{sent.Reason}|{sent.Wave}|{sent.WavesCleared}|{sent.Kills}");
    }

    [TestMethod]
    public void ARunEnded_OfThisProtocol_ReachesThePort()
    {
        NewLink();

        _transport.RaiseRunEnded(new CoopCompatLdRunEnded { ProtocolVersion = CoopCompatProtocol.ProtocolVersion, SessionId = "s1", Epoch = 3, Reason = 2, Wave = 7, WavesCleared = 6, Kills = 412 });

        Assert.AreEqual("ended:s1:3:2:7:6:412", _port.Calls.Single());
    }

    [TestMethod]
    public void ARunEnded_OfAnotherProtocol_IsDropped()
    {
        NewLink();

        _transport.RaiseRunEnded(new CoopCompatLdRunEnded { ProtocolVersion = CoopCompatProtocol.ProtocolVersion + 1, SessionId = "s1" });

        Assert.AreEqual(0, _port.Calls.Count);
    }

    [TestMethod]
    public void SessionTransportInterface_NamesNoCoopType_InAnySignature()
    {
        var coop = new[] { "Common", "GameInterface", "Coop.Core", "LiteNetLib", "Autofac", "protobuf-net", "protobuf-net.Core" };
        var type = typeof(ICoopSessionTransport);
        var signatureTypes = type.GetMethods().SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType))
            .Concat(type.GetEvents().Select(e => e.EventHandlerType!))
            .SelectMany(t => t.IsGenericType ? t.GetGenericArguments().Append(t) : new[] { t });

        Assert.IsFalse(signatureTypes.Any(t => coop.Contains(t.Assembly.GetName().Name)));
    }

    [TestMethod]
    public void SubModule_HoldsTheSessionTransportAndLink_InFields()
    {
        // Coop's broker holds its subscribers weakly: a local would be collected and the session events lost.
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;

        Assert.AreEqual(typeof(ICoopSessionTransport), typeof(CoopCompatSubModule).GetField("_sessions", flags)!.FieldType);
        Assert.AreEqual(typeof(LongDefeatSessionLink), typeof(CoopCompatSubModule).GetField("_sessionLink", flags)!.FieldType);
    }

    private sealed class FakeSessionTransport : ICoopSessionTransport
    {
        public string? HostedModId { get; private set; }
        public List<CoopCompatLdRunEnded> Sent { get; } = new();

        public event Action<string, string, byte[]>? MissionOpenRequested;
        public event Action<string, int, bool>? HostChanged;
        public event Action<string>? MissionEndRequested;
        public event Action<CoopCompatLdRunEnded>? RunEndedReceived;

        public void Host(string modId, byte[] payload) => HostedModId = modId;

        public void Join(string address, int port)
        {
        }

        public IReadOnlyList<MissionBehavior> CreateMissionBehaviors(string sessionId) => Array.Empty<MissionBehavior>();

        public bool Close() => true;

        public bool SendToSession(CoopCompatLdRunEnded message)
        {
            Sent.Add(message);
            return true;
        }

        public void RaiseOpen(string sessionId, string modId, byte[] payload) => MissionOpenRequested?.Invoke(sessionId, modId, payload);
        public void RaiseHostChanged(string sessionId, int epoch, bool isLocalHost) => HostChanged?.Invoke(sessionId, epoch, isLocalHost);
        public void RaiseEnd(string sessionId) => MissionEndRequested?.Invoke(sessionId);
        public void RaiseRunEnded(CoopCompatLdRunEnded message) => RunEndedReceived?.Invoke(message);
    }

    private sealed class RecordingPort : ILongDefeatCoopPort
    {
        public List<string> Calls { get; } = new();

        public void Attach(ILongDefeatCoopLink link) => Calls.Add("attach");
        public void OnMissionOpenRequested(string sessionId, byte[] payload) => Calls.Add($"open:{sessionId}:{payload.Length}");
        public void OnHostChanged(string sessionId, int epoch, bool isLocalHost) => Calls.Add($"host:{sessionId}:{epoch}:{isLocalHost}");
        public void OnMissionEndRequested(string sessionId) => Calls.Add($"end:{sessionId}");

        public void OnRunEnded(string sessionId, int epoch, int reason, int wave, int wavesCleared, int kills) =>
            Calls.Add($"ended:{sessionId}:{epoch}:{reason}:{wave}:{wavesCleared}:{kills}");
    }
}
