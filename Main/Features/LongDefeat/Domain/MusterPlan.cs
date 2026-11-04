using System;
using System.Collections.Generic;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>A position on the field, in the engine's metres.</summary>
internal readonly record struct LdPoint(float X, float Y, float Z);

/// <summary>One timed batch of the garrison's foot muster.</summary>
internal sealed record MusterBatch(int Count, float DelaySeconds);

/// <summary>Splits the garrison into timed batches, and counts how many of a batch landed on soldiers from an earlier one.</summary>
internal static class MusterPlan
{
    /// <summary>
    /// Batches of <c>batchSize</c>, the last one holding the remainder, the first now and each later one <c>seconds</c> after the
    /// one before. A negative or NaN interval means no delay.
    /// </summary>
    internal static IReadOnlyList<MusterBatch> Split(int total, int batchSize, float seconds)
    {
        var batches = new List<MusterBatch>();
        if (total <= 0)
            return batches;

        batchSize = Math.Max(1, batchSize);
        var gap = seconds >= 0f ? seconds : 0f;
        for (int left = total, i = 0; left > 0; left -= batchSize, i++)
            batches.Add(new MusterBatch(Math.Min(batchSize, left), gap * i));
        return batches;
    }

    /// <summary>
    /// How many of the new soldiers stand within <c>radius</c> of a soldier from an earlier batch. Batches reuse the same spawn
    /// slots, so a batch that is still on its slots when the next arrives shows up here. A bad radius or point counts nothing.
    /// </summary>
    internal static int CountOverlaps(IReadOnlyList<LdPoint> newPoints, IReadOnlyList<LdPoint> earlierPoints, float radius)
    {
        if (!(radius > 0f))
            return 0;

        var limit = radius * radius;
        var overlaps = 0;
        foreach (var n in newPoints)
        {
            foreach (var e in earlierPoints)
            {
                float dx = n.X - e.X, dy = n.Y - e.Y, dz = n.Z - e.Z;
                if (dx * dx + dy * dy + dz * dz <= limit)
                {
                    overlaps++;
                    break;
                }
            }
        }

        return overlaps;
    }
}
