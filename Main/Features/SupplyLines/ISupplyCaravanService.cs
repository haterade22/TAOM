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

    /// <summary>
    /// True while the caravan party is attached to ANY map event. The engine owns a fighting
    /// party: the order must Continue, never Deliver (destroying a party still attached to a
    /// MapEvent side violates the engine's detach-before-destroy contract) and never Lose early
    /// (a lost battle destroys the party, which <see cref="CaravanExists"/> then reports).
    /// </summary>
    bool CaravanInMapEvent(SupplyOrder order);

    /// <summary>
    /// Snapshot of the caravan's live CARGO right now: item id -> count aboard, and non-hero
    /// troop id -> count aboard MINUS the order's spawn-time non-cargo manifest (template
    /// guards and mercenary escorts are never cargo, even when they share a character id with a
    /// purchased recruit; the escort hero is never cargo either). False when the party is
    /// missing or unreadable. Delivery caps the ordered amounts by this, so goods eaten in
    /// transit or recruits lost to a battle are not resurrected at the player's feet.
    /// </summary>
    bool TryGetLiveCargo(
        SupplyOrder order,
        out System.Collections.Generic.IReadOnlyDictionary<string, int> goods,
        out System.Collections.Generic.IReadOnlyDictionary<string, int> troops);

    /// <summary>
    /// Drops every caravan tracker without touching the parties. Called whenever the order book
    /// is replaced (load, new session): a tracker's cached MobileParty belongs to the session
    /// that created it, and a deserialized campaign has NEW party objects under the same ids,
    /// so a stale tracker drives a ghost (round-A HIGH). Rebinding happens in
    /// <see cref="RespawnMissing"/> from the live campaign's party list.
    /// </summary>
    void ClearTrackers();

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

    /// <summary>
    /// Drops the tracker for an order whose caravan the ENGINE is destroying right now
    /// (MobilePartyDestroyed). Never destroys the party again and never touches the companion:
    /// the destroying battle already decided the escort's fate.
    /// </summary>
    void ForgetDestroyed(SupplyOrder order);

    /// <summary>Re-creates caravans for in-transit orders whose party vanished across a save load.</summary>
    void RespawnMissing(System.Collections.Generic.IEnumerable<SupplyOrder> orders);
}
