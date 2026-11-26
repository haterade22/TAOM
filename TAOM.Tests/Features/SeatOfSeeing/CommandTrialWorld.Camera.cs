using System;
using TAOM.Client.Features.SeatOfSeeing;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// The camera feel half of the stand-in: the pointer at an edge, the turn around a pivot and the eased zoom, by the Seat's own arithmetic
/// (<see cref="SeatEdgeScroll"/>, <see cref="SeatZoom"/>), each fault switchable.
/// </summary>
internal sealed partial class CommandTrialWorld
{
    private const float PivotAhead = 30f;

    internal bool EdgeDoesNothing;
    internal bool EdgeFliesLeft;
    internal bool EdgeIgnoresTheSetting;
    internal bool ZoomDoesNothing;
    internal bool ZoomJumps;
    internal bool ZoomBounces;
    internal bool RotateInPlace;
    internal bool RotateCentreDrifts;
    internal bool RotateJumpsOnRelease;
    internal bool RotateHasNoPivot;
    internal bool EdgeScrollOn;
    internal int CameraReleases;

    private (float X, float Y)? _pointer;
    private bool _middle;
    private SeatInput? _once;
    private float _zoomTarget = float.NaN;
    private float _angle;
    private float _centreDrift;
    private Vec3f? _pivot;
    private int _zoomFrames;
    private float _zoomSeen = float.NaN;

    public void SetEdgeScroll(bool on) => EdgeScrollOn = on;

    public void SetPointer(float fractionX, float fractionY) => _pointer = (fractionX, fractionY);

    public void SetMiddleHeld(bool held)
    {
        if (held && !_middle)
        {
            _pivot = new Vec3f(View.X, View.Y + PivotAhead, GroundZ);
            _angle = 0f;
            _centreDrift = 0f;
        }
        else if (!held && _middle && RotateJumpsOnRelease)
            _centreDrift += 10f;
        _middle = held;
    }

    public void SetInputOnce(SeatInput input) => _once = input;

    public void ReleaseCameraTrial()
    {
        CameraReleases++;
        _pointer = null;
        _middle = false;
        _once = null;
        EdgeScrollOn = false;
    }

    public Vec3f? ViewCentreOnGround()
    {
        if (RotateHasNoPivot)
            return null;
        var centre = _pivot ?? new Vec3f(View.X, View.Y + PivotAhead, GroundZ);
        return new Vec3f(centre.X + _centreDrift, centre.Y, GroundZ);
    }

    // Called once a seated frame, before the key flight: the one-frame wheel input, then the pointer, then the turn.
    private void CameraFeelFrame(float dt)
    {
        ZoomFrame(dt);
        if (_pointer.HasValue && MouseVisible && (EdgeScrollOn || EdgeIgnoresTheSetting) && !EdgeDoesNothing && !_middle && Keeping == SeatKeepKind.None)
        {
            var (right, _) = SeatEdgeScroll.Axes(_pointer.Value.X, _pointer.Value.Y, SeatEdgeScroll.Margin);
            View = new Vec3f(View.X + (EdgeFliesLeft ? -right : right) * SeatCameraMath.PanSpeed(FlightHeight) * dt, View.Y, View.Z);
        }

        if (_middle && Input is { LookYaw: not 0f } turn && _pivot.HasValue)
        {
            _angle += turn.LookYaw * SeatCameraMath.LookPerUnit;
            if (RotateCentreDrifts)
                _centreDrift += 6f * dt;
            if (!RotateInPlace)
                View = new Vec3f(_pivot.Value.X + PivotAhead * (float)Math.Sin(_angle), _pivot.Value.Y - PivotAhead * (float)Math.Cos(_angle), View.Z);
            else
                _centreDrift += PivotAhead * Math.Abs(turn.LookYaw * SeatCameraMath.LookPerUnit);
        }
    }

    private void ZoomFrame(float dt)
    {
        var notches = 0f;
        if (_once.HasValue)
        {
            notches = ZoomDoesNothing ? 0f : _once.Value.ZoomNotches;
            _once = null;
            _zoomFrames = 0;
        }

        // The lift key and a re-entry move FlightHeight on their own; the target then follows, as the Seat's does.
        var target = float.IsNaN(_zoomTarget) || Math.Abs(FlightHeight - _zoomSeen) > 1e-4f ? FlightHeight : _zoomTarget;
        var next = SeatZoom.Step(FlightHeight, target, notches, dt);
        _zoomTarget = next.Target;
        FlightHeight = ZoomJumps && notches != 0f ? next.Target : next.Height;
        if (ZoomBounces && ++_zoomFrames == 6)
            FlightHeight *= 1.1f;

        _zoomSeen = FlightHeight;
        View = new Vec3f(View.X, View.Y, GroundZ + FlightHeight);
    }
}
