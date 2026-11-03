using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>A region's band of troops. Name is a <c>{=TAOM_FOW_...}English</c> string.</summary>
internal sealed record RegionBand(string Id, string Name, IReadOnlyList<UnitOffer> Units);

/// <summary>A faction whose offers change with time: one region shows for Seconds, and CallFee shows a chosen one at once.</summary>
internal sealed record OfferRotation(float Seconds, int CallFee, IReadOnlyList<RegionBand> Regions);

/// <summary>
/// Which region a side's commander can buy from. A shuffled deck: each region comes once per cycle, and a new cycle never starts with
/// the region just shown. The next region is known in advance, so a panel can name it. Calling a region by name shows it at once and
/// restarts the timer; it leaves the draw pile, so it does not come round again soon.
/// </summary>
internal sealed class RegionDeck
{
    private readonly OfferRotation _rotation;
    private readonly IFordsRandom _random;
    private readonly Queue<RegionBand> _pile = new();

    public RegionDeck(OfferRotation rotation, IFordsRandom random)
    {
        _rotation = rotation;
        _random = random;
        SecondsLeft = rotation.Seconds;
        Refill(null);
        Current = _pile.Dequeue();
        EnsureNext();
    }

    public RegionBand Current { get; private set; }

    public RegionBand Next => _pile.Peek();

    public float SecondsLeft { get; private set; }

    public float TotalSeconds => _rotation.Seconds;

    public int CallFee => _rotation.CallFee;

    public IReadOnlyList<RegionBand> Regions => _rotation.Regions;

    public void Tick(float dt)
    {
        if (!(dt > 0f) || _rotation.Seconds <= 0f)
            return;

        SecondsLeft -= dt;
        while (SecondsLeft <= 0f)
        {
            SecondsLeft += _rotation.Seconds;
            Current = _pile.Dequeue();
            EnsureNext();
        }
    }

    /// <summary>Shows the named region now. False for a region this deck does not hold.</summary>
    public bool Call(string regionId)
    {
        var region = _rotation.Regions.FirstOrDefault(r => r.Id == regionId);
        if (region == null)
            return false;

        Current = region;
        SecondsLeft = _rotation.Seconds;
        var rest = _pile.Where(r => r.Id != regionId).ToList();
        _pile.Clear();
        foreach (var band in rest)
            _pile.Enqueue(band);
        EnsureNext();
        return true;
    }

    private void EnsureNext()
    {
        if (_pile.Count == 0)
            Refill(Current);
    }

    /// <summary>A new shuffled cycle. With <paramref name="notFirst"/>, that region does not come first, so it never shows twice running.</summary>
    private void Refill(RegionBand? notFirst)
    {
        var cards = _rotation.Regions.ToList();
        for (var i = cards.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }

        if (notFirst != null && cards.Count > 1 && cards[0].Id == notFirst.Id)
            (cards[0], cards[1]) = (cards[1], cards[0]);

        foreach (var card in cards)
            _pile.Enqueue(card);
    }
}
