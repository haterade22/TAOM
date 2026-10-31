using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// Decides what spawns and when. Every lane of every side gets a free wave on a timer (the tier is picked by match
/// minute) and can queue purchased packs. Emission is capped per lane, per side and per tick; a request that does not
/// fit is split across ticks and never dropped. A free wave that cannot start while the caps are full is not stacked:
/// the next one is skipped until the pending one is out. Purchased packs go out before the free wave of their lane.
/// </summary>
internal sealed class WaveScheduler
{
    private sealed class Batch
    {
        public Batch(string troopId, UnitRole role, int remaining, int ownerSlot, UnitOffer? bought = null)
        {
            TroopId = troopId;
            Role = role;
            Remaining = remaining;
            OwnerSlot = ownerSlot;
            Bought = bought;
        }

        public string TroopId { get; }
        public UnitRole Role { get; }
        public int Remaining { get; set; }
        public int OwnerSlot { get; }
        public UnitOffer? Bought { get; }
    }

    private sealed class Stream
    {
        public float Timer;
        public readonly List<Batch> Packs = new();
        public readonly List<Batch> FreeWave = new();
    }

    private readonly FordsWaveConfig _config;
    private readonly IReadOnlyDictionary<MatchSide, FactionCatalog> _factions;
    private readonly int _laneCount;
    private readonly Func<MatchSide, int, int> _laneOwner;
    private readonly Stream[] _streams;
    private readonly Dictionary<int, UnitOffer> _packRequests = new();
    private int _lastRequestId;
    private int _cursor;

    /// <param name="laneOwner">The commander slot that owns (side, lane); free-wave units belong to it.</param>
    public WaveScheduler(FordsWaveConfig config, IReadOnlyDictionary<MatchSide, FactionCatalog> factions, int laneCount,
        Func<MatchSide, int, int> laneOwner)
    {
        _config = config;
        _factions = factions;
        _laneCount = Math.Max(0, laneCount);
        _laneOwner = laneOwner;
        _streams = new Stream[_laneCount * 2];
        for (var i = 0; i < _streams.Length; i++)
            _streams[i] = new Stream { Timer = config.FreeWaveIntervalSeconds };
    }

    /// <summary>The next spawn request id. Hero respawns draw from the same counter so ids never repeat.</summary>
    public int NextRequestId() => ++_lastRequestId;

    /// <summary>
    /// The offer a bought pack's request came from, once per request id; null for a free wave, a hero or an id already taken.
    /// The match calls it for every request a tick returns, so the table never outgrows one tick's requests.
    /// </summary>
    public UnitOffer? TakePackOffer(int requestId)
    {
        if (!_packRequests.TryGetValue(requestId, out var offer))
            return null;

        _packRequests.Remove(requestId);
        return offer;
    }

    /// <summary>Packs still waiting in the lane (a pack part-way out still counts).</summary>
    public int QueuedPacks(MatchSide side, int lane) => lane < 0 || lane >= _laneCount ? 0 : StreamOf(side, lane).Packs.Count;

    /// <summary>Queues a bought pack. The caller has already charged the gold and checked that the buyer owns the lane.</summary>
    public CommandResult TryQueuePack(MatchSide side, int lane, int ownerSlot, UnitOffer offer)
    {
        if (lane < 0 || lane >= _laneCount)
            return CommandResult.UnknownLane;
        if (offer.PackSize <= 0)
            return CommandResult.UnknownOffer;

        var stream = StreamOf(side, lane);
        if (stream.Packs.Count >= _config.MaxQueuedPacksPerLane)
            return CommandResult.QueueFull;

        stream.Packs.Add(new Batch(offer.TroopId, offer.Role, offer.PackSize, ownerSlot, offer));
        return CommandResult.Ok;
    }

