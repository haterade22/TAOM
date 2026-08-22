using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Adapters;

/// <summary>
/// Measures how far a candidate target is from a faction's own territory, in units of the engine's
/// average distance between the closest two towns ("town gaps").
///
/// Exists because <c>MapDistanceModel</c>, <c>Settlement</c> and <c>IFaction</c> are sealed or
/// engine-owned types that must not cross into a service (ADR-007). The service takes the single
/// float this produces.
/// </summary>
public interface IMapReachAdapter
{
    /// <summary>
    /// Distance from <paramref name="targetSettlement"/> to the NEAREST fortification owned by
    /// <paramref name="attackerFaction"/>, divided by the average town gap.
    ///
    /// Nearest fortification rather than <c>FactionMidSettlement</c> deliberately: the medoid
    /// distorts wide empires by up to 3.4x and recomputes on every fief transfer
    /// (<c>OnFortificationAdded</c> / <c>OnFortificationRemoved</c>), so it drifts toward whatever
    /// a kingdom is currently conquering and widens its reach in the direction it is already
    /// winning.
    /// </summary>
    /// <returns>
    /// Normalised distance in town gaps, or <see cref="float.NaN"/> when it cannot be measured
    /// (no campaign, null arguments, a landless faction, or a degenerate average town gap).
    /// Callers treat NaN as "do not suppress".
    /// </returns>
    float GetNormalizedDistanceToNearestFortification(Settlement targetSettlement, IFaction attackerFaction);

    /// <summary>
    /// Drops everything cached for one faction. Driven by <c>OnSettlementOwnerChangedEvent</c>,
    /// because a fief-count comparison cannot see a same-count exchange (one fortification lost and
    /// another gained in the same transfer), and reach is anchored on current holdings.
    /// </summary>
    void InvalidateFaction(string factionId);

    /// <summary>
    /// Drops every cache and the campaign reference. Called on campaign teardown, because this is a
    /// process-lifetime singleton and would otherwise keep a finalized campaign's Settlement graph
    /// reachable through the whole of the next campaign's load.
    /// </summary>
    void Reset();
}
