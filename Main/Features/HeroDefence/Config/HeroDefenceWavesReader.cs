using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TAOM.Features.HeroDefence.Domain;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.HeroDefence;

/// <summary>What waves.json gives. The catalog here has no cultures yet, and its opening is not yet checked against them.</summary>
internal sealed record HeroDefenceWavesFile(DefenceRules Rules, WaveCatalog Catalog)
{
    public static HeroDefenceWavesFile Defaults { get; } = new(new DefenceRules(), HeroDefenceWavesReader.DefaultCatalog);
}

/// <summary>Reads hero_defence/waves.json. A bad value reverts alone, and a bad list reverts as a whole, because one wrong row changes what every later row means.</summary>
internal static class HeroDefenceWavesReader
{
    private const int MaxLevel = 63;
    private const int MaxWave = 100000;

    private static readonly IReadOnlyList<LevelBand> DefaultBands = new[]
    {
        new LevelBand(1, 1, 6), new LevelBand(2, 11, 11), new LevelBand(3, 11, 16), new LevelBand(5, 16, 21), new LevelBand(7, 21, 26),
        new LevelBand(10, 26, 31), new LevelBand(13, 31, 36), new LevelBand(16, 36, 41), new LevelBand(20, 41, 46), new LevelBand(24, 46, 51),
    };

    private static readonly IReadOnlyList<PowerStep> DefaultSchedule = new[]
    {
        new PowerStep(5, 1), new PowerStep(10, 2), new PowerStep(15, 3), new PowerStep(20, 4), new PowerStep(25, 5),
    };

    private static readonly IReadOnlyList<string> DefaultPool = new[] { "lash", "drums", "eye", "discipline", "hide" };

    private static readonly EndlessScale DefaultScale = Scale(0.05f, 0.03f, 20f);

    /// <summary>Mike's opening: wave 1 Mordor, wave 2 Erebor. It also applies when the key is missing or bad.</summary>
    private static readonly IReadOnlyList<string> DefaultOpening = new[] { "mordor", "erebor" };

    public static WaveCatalog DefaultCatalog { get; } = new()
    {
        Opening = DefaultOpening,
        Bands = DefaultBands,
        PowerSchedule = DefaultSchedule,
        PowerPool = DefaultPool,
        Scale = DefaultScale,
    };

    public static HeroDefenceWavesFile Read(string? text, HeroDefenceConfigScope scope)
    {
        var root = scope.OpenRoot(text, "is missing; using the defaults");
        if (root == null)
            return HeroDefenceWavesFile.Defaults;

        var ranks = OpenRanks(root, scope);
        var file = new HeroDefenceWavesFile(ReadRules(root, ranks, scope), ReadCatalog(root, ranks, scope));
        scope.Loaded($"{file.Catalog.Bands.Count} level bands");
        return file;
    }

    private sealed record RankGroups(JObject? Soldier, JObject? Captain, JObject? Lieutenant, JObject? General);

    /// <summary>Opens the rank groups once, because the rules and the catalog both read them and a bad group costs one warning.</summary>
    private static RankGroups OpenRanks(JObject root, HeroDefenceConfigScope s)
    {
        var ranks = s.Group(root, "ranks", "");
        return new RankGroups(s.Group(ranks, "soldier", "ranks"), s.Group(ranks, "captain", "ranks"), s.Group(ranks, "lieutenant", "ranks"), s.Group(ranks, "general", "ranks"));
    }

