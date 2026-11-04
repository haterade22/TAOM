using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat.Diagnostics;

/// <summary>The JSON text of a finished run: the result, the counts the checks read and the gold books. Pure; the file is written elsewhere.</summary>
internal static class LongDefeatRunReport
{
    private const int Schema = 1;

    internal static string Build(string fortressId, RunOptions options, RunProvenance provenance, RunResult result, RunStats stats, Purse purse,
        DateTime startedUtc, DateTime endedUtc)
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
            },
        };
        return report.ToString(Newtonsoft.Json.Formatting.Indented);
    }
}
