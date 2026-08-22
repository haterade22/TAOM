using TAOM.Features.SupplyLines.Domain;

namespace TAOM.Features.SupplyLines;

/// <summary>
/// Owns the caravan map parties: spawn, per-frame movement, teardown. Engine boundary.
///
/// <para><b>Movement keeps the source module's teleport model</b> (user decision 2026-08-22): the
/// party's AI is disabled at spawn and its position is set along a cached nav path as a function of
/// the order's elapsed travel fraction. Hardened relative to the source: the private
/// <c>MobileParty.Bearing</c> setter binding is pinned by a BindingVerification test so engine
/// drift fails in CI instead of silently sliding caravans sideways; party lookups are cached
/// instead of per-frame LINQ over <c>MobileParty.All</c>; a frame with no position change skips the
/// native set.</para>
/// </summary>
public interface ISupplyCaravanService
{
    /// <summary>
    /// Creates the caravan party at the source, filled with the consumed cargo, mercenary guards
    /// when chosen, and the companion escort when chosen. Returns the party StringId, or null when
    /// the spawn failed (caller refunds; the source module charged before spawning and kept the
    /// money on failure).
    /// </summary>
    string Spawn(SupplyOrder order);

    /// <summary>True while the party for this order still exists.</summary>
    bool CaravanExists(SupplyOrder order);

    /// <summary>True when the caravan is currently in a raid event.</summary>
    bool CaravanInRaid(SupplyOrder order);

    /// <summary>Map distance caravan → main party, or float.MaxValue when unknown.</summary>
    float DistanceToPlayer(SupplyOrder order);

    /// <summary>Per-frame teleport pass over all in-transit orders. Cheap when nothing moved.</summary>
    void TickPositions();

    /// <summary>
    /// Releases the companion escort (if any, and still alive) back to the main party, THEN
    /// destroys the caravan party. The order is load-bearing: the source module destroyed first,
    /// which nulled the hero's party and stranded the companion forever.
    /// </summary>
    void ReleaseEscortAndDestroy(SupplyOrder order);

    /// <summary>Re-creates caravans for in-transit orders whose party vanished across a save load.</summary>
    void RespawnMissing(System.Collections.Generic.IEnumerable<SupplyOrder> orders);
}
