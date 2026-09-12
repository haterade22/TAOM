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
}
