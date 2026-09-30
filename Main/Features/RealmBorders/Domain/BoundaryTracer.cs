using System;
using System.Collections.Generic;

namespace TAOM.Features.RealmBorders.Domain;

/// <summary>
/// Reads the province edges off a <see cref="ProvinceMap"/> as chains. Every unit edge between two
/// claimed cells of different provinces is directed so the lower province is on its left; edges of
/// the same pair then join head to tail, which runs each chain from one junction to the next or
/// round an enclave. Edges against wild land or water are not borders and are skipped.
/// </summary>
public static class BoundaryTracer
{
    public static IReadOnlyList<ProvinceBoundary> Trace(ProvinceMap map)
    {
        if (map == null)
            throw new ArgumentNullException(nameof(map));

        var edges = CollectEdges(map);
        var byFrom = new Dictionary<(LatticePoint, int, int), List<int>>();
        var byTo = new Dictionary<(LatticePoint, int, int), List<int>>();
        for (int i = 0; i < edges.Count; i++)
        {
            Edge e = edges[i];
            Add(byFrom, (e.From, e.Left, e.Right), i);
            Add(byTo, (e.To, e.Left, e.Right), i);
        }

        var used = new bool[edges.Count];
        var chains = new List<ProvinceBoundary>();
        for (int i = 0; i < edges.Count; i++)
        {
            if (used[i])
                continue;
            used[i] = true;
            Edge first = edges[i];
            var forward = new List<LatticePoint> { first.From, first.To };
            bool closed = false;
            LatticePoint at = first.To;
            while (TryTake(byFrom, (at, first.Left, first.Right), used, out int next))
            {
                at = edges[next].To;
                if (at.Equals(forward[0]))
                {
                    closed = true;
                    break;
                }
                forward.Add(at);
            }

            if (!closed)
            {
                var backward = new List<LatticePoint>();
                at = first.From;
                while (TryTake(byTo, (at, first.Left, first.Right), used, out int previous))
                {
                    at = edges[previous].From;
                    backward.Add(at);
                }
                if (backward.Count > 0)
                {
                    backward.Reverse();
                    backward.AddRange(forward);
                    forward = backward;
                }
            }
            chains.Add(new ProvinceBoundary(first.Left, first.Right, forward, closed));
        }
        return chains;
    }

    private static List<Edge> CollectEdges(ProvinceMap map)
    {
        var edges = new List<Edge>();
        for (int row = 0; row < map.Rows; row++)
        {
            for (int column = 0; column < map.Columns; column++)
            {
                int a = map[column, row];
                if (a == ProvinceMap.Unclaimed)
                    continue;

                int east = map[column + 1, row];
                if (east != ProvinceMap.Unclaimed && east != a)
                {
                    // the shared edge is x = column + 1; heading north keeps the west cell on the left
                    var south = new LatticePoint(column + 1, row);
                    var north = new LatticePoint(column + 1, row + 1);
                    edges.Add(a < east ? new Edge(south, north, a, east) : new Edge(north, south, east, a));
                }

                int above = map[column, row + 1];
                if (above != ProvinceMap.Unclaimed && above != a)
                {
                    // the shared edge is y = row + 1; heading east keeps the north cell on the left
                    var west = new LatticePoint(column, row + 1);
                    var eastEnd = new LatticePoint(column + 1, row + 1);
                    edges.Add(above < a ? new Edge(west, eastEnd, above, a) : new Edge(eastEnd, west, a, above));
                }
            }
        }
        return edges;
    }

    private static void Add(Dictionary<(LatticePoint, int, int), List<int>> index, (LatticePoint, int, int) key, int edge)
    {
        if (!index.TryGetValue(key, out var list))
            index[key] = list = new List<int>(2);
        list.Add(edge);
    }

    private static bool TryTake(Dictionary<(LatticePoint, int, int), List<int>> index, (LatticePoint, int, int) key, bool[] used, out int edge)
    {
        edge = -1;
        if (!index.TryGetValue(key, out var list))
            return false;
        foreach (int candidate in list)
        {
            if (used[candidate])
                continue;
            used[candidate] = true;
            edge = candidate;
            return true;
        }
        return false;
    }

    private readonly struct Edge
    {
        public Edge(LatticePoint from, LatticePoint to, int left, int right)
        {
            From = from;
            To = to;
            Left = left;
            Right = right;
        }

        public LatticePoint From { get; }

        public LatticePoint To { get; }

        public int Left { get; }

        public int Right { get; }
    }
}
