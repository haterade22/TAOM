namespace TAOM.Features.MapPerf;

/// <summary>A map frame's time-control class. Ordered so that a larger value is the faster class
/// (window ties go to it); Unknown loses every tie.</summary>
public enum MapSpeedClass { Unknown, Stop, Play, FF, FF2, FF3 }

/// <summary>
/// Classifies a campaign-map frame by the engine's time-control mode and speed-up multiplier, read at the
/// frame's start boundary. <c>TaleWorlds.CampaignSystem.CampaignTimeControlMode</c> on the installed
/// v1.5.3: Stop 0, UnstoppablePlay 1, UnstoppableFastForward 2, StoppablePlay 3, StoppableFastForward 4,
/// UnstoppableFastForwardForPartyWaitTime 5, FastForwardStop 6. <c>Campaign.TickMapTime</c> gives Stop
/// and FastForwardStop no campaign time, the two play modes <c>0.25 * realDt</c>, and the three
/// fast-forward modes <c>0.25 * realDt * SpeedUpMultiplier</c>; the two Stoppable modes advance time only
/// while the main party is not waiting. The profiler therefore feeds the mode that
/// <c>Campaign.GetSimplifiedTimeControlMode</c> returns (<c>ITimeControlAdapter.SimplifiedTimeControlMode</c>):
/// Stop for a Stoppable mode while the main party waits, so a Play or FF frame advances campaign time, and a
/// waiting frame does not pose as one. Every raw value is still classified, as the tests pin.
/// Fast-forward frames split by TAOM's MCM multipliers: FF up to the fast-forward multiplier, FF2 up to the
/// extra fast-forward multiplier, FF3 above (Ctrl+Space turbo, or any larger value).
/// </summary>
public static class MapSpeed
{
    private const int ModeStop = 0;
    private const int ModeUnstoppablePlay = 1;
    private const int ModeUnstoppableFastForward = 2;
    private const int ModeStoppablePlay = 3;
    private const int ModeStoppableFastForward = 4;
    private const int ModeUnstoppableFastForwardForPartyWaitTime = 5;
    private const int ModeFastForwardStop = 6;

    // Float multipliers compared with the integer MCM values: a hair of tolerance so 4f reads as 4.
    private const float Tolerance = 0.001f;

    public static MapSpeedClass Classify(int mode, float multiplier, int fastForwardMultiplier, int extraFastForwardMultiplier)
    {
        switch (mode)
        {
            case ModeStop:
            case ModeFastForwardStop:
                return MapSpeedClass.Stop;
            case ModeUnstoppablePlay:
            case ModeStoppablePlay:
                return MapSpeedClass.Play;
            case ModeUnstoppableFastForward:
            case ModeStoppableFastForward:
            case ModeUnstoppableFastForwardForPartyWaitTime:
                if (float.IsNaN(multiplier) || float.IsInfinity(multiplier))
                    return MapSpeedClass.Unknown;
                if (multiplier <= fastForwardMultiplier + Tolerance)
                    return MapSpeedClass.FF;
                if (multiplier <= extraFastForwardMultiplier + Tolerance)
                    return MapSpeedClass.FF2;
                return MapSpeedClass.FF3;
            default:
                return MapSpeedClass.Unknown;
        }
    }

    /// <summary>The class as the <c>speed=</c> and <c>byspeed=</c> fields print it.</summary>
    public static string Token(MapSpeedClass speed) => speed switch
    {
        MapSpeedClass.Stop => "Stop",
        MapSpeedClass.Play => "Play",
        MapSpeedClass.FF => "FF",
        MapSpeedClass.FF2 => "FF2",
        MapSpeedClass.FF3 => "FF3",
        _ => "na",
    };
}
