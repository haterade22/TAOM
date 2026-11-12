using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TAOM.Features.RaceAbilities.Domain;

namespace TAOM.Features.HeroAuras.Domain;

/// <summary>One hero's aura, resolved: its reach and the race ids it reaches (null: every race).</summary>
public sealed class HeroAura
{
    internal HeroAura(string id, float radius, HashSet<int>? raceIds, RaceAbilityEffects effects)
    {
        Id = id;
        Radius = radius;
        RaceIds = raceIds;
        Effects = effects;
    }

    public string Id { get; }

    public float Radius { get; }

    public RaceAbilityEffects Effects { get; }

    internal HashSet<int>? RaceIds { get; }

    // Scaled effects cached for the last strength, so each pulse does not allocate them again.
    internal float CachedStrength { get; set; } = float.NaN;

    internal RaceAbilityEffects? CachedEffects { get; set; }

    public bool Reaches(int? raceId) => RaceIds == null || (raceId.HasValue && RaceIds.Contains(raceId.Value));

    // The strength part of a soldier's aura key: built once per pulse.
    public static string StrengthSuffix(float strength) => "@" + strength.ToString("R", CultureInfo.InvariantCulture);

    // A soldier's aura key: the same auras at the same strength give the same key in any order, so the ledger only
    // moves his end time and his stats are not refreshed; another set or another strength gives another key.
    public static string KeyFor(IReadOnlyList<HeroAura> auras, string strengthSuffix) =>
        auras.Count == 1
            ? auras[0].Id + strengthSuffix
            : string.Join("+", auras.Select(a => a.Id).OrderBy(id => id, StringComparer.Ordinal)) + strengthSuffix;
}
