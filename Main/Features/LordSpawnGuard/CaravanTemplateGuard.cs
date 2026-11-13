using System.Collections.Generic;

namespace TAOM.Features.LordSpawnGuard;

/// <summary>
/// Pure decision logic behind <c>Patch147_CaravanTemplateGuard</c> (#482).
///
/// Vanilla <c>CaravansCampaignBehavior.SpawnCaravan</c> (v1.5.6 decompile, lines 495-505) picks a
/// template with <c>GetRandomElementWithPredicate(x =&gt; x.ShipHulls.Count == 0 !=
/// hero.CurrentSettlement.HasPort)</c> and reads <c>.ShipHulls</c> on the result with no null check.
/// The helper returns null, not an exception, when the list is empty or when no element matches.
/// A culture whose caravan templates are all land templates therefore matches nothing while the
/// merchant sits in a port town, and the daily hero tick throws a NullReferenceException.
/// The predicate also reads <c>hero.CurrentSettlement</c> unguarded.
///
/// Which list vanilla reads (elite or normal) is a random roll inside the method, so this answers
/// "could the roll land on a list with no usable template"; the patch then suppresses only a
/// NullReferenceException thrown on a call this flagged.
/// </summary>
public static class CaravanTemplateGuard
{
    /// <summary>
    /// True when vanilla's template pick can return null for this merchant.
    /// </summary>
    /// <param name="hasCurrentSettlement">The hero is in a settlement (the predicate reads it).</param>
    /// <param name="settlementHasPort">That settlement has a port.</param>
    /// <param name="normalHullCounts">ShipHulls.Count per normal caravan template; null if the list is missing.</param>
    /// <param name="eliteHullCounts">ShipHulls.Count per elite caravan template; null if the list is missing.</param>
    public static bool CanFailToResolve(
        bool hasCurrentSettlement,
        bool settlementHasPort,
        IEnumerable<int> normalHullCounts,
        IEnumerable<int> eliteHullCounts)
    {
        if (!hasCurrentSettlement)
            return true;

        return !HasMatch(normalHullCounts, settlementHasPort)
            || !HasMatch(eliteHullCounts, settlementHasPort);
    }

    /// <summary>Mirrors vanilla's predicate: a land template matches away from a port, a naval one at a port.</summary>
    public static bool HasMatch(IEnumerable<int> hullCounts, bool settlementHasPort)
    {
        if (hullCounts == null)
            return false;

        foreach (var hulls in hullCounts)
        {
            if ((hulls == 0) != settlementHasPort)
                return true;
        }

        return false;
    }
}
