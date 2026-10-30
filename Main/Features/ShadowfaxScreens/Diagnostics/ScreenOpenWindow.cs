namespace TAOM.Features.ShadowfaxScreens.Diagnostics;

/// <summary>One timed phase of a screen open, from <see cref="ScreenOpenTimer.Begin"/> to its one End.</summary>
public sealed class ScreenOpenWindow
{
    internal ScreenOpenWindow(string screen, string phase, long startTick)
    {
        Screen = screen;
        Phase = phase;
        StartTick = startTick;
    }

    public string Screen { get; }

    public string Phase { get; }

    public long StartTick { get; }

    internal bool Ended { get; set; }
}
