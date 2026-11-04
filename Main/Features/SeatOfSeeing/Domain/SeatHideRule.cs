namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>When the Seat gives a hidden interface back (build-spec 6.4).</summary>
internal static class SeatHideRule
{
    /// <summary>The first reason the interface comes back on its own, or null: an invisible panel still slows time, and a left click still gives an order, and a dialog or another layer needs the screen.</summary>
    internal static string? RestoreReason(bool panelOpen, bool dialogShown, bool otherLayerFocused) =>
        panelOpen ? "panel" : dialogShown ? "dialog" : otherLayerFocused ? "layer" : null;
}
