namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>
/// The in-game check ids of the Seat, read by tools/ingame_checks/0.toml (renamed to its issue number once the issue exists): entered,
/// left, a frame owned for <see cref="FramesThreshold"/> frames, the glide armed, and the fault latch (which must stay zero).
/// </summary>
internal static class SeatChecks
{
    internal const string EnterId = "taom.seat-of-seeing.enter";
    internal const string LeaveId = "taom.seat-of-seeing.leave";
    internal const string FramesId = "taom.seat-of-seeing.frames";
    internal const string GlideId = "taom.seat-of-seeing.glide";
    internal const string FaultId = "taom.seat-of-seeing.fault";

    /// <summary>The hot-path counter of owned frames: one increment a frame, no string and no I/O.</summary>
    internal const string FrameCounterId = "taom.seat-of-seeing.owned-frame";

    internal const int FramesThreshold = 60;
}
