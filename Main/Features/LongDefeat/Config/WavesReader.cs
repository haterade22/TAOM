using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>What waves.json holds: the rule numbers, the gold per attacker troop, and the wave rosters by id.</summary>
internal sealed class WavesConfig
{
    public static WavesConfig Empty { get; } = new(RunRules.Default,
        new Dictionary<string, int>(StringComparer.Ordinal), new Dictionary<string, WavePlan>(StringComparer.OrdinalIgnoreCase));

    public WavesConfig(RunRules rules, IReadOnlyDictionary<string, int> bounties, IReadOnlyDictionary<string, WavePlan> rosters)
    {
        Rules = rules;
        Bounties = bounties;
        Rosters = rosters;
    }

    public RunRules Rules { get; }
    public IReadOnlyDictionary<string, int> Bounties { get; }
    public IReadOnlyDictionary<string, WavePlan> Rosters { get; }
}

/// <summary>
/// waves.json: a bad rule number keeps its default; a wave with a bad count or weight is skipped, a mix row with a bad class or no
/// troop is skipped; a roster with no valid wave is dropped (the fortress naming it is then skipped). A troop a wave or a tier link
/// reaches with no bounty pays nothing, with one warning. A bad growth number keeps its default; a tier link that is not text, points
/// at its own troop, joins two known classes or closes a cycle is skipped. The <c>commanders</c> block is read by <see cref="CommandersReader"/>.
/// </summary>
internal static class WavesReader
{
    private const int DefaultCountPerWave = 10;
    private const int DefaultMaxCount = 300;
    private const float DefaultHealthPerWave = 0.05f;
    private const float DefaultDamagePerWave = 0.03f;
    private const float DefaultMaxFactor = 3.0f;
    private const int DefaultTierEveryWaves = 3;

    public static WavesConfig Read(string? text, LongDefeatConfigScope s)
    {
        var root = s.OpenRoot(text, "was not found or could not be read; default rules and no waves");
        if (root == null)
            return WavesConfig.Empty;

        var rules = ReadRules(s.Group(root, "rules", ""), s);
        var bounties = ReadBounties(s.Group(root, "bounties", ""), s);
        var rosters = new Dictionary<string, WavePlan>(StringComparer.OrdinalIgnoreCase);
        var block = s.Group(root, "rosters", "");
        if (block != null)
        {
            foreach (var property in block.Properties())
            {
                var plan = ReadRoster(property.Name, property.Value as JObject, s);
                if (plan != null)
                    rosters[property.Name] = plan;
            }
        }

        WarnUnpaid(bounties, rosters.Values, s);
        var ordered = KeepAboveStragglers(rules, rosters.Values, s);
        s.Loaded($"{rosters.Count} roster(s)");
        return new WavesConfig(ordered, bounties, rosters);
    }

    internal static RunRules KeepAboveStragglers(RunRules rules, IEnumerable<WavePlan> rosters, LongDefeatConfigScope s)
    {
        var floor = 0;
        foreach (var plan in rosters)
        {
            var smallest = plan.Authored.Min(w => w.Count);
            floor = Math.Max(floor, (int)(rules.StragglerShare * smallest) + 1);
        }

        if (rules.KeepMinAttackers >= floor)
            return rules;
        s.Warn($"rules.keepMinAttackers {rules.KeepMinAttackers} would let stragglers take the keep; using {floor}");
        return rules with { KeepMinAttackers = floor };
    }

