namespace TAOM.Features.TimeAcceleration;

public interface ITimeAccelerationSettingsProvider
{
    int FastForwardMultiplier { get; }
    int ExtraFastForwardMultiplier { get; }
    int CtrlSpaceMultiplier { get; }
}
