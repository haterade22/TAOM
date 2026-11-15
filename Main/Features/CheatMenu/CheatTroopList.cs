using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.CheatMenu;

/// <summary>One character as the troop picker sees it, read by the world adapter.</summary>
/// <param name="IsFieldSoldier">Occupation Soldier or Mercenary (a villager, caravan guard, bandit or townsman is not).</param>
public sealed record CheatTroopFacts(
    string Id,
    string Name,
    int Tier,
    string? CultureId,
    bool IsHero,
    bool IsTemplate,
    bool IsObsolete,
    bool IsFieldSoldier,
    IReadOnlyList<string> UpgradeTargetIds);

/// <summary>Where a culture's troop list starts (docs/features/cheat-menu.md, "Troops").</summary>
/// <param name="CultureId">The faction's culture: the player's kingdom's, or the clan's without a kingdom.</param>
/// <param name="OwnRoots">The culture's own lists, whatever culture each troop carries: its basic, elite and militia
/// troops, its vassal reward template, its tavern mercenaries, its TAOM recruitment pool and its Elite Emissary offers.</param>
/// <param name="FieldedAnywhere">Every troop a TAOM recruitment pool offers anywhere and every troop of a clan's party
/// template. Only the ones that carry <paramref name="CultureId"/> start the list.</param>
public sealed record CheatTroopSources(string CultureId, IReadOnlyCollection<string> OwnRoots, IReadOnlyCollection<string> FieldedAnywhere);

/// <summary>
/// The pure rule behind "Any troop of your faction...": the roots of <see cref="CheatTroopSources"/> and every troop they
/// upgrade into, keeping only regular field soldiers (no hero, lord, wanderer, template, obsolete character, villager,
/// caravan guard or bandit), sorted by tier, then name. A troop that no list holds and no listed troop upgrades into
/// stays out, which keeps out the vanilla troops of the cultures TAOM reuses (Rohan, Dale, Dunland, Rhun) and the
/// training dummies and town guards that carry the culture's tag.
/// </summary>
public static class CheatTroopList
{
    public static IReadOnlyList<CheatTroopFacts> Select(CheatTroopSources sources, IReadOnlyDictionary<string, CheatTroopFacts> troops)
    {
        if (sources == null || troops == null || string.IsNullOrEmpty(sources.CultureId))
            return Array.Empty<CheatTroopFacts>();

        var roots = new List<string>(sources.OwnRoots ?? Array.Empty<string>());
        foreach (var id in sources.FieldedAnywhere ?? Array.Empty<string>())
            if (id != null && troops.TryGetValue(id, out var t) && t.CultureId == sources.CultureId)
                roots.Add(id);

        var reached = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>(roots.Where(id => !string.IsNullOrEmpty(id)));
        while (stack.Count > 0)
        {
            var id = stack.Pop();
            if (!troops.TryGetValue(id, out var troop) || !reached.Add(id))
                continue;
            foreach (var next in troop.UpgradeTargetIds ?? Array.Empty<string>())
                if (!string.IsNullOrEmpty(next) && !reached.Contains(next))
                    stack.Push(next);
        }

        return reached.Select(id => troops[id])
            .Where(IsPickable)
            .OrderBy(t => t.Tier)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.Id, StringComparer.Ordinal)
            .ToList();
    }

    public static bool IsPickable(CheatTroopFacts t) =>
        !t.IsHero && !t.IsTemplate && !t.IsObsolete && t.IsFieldSoldier;
}
