using System;
using System.Collections.Generic;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

// Massed fire, hold the line (the proof slice), the hollow square and the spacing repair. Each runs as its own --steps subset, in its own battle.
internal sealed partial class CommandTrialScript
{
    private const float MassedWaitSeconds = 60f;
    private const float RestoreWaitSeconds = 5f;
    private const float SquareSettleSeconds = 8f;
    private const float RiderSettleSeconds = 12f;
    private const float SpacingVerdictWaitSeconds = 20f;

    private static void MarshalM5Steps(Dictionary<string, Func<CommandTrialScript, IEnumerable<Verdict>>> into)
    {
        into["volley_massed"] = s => s.VolleyMassed();
        into["square_side_kept"] = s => s.SquareSideKept();
        into["spacing_one_rider"] = s => s.SpacingOneRider();
        MarshalHoldSteps(into);
        MarshalThrowerSteps(into);
    }

    private sealed class VolleyGround
    {
        internal int Units;
        internal float Gap;
        internal int FreeShots;
        internal int FreeShooters;
    }

    // The ground both volley steps share: the archers walk to about 80 m from the nearest enemy company, and a control counts what they loose at free fire.
    private IEnumerable<Verdict> VolleyApproach(VolleyGround ground)
    {
        foreach (var verdict in MarshalBattle())
            yield return verdict;
        ground.Units = _host.MarshalChooseArchers();
        if (ground.Units == 0)
            yield return Verdict.Failed("reason=no-archers");
        if (_host.MarshalChooseNearestEnemy() == 0)
            yield return Verdict.Failed("reason=no-enemy");
        var walk = _host.MarshalPlanApproach(0, 0, MarshalTrialRules.VolleyReachMetres);
        if (float.IsNaN(walk))
            yield return Verdict.Failed("reason=no-point");
        var speed = _host.MarshalSpeed(0);
        var limit = MarshalTrialRules.ApproachWaitSeconds(walk, speed);

        yield return Verdict.InMissionTick;
        _host.MarshalSelect(0, 1);
        _host.MarshalMoveTo(0);
        foreach (var verdict in MarshalWaitSettled(1, limit, () => " walk=" + Num(walk) + " speed=" + Num(speed) + " limit=" + Num(limit) + " to_order=" + Num(_host.MarshalOrderGap(0))
            + " order=" + _host.MarshalOrderName(0) + " controllable=" + Bit(_host.MarshalControllable(0)) + " gap=" + Num(_host.MarshalEnemyDistance(0))))
            yield return verdict;
        ground.Gap = _host.MarshalEnemyDistance(0);
        if (_host.MarshalFireMode(0) != FireMode.Free)
            yield return Verdict.Failed("reason=not-free mode=" + _host.MarshalFireMode(0));

        var controlFrom = _host.MissionTime;
        var controlWaitedAt = _now;
        _host.MarshalWatchBegin(0);
        while (_host.MissionTime - controlFrom < MarshalTrialRules.ControlSeconds && _now - controlWaitedAt < ControlWaitSeconds)
            yield return Verdict.Waiting("free-fire-control");
        ground.FreeShots = _host.MarshalWatchShots;
        ground.FreeShooters = _host.MarshalWatchShooters;
        _host.MarshalWatchEnd();
    }

    private IEnumerable<Verdict> MarshalPressUntil(Func<bool> reached, string what, int units)
    {
        yield return Verdict.InMissionTick;
        _host.MarshalSelect(0, 1);
        _host.MarshalPressFire();
        var pressedAt = _now;
        while (!reached() && _now - pressedAt < SeatTrialRules.ShortWaitSeconds)
            yield return Verdict.Waiting(what);
        if (!reached())
            yield return Verdict.Failed("reason=" + what + "-not-set units=" + Whole(units) + " mode=" + _host.MarshalFireMode(0));
    }

    // Key 543 cycles three modes, so the archers come back to free fire in at most two presses.
    private IEnumerable<Verdict> MarshalBackToFree()
    {
        for (var presses = 0; presses < 2 && _host.MarshalFireMode(0) != FireMode.Free; presses++)
        {
            var before = _host.MarshalFireMode(0);
            _host.MarshalPressFire();
            var pressedAt = _now;
            while (_host.MarshalFireMode(0) == before && _now - pressedAt < SeatTrialRules.ShortWaitSeconds)
                yield return Verdict.Waiting("fire-mode");
        }
    }

