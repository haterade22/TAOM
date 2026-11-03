using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.FordsOfWar.Domain;

internal sealed partial class FordsMatch
{
    /// <summary>What a spawn request charged its owner, kept until the request is fully out or has failed.</summary>
    private sealed class SpawnCharge
    {
        public SpawnCharge(SpawnRequest request, int heroPaid, UnitOffer? pack)
        {
            Request = request;
            HeroPaid = heroPaid;
            Pack = pack;
            Unregistered = request.Count;
        }

        public SpawnRequest Request { get; }
        public int HeroPaid { get; }
        public UnitOffer? Pack { get; }
        public int Unregistered { get; set; }
    }

    /// <summary>The units of an ordinary request the scheduler issued and the engine layer has not spawned yet.</summary>
    private sealed class Reserved
    {
        public Reserved(MatchSide side, int lane, int units)
        {
            Side = side;
            Lane = lane;
            Units = units;
        }

        public MatchSide Side { get; }
        public int Lane { get; }
        public int Units { get; set; }
    }

    private readonly Dictionary<int, SpawnCharge> _charges = new();
    private readonly Dictionary<int, Reserved> _reserved = new();
    private readonly Dictionary<(MatchSide, string), int> _buybackPaid = new();
    private readonly HashSet<(MatchSide, string)> _lostPacks = new();
    private readonly HashSet<(MatchSide, string)> _lostHeroes = new();

    /// <summary>
    /// The engine layer could not spawn <paramref name="unspawned"/> units of a request. A hero request gives the owner back
    /// what it charged (the purchase price, the buyback price, or nothing for a natural respawn), drops the hero's entry so
    /// it holds no alive slot, and takes the hero out of the side's offers for the rest of the match. A bought pack gives back
    /// its price in proportion to the units that did not spawn (rounded down) and takes the offer out for that side. Any ordinary
    /// request, a free wave included, also gives back the room the scheduler reserved for its units; a free wave charged nobody, so that
    /// is all it changes. An unknown request and a request already settled change nothing.
    /// </summary>
    public void OnSpawnFailed(int requestId, int unspawned)
    {
        _reserved.Remove(requestId);
        if (!_charges.TryGetValue(requestId, out var charge))
            return;

        _charges.Remove(requestId);
        var request = charge.Request;
        if (request.IsHero)
        {
            _ledger.Earn(request.OwnerSlot, charge.HeroPaid, GoldSource.Refund);
            _heroes.Remove(request.Side, request.TroopId);
            _lostHeroes.Add((request.Side, request.TroopId));
            return;
        }

        var pack = charge.Pack;
        if (pack == null)
            return;

        var lost = Math.Max(0, Math.Min(unspawned, charge.Unregistered));
        _ledger.Earn(request.OwnerSlot, (int)((long)pack.PackPrice * lost / pack.PackSize), GoldSource.Refund);
        _lostPacks.Add((request.Side, pack.OfferId));
    }

    private void TrackCharge(SpawnRequest request, int heroPaid, UnitOffer? pack) =>
        _charges[request.RequestId] = new SpawnCharge(request, heroPaid, pack);

    /// <summary>Holds room for the units of an ordinary request until they spawn. A hero keeps its explicit allowance and holds none.</summary>
    private void Reserve(SpawnRequest request)
    {
        if (!request.IsHero && request.Count > 0)
            _reserved[request.RequestId] = new Reserved(request.Side, request.Lane, request.Count);
    }

    private int ReservedInLane(MatchSide side, int lane) =>
        _reserved.Values.Where(r => r.Side == side && r.Lane == lane).Sum(r => r.Units);

    private int ReservedOnSide(MatchSide side) => _reserved.Values.Where(r => r.Side == side).Sum(r => r.Units);

    /// <summary>One unit of a request is out: it frees one reserved place, and the charge record goes once all of its units are out.</summary>
    private void NoteSpawned(int requestId)
    {
        if (_reserved.TryGetValue(requestId, out var held) && --held.Units <= 0)
            _reserved.Remove(requestId);

        if (_charges.TryGetValue(requestId, out var charge) && --charge.Unregistered <= 0)
            _charges.Remove(requestId);
    }

    /// <summary>The buyback price this hero's coming respawn was paid with; 0 for a natural respawn.</summary>
    private int TakeBuybackPaid(MatchSide side, string heroId)
    {
        var key = (side, heroId);
        if (!_buybackPaid.TryGetValue(key, out var paid))
            return 0;

        _buybackPaid.Remove(key);
        return paid;
    }
}
