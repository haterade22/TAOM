using System.Collections.Generic;
using TAOM.Features.CultureMarketplace.Domain;

namespace TAOM.Adapters;

public interface IItemPoolAdapter
{
    IReadOnlyList<ItemPoolItem> GetAllItems();
    bool ItemExists(string itemId);

    /// <summary>True for arrows, bolts and bullets: a consumable the player refills from loot.</summary>
    bool IsAmmunition(string itemId);
}
