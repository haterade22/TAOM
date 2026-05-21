using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CultureMarketplace.Domain;

namespace TAOM.Features.CultureMarketplace;

public class CultureItemPoolService : ICultureItemPoolService
{
    // Codex review 2026-05-20 (C2): LOTRAOM_horses.xml lines 231-330 declare Rohan
    // harnesses with culture="Culture.rohan" — but `rohan` is NOT a valid TAOM culture
    // ID (Rohan uses `vlandia` per CLAUDE.md cheatsheet). Without normalization those
    // items group into a `rohan` pool no town ever queries.
    private static readonly Dictionary<string, string> CultureAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["rohan"] = "vlandia",
        };

    private readonly IItemPoolAdapter _itemPool;
    private readonly ICultureMarketplaceConfigProvider _config;
    private readonly IModLogger _logger;

    private Dictionary<string, CultureItemPool> _pools;
    private int _totalItems;

    public CultureItemPoolService(
        IItemPoolAdapter itemPool,
        ICultureMarketplaceConfigProvider config,
        IModLogger logger)
    {
        _itemPool = itemPool;
        _config = config;
        _logger = logger;
    }

    public int CultureCount => _pools?.Count ?? 0;
    public int TotalItemCount => _totalItems;

    public void BuildPools()
    {
        if (_pools != null) return;

        var overrides = _config.GetOverridesByCulture();
        var routing = _config.GetItemRouting();
        var all = _itemPool.GetAllItems();

        var grouped = new Dictionary<string, List<ItemPoolEntry>>(StringComparer.OrdinalIgnoreCase);
        var attribHits = 0;
        var prefixHits = 0;
        var unresolved = 0;
        var routedItems = 0;

        for (var i = 0; i < all.Count; i++)
        {
            var item = all[i];

            // Item routing OVERRIDES attribute + prefix. The item appears ONLY in the listed
            // cultures' pools (e.g., a Warg tagged Culture.isengard but routed to Isengard +
            // Mordor + Gundabad + Dol Guldur via Routing config).
            //
            // Codex self-review 2026-05-20 (S2): dedup AFTER alias normalization. Two routing
            // entries that collide post-alias (e.g., `cultures="rohan,vlandia"` — both alias
            // to vlandia, or `cultures="mordor,mordor"` — author typo) previously added the
            // item to the same canonical pool twice, silently doubling its draw weight.
            if (routing.TryGetValue(item.ItemId, out var routed))
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var duplicates = 0;
                foreach (var cId in routed.Cultures)
                {
                    var routedTarget = ApplyCultureAlias(cId);
                    if (!seen.Add(routedTarget))
                    {
                        duplicates++;
                        continue;
                    }
                    AddToGroup(grouped, overrides, routedTarget, item.ItemId);
                }
                if (duplicates > 0)
                    _logger.LogWarning(
                        $"[CultureMarketplace] Routing entry for '{item.ItemId}' had {duplicates} duplicate culture(s) after alias normalization — duplicates ignored");
                routedItems++;
                continue;
            }

            var cultureId = ClassifyEffectiveCulture(item.CultureId, item.PrefixCultureId);
            if (string.IsNullOrEmpty(cultureId))
            {
                unresolved++;
                continue;
            }

            if (string.IsNullOrEmpty(item.CultureId) && !string.IsNullOrEmpty(item.PrefixCultureId))
                prefixHits++;
            else
                attribHits++;

            AddToGroup(grouped, overrides, cultureId, item.ItemId);
        }

        _pools = new Dictionary<string, CultureItemPool>(StringComparer.OrdinalIgnoreCase);
        _totalItems = 0;
        foreach (var kvp in grouped)
        {
            _pools[kvp.Key] = new CultureItemPool(kvp.Key, kvp.Value);
            _totalItems += kvp.Value.Count;
        }

        _logger.LogInfo(
            $"[CultureMarketplace] Pool built: {_pools.Count} cultures, {_totalItems} items " +
            $"(attribute={attribHits}, prefix-fallback={prefixHits}, routed={routedItems}, unresolved={unresolved})");

        // Per-culture diagnostic so an in-game tester can confirm which items each town will
        // see. Log sorted by descending pool size; first 4 item IDs per culture as a sanity
        // sample. One info-level call per culture — fires once at pool build, never per tick.
        foreach (var kvp in _pools.OrderByDescending(p => p.Value.Items.Count))
        {
            var sample = string.Join(", ", kvp.Value.Items.Take(4).Select(e => e.ItemId));
            _logger.LogInfo($"[CultureMarketplace]   {kvp.Key}: {kvp.Value.Items.Count} items — sample: {sample}");
        }
    }

    private static string ApplyCultureAlias(string cultureId) =>
        CultureAliases.TryGetValue(cultureId, out var canonical) ? canonical : cultureId;

    private static bool AddToGroup(
        Dictionary<string, List<ItemPoolEntry>> grouped,
        IReadOnlyDictionary<string, MarketplaceConfigOverride> overrides,
        string cultureId,
        string itemId)
    {
        if (overrides.TryGetValue(cultureId, out var ov) && ov.Blacklist.Contains(itemId))
            return false;

        var weight = 1f;
        if (ov != null && ov.WeightBoosts.TryGetValue(itemId, out var boostWeight))
            weight = boostWeight;

        if (!grouped.TryGetValue(cultureId, out var list))
        {
            list = new List<ItemPoolEntry>();
            grouped[cultureId] = list;
        }
        list.Add(new ItemPoolEntry(itemId, weight));
        return true;
    }

    public CultureItemPool GetPool(string cultureId)
    {
        if (_pools == null)
            throw new InvalidOperationException("CultureItemPoolService: BuildPools must be called before GetPool");
        if (string.IsNullOrEmpty(cultureId)) return null;
        return _pools.TryGetValue(cultureId, out var pool) ? pool : null;
    }

    /// <summary>
    /// Pure classification — attribute culture wins, then prefix fallback, then alias
    /// normalization. Returns null/empty if no signal. Extracted from BuildPools so the
    /// behavior's filter pass uses the SAME chain (no logic drift).
    /// </summary>
    public string ClassifyEffectiveCulture(string attributeCultureId, string prefixCultureId)
    {
        var cultureId = !string.IsNullOrEmpty(attributeCultureId)
            ? attributeCultureId
            : prefixCultureId;
        if (string.IsNullOrEmpty(cultureId)) return null;
        return ApplyCultureAlias(cultureId);
    }

    /// <summary>
    /// All RoutedItems whose Cultures list, after alias normalization, includes the given
    /// target. Iterates the entire routing dict each call but the dict is tiny (~4 entries
    /// today) and this fires once per town per daily tick.
    /// </summary>
    public IReadOnlyList<RoutedItem> GetRoutedItemsForCulture(string cultureId)
    {
        if (string.IsNullOrEmpty(cultureId)) return Array.Empty<RoutedItem>();
        var target = ApplyCultureAlias(cultureId);
        var routing = _config.GetItemRouting();
        var result = new List<RoutedItem>(routing.Count);
        foreach (var kvp in routing)
        {
            foreach (var rawCulture in kvp.Value.Cultures)
            {
                if (string.Equals(ApplyCultureAlias(rawCulture), target, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(kvp.Value);
                    break;
                }
            }
        }
        return result;
    }
}
