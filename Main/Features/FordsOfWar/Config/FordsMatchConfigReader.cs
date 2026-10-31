using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar;

/// <summary>
/// match.json: one group per record of <see cref="FordsMatchConfig"/>, camelCase keys named after the record members. Every
/// number that is missing or bad takes the value of <see cref="FordsMatchConfig.Default"/> (one warning per bad value); a file
/// that cannot be read gives the whole default. <c>aiProfiles</c> starts from the three default profiles, so easy, normal and hard
/// always exist; an entry overrides the named profile, and a new name is added on top of the normal profile.
/// </summary>
internal static class FordsMatchConfigReader
{
    private const int MaxGold = 10_000_000;
    private const int MaxCount = 1000;

    public static FordsMatchConfig Read(string? text, FordsConfigScope s)
    {
        var d = FordsMatchConfig.Default;
        var root = s.OpenRoot(text, "was not found or could not be read; using the default match numbers");
        if (root == null)
            return d;

        return new FordsMatchConfig(
            ReadEconomy(s.Group(root, "economy", ""), d.Economy, s),
            ReadWaves(s.Group(root, "waves", ""), d.Waves, s),
            ReadTowers(s.Group(root, "towers", ""), d.Towers, s),
            ReadHeroes(s.Group(root, "heroes", ""), d.Heroes, s),
            ReadLanes(s.Group(root, "lanes", ""), d.Lanes, s),
            ReadCapture(s.Group(root, "capture", ""), d.Capture, s),
            ReadAiProfiles(s.Group(root, "aiProfiles", ""), d.AiProfiles, s),
            s.Float(root, "", "countdownSeconds", d.CountdownSeconds, 0f, 300f));
    }

    private static FordsEconomyConfig ReadEconomy(JObject? g, FordsEconomyConfig d, FordsConfigScope s) => new(
        s.Int(g, "economy", "startingGold", d.StartingGold, 0, MaxGold),
        s.Int(g, "economy", "incomePerMinute", d.IncomePerMinute, 0, MaxGold),
        s.Float(g, "economy", "killBountyFraction", d.KillBountyFraction, 0f, 10f),
        s.Int(g, "economy", "minKillBounty", d.MinKillBounty, 0, MaxGold),
        s.Int(g, "economy", "towerBounty", d.TowerBounty, 0, MaxGold),
        s.Int(g, "economy", "heroKillBounty", d.HeroKillBounty, 0, MaxGold),
        s.Int(g, "economy", "captureIncomePerMinute", d.CaptureIncomePerMinute, 0, MaxGold),
        s.Int(g, "economy", "maxGold", d.MaxGold, 1, 100_000_000));

    private static FordsWaveConfig ReadWaves(JObject? g, FordsWaveConfig d, FordsConfigScope s) => new(
        s.Float(g, "waves", "freeWaveIntervalSeconds", d.FreeWaveIntervalSeconds, 0f, 3600f),
        s.Int(g, "waves", "maxLivePerLanePerSide", d.MaxLivePerLanePerSide, 1, MaxCount),
        s.Int(g, "waves", "maxLivePerSide", d.MaxLivePerSide, 1, 5 * MaxCount),
        s.Int(g, "waves", "maxSpawnsPerTick", d.MaxSpawnsPerTick, 1, 100),
        s.Int(g, "waves", "maxQueuedPacksPerLane", d.MaxQueuedPacksPerLane, 0, 50));

