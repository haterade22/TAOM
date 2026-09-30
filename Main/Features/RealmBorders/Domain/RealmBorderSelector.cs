using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.RealmBorders.Domain;

/// <summary>
/// Turns the fixed province chains into the realm borders of the moment. A chain is a border only
/// when its two provinces belong to two different realms; it is turned so the ordinally lower realm
/// is on its left, and chains of the same realm pair that meet head to tail at a junction, where the
/// third province belongs to one of the two realms, merge into one continuous line. Re-running this
/// after a capture is the whole cost of moving a border.
/// </summary>
public static class RealmBorderSelector
{
    public static IReadOnlyList<RealmBorderLine> Select(IReadOnlyList<ProvinceBoundary> boundaries, IReadOnlyList<string?> realmOfProvince)
    {
        if (boundaries == null)
            throw new ArgumentNullException(nameof(boundaries));
        if (realmOfProvince == null)
            throw new ArgumentNullException(nameof(realmOfProvince));

        var lines = new List<RealmBorderLine>();
        var open = new Dictionary<(string, string), List<List<LatticePoint>>>();
        foreach (var boundary in boundaries)
        {
            string? left = RealmOf(realmOfProvince, boundary.Left);
            string? right = RealmOf(realmOfProvince, boundary.Right);
            if (left == null || right == null || left == right)
                continue;

            var points = boundary.Points.ToList();
            if (string.CompareOrdinal(left, right) > 0)
            {
                points.Reverse();
                (left, right) = (right, left);
            }

            if (boundary.IsClosed)
            {
                lines.Add(new RealmBorderLine(left, right, points, isClosed: true));
                continue;
            }
            if (!open.TryGetValue((left, right), out var pieces))
                open[(left, right)] = pieces = new List<List<LatticePoint>>();
            pieces.Add(points);
        }

        foreach (var pair in open)
            Merge(pair.Key.Item1, pair.Key.Item2, pair.Value, lines);
        return lines;
    }

    private static string? RealmOf(IReadOnlyList<string?> realmOfProvince, int province) =>
        province >= 0 && province < realmOfProvince.Count ? realmOfProvince[province] : null;

    /// <summary>
    /// Joins a piece's tail to the piece starting there when exactly one piece of this realm pair
    /// starts and exactly one ends at that corner; anything busier is a real end of the line.
    /// </summary>
    private static void Merge(string left, string right, List<List<LatticePoint>> pieces, List<RealmBorderLine> lines)
    {
        var starts = new Dictionary<LatticePoint, List<int>>();
        var ends = new Dictionary<LatticePoint, List<int>>();
        for (int i = 0; i < pieces.Count; i++)
        {
            Index(starts, pieces[i][0], i);
            Index(ends, pieces[i][pieces[i].Count - 1], i);
        }

        bool Joinable(LatticePoint corner) =>
            starts.TryGetValue(corner, out var s) && s.Count == 1 && ends.TryGetValue(corner, out var e) && e.Count == 1;

        var visited = new bool[pieces.Count];
        for (int i = 0; i < pieces.Count; i++)
        {
            if (!visited[i] && !Joinable(pieces[i][0]))
                lines.Add(new RealmBorderLine(left, right, Walk(i, pieces, starts, visited, Joinable, out _), isClosed: false));
        }

        // whatever is left joins round into rings: every corner on them was joinable
        for (int i = 0; i < pieces.Count; i++)
        {
            if (visited[i])
                continue;
            var ring = Walk(i, pieces, starts, visited, Joinable, out bool returned);
            if (returned)
                ring.RemoveAt(ring.Count - 1);
            lines.Add(new RealmBorderLine(left, right, ring, isClosed: returned));
        }
    }

    private static List<LatticePoint> Walk(
        int first,
        List<List<LatticePoint>> pieces,
        Dictionary<LatticePoint, List<int>> starts,
        bool[] visited,
        Func<LatticePoint, bool> joinable,
        out bool returnedToStart)
    {
        var points = new List<LatticePoint>(pieces[first]);
        visited[first] = true;
        returnedToStart = false;
        LatticePoint tail = points[points.Count - 1];
        while (joinable(tail))
        {
            int next = starts[tail][0];
            if (visited[next])
            {
                returnedToStart = next == first;
                break;
            }
            visited[next] = true;
            var piece = pieces[next];
            for (int k = 1; k < piece.Count; k++)
                points.Add(piece[k]);
            tail = points[points.Count - 1];
        }
        return points;
    }

    private static void Index(Dictionary<LatticePoint, List<int>> index, LatticePoint corner, int piece)
    {
        if (!index.TryGetValue(corner, out var list))
            index[corner] = list = new List<int>(1);
        list.Add(piece);
    }
}
