using System.Collections.Generic;

namespace TAOM.Features.CultureMarketplace.Domain;

/// <summary>
/// One entry in the &lt;Routing&gt; section of culture_marketplace_config.xml.
/// Represents an item that should appear in multiple culture pools (overriding its
/// `culture=` attribute and ID-prefix fallback), optionally with a guaranteed minimum
/// stock kept in those cultures' town markets.
/// </summary>
public sealed class RoutedItem
{
    public string ItemId { get; }
    public IReadOnlyList<string> Cultures { get; }
    public int MinStock { get; }

    public RoutedItem(string itemId, IReadOnlyList<string> cultures, int minStock)
    {
        ItemId = itemId;
        Cultures = cultures;
        MinStock = minStock;
    }
}
