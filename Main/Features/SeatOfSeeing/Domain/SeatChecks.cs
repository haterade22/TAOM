namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>
/// The in-game check ids of the Seat, read by tools/ingame_checks/0.toml (renamed to its issue number once the issue exists): entered,
/// left, a frame owned for <see cref="FramesThreshold"/> frames, the glide armed, the fault latch (which must stay zero), the pointer, panel,
/// interface and keep checks of stages S2 and S3, the hero hand-off and take of stages S4 and S5, and the mission time and the lift of the view of stages S6 and S7.
/// </summary>
internal static class SeatChecks
{
    internal const string EnterId = "taom.seat-of-seeing.enter";
    internal const string LeaveId = "taom.seat-of-seeing.leave";
    internal const string FramesId = "taom.seat-of-seeing.frames";
    internal const string GlideId = "taom.seat-of-seeing.glide";
    internal const string FaultId = "taom.seat-of-seeing.fault";
    internal const string CursorId = "taom.seat-of-seeing.cursor";
    internal const string FocusId = "taom.seat-of-seeing.focus";
    internal const string PanelReopenedId = "taom.seat-of-seeing.panel-reopened";
    internal const string HideId = "taom.seat-of-seeing.hide";
    internal const string KeepId = "taom.seat-of-seeing.keep";
    internal const string LetGoId = "taom.seat-of-seeing.let-go";
    internal const string FightOnId = "taom.seat-of-seeing.fight-on";
    internal const string TakeId = "taom.seat-of-seeing.take";
    internal const string TimeId = "taom.seat-of-seeing.time";
    internal const string RiseId = "taom.seat-of-seeing.rise";

    /// <summary>The hot-path counter of owned frames: one increment a frame, no string and no I/O.</summary>
    internal const string FrameCounterId = "taom.seat-of-seeing.owned-frame";

    internal const int FramesThreshold = 60;
}
