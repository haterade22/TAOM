using System.Collections.Generic;
using TAOM.Features.SupplyLines.Domain;

namespace TAOM.Features.SupplyLines;

/// <summary>
/// The order book: creation, hourly advance, delivery, cancellation. Owns the persisted order
/// dictionary (the campaign behavior hands it in through <see cref="LoadFrom"/>/<see cref="SaveInto"/>
/// at SyncData time).
///
/// <para>Payment ordering fixed relative to the source module: consume + spawn FIRST, charge last,
/// so a failed spawn costs nothing instead of keeping the player's gold.</para>
/// </summary>
public interface ISupplyOrderService
{
    /// <summary>Orders currently alive (Ordered or InTransit).</summary>
    IReadOnlyCollection<SupplyOrder> ActiveOrders { get; }

    /// <summary>
    /// Builds, consumes, spawns and charges one order. Returns the order on success; null with
    /// <paramref name="failReason"/> set when it could not be placed (blockaded, unaffordable,
    /// stock gone, spawn failed). Never charges on failure.
    /// <paramref name="placedFromCamp"/> marks the order as camp-placed so <see
    /// cref="CancelCampOrders"/> can cancel it (and only it) when that camp breaks.
    /// </summary>
    SupplyOrder TryPlaceOrder(
        SupplySourceInfo source,
        IReadOnlyDictionary<string, int> goods,
        IReadOnlyDictionary<string, int> troops,
        SupplyEscortOption escort,
        out string failReason,
        bool placedFromCamp = false);

    /// <summary>Hourly advance over every active order: applies the engine's verdicts.</summary>
    void HourlyTick();

    /// <summary>
    /// Per-frame: caravan position pass + proximity delivery check (delivery itself is gated on the
    /// player not being in an encounter).
    /// </summary>
    void FrameTick();

    /// <summary>
    /// Records the loss of an order whose caravan party the ENGINE is destroying right now
    /// (the behavior's MobilePartyDestroyed listener). Runs synchronously at destroy time so a
    /// save written afterwards can never carry a stale InTransit row that a load would
    /// resurrect with its full cargo. Our own teardown paths flip the status before destroying,
    /// so they fall through here.
    /// </summary>
    void OnCaravanDestroyed(string orderId);

    /// <summary>SyncData plumbing: replace the in-memory book with the loaded one.</summary>
    void LoadFrom(Dictionary<string, SupplyOrder> orders, int counter);

    /// <summary>SyncData plumbing: expose the book for saving.</summary>
    void SaveInto(out Dictionary<string, SupplyOrder> orders, out int counter);

    /// <summary>Post-load repair: respawn missing caravans, re-pin AI.</summary>
    void OnGameLoaded();

    /// <summary>
    /// Clears the book AND every transient cache (caravan trackers, route visuals) for a session
    /// with no saved record. The services are process singletons and SyncData only fires when a
    /// record exists, so without this a NEW campaign inherits the previous campaign's orders
    /// (round-A CRITICAL). The behavior calls it from OnSessionLaunched when no load occurred.
    /// </summary>
    void ResetForNewSession();

    /// <summary>Cancels only orders placed from the field camp. Breaking camp must not forfeit
    /// town-placed orders that have nothing to do with the camp (round-A HIGH).</summary>
    void CancelCampOrders();
}
