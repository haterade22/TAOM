using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// Which navmesh face ids each lane's units may not walk on: every other lane's id, in a fixed order. The engine registers one exclusion
/// set per exact ordered list, including each list on the way to the full one, and an index of 8 or more silently writes no bit, so
/// <see cref="RegisteredSets"/> must stay within <see cref="MaxSets"/> (bits 0 and 7 are reserved; docs/features/creature-siege-role.md).
/// </summary>
internal sealed class LaneFaceBars
{
    public const int MaxSets = 6;

    private readonly IReadOnlyList<int> _laneFaces;

    public LaneFaceBars(IReadOnlyList<int> laneFaces)
    {
        _laneFaces = laneFaces;
    }

    public int LaneCount => _laneFaces.Count;

    public IReadOnlyList<int> BarsFor(int lane) =>
        lane < 0 || lane >= _laneFaces.Count ? new int[0] : _laneFaces.Where((_, i) => i != lane).ToList();

    public int RegisteredSets
    {
        get
        {
            var sets = new HashSet<string>();
            for (var lane = 0; lane < _laneFaces.Count; lane++)
            {
                var bars = BarsFor(lane);
                for (var length = 1; length <= bars.Count; length++)
                    sets.Add(string.Join(",", bars.Take(length)));
            }

            return sets.Count;
        }
    }
}
