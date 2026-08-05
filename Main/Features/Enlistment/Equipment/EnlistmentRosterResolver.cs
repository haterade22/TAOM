using System;

namespace TAOM.Features.Enlistment.Equipment;

/// <summary>
/// Pure roster resolution with the locked fallback chain:
/// exact <c>enlist_{culture}_{rank}</c> → same culture, descending lower ranks →
/// <c>enlist_default_{rank}</c> (the REQUESTED rank, not a lower one) → null.
/// Existence is delegated to the caller-supplied probe so the chain is fully
/// unit-testable without the engine (the service passes
/// IEquipmentRosterCatalogAdapter.RosterExists).
/// </summary>
public static class EnlistmentRosterResolver
{
    /// <returns>The first existing roster id along the chain, or null when nothing exists.</returns>
    public static string Resolve(string cultureId, EnlistmentRank rank, Func<string, bool> rosterExists)
    {
        if (rosterExists == null)
            throw new ArgumentNullException(nameof(rosterExists));

        if (!string.IsNullOrEmpty(cultureId))
        {
            for (var r = (int)rank; r >= (int)EnlistmentRank.Recruit; r--)
            {
                var id = EnlistmentRosterIds.Build(cultureId, (EnlistmentRank)r);
                if (rosterExists(id))
                    return id;
            }
        }

        var defaultId = EnlistmentRosterIds.BuildDefault(rank);
        return rosterExists(defaultId) ? defaultId : null;
    }
}
