using System.Collections.Generic;

namespace TAOM.Features.CultureMarketplace.Domain;

public sealed class MarketplaceConfigOverride
{
    public string CultureId { get; }
    public IReadOnlyCollection<string> Blacklist { get; }
    public IReadOnlyDictionary<string, float> WeightBoosts { get; }

    /// <summary>
    /// The culture whose character armour this culture's pool also carries (when it has no
    /// <see cref="Stock"/> rows), and whose lord kit and material it uses; or null.
    /// </summary>
    public string ArmourFrom { get; }

    /// <summary>What this culture's markets carry beyond its own items, in file order (never null).</summary>
    public IReadOnlyList<StockRule> Stock { get; }

    public MarketplaceConfigOverride(
        string cultureId,
        IReadOnlyCollection<string> blacklist,
        IReadOnlyDictionary<string, float> weightBoosts,
        string armourFrom = null,
        IReadOnlyList<StockRule> stock = null)
    {
        CultureId = cultureId;
        Blacklist = blacklist;
        WeightBoosts = weightBoosts;
        ArmourFrom = armourFrom;
        Stock = stock ?? new StockRule[0];
    }
}
