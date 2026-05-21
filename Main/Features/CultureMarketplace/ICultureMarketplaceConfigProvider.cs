using System.Collections.Generic;
using TAOM.Features.CultureMarketplace.Domain;

namespace TAOM.Features.CultureMarketplace;

public interface ICultureMarketplaceConfigProvider
{
    IReadOnlyDictionary<string, MarketplaceConfigOverride> GetOverridesByCulture();

    // Multi-culture routing: item-id → RoutedItem (cultures + optional min_stock). Items
    // listed here IGNORE their `Culture.X` attribute and ID-prefix fallback and appear
    // ONLY in the listed cultures' pools. min_stock > 0 makes the item a guaranteed
    // floor — daily tick tops it up if the town's roster count drops below the floor.
    //
    // Use case: warg mounts are tagged `Culture.isengard` but lore-correctly belong in
    // Isengard, Mordor, Gundabad, and Dol Guldur (the four "evil" cultures), and the
    // user wants ≥1 of each variant always available in those markets.
    IReadOnlyDictionary<string, RoutedItem> GetItemRouting();
}
