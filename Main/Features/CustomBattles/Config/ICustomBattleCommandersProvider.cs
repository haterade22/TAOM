using System.Collections.Generic;

namespace TAOM.Features.CustomBattles.Config;

/// <summary>
/// Provides the curated, validated, ordered commander list for a Custom Battle faction.
/// <see cref="HasCuratedEntry"/> is the validate-before-lookup gate the service branches on:
/// a faction with no curated entry falls back to the default alphabetical top-N behavior.
/// </summary>
public interface ICustomBattleCommandersProvider
{
    /// <summary>True iff the faction has a non-empty curated list after validation.</summary>
    bool HasCuratedEntry(string factionId);

    /// <summary>
    /// The curated, ordered, de-duplicated lord ids for the faction, or an empty list when none.
    /// Ids are NOT resolved here (that needs the live engine) — unresolvable ids are warned and
    /// skipped at resolution time in <c>SideCommanderFilter</c>.
    /// </summary>
    IReadOnlyList<string> GetCuratedCommanderIds(string factionId);
}
