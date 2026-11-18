using System;
using Autofac;
using Common;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Messages;
using GameInterface;
using GameInterface.Services.Players;
using LiteNetLib;
using TAOM.CoopCompat.Messages;

namespace TAOM.CoopCompat.Transport;

/// <summary>
/// The only class that touches Coop: the message broker, the container provider, INetwork,
/// ModInformation, GameThread and NetPeer. Rules (F7 to F17 of the build plan):
/// subscriptions are instance methods on MessageBroker.Instance, held weakly by Coop, so this object
/// must stay alive in a field of the submodule; the role and INetwork are read per call because the
/// session container is replaced on every host or join; handlers copy the payload out and run the
/// work on the game thread, as Coop publishes on its poller thread.
/// </summary>
public sealed class CoopTransport : ICoopTransport
{
    private readonly Action<Action> _runOnGameThread;
    private readonly Func<NetPeer, string?> _heroOfPeer;
    private bool _started;

    public CoopTransport() : this(action => GameThread.RunSafe(action, context: "TAOM.CoopCompat"), HeroOfConnectedPeer)
    {
    }

    /// <summary>Tests replace the game-thread marshal, which needs a running game loop, and the player lookup.</summary>
    internal CoopTransport(Action<Action> runOnGameThread, Func<NetPeer, string?>? heroOfPeer = null)
    {
        _runOnGameThread = runOnGameThread;
        _heroOfPeer = heroOfPeer ?? HeroOfConnectedPeer;
    }

    public event Action? ClientReady;

    public event Action<CoopPeer, CoopCompatHello>? HelloReceived;

    public event Action<CoopCompatHelloReply>? HelloReplyReceived;

    public event Action<CoopCompatMomentumSnapshot>? SnapshotReceived;

    public event Action<CoopPeer, string, CoopCompatTradeRequest>? TradeRequestReceived;

    public event Action<CoopCompatTradeReply>? TradeReplyReceived;

    public event Action<CoopPeer, string, CoopCompatCareerReport>? CareerReportReceived;

    public event Action<CoopCompatCareerUpdate>? CareerUpdateReceived;

    public CoopRole CurrentRole
    {
        get
        {
            if (!ContainerProvider.TryGetContainer(out ILifetimeScope _)) return CoopRole.NoSession;

            return ModInformation.IsServer ? CoopRole.Server : CoopRole.Client;
        }
    }

    public void Start()
    {
        if (_started) return;
        _started = true;

        var broker = MessageBroker.Instance;
        broker.Subscribe<ClientCampaignReady>(OnClientCampaignReady);
        broker.Subscribe<CoopCompatHello>(OnHello);
        broker.Subscribe<CoopCompatHelloReply>(OnHelloReply);
        broker.Subscribe<CoopCompatMomentumSnapshot>(OnSnapshot);
        broker.Subscribe<CoopCompatTradeRequest>(OnTradeRequest);
        broker.Subscribe<CoopCompatTradeReply>(OnTradeReply);
        broker.Subscribe<CoopCompatCareerReport>(OnCareerReport);
        broker.Subscribe<CoopCompatCareerUpdate>(OnCareerUpdate);
    }

    public bool SendToServer(CoopCompatHello message) => Send(CoopRole.Client, n => n.SendAll(message));

    public bool SendToPeer(CoopPeer peer, CoopCompatHelloReply message) =>
        peer.Handle is NetPeer target && Send(CoopRole.Server, n => n.Send(target, message));

    public bool SendToPeer(CoopPeer peer, CoopCompatMomentumSnapshot message) =>
        peer.Handle is NetPeer target && Send(CoopRole.Server, n => n.Send(target, message));

    public bool SendToServer(CoopCompatTradeRequest message) => Send(CoopRole.Client, n => n.SendAll(message));

    public bool SendToPeer(CoopPeer peer, CoopCompatTradeReply message) =>
        peer.Handle is NetPeer target && Send(CoopRole.Server, n => n.Send(target, message));

    public bool Broadcast(CoopCompatMomentumSnapshot message) => Send(CoopRole.Server, n => n.SendAll(message));

    public bool SendToServer(CoopCompatCareerReport message) => Send(CoopRole.Client, n => n.SendAll(message));

    public bool SendToPeer(CoopPeer peer, CoopCompatCareerUpdate message) =>
        peer.Handle is NetPeer target && Send(CoopRole.Server, n => n.Send(target, message));

