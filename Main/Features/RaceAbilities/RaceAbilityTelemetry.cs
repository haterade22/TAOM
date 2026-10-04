using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using TAOM.Features.RaceAbilities.Domain;

namespace TAOM.Features.RaceAbilities;

// What the race abilities did in one battle, per ability, for the log and the taom.race_abilities
// command. Written from the main thread (activations, phases, deaths) and from the engine's threads
// (the stat and damage hooks), so every count is an Interlocked add on an array that is never replaced
// while a mission runs.
public enum RaceAbilityStat
{
    TreesAttached,
    Decisions,
    Waves,
    Activations,
    Rallied,
    Ended,
    Kills,
    Extensions,
    HealthHealed,
    Frightened,
    MoraleRestored,
    CrushesForced,
    CrushesHeld,
    ShrugOffs,
    MeleeHitsAmplified,
    RangedHitsAmplified,
    BonusDamage,
    HitsReduced,
    DamagePrevented,
    StatPasses,
}

public sealed class RaceAbilityTelemetry
{
    private static readonly int StatCount = Enum.GetValues(typeof(RaceAbilityStat)).Length;
    private static readonly int TriggerCount = Enum.GetValues(typeof(RaceAbilityTriggerKind)).Length;

    // How each stat reads in a report line, in report order.
    private static readonly (RaceAbilityStat stat, string label)[] Labels =
    {
        (RaceAbilityStat.TreesAttached, "trees"),
        (RaceAbilityStat.Decisions, "decisions"),
        (RaceAbilityStat.Waves, "waves"),
        (RaceAbilityStat.Activations, "soldiers"),
        (RaceAbilityStat.Rallied, "rallied"),
        (RaceAbilityStat.Ended, "ended"),
        (RaceAbilityStat.Kills, "kills while active"),
        (RaceAbilityStat.Extensions, "extensions"),
        (RaceAbilityStat.HealthHealed, "health healed"),
        (RaceAbilityStat.Frightened, "enemies frightened"),
        (RaceAbilityStat.MoraleRestored, "morale restored"),
        (RaceAbilityStat.CrushesForced, "crush forced"),
        (RaceAbilityStat.CrushesHeld, "crush held"),
        (RaceAbilityStat.ShrugOffs, "shrug-offs"),
        (RaceAbilityStat.MeleeHitsAmplified, "melee hits boosted"),
        (RaceAbilityStat.RangedHitsAmplified, "ranged hits boosted"),
        (RaceAbilityStat.BonusDamage, "bonus damage"),
        (RaceAbilityStat.HitsReduced, "hits softened"),
        (RaceAbilityStat.DamagePrevented, "damage prevented"),
        (RaceAbilityStat.StatPasses, "stat passes"),
    };

    private readonly ConcurrentDictionary<string, long[]> _stats = new ConcurrentDictionary<string, long[]>(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, long[]> _triggers = new ConcurrentDictionary<string, long[]>(StringComparer.Ordinal);

    public void Add(string? abilityId, RaceAbilityStat stat, long amount = 1)
    {
        if (string.IsNullOrEmpty(abilityId) || amount == 0)
            return;
        Interlocked.Add(ref _stats.GetOrAdd(abilityId!, _ => new long[StatCount])[(int)stat], amount);
    }

    public void AddTrigger(string? abilityId, RaceAbilityTriggerKind? kind)
    {
        if (string.IsNullOrEmpty(abilityId) || !kind.HasValue)
            return;
        Interlocked.Increment(ref _triggers.GetOrAdd(abilityId!, _ => new long[TriggerCount])[(int)kind.Value]);
    }

    public long Get(string abilityId, RaceAbilityStat stat) =>
        _stats.TryGetValue(abilityId, out var counts) ? Interlocked.Read(ref counts[(int)stat]) : 0;

    public long GetTrigger(string abilityId, RaceAbilityTriggerKind kind) =>
        _triggers.TryGetValue(abilityId, out var counts) ? Interlocked.Read(ref counts[(int)kind]) : 0;

    public long Total(RaceAbilityStat stat)
    {
        long total = 0;
        foreach (var counts in _stats.Values)
            total += Interlocked.Read(ref counts[(int)stat]);
        return total;
    }

    // One line per ability with anything counted, in name order; zero stats are left out.
    public string Report()
    {
        var abilities = _stats.Keys.Union(_triggers.Keys).OrderBy(id => id, StringComparer.Ordinal).ToList();
        if (abilities.Count == 0)
            return "no race ability activity";

        var report = new StringBuilder();
        foreach (var ability in abilities)
        {
            var parts = new List<string>();
            foreach (var (stat, label) in Labels)
            {
                var value = Get(ability, stat);
                if (value != 0)
                    parts.Add($"{label} {value}");
            }
            var fired = new List<string>();
            foreach (RaceAbilityTriggerKind kind in Enum.GetValues(typeof(RaceAbilityTriggerKind)))
            {
                var value = GetTrigger(ability, kind);
                if (value != 0)
                    fired.Add($"{kind} {value}");
            }
            if (fired.Count > 0)
                parts.Add("fired by " + string.Join(", ", fired));
            if (report.Length > 0)
                report.Append('\n');
            report.Append(ability).Append(": ").Append(parts.Count == 0 ? "nothing yet" : string.Join(", ", parts));
        }
        return report.ToString();
    }

    public void Reset()
    {
        _stats.Clear();
        _triggers.Clear();
    }
}
