using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>
/// Read-only view over the engine's MBEquipmentRoster catalog (ADR-007 boundary
/// for the sealed MBEquipmentRoster/ItemObject pair). Used by the enlistment
/// equipment pipeline to probe the fallback chain, enumerate a roster's battle-set
/// item ids, and price items for the discharge payoff.
/// </summary>
public interface IEquipmentRosterCatalogAdapter
{
    bool RosterExists(string rosterId);

    /// <summary>Item StringIds of every filled slot in the roster's FIRST battle set;
    /// empty list when the roster or its battle set is missing.</summary>
    IReadOnlyList<string> GetBattleSetItemIds(string rosterId);

    /// <summary>ItemObject.Value (base gold value), or 0 when the item doesn't resolve.</summary>
    int GetItemValue(string itemId);
}
