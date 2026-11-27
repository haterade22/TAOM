using System;

namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>
/// The verdicts of the three camera feel steps of the field trial (edge_pan, rotate_pivot, zoom_smooth), with no engine type. The client's
/// script measures the camera; these decide. Every number must be a real one: a measure that is NaN never passes.
/// </summary>
internal static class SeatTrialCamera
{
    /// <summary>Real seconds of pointer at the edge in <c>edge_pan</c>.</summary>
    internal const float EdgeSeconds = 1f;

    /// <summary>The pointer's place in <c>edge_pan</c>: the right edge, inside the full-push margin.</summary>
    internal const float EdgePointerX = 0.995f;

    internal const float EdgePointerY = 0.5f;

    /// <summary>The view must have flown at least this far to the right of its heading.</summary>
    internal const float EdgeMinMetres = 2f;

    /// <summary>The view's own centre point may move this far in <c>rotate_pivot</c>, while it turns and after it lets go.</summary>
    internal const float PivotDriftMetres = 2f;

    /// <summary>The camera must have swung at least this far round the point, or the turn was only in place.</summary>
    internal const float RotateMinSwingMetres = 1f;

    /// <summary>Real seconds of the middle button held in <c>rotate_pivot</c>.</summary>
    internal const float RotateSeconds = 1f;

    /// <summary>Look units a frame while the middle button is held.</summary>
    internal const float RotateLookUnits = 3f;

    /// <summary>Real seconds the zoom is watched after its notch, and the time by which it must have arrived.</summary>
    internal const float ZoomWatchSeconds = 0.6f;

    internal const float ZoomArriveSeconds = 0.5f;

    /// <summary>Slack on the heights read: the ground under the view eases too, so a height above the raw ground wobbles a little.</summary>
    internal const float ZoomSlack = 0.02f;

    internal const float ZoomEndSlack = 0.03f;

    /// <summary>Flew to the right of the heading by the least distance, and no more than half as far along it, with the cursor shown.</summary>
    internal static bool EdgePanned(float rightMetres, float forwardMetres, bool cursorShown, bool faulted) =>
        cursorShown && !faulted && rightMetres >= EdgeMinMetres && Math.Abs(forwardMetres) <= rightMetres * 0.5f;

    /// <summary>
    /// The camera swung round the point and the point stayed at the centre of the view, both while the button was held and after it was
    /// let go (the hand-back to the flight has no jump).
    /// </summary>
    internal static bool PivotHeld(float driftWhileHeld, float cameraSwung, float driftAfterRelease, bool faulted) =>
        !faulted && cameraSwung >= RotateMinSwingMetres && driftWhileHeld <= PivotDriftMetres && driftAfterRelease <= PivotDriftMetres;

    /// <summary>
    /// One notch took the height down to 0.88 of its start, smoothly: the first frame after it was only part of the way, the height never
    /// rose by more than <see cref="ZoomSlack"/> of the start, it was within the slack of the target by <see cref="ZoomArriveSeconds"/>, and
    /// it ended within <see cref="ZoomEndSlack"/> of it.
    /// </summary>
    internal static bool ZoomEased(float start, float first, float maxRise, float arrivedSeconds, float end, bool faulted)
    {
        if (faulted || !(start > 0f))
            return false;
        var target = start * 0.88f;
        return first > target * (1f + ZoomSlack) && first < start && maxRise <= start * ZoomSlack && arrivedSeconds <= ZoomArriveSeconds
            && Math.Abs(end - target) <= target * ZoomEndSlack;
    }
}
