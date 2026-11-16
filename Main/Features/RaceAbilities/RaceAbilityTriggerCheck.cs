using System;
using System.Collections.Generic;
using TAOM.Core.Validation;
using TAOM.Features.RaceAbilities.Domain;

namespace TAOM.Features.RaceAbilities;

/// <summary>
/// The trigger validation shared by race_abilities.json and hero_abilities.json. A trigger with a bad kind or a
/// bad number is skipped with one warning (the text carries no provider prefix: the caller adds it).
/// </summary>
internal static class RaceAbilityTriggerCheck
{
    // Native IMBMission.GetNearbyAgentsAux searches its proximity grid only up to 15 m
    // (ProximityMapMaxSearchRadius, read on v1.5.5 and read again on v1.5.7 by the review of #1359) and walks every agent in the mission beyond it. Five
    // shipped profiles sense enemies past 15 m: Stand Fast 20 m (every dwarf), Forth Eorlingas 30 m
    // (mounted), and Swiftness, Serpent's Venom and Bard's Aim at 30 to 40 m (holding any ranged
    // weapon, javelins included). Each such soldier pays one full walk a pass, about once a second,
    // until his ability fires. Accepted, unmeasured. The four radii and KinWithin stay on the grid.
    internal const float MaxRange = 40f;
    internal const float MaxKinRadius = 15f;
    internal const float MaxEventSeconds = 30f;

    internal static List<RaceAbilityTrigger> ValidateAll(List<RaceAbilityTrigger>? parsed, string field, Action<string> warn)
    {
        var triggers = new List<RaceAbilityTrigger>();
        if (parsed == null)
            return triggers;
        for (var i = 0; i < parsed.Count; i++)
        {
            var trigger = Validate(parsed[i], $"{field}[{i}]", warn);
            if (trigger != null)
                triggers.Add(trigger);
        }
        return triggers;
    }

    internal static RaceAbilityTrigger? Validate(RaceAbilityTrigger? parsed, string field, Action<string> warn)
    {
        if (parsed == null)
        {
            warn($"{field} is null, skipped");
            return null;
        }
        if (!TryParseKind(parsed.Kind, out var kind))
        {
            warn($"{field}.kind='{parsed.Kind}' is not a known trigger kind, skipped");
            return null;
        }

        var trigger = new RaceAbilityTrigger
        {
            Kind = kind.ToString(),
            ParsedKind = kind,
            Range = parsed.Range,
            Seconds = parsed.Seconds,
            Fraction = parsed.Fraction,
            Count = parsed.Count,
        };
        var valid = true;
        bool Skip(string message)
        {
            warn(message + ", trigger skipped");
            return false;
        }

        // KinWithin runs the same ally query as kinBonus.radius, on every pass: it stays on the grid.
        var maxRange = kind == RaceAbilityTriggerKind.KinWithin ? MaxKinRadius : MaxRange;
        if (ReadsRange(kind) && !(FiniteFloatValidator.IsFiniteAtMost(parsed.Range, maxRange) && parsed.Range > 0f))
            valid = Skip($"{field}.range={parsed.Range} must be a finite value in (0,{maxRange}]");
        if (ReadsSeconds(kind) && !(FiniteFloatValidator.IsFiniteAtMost(parsed.Seconds, MaxEventSeconds) && parsed.Seconds > 0f))
            valid = Skip($"{field}.seconds={parsed.Seconds} must be a finite value in (0,{MaxEventSeconds}]");
        if (ReadsFraction(kind) && !(FiniteFloatValidator.IsFiniteAtMost(parsed.Fraction, 1f) && parsed.Fraction > 0f))
            valid = Skip($"{field}.fraction={parsed.Fraction} must be a finite value in (0,1]");
        if (ReadsCount(kind) && (parsed.Count < 1 || parsed.Count > 50))
            valid = Skip($"{field}.count={parsed.Count} must be in [1,50]");
        return valid ? trigger : null;
    }

    // One kind's exact name, any case. Enum.TryParse would also take a number, or a comma list whose values
    // it ORs together into some other kind ("EnemyWithin,CavalryClosing" reads as RangedTargetWithin).
    internal static bool TryParseKind(string? value, out RaceAbilityTriggerKind kind)
    {
        var name = value?.Trim();
        foreach (RaceAbilityTriggerKind candidate in Enum.GetValues(typeof(RaceAbilityTriggerKind)))
        {
            if (string.Equals(candidate.ToString(), name, StringComparison.OrdinalIgnoreCase))
            {
                kind = candidate;
                return true;
            }
        }
        kind = default;
        return false;
    }

    private static bool ReadsRange(RaceAbilityTriggerKind kind) => kind switch
    {
        RaceAbilityTriggerKind.EnemyWithin => true,
        RaceAbilityTriggerKind.EnemiesWithin => true,
        RaceAbilityTriggerKind.KinFell => true,
        RaceAbilityTriggerKind.WoundedEnemyWithin => true,
        RaceAbilityTriggerKind.CavalryClosing => true,
        RaceAbilityTriggerKind.RangedTargetWithin => true,
        RaceAbilityTriggerKind.KinWithin => true,
        RaceAbilityTriggerKind.NoEnemyWithin => true,
        _ => false,
    };

    private static bool ReadsSeconds(RaceAbilityTriggerKind kind) =>
        kind == RaceAbilityTriggerKind.KinFell || kind == RaceAbilityTriggerKind.LandedKill;

    private static bool ReadsFraction(RaceAbilityTriggerKind kind) =>
        kind == RaceAbilityTriggerKind.HealthBelow || kind == RaceAbilityTriggerKind.WoundedEnemyWithin
        || kind == RaceAbilityTriggerKind.MoraleBelow;

    private static bool ReadsCount(RaceAbilityTriggerKind kind) =>
        kind == RaceAbilityTriggerKind.EnemiesWithin || kind == RaceAbilityTriggerKind.KinWithin;
}
