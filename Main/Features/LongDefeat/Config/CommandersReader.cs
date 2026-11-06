using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// The <c>commanders</c> block of one waves.json roster (phase2-plan.md section 3). A bad <c>every</c>, or a block that keeps no rank, gives no commanders; a rank with no troop, text name, a
/// bounty outside 0 to 10000 or a health outside 1 to 10 is skipped; a rank naming an ability the block lacks keeps its other abilities. An ability with a kind other than <c>Summon</c> or
/// <c>WarCry</c>, seconds outside 1 to 3600 (not finite included), a war cry that lasts as long as its cooldown, a summon count outside 1 to 50 or a mix with no valid row is skipped; a war cry
/// percent outside -90 to 300 is dropped with the ability kept. The step numbers keep their defaults when bad. The reader sees only this file: that a rank troop is a lord of the roster's
/// culture, and a summon troop one of its troops, is held by the shipped-data tests and the launcher's check.
/// </summary>
internal static class CommandersReader
{
    private const int MinEvery = 2;
    private const int MaxEvery = 20;
    private const float MaxSeconds = 3600f;
    private const float MinPercent = -90f;
    private const float MaxPercent = 300f;

    public static CommanderPlan Read(JObject roster, string label, LongDefeatConfigScope s)
    {
        var block = s.Group(roster, "commanders", label);
        if (block == null)
            return CommanderPlan.None;

        var path = label + ".commanders";
        var every = 0;
        var why = "is missing";
        if (!(block.TryGetValue("every", out var everyToken) && LongDefeatConfigScope.TryInt(everyToken, MinEvery, MaxEvery, out every, out why)))
        {
            s.Warn($"'{path}.every' {why}; no commanders for this roster");
            return CommanderPlan.None;
        }

        var abilities = ReadAbilities(s.Group(block, "abilities", path), path + ".abilities", s);
        var ranks = ReadRanks(block["ranks"] as JArray, path, abilities, s);
        if (ranks.Count == 0)
        {
            s.Warn($"'{path}.ranks' keeps no valid rank; no commanders for this roster");
            return CommanderPlan.None;
        }

        return new CommanderPlan(every, ranks, abilities, ReadBeyond(s.Group(block, "beyond", path), path + ".beyond", s));
    }

    private static IReadOnlyList<CommanderRank> ReadRanks(JArray? array, string path, IReadOnlyDictionary<string, CommanderAbility> abilities, LongDefeatConfigScope s)
    {
        var ranks = new List<CommanderRank>();
        if (array == null)
        {
            s.Warn($"'{path}.ranks' is missing or not an array");
            return ranks;
        }

        for (var i = 0; i < array.Count; i++)
        {
            var rank = ReadRank(array[i] as JObject, $"{path}.ranks[{i}]", abilities, s);
            if (rank != null && ranks.All(r => r.Id != rank.Id))
                ranks.Add(rank);
            else if (rank != null)
                s.Warn($"'{path}.ranks[{i}].rank' repeats the id '{rank.Id}'; rank skipped");
        }

        return ranks;
    }

    private static CommanderRank? ReadRank(JObject? o, string path, IReadOnlyDictionary<string, CommanderAbility> abilities, LongDefeatConfigScope s)
    {
        if (o == null)
        {
            s.Warn($"'{path}' is not an object; rank skipped");
            return null;
        }

        var id = LongDefeatConfigScope.Text(o["rank"]);
        var name = LongDefeatConfigScope.Text(o["name"]);
        var troop = LongDefeatConfigScope.Text(o["troop"]);
        if (id == null || name == null || troop == null)
        {
            s.Warn($"'{path}.{(id == null ? "rank" : name == null ? "name" : "troop")}' is missing; rank skipped");
            return null;
        }

        if (!LongDefeatConfigScope.TryInt(o["bounty"], 0, 10_000, out var bounty, out var why))
        {
            s.Warn($"'{path}.bounty' {why}; rank skipped");
            return null;
        }

        if (!LongDefeatConfigScope.TryFloat(o["health"], 1f, 10f, out var health, out why))
        {
            s.Warn($"'{path}.health' {why}; rank skipped");
            return null;
        }

        var ids = new List<string>();
        if (o["abilities"] is JArray listed)
        {
            foreach (var token in listed)
            {
                var ability = LongDefeatConfigScope.Text(token);
                if (ability != null && abilities.ContainsKey(ability))
                    ids.Add(ability);
                else
                    s.Warn($"'{path}.abilities' names '{ability ?? token.ToString()}', which the block has no valid ability for; left out");
            }
        }

        return new CommanderRank(id, name, troop, bounty, health, ids);
    }

