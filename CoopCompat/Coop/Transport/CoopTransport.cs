using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
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
/// A client's broker also publishes what a mission peer sends, directly or through Coop's relay, with nothing that
/// names the sender. So every server-to-client message carries the client's session token (<see cref="ISessionStamped"/>):
/// the client picks one per Coop session (one session container) and sends it in each hello, the server keeps it per
/// connection and stamps each send, and the client drops a message with another token. A broadcast is one send per
/// connection that said hello.
/// </summary>
public sealed class CoopTransport : ICoopTransport
{
    private const int MaxTokenLength = 64;

    private readonly Action<Action> _runOnGameThread;
    private readonly Func<NetPeer, string?> _heroOfPeer;
    private readonly Func<NetPeer, bool> _isLive;
    private readonly Action<string>? _warn;
    private readonly Dictionary<NetPeer, string> _peerTokens = new(ByReference.Instance);
    private readonly object _peerTokensLock = new();
    private volatile ClientSession? _session;
    private readonly object _dropLogLock = new();
    private WeakReference<object>? _dropLoggedIn;
    private bool _started;

    /// <summary>
    /// The production transport. A factory, not a constructor: the submodule's assembly has no LiteNetLib reference, and
    /// a constructor call would make the compiler weigh the test constructor's NetPeer parameters.
    /// </summary>
    /// <param name="warn">Told once per session that a server message was dropped for its token.</param>
    public static CoopTransport Create(Action<string> warn) =>
        new(action => GameThread.RunSafe(action, context: "TAOM.CoopCompat"), HeroOfConnectedPeer, null, warn);

    /// <summary>Tests replace the game-thread marshal, which needs a running game loop, the player lookup and the connection check.</summary>
    internal CoopTransport(Action<Action> runOnGameThread, Func<NetPeer, string?>? heroOfPeer = null, Func<NetPeer, bool>? isLive = null,
        Action<string>? warn = null)
    {
        _runOnGameThread = runOnGameThread;
        _heroOfPeer = heroOfPeer ?? HeroOfConnectedPeer;
        _isLive = isLive ?? (peer => peer.ConnectionState == ConnectionState.Connected);
        _warn = warn;
    }

