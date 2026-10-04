using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Adapters;
using TAOM.Features.CultureMarketplace.Domain;

namespace TAOM.Features.CultureMarketplace;

public sealed class CultureMarketplaceMaintenanceService : ICultureMarketplaceMaintenanceService
{
    private readonly ICultureItemPoolService _poolService;
    private readonly ITownRosterAdapter _townAdapter;

    // Per-culture routed sets, built on first use and kept for the process: the routing table is loaded
    // once (CultureMarketplaceConfigProvider.EnsureLoaded), so it never changes after the first daily
    // tick. A pool indexes its own ids (CultureItemPool.ContainsItem). Main thread only (daily ticks and
    // the new-game sweep). Not campaign state: no session reset needed.
    private readonly Dictionary<string, RoutedSets> _routed = new(StringComparer.Ordinal);

    private sealed class RoutedSets
    {
        public RoutedItem[] Guaranteed = Array.Empty<RoutedItem>();    // MinStock > 0, routed order
        public string[] GuaranteedIds = Array.Empty<string>();
        public HashSet<string>? Ids;                                   // null when nothing is routed here, as before
    }

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
        var routed = RoutedFor(cultureId);
        if (routed.Guaranteed.Length == 0) return 0;
        // One roster walk for every guaranteed item. Counting them all before any top-up gives the counts
        // the old per-item loop saw: adding one item changes no other item's count.
        var counts = _townAdapter.GetItemCounts(settlement, routed.GuaranteedIds);
        var totalAdded = 0;
        for (var i = 0; i < routed.Guaranteed.Length; i++)
        {
            var entry = routed.Guaranteed[i];
            var have = i < counts.Length ? counts[i] : 0;   // a failed read counted 0 before too
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
    /// (e.g., wargs in a mordor town), and so is anything this culture's own pool carries:
    /// the armour a culture with none of its own draws from its armour_from donor belongs
    /// there too. Items with no culture signal (vanilla universals, trade goods, base armour)
    /// are left alone. Capped at removalCap. Callers build the pools first.
    /// </summary>
    public int FilterForeignCultureItems(Settlement settlement, string cultureId, int removalCap)
    {
        if (string.IsNullOrEmpty(cultureId)) return 0;
        if (removalCap <= 0) return 0;

        var snapshot = _townAdapter.EnumerateRoster(settlement);
        if (snapshot.Count == 0) return 0;

        var routedIdsHere = RoutedFor(cultureId).Ids;
        var pool = _poolService.GetPool(cultureId);

        var removed = 0;
        for (var i = 0; i < snapshot.Count && removed < removalCap; i++)
        {
            var row = snapshot[i];
            if (routedIdsHere != null && routedIdsHere.Contains(row.ItemId)) continue;
            if (pool != null && pool.ContainsItem(row.ItemId)) continue;

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

    private RoutedSets RoutedFor(string cultureId)
    {
        if (_routed.TryGetValue(cultureId, out var sets)) return sets;
        var routed = _poolService.GetRoutedItemsForCulture(cultureId);
        sets = new RoutedSets();
        if (routed.Count > 0)
        {
            sets.Ids = new HashSet<string>(StringComparer.Ordinal);
            var guaranteed = new List<RoutedItem>();
            for (var i = 0; i < routed.Count; i++)
            {
                sets.Ids.Add(routed[i].ItemId);
                if (routed[i].MinStock > 0) guaranteed.Add(routed[i]);
            }
            sets.Guaranteed = guaranteed.ToArray();
            sets.GuaranteedIds = sets.Guaranteed.Select(e => e.ItemId).ToArray();
        }
        _routed[cultureId] = sets;
        return sets;
    }
}
