using System;
using System.Collections.Generic;

namespace TAOM.Features.CultureMarketplace.Domain;

public sealed class CultureItemPool
{
    // The pool's item ids, indexed with the weights in the one constructor pass: a pool never changes
    // after it is built, so the daily foreign-item filter asks the pool instead of re-reading its items.
    private readonly HashSet<string> _ids = new(StringComparer.Ordinal);

    public string CultureId { get; }
    public IReadOnlyList<ItemPoolEntry> Items { get; }
    public float TotalWeight { get; }

    public CultureItemPool(string cultureId, IReadOnlyList<ItemPoolEntry> items)
    {
        CultureId = cultureId;
        Items = items;

        var sum = 0f;
        for (var i = 0; i < items.Count; i++)
        {
            var entry = items[i];
            sum += entry.Weight;
            _ids.Add(entry.ItemId);
        }
        TotalWeight = sum;
    }

    /// <summary>True when an entry of this pool carries this item id (ordinal). False for an empty pool.</summary>
    public bool ContainsItem(string itemId) => _ids.Contains(itemId);
}
