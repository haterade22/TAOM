using System;
using TAOM.CoopCompat.Messages;

namespace TAOM.CoopCompat.Transport;

/// <summary>What this process is, read when asked and never cached: the session container is replaced on every host or join.</summary>
public enum CoopRole
{
    NoSession,
    Server,
    Client,
}

/// <summary>A remote peer as services see it: opaque, so no Coop or LiteNetLib type leaves the transport.</summary>
public sealed class CoopPeer
{
    internal CoopPeer(object handle, string label = "?")
    {
        Handle = handle;
        Label = label;
    }

    internal object Handle { get; }

    /// <summary>A short name for log lines, such as the transport's peer id.</summary>
    public string Label { get; }
}

/// <summary>
/// The seam between the relay services and Coop. No Coop or sealed TaleWorlds type appears here (ADR-007).
/// Events are raised on the game thread, and only when the role fits the message: a hello on the
/// server, a reply or snapshot on a client.
/// </summary>
public interface ICoopTransport
{
    CoopRole CurrentRole { get; }

    /// <summary>Subscribes to Coop's message broker. Safe to call twice.</summary>
    void Start();

    /// <summary>This client finished its campaign join (Coop's ClientCampaignReady).</summary>
    event Action ClientReady;

    event Action<CoopPeer, CoopCompatHello> HelloReceived;

    event Action<CoopCompatHelloReply> HelloReplyReceived;

    event Action<CoopCompatMomentumSnapshot> SnapshotReceived;

    /// <summary>
    /// A guest's trade request, on the server only, with the StringId of the hero that the sender's current
    /// connection controls, or "" when that connection controls none (the host then refuses it). A sender that is no
    /// peer cannot be answered and is dropped before this event.
    /// </summary>
    event Action<CoopPeer, string, CoopCompatTradeRequest> TradeRequestReceived;

    event Action<CoopCompatTradeReply> TradeReplyReceived;

    /// <summary>
    /// A player's career report, on the server only, with the StringId of the hero that the sender's current connection
    /// controls, or "" when it controls none, as for <see cref="TradeRequestReceived"/>.
    /// </summary>
    event Action<CoopPeer, string, CoopCompatCareerReport> CareerReportReceived;

    event Action<CoopCompatCareerUpdate> CareerUpdateReceived;

    /// <summary>Client to server. False when this process is not a client in a session or has no network.</summary>
    bool SendToServer(CoopCompatHello message);

    bool SendToServer(CoopCompatTradeRequest message);

    bool SendToServer(CoopCompatCareerReport message);

    /// <summary>Server to one peer. False when this process is not a server in a session or has no network.</summary>
    bool SendToPeer(CoopPeer peer, CoopCompatHelloReply message);

    bool SendToPeer(CoopPeer peer, CoopCompatMomentumSnapshot message);

    bool SendToPeer(CoopPeer peer, CoopCompatTradeReply message);

    bool SendToPeer(CoopPeer peer, CoopCompatCareerUpdate message);

    /// <summary>Server to every client. False when this process is not a server in a session or has no network.</summary>
    bool Broadcast(CoopCompatMomentumSnapshot message);

    bool Broadcast(CoopCompatCareerUpdate message);

    /// <summary>Server to every client but <paramref name="peer"/>. False when this process is not a server in a session or has no network.</summary>
    bool BroadcastExcept(CoopPeer peer, CoopCompatCareerUpdate message);
}
