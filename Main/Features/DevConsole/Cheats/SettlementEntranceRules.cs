using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.DevConsole.Cheats;

/// <summary>
/// The engine-free rules of <c>taom.audit_settlement_entrances</c> (#1434). An entrance can sit on the main navmesh
/// island and still be useless to land parties: its face can be a terrain type they may not use, or no path can reach
/// it past such faces. Vanilla's distance cache queries paths with those terrain types excluded
/// (v1.5.7 SandBoxNavigationCache :23-25, :89), so an entrance it cannot reach gets 1E+30 to every settlement and
/// armies never arrive. Silas He's report named seven such entrances that the island check passed.
/// </summary>
internal static class SettlementEntranceRules
{
    public enum Verdict
    {
        Ok,
        OffMesh,
        OtherIsland,
        ExcludedTerrain,
        NoPath,
    }

    /// <param name="pathReached">Whether a path from any reference entrance reached this one; null when there was no
    /// reference to test from, which is not a finding.</param>
    public static Verdict Classify(bool onMesh, int island, int mainIsland, int faceGroup,
        IReadOnlyCollection<int> excludedFaceGroups, bool? pathReached)
    {
        if (!onMesh)
            return Verdict.OffMesh;
        if (island != mainIsland)
            return Verdict.OtherIsland;
        if (excludedFaceGroups.Contains(faceGroup))
            return Verdict.ExcludedTerrain;
        return pathReached == false ? Verdict.NoPath : Verdict.Ok;
    }

    /// <summary>
    /// Which candidates a land party can reach from the main map. Each candidate is path-tested from its
    /// <paramref name="referenceCount"/> nearest usable entrances, and each path that succeeds joins the two into one
    /// group. The largest group is the main map. A smaller group that no main member was tested against gets one try
    /// from the main member nearest to it; without that try, a cluster whose references all lie inside it would count
    /// as cut off. A path between two members of a pocket proves nothing about the main map (the Orthanc and Isengard
    /// gates are 2.7 apart and were each other's nearest reference).
    /// </summary>
    /// <param name="candidate">Passed the cheap checks; nothing else is path-tested or connected.</param>
    /// <param name="usable">May serve as a reference (a town or castle); counts only where it is also a candidate.</param>
    /// <param name="path">Whether a path from the first index reaches the second.</param>
    public static IReadOnlyList<bool> ConnectedToMain(IReadOnlyList<(float X, float Y)> points, IReadOnlyList<bool> candidate,
        IReadOnlyList<bool> usable, int referenceCount, Func<int, int, bool> path)
    {
        var n = points.Count;
        var parent = Enumerable.Range(0, n).ToArray();
        int Root(int i) => parent[i] == i ? i : parent[i] = Root(parent[i]);
        void Join(int a, int b) => parent[Root(a)] = Root(b);

        var refUsable = Enumerable.Range(0, n).Select(i => usable[i] && candidate[i]).ToList();
        var failed = new List<(int From, int To)>();
        var anyJoined = false;
        for (var i = 0; i < n; i++)
        {
            if (!candidate[i]) continue;
            foreach (var k in NearestReferences(points, i, refUsable, referenceCount))
            {
                if (path(k, i)) { Join(k, i); anyJoined = true; }
                else failed.Add((k, i));
            }
        }

        var main = Enumerable.Range(0, n).Where(i => candidate[i]).GroupBy(Root)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Min()).Select(g => (int?)g.Key).FirstOrDefault();
        // No path succeeded between two candidates: every group is a single entrance, and which one counted as
        // main would depend on file order. Report nothing connected rather than guess.
        if (main == null || (!anyJoined && candidate.Count(c => c) > 1))
            return new bool[n];

        var mainRoot = main.Value;
        var mainUsable = Enumerable.Range(0, n).Select(i => refUsable[i] && Root(i) == mainRoot).ToList();
        var others = Enumerable.Range(0, n).Where(i => candidate[i] && Root(i) != mainRoot).GroupBy(Root).ToList();
        foreach (var group in others)
        {
            var members = new HashSet<int>(group);
            if (failed.Any(f => members.Contains(f.To) && Root(f.From) == mainRoot))
                continue;
            var first = group.Min();
            var from = NearestReferences(points, first, mainUsable, 1);
            // No main member to test from is not a finding, as with Classify's null.
            if (from.Count == 0 || path(from[0], first))
                Join(first, mainRoot);
        }

        return Enumerable.Range(0, n).Select(i => candidate[i] && Root(i) == Root(mainRoot)).ToList();
    }

    /// <summary>Indices of the <paramref name="count"/> usable points nearest to <paramref name="self"/>, nearest
    /// first, never <paramref name="self"/> itself.</summary>
    public static IReadOnlyList<int> NearestReferences(IReadOnlyList<(float X, float Y)> points, int self,
        IReadOnlyList<bool> usable, int count)
    {
        var (sx, sy) = points[self];
        return Enumerable.Range(0, points.Count)
            .Where(i => i != self && usable[i])
            .OrderBy(i => (points[i].X - sx) * (points[i].X - sx) + (points[i].Y - sy) * (points[i].Y - sy))
            .Take(count)
            .ToList();
    }
}
