using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Adapters;

public interface ITownRosterAdapter
{
    string GetCurrentCultureId(Settlement settlement);
    string GetSettlementId(Settlement settlement);
    int GetRosterDistinctItemCount(Settlement settlement);
    bool AddItem(Settlement settlement, string itemId, int count);

    /// <summary>For each id, the sum over every stack of that item, whatever its modifier, from ONE walk of
    /// the roster. 0 for a null or empty id, an id the object manager does not know, or a null settlement.
    /// The array has one entry per id, in order; empty for null ids. A failed walk logs one ERROR line
    /// naming every id and returns zeroes.</summary>
    int[] GetItemCounts(Settlement settlement, IReadOnlyList<string> itemIds);

    /// <summary>Remove `count` of itemId from the roster. Returns true if any units were
    /// removed (positive count, the item existed). Vanilla `AddToCounts` handles the
    /// internal accounting and emits OnInventoryUpdated for the market price model.</summary>
    bool RemoveItem(Settlement settlement, string itemId, int count);

    /// <summary>Snapshot all distinct roster entries for the settlement. The snapshot
    /// carries item-id + culture-string-id + count, keeping TaleWorlds types out of the
    /// caller (ADR-007). Returns empty list for null/empty settlements.</summary>
    IReadOnlyList<RosterItemSnapshot> EnumerateRoster(Settlement settlement);

    /// <summary><see cref="EnumerateRoster(Settlement)"/> for the settlement with this id, so a service
    /// never holds a Settlement (ADR-007). Empty when the id resolves to nothing.</summary>
    IReadOnlyList<RosterItemSnapshot> EnumerateRosterById(string settlementId);

    /// <summary><see cref="RemoveItem(Settlement, string, int)"/> for the settlement with this id. False
    /// when the id resolves to nothing.</summary>
    bool RemoveItemById(string settlementId, string itemId, int count);
}
