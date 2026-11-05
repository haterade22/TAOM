using System;
using System.Collections.Generic;
using System.Globalization;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

internal sealed partial class CommandTrialScript
{
    private const float LegWalkSeconds = 45f;
    private const float MarchWaitSeconds = 150f;
    private const float HoldWaitSeconds = 100f;
    private const float WordWaitSeconds = 25f;
    private const float ReleaseWaitSeconds = 6f;
    private const float MarkReachWaitSeconds = 120f;
    private const float MarkJudgeWaitSeconds = 15f;
    private const float ArraySettleSeconds = 3f;
    private const float ArrayArriveSeconds = 30f;
    private const float QueueSettleSeconds = 0.6f;
    private const float SettleHoldSeconds = 1f;
    private const float SettleLimitSeconds = 25f;
    private const float StageSettleSeconds = 40f;
    private const float ApproachSettleSeconds = 45f;
    private const float ControlWaitSeconds = 25f;

    private static void MarshalSteps(Dictionary<string, Func<CommandTrialScript, IEnumerable<Verdict>>> into)
    {
        into["queue_two_moves"] = s => s.QueueTwoMoves();
        into["direct_order_clears"] = s => s.DirectOrderClears();
        into["array_keeps_offsets"] = s => s.ArrayKeepsOffsets();
        into["arrive_together"] = s => s.ArriveTogether();
        into["volley_holds"] = s => s.VolleyHolds();
        into["volley_looses"] = s => s.VolleyLooses();
        into["mark_keeps_order"] = s => s.MarkKeepsOrder();
        into["outline_clears"] = s => s.OutlineClears();
    }

    private string Faulted() => " faulted=" + Bit(_host.MarshalFaulted);

    private static string Whole(int value) => value.ToString(CultureInfo.InvariantCulture);

    private IEnumerable<Verdict> MarshalBattle()
    {
        var since = float.NaN;
        while (true)
        {
            if (_host.MarshalFaulted)
                yield return Verdict.Failed("reason=session-faulted");
            if (!_host.InBattle || !_host.MarshalLive)
                since = float.NaN;
            else if (float.IsNaN(since))
                since = _now;

            if (!float.IsNaN(since) && _now - since >= BattleSettleSeconds)
                yield break;
            yield return Verdict.Waiting("marshal-battle");
        }
    }

    private IEnumerable<Verdict> MarshalOpenWith(int first, int count)
    {
        yield return Verdict.InMissionTick;
        _host.MarshalSelect(first, count);
        if (!_host.OpenPanel())
            yield return Verdict.Failed("reason=panel-not-opened");
        var openedAt = _now;
        while (_now - openedAt < PanelSettleSeconds)
            yield return Verdict.Waiting("panel-seen");
        if (!_host.PanelOpen)
            yield return Verdict.Failed("reason=panel-closed");
    }

    private IEnumerable<Verdict> MarshalClosePanel()
    {
        yield return Verdict.InMissionTick;
        _host.ClosePanel(asPlayer: true);
    }

    // The view arms a held key in its next screen tick, so a step waits for the static the patch reads.
    private IEnumerable<Verdict> MarshalWaitFor(Func<bool> armed, string what)
    {
        var heldAt = _now;
        while (!armed() && _now - heldAt < SeatTrialRules.ShortWaitSeconds)
            yield return Verdict.Waiting(what);
        if (!armed())
            yield return Verdict.Failed("reason=" + what + "-never panel=" + Bit(_host.PanelOpen));
    }

    // A direct order reaches the player's controller's OnOrderIssued, and the panel's listener there ends by closing the panel (MissionOrderTroopControllerVM.cs:827).
    // A player opens it again to hold a key over it, so a step does the same; a captured order never reaches that listener. The step then holds the key and waits
    // for the static the patch reads, which the view sets only with the panel open.
    private IEnumerable<Verdict> MarshalReopenAndHold(int first, int count, Action<bool> hold, Func<bool> armed, string what)
    {
        foreach (var verdict in MarshalOpenWith(first, count))
            yield return verdict;
        hold(true);
        foreach (var verdict in MarshalWaitFor(armed, what))
            yield return verdict;
    }

