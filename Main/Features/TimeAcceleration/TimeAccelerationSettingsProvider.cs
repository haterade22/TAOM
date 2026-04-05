namespace TAOM.Features.TimeAcceleration;

public class TimeAccelerationSettingsProvider : ITimeAccelerationSettingsProvider
{
    public int FastForwardMultiplier => TaomSettings.Instance?.FastForwardMultiplier ?? 4;
    public int ExtraFastForwardMultiplier => TaomSettings.Instance?.ExtraFastForwardMultiplier ?? 8;
    public int CtrlSpaceMultiplier => TaomSettings.Instance?.CtrlSpaceMultiplier ?? 16;
}
