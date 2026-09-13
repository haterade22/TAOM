using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.Library;

namespace TAOM.Features.SupplyLines;

/// <summary>A good at a source with its name pre-folded once, so a keystroke only does IndexOf.</summary>
public sealed class SupplyGoodsCatalogueItem
{
    public SupplyLineItem Item;

    /// <summary>Trimmed, diacritics stripped; the name, or the id when the name is empty.</summary>
    public string SearchName;
}

/// <summary>One orderable source and the goods it stocks, as the screen catalogued them. Pure data.</summary>
public sealed class SupplyGoodsCatalogueEntry
{
    public SupplySourceInfo Source;

    /// <summary>Sanitized map distance (finite, non-negative), the row's own value.</summary>
    public float Distance;

    public IReadOnlyList<SupplyGoodsCatalogueItem> Goods;

    /// <summary>
    /// Folds the names once and drops what a search could never return (no item, no stock, no
    /// name and no id), so the per-keystroke loop stays a flat IndexOf over ~10k pairs.
    /// </summary>
    public static SupplyGoodsCatalogueEntry Create(SupplySourceInfo source, float distance, IReadOnlyList<SupplyLineItem>? goods)
    {
        var items = new List<SupplyGoodsCatalogueItem>();
        if (goods != null)
        {
            foreach (var item in goods)
            {
                if (item == null || item.Available <= 0)
                    continue;
                var searchName = SupplyGoodsSearch.Normalize(string.IsNullOrEmpty(item.Name) ? item.Id : item.Name);
                if (searchName.Length == 0)
                    continue;
                items.Add(new SupplyGoodsCatalogueItem { Item = item, SearchName = searchName });
            }
        }
        return new SupplyGoodsCatalogueEntry { Source = source, Distance = distance, Goods = items };
    }
}

/// <summary>One search result: a good at a source. Pure data.</summary>
public sealed class SupplyGoodsHit
{
    public SupplySourceInfo Source;

    public float Distance;

    public SupplyLineItem Item;
}

/// <summary>
/// The cross-market goods search: a pure function over the catalogue the order screen builds from
/// its orderable sources. Matching is the encyclopedia's rule (diacritics stripped, case ignored,
/// substring); vanilla's inventory filter is case-sensitive by accident and is not copied. Results
/// come nearest first, then cheapest, so the top row is the natural place to order from; the cap
/// keeps the row list sane on a map with close to a thousand settlements and the caller gets the
/// uncapped total to say so.
/// </summary>
public static class SupplyGoodsSearch
{
    /// <summary>A one-letter query would match most of the map; two is the floor. One ideograph
    /// carries a word, so a query opening with an Asian character qualifies at one (vanilla's
    /// encyclopedia rule).</summary>
    public const int MinQueryLength = 2;

    /// <summary>Rows built per search; the total is still reported so the status line can say "of N".</summary>
    public const int MaxHits = 60;

    /// <summary>Trimmed, diacritics removed; never null.</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        return StringHelpers.RemoveDiacritics(text!.Trim());
    }

    public static bool IsActive(string? query)
    {
        var needle = Normalize(query);
        return needle.Length >= RequiredLength(needle);
    }

    public static List<SupplyGoodsHit> Search(
        IReadOnlyList<SupplyGoodsCatalogueEntry>? catalogue, string? query, out int totalMatches)
    {
        totalMatches = 0;
        var needle = Normalize(query);
        if (catalogue == null || needle.Length < RequiredLength(needle))
            return new List<SupplyGoodsHit>();

        var matches = new List<SupplyGoodsHit>();
        foreach (var entry in catalogue)
        {
            if (entry?.Goods == null)
                continue;
            foreach (var good in entry.Goods)
            {
                if (good?.Item == null || string.IsNullOrEmpty(good.SearchName))
                    continue;
                if (good.SearchName.IndexOf(needle, StringComparison.InvariantCultureIgnoreCase) < 0)
                    continue;
                matches.Add(new SupplyGoodsHit { Source = entry.Source, Distance = entry.Distance, Item = good.Item });
            }
        }

        totalMatches = matches.Count;
        // OrderBy is stable, so equal keys keep catalogue order and a rebuild never reshuffles rows.
        return matches
            .OrderBy(h => h.Distance)
            .ThenBy(h => h.Item.UnitPrice)
            .ThenBy(h => h.Item.Name ?? string.Empty, StringComparer.Ordinal)
            .Take(MaxHits)
            .ToList();
    }

    private static int RequiredLength(string needle)
        => needle.Length > 0 && Common.IsCharAsian(needle[0]) ? 1 : MinQueryLength;
}