    public bool Broadcast(CoopCompatCareerUpdate message) => Send(CoopRole.Server, n => n.SendAll(message));

    public bool BroadcastExcept(CoopPeer peer, CoopCompatCareerUpdate message) =>
        peer.Handle is NetPeer excluded && Send(CoopRole.Server, n => n.SendAllBut(excluded, message));

    private bool Send(CoopRole required, Action<INetwork> send)
    {
        if (CurrentRole != required) return false;
        if (!ContainerProvider.TryResolve<INetwork>(out var network)) return false;

        send(network);
        return true;
    }

    private void OnClientCampaignReady(MessagePayload<ClientCampaignReady> payload)
    {
        if (CurrentRole != CoopRole.Client) return;

        _runOnGameThread(() => ClientReady?.Invoke());
    }

    private void OnHello(MessagePayload<CoopCompatHello> payload)
    {
        if (CurrentRole != CoopRole.Server || payload.Who is not NetPeer netPeer) return;

        var peer = new CoopPeer(netPeer, netPeer.Id.ToString());
        var hello = payload.What;
        _runOnGameThread(() => HelloReceived?.Invoke(peer, hello));
    }

    private void OnHelloReply(MessagePayload<CoopCompatHelloReply> payload)
    {
        if (CurrentRole != CoopRole.Client) return;

        var reply = payload.What;
        _runOnGameThread(() => HelloReplyReceived?.Invoke(reply));
    }

    private void OnSnapshot(MessagePayload<CoopCompatMomentumSnapshot> payload)
    {
        if (CurrentRole != CoopRole.Client) return;

        var snapshot = payload.What;
        _runOnGameThread(() => SnapshotReceived?.Invoke(snapshot));
    }

    private void OnTradeRequest(MessagePayload<CoopCompatTradeRequest> payload)
    {
        if (CurrentRole != CoopRole.Server || payload.Who is not NetPeer netPeer) return;

        var request = payload.What;
        _runOnGameThread(() =>
        {
            // The hero comes from the connection, read when the request is handled: a reconnect gives the player a
            // new peer, and the old one then stands for nobody (after Sternab's TAOM-Coop request check).
            var heroId = _heroOfPeer(netPeer) ?? "";
            TradeRequestReceived?.Invoke(new CoopPeer(netPeer, netPeer.Id.ToString()), heroId, request);
        });
    }

    private void OnTradeReply(MessagePayload<CoopCompatTradeReply> payload)
    {
        if (CurrentRole != CoopRole.Client) return;

        var reply = payload.What;
        _runOnGameThread(() => TradeReplyReceived?.Invoke(reply));
    }

    private void OnCareerReport(MessagePayload<CoopCompatCareerReport> payload)
    {
        if (CurrentRole != CoopRole.Server || payload.Who is not NetPeer netPeer) return;

        var report = payload.What;
        _runOnGameThread(() =>
        {
            // As for trade requests: the hero comes from the connection when the report is handled, never from the report.
            var heroId = _heroOfPeer(netPeer) ?? "";
            CareerReportReceived?.Invoke(new CoopPeer(netPeer, netPeer.Id.ToString()), heroId, report);
        });
    }

    private void OnCareerUpdate(MessagePayload<CoopCompatCareerUpdate> payload)
    {
        if (CurrentRole != CoopRole.Client) return;

        var update = payload.What;
        _runOnGameThread(() => CareerUpdateReceived?.Invoke(update));
    }

    private static string? HeroOfConnectedPeer(NetPeer peer)
    {
        if (peer.ConnectionState != ConnectionState.Connected) return null;
        return ContainerProvider.TryResolve<IPlayerManager>(out var players) ? HeroOfCurrentController(players, peer) : null;
    }

    /// <summary>
    /// The StringId of the hero that the peer's current connection controls, or null. Player.HeroId is Coop's object
    /// key ("Hero_" + StringId), which CampaignObjectManager.Find cannot resolve, so the hero comes from Coop's own
    /// sender lookup. The controller check comes first: after a reconnect the old peer stands for nobody.
    /// </summary>
    internal static string? HeroOfCurrentController(IPlayerManager players, NetPeer peer)
    {
        if (!players.TryGetCurrentController(peer, out _)) return null;
        if (!players.TryGetSenderHero(peer, out var hero) || hero == null) return null;

        return string.IsNullOrEmpty(hero.StringId) ? null : hero.StringId;
    }
}
