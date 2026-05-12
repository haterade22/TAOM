using System;

namespace TAOM.Features.EditorCacheRebuild.Caching;

public readonly struct SortedPathKey : IEquatable<SortedPathKey>
{
    public readonly string Id1;
    public readonly bool IsPort1;
    public readonly string Id2;
    public readonly bool IsPort2;

    public SortedPathKey(string id1, bool isPort1, string id2, bool isPort2)
    {
        // Matches vanilla NavigationCacheElement<T>.Sort: swap iff num >= 0 && (num != 0 || !s1.IsPortUsed)
        // i.e. when ids are equal, the PORT entry comes first.
        int cmp = string.Compare(id1, id2, StringComparison.Ordinal);
        bool swap = cmp >= 0 && (cmp != 0 || !isPort1);
        if (swap)
        {
            Id1 = id2; IsPort1 = isPort2;
            Id2 = id1; IsPort2 = isPort1;
        }
        else
        {
            Id1 = id1; IsPort1 = isPort1;
            Id2 = id2; IsPort2 = isPort2;
        }
    }

    public bool Equals(SortedPathKey other) =>
        Id1 == other.Id1 && IsPort1 == other.IsPort1 &&
        Id2 == other.Id2 && IsPort2 == other.IsPort2;

    public override bool Equals(object obj) =>
        obj is SortedPathKey other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int h = (Id1?.GetHashCode() ?? 0) * 397;
            h = (h ^ (IsPort1 ? 1 : 0)) * 397;
            h = (h ^ (Id2?.GetHashCode() ?? 0)) * 397;
            return h ^ (IsPort2 ? 1 : 0);
        }
    }

    public override string ToString() =>
        $"{Id1}{(IsPort1 ? "(port)" : "")}<->{Id2}{(IsPort2 ? "(port)" : "")}";
}
