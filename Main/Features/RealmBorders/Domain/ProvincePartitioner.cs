using System;
using System.Collections.Generic;

namespace TAOM.Features.RealmBorders.Domain;

/// <summary>
/// Grows every province at once from its seeds with a multi-source Dijkstra over the terrain grid,
/// eight-connected, each step costing its length times the mean class cost of the two cells. A cell
/// goes to the province that reaches it cheapest; walls and water are never entered; land nobody
/// reaches within <see cref="PartitionSettings.MaxClaim"/> stays wild; small wild pockets then join
/// the realm around them. Pure managed code over arrays, so it can run off the game thread.
/// </summary>
public static class ProvincePartitioner
{
    private const float Sqrt2 = 1.41421356f;

    private static readonly (int Dc, int Dr)[] Steps =
    {
        (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1),
    };

    public static ProvinceMap Partition(TerrainGrid grid, IReadOnlyList<ProvinceSeed> seeds, PartitionSettings settings)
    {
        if (grid == null)
            throw new ArgumentNullException(nameof(grid));
        if (seeds == null)
            throw new ArgumentNullException(nameof(seeds));
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        int columns = grid.Columns, rows = grid.Rows, count = columns * rows;
        var labels = new int[count];
        var cost = new float[count];
        for (int i = 0; i < count; i++)
        {
            labels[i] = ProvinceMap.Unclaimed;
            cost[i] = float.PositiveInfinity;
        }

        float[] classCost = ClassCosts(settings);
        var heap = new CellHeap(Math.Max(16, seeds.Count * 4));
        for (int s = 0; s < seeds.Count; s++)
        {
            ProvinceSeed seed = seeds[s];
            if (!TryStartCell(grid, seed, settings.SeedSearchRadius, out int cell))
                continue;
            float start = -seed.HeadStart;
            if (start < cost[cell])
            {
                cost[cell] = start;
                labels[cell] = seed.Province;
                heap.Push(start, cell);
            }
        }

        while (heap.TryPop(out float here, out int cell))
        {
            if (here > cost[cell])
                continue;
            int column = cell % columns, row = cell / columns;
            float hereCost = classCost[(int)grid.At(cell)];
            foreach (var (dc, dr) in Steps)
            {
                int nc = column + dc, nr = row + dr;
                if ((uint)nc >= (uint)columns || (uint)nr >= (uint)rows)
                    continue;
                int next = nr * columns + nc;
                float nextCost = classCost[(int)grid.At(next)];
                if (float.IsPositiveInfinity(nextCost))
                    continue;
                bool diagonal = dc != 0 && dr != 0;
                if (diagonal && IsBlocked(grid, classCost, column + dc, row) && IsBlocked(grid, classCost, column, row + dr))
                    continue; // never slip diagonally between two wall cells
                float reach = here + grid.CellSize * (diagonal ? Sqrt2 : 1f) * 0.5f * (hereCost + nextCost);
                if (reach > settings.MaxClaim || !(reach < cost[next]))
                    continue;
                cost[next] = reach;
                labels[next] = labels[cell];
                heap.Push(reach, next);
            }
        }

        if (settings.PocketCells > 0)
            MergePockets(grid, labels, settings.PocketCells);
        return new ProvinceMap(columns, rows, grid.MinX, grid.MinY, grid.CellSize, labels);
    }

    private static float[] ClassCosts(PartitionSettings settings)
    {
        var costs = new float[6];
        costs[(int)TerrainClass.Open] = settings.OpenCost;
        costs[(int)TerrainClass.Rough] = settings.RoughCost;
        costs[(int)TerrainClass.Wall] = float.PositiveInfinity;
        costs[(int)TerrainClass.River] = settings.RiverCost;
        costs[(int)TerrainClass.Crossing] = settings.CrossingCost;
        costs[(int)TerrainClass.Water] = float.PositiveInfinity;
        return costs;
    }

    private static bool IsBlocked(TerrainGrid grid, float[] classCost, int column, int row) =>
        float.IsPositiveInfinity(classCost[(int)grid[column, row]]);

    private static bool IsOpenGround(TerrainClass cls) => cls != TerrainClass.Wall && cls != TerrainClass.Water;

