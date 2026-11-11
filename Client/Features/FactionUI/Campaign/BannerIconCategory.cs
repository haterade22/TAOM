using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;

namespace TAOM.Features.FactionUI.Campaign;

/// <summary>
/// One page of the layered banner editor's icon grid: every banner icon group that shows the same name. TAOM keeps one
/// group per people (banner_icons.xml, the Realms in Exile CoA designer's categories; each icon names its own sheet),
/// and groups another module adds under the same name join that page instead of making a second one. Pages keep the
/// order their first group loaded in; icons keep their groups' order.
/// </summary>
public sealed class BannerIconCategory
{
    private readonly HashSet<int> _members;

    private BannerIconCategory(string name, List<int> ids)
    {
        Name = name;
        Ids = ids;
        _members = new HashSet<int>(ids);
    }

    public string Name { get; }

    public IReadOnlyList<int> Ids { get; }

    public bool Contains(int meshId) => _members.Contains(meshId);

    /// <summary>The sigil pages: groups with at least one icon a player can pick.</summary>
    public static List<BannerIconCategory> Sigils(IEnumerable<BannerIconGroup> groups) =>
        Merge(groups.Where(g => !g.IsPattern).Select(g => (g.Name?.ToString() ?? "", (IEnumerable<int>)g.AvailableIcons.Keys)));

    /// <summary>The background pages: pattern groups with at least one background.</summary>
    public static List<BannerIconCategory> Patterns(IEnumerable<BannerIconGroup> groups) =>
        Merge(groups.Where(g => g.IsPattern).Select(g => (g.Name?.ToString() ?? "", (IEnumerable<int>)g.AllBackgrounds.Keys)));

    /// <summary>Merges the groups that share a name, in first-seen order, dropping empty pages and repeated ids.</summary>
    public static List<BannerIconCategory> Merge(IEnumerable<(string Name, IEnumerable<int> Ids)> groups)
    {
        var order = new List<string>();
        var byName = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        foreach (var (name, ids) in groups)
        {
            if (!byName.TryGetValue(name, out var list))
            {
                byName[name] = list = new List<int>();
                order.Add(name);
            }
            foreach (int id in ids)
            {
                if (!list.Contains(id))
                    list.Add(id);
            }
        }
        return order.Where(n => byName[n].Count > 0).Select(n => new BannerIconCategory(n, byName[n])).ToList();
    }
}
