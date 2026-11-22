using System;
using System.Collections.Generic;
using TAOM.CoopCompat.Messages;
using TAOM.CoopCompat.Transport;

namespace TAOM.CoopCompat.Tests;

/// <summary>A transport with no Coop behind it: tests set the role, raise the events and read what was sent.</summary>
internal sealed class FakeCoopTransport : ICoopTransport
{
    public CoopRole CurrentRole { get; set; } = CoopRole.NoSession;

    /// <summary>When false every send reports failure, as a missing network does.</summary>
    public bool SendsSucceed { get; set; } = true;

    public List<CoopCompatHello> HellosSent { get; } = new();
    public List<(CoopPeer Peer, CoopCompatHelloReply Reply)> RepliesSent { get; } = new();
    public List<(CoopPeer Peer, CoopCompatMomentumSnapshot Snapshot)> SnapshotsToPeer { get; } = new();
    public List<CoopCompatMomentumSnapshot> Broadcasts { get; } = new();
    public List<CoopCompatTradeRequest> TradeRequestsSent { get; } = new();
    public List<(CoopPeer Peer, CoopCompatTradeReply Reply)> TradeRepliesSent { get; } = new();
    public List<CoopCompatCareerReport> CareerReportsSent { get; } = new();

    /// <summary>When true the career sends throw, as Coop's network can.</summary>
    public bool CareerSendsThrow { get; set; }
    public List<(CoopPeer Peer, CoopCompatCareerUpdate Update)> CareerUpdatesToPeer { get; } = new();
    public List<CoopCompatCareerUpdate> CareerBroadcasts { get; } = new();
    public List<(CoopPeer Except, CoopCompatCareerUpdate Update)> CareerBroadcastsExcept { get; } = new();

    public event Action? ClientReady;
    public event Action<CoopPeer, CoopCompatHello>? HelloReceived;
    public event Action<CoopCompatHelloReply>? HelloReplyReceived;
    public event Action<CoopCompatMomentumSnapshot>? SnapshotReceived;
    public event Action<CoopPeer, string, CoopCompatTradeRequest>? TradeRequestReceived;
    public event Action<CoopCompatTradeReply>? TradeReplyReceived;
    public event Action<CoopPeer, string, CoopCompatCareerReport>? CareerReportReceived;
    public event Action<CoopCompatCareerUpdate>? CareerUpdateReceived;

    public void Start()
    {
    }

    public bool SendToServer(CoopCompatHello message)
    {
        if (!SendsSucceed) return false;
        HellosSent.Add(message);
        return true;
    }

    public bool SendToPeer(CoopPeer peer, CoopCompatHelloReply message)
    {
        if (!SendsSucceed) return false;
        RepliesSent.Add((peer, message));
        return true;
    }

    public bool SendToPeer(CoopPeer peer, CoopCompatMomentumSnapshot message)
    {
        if (!SendsSucceed) return false;
        SnapshotsToPeer.Add((peer, message));
        return true;
    }

    public bool SendToServer(CoopCompatTradeRequest message)
    {
        if (!SendsSucceed) return false;
        TradeRequestsSent.Add(message);
        return true;
    }

    public bool SendToPeer(CoopPeer peer, CoopCompatTradeReply message)
    {
        if (!SendsSucceed) return false;
        TradeRepliesSent.Add((peer, message));
        return true;
    }

    public bool Broadcast(CoopCompatMomentumSnapshot message)
    {
        if (!SendsSucceed) return false;
        Broadcasts.Add(message);
        return true;
    }

    public bool SendToServer(CoopCompatCareerReport message)
    {
        if (CareerSendsThrow) throw new InvalidOperationException("network down");
        if (!SendsSucceed) return false;
        CareerReportsSent.Add(message);
        return true;
    }

    public bool SendToPeer(CoopPeer peer, CoopCompatCareerUpdate message)
    {
        if (!SendsSucceed) return false;
        CareerUpdatesToPeer.Add((peer, message));
        return true;
    }

    public bool Broadcast(CoopCompatCareerUpdate message)
    {
        if (CareerSendsThrow) throw new InvalidOperationException("network down");
        if (!SendsSucceed) return false;
        CareerBroadcasts.Add(message);
        return true;
    }

    public bool BroadcastExcept(CoopPeer peer, CoopCompatCareerUpdate message)
    {
        if (!SendsSucceed) return false;
        CareerBroadcastsExcept.Add((peer, message));
        return true;
    }

    public void RaiseClientReady() => ClientReady?.Invoke();
    public void RaiseHello(CoopPeer peer, CoopCompatHello hello) => HelloReceived?.Invoke(peer, hello);
    public void RaiseReply(CoopCompatHelloReply reply) => HelloReplyReceived?.Invoke(reply);
    public void RaiseSnapshot(CoopCompatMomentumSnapshot snapshot) => SnapshotReceived?.Invoke(snapshot);
    public void RaiseTradeRequest(CoopPeer peer, string heroId, CoopCompatTradeRequest request) => TradeRequestReceived?.Invoke(peer, heroId, request);
    public void RaiseTradeReply(CoopCompatTradeReply reply) => TradeReplyReceived?.Invoke(reply);
    public void RaiseCareerReport(CoopPeer peer, string heroId, CoopCompatCareerReport report) => CareerReportReceived?.Invoke(peer, heroId, report);
    public void RaiseCareerUpdate(CoopCompatCareerUpdate update) => CareerUpdateReceived?.Invoke(update);

    public static CoopPeer Peer(string label = "7") => new CoopPeer(new object(), label);
}
