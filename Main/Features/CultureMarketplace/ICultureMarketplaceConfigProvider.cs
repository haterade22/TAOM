using System.Collections.Generic;
using TAOM.Features.CultureMarketplace.Domain;

namespace TAOM.Features.CultureMarketplace;

public interface ICultureMarketplaceConfigProvider
{
    IReadOnlyDictionary<string, MarketplaceConfigOverride> GetOverridesByCulture();

    // Multi-culture routing: item-id → list of culture-ids. Items listed here IGNORE their
    // `Culture.X` attribute and ID-prefix fallback and appear ONLY in the listed cultures'
    // pools. Use case: Warg mounts are tagged `Culture.isengard` but should appear in
    // Isengard, Mordor, Gundabad, and Dol Guldur markets (the four "evil" cultures).
    IReadOnlyDictionary<string, IReadOnlyList<string>> GetItemRouting();
}
