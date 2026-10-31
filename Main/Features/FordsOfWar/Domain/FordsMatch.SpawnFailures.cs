using System;
using System.Collections.Generic;

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

    private readonly Dictionary<int, SpawnCharge> _charges = new();
    private readonly Dictionary<(MatchSide, string), int> _buybackPaid = new();
    private readonly HashSet<(MatchSide, string)> _lostPacks = new();
    private readonly HashSet<(MatchSide, string)> _lostHeroes = new();

    /// <summary>
    /// The engine layer could not spawn <paramref name="unspawned"/> units of a request. A hero request gives the owner back
    /// what it charged (the purchase price, the buyback price, or nothing for a natural respawn), drops the hero's entry so
    /// it holds no alive slot, and takes the hero out of the side's offers for the rest of the match. A bought pack gives back
    /// its price in proportion to the units that did not spawn (rounded down) and takes the offer out for that side. A free
    /// wave, an unknown request and a request already settled change nothing.
    /// </summary>
    public void OnSpawnFailed(int requestId, int unspawned)
    {
        if (!_charges.TryGetValue(requestId, out var charge))
            return;

        _charges.Remove(requestId);
        var request = charge.Request;
        if (request.IsHero)
        {
            _ledger.Earn(request.OwnerSlot, charge.HeroPaid);
            _heroes.Remove(request.Side, request.TroopId);
            _lostHeroes.Add((request.Side, request.TroopId));
            return;
        }

        var pack = charge.Pack;
        if (pack == null)
            return;

        var lost = Math.Max(0, Math.Min(unspawned, charge.Unregistered));
        _ledger.Earn(request.OwnerSlot, (int)((long)pack.PackPrice * lost / pack.PackSize));
        _lostPacks.Add((request.Side, pack.OfferId));
    }

    private void TrackCharge(SpawnRequest request, int heroPaid, UnitOffer? pack) =>
        _charges[request.RequestId] = new SpawnCharge(request, heroPaid, pack);

    /// <summary>One unit of a charged request is out; the record goes once all of its units are.</summary>
    private void NoteSpawned(int requestId)
    {
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