    /// <summary>The seed's own cell, or the first open cell in the nearest ring around it.</summary>
    private static bool TryStartCell(TerrainGrid grid, ProvinceSeed seed, int searchRadius, out int cell)
    {
        cell = -1;
        double fc = Math.Floor((seed.X - grid.MinX) / grid.CellSize);
        double fr = Math.Floor((seed.Y - grid.MinY) / grid.CellSize);
        if (!(fc >= 0 && fr >= 0 && fc < grid.Columns && fr < grid.Rows))
            return false; // a positive gate: a NaN seed position starts nothing
        int column = (int)fc, row = (int)fr;
        for (int radius = 0; radius <= searchRadius; radius++)
        {
            for (int r = row - radius; r <= row + radius; r++)
            {
                for (int c = column - radius; c <= column + radius; c++)
                {
                    bool onRing = Math.Abs(r - row) == radius || Math.Abs(c - column) == radius;
                    if (!onRing || (uint)c >= (uint)grid.Columns || (uint)r >= (uint)grid.Rows)
                        continue;
                    if (IsOpenGround(grid[c, r]))
                    {
                        cell = r * grid.Columns + c;
                        return true;
                    }
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Gives every four-connected patch of unclaimed non-water cells smaller than
    /// <paramref name="pocketCells"/> to the province bordering it most, lowest index on a tie.
    /// </summary>
    private static void MergePockets(TerrainGrid grid, int[] labels, int pocketCells)
    {
        int columns = grid.Columns, rows = grid.Rows;
        var seen = new bool[labels.Length];
        var patch = new List<int>();
        var queue = new Queue<int>();
        var neighbours = new Dictionary<int, int>();
        for (int start = 0; start < labels.Length; start++)
        {
            if (seen[start] || labels[start] != ProvinceMap.Unclaimed || grid.At(start) == TerrainClass.Water)
                continue;
            patch.Clear();
            neighbours.Clear();
            seen[start] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int cell = queue.Dequeue();
                patch.Add(cell);
                int column = cell % columns, row = cell / columns;
                for (int k = 0; k < 4; k++)
                {
                    var (dc, dr) = Steps[k];
                    int nc = column + dc, nr = row + dr;
                    if ((uint)nc >= (uint)columns || (uint)nr >= (uint)rows)
                        continue;
                    int next = nr * columns + nc;
                    int label = labels[next];
                    if (label != ProvinceMap.Unclaimed)
                    {
                        neighbours.TryGetValue(label, out int n);
                        neighbours[label] = n + 1;
                    }
                    else if (!seen[next] && grid.At(next) != TerrainClass.Water)
                    {
                        seen[next] = true;
                        queue.Enqueue(next);
                    }
                }
            }

            if (patch.Count >= pocketCells || neighbours.Count == 0)
                continue;
            int best = ProvinceMap.Unclaimed, bestCount = 0;
            foreach (var pair in neighbours)
            {
                if (pair.Value > bestCount || (pair.Value == bestCount && pair.Key < best))
                {
                    best = pair.Key;
                    bestCount = pair.Value;
                }
            }
            foreach (int cell in patch)
                labels[cell] = best;
        }
    }

    /// <summary>A binary min-heap of (cost, cell); .NET Framework 4.7.2 has no PriorityQueue.</summary>
    private sealed class CellHeap
    {
        private float[] _keys;
        private int[] _cells;
        private int _count;

        public CellHeap(int capacity)
        {
            _keys = new float[capacity];
            _cells = new int[capacity];
        }

        public void Push(float key, int cell)
        {
            if (_count == _keys.Length)
            {
                Array.Resize(ref _keys, _count * 2);
                Array.Resize(ref _cells, _count * 2);
            }
            int i = _count++;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (!(key < _keys[parent]))
                    break;
                _keys[i] = _keys[parent];
                _cells[i] = _cells[parent];
                i = parent;
            }
            _keys[i] = key;
            _cells[i] = cell;
        }

        public bool TryPop(out float key, out int cell)
        {
            if (_count == 0)
            {
                key = 0f;
                cell = -1;
                return false;
            }
            key = _keys[0];
            cell = _cells[0];
            float lastKey = _keys[--_count];
            int lastCell = _cells[_count];
            int i = 0;
            while (true)
            {
                int child = 2 * i + 1;
                if (child >= _count)
                    break;
                if (child + 1 < _count && _keys[child + 1] < _keys[child])
                    child++;
                if (!(_keys[child] < lastKey))
                    break;
                _keys[i] = _keys[child];
                _cells[i] = _cells[child];
                i = child;
            }
            _keys[i] = lastKey;
            _cells[i] = lastCell;
            return true;
        }
    }
}
