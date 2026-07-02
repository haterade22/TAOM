using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TaleWorlds.Core;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.CombatMechanics.Domain;

namespace TAOM.Features.CombatMechanics;

// Validating boundary loader for combat_mechanics_config.json (SettlementFoodConfigProvider
// pattern): missing file → defaults + warn; parse failure → defaults + error; parseable-but-
// invalid values revert per-field to the compiled default + warn (csharp-architecture.md
// "Config Providers MUST Validate"). The single TaleWorlds import (WeaponClass) is deliberate:
// this is a boundary loader validating enum names at load time, not a service (ADR-007 applies
// to services; the hot-path services stay TaleWorlds-free).
public class CombatMechanicsConfigProvider : ICombatMechanicsConfigProvider
{
    // Replace, not append-merge: Json.NET's default ObjectCreationHandling appends JSON list/dict
    // entries onto the constructor-populated defaults (a single cleaveMonsterIds entry would
    // become 4). Replace makes a JSON collection REPLACE the compiled default.
    private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
    {
        ObjectCreationHandling = ObjectCreationHandling.Replace,
    };

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<CombatMechanicsConfig> _config;

    public CombatMechanicsConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<CombatMechanicsConfig>(LoadConfig);
    }

    public CombatMechanicsConfig GetConfig() => _config.Value;

    private CombatMechanicsConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "combat_mechanics", "combat_mechanics_config.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"CombatMechanicsConfigProvider: combat_mechanics_config.json not found at {path}, using defaults");
            return new CombatMechanicsConfig();
        }

        CombatMechanicsConfig parsed;
        try
        {
            var json = File.ReadAllText(path);
            parsed = JsonConvert.DeserializeObject<CombatMechanicsConfig>(json, SerializerSettings) ?? new CombatMechanicsConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"CombatMechanicsConfigProvider: Failed to parse combat_mechanics_config.json: {ex.Message}");
            return new CombatMechanicsConfig();
        }

        return Validate(parsed);
    }

    private CombatMechanicsConfig Validate(CombatMechanicsConfig parsed)
    {
        var rejected = false;
        var sanitized = new CombatMechanicsConfig
        {
            Enabled = parsed.Enabled,
            CrushThrough = ValidateCrushThrough(parsed.CrushThrough, ref rejected),
            ChargeKnockdown = ValidateChargeKnockdown(parsed.ChargeKnockdown, ref rejected),
            Creatures = ValidateCreatures(parsed.Creatures, ref rejected),
            ShieldPenetration = ValidateShieldPenetration(parsed.ShieldPenetration, ref rejected),
            RaceModifiers = ValidateRaceModifiers(parsed.RaceModifiers, ref rejected),
        };

        if (rejected)
            _logger.LogWarning("CombatMechanicsConfigProvider: combat_mechanics_config.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo("CombatMechanicsConfigProvider: Loaded combat_mechanics_config.json");

        return sanitized;
    }

    private CrushThroughConfig ValidateCrushThrough(CrushThroughConfig parsed, ref bool rejected)
    {
        var defaults = new CrushThroughConfig();
        if (parsed == null)
        {
            _logger.LogWarning("CombatMechanicsConfigProvider: crushThrough section is null, reverting to defaults");
            rejected = true;
            return defaults;
        }

        var sanitized = new CrushThroughConfig
        {
            SkillBasedEnabled = parsed.SkillBasedEnabled,
            MonsterAutoCrushEnabled = parsed.MonsterAutoCrushEnabled,
            OrcShieldCrushEnabled = parsed.OrcShieldCrushEnabled,
            ExtraCrushThroughEnergyThreshold = CheckFiniteInRange(parsed.ExtraCrushThroughEnergyThreshold, 1f, 1000f, defaults.ExtraCrushThroughEnergyThreshold, "crushThrough.extraCrushThroughEnergyThreshold", ref rejected),
            SkillTargetDelta = CheckIntInRange(parsed.SkillTargetDelta, 1, 1000, defaults.SkillTargetDelta, "crushThrough.skillTargetDelta", ref rejected),
            SkillDeadZone = CheckIntInRange(parsed.SkillDeadZone, 0, 1000, defaults.SkillDeadZone, "crushThrough.skillDeadZone", ref rejected),
            MaxSkillChance = CheckFiniteInRange(parsed.MaxSkillChance, 0f, 1f, defaults.MaxSkillChance, "crushThrough.maxSkillChance", ref rejected),
            SkillCurveExponent = CheckFiniteAboveExclusiveMin(parsed.SkillCurveExponent, 0f, 0.1f, defaults.SkillCurveExponent, "crushThrough.skillCurveExponent", ref rejected),
            EnergyRampMarginFactor = CheckFiniteAboveExclusiveMin(parsed.EnergyRampMarginFactor, 0f, 5f, defaults.EnergyRampMarginFactor, "crushThrough.energyRampMarginFactor", ref rejected),
            NonOverheadPenaltyFactor = CheckFiniteInRange(parsed.NonOverheadPenaltyFactor, 0f, 1f, defaults.NonOverheadPenaltyFactor, "crushThrough.nonOverheadPenaltyFactor", ref rejected),
            MonsterCrushMonsterIds = CleanIdList(parsed.MonsterCrushMonsterIds, defaults.MonsterCrushMonsterIds, "crushThrough.monsterCrushMonsterIds", ref rejected),
            OrcShieldCrushRaces = CleanIdList(parsed.OrcShieldCrushRaces, defaults.OrcShieldCrushRaces, "crushThrough.orcShieldCrushRaces", ref rejected),
        };

        // Ordering invariant, checked AFTER individual ranges: the dead zone must sit strictly
        // below the target delta or the chance-curve denominator 1 − e^(−(targetDelta−deadZone)×exp)
        // degenerates to 0 (equality → division by zero).
        if (sanitized.SkillDeadZone >= sanitized.SkillTargetDelta)
        {
            _logger.LogWarning($"CombatMechanicsConfigProvider: crushThrough.skillDeadZone={sanitized.SkillDeadZone} must be below skillTargetDelta={sanitized.SkillTargetDelta}, reverting skillDeadZone to default {defaults.SkillDeadZone}");
            sanitized.SkillDeadZone = defaults.SkillDeadZone;
            rejected = true;

            if (sanitized.SkillDeadZone >= sanitized.SkillTargetDelta)
            {
                _logger.LogWarning($"CombatMechanicsConfigProvider: default skillDeadZone={sanitized.SkillDeadZone} still violates skillTargetDelta={sanitized.SkillTargetDelta}, reverting skillTargetDelta to default {defaults.SkillTargetDelta}");
                sanitized.SkillTargetDelta = defaults.SkillTargetDelta;
            }
        }

        return sanitized;
    }

    private ChargeKnockdownConfig ValidateChargeKnockdown(ChargeKnockdownConfig parsed, ref bool rejected)
    {
        var defaults = new ChargeKnockdownConfig();
        if (parsed == null)
        {
            _logger.LogWarning("CombatMechanicsConfigProvider: chargeKnockdown section is null, reverting to defaults");
            rejected = true;
            return defaults;
        }

        var sanitized = new ChargeKnockdownConfig
        {
            Enabled = parsed.Enabled,
            IncludeRiderWeight = parsed.IncludeRiderWeight,
            NeutralWeightRatio = CheckFiniteInRange(parsed.NeutralWeightRatio, 0.1f, 1000f, defaults.NeutralWeightRatio, "chargeKnockdown.neutralWeightRatio", ref rejected),
            AutoKnockdownWeightRatio = CheckFiniteInRange(parsed.AutoKnockdownWeightRatio, 0.1f, 1000f, defaults.AutoKnockdownWeightRatio, "chargeKnockdown.autoKnockdownWeightRatio", ref rejected),
            AutoKnockdownMinSpeedFactor = CheckFiniteInRange(parsed.AutoKnockdownMinSpeedFactor, 0f, 1f, defaults.AutoKnockdownMinSpeedFactor, "chargeKnockdown.autoKnockdownMinSpeedFactor", ref rejected),
            DefaultChargeSpeedReference = CheckFiniteInRange(parsed.DefaultChargeSpeedReference, 0.1f, 100f, defaults.DefaultChargeSpeedReference, "chargeKnockdown.defaultChargeSpeedReference", ref rejected),
            MinPenetrationFactor = CheckFiniteInRange(parsed.MinPenetrationFactor, 0f, 10f, defaults.MinPenetrationFactor, "chargeKnockdown.minPenetrationFactor", ref rejected),
            MaxPenetrationFactor = CheckFiniteInRange(parsed.MaxPenetrationFactor, 0f, 10f, defaults.MaxPenetrationFactor, "chargeKnockdown.maxPenetrationFactor", ref rejected),
            HorseChargePenetration = CheckFiniteInRange(parsed.HorseChargePenetration, 0f, 1f, defaults.HorseChargePenetration, "chargeKnockdown.horseChargePenetration", ref rejected),
        };

        // Ordering invariant: an auto-knockdown threshold below the neutral ratio would fire
        // Branch A (unconditional knockdown) on ordinary charges Branch B exists to keep at
        // vanilla parity.
        if (sanitized.AutoKnockdownWeightRatio < sanitized.NeutralWeightRatio)
        {
            _logger.LogWarning($"CombatMechanicsConfigProvider: chargeKnockdown.autoKnockdownWeightRatio={sanitized.AutoKnockdownWeightRatio} must be at least neutralWeightRatio={sanitized.NeutralWeightRatio}, reverting autoKnockdownWeightRatio to default {defaults.AutoKnockdownWeightRatio}");
            sanitized.AutoKnockdownWeightRatio = defaults.AutoKnockdownWeightRatio;
            rejected = true;

            if (sanitized.AutoKnockdownWeightRatio < sanitized.NeutralWeightRatio)
            {
                _logger.LogWarning($"CombatMechanicsConfigProvider: default autoKnockdownWeightRatio={sanitized.AutoKnockdownWeightRatio} still violates neutralWeightRatio={sanitized.NeutralWeightRatio}, reverting neutralWeightRatio to default {defaults.NeutralWeightRatio}");
                sanitized.NeutralWeightRatio = defaults.NeutralWeightRatio;
            }
        }

        if (sanitized.MinPenetrationFactor > sanitized.MaxPenetrationFactor)
        {
            _logger.LogWarning($"CombatMechanicsConfigProvider: chargeKnockdown.minPenetrationFactor={sanitized.MinPenetrationFactor} exceeds maxPenetrationFactor={sanitized.MaxPenetrationFactor}, reverting both to defaults {defaults.MinPenetrationFactor}/{defaults.MaxPenetrationFactor}");
            sanitized.MinPenetrationFactor = defaults.MinPenetrationFactor;
            sanitized.MaxPenetrationFactor = defaults.MaxPenetrationFactor;
            rejected = true;
        }

        return sanitized;
    }

    private CreatureCombatConfig ValidateCreatures(CreatureCombatConfig parsed, ref bool rejected)
    {
        var defaults = new CreatureCombatConfig();
        if (parsed == null)
        {
            _logger.LogWarning("CombatMechanicsConfigProvider: creatures section is null, reverting to defaults");
            rejected = true;
            return defaults;
        }

        return new CreatureCombatConfig
        {
            CleaveEnabled = parsed.CleaveEnabled,
            UnstoppableEnabled = parsed.UnstoppableEnabled,
            CleaveMonsterIds = CleanIdList(parsed.CleaveMonsterIds, defaults.CleaveMonsterIds, "creatures.cleaveMonsterIds", ref rejected),
            CleaveRemainingMomentumFactor = CheckFiniteAboveExclusiveMin(parsed.CleaveRemainingMomentumFactor, 0f, 1f, defaults.CleaveRemainingMomentumFactor, "creatures.cleaveRemainingMomentumFactor", ref rejected),
            UnstoppableDamageThresholds = ValidateUnstoppableThresholds(parsed.UnstoppableDamageThresholds, defaults.UnstoppableDamageThresholds, ref rejected),
        };
    }

    private Dictionary<string, int> ValidateUnstoppableThresholds(Dictionary<string, int> parsed, Dictionary<string, int> fallback, ref bool rejected)
    {
        if (parsed == null)
        {
            _logger.LogWarning("CombatMechanicsConfigProvider: creatures.unstoppableDamageThresholds is null, reverting to defaults");
            rejected = true;
            return new Dictionary<string, int>(fallback);
        }

        var cleaned = new Dictionary<string, int>(parsed.Count);
        foreach (var pair in parsed)
        {
            if (string.IsNullOrEmpty(pair.Key))
            {
                _logger.LogWarning("CombatMechanicsConfigProvider: creatures.unstoppableDamageThresholds contains a null/empty monster id — entry dropped");
                rejected = true;
                continue;
            }

            // Parsed-but-unresolvable (the M1 trap): an out-of-range threshold is REMOVED — the
            // creature simply has no unstoppable row — rather than clamped into silently
            // different mechanics.
            if (pair.Value < 0 || pair.Value > 500)
            {
                _logger.LogWarning($"CombatMechanicsConfigProvider: creatures.unstoppableDamageThresholds['{pair.Key}']={pair.Value} outside [0,500] — entry removed");
                rejected = true;
                continue;
            }

            cleaned[pair.Key] = pair.Value;
        }

        return cleaned;
    }

    private ShieldPenetrationConfig ValidateShieldPenetration(ShieldPenetrationConfig parsed, ref bool rejected)
    {
        var defaults = new ShieldPenetrationConfig();
        if (parsed == null)
        {
            _logger.LogWarning("CombatMechanicsConfigProvider: shieldPenetration section is null, reverting to defaults");
            rejected = true;
            return defaults;
        }

        return new ShieldPenetrationConfig
        {
            Enabled = parsed.Enabled,
            AddMultiplePenetration = parsed.AddMultiplePenetration,
            RuntimeShieldDamageCorrectionEnabled = parsed.RuntimeShieldDamageCorrectionEnabled,
            WeaponClasses = ValidateWeaponClasses(parsed.WeaponClasses, defaults.WeaponClasses, ref rejected),
            ItemIds = CleanIdList(parsed.ItemIds, defaults.ItemIds, "shieldPenetration.itemIds", ref rejected),
            RuntimeShieldDamageCorrectionDivisor = CheckFiniteAboveExclusiveMin(parsed.RuntimeShieldDamageCorrectionDivisor, 0f, 1f, defaults.RuntimeShieldDamageCorrectionDivisor, "shieldPenetration.runtimeShieldDamageCorrectionDivisor", ref rejected),
        };
    }

    private List<string> ValidateWeaponClasses(List<string> parsed, List<string> fallback, ref bool rejected)
    {
        if (parsed == null)
        {
            _logger.LogWarning("CombatMechanicsConfigProvider: shieldPenetration.weaponClasses is null, reverting to defaults");
            rejected = true;
            return new List<string>(fallback);
        }

        var cleaned = new List<string>(parsed.Count);
        foreach (var name in parsed)
        {
            // The M1 unknown-string trap: a typo'd class name would silently match nothing in the
            // consumer. Case-sensitive by design (enum member casing is the documented contract);
            // Enum.IsDefined rejects the numeric strings TryParse would otherwise let through.
            if (string.IsNullOrEmpty(name)
                || !Enum.TryParse<WeaponClass>(name, ignoreCase: false, out var weaponClass)
                || !Enum.IsDefined(typeof(WeaponClass), weaponClass))
            {
                _logger.LogWarning($"CombatMechanicsConfigProvider: shieldPenetration.weaponClasses entry '{name}' is not a WeaponClass member — entry removed");
                rejected = true;
                continue;
            }

            cleaned.Add(name);
        }

        return cleaned;
    }

    private Dictionary<string, RaceCombatModifiers> ValidateRaceModifiers(Dictionary<string, RaceCombatModifiers> parsed, ref bool rejected)
    {
        if (parsed == null)
        {
            _logger.LogWarning("CombatMechanicsConfigProvider: raceModifiers section is null, reverting to defaults");
            rejected = true;
            return new CombatMechanicsConfig().RaceModifiers;
        }

        // Race-NAME key validity is deliberately NOT checked here — the race registry is engine
        // state unavailable at load time; RaceCombatModifiersResolver validates keys lazily on
        // first resolve. Row VALUES are fully validated now.
        var rowDefaults = new RaceCombatModifiers();
        var cleaned = new Dictionary<string, RaceCombatModifiers>(parsed.Count);
        foreach (var pair in parsed)
        {
            if (pair.Value == null)
            {
                _logger.LogWarning($"CombatMechanicsConfigProvider: raceModifiers['{pair.Key}'] is null, reverting to neutral modifiers");
                rejected = true;
                cleaned[pair.Key] = new RaceCombatModifiers();
                continue;
            }

            // Negative "bonuses" are sign violations (the field's meaning is directional) — the
            // lower bound 0 rejects them alongside NaN/Infinity.
            cleaned[pair.Key] = new RaceCombatModifiers
            {
                CtbAttackBonus = CheckFiniteInRange(pair.Value.CtbAttackBonus, 0f, 300f, rowDefaults.CtbAttackBonus, $"raceModifiers['{pair.Key}'].ctbAttackBonus", ref rejected),
                CtbDefenseBonus = CheckFiniteInRange(pair.Value.CtbDefenseBonus, 0f, 300f, rowDefaults.CtbDefenseBonus, $"raceModifiers['{pair.Key}'].ctbDefenseBonus", ref rejected),
                KnockdownResistanceMultiplier = CheckFiniteInRange(pair.Value.KnockdownResistanceMultiplier, 0f, 100f, rowDefaults.KnockdownResistanceMultiplier, $"raceModifiers['{pair.Key}'].knockdownResistanceMultiplier", ref rejected),
                StaggerThresholdMultiplier = CheckFiniteInRange(pair.Value.StaggerThresholdMultiplier, 0f, 100f, rowDefaults.StaggerThresholdMultiplier, $"raceModifiers['{pair.Key}'].staggerThresholdMultiplier", ref rejected),
                SwingEnergyBonusFactor = CheckFiniteInRange(pair.Value.SwingEnergyBonusFactor, 0f, 10f, rowDefaults.SwingEnergyBonusFactor, $"raceModifiers['{pair.Key}'].swingEnergyBonusFactor", ref rejected),
                RemoveNonOverheadPenalty = pair.Value.RemoveNonOverheadPenalty,
            };
        }

        return cleaned;
    }

    private List<string> CleanIdList(List<string> parsed, List<string> fallback, string field, ref bool rejected)
    {
        if (parsed == null)
        {
            _logger.LogWarning($"CombatMechanicsConfigProvider: {field} is null, reverting to default list");
            rejected = true;
            return new List<string>(fallback);
        }

        var cleaned = new List<string>(parsed.Count);
        foreach (var id in parsed)
        {
            if (string.IsNullOrEmpty(id))
            {
                _logger.LogWarning($"CombatMechanicsConfigProvider: {field} contains a null/empty entry — entry dropped");
                rejected = true;
                continue;
            }

            cleaned.Add(id);
        }

        return cleaned;
    }

    // FiniteFloatValidator fires BEFORE any range logic (csharp-architecture.md: NaN passes naive
    // `< min || > max` checks); both bounds inclusive.
    private float CheckFiniteInRange(float value, float min, float max, float fallback, string field, ref bool rejected)
    {
        if (FiniteFloatValidator.IsFiniteInRange(value, min, max))
            return value;

        _logger.LogWarning($"CombatMechanicsConfigProvider: {field}={value} must be a finite value in [{min},{max}], reverting to default {fallback}");
        rejected = true;
        return fallback;
    }

    // Exclusive lower bound — for divisors/exponents/factors where the boundary value itself
    // poisons the math (÷0, degenerate curve).
    private float CheckFiniteAboveExclusiveMin(float value, float minExclusive, float max, float fallback, string field, ref bool rejected)
    {
        if (FiniteFloatValidator.IsFinite(value) && value > minExclusive && value <= max)
            return value;

        _logger.LogWarning($"CombatMechanicsConfigProvider: {field}={value} must be a finite value in ({minExclusive},{max}], reverting to default {fallback}");
        rejected = true;
        return fallback;
    }

    private int CheckIntInRange(int value, int min, int max, int fallback, string field, ref bool rejected)
    {
        if (value >= min && value <= max)
            return value;

        _logger.LogWarning($"CombatMechanicsConfigProvider: {field}={value} outside [{min},{max}], reverting to default {fallback}");
        rejected = true;
        return fallback;
    }
}
