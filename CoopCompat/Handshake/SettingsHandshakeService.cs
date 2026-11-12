using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.CoopCompat.Messages;
using TAOM.CoopCompat.Transport;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using FingerprintReport = TAOM.Features.CoopInterop.SettingsFingerprint.FingerprintReport;

namespace TAOM.CoopCompat.Handshake;

/// <summary>The server's answer to a hello. The value travels on the wire as an int.</summary>
public enum HandshakeVerdict
{
    Match = 0,
    Differ = 1,
    Inconclusive = 2,
}

/// <summary>
/// The settings and build handshake. A client sends its fingerprint when its campaign join completes;
/// the server compares it with its own, logs the result and answers; the client tells the player.
/// It warns and never disconnects (decision D2). Each handler reads the role when its message
/// arrives, because the session container changes on every host or join.
/// </summary>
public sealed class SettingsHandshakeService
{
    private const string ServerOutcome = "taom.coopcompat.settings.server";
    private const string ClientOutcome = "taom.coopcompat.settings.client";

    private readonly ICoopTransport _transport;
    private readonly Func<FingerprintReport> _localFingerprint;
    private readonly BuildIdentity _identity;
    private readonly ISettingsNotice _notice;
    private readonly IModLogger _logger;
    private readonly IOutcomeLog _outcomes;

    public SettingsHandshakeService(ICoopTransport transport, Func<FingerprintReport> localFingerprint, BuildIdentity identity,
        ISettingsNotice notice, IModLogger logger, IOutcomeLog outcomes)
    {
        _transport = transport;
        _localFingerprint = localFingerprint;
        _identity = identity;
        _notice = notice;
        _logger = logger;
        _outcomes = outcomes;
        _transport.ClientReady += OnClientReady;
        _transport.HelloReceived += OnHello;
        _transport.HelloReplyReceived += OnReply;
    }

    private void OnClientReady()
    {
        if (_transport.CurrentRole != CoopRole.Client) return;

        var report = _localFingerprint();
        var names = report.ByGroup.Keys.ToArray();
        var hello = new CoopCompatHello
        {
            ProtocolVersion = _identity.ProtocolVersion,
            TaomVersion = _identity.TaomVersion,
            TaomMvid = _identity.TaomMvid,
            CoopCompatMvid = _identity.CoopCompatMvid,
            SettingsGlobal = report.Global,
            GroupNames = names,
            GroupHashes = names.Select(n => report.ByGroup[n]).ToArray(),
            Conclusive = report.IsConclusive,
        };

        if (_transport.SendToServer(hello))
            _logger.LogInfo("[CoopCompat] settings hello sent covered=" + report.Covered + " groups=" + names.Length);
        else
            _logger.LogWarning("[CoopCompat] settings hello not sent: no network to the server");
    }

    private void OnHello(CoopPeer peer, CoopCompatHello hello)
    {
        if (_transport.CurrentRole != CoopRole.Server) return;

        var buildMismatch = _identity.DiffersFrom(hello.ProtocolVersion, hello.TaomVersion, hello.TaomMvid, hello.CoopCompatMvid);
        var protocolMatches = hello.ProtocolVersion == _identity.ProtocolVersion;
        var local = _localFingerprint();
        var divergent = new List<string>();
        var verdict = HandshakeVerdict.Inconclusive;

        // Another protocol means the group layout cannot be trusted: say so and compare nothing.
        if (protocolMatches && local.IsConclusive && hello.Conclusive)
        {
            divergent = DivergentGroups(local, hello);
            verdict = divergent.Count > 0 || !string.Equals(local.Global, hello.SettingsGlobal, StringComparison.Ordinal)
                ? HandshakeVerdict.Differ
                : HandshakeVerdict.Match;
        }

        LogServerSide(peer.Label, verdict, divergent, buildMismatch);
        _transport.SendToPeer(peer, new CoopCompatHelloReply
        {
            ProtocolVersion = _identity.ProtocolVersion,
            Verdict = (int)verdict,
            DivergentGroups = divergent.ToArray(),
            BuildMismatch = buildMismatch,
        });
    }

    private void LogServerSide(string peer, HandshakeVerdict verdict, List<string> divergent, bool buildMismatch)
    {
        switch (verdict)
        {
            case HandshakeVerdict.Match:
                _logger.LogInfo("[CoopCompat] settings peer=" + peer + " match");
                break;
            case HandshakeVerdict.Differ:
                _logger.LogInfo("[CoopCompat] settings peer=" + peer + " differ: " + string.Join(", ", divergent));
                break;
            default:
                _logger.LogInfo("[CoopCompat] settings peer=" + peer + " inconclusive");
                break;
        }

        if (buildMismatch)
            _logger.LogWarning("[CoopCompat] build mismatch peer=" + peer + " (protocol, TAOM version or a module build differs)");

        _outcomes.Pass(ServerOutcome, peer, "verdict=" + Word(verdict) + " groups=" + divergent.Count + " build_mismatch=" + Flag(buildMismatch));
    }

    // Groups whose hash differs, or that one side lacks, in name order.
    private static List<string> DivergentGroups(FingerprintReport local, CoopCompatHello hello)
    {
        var theirs = new Dictionary<string, string>(StringComparer.Ordinal);
        var count = Math.Min(hello.GroupNames.Length, hello.GroupHashes.Length);
        for (var i = 0; i < count; i++) theirs[hello.GroupNames[i]] = hello.GroupHashes[i];

        var names = new SortedSet<string>(local.ByGroup.Keys, StringComparer.Ordinal);
        names.UnionWith(theirs.Keys);
        return names
            .Where(n => !local.ByGroup.TryGetValue(n, out var mine) || !theirs.TryGetValue(n, out var other) || !string.Equals(mine, other, StringComparison.Ordinal))
            .ToList();
    }

    private void OnReply(CoopCompatHelloReply reply)
    {
        if (_transport.CurrentRole != CoopRole.Client) return;

        var protocolMismatch = reply.ProtocolVersion != _identity.ProtocolVersion;
        var buildMismatch = reply.BuildMismatch || protocolMismatch;
        var verdict = protocolMismatch ? HandshakeVerdict.Inconclusive : (HandshakeVerdict)reply.Verdict;

        if (buildMismatch)
        {
            _logger.LogWarning("[CoopCompat] build mismatch with the server (protocol, TAOM version or a module build differs)");
            _notice.BuildMismatch();
        }

        if (verdict == HandshakeVerdict.Differ && reply.DivergentGroups.Length > 0)
        {
            _logger.LogWarning("[CoopCompat] settings differ from the server: " + string.Join(", ", reply.DivergentGroups));
            _notice.SettingsDiffer(reply.DivergentGroups);
        }
        else if (verdict == HandshakeVerdict.Match)
        {
            _logger.LogInfo("[CoopCompat] settings match the server");
        }
        else if (verdict == HandshakeVerdict.Inconclusive)
        {
            _logger.LogInfo("[CoopCompat] settings could not be compared with the server");
        }

        var detail = "verdict=" + Word(verdict) + " groups=" + reply.DivergentGroups.Length + " build_mismatch=" + Flag(buildMismatch);
        if (buildMismatch) _outcomes.Fail(ClientOutcome, "", detail);
        else _outcomes.Pass(ClientOutcome, "", detail);
    }

    private static string Word(HandshakeVerdict verdict) => verdict.ToString().ToLowerInvariant();

    private static string Flag(bool value) => value ? "true" : "false";
}
