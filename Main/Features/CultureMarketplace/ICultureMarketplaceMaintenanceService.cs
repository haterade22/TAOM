using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Features.CultureMarketplace;

/// <summary>
/// Encapsulates the two non-injection passes of the marketplace daily tick:
///   ① <see cref="EnsureGuaranteedStock"/> — top up routed items with <c>min_stock &gt; 0</c>
///   ② <see cref="FilterForeignCultureItems"/> — remove items whose effective culture
///      does not match the town's owner (bounded by <paramref name="removalCap"/>)
///
/// <para>The <see cref="Settlement"/> parameter is passed-through to
/// <see cref="TAOM.Adapters.ITownRosterAdapter"/> — the service itself never reads any
/// property off it. This keeps the public method signature ergonomic without breaking
/// ADR-007's "no sealed TaleWorlds types in services" intent (the service does not
/// reference the type, only forwards it).</para>
///
/// <para>Extracted from <see cref="CultureMarketplaceBehavior"/> in deep-review 2026-05-21
/// to satisfy ADR-002 (behavior under 150 lines, no business logic) and to allow direct
/// public-API testing (no reflection on private methods).</para>
/// </summary>
public interface ICultureMarketplaceMaintenanceService
{
    /// <summary>Returns number of units added across all guaranteed-stock entries.</summary>
    int EnsureGuaranteedStock(Settlement settlement, string cultureId);

    /// <summary>Returns number of distinct roster rows removed (≤ <paramref name="removalCap"/>).</summary>
    int FilterForeignCultureItems(Settlement settlement, string cultureId, int removalCap);
}
