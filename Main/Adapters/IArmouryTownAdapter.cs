using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>
/// The settlement facts the armour acquisition armoury needs (docs/features/armour-acquisition.md), keyed by
/// settlement id so no Settlement reaches a service (ADR-007). An id that resolves to nothing reads as a
/// settlement with no Barracks, no culture and no town.
/// </summary>
public interface IArmouryTownAdapter
{
    /// <summary>The level of the settlement's Barracks (town or castle), 0 when it has none.</summary>
    int GetBarracksLevel(string settlementId);

    /// <summary>The settlement's display name, or the id itself when it resolves to nothing.</summary>
    string GetName(string settlementId);

    /// <summary>The settlement's own culture StringId, or null.</summary>
    string? GetCultureId(string settlementId);

    bool IsTown(string settlementId);

    /// <summary>The id of the settlement the player is in, or null.</summary>
    string? CurrentSettlementId { get; }

    /// <summary>The id of every town on the map.</summary>
    IReadOnlyList<string> AllTownIds();

    /// <summary>The current campaign day, floored; 0 when the clock is unreadable or not finite.</summary>
    int Today { get; }
}