    // A step that gives an order for the capture to take reads, in the mission tick that gives it, that the panel is open and the key armed. An order given with the
    // panel shut is a direct order, whatever the step goes on to read.
    private string MarshalOrderNotArmed(bool armed) =>
        _host.PanelOpen && armed ? "" : "reason=not-armed-at-order panel=" + Bit(_host.PanelOpen) + " armed=" + Bit(armed);

    // The chosen companies stand at their order points, or hold none, for a moment: an earlier step's walk is over, so what a step reads next is what they hold.
    private IEnumerable<Verdict> MarshalWaitSettled(int count, float limitSeconds = SettleLimitSeconds)
    {
        var startedAt = _now;
        var stillSince = float.NaN;
        while (_now - startedAt < limitSeconds)
        {
            var settled = true;
            for (var company = 0; company < count; company++)
                settled &= _host.MarshalSettled(company);
            if (!settled)
                stillSince = float.NaN;
            else if (float.IsNaN(stillSince))
                stillSince = _now;
            if (!float.IsNaN(stillSince) && _now - stillSince >= SettleHoldSeconds)
                yield break;
            yield return Verdict.Waiting("settle");
        }

        yield return Verdict.Failed("reason=not-settled first=" + Bit(_host.MarshalSettled(0)) + (count > 1 ? " second=" + Bit(_host.MarshalSettled(1)) : ""));
    }

    private IEnumerable<Verdict> MarshalStageColumn()
    {
        if (!_host.MarshalPlanPoint(0, 0, -MarshalTrialRules.ColumnBehindMetres, MarshalTrialRules.ColumnSideMetres))
            yield return Verdict.Failed("reason=no-point");
        yield return Verdict.InMissionTick;
        _host.MarshalSelect(1, 1);
        _host.MarshalMoveTo(0);
        foreach (var verdict in MarshalWaitSettled(2, StageSettleSeconds))
            yield return verdict;
    }

