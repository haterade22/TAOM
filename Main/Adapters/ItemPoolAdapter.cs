using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;
using TAOM.Features.CultureMarketplace.Domain;

namespace TAOM.Adapters;

public class ItemPoolAdapter : IItemPoolAdapter
{
    private static readonly (string Prefix, string CultureId)[] PrefixMap =
    {
        ("sk_gd_",        "gondor"),
        ("sm_mordor_",    "mordor"),
        ("wm_isengard_",  "isengard"),
        ("sk_uruk_hai_",  "isengard"),
        ("sm_uruk_",      "isengard"),
        ("sk_dwarf_erebor_", "erebor"),
        ("mkwd_",         "mirkwood"),
        ("mirkwood_",     "mirkwood"),
        ("rivendell_",    "rivendell"),
        ("morannon_",     "mordor"),
        ("morgul_",       "mordor"),
        ("haradrim",      "aserai"),
        ("wm_harad_",     "aserai"),
        ("easterling",    "khuzait"),
        ("dunland_",      "empire"),
        ("rohan_",        "vlandia"),
        ("whiterun_",     "vlandia"),
        ("ithilien_",     "gondor"),
        ("gondor_",       "gondor"),
        ("faramir_",      "gondor"),
        ("imrahil_",      "gondor"),
        ("legolas_",      "mirkwood"),
        ("thranduil_",    "mirkwood"),
        ("theoden_",      "vlandia"),
        ("witchking_",    "mordor"),
        ("nazgul_",       "mordor"),
        ("sauron_",       "mordor"),
        ("anduril",       "gondor"),
        ("strider_",      "gondor"),
        ("cts_rohan_",    "vlandia"),
    };

    private readonly IModLogger _logger;
    private IReadOnlyList<ItemPoolItem> _cache;

    public ItemPoolAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<ItemPoolItem> GetAllItems()
    {
        if (_cache != null)
            return _cache;

        var items = new List<ItemPoolItem>();
        try
        {
            var all = MBObjectManager.Instance?.GetObjectTypeList<ItemObject>();
            if (all == null)
            {
                _logger.LogWarning("ItemPoolAdapter: MBObjectManager returned no ItemObject list");
                _cache = items;
                return _cache;
            }

            foreach (var item in all)
            {
                if (item == null) continue;
                if (!IsMarketCandidate(item.StringId, item.NotMerchandise)) continue;

                var attribCulture = item.Culture?.StringId;
                var prefixCulture = ResolveByPrefix(item.StringId);
                items.Add(new ItemPoolItem(item.StringId, attribCulture, prefixCulture));
            }

            _logger.LogInfo($"[CultureMarketplace] ItemPoolAdapter cached {items.Count} ItemObjects from MBObjectManager");
        }
        catch (Exception ex)
        {
            _logger.LogError($"ItemPoolAdapter: scan failed: {ex.Message}");
        }

        _cache = items;
        return _cache;
    }

    public bool ItemExists(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return false;
        try
        {
            return MBObjectManager.Instance?.GetObject<ItemObject>(itemId) != null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"ItemPoolAdapter: ItemExists('{itemId}') failed: {ex.Message}");
            return false;
        }
    }

    // An item the XML marks is_merchandise="false" (v1.4.8 ItemObject.NotMerchandise, ItemObject.cs:154, set
    // at :428-432 and :498-502) is hero-only or troop-only kit; vanilla's own inflows skip it, and so does
    // the culture-market pool (the 1.5.x line does this through its armour acquisition gate, bc39f6e4d). The
    // routed guaranteed-stock pass adds items by id and does not ask this.
    internal static bool IsMarketCandidate(string? stringId, bool notMerchandise) =>
        !string.IsNullOrEmpty(stringId) && !notMerchandise;

    private static string ResolveByPrefix(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        for (var i = 0; i < PrefixMap.Length; i++)
        {
            if (itemId.StartsWith(PrefixMap[i].Prefix, StringComparison.OrdinalIgnoreCase))
                return PrefixMap[i].CultureId;
        }
        return null;
    }
}
