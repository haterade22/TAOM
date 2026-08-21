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

        // Radii are bounded rather than merely finite: a zero or negative radius would make every
        // target out of reach, and a radius past vanilla's own 5-gap saturation is a silent no-op.
        if (!FiniteFloatValidator.IsFiniteInRange(parsed.ReachRadiusInTownGaps, 1.0f, 20.0f))
        {
            _logger.LogWarning($"ArmyTargetingConfigProvider: ReachRadiusInTownGaps={parsed.ReachRadiusInTownGaps} must be finite in [1,20], reverting to {defaults.ReachRadiusInTownGaps}");
            parsed.ReachRadiusInTownGaps = defaults.ReachRadiusInTownGaps;
            rejected = true;
        }

        if (!FiniteFloatValidator.IsFiniteInRange(parsed.ReachInnerRadiusInTownGaps, 0f, 20.0f))
        {
            _logger.LogWarning($"ArmyTargetingConfigProvider: ReachInnerRadiusInTownGaps={parsed.ReachInnerRadiusInTownGaps} must be finite in [0,20], reverting to {defaults.ReachInnerRadiusInTownGaps}");
            parsed.ReachInnerRadiusInTownGaps = defaults.ReachInnerRadiusInTownGaps;
            rejected = true;
        }

        // Ordering invariant. The service also derives the inner radius from the resolved outer one
        // so MCM cannot invert it, but a JSON file that already reads wrong should say so here.
        if (parsed.ReachInnerRadiusInTownGaps >= parsed.ReachRadiusInTownGaps)
        {
            _logger.LogWarning($"ArmyTargetingConfigProvider: ReachInnerRadiusInTownGaps={parsed.ReachInnerRadiusInTownGaps} must be below ReachRadiusInTownGaps={parsed.ReachRadiusInTownGaps}, reverting the inner radius to {defaults.ReachInnerRadiusInTownGaps}");
            parsed.ReachInnerRadiusInTownGaps = defaults.ReachInnerRadiusInTownGaps;
            rejected = true;
        }

        // Never zero: a hard zero removes a faction's whole option set on a bad day, and vanilla
        // then disbands the army for inactivity two days later.
        if (!FiniteFloatValidator.IsFiniteInRange(parsed.ReachFloor, 0.001f, 1.0f))
        {
            _logger.LogWarning($"ArmyTargetingConfigProvider: ReachFloor={parsed.ReachFloor} must be finite in [0.001,1], reverting to {defaults.ReachFloor}");
            parsed.ReachFloor = defaults.ReachFloor;
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
