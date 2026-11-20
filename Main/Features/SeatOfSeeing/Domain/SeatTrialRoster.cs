using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.Elephant;

namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>A troop id and how many of it the trial fields.</summary>
internal sealed record SeatTrialGroup(string Id, int Count);

/// <summary>
/// The field trial's two armies (build-spec section 12): the Gondor commander's defenders and the Mordor attackers, each id checked by <see cref="Missing"/> before the mission opens.
/// </summary>
internal static class SeatTrialRoster
{
    internal const string PlayerCulture = "gondor";
    internal const string EnemyCulture = "mordor";
    internal const string Hero = "lord_1_9";

    /// <summary>Pinnath Gelin Light Horseman, a mounted troop of <c>fords_of_war/fords_troops.xml</c> (Horse and HorseHarness slots, Culture.gondor).</summary>
    internal const string MountedTroop = "fow_gondor_pg_cavalry";

    internal static readonly IReadOnlyList<SeatTrialGroup> Player = new[]
    {
        new SeatTrialGroup(SpearmenTroop, 40),
        new SeatTrialGroup("fow_gondor_ano_archer_militia", 40),
        new SeatTrialGroup(MountedTroop, 20),
    };

    internal static readonly IReadOnlyList<SeatTrialGroup> Enemy = new[]
    {
        new SeatTrialGroup("fow_mordor_orc_fighter", 120),
        new SeatTrialGroup("fow_mordor_orc_hunter", 30),
    };

    internal const string SpearmenTroop = "fow_gondor_militia_spearman";

    internal const string ThrowerTroop = "gondor_har_javelineer";

    internal static readonly IReadOnlyList<string> ThrowerSteps = new[] { "advance_throwers", "advance_throwers_vanilla" };

    /// <summary>
    /// The player's army for a step list: an explicit list that names a thrower step fields javelineers where the spearmen stand, so the infantry company is a pure thrower company,
    /// and a list that names a crew step adds the beasts of <see cref="CrewTrialRows.Beasts"/> after the army.
    /// </summary>
    internal static IReadOnlyList<SeatTrialGroup> PlayerFor(string? steps)
    {
        var chosen = (steps ?? "").Split(',').Select(SeatTrialSteps.NameOf);
        var army = chosen.Any(s => ThrowerSteps.Contains(s))
            ? Player.Select(g => g.Id == SpearmenTroop ? g with { Id = ThrowerTroop } : g).ToList()
            : Player;
        return CrewTrialRows.NamedIn(steps) ? army.Concat(CrewTrialRows.Beasts).ToList() : army;
    }

    /// <summary>The enemy's army for a step list: <see cref="Enemy"/>, or the melee army of <see cref="CrewTrialRows.Enemy"/> when the list names a crew step.</summary>
    internal static IReadOnlyList<SeatTrialGroup> EnemyFor(string? steps) => CrewTrialRows.NamedIn(steps) ? CrewTrialRows.Enemy : Enemy;

    /// <summary>The ids the game does not have, each once and tagged <c>culture:</c> or <c>character:</c>, empty when the mission can open.</summary>
    internal static IReadOnlyList<string> Missing(Func<string, bool> cultureExists, Func<string, bool> characterExists, string? steps = null)
    {
        var missing = new List<string>();
        foreach (var culture in new[] { PlayerCulture, EnemyCulture })
        {
            if (!cultureExists(culture))
                missing.Add("culture:" + culture);
        }

        foreach (var character in new[] { Hero }.Concat(PlayerFor(steps).Concat(EnemyFor(steps)).Select(g => g.Id)).Distinct())
        {
            if (!characterExists(character))
                missing.Add("character:" + character);
        }

        return missing;
    }
}
