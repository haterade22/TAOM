using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.StoryMissions.Domain;

/// <summary>
/// Who a story mission hero is. A hero of a story mission (<c>taom_hero_gimli</c>) stands for a person who can also be a campaign character the player
/// controls (the wanderer <c>named_companion_gimli</c>, a lord taken over through the Player Switcher). One row per person lists every character id that is him;
/// a later hero is one more row. A mission hero is left out of a plan when the player is any of the ids of his person, so he is never fielded twice.
/// </summary>
internal static class LoreHeroPersons
{
    private static readonly string[][] Persons =
    {
        new[] { "taom_hero_aragorn", "named_companion_aragorn" },
        new[] { "taom_hero_legolas", "named_companion_legolas", "lord_M1_11" },
        new[] { "taom_hero_gimli", "named_companion_gimli" },
        new[] { "taom_hero_boromir", "lord_1_75" },
    };

    /// <summary>Every person with the character ids that are him, for the shipped-data test.</summary>
    internal static IReadOnlyList<string[]> All => Persons;

    /// <summary>True when the hero's character is the same person as one of the ids (the player's hero and character ids). A hero with no row is nobody's twin.</summary>
    public static bool IsPlayed(string heroTroopId, IReadOnlyCollection<string> playerIds)
    {
        if (playerIds.Count == 0)
            return false;

        foreach (var person in Persons)
        {
            if (person.Contains(heroTroopId, StringComparer.Ordinal))
                return person.Any(id => playerIds.Contains(id, StringComparer.Ordinal));
        }

        return false;
    }

    /// <summary>
    /// The plan without the shop offers and free heroes the player is. The slot stays empty; no other hero takes it. The must-live condition is dropped with
    /// the hero. The War Chronicle never offers Parth Galen to a Boromir player (ChronicleService.cs:631,638), so only the dev console reaches that case.
    /// </summary>
    public static RunPlan WithoutPlayed(RunPlan plan, IReadOnlyCollection<string> playerIds)
    {
        if (playerIds.Count == 0)
            return plan;

        var free = plan.FreeHeroes.Where(h => !IsPlayed(h.TroopId, playerIds)).ToList();
        var offers = plan.Shop.Heroes.Where(h => !IsPlayed(h.TroopId, playerIds)).ToList();
        if (free.Count == plan.FreeHeroes.Count && offers.Count == plan.Shop.Heroes.Count)
            return plan;

        var mustLive = plan.MustLive != null && free.All(h => h.Id != plan.MustLive) ? null : plan.MustLive;
        return plan with { FreeHeroes = free, Shop = plan.Shop with { Heroes = offers }, MustLive = mustLive };
    }
}