    private static DefenceRules ReadRules(JObject root, RankGroups ranks, HeroDefenceConfigScope s)
    {
        const string P = "rules";
        var d = new DefenceRules();
        var o = s.Group(root, P, "");
        var soldier = ranks.Soldier;
        var captain = ranks.Captain;
        var lieutenant = ranks.Lieutenant;
        var general = ranks.General;

        return new DefenceRules
        {
            StartingGold = s.Int(o, P, "startingGold", d.StartingGold, 0, 1000000),
            StartingLives = s.Int(o, P, "startingLives", d.StartingLives, 1, 100000),
            FirstPauseSeconds = s.Float(o, P, "firstPauseSeconds", d.FirstPauseSeconds, 5f, 600f),
            PauseSeconds = s.Float(o, P, "pauseSeconds", d.PauseSeconds, 5f, 600f),
            MaxPlaced = s.Int(o, P, "maxPlaced", d.MaxPlaced, 1, 6),
            EarlyGoldPerSecond = s.Float(o, P, "earlyGoldPerSecond", d.EarlyGoldPerSecond, 0f, 1000f),
            EarlyGoldCap = s.Int(o, P, "earlyGoldCap", d.EarlyGoldCap, 0, 100000),
            WaveBonusBase = s.Int(o, P, "waveBonusBase", d.WaveBonusBase, 0, 100000),
            WaveBonusPerWave = s.Int(o, P, "waveBonusPerWave", d.WaveBonusPerWave, 0, 100000),
            BountyBase = s.Float(o, P, "bountyBase", d.BountyBase, 0f, 10000f),
            BountyPerLevel = s.Float(o, P, "bountyPerLevel", d.BountyPerLevel, 0f, 1000f),
            BountyMountedPercent = s.Float(o, P, "bountyMountedPercent", d.BountyMountedPercent, 0f, 1000f),
            PackGapSeconds = s.Float(o, P, "packGapSeconds", d.PackGapSeconds, 0f, 120f),
            ExitRadiusMetres = s.Float(o, P, "exitRadiusMetres", d.ExitRadiusMetres, 1f, 50f),
            WaveLimitBaseSeconds = s.Float(o, P, "waveLimitBaseSeconds", d.WaveLimitBaseSeconds, 30f, 3600f),
            WaveLimitPerCreepSeconds = s.Float(o, P, "waveLimitPerCreepSeconds", d.WaveLimitPerCreepSeconds, 0f, 60f),
            MinPostGapMetres = s.Float(o, P, "minPostGapMetres", d.MinPostGapMetres, 0f, 50f),
            RoadEndClearMetres = s.Float(o, P, "roadEndClearMetres", d.RoadEndClearMetres, 0f, 200f),
            SoldierLifeCost = s.Int(soldier, "ranks.soldier", "lives", d.SoldierLifeCost, 1, 100000),
            CaptainLifeCost = s.Int(captain, "ranks.captain", "lives", d.CaptainLifeCost, 1, 100000),
            LieutenantLifeCost = s.Int(lieutenant, "ranks.lieutenant", "lives", d.LieutenantLifeCost, 1, 100000),
            GeneralLifeCost = s.Int(general, "ranks.general", "lives", d.GeneralLifeCost, 1, 100000),
            CaptainBounty = s.Int(captain, "ranks.captain", "bounty", d.CaptainBounty, 0, 100000),
            LieutenantBounty = s.Int(lieutenant, "ranks.lieutenant", "bounty", d.LieutenantBounty, 0, 100000),
            GeneralBounty = s.Int(general, "ranks.general", "bounty", d.GeneralBounty, 0, 100000),
        };
    }

    private static WaveCatalog ReadCatalog(JObject root, RankGroups ranks, HeroDefenceConfigScope s)
    {
        var d = DefaultCatalog;
        var count = s.Group(root, "count", "");
        var troops = s.Group(root, "troops", "");
        var growth = s.Group(root, "growth", "");
        var leaders = s.Group(root, "leaders", "");
        var powers = s.Group(root, "powers", "");

        return d with
        {
            Opening = ReadOpening(root, s),
            Bands = ReadList(root, "bands", "bands", TryBands, DefaultBands, s),
            PowerSchedule = ReadList(powers, "schedule", "powers.schedule", TrySchedule, DefaultSchedule, s),
            PowerPool = ReadList(powers, "pool", "powers.pool", TryPool, DefaultPool, s),
            Scale = Scale(
                s.Float(growth, "growth", "healthPerWave", DefaultScale.HealthPerWave, 0f, 1f),
                s.Float(growth, "growth", "damagePerWave", DefaultScale.DamagePerWave, 0f, 1f),
                s.Float(growth, "growth", "maxFactor", DefaultScale.MaxFactor, 1f, 100f)),
            CountStart = s.Int(count, "count", "start", d.CountStart, 1, 300),
            CountPerWave = s.Int(count, "count", "perWave", d.CountPerWave, 0, 300),
            CountMax = s.Int(count, "count", "max", d.CountMax, 1, 300),
            PackSize = s.Int(count, "count", "packSize", d.PackSize, 1, 40),
            TroopsPerLevel = s.Int(troops, "troops", "perLevel", d.TroopsPerLevel, 1, 16),
            NearestLowerLevels = s.Int(troops, "troops", "nearestLowerLevels", d.NearestLowerLevels, 0, MaxLevel),
            HighLevelShare = s.Float(troops, "troops", "highLevelPercent", (float)(d.HighLevelShare * 100d), 0f, 100f) / 100d,
            ArmourPercentPerWave = s.Float(growth, "growth", "armourPercentPerWave", d.ArmourPercentPerWave, 0f, 100f),
            ArmourPercentMax = s.Float(growth, "growth", "armourPercentMax", d.ArmourPercentMax, 0f, 100f),
            SwingPercentPerWave = s.Float(growth, "growth", "swingPercentPerWave", d.SwingPercentPerWave, 0f, 100f),
            SwingPercentMax = s.Float(growth, "growth", "swingPercentMax", d.SwingPercentMax, 0f, 100f),
            GapHealthPerLevel = s.Float(growth, "growth", "gapHealthPerLevel", d.GapHealthPerLevel, 0f, 1f),
            GapDamagePerLevel = s.Float(growth, "growth", "gapDamagePerLevel", d.GapDamagePerLevel, 0f, 1f),
            LeaderEveryWaves = s.Int(leaders, "leaders", "everyWaves", d.LeaderEveryWaves, 1, 1000),
            LeaderHealth = new[]
            {
                s.Float(ranks.Captain, "ranks.captain", "health", d.LeaderHealth[0], 1f, 100f),
                s.Float(ranks.Lieutenant, "ranks.lieutenant", "health", d.LeaderHealth[1], 1f, 100f),
                s.Float(ranks.General, "ranks.general", "health", d.LeaderHealth[2], 1f, 100f),
            },
        };
    }

