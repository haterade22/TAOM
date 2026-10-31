using System;
using System.Collections.Generic;
using System.Globalization;
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
    private const float PoseEverySeconds = 5f;
    private const int PoseLines = 7;
    private const float RadiansToDegrees = (float)(180.0 / Math.PI);

    private readonly FordsRunReport? _report;
    private readonly Func<float, float, bool> _engineInside;
    private readonly Func<float, float, (float Terrain, float Ground)>? _groundAt;
    private float _sinceInside;
    private float _poseClock;
    private float _nextPoseAt;
    private int _poses;

    internal FordsCameraProbe(FordsRunReport? report, Func<float, float, bool> engineInside,
        Func<float, float, (float Terrain, float Ground)>? groundAt = null)
    {
        _report = report;
        _engineInside = engineInside;
        _groundAt = groundAt;
    }

    /// <summary>
    /// Logs where the camera is and where it looks, with the terrain height and the ground under it, at the first frame and then
    /// every 5 s up to 30 s, so a camera inside the ground or turned at the sky is visible in the log. <c>ground</c> is
    /// <c>FordsEngine.GroundAt</c>: a probe from 3 m above the terrain that falls back to the terrain height, so a ground equal to
    /// the terrain means no separate ground was found.
    /// </summary>
    internal void Pose(FordsCameraState state, float groundZ, float dt)
    {
        var report = _report;
        if (report == null || _poses >= PoseLines)
            return;

        _poseClock += dt;
        if (!(_poseClock >= _nextPoseAt))
            return;

        _nextPoseAt = _poseClock + PoseEverySeconds;
        _poses++;
        var pitch = FordsCameraMath.Pitch(state);
        var look = FordsCameraMath.LookDirection(state.Yaw, pitch);
        var (terrain, ground) = _groundAt?.Invoke(state.X, state.Y) ?? (float.NaN, float.NaN);
        report.CameraPose(string.Format(CultureInfo.InvariantCulture,
            "x={0:F1} y={1:F1} z={2:F1} groundZ={3:F1} terrain={4:F1} ground={5:F1} height={6:F1} yaw={7:F0} pitch={8:F0} look=({9:F2},{10:F2},{11:F2})",
            state.X, state.Y, groundZ + state.Height, groundZ, terrain, ground, state.Height, state.Yaw * RadiansToDegrees,
            pitch * RadiansToDegrees, look.X, look.Y, look.Z));
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
