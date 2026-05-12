using System.Collections.Generic;

namespace TAOM.Features.EditorCacheRebuild.Diff;

public class SettlementDiff
{
    public HashSet<string> Added { get; init; } = new();
    public HashSet<string> Removed { get; init; } = new();
    public HashSet<string> Moved { get; init; } = new();
    public bool ForcedFullRebuild { get; init; }
    public string? Reason { get; init; }

    public int TotalChanged => Added.Count + Removed.Count + Moved.Count;

    public HashSet<string> AllChangedIds()
    {
        var s = new HashSet<string>(Added);
        s.UnionWith(Removed);
        s.UnionWith(Moved);
        return s;
    }
}
