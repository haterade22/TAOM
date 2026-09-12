namespace TAOM.Features.TimeAcceleration;

public class TimeAccelerationSettingsProvider : ITimeAccelerationSettingsProvider
{
    public int FastForwardMultiplier => TaomSettings.Instance?.FastForwardMultiplier ?? 4;

    // Both sliders range 1 to 128 on their own, so without this a player can make "extra" slower
    // than "fast", and the MapBar button's lit state (a fast-forward mode running ABOVE the normal
    // multiplier) becomes unreachable. Floor, never swap: the fast-forward value stays exactly what
    // the player set.
    public int ExtraFastForwardMultiplier =>
        ClampExtra(FastForwardMultiplier, TaomSettings.Instance?.ExtraFastForwardMultiplier ?? 8);

    public int CtrlSpaceMultiplier => TaomSettings.Instance?.CtrlSpaceMultiplier ?? 16;

    internal static int ClampExtra(int fast, int extra) => extra < fast ? fast : extra;
}
