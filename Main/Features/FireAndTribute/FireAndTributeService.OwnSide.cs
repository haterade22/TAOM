using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.Execution;

namespace TAOM.Features.FireAndTribute;

/// <summary>
/// The own-side half of <see cref="FireAndTributeService"/>: the snapshot that tells the party AI model which parties
/// leave each live host alone. A host's clan is an outlaw clan at war with every kingdom, and a host rides out of its own
/// side's realms, so without this its own side's lords broke it before it reached its target (2026-10-26: 7 hosts,
/// 0 raids). Only the host's own party is spared; the clan's ordinary bandits stay vanilla's.
/// </summary>
/// <remarks>
/// The snapshot holds each live host's side, keyed by party id, and is replaced whole on the main thread whenever the
/// host list changes. A kingdom's side is resolved when the model asks, through <see cref="IAlignmentService"/>. Its own
/// map is written only in its constructor, and the War Pledge store it also reads (#1113) swaps whole maps behind a
/// volatile reference, so the AI worker reads nothing that changes under it. The side function is the
/// one the target picker uses (<c>TargetCandidates</c>), so a Neutral host's own side is every Neutral-resolving kingdom.
/// </remarks>
public sealed partial class FireAndTributeService : IRaidHostOwnSideQuery
{
    private static readonly IReadOnlyDictionary<string, FactionSide> NoHosts =
        new Dictionary<string, FactionSide>(StringComparer.Ordinal);

    // Host party id to the host's side. Written whole on the main thread, read on the AI worker.
    private volatile IReadOnlyDictionary<string, FactionSide> _hostSides = NoHosts;

    public bool AnyHost => _hostSides.Count > 0;

    public bool IsOwnSide(string? kingdomId, string? kingdomCultureId, string? hostPartyId) =>
        kingdomId != null && hostPartyId != null
        && _hostSides.TryGetValue(hostPartyId, out var side)
        && _alignment.ResolveSide(kingdomId, kingdomCultureId ?? string.Empty) == side;

    public bool IsHost(string? partyId) => partyId != null && _hostSides.ContainsKey(partyId);

    /// <summary>Drops the record and publishes the snapshot without it.</summary>
    private void RemoveHost(RaidHostRecord record)
    {
        _state.Hosts.Remove(record);
        PublishOwnSide();
    }

    /// <summary>Replaces the snapshot from the live records. Reads no engine state, so any main-thread path may call it.</summary>
    private void PublishOwnSide() =>
        _hostSides = _state.Hosts.Count == 0
            ? NoHosts
            : _state.Hosts.GroupBy(h => h.PartyId, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First().Side, StringComparer.Ordinal);
}