    private static FordsTowerConfig ReadTowers(JObject? g, FordsTowerConfig d, FordsConfigScope s) => new(
        s.Int(g, "towers", "towerHitPoints", d.TowerHitPoints, 1, MaxGold),
        s.Int(g, "towers", "innerTowerHitPoints", d.InnerTowerHitPoints, 1, MaxGold),
        s.Int(g, "towers", "keepHitPoints", d.KeepHitPoints, 1, MaxGold),
        s.Float(g, "towers", "rangedDamageFactor", d.RangedDamageFactor, 0f, 100f),
        s.Float(g, "towers", "heroDamageFactor", d.HeroDamageFactor, 0f, 100f),
        s.Float(g, "towers", "siegeDamageFactor", d.SiegeDamageFactor, 0f, 100f),
        s.Float(g, "towers", "regenPerSecond", d.RegenPerSecond, 0f, 100_000f),
        s.Float(g, "towers", "regenDelaySeconds", d.RegenDelaySeconds, 0f, 3600f),
        s.Int(g, "towers", "gunnersPerTower", d.GunnersPerTower, 0, 10),
        s.Float(g, "towers", "volleyIntervalSeconds", d.VolleyIntervalSeconds, 0.1f, 600f),
        s.Int(g, "towers", "volleyMissiles", d.VolleyMissiles, 0, 50),
        s.Float(g, "towers", "fireRange", d.FireRange, 1f, 500f),
        s.Float(g, "towers", "attackRange", d.AttackRange, 0.5f, 200f));

    private static FordsHeroConfig ReadHeroes(JObject? g, FordsHeroConfig d, FordsConfigScope s) => new(
        s.Int(g, "heroes", "maxAlivePerSide", d.MaxAlivePerSide, 0, 10),
        s.Float(g, "heroes", "respawnBaseSeconds", d.RespawnBaseSeconds, 0f, 3600f),
        s.Float(g, "heroes", "respawnPerMinuteSeconds", d.RespawnPerMinuteSeconds, 0f, 600f),
        s.Float(g, "heroes", "buybackBaseFraction", d.BuybackBaseFraction, 0f, 10f),
        s.Float(g, "heroes", "buybackPerMinuteFraction", d.BuybackPerMinuteFraction, 0f, 10f));

    private static FordsLaneConfig ReadLanes(JObject? g, FordsLaneConfig d, FordsConfigScope s) => new(
        s.Float(g, "lanes", "orderCooldownSeconds", d.OrderCooldownSeconds, 0f, 600f),
        s.Float(g, "lanes", "engageRange", d.EngageRange, 1f, 300f),
        s.Float(g, "lanes", "waypointReachRadius", d.WaypointReachRadius, 0.5f, 100f),
        s.Float(g, "lanes", "holdDistanceBeforeTower", d.HoldDistanceBeforeTower, 0f, 300f),
        s.Int(g, "lanes", "assaultThreshold", d.AssaultThreshold, 1, MaxCount));

    private static FordsCaptureConfig ReadCapture(JObject? g, FordsCaptureConfig d, FordsConfigScope s) => new(
        s.Float(g, "capture", "radius", d.Radius, 1f, 200f),
        s.Float(g, "capture", "secondsToCapture", d.SecondsToCapture, 0.1f, 3600f));

    private static IReadOnlyDictionary<string, FordsAiProfile> ReadAiProfiles(
        JObject? block, IReadOnlyDictionary<string, FordsAiProfile> defaults, FordsConfigScope s)
    {
        var result = new Dictionary<string, FordsAiProfile>(defaults.Count);
        foreach (var pair in defaults)
            result[pair.Key] = pair.Value;
        if (block == null)
            return result;

        foreach (var property in block.Properties())
        {
            var name = property.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                s.Warn("an aiProfiles entry has an empty name; skipped");
                continue;
            }

            var path = "aiProfiles." + name;
            var known = result.TryGetValue(name, out var current);
            if (!(property.Value is JObject entry))
            {
                s.Warn($"'{path}' is not an object; {(known ? "using the default profile" : "profile skipped")}");
                continue;
            }

            var baseline = known ? current! : defaults["normal"] with { Id = name };
            result[name] = new FordsAiProfile(
                name,
                s.Float(entry, path, "decisionIntervalSeconds", baseline.DecisionIntervalSeconds, 0.5f, 300f),
                s.Float(entry, path, "incomeMultiplier", baseline.IncomeMultiplier, 0f, 10f),
                s.Float(entry, path, "mistakeChance", baseline.MistakeChance, 0f, 1f),
                s.Int(entry, path, "heroReserveGold", baseline.HeroReserveGold, 0, MaxGold));
        }

        return result;
    }
}