    // Massed fire: the archers hold, and the word is given by the share rule with nobody pressing the word key. The control is the free-fire count of the ground
    // above, and the pass re-reads the share from the numbers the word was given on.
    private IEnumerable<Verdict> VolleyMassed()
    {
        var ground = new VolleyGround();
        foreach (var verdict in VolleyApproach(ground))
            yield return verdict;
        var wordsBefore = _host.MarshalMassedWords;
        var releasesBefore = _host.MarshalReleasesJudged;

        foreach (var verdict in MarshalPressUntil(() => _host.MarshalFireMode(0) == FireMode.OnWord, "on-word", ground.Units))
            yield return verdict;
        foreach (var verdict in MarshalPressUntil(() => _host.MarshalFireMode(0) == FireMode.Massed, "massed", ground.Units))
            yield return verdict;

        var peakHolding = 0;
        var peakDrawing = 0;
        var peakIdle = 0;
        var peakAside = 0;
        var waitedAt = _now;
        while (true)
        {
            peakHolding = Math.Max(peakHolding, _host.MarshalHolding(0));
            peakDrawing = Math.Max(peakDrawing, _host.MarshalDrawing(0));
            peakIdle = Math.Max(peakIdle, _host.MarshalIdle(0));
            peakAside = Math.Max(peakAside, _host.MarshalAside(0));
            if (_host.MarshalMassedWords != wordsBefore || _now - waitedAt >= MassedWaitSeconds)
                break;
            yield return Verdict.Waiting("massed-word");
        }

        var control = "units=" + Whole(ground.Units) + " gap=" + Num(ground.Gap) + " free_shots=" + Whole(ground.FreeShots) + " free_shooters=" + Whole(ground.FreeShooters);
        var peaks = " peak_holding=" + Whole(peakHolding) + " peak_drawing=" + Whole(peakDrawing) + " peak_idle=" + Whole(peakIdle) + " peak_aside=" + Whole(peakAside);
        var word = _host.MarshalLastMassed;
        if (_host.MarshalMassedWords == wordsBefore)
            yield return Verdict.Failed("reason=no-word " + control + peaks + Faulted());
        if (word == null)
            yield return Verdict.Failed("reason=word-not-recorded words=" + Whole(_host.MarshalMassedWords - wordsBefore) + " " + control + peaks + Faulted());

        var releasedAt = _now;
        while (_host.MarshalReleasesJudged == releasesBefore && _now - releasedAt < ReleaseWaitSeconds)
            yield return Verdict.Waiting("release-window");
        var release = _host.MarshalReleaseVerdict;
        var judged = release != null && _host.MarshalReleasesJudged > releasesBefore;
        foreach (var verdict in MarshalBackToFree())
            yield return verdict;

        var massed = word!.Value;
        var facts = control + " massed_holding=" + Whole(massed.Holding) + " eligible=" + Whole(massed.Eligible) + " stale=" + Whole(massed.StaleDrawing)
            + " judged=" + Bit(judged) + (judged ? " loosed=" + Whole(release!.Value.Shots) + " of=" + Whole(release.Value.Holding) : "") + peaks + Faulted();
        var pass = MarshalTrialRules.MassedPasses(_host.MarshalMassedWords - wordsBefore, massed.Holding, massed.Eligible, judged && release!.Value.Pass, _host.MarshalFaulted);
        yield return pass ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    // The hollow square (build-spec 9.1): a company of foot soldiers takes the plain engine square at a side the step asks for, a soldier is removed, and the side
    // stays within one unit's width while every soldier keeps a slot.
    private IEnumerable<Verdict> SquareSideKept()
    {
        foreach (var verdict in MarshalBattle())
            yield return verdict;
        var units0 = _host.MarshalChoosePureInfantry();
        if (units0 == 0)
            yield return Verdict.Failed("reason=no-infantry");

        yield return Verdict.InMissionTick;
        _host.MarshalSelect(0, 1);
        _host.MarshalOrderSquare();
        var squareAt = _now;
        while (!_host.MarshalPlainSquare(0) && _now - squareAt < SquareSettleSeconds)
            yield return Verdict.Waiting("square-arrangement");
        var plain = _host.MarshalPlainSquare(0);
        if (!plain)
            yield return Verdict.Failed("reason=" + MarshalTrialRules.SquareTrialReason(false, false, true, true, true, 0, 0, 1) + " type=" + _host.MarshalArrangementName(0) + " units=" + Whole(units0));

        var unit = _host.MarshalUnitWidth(0);
        var sideBefore = _host.MarshalSquareSide(0);
        var chosen = MarshalTrialRules.SideTarget(sideBefore, _host.MarshalSquareMinimum(0), _host.MarshalSquareMaximum(0), unit);
        if (float.IsNaN(chosen))
            yield return Verdict.Failed("reason=no-side before=" + Num(sideBefore) + " min=" + Num(_host.MarshalSquareMinimum(0)) + " max=" + Num(_host.MarshalSquareMaximum(0)) + " unit=" + Num(unit));

        yield return Verdict.InMissionTick;
        _host.MarshalSelect(0, 1);
        _host.MarshalPlanSideDrag(0, chosen);
        _host.MarshalDrag();
        var sideAt = _now;
        while (!MarshalTrialRules.SideKept(chosen, _host.MarshalSquareSide(0), unit) && _now - sideAt < SquareSettleSeconds)
            yield return Verdict.Waiting("square-side");
        var side0 = _host.MarshalSquareSide(0);
        var reached = MarshalTrialRules.SideKept(chosen, side0, unit);
        var dragMoved = MarshalTrialRules.SideMoved(sideBefore, side0, unit);
        units0 = _host.MarshalUnits(0);

        yield return Verdict.InMissionTick;
        var killed = _host.MarshalKillOne(0);
        var killedAt = _now;
        while (killed && _host.MarshalUnits(0) >= units0 && _now - killedAt < SeatTrialRules.ShortWaitSeconds)
            yield return Verdict.Waiting("unit-removed");
        var settledAt = _now;
        while (_now - settledAt < SeatTrialRules.ShortWaitSeconds / 2f)
            yield return Verdict.Waiting("square-settles");

        var side1 = _host.MarshalSquareSide(0);
        var units1 = _host.MarshalUnits(0);
        var placed1 = _host.MarshalPlaced(0);
        var plainAfter = _host.MarshalPlainSquare(0);
        var laidOut = _host.MarshalSquareLaidOut(0);
        var reason = MarshalTrialRules.SquareTrialReason(plainAfter, laidOut, dragMoved, reached, MarshalTrialRules.SideKept(side0, side1, unit), placed1, units1, units0 - units1);
        var facts = (reason.Length > 0 ? "reason=" + reason + " " : "") + "type=" + _host.MarshalArrangementName(0) + " before=" + Num(sideBefore) + " chosen=" + Num(chosen) + " side0=" + Num(side0)
            + " side1=" + Num(side1) + " unit=" + Num(unit) + " units0=" + Whole(units0) + " units1=" + Whole(units1) + " placed=" + Whole(placed1) + " laid_out=" + Bit(laidOut) + Faulted();
        yield return reason.Length == 0 && !_host.MarshalFaulted ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    // The spacing repair (build-spec 9.3): one rider joins a company of foot soldiers, and the company's four limits must read as a foot company's. The repair is a
    // transpiler applied when the first mission starts, from the saved settings, so the step cannot switch it on: a run whose saved Marshal's Word or Spacing Repair
    // setting is off, or that runs under co-op or on a dedicated server, fails here with the setting named in why=.
    private IEnumerable<Verdict> SpacingOneRider()
    {
        foreach (var verdict in MarshalBattle())
            yield return verdict;
        var before = _host.MarshalChoosePureInfantry();
        if (before == 0)
            yield return Verdict.Failed("reason=no-infantry");
        yield return Verdict.InMissionTick;
        if (_host.MarshalMoveOneRider() == 0)
            yield return Verdict.Failed("reason=no-rider-company");

        var movedAt = _now;
        while (SpacingReason(before).Length > 0 && _now - movedAt < RiderSettleSeconds)
            yield return Verdict.Waiting("rider-counted");
        var units = _host.MarshalUnits(0);
        var riders = _host.MarshalRiders(0);
        var any = _host.MarshalHasAnyRider(0);
        var significant = _host.MarshalHasSignificantRiders(0);
        var reason = MarshalTrialRules.SpacingTrialReason(before, units, riders, any, significant);
        if (reason.Length > 0)
            yield return Verdict.Failed("reason=" + reason + " before=" + Whole(before) + " units=" + Whole(units) + " riders=" + Whole(riders) + " any=" + Bit(any)
                + " significant=" + Bit(significant));

        var why = _host.MarshalSpacingWhy;
        if (why.Length > 0)
            yield return Verdict.Failed("reason=repair-not-applied why=" + why + " riders=" + Whole(riders) + " units=" + Whole(units));

        var read = _host.MarshalSpacingOfChosen(0);
        var reading = read == null ? SpacingReading.NoTest : MarshalSpacingRules.Read(read.Value.Measured, read.Value.Infantry, read.Value.Cavalry);
        var driverAt = _now;
        while (_host.MarshalSpacingVerdict == null && _now - driverAt < SpacingVerdictWaitSeconds)
            yield return Verdict.Waiting("spacing-verdict");

        var facts = "reads=" + reading + " riders=" + Whole(riders) + " units=" + Whole(units) + " any=" + Bit(any) + " significant=" + Bit(significant) + " why=none"
            + " driver=" + (_host.MarshalSpacingVerdict == null ? "none" : _host.MarshalSpacingVerdict.Value.ToString()) + Faulted();
        yield return reading == SpacingReading.Infantry && _host.MarshalSpacingVerdict == SpacingReading.Infantry && !_host.MarshalFaulted ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private string SpacingReason(int before) =>
        MarshalTrialRules.SpacingTrialReason(before, _host.MarshalUnits(0), _host.MarshalRiders(0), _host.MarshalHasAnyRider(0), _host.MarshalHasSignificantRiders(0));
}
