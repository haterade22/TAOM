using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.RaceAbilities.Domain;

namespace TAOM.Features.RaceAbilities;

// Validating loader for race_abilities.json (the CombatMechanicsConfigProvider pattern): a missing
// file gives the compiled profiles with a warning, a parse failure gives them with an error, and a
// parseable but invalid value reverts per field, or drops the bad trigger or the whole profile, with a
// warning and one summary line. A key may name several races or cultures, comma-separated: they share one
// validated profile object, which is what makes them one ability for the rally. Loaded once per process
// (Reuse.Singleton): an edit needs a restart.
public class RaceAbilitiesConfigProvider : IRaceAbilitiesConfigProvider
{
    // Replace, not append-merge: a JSON list or map replaces the compiled default instead of adding to it.
    private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
    {
        ObjectCreationHandling = ObjectCreationHandling.Replace,
    };

    private static readonly string[] WarCries = { "", "Yell", "Charge", "Victory", "Grunt" };
    private static readonly string EmptyEffects = JsonConvert.SerializeObject(new RaceAbilityEffects());

    // Proximity searches past this degrade towards a scan of every agent (the Dread Aura's 30 m cap,
    // with room for an archer's sight line).
    private const float MaxRange = 40f;
    private const float MaxEventSeconds = 30f;

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<RaceAbilitiesConfig> _config;

