using System;
using System.Collections.Generic;

namespace TAOM.Features.StagedSiege.Domain;

/// <summary>What the detachment holding point A is told to do.</summary>
public enum DetachmentOrder
{
    Move,
    Charge,
}

/// <summary>The size, the ground slots and the charge-or-hold decision of the defender detachment at point A.</summary>
public static class DetachmentRule
{
    private const int PerRow = 10;
    private const float SpacingMetres = 1.5f;

    /// <summary>A share of the defenders' infantry, floored and capped. Zero for a non-finite share or no infantry.</summary>
    public static int Size(int infantry, float share, int cap)
    {
        if (infantry <= 0 || cap <= 0 || float.IsNaN(share) || float.IsInfinity(share) || share <= 0f)
            return 0;
        var size = Math.Floor((double)share * infantry);
        return (int)Math.Min(size, cap);
    }

    /// <summary>
    /// Charge as soon as an attacker is within two radii of the point; go back to holding only once none is within three, so a
    /// single attacker stepping in and out of range does not make the detachment flap.
    /// </summary>
    public static DetachmentOrder Decide(DetachmentOrder current, int attackersWithin2r, int attackersWithin3r)
    {
        if (attackersWithin2r >= 1)
            return DetachmentOrder.Charge;
        return current == DetachmentOrder.Charge && attackersWithin3r > 0 ? DetachmentOrder.Charge : DetachmentOrder.Move;
    }

    /// <summary>
    /// Ground slots for <paramref name="count"/> units: rows of ten, 1.5 m apart along the line perpendicular to the facing, each
    /// row centred on the frame and 1.5 m behind the one before it.
    /// </summary>
    public static IReadOnlyList<StagePoint> Slots(StageFrame frame, int count)
    {
        var slots = new List<StagePoint>(Math.Max(0, count));
        var fx = frame.Facing.X;
        var fy = frame.Facing.Y;
        // The line across the facing runs along (fy, -fx); behind is (-fx, -fy).
        for (var i = 0; i < count; i++)
        {
            var row = i / PerRow;
            var inRow = Math.Min(PerRow, count - row * PerRow);
            var across = (i % PerRow - (inRow - 1) / 2f) * SpacingMetres;
            var back = row * SpacingMetres;
            slots.Add(new StagePoint(
                frame.Position.X + fy * across - fx * back,
                frame.Position.Y - fx * across - fy * back,
                frame.Position.Z));
        }

        return slots;
    }
}
