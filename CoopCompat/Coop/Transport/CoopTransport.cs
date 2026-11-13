using System;
using Autofac;
using Common;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Messages;
using GameInterface;
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
    private bool _started;

    public CoopTransport() : this(action => GameThread.RunSafe(action, context: "TAOM.CoopCompat"))
    {
    }

    /// <summary>Tests replace the game-thread marshal, which needs a running game loop.</summary>
    internal CoopTransport(Action<Action> runOnGameThread)
    {
        _runOnGameThread = runOnGameThread;
    }

    public event Action? ClientReady;

    public event Action<CoopPeer, CoopCompatHello>? HelloReceived;

    public event Action<CoopCompatHelloReply>? HelloReplyReceived;

    public event Action<CoopCompatMomentumSnapshot>? SnapshotReceived;

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
    }

    public void RunOnGameThread(Action action) => _runOnGameThread(action);

    public bool SendToServer(CoopCompatHello message) => Send(CoopRole.Client, n => n.SendAll(message));

    public bool SendToPeer(CoopPeer peer, CoopCompatHelloReply message) =>
        peer.Handle is NetPeer target && Send(CoopRole.Server, n => n.Send(target, message));

    public bool SendToPeer(CoopPeer peer, CoopCompatMomentumSnapshot message) =>
        peer.Handle is NetPeer target && Send(CoopRole.Server, n => n.Send(target, message));

    public bool Broadcast(CoopCompatMomentumSnapshot message) => Send(CoopRole.Server, n => n.SendAll(message));

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
}
