using System.Collections.Generic;
using TAOM.Features.CultureMarketplace.Domain;

namespace TAOM.Adapters;

public interface IItemPoolAdapter
{
    IReadOnlyList<ItemPoolItem> GetAllItems();
    bool ItemExists(string itemId);
}
