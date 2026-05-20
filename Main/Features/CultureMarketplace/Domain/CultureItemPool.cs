using System.Collections.Generic;

namespace TAOM.Features.CultureMarketplace.Domain;

public sealed class CultureItemPool
{
    public string CultureId { get; }
    public IReadOnlyList<ItemPoolEntry> Items { get; }
    public float TotalWeight { get; }

    public CultureItemPool(string cultureId, IReadOnlyList<ItemPoolEntry> items)
    {
        CultureId = cultureId;
        Items = items;

        var sum = 0f;
        for (var i = 0; i < items.Count; i++)
            sum += items[i].Weight;
        TotalWeight = sum;
    }
}