    private static IReadOnlyDictionary<string, CommanderAbility> ReadAbilities(JObject? block, string path, LongDefeatConfigScope s)
    {
        var abilities = new Dictionary<string, CommanderAbility>(StringComparer.Ordinal);
        if (block == null)
            return abilities;

        foreach (var property in block.Properties())
        {
            var ability = ReadAbility(property.Name, property.Value as JObject, $"{path}.{property.Name}", s);
            if (ability != null)
                abilities[property.Name] = ability;
        }

        return abilities;
    }

    private static CommanderAbility? ReadAbility(string id, JObject? o, string path, LongDefeatConfigScope s)
    {
        if (o == null)
        {
            s.Warn($"'{path}' is not an object; ability skipped");
            return null;
        }

        var kindText = LongDefeatConfigScope.Text(o["kind"]);
        var kind = kindText == "Summon" ? CommanderAbilityKind.Summon : CommanderAbilityKind.WarCry;
        if (kindText != "Summon" && kindText != "WarCry")
        {
            s.Warn($"'{path}.kind' is '{kindText ?? "missing"}', not 'Summon' or 'WarCry'; ability skipped");
            return null;
        }

        var name = LongDefeatConfigScope.Text(o["name"]);
        if (name == null)
        {
            s.Warn($"'{path}.name' is missing; ability skipped");
            return null;
        }

        if (!Seconds(o, path, "firstSeconds", s, out var first) || !Seconds(o, path, "cooldownSeconds", s, out var cooldown))
            return null;

        var ability = new CommanderAbility(id, kind, name, first, cooldown);
        return kind == CommanderAbilityKind.WarCry ? ReadWarCry(ability, o, path, s) : ReadSummon(ability, o, path, s);
    }

    private static CommanderAbility? ReadWarCry(CommanderAbility ability, JObject o, string path, LongDefeatConfigScope s)
    {
        if (!Seconds(o, path, "durationSeconds", s, out var duration))
            return null;

        if (!(duration < ability.CooldownSeconds))
        {
            s.Warn($"'{path}.durationSeconds' {LongDefeatConfigScope.Format(duration)} is not below the cooldown {LongDefeatConfigScope.Format(ability.CooldownSeconds)}; ability skipped");
            return null;
        }

        return ability with
        {
            DurationSeconds = duration,
            SwingSpeedPercent = s.Float(o, path, "swingSpeedPercent", 0f, MinPercent, MaxPercent),
            MoveSpeedPercent = s.Float(o, path, "moveSpeedPercent", 0f, MinPercent, MaxPercent),
        };
    }

    private static CommanderAbility? ReadSummon(CommanderAbility ability, JObject o, string path, LongDefeatConfigScope s)
    {
        if (!LongDefeatConfigScope.TryInt(o["count"], 1, CommanderPlan.MaxSummon, out var count, out var why))
        {
            s.Warn($"'{path}.count' {why}; ability skipped");
            return null;
        }

        var mix = ReadMix(o["mix"] as JArray, path + ".mix", s);
        if (mix.Count == 0)
        {
            s.Warn($"'{path}.mix' has no valid row; ability skipped");
            return null;
        }

        return ability with { Count = count, Mix = mix };
    }

    private static List<WaveTroop> ReadMix(JArray? array, string path, LongDefeatConfigScope s)
    {
        var rows = new List<WaveTroop>();
        if (array == null)
            return rows;

        for (var i = 0; i < array.Count; i++)
        {
            var row = array[i] as JObject;
            var rowPath = $"{path}[{i}]";
            var troop = LongDefeatConfigScope.Text(row?["troop"]);
            if (row == null || troop == null)
            {
                s.Warn($"'{rowPath}.troop' is missing; row skipped");
                continue;
            }

            if (!LongDefeatConfigScope.TrySlot(row["class"], out var slot, out var why))
            {
                s.Warn($"'{rowPath}.class' {why}; row skipped");
                continue;
            }

            if (!LongDefeatConfigScope.TryInt(row["weight"], 1, int.MaxValue, out var weight, out why))
            {
                s.Warn($"'{rowPath}.weight' {why} (a positive whole number); row skipped");
                continue;
            }

            rows.Add(new WaveTroop(troop, slot, weight));
        }

        return rows;
    }

    private static CommanderBeyond ReadBeyond(JObject? o, string path, LongDefeatConfigScope s) => new(
        s.Float(o, path, "healthPerStep", 0.25f, 0f, 5f),
        s.Float(o, path, "cooldownFactorPerStep", 0.9f, 0.1f, 1f),
        s.Int(o, path, "summonPerStep", 5, 0, CommanderPlan.MaxSummon),
        s.Int(o, path, "maxSteps", 10, 0, 100));

    private static bool Seconds(JObject o, string path, string key, LongDefeatConfigScope s, out float seconds)
    {
        if (LongDefeatConfigScope.TryFloat(o[key], 1f, MaxSeconds, out seconds, out var why))
            return true;

        s.Warn($"'{path}.{key}' {why}; ability skipped");
        return false;
    }
}
