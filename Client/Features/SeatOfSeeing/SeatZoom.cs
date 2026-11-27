using System;
using TAOM.Core.Validation;

namespace TAOM.Client.Features.SeatOfSeeing;

/// <summary>The view's height now and the height the wheel has asked for.</summary>
internal readonly record struct SeatZoomState(float Height, float Target);

/// <summary>
/// The Seat's smooth wheel zoom, with no engine type: each notch multiplies the target height by <see cref="SeatCameraMath.ZoomPerNotch"/>,
/// the target stays between the lowest height and a ceiling (the highest height, or the scene's height limit less the ground), and the
/// height eases toward it by <c>1 - exp(-12 dt)</c> a frame. The ceiling that caps the target is the one that caps the flight, so the
/// two cannot disagree.
/// </summary>
internal static class SeatZoom
{
    internal const float EaseRate = 12f;

    /// <summary>The share of the way to the target one frame covers: <c>1 - exp(-12 dt)</c>.</summary>
    internal static float Ease(float dt) => 1f - (float)Math.Exp(-EaseRate * dt);

    /// <summary>
    /// One frame. A non-finite or negative <paramref name="dt"/> returns the state as it is; a notch count that is not a real number counts
    /// as zero, a target that is not a real number starts from the height, and a ceiling that is not a real number is the highest height.
    /// </summary>
    internal static SeatZoomState Step(float height, float target, float notches, float dt, float ceiling = SeatCameraMath.MaxHeight)
    {
        if (!FiniteFloatValidator.IsFinite(dt) || dt < 0f || !FiniteFloatValidator.IsFinite(height))
            return new SeatZoomState(height, target);

        var top = FiniteFloatValidator.IsFinite(ceiling) ? Math.Max(SeatCameraMath.MinHeight, Math.Min(ceiling, SeatCameraMath.MaxHeight)) : SeatCameraMath.MaxHeight;
        var from = FiniteFloatValidator.IsFinite(target) ? target : height;
        var asked = from * (float)Math.Pow(SeatCameraMath.ZoomPerNotch, FiniteFloatValidator.IsFinite(notches) ? notches : 0f);
        var next = Math.Max(SeatCameraMath.MinHeight, Math.Min(asked, top));
        return new SeatZoomState(height + (next - height) * Ease(dt), next);
    }
}
