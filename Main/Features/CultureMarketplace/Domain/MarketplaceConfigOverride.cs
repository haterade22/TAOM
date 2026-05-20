using System.Collections.Generic;

namespace TAOM.Features.CultureMarketplace.Domain;

public sealed class MarketplaceConfigOverride
{
    public string CultureId { get; }
    public IReadOnlyCollection<string> Blacklist { get; }
    public IReadOnlyDictionary<string, float> WeightBoosts { get; }

    public MarketplaceConfigOverride(
        string cultureId,
        IReadOnlyCollection<string> blacklist,
        IReadOnlyDictionary<string, float> weightBoosts)
    {
        CultureId = cultureId;
        Blacklist = blacklist;
        WeightBoosts = weightBoosts;
    }
}
