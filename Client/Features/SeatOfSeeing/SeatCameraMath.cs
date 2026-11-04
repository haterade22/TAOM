using System;
using TAOM.Client.Features.FordsOfWar;
using TAOM.Core.Validation;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Features.SeatOfSeeing;

/// <summary>
/// The arithmetic of the Seat's free camera (build-spec 5.2), with no engine type so it runs in a test. Ground is X and Y, Z is up;
/// yaw 0 looks along +Y and a positive yaw turns left; pitch is the angle below the horizon. The frame builder reuses
/// <see cref="FordsCameraMath.Basis"/> and the pan speed, so the Seat flies like the Fords camera. Every float from the engine is
/// checked with <see cref="FiniteFloatValidator"/> before it reaches the state, and a bad <c>dt</c> leaves the state alone.
/// </summary>
internal static class SeatCameraMath
{
    internal const float MinHeight = 1.5f;
    internal const float MaxHeight = 110f;
    internal const float MinPitch = -0.35f;
    internal const float MaxPitch = 1.45f;
    internal const float LiftSpeedBase = 8f;
    internal const float LiftSpeedPerMetre = 0.5f;
    internal const float ZoomPerNotch = 0.88f;
    internal const float LookPerUnit = 0.0035f;
    internal const float EntryHeight = 22f;
    internal const float EntryPitch = 0.60f;
    internal const float EntryBackstep = 9f;
    internal const float EntryBlendSeconds = 0.7f;
    internal const float GroundSmoothingRate = 6f;
    internal const float ListenerCeiling = 16f;

    /// <summary>The view never goes lower than this above the raw ground; the engine keeps its own camera the same distance off (MissionScreen.cs:1885-1888).</summary>
    internal const float GroundClearance = 0.5f;
    internal const float MinViewAngle = 50f;
    internal const float AxisDeadZone = 0.2f;

    private const float StickLookDivisor = 0.0009f;
    private const float StandardViewAngle = 65f;

    /// <summary>Metres per second at the given height; the Fords camera's speed.</summary>
    internal static float PanSpeed(float height) => FordsCameraMath.PanSpeed(height);

    /// <summary>Metres per second of rise or sink at the given height.</summary>
    internal static float LiftSpeed(float height) => LiftSpeedBase + LiftSpeedPerMetre * height;

    /// <summary>
    /// The flight one frame later: diagonal movement normalised, moved relative to the yaw at the speed of the height before this
    /// step, the height multiplied for zoom, lift added, then the height and pitch clamped and the yaw wrapped. A non-finite or
    /// negative <paramref name="dt"/> returns <paramref name="state"/> as it is; a non-finite input field counts as zero.
    /// </summary>
    internal static SeatFlight Step(SeatFlight state, SeatInput input, float dt)
    {
        if (!FiniteFloatValidator.IsFinite(dt) || dt < 0f)
            return state;

        var yaw = FordsCameraMath.WrapAngle(state.Yaw + Finite(input.LookYaw) * LookPerUnit);
        var pitch = FordsCameraMath.Clamp(state.Pitch + Finite(input.LookPitch) * LookPerUnit, MinPitch, MaxPitch);

        var speedScale = input.Fast ? 2f : 1f;
        var distance = PanSpeed(state.Height) * speedScale * dt;
        var (x, y) = FordsCameraMath.Pan(state.X, state.Y, yaw, Finite(input.Forward), Finite(input.Right), distance);

        var zoomed = state.Height * (float)Math.Pow(ZoomPerNotch, Finite(input.ZoomNotches));
        var height = FordsCameraMath.Clamp(zoomed + Finite(input.Lift) * LiftSpeed(state.Height) * speedScale * dt, MinHeight, MaxHeight);
        return new SeatFlight(x, y, height, yaw, pitch);
    }

    /// <summary>The flight that holds a pose: its position and angles, and the height above <paramref name="groundZ"/> held to the height range.</summary>
    internal static SeatFlight FlightOf(SeatPose pose, float groundZ) =>
        new(pose.X, pose.Y, FordsCameraMath.Clamp(pose.Z - groundZ, MinHeight, MaxHeight), pose.Yaw,
            FordsCameraMath.Clamp(pose.Pitch, MinPitch, MaxPitch));

