using System.Collections.Generic;
using TAOM.Features.MarshalsWord.Domain;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

/// <summary>
/// The six command mouse steps of the field trial (Seat build-spec 12): a click that selects, a Shift click that toggles, a right click that moves, a
/// right drag that draws a line and a facing, a right click on an enemy that charges, and a Shift right click that queues. The pointer is injected into the
/// Seat's own pointer path (never an operating system click), so each step runs the screen tick's capture, the mission tick's pick and the real
/// order controller. What the steps cannot prove is the feel of the real mouse, the pixels at a UI scale other than 1 and the look of the preview line:
/// those stay with a person.
/// </summary>
internal sealed partial class CommandTrialScript
{
    private const float MouseSettleSeconds = 0.5f;

    // The command mouse turned on for the step: the value is in memory only, and the step waits for the Seat to take it up and show its cursor.
    private IEnumerable<Verdict> MouseLive()
    {
        _host.SetCommandMouse(true);
        var startedAt = _now;
        while (!(_host.CommandLive && _host.MouseVisible) && _now - startedAt < SeatTrialRules.ShortWaitSeconds)
            yield return Verdict.Waiting("command-live");
        if (!_host.CommandLive)
            yield return Verdict.Failed("reason=not-live cursor=" + Bit(_host.MouseVisible) + Standing());
    }

    private IEnumerable<Verdict> MouseWait(System.Func<bool> done)
    {
        var startedAt = _now;
        while (!done() && _now - startedAt < MouseSettleSeconds)
            yield return Verdict.Waiting("mouse-order");
    }

    // A soldier of the player's side is chosen, the view turns onto him and he is found on the screen.
    private IEnumerable<Verdict> MouseAimAtOwn(bool otherCompany, string what)
    {
        yield return Verdict.InMissionTick;
        if (!_host.MouseChooseSoldier(otherCompany))
            yield return Verdict.Failed("reason=no-" + what);
        foreach (var verdict in Aim())
            yield return verdict;
    }

    private string MousePointFacts(SeatMousePoint at) =>
        " pixel=" + Num(at.PixelX) + "," + Num(at.PixelY) + " ranged=" + Num(at.RangedX) + "," + Num(at.RangedY);

