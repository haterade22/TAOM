using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat.Diagnostics;

/// <summary>The JSON text of a finished run: the result, the counts the checks read and the gold books. Pure; the file is written elsewhere.</summary>
internal static class LongDefeatRunReport
{
    /// <summary>Schema 2 (phase2-plan.md section 7): schema 1's fields keep their names and meaning, the Phase 2 fields are added, and a schema 1 reader refuses the file at its first rule.</summary>
    private const int Schema = 2;

    internal static string Build(string fortressId, RunOptions options, RunProvenance provenance, RunResult result, RunStats stats, Purse purse,
        DateTime startedUtc, DateTime endedUtc, int maxSpeed = 1, RunBoons? boons = null)
    {
        var report = new JObject
        {
            ["schema"] = Schema,
            ["fortress"] = fortressId,
            ["build"] = provenance.Build,
            ["balance"] = provenance.Balance,
            ["cheatMode"] = provenance.CheatMode,
            ["mods"] = new JArray(provenance.Mods),
            ["autoplay"] = options.Autoplay,
            ["garrisonScale"] = options.GarrisonScale,
            ["maxSpeed"] = maxSpeed,
            ["seed"] = options.Seed is ulong seed ? new JValue(seed) : JValue.CreateNull(),
            ["class"] = "",
            ["startWave"] = options.StartWave,
            ["startRerolls"] = options.StartRerolls,
            ["boons"] = new JArray((boons?.Picks ?? Array.Empty<BoonPickRecord>()).Select(Pick)),
            ["rerolls"] = new JObject { ["earned"] = boons?.Earned ?? 0, ["used"] = boons?.Used ?? 0, ["banked"] = boons?.Banked ?? 0 },
            ["targetWaves"] = options.StopAfterWaves is int target ? new JValue(target) : JValue.CreateNull(),
            ["startedUtc"] = startedUtc.ToString("o", CultureInfo.InvariantCulture),
            ["endedUtc"] = endedUtc.ToString("o", CultureInfo.InvariantCulture),
            ["final"] = result.IsFinal,
            ["reason"] = result.Reason.ToString(),
            ["wave"] = result.Wave,
            ["wavesCleared"] = result.WavesCleared,
            ["kills"] = result.Kills,
            ["fled"] = stats.Fled,
            ["fleeingPeak"] = stats.FleeingPeak,
            ["playerDeaths"] = stats.PlayerDeaths,
            ["wavesFelled"] = stats.WavesFelled,
            ["waves"] = new JArray(stats.Waves.Select(w => new JObject
            {
                ["wave"] = w.Wave,
                ["kills"] = w.Kills,
                ["felled"] = w.Felled,
                ["lost"] = w.Lost,
                ["seconds"] = w.Seconds,
                ["affixes"] = new JArray(w.Affixes),
                ["healthFactor"] = w.HealthFactor,
                ["damageFactor"] = w.DamageFactor,
                ["summoned"] = 0,
                ["summonKills"] = 0,
                ["summonFelled"] = 0,
                ["commander"] = JValue.CreateNull(),
            })),
            ["gold"] = new JObject
            {
                ["start"] = purse.Start,
                ["earned"] = purse.TotalEarned,
                ["spent"] = purse.Spent,
                ["balance"] = purse.Balance,
                ["bounty"] = purse.EarnedFrom(PurseSource.Bounty),
                ["waveBonus"] = purse.EarnedFrom(PurseSource.WaveBonus),
                ["earlyStart"] = purse.EarnedFrom(PurseSource.EarlyStart),
                ["commander"] = 0,
                ["boon"] = purse.EarnedFrom(PurseSource.Boon),
            },
            ["poison"] = new JObject { ["doses"] = 0, ["hits"] = 0, ["damage"] = 0, ["ownerMissing"] = 0, ["withheld"] = 0 },
        };
        return report.ToString(Newtonsoft.Json.Formatting.Indented);
    }

    /// <summary>One pick: its number, the wave whose clear offered it, every draw (the first, then each reroll), the boon taken (null when none) and the wave it acts on (0 when none).</summary>
    private static JObject Pick(BoonPickRecord p) => new()
    {
        ["pick"] = p.Pick,
        ["wave"] = p.Wave,
        ["offered"] = new JArray(p.Draws.Select(d => new JArray(d.Ids))),
        ["taken"] = p.Taken == null ? JValue.CreateNull() : new JValue(p.Taken),
        ["takenBeforeWave"] = p.TakenBeforeWave,
    };
}