    /// <summary>
    /// Advances the free-wave timers and returns what to spawn now. <paramref name="liveInLane"/> and
    /// <paramref name="liveOnSide"/> read the live unit counts at the start of the tick; units emitted by this call are
    /// counted on top by the scheduler itself.
    /// </summary>
    public IReadOnlyList<SpawnRequest> Tick(float dt, float minute, Func<MatchSide, int, int> liveInLane, Func<MatchSide, int> liveOnSide)
    {
        var requests = new List<SpawnRequest>();
        if (_streams.Length == 0)
            return requests;

        AdvanceTimers(dt, minute);

        var budget = _config.MaxSpawnsPerTick;
        var emittedBySide = new Dictionary<MatchSide, int> { [MatchSide.West] = 0, [MatchSide.East] = 0 };
        var emittedInLane = new int[_streams.Length];
        for (var n = 0; n < _streams.Length && budget > 0; n++)
        {
            var index = (_cursor + n) % _streams.Length;
            var side = SideOf(index);
            var lane = LaneOf(index);
            var room = Math.Min(
                _config.MaxLivePerLanePerSide - liveInLane(side, lane) - emittedInLane[index],
                _config.MaxLivePerSide - liveOnSide(side) - emittedBySide[side]);
            room = Math.Min(room, budget);
            if (room <= 0)
                continue;

            var sent = Emit(side, lane, _streams[index], room, requests);
            emittedInLane[index] += sent;
            emittedBySide[side] += sent;
            budget -= sent;
        }

        _cursor = (_cursor + 1) % _streams.Length;
        return requests;
    }

    private void AdvanceTimers(float dt, float minute)
    {
        var interval = _config.FreeWaveIntervalSeconds;
        if (interval <= 0f || dt <= 0f)
            return;

        for (var index = 0; index < _streams.Length; index++)
        {
            var stream = _streams[index];
            stream.Timer += dt;
            if (stream.Timer < interval)
                continue;

            stream.Timer %= interval;
            if (stream.FreeWave.Count == 0)
                StartFreeWave(SideOf(index), stream, minute);
        }
    }

    private void StartFreeWave(MatchSide side, Stream stream, float minute)
    {
        if (!_factions.TryGetValue(side, out var faction))
            return;

        FreeWaveTier? tier = null;
        foreach (var candidate in faction.FreeWaves)
        {
            if (candidate.FromMinute <= minute && (tier == null || candidate.FromMinute > tier.FromMinute))
                tier = candidate;
        }

        if (tier == null)
            return;

        foreach (var unit in tier.Units.Where(u => u.Count > 0))
        {
            var role = faction.Units.FirstOrDefault(o => o.TroopId == unit.TroopId)?.Role ?? UnitRole.Infantry;
            stream.FreeWave.Add(new Batch(unit.TroopId, role, unit.Count, -1));
        }
    }

    private int Emit(MatchSide side, int lane, Stream stream, int room, List<SpawnRequest> requests)
    {
        var sent = Drain(side, lane, stream.Packs, room, requests, null);
        if (sent < room)
            sent += Drain(side, lane, stream.FreeWave, room - sent, requests, _laneOwner(side, lane));
        return sent;
    }

    private int Drain(MatchSide side, int lane, List<Batch> batches, int room, List<SpawnRequest> requests, int? ownerOverride)
    {
        var sent = 0;
        while (room > sent && batches.Count > 0)
        {
            var batch = batches[0];
            var count = Math.Min(batch.Remaining, room - sent);
            var request = new SpawnRequest(NextRequestId(), side, lane, batch.TroopId, count,
                ownerOverride ?? batch.OwnerSlot, batch.Role, false);
            requests.Add(request);
            if (batch.Bought != null)
                _packRequests[request.RequestId] = batch.Bought;
            batch.Remaining -= count;
            sent += count;
            if (batch.Remaining <= 0)
                batches.RemoveAt(0);
        }

        return sent;
    }

    private Stream StreamOf(MatchSide side, int lane) => _streams[lane * 2 + (side == MatchSide.West ? 0 : 1)];

    private static MatchSide SideOf(int streamIndex) => streamIndex % 2 == 0 ? MatchSide.West : MatchSide.East;

    private static int LaneOf(int streamIndex) => streamIndex / 2;
}
