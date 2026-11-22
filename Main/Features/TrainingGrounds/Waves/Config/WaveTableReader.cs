using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TAOM.Features.TrainingGrounds.Waves.Domain;

namespace TAOM.Features.TrainingGrounds.Waves.Config;

/// <summary>
/// Reads training_waves.json into a <see cref="WaveTable"/> and never throws (config-providers.md): a row that breaks a rule is
/// skipped, a row that can be adjusted is adjusted, a rule that breaks its range keeps its default, and each of these is one
/// warning. When anything was reverted, skipped or adjusted, one summary warning follows. No valid row gives the default waves.
/// Every number is a whole number read as an integer token, so a fraction, a string or a value past int range is invalid.
/// </summary>
internal static class WaveTableReader
{
    internal const int MaxRows = 20;

    private const int MinCount = 1;
    private const int MaxCount = 60;
    private const int MinStage = 1;
    private const int MaxStage = 3;
    private const int MinCap = 10;
    private const int MaxCap = 1_000_000;
    private const int MinOdds = 11;
    private const int MaxOdds = 40;

    private static readonly string[] TopKeys = { "rules", "waves" };

    private static readonly string[] RuleKeys = { "minStake", "hardCeiling", "oneRunPerTownPerDay", "returnSeconds", "refillSeconds" };

    public static WaveTable Read(string? json, Action<string> warn)
    {
        if (string.IsNullOrWhiteSpace(json))
            return WaveTable.Default;

        var run = new Run(warn);
        try
        {
            var root = JToken.Parse(json);
            if (!(root is JObject file))
            {
                run.Warn("training_waves.json is not a JSON object; using the default table");
                run.Summary();
                return WaveTable.Default;
            }

            foreach (var property in file.Properties().Where(p => !TopKeys.Contains(p.Name)))
                run.Note($"'{property.Name}' is not a known top-level key (the file reads 'rules' and 'waves'); ignored");

            var rules = ReadRules(file["rules"], run);
            var waves = ReadWaves(file["waves"], run);
            run.Summary();
            return new WaveTable(waves, rules);
        }
        catch (Exception ex)
        {
            run.Warn($"training_waves.json could not be read ({ex.GetType().Name}: {ex.Message}); using the default table");
            return WaveTable.Default;
        }
    }

    private static WaveRules ReadRules(JToken? token, Run run)
    {
        var d = WaveRules.Default;
        if (token == null)
            return d;
        if (!(token is JObject o))
        {
            run.Warn("'rules' is not an object; the default rules apply");
            return d;
        }

        foreach (var property in o.Properties().Where(p => !RuleKeys.Contains(p.Name)))
            run.Note($"'rules.{property.Name}' is not a known rule; ignored");

        var minStake = RuleInt(o, "minStake", d.MinStake, 1, 1000, run);
        var hardCeiling = RuleInt(o, "hardCeiling", d.HardCeiling, 100, 1_000_000, run);
        if (hardCeiling < minStake)
        {
            run.Warn($"'rules.hardCeiling' {hardCeiling} is below 'rules.minStake' {minStake}; both revert to their defaults");
            minStake = d.MinStake;
            hardCeiling = d.HardCeiling;
        }

        var oneRun = d.OneRunPerTownPerDay;
        if (o.TryGetValue("oneRunPerTownPerDay", out var flag))
        {
            if (flag.Type == JTokenType.Boolean)
                oneRun = (bool)flag;
            else
                run.Warn("'rules.oneRunPerTownPerDay' must be true or false; the default applies");
        }

        return new WaveRules(minStake, hardCeiling, oneRun,
            RuleInt(o, "returnSeconds", d.ReturnSeconds, 1, 30, run),
            RuleInt(o, "refillSeconds", d.RefillSeconds, 1, 30, run));
    }

    private static int RuleInt(JObject o, string key, int fallback, int min, int max, Run run)
    {
        if (!o.TryGetValue(key, out var token))
            return fallback;
        if (TryInt(token, min, max, out var value))
            return value;
        run.Warn($"'rules.{key}' must be a whole number from {min} to {max}; the default {fallback} applies");
        return fallback;
    }

    private static IReadOnlyList<WaveRow> ReadWaves(JToken? token, Run run)
    {
        if (token == null)
        {
            run.Warn("training_waves.json has no 'waves' array; the default waves apply");
            return WaveTable.Default.Waves;
        }

        if (!(token is JArray array))
        {
            run.Warn("'waves' is not an array; the default waves apply");
            return WaveTable.Default.Waves;
        }

        var rows = new List<WaveRow>();
        for (var i = 0; i < array.Count; i++)
        {
            if (rows.Count >= MaxRows)
            {
                run.Warn($"'waves' holds more than {MaxRows} rows; the rest are ignored");
                break;
            }

            var row = ReadRow(array[i], $"waves[{i}]", rows.Count == 0 ? 0 : rows[rows.Count - 1].Cap, run);
            if (row != null)
                rows.Add(row);
        }

        if (rows.Count > 0)
            return rows;
        run.Warn("'waves' has no valid row; the default waves apply");
        return WaveTable.Default.Waves;
    }