    internal static RunRules ReadRules(JObject? o, LongDefeatConfigScope s)
    {
        var d = RunRules.Default;
        const string p = "rules";
        return new RunRules
        {
            PreparationSeconds = s.Float(o, p, "preparationSeconds", d.PreparationSeconds, 1f, 3600f),
            IntermissionSeconds = s.Float(o, p, "intermissionSeconds", d.IntermissionSeconds, 1f, 3600f),
            EarlyGoldPerSecond = s.Float(o, p, "earlyGoldPerSecond", d.EarlyGoldPerSecond, 0f, 100f),
            EarlyGoldCap = s.Int(o, p, "earlyGoldCap", d.EarlyGoldCap, 0, 1_000_000),
            WaveBonusBase = s.Int(o, p, "waveBonusBase", d.WaveBonusBase, 0, 1_000_000),
            WaveBonusPerWave = s.Int(o, p, "waveBonusPerWave", d.WaveBonusPerWave, 0, 1_000_000),
            SpawnsPerTick = s.Int(o, p, "spawnsPerTick", d.SpawnsPerTick, 1, 50),
            LiveCap = s.Int(o, p, "liveCap", d.LiveCap, 101, 400),
            GarrisonCap = s.Int(o, p, "garrisonCap", d.GarrisonCap, 1, 400),
            MusterBatch = s.Int(o, p, "musterBatch", d.MusterBatch, 1, 50),
            MusterBatchSeconds = s.Float(o, p, "musterBatchSeconds", d.MusterBatchSeconds, 1f, 3600f),
            QuietSeconds = s.Float(o, p, "quietSeconds", d.QuietSeconds, 1f, 3600f),
            QuietAnySeconds = s.Float(o, p, "quietAnySeconds", d.QuietAnySeconds, 1f, 3600f),
            StrandedSeconds = s.Float(o, p, "strandedSeconds", d.StrandedSeconds, 1f, 3600f),
            StragglerCount = s.Int(o, p, "stragglerCount", d.StragglerCount, 0, 100),
            StragglerSeconds = s.Float(o, p, "stragglerSeconds", d.StragglerSeconds, 1f, 3600f),
            StragglerShare = s.Float(o, p, "stragglerShare", d.StragglerShare, 0f, 0.5f),
            KeepRadius = s.Float(o, p, "keepRadius", d.KeepRadius, FortressesReader.KeepRadiusMin, FortressesReader.KeepRadiusMax),
            KeepHeightBand = s.Float(o, p, "keepHeightBand", d.KeepHeightBand, 1f, 20f),
            KeepHoldSeconds = s.Float(o, p, "keepHoldSeconds", d.KeepHoldSeconds, 1f, 3600f),
            KeepDecaySeconds = s.Float(o, p, "keepDecaySeconds", d.KeepDecaySeconds, 1f, 3600f),
            KeepMinAttackers = s.Int(o, p, "keepMinAttackers", d.KeepMinAttackers, 1, 100),
            KeepDominance = s.Float(o, p, "keepDominance", d.KeepDominance, 1f, 10f),
            RestShare = s.Float(o, p, "restShare", d.RestShare, 0f, 1f),
        };
    }

    private static Dictionary<string, int> ReadBounties(JObject? o, LongDefeatConfigScope s)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        if (o == null)
            return result;

        foreach (var property in o.Properties())
        {
            if (LongDefeatConfigScope.TryInt(property.Value, 0, 10_000, out var gold, out var why))
            {
                result[property.Name] = gold;
                continue;
            }

            s.Warn($"'bounties.{property.Name}' {why}; that troop pays 0");
            result[property.Name] = 0;
        }

