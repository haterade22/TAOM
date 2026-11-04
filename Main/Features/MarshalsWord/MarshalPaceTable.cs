using TaleWorlds.MountAndBlade;

namespace TAOM.Features.MarshalsWord;

/// <summary>An immutable table of paces, published whole by one reference write and read by the speed postfix on any thread.</summary>
internal sealed class MarshalPaceTable
{
    internal readonly Formation[] Formations;
    internal readonly float[] Paces;

    private const float Floor = 0.2f;

    internal MarshalPaceTable(Formation[] formations, float[] paces)
    {
        Formations = formations;
        Paces = paces;
    }

    /// <summary>
    /// The speed multiplier scaled by the company's pace, clamped to 0.2 to 1 (the range the engine's own value has); a company with no pace in the
    /// table keeps the multiplier. Found by reference, with no lock, no allocation and no call into the formation: it runs on three threads.
    /// </summary>
    internal float Scale(Formation? formation, float multiplier)
    {
        for (var i = 0; i < Formations.Length; i++)
        {
            if (!ReferenceEquals(Formations[i], formation))
                continue;
            var scaled = multiplier * Paces[i];
            return scaled < Floor ? Floor : scaled > 1f ? 1f : scaled;
        }

        return multiplier;
    }
}
