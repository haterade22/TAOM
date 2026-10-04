using System.Collections.Generic;
using TAOM.Core.Collections;
using TAOM.Core.Validation;

namespace TAOM.Features.RaceAbilities;

// Who wears an active ability's outline. Each pass offers every soldier who should glow, with his colour and his
// squared distance to the camera; Settle keeps the nearest up to the cap and lists them all to paint again (an
// equipment rebuild strips the outline, so a pass repaints rather than trusts it), then lists everyone lit
// before who dropped out, to clear. Main thread only. Keys are compared by identity (#592).
public sealed class RaceAbilityGlowLedger<TKey> where TKey : class
{
    private readonly List<(TKey Key, uint Color, float DistanceSquared)> _offers = new List<(TKey Key, uint Color, float DistanceSquared)>();
    private HashSet<TKey> _lit = new HashSet<TKey>(ReferenceIdentity.Instance);
    private HashSet<TKey> _picked = new HashSet<TKey>(ReferenceIdentity.Instance);

    public int LitCount => _lit.Count;

    // A distance that is not a finite square never lights: a broken camera or position outlines no one.
    public void Offer(TKey key, uint color, float distanceSquared)
    {
        if (FiniteFloatValidator.IsFiniteAtLeast(distanceSquared, 0f))
            _offers.Add((key, color, distanceSquared));
    }

    public void Settle(int max, List<(TKey Key, uint Color)> paint, List<TKey> clear)
    {
        _offers.Sort(NearestFirst.Instance);
        for (var i = 0; i < _offers.Count && _picked.Count < max; i++)
            if (_picked.Add(_offers[i].Key))
                paint.Add((_offers[i].Key, _offers[i].Color));
        foreach (var key in _lit)
            if (!_picked.Contains(key))
                clear.Add(key);

        var previous = _lit;
        _lit = _picked;
        _picked = previous;
        _picked.Clear();
        _offers.Clear();
    }

    // A soldier gone from the battle: forgets him, and says whether he was lit so the caller clears him now.
    public bool Forget(TKey key) => _lit.Remove(key);

    // Mission end: drops every reference without listing anyone to clear.
    public void Clear()
    {
        _offers.Clear();
        _lit.Clear();
        _picked.Clear();
    }

    private sealed class NearestFirst : IComparer<(TKey Key, uint Color, float DistanceSquared)>
    {
        public static readonly NearestFirst Instance = new NearestFirst();

        public int Compare((TKey Key, uint Color, float DistanceSquared) x, (TKey Key, uint Color, float DistanceSquared) y) =>
            x.DistanceSquared.CompareTo(y.DistanceSquared);
    }
}