    public RaceAbilitiesConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<RaceAbilitiesConfig>(LoadConfig);
    }

    public RaceAbilitiesConfig GetConfig() => _config.Value;

    private RaceAbilitiesConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "race_abilities", "race_abilities.json");
        RaceAbilitiesConfig parsed;
        if (!File.Exists(path))
        {
            _logger.LogWarning($"RaceAbilitiesConfigProvider: race_abilities.json not found at {path}, using the compiled profiles");
            parsed = new RaceAbilitiesConfig();
        }
        else
        {
            try
            {
                parsed = JsonConvert.DeserializeObject<RaceAbilitiesConfig>(File.ReadAllText(path), SerializerSettings)
                    ?? new RaceAbilitiesConfig();
            }
            catch (Exception ex)
            {
                _logger.LogError($"RaceAbilitiesConfigProvider: Failed to parse race_abilities.json: {ex.Message}");
                parsed = new RaceAbilitiesConfig();
            }
        }

        // The compiled defaults go through the same checks, which parse their trigger kinds.
        var rejected = false;
        var config = new RaceAbilitiesConfig
        {
            Enabled = parsed.Enabled,
            TierScaling = ValidateTierScaling(parsed.TierScaling, ref rejected),
            Races = ValidateSection(parsed.Races, "races", RaceAbilityDefaults.RaceProfiles, ref rejected),
            Cultures = ValidateSection(parsed.Cultures, "cultures", RaceAbilityDefaults.CultureProfiles, ref rejected),
        };
        if (rejected)
            _logger.LogWarning("RaceAbilitiesConfigProvider: race_abilities.json had invalid values; they were reverted or skipped (see the warnings above)");
        return config;
    }

    private RaceAbilityTierScaling ValidateTierScaling(RaceAbilityTierScaling? parsed, ref bool rejected)
    {
        var defaults = new RaceAbilityTierScaling();
        if (parsed == null)
        {
            Warn("tierScaling is null, using the defaults", ref rejected);
            return defaults;
        }

        // MinFactor <= 1 <= MaxFactor by construction, so the pair can never cross.
        return new RaceAbilityTierScaling
        {
            BaseTier = parsed.BaseTier >= 0 && parsed.BaseTier <= 10 ? parsed.BaseTier : Revert(parsed.BaseTier, defaults.BaseTier, "tierScaling.baseTier", "[0,10]", ref rejected),
            PercentPerTier = Check(parsed.PercentPerTier, 0f, 50f, defaults.PercentPerTier, "tierScaling.percentPerTier", ref rejected),
            MinFactor = CheckAboveZero(parsed.MinFactor, 1f, defaults.MinFactor, "tierScaling.minFactor", ref rejected),
            MaxFactor = Check(parsed.MaxFactor, 1f, 3f, defaults.MaxFactor, "tierScaling.maxFactor", ref rejected),
            HeroFactor = CheckAboveZero(parsed.HeroFactor, 3f, defaults.HeroFactor, "tierScaling.heroFactor", ref rejected),
        };
    }

    // One validated profile object per JSON entry, under every id its key lists.
    private Dictionary<string, RaceAbilityProfile> ValidateSection(Dictionary<string, RaceAbilityProfile>? parsed, string section,
        Func<Dictionary<string, RaceAbilityProfile>> defaults, ref bool rejected)
    {
        if (parsed == null)
        {
            Warn($"{section} is null, using the compiled profiles", ref rejected);
            parsed = defaults();
        }

        var byId = new Dictionary<string, RaceAbilityProfile>(StringComparer.Ordinal);
        foreach (var pair in parsed)
        {
            var ids = SplitKey(pair.Key);
            if (ids.Count == 0)
            {
                Warn($"a {section} entry has an empty name, skipped", ref rejected);
                continue;
            }
            var profile = ValidateProfile($"{section}['{pair.Key}']", ids[0], pair.Value, ref rejected);
            if (profile == null)
                continue;
            foreach (var id in ids)
            {
                if (byId.ContainsKey(id))
                    Warn($"{section}: '{id}' is named by more than one entry; the later one ('{pair.Key}') wins", ref rejected);
                byId[id] = profile;
            }
        }
        return byId;
    }

    private static List<string> SplitKey(string? key)
    {
        var ids = new List<string>();
        foreach (var part in (key ?? "").Split(','))
        {
            var id = part.Trim();
            if (id.Length > 0 && !ids.Contains(id))
                ids.Add(id);
        }
        return ids;
    }

    private RaceAbilityProfile? ValidateProfile(string field, string firstId, RaceAbilityProfile? parsed, ref bool rejected)
    {
        if (parsed == null)
        {
            Warn($"{field} is null, skipped", ref rejected);
            return null;
        }

        if (!FiniteFloatValidator.IsFinite(parsed.DurationSeconds) || !(parsed.DurationSeconds > 0f) || parsed.DurationSeconds > 120f)
        {
            Warn($"{field}.durationSeconds={parsed.DurationSeconds} must be a finite value in (0,120], skipped", ref rejected);
            return null;
        }
        if (!FiniteFloatValidator.IsFiniteInRange(parsed.CooldownSeconds, 1f, 600f))
        {
            Warn($"{field}.cooldownSeconds={parsed.CooldownSeconds} must be a finite value in [1,600], skipped", ref rejected);
            return null;
        }

        var profile = new RaceAbilityProfile
        {
            AbilityId = string.IsNullOrEmpty(parsed.AbilityId) ? firstId : parsed.AbilityId,
            DurationSeconds = parsed.DurationSeconds,
            CooldownSeconds = parsed.CooldownSeconds,
            SpentSeconds = Check(parsed.SpentSeconds, 0f, 60f, 0f, $"{field}.spentSeconds", ref rejected),
            KillExtensionSeconds = Check(parsed.KillExtensionSeconds, 0f, MaxEventSeconds, 0f, $"{field}.killExtensionSeconds", ref rejected),
            RallyRadius = Check(parsed.RallyRadius, 0f, 30f, 0f, $"{field}.rallyRadius", ref rejected),
            WarCry = ValidateWarCry(parsed.WarCry, $"{field}.warCry", ref rejected),
            KinRaces = SplitList(parsed.KinRaces),
            KinBonus = ValidateKinBonus(parsed.KinBonus, $"{field}.kinBonus", ref rejected),
            Requires = ValidateTriggers(parsed.Requires, $"{field}.requires", ref rejected),
            AnyOf = ValidateTriggers(parsed.AnyOf, $"{field}.anyOf", ref rejected),
            Effects = ValidateEffects(parsed.Effects, $"{field}.effects", ref rejected),
            Spent = ValidateEffects(parsed.Spent, $"{field}.spent", ref rejected),
        };

        // 0 means no extension past the duration; anything else may not end before the duration does.
        var maxDuration = Check(parsed.MaxDurationSeconds, 0f, 180f, 0f, $"{field}.maxDurationSeconds", ref rejected);
        if (maxDuration > 0f && maxDuration < profile.DurationSeconds)
            Warn($"{field}.maxDurationSeconds={maxDuration} is below durationSeconds={profile.DurationSeconds}, raised to it", ref rejected);
        profile.MaxDurationSeconds = Math.Max(maxDuration, profile.DurationSeconds);

        // Cooldown runs from activation, so it must outlast the longest active window and the spent
        // phase, or the ability could fire again while still running.
        var longest = profile.MaxDurationSeconds + profile.SpentSeconds;
        if (profile.CooldownSeconds < longest)
        {
            Warn($"{field}.cooldownSeconds={profile.CooldownSeconds} is shorter than the longest window ({longest}), raised to it", ref rejected);
            profile.CooldownSeconds = longest;
        }

        // Accepted, but nothing would ever read it: say so rather than let a tuning edit do nothing.
        if (profile.KillExtensionSeconds > 0f && !(profile.MaxDurationSeconds > profile.DurationSeconds))
            Warn($"{field}.killExtensionSeconds is set but maxDurationSeconds does not exceed durationSeconds, so kills cannot extend it", ref rejected);
        if (!(profile.SpentSeconds > 0f) && JsonConvert.SerializeObject(profile.Spent) != EmptyEffects)
            Warn($"{field}.spent has effects but spentSeconds is 0, so they never apply", ref rejected);
        if (profile.KinRaces.Count > 0 && profile.KinBonus == null
            && !Reads(profile, RaceAbilityTriggerKind.KinWithin) && !Reads(profile, RaceAbilityTriggerKind.KinFell))
            Warn($"{field}.kinRaces is set but no KinWithin or KinFell trigger and no kinBonus reads it", ref rejected);
        if (profile.KinBonus != null && !(profile.KinBonus.PerKinPercent > 0f))
            Warn($"{field}.kinBonus.perKinPercent is 0, so the bonus adds nothing", ref rejected);
        if (profile.Spent.MoraleOnEnd != 0f)
            Warn($"{field}.spent.moraleOnEnd is never read: the price is paid from effects when the active window ends", ref rejected);
        WarnHalfPairs(profile.Effects, $"{field}.effects", ref rejected);
        WarnHalfPairs(profile.Spent, $"{field}.spent", ref rejected);

        if (profile.Requires.Count == 0 && profile.AnyOf.Count == 0)
        {
            Warn($"{field} has no valid trigger (use kind Always for a plain timer), skipped", ref rejected);
            return null;
        }
        return profile;
    }

    private static bool Reads(RaceAbilityProfile profile, RaceAbilityTriggerKind kind) =>
        profile.Requires.Exists(t => t.ParsedKind == kind) || profile.AnyOf.Exists(t => t.ParsedKind == kind);

    // Fear on a kill needs both its radius and its morale, and a fear aura both its radius and its rate.
    private void WarnHalfPairs(RaceAbilityEffects effects, string field, ref bool rejected)
    {
        if ((effects.FearOnKillRadius > 0f) != (effects.FearOnKillMorale > 0f))
            Warn($"{field}: fearOnKillRadius and fearOnKillMorale act only together, and one of them is 0", ref rejected);
        if ((effects.FearAuraRadius > 0f) != (effects.FearAuraMoralePerSecond > 0f))
            Warn($"{field}: fearAuraRadius and fearAuraMoralePerSecond act only together, and one of them is 0", ref rejected);
    }

    private static List<string> SplitList(List<string>? values)
    {
        var cleaned = new List<string>();
        if (values == null)
            return cleaned;
        foreach (var value in values)
            foreach (var id in SplitKey(value))
                if (!cleaned.Contains(id))
                    cleaned.Add(id);
        return cleaned;
    }

    private RaceAbilityKinBonus? ValidateKinBonus(RaceAbilityKinBonus? parsed, string field, ref bool rejected)
    {
        if (parsed == null)
            return null;
        var valid = FiniteFloatValidator.IsFiniteAtMost(parsed.Radius, 30f) && parsed.Radius > 0f
            && FiniteFloatValidator.IsFiniteInRange(parsed.PerKinPercent, 0f, 50f)
            && parsed.MaxKin >= 1 && parsed.MaxKin <= 50;
        if (valid)
            return new RaceAbilityKinBonus { Radius = parsed.Radius, PerKinPercent = parsed.PerKinPercent, MaxKin = parsed.MaxKin };
        Warn($"{field} needs radius in (0,30], perKinPercent in [0,50] and maxKin in [1,50]; dropped", ref rejected);
        return null;
    }

    private string ValidateWarCry(string? value, string field, ref bool rejected)
    {
        foreach (var cry in WarCries)
            if (string.Equals(cry, value ?? "", StringComparison.OrdinalIgnoreCase))
                return cry;
        Warn($"{field}='{value}' is not one of Yell, Charge, Victory, Grunt or empty; silenced", ref rejected);
        return "";
    }

    private List<RaceAbilityTrigger> ValidateTriggers(List<RaceAbilityTrigger>? parsed, string field, ref bool rejected)
    {
        var triggers = new List<RaceAbilityTrigger>();
        if (parsed == null)
            return triggers;
        for (var i = 0; i < parsed.Count; i++)
        {
            var trigger = ValidateTrigger(parsed[i], $"{field}[{i}]", ref rejected);
            if (trigger != null)
                triggers.Add(trigger);
        }
        return triggers;
    }

    private RaceAbilityTrigger? ValidateTrigger(RaceAbilityTrigger? parsed, string field, ref bool rejected)
    {
        if (parsed == null)
        {
            Warn($"{field} is null, skipped", ref rejected);
            return null;
        }
        if (!TryParseKind(parsed.Kind, out var kind))
        {
            Warn($"{field}.kind='{parsed.Kind}' is not a known trigger kind, skipped", ref rejected);
            return null;
        }

        var trigger = new RaceAbilityTrigger
        {
            Kind = kind.ToString(),
            ParsedKind = kind,
            Range = parsed.Range,
            Seconds = parsed.Seconds,
            Fraction = parsed.Fraction,
            Count = parsed.Count,
        };
        var valid = true;
        if (ReadsRange(kind) && !(FiniteFloatValidator.IsFiniteAtMost(parsed.Range, MaxRange) && parsed.Range > 0f))
            valid = SkipTrigger($"{field}.range={parsed.Range} must be a finite value in (0,{MaxRange}]", ref rejected);
        if (ReadsSeconds(kind) && !(FiniteFloatValidator.IsFiniteAtMost(parsed.Seconds, MaxEventSeconds) && parsed.Seconds > 0f))
            valid = SkipTrigger($"{field}.seconds={parsed.Seconds} must be a finite value in (0,{MaxEventSeconds}]", ref rejected);
        if (ReadsFraction(kind) && !(FiniteFloatValidator.IsFiniteAtMost(parsed.Fraction, 1f) && parsed.Fraction > 0f))
            valid = SkipTrigger($"{field}.fraction={parsed.Fraction} must be a finite value in (0,1]", ref rejected);
        if (ReadsCount(kind) && (parsed.Count < 1 || parsed.Count > 50))
            valid = SkipTrigger($"{field}.count={parsed.Count} must be in [1,50]", ref rejected);
        return valid ? trigger : null;
    }

    // One kind's exact name, any case. Enum.TryParse would also take a number, or a comma list whose values
    // it ORs together into some other kind ("EnemyWithin,CavalryClosing" reads as RangedTargetWithin).
    private static bool TryParseKind(string? value, out RaceAbilityTriggerKind kind)
    {
        var name = value?.Trim();
        foreach (RaceAbilityTriggerKind candidate in Enum.GetValues(typeof(RaceAbilityTriggerKind)))
        {
            if (string.Equals(candidate.ToString(), name, StringComparison.OrdinalIgnoreCase))
            {
                kind = candidate;
                return true;
            }
        }
        kind = default;
        return false;
    }

    private static bool ReadsRange(RaceAbilityTriggerKind kind) => kind switch
    {
        RaceAbilityTriggerKind.EnemyWithin => true,
        RaceAbilityTriggerKind.EnemiesWithin => true,
        RaceAbilityTriggerKind.KinFell => true,
        RaceAbilityTriggerKind.WoundedEnemyWithin => true,
        RaceAbilityTriggerKind.CavalryClosing => true,
        RaceAbilityTriggerKind.RangedTargetWithin => true,
        RaceAbilityTriggerKind.KinWithin => true,
        RaceAbilityTriggerKind.NoEnemyWithin => true,
        _ => false,
    };

    private static bool ReadsSeconds(RaceAbilityTriggerKind kind) =>
        kind == RaceAbilityTriggerKind.KinFell || kind == RaceAbilityTriggerKind.LandedKill;

    private static bool ReadsFraction(RaceAbilityTriggerKind kind) =>
        kind == RaceAbilityTriggerKind.HealthBelow || kind == RaceAbilityTriggerKind.WoundedEnemyWithin
        || kind == RaceAbilityTriggerKind.MoraleBelow;

    private static bool ReadsCount(RaceAbilityTriggerKind kind) =>
        kind == RaceAbilityTriggerKind.EnemiesWithin || kind == RaceAbilityTriggerKind.KinWithin;

    private RaceAbilityEffects ValidateEffects(RaceAbilityEffects? parsed, string field, ref bool rejected)
    {
        if (parsed == null)
            return new RaceAbilityEffects();

        // -95% is the floor for a multiplier so no stat reaches zero or flips sign.
        return new RaceAbilityEffects
        {
            MoveSpeedPercent = Check(parsed.MoveSpeedPercent, -95f, 300f, 0f, $"{field}.moveSpeedPercent", ref rejected),
            AccelerationPercent = Check(parsed.AccelerationPercent, -95f, 300f, 0f, $"{field}.accelerationPercent", ref rejected),
            SwingSpeedPercent = Check(parsed.SwingSpeedPercent, -95f, 300f, 0f, $"{field}.swingSpeedPercent", ref rejected),
            DrawSpeedPercent = Check(parsed.DrawSpeedPercent, -95f, 300f, 0f, $"{field}.drawSpeedPercent", ref rejected),
            ReloadSpeedPercent = Check(parsed.ReloadSpeedPercent, -95f, 300f, 0f, $"{field}.reloadSpeedPercent", ref rejected),
            MissileSpeedPercent = Check(parsed.MissileSpeedPercent, -95f, 300f, 0f, $"{field}.missileSpeedPercent", ref rejected),
            MountSpeedPercent = Check(parsed.MountSpeedPercent, -95f, 300f, 0f, $"{field}.mountSpeedPercent", ref rejected),
            MeleeDamagePercent = Check(parsed.MeleeDamagePercent, -95f, 300f, 0f, $"{field}.meleeDamagePercent", ref rejected),
            RangedDamagePercent = Check(parsed.RangedDamagePercent, -95f, 300f, 0f, $"{field}.rangedDamagePercent", ref rejected),
            DamageReductionPercent = Check(parsed.DamageReductionPercent, 0f, 90f, 0f, $"{field}.damageReductionPercent", ref rejected),
            KnockdownResistancePercent = Check(parsed.KnockdownResistancePercent, -95f, 1000f, 0f, $"{field}.knockdownResistancePercent", ref rejected),
            KnockbackResistancePercent = Check(parsed.KnockbackResistancePercent, -95f, 1000f, 0f, $"{field}.knockbackResistancePercent", ref rejected),
            DismountResistancePercent = Check(parsed.DismountResistancePercent, -95f, 1000f, 0f, $"{field}.dismountResistancePercent", ref rejected),
            BlockAbilityPercent = Check(parsed.BlockAbilityPercent, -95f, 300f, 0f, $"{field}.blockAbilityPercent", ref rejected),
            ParryAbilityPercent = Check(parsed.ParryAbilityPercent, -95f, 300f, 0f, $"{field}.parryAbilityPercent", ref rejected),
            AttackEagernessPercent = Check(parsed.AttackEagernessPercent, -95f, 300f, 0f, $"{field}.attackEagernessPercent", ref rejected),
            AimErrorPercent = Check(parsed.AimErrorPercent, -95f, 300f, 0f, $"{field}.aimErrorPercent", ref rejected),
            ForceCrushThrough = parsed.ForceCrushThrough,
            HoldAgainstCrush = parsed.HoldAgainstCrush,
            ShrugOffBlows = parsed.ShrugOffBlows,
            MoraleFloor = Check(parsed.MoraleFloor, 0f, 100f, 0f, $"{field}.moraleFloor", ref rejected),
            MoraleOnEnd = Check(parsed.MoraleOnEnd, -100f, 100f, 0f, $"{field}.moraleOnEnd", ref rejected),
            HealPerKill = Check(parsed.HealPerKill, 0f, 100f, 0f, $"{field}.healPerKill", ref rejected),
            FearOnKillRadius = Check(parsed.FearOnKillRadius, 0f, 30f, 0f, $"{field}.fearOnKillRadius", ref rejected),
            FearOnKillMorale = Check(parsed.FearOnKillMorale, 0f, 100f, 0f, $"{field}.fearOnKillMorale", ref rejected),
            FearAuraRadius = Check(parsed.FearAuraRadius, 0f, 30f, 0f, $"{field}.fearAuraRadius", ref rejected),
            FearAuraMoralePerSecond = Check(parsed.FearAuraMoralePerSecond, 0f, 50f, 0f, $"{field}.fearAuraMoralePerSecond", ref rejected),
        };
    }

    private float Check(float value, float min, float max, float fallback, string field, ref bool rejected)
    {
        if (FiniteFloatValidator.IsFiniteInRange(value, min, max))
            return value;
        Warn($"{field}={value} must be a finite value in [{min},{max}], reverting to {fallback}", ref rejected);
        return fallback;
    }

    // Exclusive zero: a factor of 0 would erase the ability.
    private float CheckAboveZero(float value, float max, float fallback, string field, ref bool rejected)
    {
        if (FiniteFloatValidator.IsFiniteAtMost(value, max) && value > 0f)
            return value;
        Warn($"{field}={value} must be a finite value in (0,{max}], reverting to {fallback}", ref rejected);
        return fallback;
    }

    private int Revert(int value, int fallback, string field, string range, ref bool rejected)
    {
        Warn($"{field}={value} outside {range}, reverting to {fallback}", ref rejected);
        return fallback;
    }

    private bool SkipTrigger(string message, ref bool rejected)
    {
        Warn(message + ", trigger skipped", ref rejected);
        return false;
    }

    private void Warn(string message, ref bool rejected)
    {
        _logger.LogWarning("RaceAbilitiesConfigProvider: " + message);
        rejected = true;
    }
}