    private static EndlessScale Scale(float health, float damage, float max) =>
        new(health, damage, max, int.MaxValue, new Dictionary<string, string>());

    /// <summary>The ids as written. The catalog drops those that name no culture, because only it knows the cultures.</summary>
    private static IReadOnlyList<string> ReadOpening(JObject root, HeroDefenceConfigScope s) =>
        ReadList(root, "opening", "opening", TryOpening, DefaultOpening, s);

    private delegate bool ListParser<T>(JToken token, out IReadOnlyList<T> list, out string why);

    private static IReadOnlyList<T> ReadList<T>(JObject? parent, string key, string path, ListParser<T> parse, IReadOnlyList<T> fallback, HeroDefenceConfigScope s)
    {
        if (parent == null || !parent.TryGetValue(key, out var token))
            return fallback;
        if (parse(token, out var list, out var why))
            return list;
        s.Warn($"'{path}' {why}; using the shipped list");
        return fallback;
    }

    private static bool TryOpening(JToken token, out IReadOnlyList<string> list, out string why)
    {
        list = Array.Empty<string>();
        if (!TryTexts(token, out var ids, out why))
            return false;
        list = ids;
        return true;
    }

    private static bool TryPool(JToken token, out IReadOnlyList<string> list, out string why)
    {
        list = Array.Empty<string>();
        if (!TryTexts(token, out var ids, out why))
            return false;
        if (ids.Count == 0 || ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
        {
            why = "must list at least one power and no power twice";
            return false;
        }

        list = ids;
        return true;
    }

    private static bool TryTexts(JToken token, out List<string> ids, out string why)
    {
        ids = new List<string>();
        why = "";
        if (token is not JArray array)
        {
            why = "is not a list";
            return false;
        }

        foreach (var item in array)
        {
            var id = LongDefeatConfigScope.Text(item);
            if (id == null)
            {
                why = $"has the entry '{item}', which is not text";
                return false;
            }

            ids.Add(id);
        }

        return true;
    }

    private static bool TryBands(JToken token, out IReadOnlyList<LevelBand> list, out string why)
    {
        list = Array.Empty<LevelBand>();
        if (token is not JArray array || array.Count == 0)
        {
            why = "must be a list with at least one band";
            return false;
        }

        var bands = new List<LevelBand>();
        foreach (var row in array)
        {
            var fromWave = 0;
            var low = 0;
            var high = 0;
            if (row is not JObject o
                || !LongDefeatConfigScope.TryInt(o["fromWave"], 1, MaxWave, out fromWave, out why)
                || !LongDefeatConfigScope.TryInt(o["low"], 1, MaxLevel, out low, out why)
                || !LongDefeatConfigScope.TryInt(o["high"], 1, MaxLevel, out high, out why))
            {
                why = $"has a row with a bad value: {row.ToString(Newtonsoft.Json.Formatting.None)}";
                return false;
            }

            if (low > high || (bands.Count == 0 ? fromWave != 1 : fromWave <= bands[bands.Count - 1].FromWave))
            {
                why = $"has a row out of order: {row.ToString(Newtonsoft.Json.Formatting.None)}. The first row starts at wave 1, each later row starts later, and low is at most high";
                return false;
            }

            bands.Add(new LevelBand(fromWave, low, high));
        }

        list = bands;
        why = "";
        return true;
    }

    private static bool TrySchedule(JToken token, out IReadOnlyList<PowerStep> list, out string why)
    {
        list = Array.Empty<PowerStep>();
        if (token is not JArray array)
        {
            why = "is not a list";
            return false;
        }

        var steps = new List<PowerStep>();
        foreach (var row in array)
        {
            var fromWave = 0;
            var count = 0;
            if (row is not JObject o
                || !LongDefeatConfigScope.TryInt(o["fromWave"], 1, MaxWave, out fromWave, out why)
                || !LongDefeatConfigScope.TryInt(o["count"], 1, 64, out count, out why))
            {
                why = $"has a row with a bad value: {row.ToString(Newtonsoft.Json.Formatting.None)}";
                return false;
            }

            var previous = steps.LastOrDefault();
            if (previous != null && (fromWave <= previous.FromWave || count < previous.Count))
            {
                why = $"has a row out of order: {row.ToString(Newtonsoft.Json.Formatting.None)}. Each later row starts later and has at least as many powers";
                return false;
            }

            steps.Add(new PowerStep(fromWave, count));
        }

        list = steps;
        why = "";
        return true;
    }
}
