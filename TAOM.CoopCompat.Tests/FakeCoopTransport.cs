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

    public event Action? ClientReady;
    public event Action<CoopPeer, CoopCompatHello>? HelloReceived;
    public event Action<CoopCompatHelloReply>? HelloReplyReceived;
    public event Action<CoopCompatMomentumSnapshot>? SnapshotReceived;

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

    public bool Broadcast(CoopCompatMomentumSnapshot message)
    {
        if (!SendsSucceed) return false;
        Broadcasts.Add(message);
        return true;
    }

    public void RaiseClientReady() => ClientReady?.Invoke();
    public void RaiseHello(CoopPeer peer, CoopCompatHello hello) => HelloReceived?.Invoke(peer, hello);
    public void RaiseReply(CoopCompatHelloReply reply) => HelloReplyReceived?.Invoke(reply);
    public void RaiseSnapshot(CoopCompatMomentumSnapshot snapshot) => SnapshotReceived?.Invoke(snapshot);

    public static CoopPeer Peer(string label = "7") => new CoopPeer(new object(), label);
}
