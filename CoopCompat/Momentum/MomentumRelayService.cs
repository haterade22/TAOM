using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TAOM.CoopCompat.Messages;
using TAOM.CoopCompat.Transport;
using TAOM.Core.Logging;
using TAOM.Features.WarOfTheRingMomentum;

namespace TAOM.CoopCompat.Momentum;

/// <summary>
/// Sends the server's War of the Ring momentum to the clients, and applies it on a client. The
/// server answers each hello with a snapshot, then broadcasts after a change (at most once a
/// minute of real time) and once every 24 in-game hours as a keep-alive. A client writes only
/// TAOM's own mirror of the war state. The clocks are seams so a test needs no game.
/// </summary>
public sealed class MomentumRelayService
{
    private const string ServerOutcome = "taom.coopcompat.momentum.server";
    private const string ClientOutcome = "taom.coopcompat.momentum.client";
    private const double MinSecondsBetweenSends = 60;
    private const double KeepAliveHours = 24;

    private readonly ICoopTransport _transport;
    private readonly IMomentumStateStore _store;
    private readonly IModLogger _logger;
    private readonly IOutcomeLog _outcomes;
    private readonly Func<double> _realSeconds;
    private readonly Func<double> _campaignHours;

    private long _sequence;
    private bool _dirty;
    private bool _hasSentBroadcast;
    private double _lastSendSeconds;
    private double _keepAliveBaselineHours = double.NaN;
    private string _lastSentComparable = "";

    private bool _clientReady;
    private long _lastAppliedSequence;
    private bool _protocolMismatchLogged;

    /// <param name="realSeconds">A monotonic real-time clock in seconds.</param>
    /// <param name="campaignHours">The campaign clock in hours.</param>
    public MomentumRelayService(ICoopTransport transport, IMomentumStateStore store, IModLogger logger, IOutcomeLog outcomes,
        Func<double> realSeconds, Func<double> campaignHours)
    {
        _transport = transport;
        _store = store;
        _logger = logger;
        _outcomes = outcomes;
        _realSeconds = realSeconds;
        _campaignHours = campaignHours;
        _transport.HelloReceived += OnHello;
        _transport.ClientReady += OnClientReady;
        _transport.SnapshotReceived += OnSnapshot;
        _store.MomentumChanged += OnMomentumChanged;
    }

    private void OnMomentumChanged() => _dirty = true;

    /// <summary>Called from the campaign behavior's hourly tick.</summary>
    public void OnHourlyTick()
    {
        if (_transport.CurrentRole != CoopRole.Server) return;

        var hours = _campaignHours();
        if (double.IsNaN(_keepAliveBaselineHours)) _keepAliveBaselineHours = hours;

        var keepAliveDue = hours - _keepAliveBaselineHours >= KeepAliveHours;
        if (!_dirty && !keepAliveDue) return;

        // The real-time floor comes first so a busy campaign never serializes more than once a minute.
        var now = _realSeconds();
        if (_hasSentBroadcast && now - _lastSendSeconds < MinSecondsBetweenSends) return;

        var data = _store.CaptureWarState();
        var comparable = Comparable(data);
        if (!keepAliveDue && comparable == _lastSentComparable)
        {
            _dirty = false;
            return;
        }

        var snapshot = Build(data);
        if (!_transport.Broadcast(snapshot)) return;

        _dirty = false;
        _hasSentBroadcast = true;
        _lastSendSeconds = now;
        _keepAliveBaselineHours = hours;
        _lastSentComparable = comparable;
        _logger.LogInfo("[CoopCompat] momentum broadcast sequence=" + snapshot.Sequence + " entries=" + snapshot.Keys.Length + (keepAliveDue ? " keepalive" : ""));
    }

    private void OnHello(CoopPeer peer, CoopCompatHello hello)
    {
        if (_transport.CurrentRole != CoopRole.Server) return;

        var snapshot = Build(_store.CaptureWarState());
        if (!_transport.SendToPeer(peer, snapshot))
        {
            _logger.LogWarning("[CoopCompat] momentum snapshot to peer=" + peer.Label + " not sent: no network");
            return;
        }

        // The check line proves a joining client was answered, so it is written here, once per peer.
        var detail = "sequence=" + snapshot.Sequence + " entries=" + snapshot.Keys.Length;
        _logger.LogInfo("[CoopCompat] momentum snapshot to peer=" + peer.Label + " " + detail);
        _outcomes.Pass(ServerOutcome, peer.Label, detail);
    }

    private CoopCompatMomentumSnapshot Build(Dictionary<string, string> data)
    {
        var keys = data.Keys.ToArray();
        return new CoopCompatMomentumSnapshot
        {
            ProtocolVersion = CoopCompatProtocol.ProtocolVersion,
            Sequence = ++_sequence,
            Keys = keys,
            Values = keys.Select(k => data[k]).ToArray(),
        };
    }

    // The snapshot as text in key order, so two captures of the same state compare equal.
    private static string Comparable(Dictionary<string, string> data)
    {
        var text = new StringBuilder();
        foreach (var pair in data.OrderBy(p => p.Key, StringComparer.Ordinal))
            text.Append(pair.Key).Append('=').Append(pair.Value).Append('\n');

        return text.ToString();
    }

    private void OnClientReady()
    {
        if (_transport.CurrentRole != CoopRole.Client) return;

        // A new join may meet a restarted server whose sequence starts over.
        _clientReady = true;
        _lastAppliedSequence = 0;
    }

    private void OnSnapshot(CoopCompatMomentumSnapshot snapshot)
    {
        if (snapshot.ProtocolVersion != CoopCompatProtocol.ProtocolVersion)
        {
            if (!_protocolMismatchLogged)
                _logger.LogWarning("[CoopCompat] momentum snapshot dropped: protocol " + snapshot.ProtocolVersion + " is not " + CoopCompatProtocol.ProtocolVersion);
            _protocolMismatchLogged = true;
            return;
        }

        if (_transport.CurrentRole != CoopRole.Client || !_clientReady || snapshot.Sequence <= _lastAppliedSequence) return;

        if (snapshot.Keys.Length != snapshot.Values.Length)
        {
            Reject(snapshot, "keys and values differ in count");
            return;
        }

        var data = new Dictionary<string, string>(snapshot.Keys.Length, StringComparer.Ordinal);
        for (var i = 0; i < snapshot.Keys.Length; i++) data[snapshot.Keys[i]] = snapshot.Values[i];

        if (!_store.ApplyWarState(data))
        {
            Reject(snapshot, "the store refused it");
            return;
        }

        _lastAppliedSequence = snapshot.Sequence;
        _logger.LogInfo("[CoopCompat] momentum applied sequence=" + snapshot.Sequence + " entries=" + data.Count);
        _outcomes.Pass(ClientOutcome, "", "sequence=" + snapshot.Sequence + " entries=" + data.Count);
    }

    private void Reject(CoopCompatMomentumSnapshot snapshot, string reason)
    {
        _logger.LogWarning("[CoopCompat] momentum snapshot rejected sequence=" + snapshot.Sequence + ": " + reason);
        _outcomes.Fail(ClientOutcome, "", "sequence=" + snapshot.Sequence + " reason=" + reason.Replace(' ', '_'));
    }
}
