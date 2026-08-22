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
    /// </summary>
    SupplyOrder TryPlaceOrder(
        SupplySourceInfo source,
        IReadOnlyDictionary<string, int> goods,
        IReadOnlyDictionary<string, int> troops,
        SupplyEscortOption escort,
        out string failReason);

    /// <summary>Hourly advance over every active order: applies the engine's verdicts.</summary>
    void HourlyTick();

    /// <summary>
    /// Per-frame: caravan position pass + proximity delivery check (delivery itself is gated on the
    /// player not being in an encounter).
    /// </summary>
    void FrameTick();

    /// <summary>Cancels every active order (goods and gold are lost; escorts come home). The
    /// FieldCamp break-camp path calls this directly in the merged port.</summary>
    void CancelAll();

    /// <summary>SyncData plumbing: replace the in-memory book with the loaded one.</summary>
    void LoadFrom(Dictionary<string, SupplyOrder> orders, int counter);

    /// <summary>SyncData plumbing: expose the book for saving.</summary>
    void SaveInto(out Dictionary<string, SupplyOrder> orders, out int counter);

    /// <summary>Post-load repair: respawn missing caravans, re-pin AI.</summary>
    void OnGameLoaded();
}
