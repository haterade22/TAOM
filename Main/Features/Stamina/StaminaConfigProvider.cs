using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.Stamina.Domain;

namespace TAOM.Features.Stamina;

/// <summary>
/// Validating loader for <c>configs/stamina.json</c> (#1497). A missing file gives the compiled defaults and a
/// warning; a file that does not parse gives the defaults and an error; a value out of its range, or not a number,
/// gives its default and a warning; a name the economy sections do not know gives a warning. Values that must keep an
/// order go back to their defaults together when the order is broken: a band's multiplier may not be milder than the
/// band before it, the swing minimum may not pass its maximum, and the band edges must leave a recovered soldier in
/// Tired. Loaded once per process: an edit needs a game restart.
/// </summary>
public sealed class StaminaConfigProvider
{
    private const float MinSwing = 0.5f;
    private const float MinHandling = 0.5f;
    private const float MinMove = 0.1f;
    // AgentStatCalculateModel clamps AIAttackOnDecideChance to 0.05 at the bottom; a factor below it only hits that floor.
    private const float MinAiAttack = 0.05f;

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<StaminaTuning> _tuning;
    private bool _rejected;

    public StaminaConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _tuning = new Lazy<StaminaTuning>(Load);
    }

    public StaminaTuning GetTuning() => _tuning.Value;

    private StaminaTuning Load()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "configs", "stamina.json");
        if (!File.Exists(path))
        {
            _logger.LogWarning($"StaminaConfigProvider: stamina.json not found at {path}, using the compiled defaults");
            return StaminaTuning.Default;
        }

        StaminaConfig parsed;
        try
        {
            parsed = JsonConvert.DeserializeObject<StaminaConfig>(File.ReadAllText(path)) ?? new StaminaConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"StaminaConfigProvider: failed to parse stamina.json, using the compiled defaults: {ex.Message}");
            return StaminaTuning.Default;
        }

        var tuning = Validate(parsed);
        if (_rejected)
            _logger.LogWarning("StaminaConfigProvider: one or more stamina.json values were rejected and replaced by their defaults; see the warnings above");
        else
            _logger.LogInfo("StaminaConfigProvider: loaded stamina.json");
        return tuning;
    }

    private StaminaTuning Validate(StaminaConfig parsed)
    {
        var winded = parsed.Effects?.Winded;
        var tired = parsed.Effects?.Tired;
        var exhausted = parsed.Effects?.Exhausted;
        var dw = StaminaTuning.DefaultWinded;
        var dt = StaminaTuning.DefaultTired;
        var de = StaminaTuning.DefaultExhausted;

        var swing = Ordered("swing", MinSwing, winded?.Swing, tired?.Swing, exhausted?.Swing, dw.Swing, dt.Swing, de.Swing);
        var handling = Ordered("handling", MinHandling, winded?.Handling, tired?.Handling, exhausted?.Handling, dw.Handling, dt.Handling, de.Handling);
        var move = Ordered("move", MinMove, winded?.Move, tired?.Move, exhausted?.Move, dw.Move, dt.Move, de.Move);
        var aiAttack = Ordered("aiAttack", MinAiAttack, winded?.AiAttack, tired?.AiAttack, exhausted?.AiAttack, dw.AiAttack, dt.AiAttack, de.AiAttack);

        var sweep = InRange(parsed.Tick?.SweepSeconds, StaminaTuning.DefaultSweepSeconds, 0.1f, 1f, "tick.sweepSeconds");
        var minAge = InRange(parsed.Tick?.MinRefreshAgeSeconds, StaminaTuning.DefaultMinRefreshAgeSeconds, 0f, 5f, "tick.minRefreshAgeSeconds");
        var refreshes = parsed.Tick?.MaxRefreshesPerFrame ?? StaminaTuning.DefaultMaxRefreshesPerFrame;
        if (refreshes < 1 || refreshes > 64)
        {
            Reject($"tick.maxRefreshesPerFrame {refreshes} is outside 1 to 64, using {StaminaTuning.DefaultMaxRefreshesPerFrame}");
            refreshes = StaminaTuning.DefaultMaxRefreshesPerFrame;
        }

        return new StaminaTuning(
            new StaminaBandEffects(swing[0], handling[0], move[0], aiAttack[0]),
            new StaminaBandEffects(swing[1], handling[1], move[1], aiAttack[1]),
            new StaminaBandEffects(swing[2], handling[2], move[2], aiAttack[2]),
            sweep, refreshes, minAge, Economy(parsed), Peoples(parsed));
    }

    private StaminaEconomy Economy(StaminaConfig parsed)
    {
        var d = StaminaEconomy.Default;
        var pool = new Section(this, "pool", parsed.Pool);
        var costs = new Section(this, "costs", parsed.Costs);
        var run = new Section(this, "run", parsed.Run);
        var regen = new Section(this, "regen", parsed.Regen);
        var bands = new Section(this, "bands", parsed.Bands);

        var e = new StaminaEconomy
        {
            PoolBase = pool.Take("base", d.PoolBase, 20f, 1000f),
            PoolPerTier = pool.Take("perTier", d.PoolPerTier, 0f, 200f),
            HeroBonus = pool.Take("heroBonus", d.HeroBonus, 0f, 1000f),
            HeroPerLevel = pool.Take("heroPerLevel", d.HeroPerLevel, 0f, 20f),
            HeroLevelCap = pool.Take("heroLevelCap", d.HeroLevelCap, 0f, 100f),

            SwingBase = costs.Take("swingBase", d.SwingBase, 0f, 100f),
            SwingPerKg = costs.Take("swingPerKg", d.SwingPerKg, 0f, 20f),
            SwingMin = costs.Take("swingMin", d.SwingMin, 0f, 100f),
            SwingMax = costs.Take("swingMax", d.SwingMax, 0f, 100f),
            BowShot = costs.Take("bowShot", d.BowShot, 0f, 100f),
            CrossbowShot = costs.Take("crossbowShot", d.CrossbowShot, 0f, 100f),
            Throw = costs.Take("throw", d.Throw, 0f, 100f),
            Jump = costs.Take("jump", d.Jump, 0f, 100f),
            Kick = costs.Take("kick", d.Kick, 0f, 100f),
            Bash = costs.Take("bash", d.Bash, 0f, 100f),
            BowHoldPerSecond = costs.Take("bowHoldPerSecond", d.BowHoldPerSecond, 0f, 100f),
            BowHoldGraceSeconds = costs.Take("bowHoldGraceSeconds", d.BowHoldGraceSeconds, 0f, 10f),
            Parry = costs.Take("parry", d.Parry, 0f, 100f),
            ShieldBase = costs.Take("shieldBase", d.ShieldBase, 0f, 100f),
            ShieldPerDamage = costs.Take("shieldPerDamage", d.ShieldPerDamage, 0f, 1f),
            ShieldMax = costs.Take("shieldMax", d.ShieldMax, 0f, 100f),
            MissileBlock = costs.Take("missileBlock", d.MissileBlock, 0f, 100f),

            RunDrainPerSecond = run.Take("drainPerSecond", d.RunDrainPerSecond, 0f, 0.5f),
            RunFloor = run.Take("floor", d.RunFloor, 0f, 1f),
            RunSpeedThreshold = run.Take("speedThreshold", d.RunSpeedThreshold, 0.5f, 10f),

            RegenPerSecond = regen.Take("perSecond", d.RegenPerSecond, 0.005f, 1f),
            RegenPauseSeconds = regen.Take("pauseSeconds", d.RegenPauseSeconds, 0f, 10f),
            BlockingRegen = regen.Take("blocking", d.BlockingRegen, 0f, 1f),
            ExhaustedRegen = regen.Take("exhausted", d.ExhaustedRegen, 0.1f, 5f),

            WindedBelow = bands.Take("windedBelow", d.WindedBelow, 0.05f, 0.95f),
            TiredBelow = bands.Take("tiredBelow", d.TiredBelow, 0.02f, 0.9f),
            Hysteresis = bands.Take("hysteresis", d.Hysteresis, 0f, 0.2f),
            RecoverAt = bands.Take("recoverAt", d.RecoverAt, 0.05f, 0.9f),
        };
        pool.WarnUnknown();
        costs.WarnUnknown();
        run.WarnUnknown();
        regen.WarnUnknown();
        bands.WarnUnknown();

        if (e.SwingMin > e.SwingMax)
        {
            Reject($"costs.swingMin {e.SwingMin} is above costs.swingMax {e.SwingMax}, using {d.SwingMin} and {d.SwingMax}");
            e.SwingMin = d.SwingMin;
            e.SwingMax = d.SwingMax;
        }

        if (e.ShieldBase > e.ShieldMax)
        {
            Reject($"costs.shieldBase {e.ShieldBase} is above costs.shieldMax {e.ShieldMax}, using {d.ShieldBase} and {d.ShieldMax}");
            e.ShieldBase = d.ShieldBase;
            e.ShieldMax = d.ShieldMax;
        }

        // Tired below Winded; and a soldier who recovers must come out inside Tired, or he would flap straight back.
        if (!(e.TiredBelow < e.WindedBelow) || !(e.RecoverAt < e.TiredBelow + e.Hysteresis + 0.0001f))
        {
            Reject($"bands must keep tiredBelow below windedBelow and recoverAt at or below tiredBelow plus hysteresis "
                   + $"({e.WindedBelow}, {e.TiredBelow}, {e.Hysteresis}, {e.RecoverAt}), using {d.WindedBelow}, {d.TiredBelow}, {d.Hysteresis}, {d.RecoverAt}");
            e.WindedBelow = d.WindedBelow;
            e.TiredBelow = d.TiredBelow;
            e.Hysteresis = d.Hysteresis;
            e.RecoverAt = d.RecoverAt;
        }

        return e;
    }

    private StaminaPeoples Peoples(StaminaConfig parsed)
    {
        var min = InRange(parsed.Peoples?.MultiplierMin, StaminaPeoples.DefaultMultiplierMin, 0.25f, 1f, "peoples.multiplierMin");
        var max = InRange(parsed.Peoples?.MultiplierMax, StaminaPeoples.DefaultMultiplierMax, 1f, 4f, "peoples.multiplierMax");
        var armour = new Section(this, "armour", parsed.Armour);
        var freeKg = armour.Take("freeKg", StaminaPeoples.DefaultArmourFreeKg, 0f, 100f);
        var kgPerStep = armour.Take("kgPerStep", StaminaPeoples.DefaultArmourKgPerStep, 5f, 1000f);
        var factorMax = armour.Take("factorMax", StaminaPeoples.DefaultArmourFactorMax, 1f, 3f);
        armour.WarnUnknown();
        return new StaminaPeoples(People("peoples.races", parsed.Peoples?.Races), People("peoples.cultures", parsed.Peoples?.Cultures),
            min, max, freeKg, kgPerStep, factorMax);
    }

    // One table of peoples. A name is matched without regard to case, so two rows that differ only in case are one
    // people: the second is dropped with a warning. Whether a name is a real race or culture is not known here: the
    // race registry needs the engine (StaminaRuntime warns once), and StaminaPeoplesTests pins the shipped names.
    private IReadOnlyDictionary<string, StaminaPeople> People(string name, Dictionary<string, StaminaPeopleConfig?>? rows)
    {
        var table = new Dictionary<string, StaminaPeople>(StringComparer.OrdinalIgnoreCase);
        if (rows == null)
            return table;
        foreach (var pair in rows)
        {
            var key = (pair.Key ?? "").Trim();
            if (key.Length == 0 || pair.Value == null)
            {
                Reject($"{name} has a row with no name or no values; it was skipped");
                continue;
            }

            if (table.ContainsKey(key))
            {
                Reject($"{name}.{key} is listed twice; the second row was skipped");
                continue;
            }

            table[key] = new StaminaPeople(
                InRange(pair.Value.Pool, 1f, 0.25f, 4f, $"{name}.{key}.pool"),
                InRange(pair.Value.Regen, 1f, 0.25f, 4f, $"{name}.{key}.regen"),
                pair.Value.Exempt ?? false);
        }

        return table;
    }

    // One multiplier across the three bands: each in its range, and never milder in a more tired band.
    private float[] Ordered(string name, float min, float? winded, float? tired, float? exhausted, float dWinded, float dTired, float dExhausted)
    {
        var values = new[]
        {
            InRange(winded, dWinded, min, 1f, $"effects.winded.{name}"),
            InRange(tired, dTired, min, 1f, $"effects.tired.{name}"),
            InRange(exhausted, dExhausted, min, 1f, $"effects.exhausted.{name}"),
        };
        if (values[1] <= values[0] && values[2] <= values[1])
            return values;
        Reject($"effects.*.{name} must not rise from winded to tired to exhausted ({values[0]}, {values[1]}, {values[2]}), using {dWinded}, {dTired}, {dExhausted}");
        return new[] { dWinded, dTired, dExhausted };
    }

    private float InRange(float? value, float fallback, float min, float max, string name)
    {
        if (value == null)
            return fallback;
        if (FiniteFloatValidator.IsFiniteInRange(value.Value, min, max))
            return value.Value;
        Reject($"{name} {value.Value} is outside {min} to {max}, using {fallback}");
        return fallback;
    }

    private void Reject(string message)
    {
        _rejected = true;
        _logger.LogWarning("StaminaConfigProvider: " + message);
    }

    // One economy section of the file: hands out its values by name and remembers which names were asked for.
    private sealed class Section
    {
        private readonly StaminaConfigProvider _owner;
        private readonly string _name;
        private readonly Dictionary<string, float?>? _values;
        private readonly HashSet<string> _known = new HashSet<string>(StringComparer.Ordinal);

        internal Section(StaminaConfigProvider owner, string name, Dictionary<string, float?>? values)
        {
            _owner = owner;
            _name = name;
            _values = values;
        }

        internal float Take(string key, float fallback, float min, float max)
        {
            _known.Add(key);
            float? value = null;
            _values?.TryGetValue(key, out value);
            return _owner.InRange(value, fallback, min, max, _name + "." + key);
        }

        // A misspelled name would otherwise load its default with no word in the log.
        internal void WarnUnknown()
        {
            if (_values == null)
                return;
            foreach (var key in _values.Keys)
            {
                if (!_known.Contains(key))
                    _owner.Reject($"{_name}.{key} is not a known setting and was ignored");
            }
        }
    }
}