    private static WaveRow? ReadRow(JToken token, string label, int previousCap, Run run)
    {
        if (!(token is JObject o))
            return Skip(run, label, "is not an object");

        if (!TryInt(o["count"], MinCount, MaxCount, out var count))
            return Skip(run, label, $"'count' must be a whole number from {MinCount} to {MaxCount}");
        if (!TryInt(o["atOnce"], 1, int.MaxValue, out var atOnce))
            return Skip(run, label, "'atOnce' must be a whole number of at least 1");
        if (!TryTiers(o["tiers"], out var weights, out var why))
            return Skip(run, label, why);
        if (!TryInt(o["weaponStage"], MinStage, MaxStage, out var stage))
            return Skip(run, label, $"'weaponStage' must be a whole number from {MinStage} to {MaxStage}");
        if (!TryInt(o["cap"], MinCap, MaxCap, out var cap))
            return Skip(run, label, $"'cap' must be a whole number from {MinCap} to {MaxCap}");
        if (!TryInt(o["oddsTenths"], MinOdds, MaxOdds, out var odds))
            return Skip(run, label, $"'oddsTenths' must be a whole number from {MinOdds} to {MaxOdds}");

        if (atOnce > count)
        {
            run.Warn($"'{label}.atOnce' {atOnce} is above 'count' {count}; lowered to {count}");
            atOnce = count;
        }

        if (cap < previousCap)
        {
            run.Warn($"'{label}.cap' {cap} is below the previous wave's {previousCap}; raised to it");
            cap = previousCap;
        }

        return new WaveRow(count, atOnce, weights, stage, cap, odds);
    }

    private static WaveRow? Skip(Run run, string label, string reason)
    {
        run.Warn($"'{label}' {reason}; row skipped");
        return null;
    }

    /// <summary>The tier weights: keys 3, 4 and 5 only, each a whole number of 0 or more, at least one above 0. A tier not named weighs 0.</summary>
    private static bool TryTiers(JToken? token, out IReadOnlyDictionary<int, int> weights, out string why)
    {
        weights = new Dictionary<int, int>();
        if (!(token is JObject o))
        {
            why = "'tiers' is missing or not an object";
            return false;
        }

        var read = new Dictionary<int, int> { [3] = 0, [4] = 0, [5] = 0 };
        foreach (var property in o.Properties())
        {
            if (!int.TryParse(property.Name, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var tier)
                || tier < 3 || tier > 5)
            {
                why = $"'tiers.{property.Name}' is not tier 3, 4 or 5";
                return false;
            }

            if (!TryInt(property.Value, 0, int.MaxValue, out var weight))
            {
                why = $"'tiers.{property.Name}' must be a whole number of 0 or more";
                return false;
            }

            read[tier] = weight;
        }

        if (read.Values.All(w => w == 0))
        {
            why = "'tiers' gives every tier a weight of 0";
            return false;
        }

        weights = read;
        why = "";
        return true;
    }

    /// <summary>An integer token inside [min, max]. A float, a string, a null or a value past int range is no integer.</summary>
    private static bool TryInt(JToken? token, int min, int max, out int value)
    {
        value = 0;
        if (token == null || token.Type != JTokenType.Integer)
            return false;
        try
        {
            var raw = (long)token;
            if (raw < min || raw > max)
                return false;
            value = (int)raw;
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    /// <summary>The warnings of one read, and whether the summary is owed.</summary>
    private sealed class Run
    {
        private readonly Action<string> _warn;
        private bool _changed;

        public Run(Action<string> warn) => _warn = warn;

        /// <summary>A warning for a value that was reverted, skipped or adjusted; it makes the summary owed.</summary>
        public void Warn(string message)
        {
            _changed = true;
            _warn("TrainingWavesConfigProvider: " + message);
        }

        /// <summary>A warning that changes nothing (an unknown key); no summary follows from it alone.</summary>
        public void Note(string message) => _warn("TrainingWavesConfigProvider: " + message);

        public void Summary()
        {
            if (_changed)
                _warn("TrainingWavesConfigProvider: training_waves.json had values that were reverted, skipped or adjusted; see the warnings above");
        }
    }
}
