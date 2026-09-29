using System.Collections.Generic;

namespace TAOM.Features.CultureMarketplace.Domain;

public sealed class MarketplaceConfigOverride
{
    public string CultureId { get; }
    public IReadOnlyCollection<string> Blacklist { get; }
    public IReadOnlyDictionary<string, float> WeightBoosts { get; }

    /// <summary>The culture whose character armour this culture's pool also carries, or null.</summary>
    public string ArmourFrom { get; }

    public MarketplaceConfigOverride(
        string cultureId,
        IReadOnlyCollection<string> blacklist,
        IReadOnlyDictionary<string, float> weightBoosts,
        string armourFrom = null)
    {
        CultureId = cultureId;
        Blacklist = blacklist;
        WeightBoosts = weightBoosts;
        ArmourFrom = armourFrom;
    }
}
