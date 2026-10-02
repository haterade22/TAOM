using System;

namespace TAOM.Features.CultureMarketplace;

/// <summary>
/// Which pool items a town's market may be given. CultureMarketplace is the one TAOM inflow the engine's
/// NotMerchandise flag does not reach, so the armour acquisition gate (docs/features/armour-acquisition.md)
/// answers here, per town: a heavy, elite or lord piece only where the town's armoury level allows it, never
/// a named piece, never an item its XML marked non-merchandise. With that feature off, every item its XML
/// lets be merchandise qualifies.
/// </summary>
public interface IMarketplaceStockGate
{
    /// <summary>The eligibility test for one town's market today, by settlement id.</summary>
    Func<string, bool> ForTown(string townId);
}
