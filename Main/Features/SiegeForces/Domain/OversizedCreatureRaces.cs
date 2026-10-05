using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TAOM.Core.Domain;
using TAOM.Features.TrollBruteForce;

namespace TAOM.Features.SiegeForces.Domain;

/// <summary>
/// The one definition of an oversized creature race: a troop too big for a siege ladder, tower or wall stair. The
/// siege troop picker starts these troops unticked, and the creature siege role keys its siege behaviour on the same
/// set, so the two cannot drift apart. The race name equals the creature's base monster id, so the names come from the
/// <see cref="TrollBruteForceConfig"/> constants.
///
/// The ids are derived once, from the names, and a caller compares a troop's race int against them. The derivation
/// validates before it looks anything up (csharp-architecture.md "Lookup Functions With Fallbacks"):
/// <c>IRaceManager.GetRaceIdFromName</c> answers 0 for a name it does not know, so trusting that fallback would treat
/// every human as a creature.
/// </summary>
public static class OversizedCreatureRaces
{
    private static readonly ReadOnlyCollection<string> Names = new(new[]
    {
        TrollBruteForceConfig.CaveTrollMonsterId,
        TrollBruteForceConfig.HillTrollMonsterId,
    });

    /// <summary>The oversized race names, read-only: <c>cave_troll</c> and <c>hill_troll</c>.</summary>
    public static IReadOnlyList<string> RaceNames => Names;

    /// <summary>
    /// The distinct race ids of <see cref="RaceNames"/>, in name order. A name the race table does not hold is skipped
    /// without a lookup, so an absent race never resolves to the human fallback. Empty when none resolve. A caller may
    /// keep and edit a non-empty result: each call builds its own array (an empty result is the shared empty array,
    /// which nothing can edit).
    /// </summary>
    public static int[] ResolveRaceIds(IRaceManager raceManager)
    {
        if (raceManager == null)
            return Array.Empty<int>();

        var ids = new List<int>(Names.Count);
        foreach (var name in Names)
        {
            if (!raceManager.IsValidRaceName(name))
                continue;

            var id = raceManager.GetRaceIdFromName(name);
            if (!ids.Contains(id))
                ids.Add(id);
        }

        return ids.Count == 0 ? Array.Empty<int>() : ids.ToArray();
    }
}
