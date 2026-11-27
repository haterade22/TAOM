using System;

namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>A point the field trial's mouse steps click: found or not, the pixel and the fraction of the window it is at, and the ground point behind it when it is one.</summary>
internal readonly record struct SeatMousePoint(bool Found, float PixelX, float PixelY, float RangedX, float RangedY, float WorldX, float WorldY);

/// <summary>
/// The verdicts of the command mouse steps of the field trial, with no engine type; every number must be a real one, since a NaN never passes.
/// </summary>
internal static class SeatTrialMouse
{
    /// <summary>The order position of a company that was sent to a clicked point may be this far from it (design, section 3).</summary>
    internal const float MoveToleranceMetres = 3f;

    /// <summary>The company's facing after a dragged line may be this far, in degrees, from the line's normal.</summary>
    internal const float FacingToleranceDegrees = 15f;

    /// <summary>Half the length of the line the drag step draws, along the view's right.</summary>
    internal const float DragHalfMetres = 8f;

    /// <summary>The two ends of the dragged line must be at least this many pixels apart, or the drag is a click.</summary>
    internal const float DragMinPixels = 30f;

    /// <summary>Real seconds the drag's held frame is said to have lasted: past the 0.3 s of a click.</summary>
    internal const float DragSeconds = 0.5f;

    /// <summary>A plain click on a soldier left his company, and only it, selected.</summary>
    internal static bool Selected(bool onlyChosenCompany, bool faulted) => onlyChosenCompany && !faulted;

    /// <summary>Shift added a second company to the first, and a second Shift click on it took it off again, leaving the first alone.</summary>
    internal static bool Toggled(bool firstOnlyBefore, bool bothAfterAdd, bool firstOnlyAfterRemove, bool faulted) =>
        firstOnlyBefore && bothAfterAdd && firstOnlyAfterRemove && !faulted;

    /// <summary>The selected company's order position came within <see cref="MoveToleranceMetres"/> of the clicked point.</summary>
    internal static bool Moved(float distanceMetres, bool faulted) =>
        !faulted && IsReal(distanceMetres) && distanceMetres <= MoveToleranceMetres;

    /// <summary>The company faces the line's normal, on either side, within <see cref="FacingToleranceDegrees"/>.</summary>
    internal static bool FacedTheNormal(float errorDegrees, bool faulted) =>
        !faulted && IsReal(errorDegrees) && errorDegrees <= FacingToleranceDegrees;

    /// <summary>The selected company has the Charge order against the enemy company the click named.</summary>
    internal static bool Charged(bool chargeOrder, bool targetIsClicked, bool faulted) => chargeOrder && targetIsClicked && !faulted;

    /// <summary>The second order was taken by the queue (held key armed when it was given), and a step waits behind the first.</summary>
    internal static bool Queued(int captured, int queued, bool armedAtOrder, bool faulted) =>
        !faulted && armedAtOrder && captured >= 1 && queued >= 1;

    /// <summary>
    /// The angle in degrees between a facing and the normal of a line from one point to another, taking either normal: 0 along the normal, 90 along
    /// the line, NaN when either has no length.
    /// </summary>
    internal static float NormalError(float lineX, float lineY, float faceX, float faceY)
    {
        var line = Math.Sqrt((double)lineX * lineX + (double)lineY * lineY);
        var face = Math.Sqrt((double)faceX * faceX + (double)faceY * faceY);
        if (!(line > 1e-6) || !(face > 1e-6))
            return float.NaN;

        var cosine = Math.Abs((double)lineX * faceY - (double)lineY * faceX) / (line * face);
        return (float)(Math.Acos(Math.Min(1.0, cosine)) * 180.0 / Math.PI);
    }

    private static bool IsReal(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
