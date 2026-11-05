using System;

namespace TAOM.Features.MarshalsWord.Domain;

/// <summary>One read of a company in the plain engine square: its side, the soldiers along a side, the ranks, the soldiers, how many have a slot, and whether Patch30's mixed layout answers for it.</summary>
internal readonly record struct SquareRead(int Company, float Side, int SideUnits, int Ranks, int Units, int Placed, bool LaidOut);

/// <summary>The hollow square's small rules (build-spec 9.1), over plain floats so a prefix on the engine's private methods stays thin.</summary>
internal static class MarshalSquareRules
{
    /// <summary>
    /// The side a square keeps when a unit joins or leaves: its current side, grown to the arrangement's minimum when more soldiers than the side holds
    /// would be left with no slot. False for a number that is not finite, and vanilla then runs.
    /// </summary>
    internal static bool TryKeptSide(float width, float minimumWidth, out float side)
    {
        side = 0f;
        if (!IsFinite(width) || !IsFinite(minimumWidth))
            return false;
        side = Math.Max(width, minimumWidth);
        return true;
    }

    /// <summary>The side of a square read as a desired width: clamped to the arrangement's bounds, the minimum winning when the bounds cross.</summary>
    internal static bool TryDesiredWidth(float width, float minimumWidth, float maximumWidth, out float side)
    {
        side = 0f;
        if (!IsFinite(width) || !IsFinite(minimumWidth) || !IsFinite(maximumWidth))
            return false;
        side = Math.Max(minimumWidth, Math.Min(width, maximumWidth));
        return true;
    }

    /// <summary>Every soldier of a company with soldiers has a slot.</summary>
    internal static bool ProofPasses(int placed, int units) => units > 0 && placed == units;

    /// <summary>The first line of a square is due at once, and a changed unit count writes another at most once per <c>SquareLineSeconds</c>.</summary>
    internal static bool ProofLineDue(double now, double lastLine, int lastUnits, int units) =>
        double.IsNegativeInfinity(lastLine) || (units != lastUnits && now - lastLine >= MarshalTuning.SquareLineSeconds);

    /// <summary>Soldiers along one side of a square of this side width; 0 when the inputs are not finite or not positive.</summary>
    internal static int UnitsAlongSide(float side, float unitDiameter, float interval)
    {
        if (!IsFinite(side) || !IsFinite(unitDiameter) || !IsFinite(interval) || !(unitDiameter + interval > 0f) || side < unitDiameter)
            return 0;
        return (int)Math.Round((side - unitDiameter) / (unitDiameter + interval)) + 1;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
