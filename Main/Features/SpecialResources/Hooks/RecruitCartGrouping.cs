using System.Collections.Generic;

namespace TAOM.Features.SpecialResources.Hooks;

/// <summary>
/// Folds the recruit screen's cart (one <c>RecruitVolunteerTroopVM</c> per recruited unit) into one
/// <see cref="RecruitCartEntry"/> per troop id, first-seen order, null ids skipped. Pure so the two
/// Patch51 entry points (the Done-button postfix and the ExecuteDone prefix) share one grouping and
/// a test can pin it without an engine VM.
/// </summary>
public static class RecruitCartGrouping
{
    public static IReadOnlyList<RecruitCartEntry> Group(IEnumerable<string> troopIds)
    {
        var counts = new Dictionary<string, int>();
        var order = new List<string>();
        foreach (var id in troopIds)
        {
            if (id == null) continue;
            if (!counts.TryGetValue(id, out var n)) order.Add(id);
            counts[id] = n + 1;
        }

        var entries = new List<RecruitCartEntry>(order.Count);
        foreach (var id in order)
            entries.Add(new RecruitCartEntry(id, counts[id]));
        return entries;
    }
}
