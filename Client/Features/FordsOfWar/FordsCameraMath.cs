using System;
using System.Collections.Generic;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Client.Features.FordsOfWar;

/// <summary>
/// The arithmetic of the Fords of War commander camera (build-spec 6.1), with no engine type so it runs in a test. Ground plane
/// is X and Y, Z is up; yaw 0 looks along +Y and a positive yaw turns left, the engine's bearing convention. The view reads the
/// keys into a <see cref="FordsCameraInput"/>, calls <see cref="Step"/> once a frame and turns the result into a frame with
/// <see cref="Basis"/>.
/// </summary>
internal static class FordsCameraMath
{
    internal const float MinHeight = 15f;
    internal const float MaxHeight = 120f;

    internal const float MinPitch = 20f * Deg;
    internal const float MaxPitch = 85f * Deg;
    internal const float MaxTilt = 20f * Deg;

    private const float Deg = (float)(Math.PI / 180.0);
    private const float PitchAtMinHeight = 35f * Deg;
    private const float PitchAtMaxHeight = 70f * Deg;
    private const float PanSpeedBase = 10f;
    private const float PanSpeedPerMetre = 0.9f;
    private const float RotateSpeed = 1.6f;
    private const float ZoomPerNotch = 0.88f;
    private const float DragYawPerPixel = 0.005f;
    private const float DragTiltPerPixel = 0.004f;
    private const float TwoPi = (float)(2.0 * Math.PI);

    /// <summary>The camera at <paramref name="start"/> (height above <paramref name="groundZ"/>) turned toward <paramref name="lookAt"/>.</summary>
    internal static FordsCameraState Start(Vec3f start, Vec3f lookAt, float groundZ)
    {
        var dx = lookAt.X - start.X;
        var dy = lookAt.Y - start.Y;
        var yaw = dx * dx + dy * dy < 1e-6f ? 0f : (float)Math.Atan2(-dx, dy);
        return new FordsCameraState(start.X, start.Y, yaw, ClampHeight(start.Z - groundZ), 0f);
    }

    /// <summary>Metres per second at the given height: a higher camera pans faster, so a screen crossing takes about as long at any zoom.</summary>
    internal static float PanSpeed(float height) => PanSpeedBase + PanSpeedPerMetre * height;

    /// <summary>The camera one frame later: pan relative to the yaw, rotate, zoom (clamped to the height range), drag tilt, then kept inside the boundary.</summary>
    internal static FordsCameraState Step(FordsCameraState state, FordsCameraInput input, float dt, IReadOnlyList<Vec3f> boundary)
    {
        var yaw = WrapAngle(state.Yaw + input.Rotate * RotateSpeed * dt - input.DragPixelsX * DragYawPerPixel);
        var tilt = Clamp(state.Tilt + input.DragPixelsY * DragTiltPerPixel, -MaxTilt, MaxTilt);
        var height = ClampHeight(state.Height * (float)Math.Pow(ZoomPerNotch, input.ZoomNotches));

        var forward = input.PanForward;
        var right = input.PanRight;
        var magnitude = (float)Math.Sqrt(forward * forward + right * right);
        if (magnitude > 1f)
        {
            forward /= magnitude;
            right /= magnitude;
        }

        var distance = PanSpeed(state.Height) * (input.Fast ? 2f : 1f) * dt;
        var heading = Forward(yaw);
        var x = state.X + (heading.X * forward + (float)Math.Cos(yaw) * right) * distance;
        var y = state.Y + (heading.Y * forward + (float)Math.Sin(yaw) * right) * distance;
        var (clampedX, clampedY) = ClampInside(boundary, x, y);
        return new FordsCameraState(clampedX, clampedY, yaw, height, tilt);
    }

    /// <summary>The pitch below the horizon in radians: steeper at height, shifted by the player's tilt, held between <see cref="MinPitch"/> and <see cref="MaxPitch"/>.</summary>
    internal static float Pitch(FordsCameraState state)
    {
        var t = (ClampHeight(state.Height) - MinHeight) / (MaxHeight - MinHeight);
        return Clamp(PitchAtMinHeight + (PitchAtMaxHeight - PitchAtMinHeight) * t + state.Tilt, MinPitch, MaxPitch);
    }

    /// <summary>The ground direction the camera faces.</summary>
    internal static Vec3f Forward(float yaw) => new(-(float)Math.Sin(yaw), (float)Math.Cos(yaw), 0f);

    /// <summary>The unit direction the camera looks along, pitched down by <paramref name="pitch"/>.</summary>
    internal static Vec3f LookDirection(float yaw, float pitch)
    {
        var ground = Forward(yaw);
        var cosPitch = (float)Math.Cos(pitch);
        return new Vec3f(ground.X * cosPitch, ground.Y * cosPitch, -(float)Math.Sin(pitch));
    }

    /// <summary>
    /// The engine's camera axes for that look: side (screen right), up (screen up) and back, a right handed set with side x up = back.
    /// The engine camera looks along minus its third axis, so the view builds its frame as (side, up, back).
    /// </summary>
    internal static (Vec3f Side, Vec3f Up, Vec3f Back) Basis(float yaw, float pitch)
    {
        var ground = Forward(yaw);
        var sinPitch = (float)Math.Sin(pitch);
        var up = new Vec3f(ground.X * sinPitch, ground.Y * sinPitch, (float)Math.Cos(pitch));
        var look = LookDirection(yaw, pitch);
        var back = new Vec3f(-look.X, -look.Y, -look.Z);
        var side = new Vec3f(up.Y * back.Z - up.Z * back.Y, up.Z * back.X - up.X * back.Z, up.X * back.Y - up.Y * back.X);
        return (side, up, back);
    }

    /// <summary>The point itself while inside the polygon (or when the polygon has fewer than three points), else the nearest point on its edge.</summary>
    internal static (float X, float Y) ClampInside(IReadOnlyList<Vec3f> polygon, float x, float y)
    {
        if (polygon.Count < 3 || IsInside(polygon, x, y))
            return (x, y);

        var bestX = x;
        var bestY = y;
        var best = float.MaxValue;
        for (var i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            var (px, py) = NearestOnSegment(a, b, x, y);
            var d = (px - x) * (px - x) + (py - y) * (py - y);
            if (d < best)
            {
                best = d;
                bestX = px;
                bestY = py;
            }
        }

        return (bestX, bestY);
    }

    /// <summary>Even-odd test on the ground plane.</summary>
    internal static bool IsInside(IReadOnlyList<Vec3f> polygon, float x, float y)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var a = polygon[i];
            var b = polygon[j];
            if (a.Y > y != b.Y > y && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }

        return inside;
    }

    private static (float X, float Y) NearestOnSegment(Vec3f a, Vec3f b, float x, float y)
    {
        var abx = b.X - a.X;
        var aby = b.Y - a.Y;
        var lengthSquared = abx * abx + aby * aby;
        if (lengthSquared < 1e-9f)
            return (a.X, a.Y);

        var t = Clamp(((x - a.X) * abx + (y - a.Y) * aby) / lengthSquared, 0f, 1f);
        return (a.X + abx * t, a.Y + aby * t);
    }

    private static float ClampHeight(float height) => Clamp(height, MinHeight, MaxHeight);

    private static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;

    private static float WrapAngle(float angle)
    {
        while (angle > Math.PI)
            angle -= TwoPi;
        while (angle <= -Math.PI)
            angle += TwoPi;
        return angle;
    }
}