    private IEnumerable<Verdict> SelectClick()
    {
        if (!Seated)
            yield return NotSeated();
        foreach (var verdict in MouseAimAtOwn(false, "soldier"))
            yield return verdict;
        foreach (var verdict in MouseLive())
            yield return verdict;

        yield return Verdict.InMissionTick;
        _host.ClearSelectedCompanies();
        var at = _host.MouseProjectChosen();
        if (!at.Found)
            yield return Verdict.Failed("reason=off-screen" + Standing());

        _host.MouseClick(at, right: false, shift: false, ctrl: false);
        foreach (var verdict in MouseWait(() => _host.MouseSelectedCount > 0))
            yield return verdict;

        var only = _host.MouseOnlyFirstSelected;
        var facts = "only_chosen=" + Bit(only) + " selected=" + Whole(_host.MouseSelectedCount) + MousePointFacts(at) + Standing();
        yield return SeatTrialMouse.Selected(only, _host.Faulted) ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private IEnumerable<Verdict> SelectShift()
    {
        if (!Seated)
            yield return NotSeated();
        foreach (var verdict in MouseAimAtOwn(false, "soldier"))
            yield return verdict;
        foreach (var verdict in MouseLive())
            yield return verdict;

        yield return Verdict.InMissionTick;
        _host.ClearSelectedCompanies();
        var first = _host.MouseProjectChosen();
        if (!first.Found)
            yield return Verdict.Failed("reason=off-screen first=1" + Standing());
        _host.MouseClick(first, right: false, shift: false, ctrl: false);
        foreach (var verdict in MouseWait(() => _host.MouseSelectedCount > 0))
            yield return verdict;
        var firstOnly = _host.MouseOnlyFirstSelected;

        foreach (var verdict in MouseAimAtOwn(true, "second-company"))
            yield return verdict;
        yield return Verdict.InMissionTick;
        var second = _host.MouseProjectChosen();
        if (!second.Found)
            yield return Verdict.Failed("reason=off-screen second=1 first_only=" + Bit(firstOnly) + Standing());
        _host.MouseClick(second, right: false, shift: true, ctrl: false);
        foreach (var verdict in MouseWait(() => _host.MouseSelectedCount > 1))
            yield return verdict;
        var both = _host.MouseFirstAndSecondSelected;

        _host.MouseClick(second, right: false, shift: true, ctrl: false);
        foreach (var verdict in MouseWait(() => _host.MouseSelectedCount < 2))
            yield return verdict;
        var back = _host.MouseOnlyFirstSelected;

        var facts = "first_only=" + Bit(firstOnly) + " both_after_add=" + Bit(both) + " first_only_after_remove=" + Bit(back) + Standing();
        yield return SeatTrialMouse.Toggled(firstOnly, both, back, _host.Faulted) ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private IEnumerable<Verdict> MoveRightClick()
    {
        if (!Seated)
            yield return NotSeated();
        yield return Verdict.InMissionTick;
        if (!_host.MouseChooseSoldier(false))
            yield return Verdict.Failed("reason=no-soldier");
        _host.MouseSelectFirstCompany();
        foreach (var verdict in MouseLive())
            yield return verdict;

        yield return Verdict.InMissionTick;
        var at = _host.MouseGround(0f);
        if (!at.Found)
            yield return Verdict.Failed("reason=no-ground" + Standing());

        _host.MouseClick(at, right: true, shift: false, ctrl: false);
        var startedAt = _now;
        while (float.IsNaN(_host.MouseOrderDistance(at)) && _now - startedAt < MouseSettleSeconds)
            yield return Verdict.Waiting("mouse-order");

        var distance = _host.MouseOrderDistance(at);
        var facts = "distance=" + Num(distance) + " limit=" + Num(SeatTrialMouse.MoveToleranceMetres) + MousePointFacts(at) + Standing();
        yield return SeatTrialMouse.Moved(distance, _host.Faulted) ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private IEnumerable<Verdict> DragFacing()
    {
        if (!Seated)
            yield return NotSeated();
        yield return Verdict.InMissionTick;
        if (!_host.MouseChooseSoldier(false))
            yield return Verdict.Failed("reason=no-soldier");
        _host.MouseSelectFirstCompany();
        foreach (var verdict in MouseLive())
            yield return verdict;

        yield return Verdict.InMissionTick;
        var from = _host.MouseGround(-SeatTrialMouse.DragHalfMetres);
        var to = _host.MouseGround(SeatTrialMouse.DragHalfMetres);
        if (!from.Found || !to.Found)
            yield return Verdict.Failed("reason=no-ground from=" + Bit(from.Found) + " to=" + Bit(to.Found) + Standing());
        var span = System.Math.Abs(to.PixelX - from.PixelX) + System.Math.Abs(to.PixelY - from.PixelY);
        if (span < SeatTrialMouse.DragMinPixels)
            yield return Verdict.Failed("reason=line-too-short pixels=" + Num(span) + Standing());

        _host.MouseDragRight(from, to);
        var startedAt = _now;
        while (float.IsNaN(_host.MouseFacingError(from, to)) && _now - startedAt < MouseSettleSeconds)
            yield return Verdict.Waiting("mouse-line");
        var settledAt = _now;
        while (_now - settledAt < MouseSettleSeconds)
            yield return Verdict.Waiting("mouse-line-settle");

        var error = _host.MouseFacingError(from, to);
        var facts = "error_deg=" + Num(error) + " limit=" + Num(SeatTrialMouse.FacingToleranceDegrees) + " pixels=" + Num(span) + MousePointFacts(from) + Standing();
        yield return SeatTrialMouse.FacedTheNormal(error, _host.Faulted) ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private IEnumerable<Verdict> ChargeEnemy()
    {
        if (!Seated)
            yield return NotSeated();
        yield return Verdict.InMissionTick;
        if (!_host.MouseChooseSoldier(false))
            yield return Verdict.Failed("reason=no-soldier");
        _host.MouseSelectFirstCompany();
        foreach (var verdict in MouseLive())
            yield return verdict;

        yield return Verdict.InMissionTick;
        if (!_host.MouseChooseEnemy())
            yield return Verdict.Failed("reason=no-enemy");
        foreach (var verdict in Aim())
            yield return verdict;

        yield return Verdict.InMissionTick;
        var at = _host.MouseProjectChosen();
        if (!at.Found)
            yield return Verdict.Failed("reason=off-screen" + Standing());

        _host.MouseClick(at, right: true, shift: false, ctrl: false);
        foreach (var verdict in MouseWait(() => _host.MouseChargesChosenEnemy))
            yield return verdict;

        var charged = _host.MouseChargesChosenEnemy;
        var facts = "charge_on_enemy=" + Bit(charged) + MousePointFacts(at) + Standing();
        yield return SeatTrialMouse.Charged(charged, charged, _host.Faulted) ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private IEnumerable<Verdict> QueueWithMouse()
    {
        if (!Seated)
            yield return NotSeated();
        foreach (var verdict in MarshalBattle())
            yield return verdict;
        yield return Verdict.InMissionTick;
        if (_host.MarshalChooseCompanies(1) == 0)
            yield return Verdict.Failed("reason=no-company");
        _host.MarshalSelect(0, 1);
        foreach (var verdict in MouseLive())
            yield return verdict;

        yield return Verdict.InMissionTick;
        var first = _host.MouseGround(-SeatTrialMouse.DragHalfMetres);
        var second = _host.MouseGround(SeatTrialMouse.DragHalfMetres);
        if (!first.Found || !second.Found)
            yield return Verdict.Failed("reason=no-ground first=" + Bit(first.Found) + " second=" + Bit(second.Found) + Standing());

        // The first right click goes through as a plain order: no key is held.
        _host.MouseClick(first, right: true, shift: false, ctrl: false);
        var firstAt = _now;
        while (_now - firstAt < MouseSettleSeconds)
            yield return Verdict.Waiting("first-order");

        _host.MarshalHoldQueue(true);
        foreach (var verdict in MarshalWaitFor(() => _host.MarshalQueueArmed, "queue-armed"))
            yield return verdict;

        var capturedBefore = _host.MarshalStepsCaptured;
        var armed = _host.MarshalQueueArmed;
        _host.MouseClick(second, right: true, shift: true, ctrl: false);
        var orderedAt = _now;
        while (_host.MarshalStepsCaptured == capturedBefore && _now - orderedAt < MouseSettleSeconds)
            yield return Verdict.Waiting("queued-order");

        var captured = _host.MarshalStepsCaptured - capturedBefore;
        var queued = _host.MarshalQueuedSteps(0);
        _host.MarshalHoldQueue(false);

        // A direct order clears the queue the step made, so the company does not walk the legs after the step.
        yield return Verdict.InMissionTick;
        _host.MarshalStandGround();
        var facts = "captured=" + Whole(captured) + " queued=" + Whole(queued) + " armed_at_order=" + Bit(armed) + " panel=" + Bit(_host.PanelOpen) + Faulted() + Standing();
        yield return SeatTrialMouse.Queued(captured, queued, armed, _host.Faulted || _host.MarshalFaulted) ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private static void MouseSteps(Dictionary<string, System.Func<CommandTrialScript, IEnumerable<Verdict>>> into)
    {
        into["select_click"] = s => s.SelectClick();
        into["select_shift"] = s => s.SelectShift();
        into["move_right_click"] = s => s.MoveRightClick();
        into["drag_facing"] = s => s.DragFacing();
        into["charge_enemy"] = s => s.ChargeEnemy();
        into["queue_with_mouse"] = s => s.QueueWithMouse();
    }
}
