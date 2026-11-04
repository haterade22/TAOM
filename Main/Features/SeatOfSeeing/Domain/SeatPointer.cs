using TAOM.Core.Validation;

namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>The cursor in the coordinates of the formation focus handler (build-spec 6.3).</summary>
internal static class SeatPointer
{
    /// <summary>
    /// The pixel the handler compares with <c>WorldToScreenInsideUsableArea</c>, which subtracts the usable area's margins: the ranged
    /// mouse position times the resolution, less half of what the usable area leaves out. False when any input is not a real number
    /// or the resolution or the usable area is not positive, and the handler then keeps its own point.
    /// </summary>
    internal static bool TryInUsableArea(float rangedX, float rangedY, float width, float height, float usableX, float usableY,
        out float x, out float y)
    {
        x = y = 0f;
        if (!FiniteFloatValidator.IsFinite(rangedX) || !FiniteFloatValidator.IsFinite(rangedY) || !FiniteFloatValidator.IsFinite(width)
            || !FiniteFloatValidator.IsFinite(height) || !FiniteFloatValidator.IsFinite(usableX) || !FiniteFloatValidator.IsFinite(usableY))
            return false;
        if (width <= 0f || height <= 0f || usableX <= 0f || usableY <= 0f)
            return false;

        x = rangedX * width - (width - usableX * width) / 2f;
        y = rangedY * height - (height - usableY * height) / 2f;
        return true;
    }
}
