using System;
using System.Collections.Generic;

namespace TAOM.Features.FieldCommission.Domain;

/// <summary>
/// Pure breadth-first walk down a troop's upgrade tree for the first descendant present in the
/// player's roster. Ported from the donor mod's <c>FindUpgradedDescendantInParty</c> with the
/// mandatory bug fix (h): a <see cref="HashSet{T}"/> of visited ids, so a cyclic upgrade graph
/// (data error or future-proofing) terminates instead of looping forever. Depth is still capped
/// at 3 tiers, matching the donor's cap.
/// </summary>
public class TroopUpgradeGraph
{
    private const int MaxDepth = 3;

    /// <param name="rootTroopId">The troop id whose descendants are searched (not itself checked).</param>
    /// <param name="getUpgradeTargets">Returns the upgrade-target ids for a troop id, or null/empty.</param>
    /// <param name="isInRoster">True when the given troop id currently has a positive count in the roster.</param>
    /// <returns>The first descendant id (breadth-first, branch order preserved) present in the
    /// roster, or null when none is found within the depth cap.</returns>
    public string FindDescendantInRoster(
        string rootTroopId,
        Func<string, IReadOnlyList<string>> getUpgradeTargets,
        Func<string, bool> isInRoster)
    {
        if (string.IsNullOrEmpty(rootTroopId) || getUpgradeTargets == null || isInRoster == null)
            return null;

        var visited = new HashSet<string>(StringComparer.Ordinal) { rootTroopId };
        var queue = new Queue<(string Id, int Depth)>();
        queue.Enqueue((rootTroopId, 0));

        while (queue.Count > 0)
        {
            var (id, depth) = queue.Dequeue();
            if (depth >= MaxDepth)
                continue;

            var targets = getUpgradeTargets(id);
            if (targets == null)
                continue;

            foreach (var target in targets)
            {
                if (string.IsNullOrEmpty(target) || !visited.Add(target))
                    continue; // null/empty entries skipped; already-visited nodes never re-enqueue (cycle guard)

                if (isInRoster(target))
                    return target;

                queue.Enqueue((target, depth + 1));
            }
        }

        return null;
    }
}
