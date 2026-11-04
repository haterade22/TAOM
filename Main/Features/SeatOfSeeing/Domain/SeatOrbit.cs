using System;
using TAOM.Core.Validation;

namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>The orbit around a kept target: the point the camera looks at (eased toward the target), the angles and the distance.</summary>
internal readonly record struct SeatOrbitState(float PointX, float PointY, float PointZ, float Yaw, float Pitch, float Distance);

/// <summary>The camera that orbits a kept target (build-spec section 7), with no engine type: yaw 0 looks along +Y, a positive yaw turns left and a positive pitch looks down.</summary>
internal static class SeatOrbit
{
    internal const float StartDistance = 16f;
    internal const float MinDistance = 4f;
    internal const float MaxDistance = 90f;
    internal const float MinPitch = 0.05f;
    internal const float MaxPitch = 1.45f;
    internal const float EaseRate = 5f;
    internal const float ZoomPerNotch = 0.88f;
    internal const float LookPerUnit = 0.0035f;

    /// <summary>The share of the way to the target one frame covers: <c>1 - exp(-5 dt)</c>.</summary>
    internal static float Ease(float dt) => 1f - (float)Math.Exp(-EaseRate * dt);

    /// <summary>The orbit that starts where the camera is: the point on its look line at the start distance, with the pitch held above its floor.</summary>
    internal static SeatOrbitState Begin(SeatPose camera)
    {
        var pitch = Clamp(camera.Pitch, MinPitch, MaxPitch);
        var (gx, gy, gz) = Look(camera.Yaw, pitch);
        return new SeatOrbitState(camera.X + gx * StartDistance, camera.Y + gy * StartDistance, camera.Z + gz * StartDistance,
            camera.Yaw, pitch, StartDistance);
    }

    /// <summary>One frame: the mouse turns the orbit, the wheel changes the distance within its range, and the point eases toward the target; a bad <paramref name="dt"/> returns the state as it is, and a target that is not a real number keeps the last point.</summary>
    internal static SeatOrbitState Step(SeatOrbitState state, SeatInput input, float targetX, float targetY, float targetZ, float dt)
    {
        if (!FiniteFloatValidator.IsFinite(dt) || dt < 0f)
            return state;

        var yaw = Wrap(state.Yaw + Finite(input.LookYaw) * LookPerUnit);
        var pitch = Clamp(state.Pitch + Finite(input.LookPitch) * LookPerUnit, MinPitch, MaxPitch);
        var distance = Clamp(state.Distance * (float)Math.Pow(ZoomPerNotch, Finite(input.ZoomNotches)), MinDistance, MaxDistance);
        if (!FiniteFloatValidator.IsFinite(targetX) || !FiniteFloatValidator.IsFinite(targetY) || !FiniteFloatValidator.IsFinite(targetZ))
            return state with { Yaw = yaw, Pitch = pitch, Distance = distance };

        var ease = Ease(dt);
        return new SeatOrbitState(state.PointX + (targetX - state.PointX) * ease, state.PointY + (targetY - state.PointY) * ease,
            state.PointZ + (targetZ - state.PointZ) * ease, yaw, pitch, distance);
    }

    /// <summary>The camera pose of an orbit: the point less the look direction times the distance.</summary>
    internal static SeatPose Pose(SeatOrbitState state)
    {
        var (gx, gy, gz) = Look(state.Yaw, state.Pitch);
        return new SeatPose(state.PointX - gx * state.Distance, state.PointY - gy * state.Distance, state.PointZ - gz * state.Distance,
            state.Yaw, state.Pitch);
    }

    private static (float X, float Y, float Z) Look(float yaw, float pitch)
    {
        var flat = (float)Math.Cos(pitch);
        return (-(float)Math.Sin(yaw) * flat, (float)Math.Cos(yaw) * flat, -(float)Math.Sin(pitch));
    }

    internal static float Wrap(float angle)
    {
        const float twoPi = (float)(2.0 * Math.PI);
        while (angle > Math.PI)
            angle -= twoPi;
        while (angle < -Math.PI)
            angle += twoPi;
        return angle;
    }

    private static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;

    private static float Finite(float value) => FiniteFloatValidator.IsFinite(value) ? value : 0f;
}