    /// <summary>The connections whose hello the server keeps a token for.</summary>
    internal int KnownConnections
    {
        get
        {
            lock (_peerTokensLock)
                return _peerTokens.Count;
        }
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

    public bool SendToServer(CoopCompatHello message)
    {
        if (CurrentRole != CoopRole.Client || !ContainerProvider.TryGetContainer(out ILifetimeScope scope)) return false;

        var session = _session;
        if (session == null || !session.Belongs(scope))
            _session = session = new ClientSession(NewToken(), scope);
        message.SessionToken = session.Token;
        return Send(CoopRole.Client, n => n.SendAll(message));
    }

    public bool SendToPeer(CoopPeer peer, CoopCompatHelloReply message) => SendStamped(peer, message);

    public bool SendToPeer(CoopPeer peer, CoopCompatMomentumSnapshot message) => SendStamped(peer, message);

    public bool SendToServer(CoopCompatTradeRequest message) => Send(CoopRole.Client, n => n.SendAll(message));

    public bool SendToPeer(CoopPeer peer, CoopCompatTradeReply message) => SendStamped(peer, message);

    public bool Broadcast(CoopCompatMomentumSnapshot message) => BroadcastStamped(message, null);

    public bool SendToServer(CoopCompatCareerReport message) => Send(CoopRole.Client, n => n.SendAll(message));

    public bool SendToPeer(CoopPeer peer, CoopCompatCareerUpdate message) => SendStamped(peer, message);

    public bool Broadcast(CoopCompatCareerUpdate message) => BroadcastStamped(message, null);

    public bool BroadcastExcept(CoopPeer peer, CoopCompatCareerUpdate message) =>
        peer.Handle is NetPeer excluded && BroadcastStamped(message, excluded);

    private bool Send(CoopRole required, Action<INetwork> send)
    {
        if (CurrentRole != required) return false;
        if (!ContainerProvider.TryResolve<INetwork>(out var network)) return false;

        send(network);
        return true;
    }

    private bool SendStamped(CoopPeer peer, ISessionStamped message) =>
        peer.Handle is NetPeer target && Send(CoopRole.Server, n => SendStampedTo(n, target, message));

    private bool BroadcastStamped(ISessionStamped message, NetPeer? excluded) =>
        Send(CoopRole.Server, n =>
        {
            foreach (var target in LivePeers())
                if (!ReferenceEquals(target, excluded))
                    SendStampedTo(n, target, message);
        });

    /// <summary>Coop serializes the message inside Send, so one object can carry each connection's token in turn.</summary>
    private void SendStampedTo(INetwork network, NetPeer target, ISessionStamped message)
    {
        message.SessionToken = TokenOf(target);
        network.Send(target, message);
    }

    private string TokenOf(NetPeer peer)
    {
        lock (_peerTokensLock)
            return _peerTokens.TryGetValue(peer, out var token) ? token : "";
    }

    /// <summary>The connections that said hello, in hello order.</summary>
    private List<NetPeer> LivePeers()
    {
        lock (_peerTokensLock)
        {
            ForgetClosedConnections();
            return _peerTokens.Keys.ToList();
        }
    }

    /// <summary>Called under the lock, at each hello and each broadcast, so the map holds no closed connection for long.</summary>
    private void ForgetClosedConnections()
    {
        foreach (var gone in _peerTokens.Keys.Where(p => !_isLive(p)).ToList())
            _peerTokens.Remove(gone);
    }

    private bool FromServer(string? token, string what)
    {
        var session = _session;
        if (ContainerProvider.TryGetContainer(out ILifetimeScope scope))
        {
            if (session != null && session.Belongs(scope) && string.Equals(session.Token, token, StringComparison.Ordinal))
                return true;
            WarnDroppedOnce(scope, what);
        }
        return false;
    }

    private void WarnDroppedOnce(object scope, string what)
    {
        var warn = _warn;
        if (warn == null) return;
        lock (_dropLogLock)
        {
            if (_dropLoggedIn != null && _dropLoggedIn.TryGetTarget(out var held) && ReferenceEquals(held, scope)) return;
            _dropLoggedIn = new WeakReference<object>(scope);
        }
        _runOnGameThread(() => warn("[CoopCompat] dropped a " + what + ": its session token is not this client's. A mission peer sent it, "
            + "or the host runs another TAOM.CoopCompat build. Logged once per session."));
    }

    private static string NewToken()
    {
        var bytes = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
            rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
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
        var token = hello.SessionToken ?? "";
        lock (_peerTokensLock)
        {
            ForgetClosedConnections();
            _peerTokens[netPeer] = token.Length <= MaxTokenLength ? token : "";
        }
        _runOnGameThread(() => HelloReceived?.Invoke(peer, hello));
    }

    /// <summary>
    /// A host built before the token sends a reply with none, and a newer client always differs from it in build, so a
    /// reply with no token is passed on as a bare build mismatch at protocol 0, which the handshake reads as inconclusive:
    /// a forged one can show that one warning and nothing else.
    /// </summary>
    private void OnHelloReply(MessagePayload<CoopCompatHelloReply> payload)
    {
        var reply = payload.What;
        if (CurrentRole != CoopRole.Client) return;
        if (string.IsNullOrEmpty(reply.SessionToken) && reply.BuildMismatch)
            reply = new CoopCompatHelloReply { BuildMismatch = true };
        else if (!FromServer(reply.SessionToken, "hello reply"))
            return;

        _runOnGameThread(() => HelloReplyReceived?.Invoke(reply));
    }

    private void OnSnapshot(MessagePayload<CoopCompatMomentumSnapshot> payload)
    {
        if (CurrentRole != CoopRole.Client || !FromServer(payload.What.SessionToken, "momentum snapshot")) return;

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
        if (CurrentRole != CoopRole.Client || !FromServer(payload.What.SessionToken, "trade reply")) return;

        var reply = payload.What;
        _runOnGameThread(() => TradeReplyReceived?.Invoke(reply));
    }

    private void OnCareerReport(MessagePayload<CoopCompatCareerReport> payload)
    {
        if (CurrentRole != CoopRole.Server || payload.Who is not NetPeer netPeer) return;

        var report = payload.What;
        _runOnGameThread(() =>
        {
            var heroId = _heroOfPeer(netPeer) ?? "";
            CareerReportReceived?.Invoke(new CoopPeer(netPeer, netPeer.Id.ToString()), heroId, report);
        });
    }

    private void OnCareerUpdate(MessagePayload<CoopCompatCareerUpdate> payload)
    {
        if (CurrentRole != CoopRole.Client || !FromServer(payload.What.SessionToken, "career update")) return;

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

    /// <summary>The token of one Coop session: a new session container (host or join) makes a new token.</summary>
    private sealed class ClientSession
    {
        private readonly WeakReference<object> _scope;

        public ClientSession(string token, object scope)
        {
            Token = token;
            _scope = new WeakReference<object>(scope);
        }

        public string Token { get; }

        public bool Belongs(object scope) => _scope.TryGetTarget(out var held) && ReferenceEquals(held, scope);
    }

    /// <summary>A reconnect is a new NetPeer object; NetPeer (an IPEndPoint) compares endpoints, which would keep the old key.</summary>
    private sealed class ByReference : IEqualityComparer<NetPeer>
    {
        public static readonly ByReference Instance = new();

        public bool Equals(NetPeer? x, NetPeer? y) => ReferenceEquals(x, y);

        public int GetHashCode(NetPeer obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
