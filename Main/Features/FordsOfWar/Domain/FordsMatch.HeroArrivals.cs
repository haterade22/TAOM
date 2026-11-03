using System;
using System.Linq;

namespace TAOM.Features.FordsOfWar.Domain;

internal sealed partial class FordsMatch
{
    private int _heroArrivalsDone;

    /// <summary>
    /// Every FreeArrivalSeconds of live time each side's next hero takes the field for nothing, so both sides see their heroes whatever their gold. A side with no slot free or
    /// no hero left waits for the next arrival: a missed one does not carry over.
    /// </summary>
    private void RunHeroArrivals()
    {
        var seconds = _config.Heroes.FreeArrivalSeconds;
        if (seconds <= 0f)
            return;

        while (_clock.Elapsed >= (_heroArrivalsDone + 1) * seconds)
        {
            _heroArrivalsDone++;
            foreach (var side in Sides)
                FieldFreeHero(side);
        }
    }

    /// <summary>Into the lane where the side is most outnumbered, the lowest index winning a tie. The hero keeps its price, because a buyback is priced on the hero.</summary>
    private void FieldFreeHero(MatchSide side)
    {
        var offer = NextFreeHero(side);
        if (offer == null)
            return;

        var enemy = MatchSides.Opponent(side);
        var lane = _layout.Lanes.Select(l => l.Index).OrderByDescending(i => LiveInLane(enemy, i) - LiveInLane(side, i)).ThenBy(i => i).First();
        var owner = SlotsOf(side).FirstOrDefault(s => s.Lanes.Contains(lane));
        if (owner != null && _heroes.TryField(side, offer.HeroId, owner.Id, lane, offer.Price) == CommandResult.Ok)
            QueueFieldedHero(side, offer, 0);
    }

    /// <summary>The cheapest hero the side has not fielded, so the strongest comes last.</summary>
    private HeroOffer? NextFreeHero(MatchSide side) =>
        HeroOffersOf(side).Where(h => _heroes.Get(side, h.HeroId) == null).OrderBy(h => h.Price).ThenBy(h => h.HeroId, StringComparer.Ordinal).FirstOrDefault();

    /// <summary>Seconds to the next arrival; null when it would bring this side nothing, because arrivals are off, no hero is left or no slot is free now.</summary>
    private float? NextFreeHeroIn(MatchSide side)
    {
        var seconds = _config.Heroes.FreeArrivalSeconds;
        if (seconds <= 0f || NextFreeHero(side) == null || _heroes.AliveCount(side) >= _heroes.MaxAlive(side))
            return null;

        return Math.Max(0f, (_heroArrivalsDone + 1) * seconds - _clock.Elapsed);
    }

    /// <summary>A hero the roster has just fielded: its spawn, what a failed spawn refunds, and the event.</summary>
    private void QueueFieldedHero(MatchSide side, HeroOffer offer, int paid)
    {
        var request = _heroes.Get(side, offer.HeroId)!.ToSpawnRequest(_waves.NextRequestId());
        _pendingSpawns.Add(request);
        TrackCharge(request, paid, null);
        _pendingEvents.Add(new MatchEvent(MatchEventKind.HeroFielded, side, offer.HeroId, paid));
    }
}
