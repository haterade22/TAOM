using TAOM.Features.Warg;

namespace TAOM.Tests.Features.SeatOfSeeing;

// The warg riders of the stand-in (#1563): twelve riders whose wargs stand while held and run after the Charge order, beside the horsemen of the army.
internal sealed partial class CommandTrialWorld
{
    internal int WargRiders = 12;
    internal bool NoWargs;
    internal float WargSpeedLimit = 13f;
    internal float WargScale = 1.1f;
    internal float WargChargeSpeed = 9f;
    internal float HorseChargeSpeed = 13f;
    internal bool WargSpeedIsNoNumber;

    /// <summary>The one reading of the charge, counted from 1, whose warg speed is no number; the readings before and after it are good.</summary>
    internal int? WargSpeedIsNoNumberOnChargeRead;
    internal int WargReadsOutsideTheMissionTick;

    private int _wargChargeReads;

    public WargReading ReadWargs()
    {
        if (!InMissionTick)
            WargReadsOutsideTheMissionTick++;
        if (NoWargs)
            return new WargReading(0, 0, 0f, 0f, 0f, 0f);

        var charging = !float.IsNaN(_crewChargedAt) && Time - _crewChargedAt >= 1f;
        if (charging)
            _wargChargeReads++;
        var noNumber = WargSpeedIsNoNumber || (charging && _wargChargeReads == WargSpeedIsNoNumberOnChargeRead);
        var speed = noNumber ? float.NaN : charging ? WargChargeSpeed : 0f;
        return new WargReading(WargRiders, WargRiders, speed, charging ? HorseChargeSpeed : 0f, WargSpeedLimit, WargScale);
    }
}
