using System.Collections.Generic;
using TAOM.Features.MarshalsWord.Domain;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

/// <summary>
/// The eight command mouse steps of the field trial (Seat build-spec 12): a click that selects, a Shift click that toggles, a right click that moves, a
/// right drag that draws a line and a facing, a right click on an enemy that charges, a Shift right click that queues, a left drag that box selects,
/// and Ctrl and a number that moves the selected company's soldiers into another company.
/// </summary>
internal sealed partial class CommandTrialScript
{
    private const float MouseSettleSeconds = 0.5f;

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

        yield return Verdict.InMissionTick;
        _host.MarshalStandGround();
        var facts = "captured=" + Whole(captured) + " queued=" + Whole(queued) + " armed_at_order=" + Bit(armed) + " panel=" + Bit(_host.PanelOpen) + Faulted() + Standing();
        yield return SeatTrialMouse.Queued(captured, queued, armed, _host.Faulted || _host.MarshalFaulted) ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    // A left drag round a soldier of the player's side: the rectangle shows while it runs and is gone at the release, and the soldier's company is
    // selected. The box is 120 pixels square about his chest, so neighbouring companies may be inside it too; only the chosen one is required.
    private IEnumerable<Verdict> SelectBox()
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

        var from = SeatBoxPoint(at, -SeatTrialMouse.BoxHalfPixels);
        var to = SeatBoxPoint(at, SeatTrialMouse.BoxHalfPixels);
        _host.MouseDragLeft(from, to);
        var shown = false;
        var startedAt = _now;
        while (!_host.MouseFirstSelected && _now - startedAt < MouseSettleSeconds + SeatTrialMouse.DragSeconds + 1f)
        {
            shown |= _host.MouseBoxShown;
            yield return Verdict.Waiting("mouse-box");
        }

        var selected = _host.MouseFirstSelected;
        var hidden = !_host.MouseBoxShown;
        var facts = "first_selected=" + Bit(selected) + " box_shown=" + Bit(shown) + " box_hidden_after=" + Bit(hidden) + " selected=" + Whole(_host.MouseSelectedCount)
            + MousePointFacts(at) + Standing();
        yield return SeatTrialMouse.BoxSelected(selected, shown, hidden, _host.Faulted) ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private static SeatMousePoint SeatBoxPoint(SeatMousePoint at, float offset)
    {
        var x = at.PixelX + offset;
        var y = at.PixelY + offset;
        return new SeatMousePoint(true, x, y, at.RangedX + offset / 1000f, at.RangedY + offset / 1000f, at.WorldX, at.WorldY);
    }

    // Ctrl and the second company's number key, as the patch would store it: the first company's soldiers move into the second. The trial cannot hold a
    // real Control key, so the shared request is called itself; the Seat's mission tick takes it. The soldiers are put back at the end, so the later steps
    // find the companies they chose from.
    private IEnumerable<Verdict> TransferCtrlNumber()
    {
        if (!Seated)
            yield return NotSeated();
        yield return Verdict.InMissionTick;
        if (!_host.MouseChooseSoldier(false))
            yield return Verdict.Failed("reason=no-soldier");
        if (!_host.MouseChooseSoldier(true))
            yield return Verdict.Failed("reason=no-second-company");
        _host.MouseSelectFirstCompany();
        foreach (var verdict in MouseLive())
            yield return verdict;

        yield return Verdict.InMissionTick;
        var firstBefore = _host.MouseFirstCount;
        var secondBefore = _host.MouseSecondCount;
        _host.MouseRequestTransfer();
        foreach (var verdict in MouseWait(() => _host.MouseFirstCount != firstBefore))
            yield return verdict;

        var firstAfter = _host.MouseFirstCount;
        var secondAfter = _host.MouseSecondCount;
        var facts = "first=" + Whole(firstBefore) + ">" + Whole(firstAfter) + " second=" + Whole(secondBefore) + ">" + Whole(secondAfter) + Standing();
        var passed = SeatTrialMouse.Transferred(firstBefore, firstAfter, secondBefore, secondAfter, _host.Faulted);

        // Back to where they were, whatever the verdict: what moved is what the second company gained.
        yield return Verdict.InMissionTick;
        _host.MouseTransferBack(secondAfter - secondBefore);
        yield return passed ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private static void MouseSteps(Dictionary<string, System.Func<CommandTrialScript, IEnumerable<Verdict>>> into)
    {
        into["select_click"] = s => s.SelectClick();
        into["select_shift"] = s => s.SelectShift();
        into["move_right_click"] = s => s.MoveRightClick();
        into["drag_facing"] = s => s.DragFacing();
        into["charge_enemy"] = s => s.ChargeEnemy();
        into["queue_with_mouse"] = s => s.QueueWithMouse();
        into["select_box"] = s => s.SelectBox();
        into["transfer_ctrl_number"] = s => s.TransferCtrlNumber();
    }
}
