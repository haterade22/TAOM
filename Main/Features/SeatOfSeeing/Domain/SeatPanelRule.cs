namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>The player's own ways to close the order panel that one frame showed (build-spec 6.2).</summary>
internal readonly record struct SeatCloseSignals(bool Escape, bool RightMouse, bool ViewOrders, bool HoldOrder)
{
    internal bool Any => Escape || RightMouse || ViewOrders || HoldOrder;
}

/// <summary>Whether the panel is reopened after a close (build-spec 6.2).</summary>
internal static class SeatPanelRule
{
    /// <summary>The frame the panel that was open is found closed.</summary>
    internal static bool ClosingEdge(bool wasOpen, bool isOpen) => wasOpen && !isOpen;

    /// <summary>
    /// True on a closing edge unless a close route of the player was seen on this frame or the previous one (an Escape close runs after the
    /// views tick, and view order is not fixed), or the troop transfer menu is open: the Transfer order closes the panel on purpose, and
    /// reopening it clears <c>IsTransferActive</c> and cancels the transfer.
    /// </summary>
    internal static bool ShouldReopen(SeatCloseSignals now, SeatCloseSignals previous, bool transferOpen) =>
        !now.Any && !previous.Any && !transferOpen;
}
