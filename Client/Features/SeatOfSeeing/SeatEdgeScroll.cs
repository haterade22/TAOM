using TAOM.Core.Validation;

namespace TAOM.Client.Features.SeatOfSeeing;

/// <summary>
/// The edge scroll of the Seat's flight (camera feel), with no engine type: the pointer's place in the window, as the engine reads it
/// (<c>Input.MousePositionRanged</c>, 0 to 1 across and down), becomes a push along the view's right and forward. The push is full inside
/// <see cref="Margin"/> of an edge and falls linearly to nothing over a further <see cref="RampWidth"/>. A pointer outside the window
/// (a windowed game with the mouse on another monitor) pushes nothing, and so does a position that is not a real number.
/// </summary>
internal static class SeatEdgeScroll
{
    /// <summary>The share of the window next to an edge where the push is full.</summary>
    internal const float Margin = 0.01f;

    /// <summary>The further share over which the push falls from full to nothing.</summary>
    internal const float RampWidth = 0.02f;

    /// <summary>The push along the view's right and forward, each from -1 to 1: the right edge pushes right, the top edge pushes forward.</summary>
    internal static (float Right, float Forward) Axes(float rangedX, float rangedY, float margin)
    {
        if (!Inside(rangedX) || !Inside(rangedY))
            return (0f, 0f);

        var full = FiniteFloatValidator.IsFinite(margin) && margin >= 0f ? margin : Margin;
        return (Push(rangedX, full), -Push(rangedY, full));
    }

    private static bool Inside(float ranged) => FiniteFloatValidator.IsFinite(ranged) && ranged >= 0f && ranged <= 1f;

    // Positive toward the high edge (1), negative toward the low edge (0); the two ramps never overlap while the margin stays under a quarter.
    private static float Push(float ranged, float margin) => Strength(1f - ranged, margin) - Strength(ranged, margin);

    private static float Strength(float distanceFromEdge, float margin)
    {
        if (distanceFromEdge <= margin)
            return 1f;
        var over = (distanceFromEdge - margin) / RampWidth;
        return over >= 1f ? 0f : 1f - over;
    }
}
