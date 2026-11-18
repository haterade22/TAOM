using System;
using System.Collections.Generic;
using TAOM.CoopCompat.Messages;
using TAOM.CoopCompat.Transport;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop.CareerSync;

namespace TAOM.CoopCompat.CareerSync;

/// <summary>
/// Carries career sync (#967, docs/features/coop-interop.md "Career sync") over Coop: a client's career report to the
/// server, and the server's careers back to the clients. TAOM's <see cref="CareerSyncHost"/> decides and stores; TAOM's
/// <see cref="CareerSyncClient"/> reports and writes. <see cref="Tick"/> runs every frame so the client sends one report
/// per action. A message from another protocol version is dropped. Design credit: minimalcash63 (TAOM Co-op Bridge).
/// </summary>
public sealed class CareerSyncRelayService : ICareerSyncLink
{
    private readonly ICoopTransport _transport;
    private readonly CareerSyncClient _client;
    private readonly CareerSyncHost _host;
    private readonly IModLogger _logger;
    private bool _protocolMismatchLogged;
    private bool _sendFailureLogged;
    private bool _broadcastFailureLogged;

    public CareerSyncRelayService(ICoopTransport transport, CareerSyncClient client, CareerSyncHost host, IModLogger logger)
    {
        _transport = transport;
        _client = client;
        _host = host;
        _logger = logger;
        _transport.ClientReady += _client.OnJoined;
        _transport.CareerReportReceived += OnReport;
        _transport.CareerUpdateReceived += OnUpdate;
        _host.StoredCareerChanged += OnStoredCareerChanged;
        _client.Attach(this);
    }

    /// <summary>
    /// Called every frame, outside Coop's GameThread.RunSafe; sends the client's waiting report, if any. A failure is
    /// logged once and goes no further, so it never reaches the engine's tick.
    /// </summary>
    public void Tick()
    {
        try
        {
            _client.Flush();
        }
        catch (Exception ex)
        {
            if (!_sendFailureLogged) _logger.LogWarning($"[CareerSync] could not send a career report: {ex.GetType().Name}: {ex.Message}");
            _sendFailureLogged = true;
        }
    }

    public bool SendToHost(CareerSyncSnapshot career, long sequence, bool joinReport) => _transport.SendToServer(new CoopCompatCareerReport
    {
        ProtocolVersion = CoopCompatProtocol.ProtocolVersion,
        Sequence = sequence,
        JoinReport = joinReport,
        HeroId = career.HeroId ?? "",
        CareerId = career.CareerId ?? "",
        Choices = ToArray(career.Choices),
        Tiers = ToArray(career.Tiers),
        Flags = ToArray(career.Flags),
    });

    private void OnReport(CoopPeer peer, string heroId, CoopCompatCareerReport message)
    {
        if (!SameProtocol(message.ProtocolVersion)) return;

        var sent = new CareerSyncSnapshot(message.HeroId ?? "", OrNull(message.CareerId), message.Choices ?? Array.Empty<string>(),
            message.Tiers ?? Array.Empty<int>(), message.Flags ?? Array.Empty<string>());
        var outcome = _host.Handle(heroId, sent, message.Sequence, message.JoinReport);

        if (outcome.ToSender != null && !_transport.SendToPeer(peer, ToWire(outcome.ToSender)))
            _logger.LogWarning($"[CareerSync] could not answer the career report of '{heroId}' (peer {peer.Label}).");
        if (outcome.ToOthers != null && !_transport.BroadcastExcept(peer, ToWire(outcome.ToOthers)))
            _logger.LogWarning($"[CareerSync] could not pass on the career of '{heroId}'.");
    }

    private void OnUpdate(CoopCompatCareerUpdate message)
    {
        if (!SameProtocol(message.ProtocolVersion)) return;

        _client.Receive(new CareerSyncSnapshot(message.HeroId ?? "", OrNull(message.CareerId), message.Choices ?? Array.Empty<string>(),
            message.Tiers ?? Array.Empty<int>(), message.Flags ?? Array.Empty<string>()), message.Sequence, message.Refused, message.Known);
    }

    // Raised inside the career book's mutator (a death clear runs in a campaign event), so a failure stays here.
    private void OnStoredCareerChanged(CareerSyncAnswer answer)
    {
        try
        {
            if (_transport.CurrentRole != CoopRole.Server) return;

            _transport.Broadcast(ToWire(answer));
        }
        catch (Exception ex)
        {
            if (!_broadcastFailureLogged) _logger.LogWarning($"[CareerSync] could not pass on a change to '{answer.Career.HeroId}': {ex.GetType().Name}: {ex.Message}");
            _broadcastFailureLogged = true;
        }
    }

    private static CoopCompatCareerUpdate ToWire(CareerSyncAnswer answer) => new()
    {
        ProtocolVersion = CoopCompatProtocol.ProtocolVersion,
        Sequence = answer.Sequence,
        HeroId = answer.Career.HeroId ?? "",
        CareerId = answer.Career.CareerId ?? "",
        Choices = ToArray(answer.Career.Choices),
        Tiers = ToArray(answer.Career.Tiers),
        Flags = ToArray(answer.Career.Flags),
        Refused = answer.Refused,
        Known = answer.Known,
    };

    private bool SameProtocol(int version)
    {
        if (version == CoopCompatProtocol.ProtocolVersion) return true;

        if (!_protocolMismatchLogged)
        {
            _protocolMismatchLogged = true;
            _logger.LogWarning($"[CareerSync] dropped a career message of protocol {version}; this build speaks {CoopCompatProtocol.ProtocolVersion}.");
        }
        return false;
    }

    private static T[] ToArray<T>(IReadOnlyList<T>? list)
    {
        if (list == null || list.Count == 0) return Array.Empty<T>();

        var array = new T[list.Count];
        for (var i = 0; i < list.Count; i++) array[i] = list[i];
        return array;
    }

    private static string? OrNull(string? text) => string.IsNullOrEmpty(text) ? null : text;
}
