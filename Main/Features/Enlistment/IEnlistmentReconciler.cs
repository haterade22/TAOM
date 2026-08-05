using System;

namespace TAOM.Features.Enlistment;

/// <summary>
/// The hourly reconciler — the SINGLE terminal-decision authority for the enlisted state:
/// commander death, grace start/expiry, captivity transitions, re-parking, position sync.
/// Menus and dialogs only render state; they never make terminal decisions (the donor's
/// wait-menu/daily-tick policy split is the bug class this centralization removes).
/// </summary>
public interface IEnlistmentReconciler
{
    void ReconcileHourly(double nowDays);

    /// <summary>Raised when the commander is fighting a map event the player has not joined. The battle layer subscribes.</summary>
    event Action<string> BattleJoinRequested;
}
