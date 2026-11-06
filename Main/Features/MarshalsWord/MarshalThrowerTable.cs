using TaleWorlds.MountAndBlade;

namespace TAOM.Features.MarshalsWord;

/// <summary>An immutable table of stand points by company, published whole by one reference write and read by the thrower patches on any thread.</summary>
internal sealed class MarshalThrowerTable
{
    internal readonly Formation[] Formations;
    internal readonly float[] X;
    internal readonly float[] Y;

    internal MarshalThrowerTable(Formation[] formations, float[] x, float[] y)
    {
        Formations = formations;
        X = x;
        Y = y;
    }

    /// <summary>The slot of a formation, found by reference with no lock and no allocation; -1 when it has none. <c>Formation.GetHashCode</c> reads the team, so no hash lookup is safe here.</summary>
    internal int IndexOf(Formation? formation)
    {
        for (var i = 0; i < Formations.Length; i++)
        {
            if (ReferenceEquals(Formations[i], formation))
                return i;
        }

        return -1;
    }
}
