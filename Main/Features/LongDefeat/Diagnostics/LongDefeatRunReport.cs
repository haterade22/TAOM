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
        DateTime startedUtc, DateTime endedUtc, int maxSpeed = 1, RunBoons? boons = null, RunFort? fort = null)
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
            ["class"] = options.ClassId ?? "",
            ["startWave"] = options.StartWave,
            ["startRerolls"] = options.StartRerolls,
            ["startGold"] = options.StartGold is int startGold ? new JValue(startGold) : JValue.CreateNull(),
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
                ["summoned"] = w.Summoned,
                ["summonSpawned"] = w.SummonSpawned,
                ["summonKills"] = w.SummonKills,
                ["summonFelled"] = w.SummonFelled,
                ["commander"] = w.Commander is { } commander ? Commander(commander) : JValue.CreateNull(),
                ["gate"] = Gate(w.Fort.Gate),
                ["posts"] = new JObject { ["filled"] = w.Fort.Posts.Filled, ["short"] = w.Fort.Posts.Short, ["manned"] = w.Fort.Posts.Manned, ["lost"] = w.Fort.Posts.Lost },
                ["volley"] = Volley(w.Fort.Volley),
                ["towerVolley"] = Volley(w.Fort.TowerVolley),
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
                ["commander"] = purse.EarnedFrom(PurseSource.Commander),
                ["boon"] = purse.EarnedFrom(PurseSource.Boon),
                ["spentOn"] = new JObject
                {
                    ["packs"] = purse.SpentOn(PurseSink.Packs),
                    ["gate"] = purse.SpentOn(PurseSink.Gate),
                    ["towers"] = purse.SpentOn(PurseSink.Towers),
                    ["fieldCare"] = purse.SpentOn(PurseSink.FieldCare),
                    ["volley"] = purse.SpentOn(PurseSink.Volley),
                },
            },
            ["purchases"] = new JArray((fort ?? RunFort.None).Purchases.Select(Purchase)),
            ["fortifications"] = new JObject
            {
                ["gateLevel"] = (fort ?? RunFort.None).GateLevel,
                ["towerLevel"] = (fort ?? RunFort.None).TowerLevel,
                ["volleyLevel"] = (fort ?? RunFort.None).VolleyLevel,
            },
            ["poison"] = new JObject
            {
                ["doses"] = stats.Poison.Doses,
                ["hits"] = stats.Poison.Hits,
                ["damage"] = stats.Poison.Damage,
                ["ownerMissing"] = stats.Poison.OwnerMissing,
                ["withheld"] = stats.Poison.Withheld,
            },
        };
        return report.ToString(Newtonsoft.Json.Formatting.Indented);
    }

    /// <summary>A wave's gate: the level in force at its start, the hit points it began with and could hold, whether it fell and when (null when it stood).</summary>
    private static JObject Gate(GateWaveRecord g) => new()
    {
        ["level"] = g.Level,
        ["startHitPoints"] = g.StartHitPoints,
        ["maxHitPoints"] = g.MaxHitPoints,
        ["fell"] = g.Fell,
        ["fellAtSeconds"] = g.FellAtSeconds is float at ? new JValue(at) : JValue.CreateNull(),
    };

    private static JObject Volley(VolleyWaveRecord v) => new() { ["uses"] = v.Uses, ["arrows"] = v.Arrows, ["hits"] = v.Hits, ["kills"] = v.Kills, ["withheld"] = v.Withheld };

    private static readonly string[] PurchaseKinds = { "pack", "gateLevel", "gateRepair", "towerLevel", "fieldCare", "volleyLevel" };

    /// <summary>One purchase: the wave it precedes, its kind, the offer (a pack's only), the level a level kind reached, shop.json's price and the price paid, and the share (a repair's missing share, care's heal share) to three decimals.</summary>
    private static JObject Purchase(FortPurchase p) => new()
    {
        ["beforeWave"] = p.BeforeWave,
        ["kind"] = PurchaseKinds[(int)p.Kind],
        ["id"] = p.Id,
        ["level"] = p.Level,
        ["basePrice"] = p.BasePrice,
        ["price"] = p.Price,
        ["share"] = Math.Round((double)p.Share, 3),
    };

    /// <summary>A wave's commander: his troop, the rank's id and the steps past the ranks, how he ended, how often each ability fired and how often after his end (0 unless a clock failed to stop).</summary>
    private static JObject Commander(CommanderRecord c) => new()
    {
        ["troop"] = c.TroopId,
        ["rank"] = c.RankId,
        ["steps"] = c.Steps,
        ["outcome"] = c.Outcome.ToString().ToLowerInvariant(),
        ["uses"] = new JObject(c.Uses.Select(u => new JProperty(u.Key, u.Value))),
        ["usesAfterEnd"] = c.UsesAfterEnd,
    };

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
