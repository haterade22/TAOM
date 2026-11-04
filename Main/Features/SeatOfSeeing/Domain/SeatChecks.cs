namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>
/// The in-game check ids of the Seat, read by tools/ingame_checks/0.toml (renamed to its issue number once the issue exists): entered,
/// left, a frame owned for <see cref="FramesThreshold"/> frames, the glide armed, the fault latch (which must stay zero), and the pointer, panel,
/// interface and keep checks of stages S2 and S3.
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

    /// <summary>The hot-path counter of owned frames: one increment a frame, no string and no I/O.</summary>
    internal const string FrameCounterId = "taom.seat-of-seeing.owned-frame";

    internal const int FramesThreshold = 60;
}
