using System.Collections.Generic;
using TAOM.Features.CultureMarketplace.Domain;

namespace TAOM.Features.CultureMarketplace;

public interface ICultureItemPoolService
{
    void BuildPools();
    CultureItemPool GetPool(string cultureId);
    int CultureCount { get; }
    int TotalItemCount { get; }

    /// <summary>
    /// Classify an item's "effective culture" using the same chain BuildPools uses:
    /// attribute culture wins, then ID-prefix fallback, then alias normalization.
    /// Returns null if the item has no recognized culture signal (vanilla universal).
    /// Inputs come from <see cref="IItemPoolAdapter"/> (attribute culture +
    /// prefix-resolved culture computed at item-pool load).
    /// </summary>
    string ClassifyEffectiveCulture(string attributeCultureId, string prefixCultureId);

    /// <summary>
    /// All <see cref="RoutedItem"/>s whose Cultures list includes the given target.
    /// Used by the behavior's guaranteed-stock and filter passes — the filter must
    /// know which routed items are "allowed" in this culture even if their primary
    /// culture is different (wargs in mordor town, etc.).
    /// </summary>
    IReadOnlyList<RoutedItem> GetRoutedItemsForCulture(string cultureId);
}
