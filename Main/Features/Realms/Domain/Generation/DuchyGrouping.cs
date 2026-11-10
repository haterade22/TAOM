using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.Realms.Domain.World;

namespace TAOM.Features.Realms.Domain.Generation;

/// <summary>
/// Groups a kingdom's towns into duchies by distance. Deterministic: the westmost remaining town seeds
/// a group and takes its nearest neighbours until the group is full. A lone town left over joins the
/// group whose seed is nearest. One town on its own never forms a duchy.
/// </summary>
public static class DuchyGrouping
{
    public static IReadOnlyList<IReadOnlyList<FiefInfo>> Group(IEnumerable<FiefInfo> towns, int groupSize)
    {
        var remaining = towns.OrderBy(t => t.X).ThenBy(t => t.Id, StringComparer.Ordinal).ToList();
        var groups = new List<List<FiefInfo>>();
        if (groupSize < 2 || remaining.Count < 2)
            return groups.Cast<IReadOnlyList<FiefInfo>>().ToList();

        while (remaining.Count > 0)
        {
            var seed = remaining[0];
            var group = new List<FiefInfo> { seed };
            group.AddRange(remaining.Skip(1)
                .OrderBy(t => Distance(seed, t)).ThenBy(t => t.Id, StringComparer.Ordinal)
                .Take(groupSize - 1));
            remaining.RemoveAll(group.Contains);
            groups.Add(group);
        }

        foreach (var lone in groups.Where(g => g.Count == 1).ToList())
        {
            var target = groups.Where(g => g.Count > 1)
                .OrderBy(g => Distance(g[0], lone[0])).ThenBy(g => g[0].Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (target == null)
                continue;
            target.Add(lone[0]);
            groups.Remove(lone);
        }

        return groups.Where(g => g.Count > 1).Cast<IReadOnlyList<FiefInfo>>().ToList();
    }

    public static double Distance(FiefInfo a, FiefInfo b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