    private IEnumerable<Verdict> QueueTwoMoves()
    {
        foreach (var verdict in MarshalBattle())
            yield return verdict;
        if (_host.MarshalChooseCompanies(1) == 0)
            yield return Verdict.Failed("reason=no-company");
        if (!_host.MarshalPlanPoint(0, 0, MarshalTrialRules.LegMetres, 0f) || !_host.MarshalPlanPoint(1, 0, MarshalTrialRules.LegMetres, MarshalTrialRules.LegMetres))
            yield return Verdict.Failed("reason=no-point");

        foreach (var verdict in MarshalOpenWith(0, 1))
            yield return verdict;
        var capturedBefore = _host.MarshalStepsCaptured;
        var finishedBefore = _host.MarshalStepsFinished;
        var patienceBefore = _host.MarshalStepsByPatience;
        var arrivalBefore = _host.MarshalStepsByArrival;
        yield return Verdict.InMissionTick;
        _host.MarshalMoveTo(0);
        foreach (var verdict in MarshalReopenAndHold(0, 1, _host.MarshalHoldQueue, () => _host.MarshalQueueArmed, "queue-armed"))
            yield return verdict;

        yield return Verdict.InMissionTick;
        var notArmed = MarshalOrderNotArmed(_host.MarshalQueueArmed);
        if (notArmed.Length > 0)
            yield return Verdict.Failed(notArmed);
        _host.MarshalMoveTo(1);
        var captured = _host.MarshalStepsCaptured - capturedBefore;
        var queued = _host.MarshalQueuedSteps(0);
        _host.MarshalHoldQueue(false);

        // An open panel slows mission time to a quarter by default and the leg limit counts real seconds, so the panel is shut before the walk.
        foreach (var verdict in MarshalClosePanel())
            yield return verdict;

        var startedAt = _now;
        var first = float.NaN;
        var second = float.NaN;
        while (float.IsNaN(second) && _now - startedAt < LegWalkSeconds)
        {
            if (float.IsNaN(first) && _host.MarshalDistanceToPoint(0, 0) <= MarshalTuning.ArrivalSlack)
                first = _now - startedAt;
            else if (!float.IsNaN(first) && _host.MarshalDistanceToPoint(0, 1) <= MarshalTuning.ArrivalSlack)
                second = _now - startedAt;
            if (float.IsNaN(second))
                yield return Verdict.Waiting("walk");
        }

        var settledAt = _now;
        while (_host.MarshalQueuedSteps(0) > 0 && _now - settledAt < SeatTrialRules.ShortWaitSeconds)
            yield return Verdict.Waiting("queue-empty");

        var finished = _host.MarshalStepsFinished - finishedBefore;
        var byPatience = _host.MarshalStepsByPatience - patienceBefore;
        var byArrival = _host.MarshalStepsByArrival - arrivalBefore;
        var left = _host.MarshalQueuedSteps(0);
        var facts = "captured=" + captured.ToString(CultureInfo.InvariantCulture) + " queued=" + queued.ToString(CultureInfo.InvariantCulture)
            + " first_at=" + Num(first) + " second_at=" + Num(second)
            + " finished=" + finished.ToString(CultureInfo.InvariantCulture) + " arrival=" + byArrival.ToString(CultureInfo.InvariantCulture)
            + " by_patience=" + byPatience.ToString(CultureInfo.InvariantCulture) + " left=" + left.ToString(CultureInfo.InvariantCulture) + Faulted();

        // The contract's rule: the capture took the second Move, the company reached the first point and then the second, and both steps (the wait for the
        // first Move, then the queued Move) ended with reason=arrival, none by patience.
        var pass = captured >= 1 && queued >= 1 && MarshalTrialRules.ReachedInOrder(first, second) && byArrival >= 2 && byArrival == finished
            && byPatience == 0 && left == 0 && !_host.MarshalFaulted;
        yield return pass ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private IEnumerable<Verdict> DirectOrderClears()
    {
        foreach (var verdict in MarshalBattle())
            yield return verdict;
        if (_host.MarshalChooseCompanies(1) == 0)
            yield return Verdict.Failed("reason=no-company");
        if (!_host.MarshalPlanPoint(0, 0, MarshalTrialRules.LegMetres, 0f) || !_host.MarshalPlanPoint(1, 0, MarshalTrialRules.LegMetres, MarshalTrialRules.LegMetres)
            || !_host.MarshalPlanPoint(2, 0, 0f, MarshalTrialRules.LegMetres * 2f))
            yield return Verdict.Failed("reason=no-point");

        foreach (var verdict in MarshalOpenWith(0, 1))
            yield return verdict;
        var capturedBefore = _host.MarshalStepsCaptured;
        yield return Verdict.InMissionTick;
        _host.MarshalMoveTo(0);
        foreach (var verdict in MarshalReopenAndHold(0, 1, _host.MarshalHoldQueue, () => _host.MarshalQueueArmed, "queue-armed"))
            yield return verdict;

        yield return Verdict.InMissionTick;
        var notArmed = MarshalOrderNotArmed(_host.MarshalQueueArmed);
        if (notArmed.Length > 0)
            yield return Verdict.Failed(notArmed);
        _host.MarshalMoveTo(1);
        var captured = _host.MarshalStepsCaptured - capturedBefore;
        _host.MarshalHoldQueue(false);
        var letGoAt = _now;
        while ((_host.MarshalQueueArmed || _now - letGoAt < QueueSettleSeconds) && _now - letGoAt < SeatTrialRules.ShortWaitSeconds)
            yield return Verdict.Waiting("key-released-and-wait-step");

        var before = _host.MarshalQueuedSteps(0);
        yield return Verdict.InMissionTick;
        _host.MarshalMoveTo(2);
        var after = _host.MarshalQueuedSteps(0);
        foreach (var verdict in MarshalClosePanel())
            yield return verdict;

        var facts = "captured=" + captured.ToString(CultureInfo.InvariantCulture) + " before=" + before.ToString(CultureInfo.InvariantCulture) + " after="
            + after.ToString(CultureInfo.InvariantCulture) + " armed=" + Bit(_host.MarshalQueueArmed) + Faulted();
        var pass = captured == 1 && before >= 1 && after == 0 && !_host.MarshalQueueArmed && !_host.MarshalFaulted;
        yield return pass ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private IEnumerable<Verdict> ArrayKeepsOffsets()
    {
        foreach (var verdict in MarshalBattle())
            yield return verdict;
        if (_host.MarshalChooseCompanies(2) == 0)
            yield return Verdict.Failed("reason=no-two-companies");

        // An earlier step can leave a company walking, and the solver keeps the array the player last ordered (6.2), so the step starts from companies that stand.
        foreach (var verdict in MarshalWaitSettled(2))
            yield return verdict;

        // The second company walks to a place MarshalTrialRules.LegMetres ahead of the first, whatever the two stood like before, so a side by side layout has to move one.
        var ahead = _host.MarshalAhead(0, 1);
        if (float.IsNaN(ahead) || !_host.MarshalPlanPoint(0, 1, MarshalTrialRules.LegMetres - ahead, 0f))
            yield return Verdict.Failed("reason=no-point ahead=" + Num(ahead));

        foreach (var verdict in MarshalOpenWith(1, 1))
            yield return verdict;
        yield return Verdict.InMissionTick;
        _host.MarshalMoveTo(0);
        foreach (var verdict in MarshalWaitSettled(2))
            yield return verdict;

        // The drag is the group's own front moved sideways: each order point must follow by that shift, and a layout never applied moves nobody.
        var drag = _host.MarshalPlanFrontDrag(MarshalTrialRules.ShiftMetres);
        var offsetBefore = _host.MarshalOffset(0, 1);
        var orderedBefore = _host.MarshalOrderedOffset(0, 1);
        var pointBeforeFirst = _host.MarshalOrderPoint(0);
        var pointBeforeSecond = _host.MarshalOrderPoint(1);
        var arraysBefore = _host.MarshalArraysSolved;
        if (drag == null || offsetBefore == null || orderedBefore == null || pointBeforeFirst == null || pointBeforeSecond == null)
            yield return Verdict.Failed("reason=no-front");

        yield return Verdict.InMissionTick;
        _host.MarshalSelect(0, 2);
        _host.MarshalDrag();
        var draggedAt = _now;
        while (_now - draggedAt < ArraySettleSeconds)
            yield return Verdict.Waiting("array-settles");
        foreach (var verdict in MarshalWaitSettled(2))
            yield return verdict;

        var offsetAfter = _host.MarshalOffset(0, 1);
        var orderedAfter = _host.MarshalOrderedOffset(0, 1);
        var pointAfterFirst = _host.MarshalOrderPoint(0);
        var pointAfterSecond = _host.MarshalOrderPoint(1);
        var arrays = _host.MarshalArraysSolved - arraysBefore;
        foreach (var verdict in MarshalClosePanel())
            yield return verdict;
        var plan = drag!.Value;
        var before = offsetBefore!.Value;
        var after = offsetAfter ?? new Vec3f(float.NaN, float.NaN, 0f);
        var shift = MarshalTrialRules.OffsetShift(before.X, before.Y, after.X, after.Y);
        var orderedFrom = orderedBefore!.Value;
        var orderedTo = orderedAfter ?? new Vec3f(float.NaN, float.NaN, 0f);
        var ordered = MarshalTrialRules.OffsetShift(orderedFrom.X, orderedFrom.Y, orderedTo.X, orderedTo.Y);
        var miss = Math.Max(MissOf(plan, pointBeforeFirst!.Value, pointAfterFirst), MissOf(plan, pointBeforeSecond!.Value, pointAfterSecond));

        // shift and ordered are offsets between the companies, standing and ordered; miss is the larger distance of an order point from where the shift put it.
        var planned = (float)Math.Sqrt(plan.ShiftX * plan.ShiftX + plan.ShiftY * plan.ShiftY);
        var facts = "stagger=" + Num(plan.Stagger) + " shift=" + Num(shift) + " ordered=" + Num(ordered) + " planned=" + Num(planned) + " miss=" + Num(miss)
            + " arrays=" + arrays.ToString(CultureInfo.InvariantCulture) + Faulted();
        var pass = MarshalTrialRules.StaggerProves(plan.Stagger) && MarshalTrialRules.ShiftProves(plan.ShiftX, plan.ShiftY)
            && MarshalTrialRules.OffsetKept(before.X, before.Y, after.X, after.Y) && MarshalTrialRules.OffsetKept(orderedFrom.X, orderedFrom.Y, orderedTo.X, orderedTo.Y)
            && MarshalTrialRules.ShiftFollowed(miss) && arrays >= 1 && !_host.MarshalFaulted;
        yield return pass ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private static float MissOf(FrontDrag plan, Vec3f from, Vec3f? to) =>
        to == null ? float.NaN : MarshalTrialRules.ShiftMiss(from.X, from.Y, plan.ShiftX, plan.ShiftY, to.Value.X, to.Value.Y);

    private IEnumerable<Verdict> ArriveTogether()
    {
        foreach (var verdict in MarshalBattle())
            yield return verdict;
        if (_host.MarshalChooseCompanies(2) == 0)
            yield return Verdict.Failed("reason=no-two-companies");
        if (!_host.MarshalInLine(0) || !_host.MarshalInLine(1))
            yield return Verdict.Failed("reason=not-line first=" + Bit(_host.MarshalInLine(0)) + " second=" + Bit(_host.MarshalInLine(1)));

        // The plan counts the walks from the order points and the march measures them from where the companies stand, so they must stand at their points.
        foreach (var verdict in MarshalWaitSettled(2))
            yield return verdict;
        foreach (var verdict in MarshalStageColumn())
            yield return verdict;
        var plan = _host.MarshalPlanArrivalDrag();
        if (plan == null)
            yield return Verdict.Failed("reason=no-plan speed=" + Num(Math.Min(_host.MarshalSpeed(0), _host.MarshalSpeed(1))) + " apart=" + Num(_host.MarshalAhead(0, 1)));

        foreach (var verdict in MarshalOpenWith(0, 2))
            yield return verdict;
        _host.MarshalHoldWidths(true);
        foreach (var verdict in MarshalWaitFor(() => _host.MarshalWidthsArmed, "widths-armed"))
            yield return verdict;

        var concludedBefore = _host.MarshalMarchesConcluded;
        var arraysBefore = _host.MarshalArraysSolved;
        yield return Verdict.InMissionTick;
        _host.MarshalDrag();
        _host.MarshalHoldWidths(false);
        var draggedAt = _now;
        while (_host.MarshalMarchesConcluded == concludedBefore && _now - draggedAt < MarchWaitSeconds)
            yield return Verdict.Waiting("march");

        var conclusion = _host.MarshalMarchConclusion;
        var arrays = _host.MarshalArraysSolved - arraysBefore;
        foreach (var verdict in MarshalClosePanel())
            yield return verdict;
        var facts = "conclusion=" + (_host.MarshalMarchesConcluded == concludedBefore ? "none" : conclusion) + " arrays=" + arrays.ToString(CultureInfo.InvariantCulture)
            + " " + MarshalTrialRules.Describe(plan!.Value) + " seconds=" + Num(_now - draggedAt) + Faulted();
        var pass = _host.MarshalMarchesConcluded > concludedBefore && conclusion == "pass" && arrays >= 1 && !_host.MarshalFaulted;
        yield return pass ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private IEnumerable<Verdict> VolleyHolds()
    {
        foreach (var verdict in MarshalBattle())
            yield return verdict;
        var units = _host.MarshalChooseArchers();
        if (units == 0)
            yield return Verdict.Failed("reason=no-archers");
        if (_host.MarshalChooseNearestEnemy() == 0)
            yield return Verdict.Failed("reason=no-enemy");
        if (float.IsNaN(_host.MarshalPlanApproach(0, 0, MarshalTrialRules.VolleyReachMetres)))
            yield return Verdict.Failed("reason=no-point");

        yield return Verdict.InMissionTick;
        _host.MarshalSelect(0, 1);
        _host.MarshalMoveTo(0);
        foreach (var verdict in MarshalWaitSettled(1, ApproachSettleSeconds))
            yield return verdict;
        var gap = _host.MarshalEnemyDistance(0);
        if (_host.MarshalFireMode(0) != FireMode.Free)
            yield return Verdict.Failed("reason=not-free mode=" + _host.MarshalFireMode(0));

        var controlFrom = _host.MissionTime;
        var controlWaitedAt = _now;
        _host.MarshalWatchBegin(0);
        while (_host.MissionTime - controlFrom < MarshalTrialRules.ControlSeconds && _now - controlWaitedAt < ControlWaitSeconds)
            yield return Verdict.Waiting("free-fire-control");
        var freeShots = _host.MarshalWatchShots;
        var freeShooters = _host.MarshalWatchShooters;
        _host.MarshalWatchEnd();

        yield return Verdict.InMissionTick;
        _host.MarshalSelect(0, 1);
        _host.MarshalPressFire();
        var pressedAt = _now;
        while (_host.MarshalFireMode(0) != FireMode.OnWord && _now - pressedAt < SeatTrialRules.ShortWaitSeconds)
            yield return Verdict.Waiting("fire-mode");
        if (_host.MarshalFireMode(0) != FireMode.OnWord)
            yield return Verdict.Failed("reason=mode-not-set units=" + Whole(units));

        var mostHolding = 0;
        var peakIdle = 0;
        var peakAside = 0;
        var waitedAt = _now;
        while (_host.MarshalHoldVerdict == null && _now - waitedAt < HoldWaitSeconds)
        {
            mostHolding = Math.Max(mostHolding, _host.MarshalHolding(0));
            peakIdle = Math.Max(peakIdle, _host.MarshalIdle(0));
            peakAside = Math.Max(peakAside, _host.MarshalAside(0));
            yield return Verdict.Waiting("hold-window");
        }

        var control = "units=" + Whole(units) + " gap=" + Num(gap) + " free_shots=" + Whole(freeShots) + " free_shooters=" + Whole(freeShooters);
        var census = " most_holding=" + Whole(mostHolding) + " peak_idle=" + Whole(peakIdle) + " peak_aside=" + Whole(peakAside);
        var verdictOfHold = _host.MarshalHoldVerdict;
        if (verdictOfHold == null)
            yield return Verdict.Failed("reason=no-window " + control + census + Faulted());
        var hold = verdictOfHold!.Value;

        // held is the window's smallest count and reads near five on a count that climbs; most_holding is the most any census showed.
        var facts = control + " held=" + Whole(hold.Holding) + " most_holding=" + Whole(Math.Max(mostHolding, hold.Holding)) + " peak_idle=" + Whole(peakIdle)
            + " peak_aside=" + Whole(peakAside) + " shots=" + Whole(hold.Shots) + " mode=" + _host.MarshalFireMode(0) + Faulted();
        yield return hold.Pass && hold.Holding >= MarshalTuning.VolleyMinHolding && !_host.MarshalFaulted ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private IEnumerable<Verdict> VolleyLooses()
    {
        if (_host.MarshalFireMode(0) != FireMode.OnWord)
            yield return Verdict.Failed("reason=not-on-word mode=" + _host.MarshalFireMode(0));

        var waitedAt = _now;
        while (_host.MarshalHolding(0) < MarshalTuning.VolleyMinHolding && _now - waitedAt < WordWaitSeconds)
            yield return Verdict.Waiting("holding");
        var holding = _host.MarshalHolding(0);
        var previous = _host.MarshalReleaseVerdict;
        _host.MarshalPressWord();
        var wordAt = _now;
        while (_host.MarshalReleaseVerdict == previous && _now - wordAt < ReleaseWaitSeconds)
            yield return Verdict.Waiting("release-window");

        var release = _host.MarshalReleaseVerdict;
        _host.MarshalPressFire();
        var facts = "holding=" + holding.ToString(CultureInfo.InvariantCulture) + " judged=" + Bit(release != previous && release != null)
            + (release != null ? " loosed=" + release.Value.Shots.ToString(CultureInfo.InvariantCulture) + " of=" + release.Value.Holding.ToString(CultureInfo.InvariantCulture) : "")
            + Faulted();
        var pass = release != null && release != previous && release.Value.Pass && !_host.MarshalFaulted;
        yield return pass ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    // Build-spec 8.1: a mark keeps a ranged company's order and steers its archers. The step puts the archers on a Stop, marks an enemy company that is within reach of
    // their bows so that the sampled soldiers have targets, keeps the order, and passes only when the watch judged the case and the judgment was a pass.
    private IEnumerable<Verdict> MarkKeepsOrder()
    {
        foreach (var verdict in MarshalBattle())
            yield return verdict;
        if (_host.MarshalChooseArchers() == 0)
            yield return Verdict.Failed("reason=no-archers");
        if (_host.MarshalChooseEnemy() == 0)
            yield return Verdict.Failed("reason=no-enemy");

        var reach = MarshalTrialRules.ArcherReachMetres;
        var reachWaitedAt = _now;
        while (!(_host.MarshalEnemyDistance(0) <= reach) && _now - reachWaitedAt < MarkReachWaitSeconds)
            yield return Verdict.Waiting("enemy-in-reach");
        var distance = _host.MarshalEnemyDistance(0);
        if (!(distance <= reach))
            yield return Verdict.Failed("reason=enemy-out-of-reach distance=" + Num(distance) + " reach=" + Num(reach) + Faulted());

        foreach (var verdict in MarshalOpenWith(0, 1))
            yield return verdict;
        yield return Verdict.InMissionTick;
        _host.MarshalStandGround();
        var before = _host.MarshalOrderName(0);
        foreach (var verdict in MarshalReopenAndHold(0, 1, _host.MarshalHoldMark, () => _host.MarshalMarkArmed, "mark-armed"))
            yield return verdict;

        yield return Verdict.InMissionTick;
        var notArmed = MarshalOrderNotArmed(_host.MarshalMarkArmed);
        if (notArmed.Length > 0)
            yield return Verdict.Failed(notArmed);
        _host.MarshalChargeEnemy();
        _host.MarshalHoldMark(false);
        var markedAt = _now;
        while (!_host.MarshalMarkedEnemy(0) && _now - markedAt < SeatTrialRules.ShortWaitSeconds)
            yield return Verdict.Waiting("mark-applied");
        var marked = _host.MarshalMarkedEnemy(0);

        // The watch counts mission seconds, which an open panel slows to a quarter, so the panel is shut before the wait.
        foreach (var verdict in MarshalClosePanel())
            yield return verdict;
        var judgedAt = _now;
        while (_host.MarshalMarkJudged(0) == null && _now - judgedAt < MarkJudgeWaitSeconds)
            yield return Verdict.Waiting("mark-effect");

        var order = _host.MarshalOrderName(0);
        var judged = _host.MarshalMarkJudged(0);
        var read = _host.MarshalMarkRead(0);
        var why = judged != null ? "" : read == null ? "no-read" : read.Value.Context != MarkContext.RangedOnStop ? "wrong-context"
            : read.Value.Of < MarshalTuning.MarkMinSampled ? "too-few-targets" : "no-verdict";
        var shown = judged != null ? judged.Value.Sample : read;
        var facts = (why.Length > 0 ? "reason=" + why + " " : "") + "marked=" + Bit(marked) + " before=" + before + " order=" + order
            + " context=" + (shown == null ? "none" : shown.Value.Context.ToString())
            + " onTarget=" + (shown == null ? "none" : shown.Value.OnTarget.ToString(CultureInfo.InvariantCulture))
            + " of=" + (shown == null ? "none" : shown.Value.Of.ToString(CultureInfo.InvariantCulture))
            + " effect=" + (judged == null ? "none" : judged.Value.Pass ? "pass" : "fail") + " distance=" + Num(distance) + Faulted();
        var pass = marked && before == "Stop" && order == "Stop" && judged != null && judged.Value.Sample.Context == MarkContext.RangedOnStop && judged.Value.Pass
            && !_host.MarshalFaulted;
        yield return pass ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private IEnumerable<Verdict> OutlineClears()
    {
        foreach (var verdict in MarshalBattle())
            yield return verdict;
        if (_host.MarshalChooseCompanies(1) == 0)
            yield return Verdict.Failed("reason=no-company");

        foreach (var verdict in MarshalOpenWith(0, 1))
            yield return verdict;
        var shownAt = _now;
        while (_host.MarshalOutlinesPainted == 0 && _now - shownAt < SeatTrialRules.ShortWaitSeconds * 2f)
            yield return Verdict.Waiting("outlines-shown");
        var painted = _host.MarshalOutlinesPainted;

        foreach (var verdict in MarshalClosePanel())
            yield return verdict;
        var closedAt = _now;
        while (_host.MarshalOutlinesPainted > 0 && _now - closedAt < SeatTrialRules.ShortWaitSeconds)
            yield return Verdict.Waiting("outlines-cleared");

        var left = _host.MarshalOutlinesPainted;
        var facts = "painted=" + painted.ToString(CultureInfo.InvariantCulture) + " left=" + left.ToString(CultureInfo.InvariantCulture) + " panel=" + Bit(_host.PanelOpen) + Faulted();
        yield return painted > 0 && left == 0 && !_host.PanelOpen && !_host.MarshalFaulted ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }
}
