using System;
using TAOM.Core.Validation;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Features.SeatOfSeeing;

/// <summary>Where the view's centre ray meets the ground plane: the point, and how far along the ray it is.</summary>
internal readonly record struct SeatPivotFind(bool Found, float X, float Y, float Z, float Distance);

/// <summary>
/// The pivot of the Seat's middle-button rotation, with no engine type and no ray cast. The centre of the view looks along the camera's
/// look direction; the pivot is where that line meets the plane at the (smoothed) ground height, found by arithmetic. Held, the camera
/// turns around that point at the distance it had, so the point stays at the centre of the screen.
/// </summary>
internal static class SeatPivot
{
    /// <summary>A view that looks flatter than this has no pivot: the ray meets the plane too far off, so the turn stays in place.</summary>
    internal const float MinPitch = 0.15f;

    /// <summary>A pivot further than this along the ray is dropped, and the turn stays in place.</summary>
    internal const float MaxDistance = 300f;

    private static readonly SeatPivotFind None = new(false, 0f, 0f, 0f, 0f);

    internal static SeatPivotFind Find(SeatPose camera, float groundZ)
    {
        if (!FiniteFloatValidator.IsFinite(groundZ) || !SeatCameraMath.IsFinite(camera) || !(camera.Pitch > MinPitch))
            return None;

        var height = camera.Z - groundZ;
        var sin = (float)Math.Sin(camera.Pitch);
        if (!(height > 0f) || !(sin > 0f))
            return None;

        var distance = height / sin;
        if (!(distance <= MaxDistance))
            return None;

        var flat = (float)Math.Cos(camera.Pitch);
        return new SeatPivotFind(true, camera.X - (float)Math.Sin(camera.Yaw) * flat * distance, camera.Y + (float)Math.Cos(camera.Yaw) * flat * distance,
            groundZ, distance);
    }

    /// <summary>The orbit that holds the camera where it is and turns around the pivot.</summary>
    internal static SeatOrbitState Begin(SeatPose camera, SeatPivotFind pivot) =>
        new(pivot.X, pivot.Y, pivot.Z, camera.Yaw, camera.Pitch, pivot.Distance);

    /// <summary>
    /// One frame: the look turns the orbit by the same units per step as the flight's look, the pitch held to the orbit's range, and the
    /// pivot and the distance stay as they are (no ease, no wheel). A look value that is not a real number counts as zero. The pitch is also
    /// held so the camera stays under <paramref name="ceiling"/> over the pivot (<c>distance * sin(pitch)</c>), the height the flight can
    /// hold, so letting go does not drop the view.
    /// </summary>
    internal static SeatOrbitState Step(SeatOrbitState state, float lookYaw, float lookPitch, float ceiling = SeatCameraMath.MaxHeight)
    {
        var yaw = SeatOrbit.Wrap(state.Yaw + Finite(lookYaw) * SeatOrbit.LookPerUnit);
        var top = FiniteFloatValidator.IsFinite(ceiling) ? Math.Min(ceiling, SeatCameraMath.MaxHeight) : SeatCameraMath.MaxHeight;
        var cap = state.Distance > 0f ? (float)Math.Asin(Math.Min(1f, Math.Max(0f, top) / state.Distance)) : SeatOrbit.MaxPitch;
        var pitch = Math.Max(SeatOrbit.MinPitch, Math.Min(Math.Min(state.Pitch + Finite(lookPitch) * SeatOrbit.LookPerUnit, SeatOrbit.MaxPitch), cap));
        return state with { Yaw = yaw, Pitch = pitch };
    }

    private static float Finite(float value) => FiniteFloatValidator.IsFinite(value) ? value : 0f;
}