    /// <summary>The camera pose for a flight over the given ground height.</summary>
    internal static SeatPose Pose(SeatFlight flight, float groundZ) =>
        new(flight.X, flight.Y, groundZ + flight.Height, flight.Yaw, flight.Pitch);

    /// <summary>
    /// The flight a first entry blends to: the camera's position backed off along the ground against its look direction, its yaw,
    /// and the entry height and pitch. A later entry (<paramref name="last"/> set) keeps the last height and pitch.
    /// </summary>
    internal static SeatFlight Enter(SeatPose camera, SeatFlight? last)
    {
        var back = FordsCameraMath.Forward(camera.Yaw);
        var height = last.HasValue ? last.Value.Height : EntryHeight;
        var pitch = last.HasValue ? last.Value.Pitch : EntryPitch;
        return new SeatFlight(camera.X - back.X * EntryBackstep, camera.Y - back.Y * EntryBackstep,
            FordsCameraMath.Clamp(height, MinHeight, MaxHeight), camera.Yaw, FordsCameraMath.Clamp(pitch, MinPitch, MaxPitch));
    }

    /// <summary>The flight that holds the camera frame exactly where it is (a battle that begins in the Seat): no backstep, no blend.</summary>
    internal static SeatFlight Hold(SeatPose camera, float groundZ) =>
        new(camera.X, camera.Y, FordsCameraMath.Clamp(camera.Z - groundZ, MinHeight, MaxHeight), camera.Yaw, FordsCameraMath.Clamp(camera.Pitch, MinPitch, MaxPitch));

    /// <summary>The smoothstep of <paramref name="t"/> held between 0 and 1.</summary>
    internal static float Smoothstep(float t)
    {
        var c = FordsCameraMath.Clamp(t, 0f, 1f);
        return c * c * (3f - 2f * c);
    }

    /// <summary>How far through the entry blend <paramref name="elapsedSeconds"/> is, 0 to 1 and linear.</summary>
    internal static float BlendFraction(float elapsedSeconds) =>
        FordsCameraMath.Clamp(elapsedSeconds / EntryBlendSeconds, 0f, 1f);

    /// <summary>Position and angles from one pose to the next with a smoothstep; the yaw takes the shortest arc.</summary>
    internal static SeatPose Blend(SeatPose from, SeatPose to, float t)
    {
        if (t <= 0f)
            return from;
        if (t >= 1f)
            return to;

        var s = Smoothstep(t);
        return new SeatPose(
            from.X + (to.X - from.X) * s,
            from.Y + (to.Y - from.Y) * s,
            from.Z + (to.Z - from.Z) * s,
            FordsCameraMath.WrapAngle(from.Yaw + FordsCameraMath.WrapAngle(to.Yaw - from.Yaw) * s),
            from.Pitch + (to.Pitch - from.Pitch) * s);
    }

    /// <summary>The share of the way to the target ground height one frame covers: <c>1 - exp(-6 dt)</c>.</summary>
    internal static float GroundSmoothing(float dt) => 1f - (float)Math.Exp(-GroundSmoothingRate * dt);

    /// <summary>The ground height under the camera eased toward its target; a bad <paramref name="dt"/> or target keeps the current value.</summary>
    internal static float SmoothGround(float current, float target, float dt)
    {
        if (!FiniteFloatValidator.IsFinite(dt) || dt < 0f || !FiniteFloatValidator.IsFinite(target))
            return current;
        return current + (target - current) * GroundSmoothing(dt);
    }

    /// <summary>
    /// The floor (build-spec 5.2). Smoothing lags the ground on a slope, so a low, fast view would go under the terrain; this
    /// returns the pose with <c>Z</c> at least <paramref name="rawGround"/> plus <paramref name="clearance"/> (a flight frame keeps
    /// <see cref="GroundClearance"/>; an orbit passes <see cref="MinHeight"/>, the lowest height a flight holds, so letting go has no jump),
    /// and the pose as it is when the raw ground is not a real number.
    /// </summary>
    internal static SeatPose AboveGround(SeatPose pose, float rawGround, float clearance = GroundClearance)
    {
        if (!FiniteFloatValidator.IsFinite(rawGround))
            return pose;
        var floor = rawGround + clearance;
        return pose.Z >= floor ? pose : new SeatPose(pose.X, pose.Y, floor, pose.Yaw, pose.Pitch);
    }

