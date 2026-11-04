using System;

namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>One tick of the trial's aiming: the look input for the flight, whether the view is on the soldier, and whether he is gone.</summary>
internal readonly record struct SeatAimStep(SeatInput Input, bool OnTarget, bool Gone);

/// <summary>The look input that turns the Seat's camera onto a point (the field trial's aim before it keeps to a soldier), with the flight's signs and no engine type.</summary>
internal static class SeatTrialAim
{
    /// <summary>The view is on the point when both angles are this close, in radians.</summary>
    internal const float OnTargetRadians = 0.01f;

    /// <summary>Share of the remaining angle turned per tick: under 1 so a late input cannot overshoot and swing.</summary>
    internal const float Gain = 0.7f;

    internal const float MinPitch = -0.35f;

    /// <summary>The look units that turn a camera at <c>cam</c> with the given yaw and pitch toward <c>target</c>, and the larger angle still to go.</summary>
    internal static (float LookYaw, float LookPitch, float ErrorRadians) Step(float camX, float camY, float camZ, float yaw, float pitch,
        float targetX, float targetY, float targetZ)
    {
        var dx = targetX - camX;
        var dy = targetY - camY;
        var ground = Math.Sqrt(dx * dx + dy * dy);
        if (!IsReal(ground) || !IsReal(yaw) || !IsReal(pitch) || !IsReal(targetZ - camZ))
            return (0f, 0f, float.NaN);

        var wantedYaw = ground < 1e-4 ? yaw : (float)Math.Atan2(-dx, dy);
        var wantedPitch = Math.Min(SeatOrbit.MaxPitch, Math.Max(MinPitch, (float)Math.Atan2(-(targetZ - camZ), ground)));
        var deltaYaw = SeatOrbit.Wrap(wantedYaw - yaw);
        var deltaPitch = wantedPitch - pitch;
        return (Gain * deltaYaw / SeatOrbit.LookPerUnit, Gain * deltaPitch / SeatOrbit.LookPerUnit,
            Math.Max(Math.Abs(deltaYaw), Math.Abs(deltaPitch)));
    }

    /// <summary>The view is on the point: the error is a real number within <see cref="OnTargetRadians"/>.</summary>
    internal static bool OnTarget(float errorRadians) => IsReal(errorRadians) && errorRadians <= OnTargetRadians;

    private static bool IsReal(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
