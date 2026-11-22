namespace TAOM.Features.TimeAcceleration;

public interface ITimeAccelerationService
{
    void OnTick();

    /// <summary>
    /// The MapBar Extra Fast Forward button. Writes the extra multiplier and enters a fast-forward
    /// mode, behind the same gate vanilla's own time buttons use (no menu, or a wait menu that is
    /// not time-locked). Campaign.TickMapTime scales real time by SpeedUpMultiplier only in the
    /// fast-forward modes, so the mode change is what makes the multiplier count.
    /// </summary>
    void EnterExtraFastForward();

    /// <summary>
    /// Vanilla's MapBar FastForward button, rebound here so it writes the NORMAL multiplier back.
    /// Vanilla's handler sets the mode only, so after one extra press it kept running at the extra
    /// value until Space happened to restore it.
    /// </summary>
    void EnterFastForward();

    /// <summary>
    /// True while a fast-forward mode runs above the normal fast-forward multiplier. Read every
    /// frame by the MapBar mixin for the Extra Fast Forward button's lit state.
    /// </summary>
    bool IsExtraFastForwardActive { get; }

    /// <summary>
    /// False while this peer defers campaign time to a co-op server: the extra multiplier is never written there, so
    /// the themed map bar hides its Extra Fast Forward button. Read live, every frame, by the MapBar mixin.
    /// </summary>
    bool IsExtraFastForwardAvailable { get; }

    /// <summary>
    /// True while the turbo latch is open (Ctrl held and the turbo key pressed, until either is released or a
    /// menu, the map or co-op closes it). Read by Patch124 on the frame a Ctrl click is released (#1040).
    /// </summary>
    bool IsTurboActive { get; }
}
