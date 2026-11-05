using System;
using System.Collections.Generic;
using TAOM.Features.MarshalsWord.Domain;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

// Hold the line (build-spec 9.2). Four steps, each its own --steps subset in its own battle: the proof that a cleared flag stops the blows, the slot under Stop, the
// strike back after a wound, and the charge that gives the flag back. None sends for the enemy, because a charge ends the hold: the enemy comes to the line.
internal sealed partial class CommandTrialScript
{
    private const float HoldQualifyWaitSeconds = 6f;

    private static void MarshalHoldSteps(Dictionary<string, Func<CommandTrialScript, IEnumerable<Verdict>>> into)
    {
        into["hold_line_proof"] = s => s.HoldLineProof();
        into["hold_line_slot"] = s => s.HoldLineSlot();
        into["hold_line_strike"] = s => s.HoldLineStrike();
        into["hold_line_flag"] = s => s.HoldLineFlag();
    }

    private sealed class HoldGround
    {
        internal int Units;
        internal int Companies;
    }

    // The ground every hold step shares: a company of foot soldiers (two for the proof, split from one) in the shield wall under Stop, hold on, and enough of the line
    // reading as qualified. The step makes this itself, so that the arrangement and the order are the ones the rule asks for.
    private IEnumerable<Verdict> HoldLineGround(bool split, HoldGround ground)
    {
        foreach (var verdict in MarshalBattle())
            yield return verdict;
        yield return Verdict.InMissionTick;
        ground.Companies = split ? _host.MarshalSplitInfantry() : _host.MarshalChoosePureInfantry() > 0 ? 1 : 0;
        if (ground.Companies < (split ? 2 : 1))
            yield return Verdict.Failed(split ? "reason=no-split held_units=" + Whole(_host.MarshalUnits(0)) + " control_units=" + Whole(_host.MarshalUnits(1)) : "reason=no-infantry");
        var splitAt = _now;
        while (split && !(_host.MarshalControllable(0) && _host.MarshalControllable(1)) && _now - splitAt < SeatTrialRules.ShortWaitSeconds)
            yield return Verdict.Waiting("companies-controllable");
        var reason = split ? MarshalTrialRules.HoldGroundReason(_host.MarshalUnits(0), _host.MarshalUnits(1), _host.MarshalControllable(0), _host.MarshalControllable(1)) : "";
        if (reason.Length > 0)
            yield return Verdict.Failed("reason=" + reason + " held=" + Bit(_host.MarshalControllable(0)) + " control=" + Bit(_host.MarshalControllable(1))
                + " held_units=" + Whole(_host.MarshalUnits(0)) + " control_units=" + Whole(_host.MarshalUnits(1)));
        if (_host.MarshalChooseNearestEnemy() == 0)
            yield return Verdict.Failed("reason=no-enemy");

        yield return Verdict.InMissionTick;
        _host.MarshalSelect(0, ground.Companies);
        _host.MarshalOrderShieldWall();
        _host.MarshalStandGround();
        _host.MarshalSelect(0, 1);
        _host.MarshalPressHold();
        var pressedAt = _now;
        while (!_host.MarshalHeld(0) && _now - pressedAt < SeatTrialRules.ShortWaitSeconds)
            yield return Verdict.Waiting("hold-on");
        if (!_host.MarshalHeld(0) || (split && _host.MarshalHeld(1)))
            yield return Verdict.Failed("reason=hold-not-set held=" + Bit(_host.MarshalHeld(0)) + " control=" + Bit(_host.MarshalHeld(1)));

        while (_host.MarshalHoldCensus(0).Qualified < MarshalTuning.HoldMinFlagOff && _now - pressedAt < HoldQualifyWaitSeconds)
            yield return Verdict.Waiting("line-qualifies");
        var census = _host.MarshalHoldCensus(0);
        ground.Units = _host.MarshalUnits(0);
        if (census.Qualified < MarshalTuning.HoldMinFlagOff)
            yield return Verdict.Failed("reason=not-qualified why=" + (census.WhyNot.Length == 0 ? "no-read" : census.WhyNot) + " qualified=" + Whole(census.Qualified)
                + " units=" + Whole(ground.Units) + " arrangement=" + _host.MarshalArrangementName(0) + " order=" + _host.MarshalOrderName(0) + Faulted());
    }