        return result;
    }

    internal static WavePlan? ReadRoster(string id, JObject? roster, LongDefeatConfigScope s)
    {
        var label = $"rosters.{id}";
        if (roster == null)
        {
            s.Warn($"'{label}' is not an object; roster skipped");
            return null;
        }

        var scaling = s.Group(roster, "scaling", label);
        var scalingPath = label + ".scaling";
        var scale = new WaveScaling(
            s.Int(scaling, scalingPath, "countPerWave", DefaultCountPerWave, 0, 100),
            s.Int(scaling, scalingPath, "maxCount", DefaultMaxCount, 101, 1000));
        var healthPerWave = s.Float(scaling, scalingPath, "healthPerWave", DefaultHealthPerWave, 0f, 1f);
        var damagePerWave = s.Float(scaling, scalingPath, "damagePerWave", DefaultDamagePerWave, 0f, 1f);
        var maxFactor = s.Float(scaling, scalingPath, "maxFactor", DefaultMaxFactor, 1f, 10f);
        var tierEveryWaves = s.Int(scaling, scalingPath, "tierEveryWaves", DefaultTierEveryWaves, 1, 50);

        var waves = new List<WaveSpec>();
        if (roster["waves"] is JArray array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                var spec = ReadWave(array[i] as JObject, $"{label}.waves[{i}]", s);
                if (spec != null)
                    waves.Add(spec);
            }
        }
        else
        {
            s.Warn($"'{label}.waves' is missing or not an array; roster skipped");
            return null;
        }

        if (waves.Count > 0)
            return new WavePlan(waves, scale, new EndlessScale(healthPerWave, damagePerWave, maxFactor, tierEveryWaves, ReadTiers(roster, label, waves, s)),
                CommandersReader.Read(roster, label, s));

        s.Warn($"'{label}' has no valid wave; roster skipped");
        return null;
    }

    /// <summary>
    /// The tier chain: troop id to the troop it becomes one step up. The reader sees only this file, so it judges what the file shows: a target
    /// that is text, no link to the same troop, no cycle, and no link between two troops this roster's own waves give different classes. That
    /// both troops exist, share a culture and rise in level is the shipped-data tests' and the launcher's check.
    /// </summary>
    private static IReadOnlyDictionary<string, string> ReadTiers(JObject roster, string label, IReadOnlyList<WaveSpec> waves, LongDefeatConfigScope s)
    {
        var chain = new Dictionary<string, string>(StringComparer.Ordinal);
        var block = s.Group(roster, "tiers", label);
        if (block == null)
            return chain;

        var slots = new Dictionary<string, FormationSlot>(StringComparer.Ordinal);
        foreach (var troop in waves.SelectMany(w => w.Mix))
            if (!slots.ContainsKey(troop.TroopId))
                slots[troop.TroopId] = troop.Slot;

        foreach (var link in block.Properties())
        {
            var from = link.Name;
            var path = $"{label}.tiers.{from}";
            var to = LongDefeatConfigScope.Text(link.Value);
            if (to == null)
                s.Warn($"'{path}' is not a troop id; link skipped");
            else if (to == from)
                s.Warn($"'{path}' links the troop to itself; link skipped");
            else if (slots.TryGetValue(from, out var a) && slots.TryGetValue(to, out var b) && a != b)
                s.Warn($"'{path}' joins a {a} troop to a {b} troop; link skipped");
            else if (ClosesCycle(chain, from, to))
                s.Warn($"'{path}' closes a cycle through '{to}'; link skipped");
            else
                chain[from] = to;
        }

        return chain;
    }

    // Adding from -> to makes a cycle when the chain already leads from "to" back to "from".
    private static bool ClosesCycle(IReadOnlyDictionary<string, string> chain, string from, string to)
    {
        var at = to;
        for (var i = 0; i <= chain.Count && at != from; i++)
        {
            if (!chain.TryGetValue(at, out var next))
                return false;
            at = next;
        }

        return at == from;
    }

    private static WaveSpec? ReadWave(JObject? wave, string label, LongDefeatConfigScope s)
    {
        if (wave == null)
        {
            s.Warn($"'{label}' is not an object; wave skipped");
            return null;
        }

        if (!LongDefeatConfigScope.TryInt(wave["count"], 101, 1000, out var count, out var why))
        {
            s.Warn($"'{label}.count' {why}; wave skipped");
            return null;
        }

        if (!(wave["mix"] is JArray mix))
        {
            s.Warn($"'{label}.mix' is missing or not an array; wave skipped");
            return null;
        }

        var rows = new List<WaveTroop>();
        for (var i = 0; i < mix.Count; i++)
        {
            var row = mix[i] as JObject;
            var rowLabel = $"{label}.mix[{i}]";
            var troop = LongDefeatConfigScope.Text(row?["troop"]);
            if (row == null || troop == null)
            {
                s.Warn($"'{rowLabel}.troop' is missing; row skipped");
                continue;
            }

            if (!LongDefeatConfigScope.TrySlot(row["class"], out var slot, out why))
            {
                s.Warn($"'{rowLabel}.class' {why}; row skipped");
                continue;
            }

            if (!LongDefeatConfigScope.TryInt(row["weight"], 1, int.MaxValue, out var weight, out why))
            {
                s.Warn($"'{rowLabel}.weight' {why} (a positive whole number); wave skipped");
                return null;
            }

            rows.Add(new WaveTroop(troop, slot, weight));
        }

        if (rows.Count > 0)
            return new WaveSpec(count, rows);

        s.Warn($"'{label}' has no valid mix row; wave skipped");
        return null;
    }

    private static void WarnUnpaid(IReadOnlyDictionary<string, int> bounties, IEnumerable<WavePlan> rosters, LongDefeatConfigScope s)
    {
        var warned = new HashSet<string>(StringComparer.Ordinal);
        foreach (var roster in rosters)
        {
            foreach (var troop in roster.Authored.SelectMany(w => w.Mix.Select(m => m.TroopId)).Concat(roster.Endless.Targets).Concat(roster.Commanders.SummonTroopIds))
            {
                if (!bounties.ContainsKey(troop) && warned.Add(troop))
                    s.Warn($"'bounties' has no entry for '{troop}'; it pays 0");
            }
        }
    }
}
