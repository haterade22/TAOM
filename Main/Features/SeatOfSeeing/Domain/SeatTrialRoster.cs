using System;
using System.Collections.Generic;
using System.Linq;

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
        new SeatTrialGroup("fow_gondor_militia_spearman", 40),
        new SeatTrialGroup("fow_gondor_ano_archer_militia", 40),
        new SeatTrialGroup(MountedTroop, 20),
    };

    internal static readonly IReadOnlyList<SeatTrialGroup> Enemy = new[]
    {
        new SeatTrialGroup("fow_mordor_orc_fighter", 120),
        new SeatTrialGroup("fow_mordor_orc_hunter", 30),
    };

    /// <summary>Every troop id of both sides.</summary>
    internal static IEnumerable<string> TroopIds => Player.Concat(Enemy).Select(g => g.Id);

    /// <summary>The ids the game does not have, each once and tagged <c>culture:</c> or <c>character:</c>, empty when the mission can open.</summary>
    internal static IReadOnlyList<string> Missing(Func<string, bool> cultureExists, Func<string, bool> characterExists)
    {
        var missing = new List<string>();
        foreach (var culture in new[] { PlayerCulture, EnemyCulture })
        {
            if (!cultureExists(culture))
                missing.Add("culture:" + culture);
        }

        foreach (var character in new[] { Hero }.Concat(TroopIds).Distinct())
        {
            if (!characterExists(character))
                missing.Add("character:" + character);
        }

        return missing;
    }
}
