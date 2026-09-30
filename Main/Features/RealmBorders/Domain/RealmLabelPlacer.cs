using System;
using System.Collections.Generic;

namespace TAOM.Features.RealmBorders.Domain;

/// <summary>Where a realm's name is lettered: the deepest point of its largest piece of land.</summary>
public readonly record struct RealmLabel(string Realm, MapPoint Position, int Cells);

/// <summary>
/// Places one name per realm, the way an atlas letters a country: on the realm's largest connected
/// piece of land, at the cell farthest (chamfer distance) from any other realm, wild land, water or
/// the map edge. Pieces smaller than <c>minimumCells</c> get no name.
/// </summary>
public static class RealmLabelPlacer
{
    private const float Diagonal = 1.41421356f;

    public static IReadOnlyList<RealmLabel> Place(ProvinceMap map, IReadOnlyList<string?> realmOfProvince, int minimumCells)
    {
        if (map == null)
            throw new ArgumentNullException(nameof(map));
        if (realmOfProvince == null)
            throw new ArgumentNullException(nameof(realmOfProvince));

        int columns = map.Columns, rows = map.Rows, count = columns * rows;
        var ids = new Dictionary<string, int>(StringComparer.Ordinal);
        var names = new List<string>();
        var idOfProvince = new int[realmOfProvince.Count];
        for (int province = 0; province < idOfProvince.Length; province++)
        {
            string? name = realmOfProvince[province];
            if (name == null)
            {
                idOfProvince[province] = -1;
                continue;
            }
            if (!ids.TryGetValue(name, out int id))
            {
                ids[name] = id = names.Count;
                names.Add(name);
            }
            idOfProvince[province] = id;
        }

        var realm = new int[count];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                int province = map[c, r];
                realm[r * columns + c] = province >= 0 && province < idOfProvince.Length ? idOfProvince[province] : -1;
            }
        }

        float[] depth = Depths(realm, columns, rows);
        var (component, sizes) = Components(realm, columns, rows);

        var largest = new int[names.Count];
        var largestSize = new int[names.Count];
        for (int i = 0; i < largest.Length; i++)
            largest[i] = -1;
        for (int cell = 0; cell < count; cell++)
        {
            int id = realm[cell];
            if (id < 0)
                continue;
            int comp = component[cell];
            if (sizes[comp] > largestSize[id])
            {
                largestSize[id] = sizes[comp];
                largest[id] = comp;
            }
        }

        var best = new int[names.Count];
        for (int i = 0; i < best.Length; i++)
            best[i] = -1;
        for (int cell = 0; cell < count; cell++)
        {
            int id = realm[cell];
            if (id < 0 || component[cell] != largest[id])
                continue;
            if (best[id] < 0 || depth[cell] > depth[best[id]])
                best[id] = cell;
        }

        var labels = new List<RealmLabel>();
        for (int id = 0; id < names.Count; id++)
        {
            if (best[id] < 0 || largestSize[id] < minimumCells)
                continue;
            int c = best[id] % columns, r = best[id] / columns;
            var position = new MapPoint(map.MinX + (c + 0.5f) * map.CellSize, map.MinY + (r + 0.5f) * map.CellSize);
            labels.Add(new RealmLabel(names[id], position, largestSize[id]));
        }
        return labels;
    }

    /// <summary>Two-pass chamfer distance to the nearest cell outside the realm (or the map edge).</summary>
    private static float[] Depths(int[] realm, int columns, int rows)
    {
        var depth = new float[realm.Length];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                int i = r * columns + c;
                int id = realm[i];
                bool edge = id < 0 || c == 0 || r == 0 || c == columns - 1 || r == rows - 1
                    || realm[i - 1] != id || realm[i + 1] != id || realm[i - columns] != id || realm[i + columns] != id;
                depth[i] = edge ? 0f : float.MaxValue;
            }
        }

        for (int r = 1; r < rows; r++)
        {
            for (int c = 1; c < columns; c++)
            {
                int i = r * columns + c;
                if (depth[i] == 0f)
                    continue;
                float best = Math.Min(depth[i - 1] + 1f, depth[i - columns] + 1f);
                best = Math.Min(best, depth[i - columns - 1] + Diagonal);
                if (c + 1 < columns)
                    best = Math.Min(best, depth[i - columns + 1] + Diagonal);
                depth[i] = Math.Min(depth[i], best);
            }
        }
        for (int r = rows - 2; r >= 0; r--)
        {
            for (int c = columns - 2; c >= 0; c--)
            {
                int i = r * columns + c;
                if (depth[i] == 0f)
                    continue;
                float best = Math.Min(depth[i + 1] + 1f, depth[i + columns] + 1f);
                best = Math.Min(best, depth[i + columns + 1] + Diagonal);
                if (c > 0)
                    best = Math.Min(best, depth[i + columns - 1] + Diagonal);
                depth[i] = Math.Min(depth[i], best);
            }
        }
        return depth;
    }

    private static (int[] Component, List<int> Sizes) Components(int[] realm, int columns, int rows)
    {
        var component = new int[realm.Length];
        for (int i = 0; i < component.Length; i++)
            component[i] = -1;
        var sizes = new List<int>();
        var queue = new Queue<int>();
        for (int start = 0; start < realm.Length; start++)
        {
            if (realm[start] < 0 || component[start] >= 0)
                continue;
            int comp = sizes.Count, size = 0;
            component[start] = comp;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int cell = queue.Dequeue();
                size++;
                int c = cell % columns, r = cell / columns;
                Visit(c + 1, r);
                Visit(c - 1, r);
                Visit(c, r + 1);
                Visit(c, r - 1);

                void Visit(int nc, int nr)
                {
                    if ((uint)nc >= (uint)columns || (uint)nr >= (uint)rows)
                        return;
                    int n = nr * columns + nc;
                    if (component[n] >= 0 || realm[n] != realm[start])
                        return;
                    component[n] = comp;
                    queue.Enqueue(n);
                }
            }
            sizes.Add(size);
        }
        return (component, sizes);
    }
}