    // The step lets go of its hold, so that the next step in the same battle starts from a company that is not held, and waits for the flag to come back.
    private IEnumerable<Verdict> HoldLineLift()
    {
        yield return Verdict.InMissionTick;
        _host.MarshalSelect(0, 1);
        if (_host.MarshalHeld(0))
            _host.MarshalPressHold();
        var liftedAt = _now;
        while ((_host.MarshalHeld(0) || _host.MarshalFlagOff(0) > 0) && _now - liftedAt < RestoreWaitSeconds)
            yield return Verdict.Waiting("hold-off");
    }

    // The verdict is the driver's judged hold-line check for this mission: its numbers, and a limit named for it. The step adds only what the check cannot know, that the
    // enemy was near the held company at some point, and that the flag comes back when the hold is lifted.
    private IEnumerable<Verdict> HoldLineProof()
    {
        var ground = new HoldGround();
        foreach (var verdict in HoldLineGround(split: true, ground))
            yield return verdict;

        var peakNear = 0;
        var waitedAt = _now;
        while (HoldLineUnjudged() && _now - waitedAt < MarshalTrialRules.HoldVerdictWaitSeconds)
        {
            peakNear = Math.Max(peakNear, _host.MarshalEnemiesNear(0, MarshalTrialRules.HoldReachMetres));
            yield return Verdict.Waiting("hold-line-verdict");
        }

        peakNear = Math.Max(peakNear, _host.MarshalEnemiesNear(0, MarshalTrialRules.HoldReachMetres));
        var driver = _host.MarshalHoldLineVerdict;
        var flagOffNow = _host.MarshalFlagOff(0);
        var closer = _host.MarshalSlotCloser(0);
        var farther = _host.MarshalSlotFarther(0);
        foreach (var verdict in HoldLineLift())
            yield return verdict;
        var restored = !_host.MarshalHeld(0) && _host.MarshalFlagOff(0) == 0;

        var reason = MarshalTrialRules.HoldVerdictReason(driver, peakNear, restored, flagOffNow);
        var facts = (reason.Length > 0 ? "reason=" + reason + " " : "") + "driver=" + (driver == null ? "none" : driver.Value.Proof.ToString().ToLowerInvariant())
            + (driver == null ? "" : " flag_off=" + Whole(driver.Value.FlagOff) + " held_blows=" + Whole(driver.Value.HeldBlows) + " control_blows=" + Whole(driver.Value.ControlBlows)
                + " seconds=" + Num((float)driver.Value.Seconds))
            + " peak_near=" + Whole(peakNear) + " units=" + Whole(ground.Units) + " restored=" + Bit(restored) + " closer=" + Whole(closer) + " farther=" + Whole(farther) + Faulted();
        yield return reason.Length == 0 && !_host.MarshalFaulted ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    // A control too quiet to prove anything is not a judgement: the driver takes a new baseline and judges again, so the step keeps waiting through it.
    private bool HoldLineUnjudged() => _host.MarshalHoldLineVerdict == null || _host.MarshalHoldLineVerdict.Value.Proof == HoldProof.NoControl;

    // Under Stop the slot is an anchor the soldier's own place set, so a zero distance proves nothing alone. The step needs the enemy at the line, a soldier wounded, the clamp
    // to have replaced an input, and then 15 seconds of hold reads after the first steer, so the driver's five second windows have judged them. It fails when the driver's slot check
    // failed or when the totals of the whole hold moved more steered soldiers away from their slot than toward it. The clamps are counted from the step's own start, because the
    // counter is the process's.
    private IEnumerable<Verdict> HoldLineSlot()
    {
        var clampsAtStart = _host.MarshalClamps;
        var ground = new HoldGround();
        foreach (var verdict in HoldLineGround(split: false, ground))
            yield return verdict;

        var samples = 0;
        var peakNear = 0;
        var firstSteerAt = double.NaN;
        var lastSampleAt = double.NegativeInfinity;
        var waitedAt = _now;
        while (samples < MarshalTrialRules.SlotMinSamples && !_host.MarshalSlotFailed(0) && _now - waitedAt < MarshalTrialRules.HoldVerdictWaitSeconds)
        {
            peakNear = Math.Max(peakNear, _host.MarshalEnemiesNear(0, MarshalTrialRules.HoldReachMetres));
            if (double.IsNaN(firstSteerAt) && _host.MarshalClamps - clampsAtStart >= 1)
                firstSteerAt = _host.MissionTime;
            var afterTheSteer = !double.IsNaN(firstSteerAt) && _host.MissionTime - firstSteerAt >= MarshalTuning.HoldReadSeconds;
            if (afterTheSteer && _host.MissionTime - lastSampleAt >= MarshalTuning.HoldReadSeconds)
            {
                lastSampleAt = _host.MissionTime;
                var read = _host.MarshalHoldCensus(0);
                if (read.WoundedEver > 0 && read.Qualified >= MarshalTuning.HoldMinFlagOff)
                    samples++;
            }

            yield return Verdict.Waiting("slot-samples");
        }

        var census = _host.MarshalHoldCensus(0);
        var clamps = _host.MarshalClamps - clampsAtStart;
        var closer = _host.MarshalSlotCloser(0);
        var farther = _host.MarshalSlotFarther(0);
        var steered = _host.MarshalSlotSteered(0);
        var slotFailed = _host.MarshalSlotFailed(0);
        foreach (var verdict in HoldLineLift())
            yield return verdict;
        var reason = MarshalTrialRules.SlotTrialReason(samples, slotFailed, steered, closer, farther, census.Order, census.Qualified, census.Anchored, census.WoundedEver, peakNear, clamps);
        var facts = (reason.Length > 0 ? "reason=" + reason + " " : "") + "order=" + census.Order + " qualified=" + Whole(census.Qualified) + " anchored=" + Whole(census.Anchored)
            + " far=" + Whole(census.Far) + " max=" + Num(census.SlotMax) + " mean=" + Num(census.SlotMean) + " wounded=" + Whole(census.WoundedEver) + " samples=" + Whole(samples)
            + " clamps=" + Whole(clamps) + " steered=" + Whole(steered) + " closer=" + Whole(closer) + " farther=" + Whole(farther) + " slot_failed=" + Bit(slotFailed)
            + " peak_near=" + Whole(peakNear) + " units=" + Whole(ground.Units) + Faulted();
        yield return reason.Length == 0 && !_host.MarshalFaulted ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    // The strike back: the line is disarmed first, read from the soldiers' own flags, and then the first wound opens a window in which the wounded soldiers' flag reads on. The step
    // keeps waiting past the window until the company's blows rise or the limit passes, because the first soldiers wounded can die before they land one.
    private IEnumerable<Verdict> HoldLineStrike()
    {
        var ground = new HoldGround();
        foreach (var verdict in HoldLineGround(split: false, ground))
            yield return verdict;

        var flagOffBefore = _host.MarshalFlagOff(0);
        var flagAt = _now;
        while (flagOffBefore < MarshalTuning.HoldMinFlagOff && _now - flagAt < SeatTrialRules.ShortWaitSeconds)
        {
            yield return Verdict.Waiting("flag-off");
            flagOffBefore = _host.MarshalFlagOff(0);
        }

        var woundedEver = 0;
        var settledPeak = 0;
        var flagOnAtPeak = 0;
        var strikeBlowsAtWound = -1;
        var windowEndsAt = double.PositiveInfinity;
        var waitedAt = _now;
        while (flagOffBefore >= MarshalTuning.HoldMinFlagOff && _now - waitedAt < MarshalTrialRules.HoldVerdictWaitSeconds
            && (_host.MissionTime < windowEndsAt || strikeBlowsAtWound < 0 || _host.MarshalStrikeBlows(0) <= strikeBlowsAtWound))
        {
            var census = _host.MarshalHoldCensus(0);
            woundedEver = Math.Max(woundedEver, census.WoundedEver);
            if (census.WoundedEver > 0 && strikeBlowsAtWound < 0)
                strikeBlowsAtWound = _host.MarshalStrikeBlows(0);
            if (census.StruckSettled >= settledPeak && census.StruckSettled > 0)
            {
                settledPeak = census.StruckSettled;
                flagOnAtPeak = census.StruckFlagOn;
                if (double.IsPositiveInfinity(windowEndsAt))
                    windowEndsAt = _host.MissionTime + MarshalTuning.StrikeBackSeconds + 1.0;
            }

            yield return Verdict.Waiting("strike-back");
        }

        var blowsAfter = strikeBlowsAtWound < 0 ? 0 : _host.MarshalStrikeBlows(0) - strikeBlowsAtWound;
        var qualifiedAtEnd = _host.MarshalHoldCensus(0).Qualified;
        foreach (var verdict in HoldLineLift())
            yield return verdict;
        var reason = MarshalTrialRules.StrikeTrialReason(flagOffBefore, woundedEver, settledPeak, flagOnAtPeak, blowsAfter);
        var driverPassed = _host.MarshalStrikePassed(0);
        var facts = (reason.Length > 0 ? "reason=" + reason + " " : "") + "flag_off_before=" + Whole(flagOffBefore) + " wounded=" + Whole(woundedEver) + " settled=" + Whole(settledPeak) + " flag_on=" + Whole(flagOnAtPeak)
            + " blows_after=" + Whole(blowsAfter) + " driver_strike=" + Bit(driverPassed) + " qualified=" + Whole(qualifiedAtEnd) + " units=" + Whole(ground.Units) + Faulted();
        yield return reason.Length == 0 && driverPassed && !_host.MarshalFaulted ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    // Hold on, then a charge: the order that ends the line gives every soldier's attack flag back within the limit, read from the soldiers themselves.
    private IEnumerable<Verdict> HoldLineFlag()
    {
        var ground = new HoldGround();
        foreach (var verdict in HoldLineGround(split: false, ground))
            yield return verdict;
        var flagAt = _now;
        while (_host.MarshalFlagOff(0) < MarshalTuning.HoldMinFlagOff && _now - flagAt < SeatTrialRules.ShortWaitSeconds)
            yield return Verdict.Waiting("flag-off");
        var qualified = _host.MarshalHoldCensus(0).Qualified;
        var before = _host.MarshalFlagOff(0);

        yield return Verdict.InMissionTick;
        _host.MarshalSelect(0, 1);
        _host.MarshalChargeEnemy();
        var chargedAt = _now;
        while (_host.MarshalFlagOff(0) > 0 && _now - chargedAt < MarshalTrialRules.HoldRestoreSeconds)
            yield return Verdict.Waiting("flag-back");
        var restored = _host.MarshalFlagOff(0) == 0;
        var restoredIn = _now - chargedAt;
        var orderAfter = _host.MarshalOrderName(0);
        var heldAfter = _host.MarshalHeld(0);
        var flagAfter = _host.MarshalFlagOff(0);
        foreach (var verdict in HoldLineLift())
            yield return verdict;

        var reason = MarshalTrialRules.FlagTrialReason(qualified, before, restored);
        var facts = (reason.Length > 0 ? "reason=" + reason + " " : "") + "qualified=" + Whole(qualified) + " flag_off_before=" + Whole(before) + " flag_off_after=" + Whole(flagAfter)
            + " restored_in=" + Num(restoredIn) + " limit=" + Num(MarshalTrialRules.HoldRestoreSeconds) + " order=" + orderAfter + " held=" + Bit(heldAfter)
            + " units=" + Whole(ground.Units) + Faulted();
        yield return reason.Length == 0 && !_host.MarshalFaulted ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }
}
