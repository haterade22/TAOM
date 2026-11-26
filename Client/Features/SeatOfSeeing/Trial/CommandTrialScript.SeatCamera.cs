using System;
using System.Collections.Generic;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

/// <summary>
/// The three camera feel steps of the field trial (Seat build-spec 12): the pointer at a window edge, the middle button's turn around a pivot,
/// and one wheel notch easing the height down. The pointer, the middle button and the notch are injected inside the Seat (never an operating
/// system input), and the step reads only the camera it produces. What the steps cannot prove is how the real mouse feels, a pointer on another
/// monitor of a windowed game, and where the cursor stands after a turn: those stay with a person.
/// </summary>
internal sealed partial class CommandTrialScript
{
    private static readonly SeatInput TurnLeft = new(Right: 0f, Forward: 0f, Lift: 0f, ZoomNotches: 0f, LookYaw: SeatTrialCamera.RotateLookUnits, LookPitch: 0f, Fast: false);
    private static readonly SeatInput OneNotch = new(Right: 0f, Forward: 0f, Lift: 0f, ZoomNotches: 1f, LookYaw: 0f, LookPitch: 0f, Fast: false);

    private const float PivotSettleSeconds = 0.3f;

    private IEnumerable<Verdict> EdgePan()
    {
        if (!Seated)
            yield return NotSeated();

        yield return Verdict.InMissionTick;
        var opened = _host.OpenPanel();
        var openedAt = _now;
        while (_now - openedAt < PanelSettleSeconds)
            yield return Verdict.Waiting("panel-seen");

        var start = _host.ViewPosition();
        var heading = _host.ViewForward();
        if (start == null || heading == null)
            yield return Verdict.Failed("reason=no-camera opened=" + Bit(opened) + Standing());

        var cursor = _host.MouseVisible;
        _host.SetEdgeScroll(true);
        _host.SetPointer(SeatTrialCamera.EdgePointerX, SeatTrialCamera.EdgePointerY);
        var startedAt = _now;
        while (_now - startedAt < SeatTrialCamera.EdgeSeconds)
            yield return Verdict.Waiting("edge");

        var end = _host.ViewPosition();
        _host.ReleaseCameraTrial();
        yield return Verdict.InMissionTick;
        _host.ClosePanel(asPlayer: true);

        var along = heading!.Value;
        var dx = end == null ? float.NaN : end.Value.X - start!.Value.X;
        var dy = end == null ? float.NaN : end.Value.Y - start!.Value.Y;

        // The view's right is the heading turned a quarter clockwise (FordsCameraMath.Pan: right is (cos yaw, sin yaw), forward (-sin yaw, cos yaw)).
        var right = dx * along.Y - dy * along.X;
        var forward = dx * along.X + dy * along.Y;
        var facts = "opened=" + Bit(opened) + " cursor=" + Bit(cursor) + " right=" + Num(right) + " forward=" + Num(forward) + " seconds=" + Num(_now - startedAt) + Standing();
        yield return Seated && opened && SeatTrialCamera.EdgePanned(right, forward, cursor, _host.Faulted) ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private IEnumerable<Verdict> RotatePivot()
    {
        if (!Seated)
            yield return NotSeated();

        var centre = _host.ViewCentreOnGround();
        var from = _host.ViewPosition();
        if (centre == null || from == null)
            yield return Verdict.Failed("reason=no-pivot" + Standing());

        _host.SetMiddleHeld(true);
        _host.SetInput(TurnLeft);
        var startedAt = _now;
        while (_now - startedAt < SeatTrialCamera.RotateSeconds)
            yield return Verdict.Waiting("turn");

        var during = _host.ViewCentreOnGround();
        var swungTo = _host.ViewPosition();
        _host.SetInput(NoInput);
        _host.SetMiddleHeld(false);
        var releasedAt = _now;
        while (_now - releasedAt < PivotSettleSeconds)
            yield return Verdict.Waiting("hand-back");

        var after = _host.ViewCentreOnGround();
        var driftHeld = during == null ? float.NaN : centre!.Value.Distance2D(during.Value);
        var driftAfter = after == null ? float.NaN : centre!.Value.Distance2D(after.Value);
        var swung = swungTo == null ? float.NaN : from!.Value.Distance2D(swungTo.Value);
        var facts = "drift_held=" + Num(driftHeld) + " swung=" + Num(swung) + " drift_after=" + Num(driftAfter) + " seconds=" + Num(_now - startedAt) + Standing();
        yield return Seated && SeatTrialCamera.PivotHeld(driftHeld, swung, driftAfter, _host.Faulted) ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private IEnumerable<Verdict> ZoomSmooth()
    {
        var at = _host.ViewPosition();
        if (!Seated || at == null)
            yield return NotSeated();

        var start = HeightAboveGround(at!.Value);
        var target = start * SeatCameraMath.ZoomPerNotch;
        if (!(target >= SeatCameraMath.MinHeight))
            yield return Verdict.Failed("reason=too-low height=" + Num(start) + Standing());

        _host.SetInput(NoInput);
        _host.SetInputOnce(OneNotch);
        var notchedAt = _now;
        var first = float.NaN;
        var previous = start;
        var last = start;
        var maxRise = 0f;
        var arrived = float.NaN;
        while (_now - notchedAt < SeatTrialCamera.ZoomWatchSeconds)
        {
            yield return Verdict.Waiting("zoom");
            var view = _host.ViewPosition();
            if (view == null)
                continue;

            last = HeightAboveGround(view.Value);
            if (float.IsNaN(first))
                first = last;
            else
                maxRise = Math.Max(maxRise, last - previous);
            previous = last;
            if (float.IsNaN(arrived) && last <= target * (1f + SeatTrialCamera.ZoomSlack))
                arrived = _now - notchedAt;
        }

        var facts = "start=" + Num(start) + " target=" + Num(target) + " first=" + Num(first) + " arrived=" + Num(arrived) + " end=" + Num(last)
            + " max_rise=" + Num(maxRise) + Standing();
        yield return Seated && SeatTrialCamera.ZoomEased(start, first, maxRise, float.IsNaN(arrived) ? float.PositiveInfinity : arrived, last, _host.Faulted)
            ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }
}
