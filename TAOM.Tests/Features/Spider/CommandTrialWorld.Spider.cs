using System;
using TAOM.Features.SeatOfSeeing.Domain;
using TAOM.Features.Spider;

namespace TAOM.Tests.Features.SeatOfSeeing;

// The spider riders of the stand-in (#1541): twelve riders whose mounts stand while the army is held, run after the Charge order and slow down after the next Stop.
internal sealed partial class CommandTrialWorld
{
    internal int SpiderRiders = 12;
    internal bool NoSpiders;
    internal float SpidersAppearAt;
    internal float SpiderStandSpeed;

    /// <summary>The speed of spiders that still walk into their ranks, for <see cref="SpiderSettlesAfter"/> seconds after the first Stop order.</summary>
    internal float SpiderSettleSpeed;
    internal float SpiderSettlesAfter;
    internal float SpiderChargeSpeed = 8f;
    internal float SpiderStopSpeed = 0.2f;

    /// <summary>Seconds after the Stop that follows the charge before the spiders slow down; they run at the charge speed until then.</summary>
    internal float SpiderSlowsAfter = 1f;
    internal float? SpiderMountsDieAfterSeconds;
    internal float? KeptRiderFallsAfterSeconds;
    internal bool NoOtherRider;
    internal bool SpiderSpeedIsNoNumber;

    internal int HoldOrders;
    internal int SpiderReadsOutsideTheMissionTick;
    internal int SpiderChoices;
    internal int SpiderKeepPresses;

    private float _spiderStoppedAt = float.NaN;
    private float _spiderFirstHeldAt = float.NaN;
    private bool _keptRiderFell;

    /// <summary>The time of the Charge order, NaN before it.</summary>
    internal float ChargedAtTime => _crewChargedAt;

    private bool SpiderMountsLive => !(SpiderMountsDieAfterSeconds.HasValue && !float.IsNaN(_crewChargedAt) && Time - _crewChargedAt >= SpiderMountsDieAfterSeconds.Value);

    private float SpiderSpeed
    {
        get
        {
            if (float.IsNaN(_crewChargedAt))
                return !float.IsNaN(_spiderFirstHeldAt) && Time - _spiderFirstHeldAt < SpiderSettlesAfter ? SpiderSettleSpeed : SpiderStandSpeed;
            if (float.IsNaN(_spiderStoppedAt))
                return Time - _crewChargedAt >= 1f ? SpiderChargeSpeed : SpiderChargeSpeed / 2f;
            return Time - _spiderStoppedAt >= SpiderSlowsAfter ? SpiderStopSpeed : SpiderChargeSpeed;
        }
    }

    public SpiderReading ReadSpiders()
    {
        NoteSpiderRead();
        if (NoSpiders || Time < SpidersAppearAt)
            return new SpiderReading(0, 0, 0f);

        var mounted = SpiderMountsLive ? SpiderRiders : 0;
        var speed = SpiderSpeedIsNoNumber ? float.NaN : SpiderMountsLive ? SpiderSpeed : 0f;
        return new SpiderReading(SpiderRiders, mounted, speed);
    }

    public bool ChooseSpiderRider()
    {
        NoteSpiderRead();
        SpiderChoices++;
        if (NoSpiders || Time < SpidersAppearAt || !SpiderMountsLive || (_keptRiderFell && NoOtherRider))
            return false;

        ResetTheKeepTarget();
        return true;
    }

    public void HoldAll()
    {
        Note("hold-all");
        HoldOrders++;
        if (float.IsNaN(_spiderFirstHeldAt))
            _spiderFirstHeldAt = Time;
        if (!float.IsNaN(_crewChargedAt) && float.IsNaN(_spiderStoppedAt))
            _spiderStoppedAt = Time;
    }

    private void NoteSpiderRead()
    {
        if (!InMissionTick)
            SpiderReadsOutsideTheMissionTick++;
    }

    // Once per mission tick, with the crew frame: the kept rider can fall once. ChargeAll is the crew partial's, and its time starts the run.
    private void SpiderFrame()
    {
        if (_keepPressed)
            SpiderKeepPresses++;
        if (!_keptRiderFell && KeptRiderFallsAfterSeconds.HasValue && Keeping == SeatKeepKind.Soldier
            && !float.IsNaN(_crewChargedAt) && Time - _crewChargedAt >= KeptRiderFallsAfterSeconds.Value)
        {
            _keptRiderFell = true;
            _soldierDead = true;
        }
    }
}
