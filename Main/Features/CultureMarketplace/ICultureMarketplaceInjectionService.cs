using System;
using System.Collections.Generic;

namespace TAOM.Features.CultureMarketplace;

public interface ICultureMarketplaceInjectionService
{
    /// <summary>
    /// Draws the day's picks for a town from its culture pool. <paramref name="isEligible"/> narrows the pool
    /// for that town (the armour acquisition stock gate, <see cref="IMarketplaceStockGate.ForTown"/>); null
    /// draws from the whole pool.
    /// </summary>
    IReadOnlyList<string> SelectItems(string cultureId, int currentRosterCount, Random rng, Func<string, bool> isEligible = null);
}
