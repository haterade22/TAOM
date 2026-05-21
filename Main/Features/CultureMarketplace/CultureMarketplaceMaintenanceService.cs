using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Adapters;

namespace TAOM.Features.CultureMarketplace;

public sealed class CultureMarketplaceMaintenanceService : ICultureMarketplaceMaintenanceService
{
    private readonly ICultureItemPoolService _poolService;
    private readonly ITownRosterAdapter _townAdapter;

    public CultureMarketplaceMaintenanceService(
        ICultureItemPoolService poolService,
        ITownRosterAdapter townAdapter)
    {
        _poolService = poolService;
        _townAdapter = townAdapter;
    }

    /// <summary>
    /// For each routed item whose Cultures list includes this town's culture and whose
    /// MinStock &gt; 0, ensure the town's roster contains at least MinStock units. Bypasses
    /// PerTownTotalRosterCap by design — lore-essential items must always be available.
    /// </summary>
    public int EnsureGuaranteedStock(Settlement settlement, string cultureId)
    {
        if (string.IsNullOrEmpty(cultureId)) return 0;
        var routed = _poolService.GetRoutedItemsForCulture(cultureId);
        if (routed.Count == 0) return 0;
        var totalAdded = 0;
        for (var i = 0; i < routed.Count; i++)
        {
            var entry = routed[i];
            if (entry.MinStock <= 0) continue;
            var have = _townAdapter.GetItemCount(settlement, entry.ItemId);
            if (have >= entry.MinStock) continue;
            var need = entry.MinStock - have;
            if (_townAdapter.AddItem(settlement, entry.ItemId, need))
                totalAdded += need;
        }
        return totalAdded;
    }

    /// <summary>
    /// Remove items whose effective culture (attribute → prefix → alias) does not match
    /// the town's current owner culture. Routed items targeted at this culture are kept
    /// (e.g., wargs in a mordor town). Items with no culture signal (vanilla universals,
    /// trade goods, base armour) are left alone. Capped at removalCap.
    /// </summary>
    public int FilterForeignCultureItems(Settlement settlement, string cultureId, int removalCap)
    {
        if (string.IsNullOrEmpty(cultureId)) return 0;
        if (removalCap <= 0) return 0;

        var snapshot = _townAdapter.EnumerateRoster(settlement);
        if (snapshot.Count == 0) return 0;

        var routedHere = _poolService.GetRoutedItemsForCulture(cultureId);
        HashSet<string> routedIdsHere = null;
        if (routedHere.Count > 0)
        {
            routedIdsHere = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < routedHere.Count; i++)
                routedIdsHere.Add(routedHere[i].ItemId);
        }

        var removed = 0;
        for (var i = 0; i < snapshot.Count && removed < removalCap; i++)
        {
            var row = snapshot[i];
            if (routedIdsHere != null && routedIdsHere.Contains(row.ItemId)) continue;

            // Effective culture = attribute alias (prefix-only items lack a Culture
            // attribute in the roster snapshot; treat them as universals → keep).
            var effective = _poolService.ClassifyEffectiveCulture(row.CultureStringId, prefixCultureId: null);
            if (string.IsNullOrEmpty(effective)) continue;   // vanilla universal — leave alone
            if (string.Equals(effective, cultureId, StringComparison.OrdinalIgnoreCase)) continue;

            if (_townAdapter.RemoveItem(settlement, row.ItemId, row.Count))
                removed++;
        }
        return removed;
    }
}
