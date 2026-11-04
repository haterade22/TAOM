using System.Linq;
using Newtonsoft.Json.Linq;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat.Diagnostics;

/// <summary>The JSON text of a finished run: the result, the counts the checks read and the gold books. Pure; the file is written elsewhere.</summary>
internal static class LongDefeatRunReport
{
    internal static string Build(string fortressId, RunResult result, RunStats stats, Purse purse)
    {
        var report = new JObject
        {
            ["fortress"] = fortressId,
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
