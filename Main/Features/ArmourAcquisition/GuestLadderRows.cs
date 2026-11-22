using System;
using System.Collections.Generic;
using TAOM.Features.CoopInterop.GuestTrade;

namespace TAOM.Features.ArmourAcquisition;

public enum GuestLadderRowKind
{
    HandIn,
    Claim,
}

/// <summary>One ladder row of a co-op guest's armoury, and the server's view it was built from.</summary>
public sealed record GuestLadderRow(GuestLadderRowKind Kind, bool Enabled, GuestLadderView View);

/// <summary>
/// Which ladder rows a co-op guest's armoury lists (docs/features/armour-acquisition.md, item 8): built from the co-op
/// server's <see cref="GuestLadderView"/>, as <c>LadderPresenter.Rows</c> builds a local player's from this machine's
/// state. A done rung offers its claim, enabled at a lord-level armoury; a rung that waits for materials offers the
/// hand-in, enabled when the party carries enough. The rung quest is not offered to a guest.
/// </summary>
public static class GuestLadderRows
{
    public static IReadOnlyList<GuestLadderRow> Plan(GuestLadderView? view)
    {
        if (view == null)
            return Array.Empty<GuestLadderRow>();
        if (view.Ready)
            return new[] { new GuestLadderRow(GuestLadderRowKind.Claim, view.CanClaim, view) };
        if (view.MaterialId != null)
            return new[] { new GuestLadderRow(GuestLadderRowKind.HandIn, view.Carried >= view.Needed, view) };
        return Array.Empty<GuestLadderRow>();
    }
}
