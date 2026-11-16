using System;
using System.Collections.Generic;
using TAOM.Features.HeroAbilities.Domain;

namespace TAOM.Features.HeroAbilities.Hooks;

/// <summary>
/// One mission's ranged hero tallies (#1361), one for each hero body, by reference. They outlive the bodies on purpose,
/// for the end-of-mission lines (HeroRangedChecks.WriteEnd). No engine type. Main thread only.
/// </summary>
internal sealed class HeroRangedTallies<TBody> where TBody : class
{
    private readonly Dictionary<TBody, HeroRangedTally> _byBody = new Dictionary<TBody, HeroRangedTally>();
    private readonly Func<TBody, string> _heroIdOf;

    internal HeroRangedTallies(Func<TBody, string> heroIdOf)
    {
        _heroIdOf = heroIdOf;
    }

    internal IEnumerable<HeroRangedTally> All => _byBody.Values;

    /// <summary>The tally of this body. A new one takes the kit's switches (no kit: both off) and the next serial among
    /// this mission's bodies of the same hero.</summary>
    internal HeroRangedTally Of(TBody body, HeroRangedConfig? kit)
    {
        if (_byBody.TryGetValue(body, out var tally))
            return tally;
        var heroId = _heroIdOf(body);
        var serial = 1;
        foreach (var other in _byBody.Values)
            if (other.HeroId == heroId)
                serial++;
        tally = new HeroRangedTally(heroId, serial) { ZeroAimError = kit?.ZeroAimError == true, PierceShields = kit?.PierceShields == true };
        _byBody[body] = tally;
        return tally;
    }

    /// <summary>A removal names no shooter, so every tally forgets the index.</summary>
    internal void MissileRemoved(int index)
    {
        foreach (var tally in _byBody.Values)
            tally.MissileRemoved(index);
    }

    internal void Clear() => _byBody.Clear();
}