    /// <summary>True when a scene's <c>camera_height_limiter</c> gives a usable limit: a real number above 0. No tag reads as 0, which is no limit.</summary>
    internal static bool HasHeightLimit(float limit) => FiniteFloatValidator.IsFinite(limit) && limit > 0f;

    /// <summary>The pose with <c>Z</c> no higher than the scene's height limit (MissionScreen.cs:1894-1897); unchanged with no limit.</summary>
    internal static SeatPose CapHeight(SeatPose pose, float limit) =>
        HasHeightLimit(limit) && pose.Z > limit ? new SeatPose(pose.X, pose.Y, limit, pose.Yaw, pose.Pitch) : pose;

    /// <summary>
    /// The flight with its height no higher than <paramref name="limit"/> minus the ground under it, floored at <see cref="MinHeight"/>,
    /// so the wheel does not wind the height up past a limit it cannot show. Unchanged with no limit or a ground that is not real.
    /// </summary>
    internal static SeatFlight CapFlight(SeatFlight flight, float limit, float ground)
    {
        if (!HasHeightLimit(limit))
            return flight;
        var room = Math.Max(MinHeight, limit - ground);
        return flight.Height > room ? flight with { Height = room } : flight;
    }

    /// <summary>The sound listener's height: the camera's, but no higher than <see cref="ListenerCeiling"/> above the ground.</summary>
    internal static float ListenerHeight(float cameraZ, float groundZ) => Math.Min(cameraZ, groundZ + ListenerCeiling);

    /// <summary>The view angle the Seat holds: the one the player had at entry, floored at <see cref="MinViewAngle"/>.</summary>
    internal static float ViewAngle(float entryAngle) =>
        FiniteFloatValidator.IsFinite(entryAngle) ? Math.Max(MinViewAngle, entryAngle) : MinViewAngle;

    /// <summary>The engine's zoom factor for a view angle, <c>65 / angle</c>.</summary>
    internal static float ZoomFactor(float viewAngle) => StandardViewAngle / viewAngle;

    /// <summary>An axis with the engine's dead zone: a magnitude under <see cref="AxisDeadZone"/> reads as zero.</summary>
    internal static float DeadZone(float axis) =>
        FiniteFloatValidator.IsFinite(axis) && Math.Abs(axis) >= AxisDeadZone ? axis : 0f;

    /// <summary>
    /// Look units from the mouse and, with no active mouse, the sticks scaled by <c>dt / 0.0009</c>, in the sign the Seat's input
    /// uses: a positive yaw turns left, so rolling the mouse right is negative; a positive pitch looks down, so the mouse moving
    /// down is positive unless the player inverts it. The same signs as MissionScreen.UpdateDragData.
    /// </summary>
    internal static (float Yaw, float Pitch) Look(float moveX, float moveY, float stickX, float stickY, float dt, float sensitivity,
        bool invertMouse, bool mouseActive)
    {
        var x = Finite(moveX);
        var y = Finite(moveY);
        if (!mouseActive && FiniteFloatValidator.IsFinite(dt) && dt >= 0f)
        {
            var scale = dt / StickLookDivisor;
            x += scale * Finite(stickX);
            y -= scale * Finite(stickY);
        }

        var s = Finite(sensitivity);
        return (-x * s, (invertMouse ? -y : y) * s);
    }

    /// <summary>The pose for a camera at a position looking along a direction (the camera frame's own look vector).</summary>
    internal static SeatPose PoseFromLook(float x, float y, float z, float lookX, float lookY, float lookZ)
    {
        var ground = Math.Sqrt(lookX * lookX + lookY * lookY);
        var yaw = ground < 1e-4 ? 0f : (float)Math.Atan2(-lookX, lookY);
        var pitch = (float)Math.Atan2(-lookZ, ground);
        return new SeatPose(x, y, z, yaw, pitch);
    }

    /// <summary>True when every member of the pose is a real number.</summary>
    internal static bool IsFinite(SeatPose pose) =>
        FiniteFloatValidator.IsFinite(pose.X) && FiniteFloatValidator.IsFinite(pose.Y) && FiniteFloatValidator.IsFinite(pose.Z)
        && FiniteFloatValidator.IsFinite(pose.Yaw) && FiniteFloatValidator.IsFinite(pose.Pitch);

    private static float Finite(float value) => FiniteFloatValidator.IsFinite(value) ? value : 0f;

}
