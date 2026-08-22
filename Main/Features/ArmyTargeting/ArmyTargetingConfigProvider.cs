using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.ArmyTargeting;

public class ArmyTargetingConfigProvider : IArmyTargetingConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<ArmyTargetingConfig> _config;

    public ArmyTargetingConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<ArmyTargetingConfig>(LoadConfig);
    }

    public ArmyTargetingConfig GetConfig() => _config.Value;

    private ArmyTargetingConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "configs", "army_targeting.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"ArmyTargetingConfigProvider: army_targeting.json not found at {path}, using defaults");
            return new ArmyTargetingConfig();
        }

        ArmyTargetingConfig parsed;
        try
        {
            var json = File.ReadAllText(path);
            parsed = JsonConvert.DeserializeObject<ArmyTargetingConfig>(json) ?? new ArmyTargetingConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"ArmyTargetingConfigProvider: Failed to parse army_targeting.json: {ex.Message}");
            return new ArmyTargetingConfig();
        }

        return Validate(parsed);
    }

    /// <summary>
    /// Parse success is not validation success. This file drives AI target selection, so a
    /// sign-flipped weight or a mistyped theater name changes campaign behaviour with no error.
    /// Every rejection reverts to the compiled default and warns; a summary warning fires once so
    /// the earlier lines get looked at.
    /// </summary>
    internal ArmyTargetingConfig Validate(ArmyTargetingConfig parsed)
    {
        var defaults = new ArmyTargetingConfig();
        var rejected = false;

        parsed.Theaters ??= new List<string>();
        parsed.KingdomTheaters ??= new Dictionary<string, List<string>>();
        parsed.FactionPriorityTargets ??= new Dictionary<string, List<string>>();
        parsed.FactionAggressionMultipliers ??= new Dictionary<string, float>();

        // A theater name that is not declared would otherwise become a private theater of one:
        // the owning kingdom would match nobody on it, silently losing a front to a typo.
        var declared = new HashSet<string>(parsed.Theaters, StringComparer.Ordinal);
        foreach (var key in new List<string>(parsed.KingdomTheaters.Keys))
        {
            var names = parsed.KingdomTheaters[key];
            if (names == null)
            {
                parsed.KingdomTheaters[key] = new List<string>();
                continue;
            }

            var kept = new List<string>(names.Count);
            foreach (var name in names)
            {
                if (name != null && declared.Contains(name))
                {
                    kept.Add(name);
                }
                else
                {
                    _logger.LogWarning($"ArmyTargetingConfigProvider: kingdom '{key}' lists undeclared theater '{name}', skipping it. Declared theaters: {string.Join(", ", parsed.Theaters)}");
                    rejected = true;
                }
            }
            parsed.KingdomTheaters[key] = kept;
        }

        // Priority target ids are dictionary KEYS and ordinal indices downstream, so a null id
        // throws while the model is being registered and a duplicate makes the boost curve go
        // negative (the surviving index keeps climbing while the deduped Count does not). The
        // service builds its index defensively too; this is the surface that can warn about it.
        foreach (var faction in new List<string>(parsed.FactionPriorityTargets.Keys))
        {
            var targets = parsed.FactionPriorityTargets[faction];
            if (targets == null)
            {
                parsed.FactionPriorityTargets[faction] = new List<string>();
                continue;
            }

            var kept = new List<string>(targets.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var target in targets)
            {
                if (string.IsNullOrWhiteSpace(target))
                {
                    _logger.LogWarning($"ArmyTargetingConfigProvider: faction '{faction}' has a null or blank priority target, skipping it");
                    rejected = true;
                    continue;
                }
                if (!seen.Add(target))
                {
                    _logger.LogWarning($"ArmyTargetingConfigProvider: faction '{faction}' lists priority target '{target}' more than once, keeping the first position only");
                    rejected = true;
                    continue;
                }
                kept.Add(target);
            }
            parsed.FactionPriorityTargets[faction] = kept;
        }

        // The rescue radius bounds ONE thing: how far Patch22 may reach when overturning vanilla's
        // unreachable verdict. It is deliberately its own value and not shared with anything else,
        // because an earlier version reused a score-side falloff radius and widening that silently
        // widened this gate until it bounded nothing.
        if (!FiniteFloatValidator.IsFiniteInRange(parsed.BorderRescueRadiusInTownGaps, 1.0f, 20.0f))
        {
            _logger.LogWarning($"ArmyTargetingConfigProvider: BorderRescueRadiusInTownGaps={parsed.BorderRescueRadiusInTownGaps} must be finite in [1,20], reverting to {defaults.BorderRescueRadiusInTownGaps}");
            parsed.BorderRescueRadiusInTownGaps = defaults.BorderRescueRadiusInTownGaps;
            rejected = true;
        }

        // Aggression multipliers inflate ourStrength BEFORE vanilla's `ourStrength < defender * 2`
        // siege veto, so an infinity here defeats that veto for every fortress on the map. Json.NET
        // parses 1e39, "Infinity" and a bare Infinity token into float.PositiveInfinity.
        foreach (var faction in new List<string>(parsed.FactionAggressionMultipliers.Keys))
        {
            float value = parsed.FactionAggressionMultipliers[faction];
            if (FiniteFloatValidator.IsFiniteInRange(value, 1.0f, 100.0f)) continue;

            _logger.LogWarning($"ArmyTargetingConfigProvider: FactionAggressionMultipliers['{faction}']={value} must be finite in [1,100], dropping it so the faction falls back to neutral");
            parsed.FactionAggressionMultipliers.Remove(faction);
            rejected = true;
        }

        parsed.PrimaryTheaterWeight   = ValidateWeight("PrimaryTheaterWeight",   parsed.PrimaryTheaterWeight,   defaults.PrimaryTheaterWeight,   ref rejected);
        parsed.SecondaryTheaterWeight = ValidateWeight("SecondaryTheaterWeight", parsed.SecondaryTheaterWeight, defaults.SecondaryTheaterWeight, ref rejected);
        parsed.ForeignTheaterWeight   = ValidateWeight("ForeignTheaterWeight",   parsed.ForeignTheaterWeight,   defaults.ForeignTheaterWeight,   ref rejected);

        // Ordering invariant: a foreign front must never outrank the attacker's own primary one.
        // Sign-flipping these three is the plausible hand edit that would quietly invert the whole
        // feature into "prefer the far war", which is the behaviour it exists to remove.
        if (parsed.ForeignTheaterWeight > parsed.SecondaryTheaterWeight || parsed.SecondaryTheaterWeight > parsed.PrimaryTheaterWeight)
        {
            _logger.LogWarning($"ArmyTargetingConfigProvider: theater weights must be ordered foreign({parsed.ForeignTheaterWeight}) <= secondary({parsed.SecondaryTheaterWeight}) <= primary({parsed.PrimaryTheaterWeight}), reverting all three to defaults");
            parsed.PrimaryTheaterWeight = defaults.PrimaryTheaterWeight;
            parsed.SecondaryTheaterWeight = defaults.SecondaryTheaterWeight;
            parsed.ForeignTheaterWeight = defaults.ForeignTheaterWeight;
            rejected = true;
        }

        if (rejected)
            _logger.LogWarning("ArmyTargetingConfigProvider: army_targeting.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo($"ArmyTargetingConfigProvider: Loaded army_targeting.json ({parsed.KingdomTheaters.Count} kingdoms across {parsed.Theaters.Count} theaters)");

        return parsed;
    }

    private float ValidateWeight(string name, float value, float fallback, ref bool rejected)
    {
        // Weights are multipliers on a positive score. A negative weight would invert preference
        // (the AI would seek what it should avoid); zero would veto, which this design rejects.
        if (FiniteFloatValidator.IsFiniteInRange(value, 0.01f, 10.0f)) return value;

        _logger.LogWarning($"ArmyTargetingConfigProvider: {name}={value} must be finite in [0.01,10], reverting to {fallback}");
        rejected = true;
        return fallback;
    }
}
