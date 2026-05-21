using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Adapters;

public interface ITownRosterAdapter
{
    string GetCurrentCultureId(Settlement settlement);
    string GetSettlementId(Settlement settlement);
    int GetRosterDistinctItemCount(Settlement settlement);
    bool AddItem(Settlement settlement, string itemId, int count);

    /// <summary>Count of a specific itemId across the roster (sum across all stacks of
    /// that item). 0 if absent or settlement is null.</summary>
    int GetItemCount(Settlement settlement, string itemId);

    /// <summary>Remove `count` of itemId from the roster. Returns true if any units were
    /// removed (positive count, the item existed). Vanilla `AddToCounts` handles the
    /// internal accounting and emits OnInventoryUpdated for the market price model.</summary>
    bool RemoveItem(Settlement settlement, string itemId, int count);

    /// <summary>Snapshot all distinct roster entries for the settlement. The snapshot
    /// carries item-id + culture-string-id + count, keeping TaleWorlds types out of the
    /// caller (ADR-007). Returns empty list for null/empty settlements.</summary>
    IReadOnlyList<RosterItemSnapshot> EnumerateRoster(Settlement settlement);
}
