using System;
using System.Collections.Generic;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Client.Features.FordsOfWar;

/// <summary>
/// Feeds the Fords run report from the commander camera: what the player asked for against what the camera did, whether the camera
/// stays inside the boundary, the photo-mode veto and a latched failure. One per camera view. A class, not a record.
/// </summary>
internal sealed class FordsCameraProbe
{
    private const float MovedMin = 0.01f;
    private const float TurnedMin = 1e-4f;
    private const float InsideSampleSeconds = 1f;
    private const float EdgeToleranceMetres = 0.5f;

    private readonly FordsRunReport? _report;
    private readonly Func<float, float, bool> _engineInside;
    private float _sinceInside;

    internal FordsCameraProbe(FordsRunReport? report, Func<float, float, bool> engineInside)
    {
        _report = report;
        _engineInside = engineInside;
    }

    internal bool PhotoVetoed()
    {
        _report?.PhotoVetoed();
        return false;
    }

    internal void Frame(FordsCameraState before, FordsCameraInput input, FordsCameraState after, float dt,
        IReadOnlyList<Vec3f> boundary)
    {
        var report = _report;
        if (report == null)
            return;

        var f = Flags(before, input, after);
        report.CameraFrame(f.Pan, f.Rotate, f.Zoom, f.Moved, f.Turned, f.Zoomed);
        _sinceInside += dt;
        if (_sinceInside < InsideSampleSeconds)
            return;

        _sinceInside = 0f;
        report.CameraInside(IsInside(boundary, after.X, after.Y));
    }

    /// <summary>
    /// Inside when the engine says so, or when the camera rests within half a metre of the polygon edge: the clamp parks a pushed camera
    /// exactly on the edge, and the native test is strict there.
    /// </summary>
    internal bool IsInside(IReadOnlyList<Vec3f> boundary, float x, float y) =>
        _engineInside(x, y) || FordsCameraMath.DistanceToEdge(boundary, x, y) <= EdgeToleranceMetres;

    internal void Failed() => _report?.CameraFailed();

    internal static (bool Pan, bool Rotate, bool Zoom, bool Moved, bool Turned, bool Zoomed) Flags(
        FordsCameraState before, FordsCameraInput input, FordsCameraState after) =>
        (input.PanForward != 0f || input.PanRight != 0f,
            input.Rotate != 0f || input.DragPixelsX != 0f || input.DragPixelsY != 0f,
            input.ZoomNotches != 0f,
            Math.Abs(after.X - before.X) + Math.Abs(after.Y - before.Y) > MovedMin,
            Math.Abs(after.Yaw - before.Yaw) > TurnedMin || Math.Abs(after.Tilt - before.Tilt) > TurnedMin,
            Math.Abs(after.Height - before.Height) > MovedMin);
}
