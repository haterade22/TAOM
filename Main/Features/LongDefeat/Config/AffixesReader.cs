using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>What affixes.json holds: the schedule, each power's numbers, and the power names of each attacker culture.</summary>
internal sealed class AffixesConfig
{
    public static AffixesConfig None { get; } = new(
        Array.Empty<AffixStage>(),
        new Dictionary<string, IReadOnlyDictionary<string, float>>(),
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase));

    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _names;

    public AffixesConfig(IReadOnlyList<AffixStage> schedule, IReadOnlyDictionary<string, IReadOnlyDictionary<string, float>> powers,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> names)
    {
        Schedule = schedule;
        Powers = powers;
        _names = names;
    }

    public IReadOnlyList<AffixStage> Schedule { get; }
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, float>> Powers { get; }

    /// <summary>Whether the file has a name table for this attacker culture.</summary>
    public bool HasNames(string culture) => _names.ContainsKey(culture);

    /// <summary>The plan for a fortress whose attackers are of <paramref name="culture"/>: the same schedule and numbers, with that culture's names (none when it has no table).</summary>
    public AffixPlan For(string culture) =>
        new(Schedule, Powers, _names.TryGetValue(culture, out var table) ? table : new Dictionary<string, string>());
}

/// <summary>
/// affixes.json (phase2-plan.md section 3): a schedule of <c>fromWave</c> and <c>count</c> rows, each power's numbers, and a name table per attacker
/// culture. A bad schedule keeps the shipped one, a bad number keeps its default (a percentage skips to 0, so the effect is off), a power the
/// file leaves out reads as its defaults, and a culture table that lacks powers costs one warning naming them, because those powers then show as their ids.
/// </summary>
internal static class AffixesReader
{
    private const int MaxCount = 8;
    private const float PercentMin = -90f;
    private const float PercentMax = 300f;

    private static readonly IReadOnlyList<AffixStage> DefaultSchedule = new[] { new AffixStage(5, 1), new AffixStage(10, 2), new AffixStage(15, 3) };

    // (min, max, default) per key. Every percentage defaults to 0 (off); the terror and blades numbers default to the shipped ones.
    private static readonly IReadOnlyDictionary<string, (float Min, float Max, float Default)> Ranges = new Dictionary<string, (float, float, float)>
    {
        ["radius"] = (1f, 100f, 8f),
        ["pulseSeconds"] = (0.5f, 60f, 2f),
        ["amount"] = (0f, 100f, 3f),
        ["floor"] = (0f, 100f, 35f),
        ["poisonPerSecond"] = (0f, 100f, 2f),
        ["poisonSeconds"] = (0.5f, 60f, 6f),
    };

    public static AffixesConfig Read(string? text, LongDefeatConfigScope s)
    {
        var root = s.OpenRoot(text, "was not found or could not be read; no wave will carry an affix");
        if (root == null)
            return AffixesConfig.None;

        var schedule = ReadSchedule(root, s);
        var powers = ReadPowers(s.Group(root, "powers", ""), s);
        var names = ReadNames(s.Group(root, "names", ""), s);
        s.Loaded($"{powers.Count} power(s), {names.Count} name table(s), {schedule.Count} stage(s)");
        return new AffixesConfig(schedule, powers, names);
    }

    private static IReadOnlyList<AffixStage> ReadSchedule(JObject root, LongDefeatConfigScope s)
    {
        if (!root.TryGetValue("schedule", out var token))
            return DefaultSchedule;

        if (!(token is JArray rows))
        {
            s.Warn("'schedule' is not an array; using the shipped schedule");
            return DefaultSchedule;
        }

        var stages = new List<AffixStage>();
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i] as JObject;
            if (row == null
                || !LongDefeatConfigScope.TryInt(row["fromWave"], 1, 1000, out var from, out var why)
                || !LongDefeatConfigScope.TryInt(row["count"], 0, MaxCount, out var count, out why))
            {
                s.Warn($"'schedule[{i}]' needs a whole fromWave of 1 to 1000 and a whole count of 0 to {MaxCount}; using the shipped schedule");
                return DefaultSchedule;
            }

            if (stages.Count > 0 && from <= stages[stages.Count - 1].FromWave)
            {
                s.Warn($"'schedule[{i}].fromWave' {from} does not rise above the row before it; using the shipped schedule");
                return DefaultSchedule;
            }

            stages.Add(new AffixStage(from, count));
        }

        return stages;
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, float>> ReadPowers(JObject? block, LongDefeatConfigScope s)
    {
        var powers = new Dictionary<string, IReadOnlyDictionary<string, float>>(StringComparer.Ordinal);
        foreach (var id in AffixPlan.Ids)
        {
            var group = s.Group(block, id, "powers");
            var numbers = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (var key in AffixPlan.Keys[id])
            {
                var range = Ranges.TryGetValue(key, out var found) ? found : (Min: PercentMin, Max: PercentMax, Default: 0f);
                numbers[key] = s.Float(group, "powers." + id, key, range.Default, range.Min, range.Max);
            }

            powers[id] = numbers;
        }

        if (block != null)
        {
            foreach (var extra in block.Properties().Where(p => !AffixPlan.Ids.Contains(p.Name)))
                s.Warn($"'powers.{extra.Name}' is not one of the eight powers ({string.Join(", ", AffixPlan.Ids)}); ignored");
        }

        return powers;
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ReadNames(JObject? block, LongDefeatConfigScope s)
    {
        var cultures = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (block == null)
            return cultures;

        foreach (var culture in block.Properties())
        {
            var table = new Dictionary<string, string>(StringComparer.Ordinal);
            var group = culture.Value as JObject;
            if (group == null)
            {
                s.Warn($"'names.{culture.Name}' is not an object; its powers show as their ids");
                continue;
            }

            foreach (var id in AffixPlan.Ids)
            {
                var name = LongDefeatConfigScope.Text(group[id]);
                if (name != null)
                    table[id] = name;
            }

            var missing = AffixPlan.Ids.Where(id => !table.ContainsKey(id)).ToList();
            if (missing.Count > 0)
                s.Warn($"'names.{culture.Name}' has no name for {string.Join(", ", missing)}; those powers show as their ids");
            cultures[culture.Name] = table;
        }

        return cultures;
    }
}
