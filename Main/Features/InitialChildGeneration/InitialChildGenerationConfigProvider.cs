using System;
using System.IO;
using Newtonsoft.Json.Linq;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.InitialChildGeneration.Config;

namespace TAOM.Features.InitialChildGeneration;

public class InitialChildGenerationConfigProvider : IInitialChildGenerationConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private InitialChildGenerationConfig _cached;

    public InitialChildGenerationConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public InitialChildGenerationConfig LoadConfig()
    {
        if (_cached != null)
            return _cached;

        var path = Path.Combine(_pathService.ConfigPath, "initial_child_generation.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"InitialChildGenerationConfigProvider: config not found: {path}");
            _cached = new InitialChildGenerationConfig();
            return _cached;
        }

        try
        {
            var json = File.ReadAllText(path);
            var obj = JObject.Parse(json);
            _cached = ParseConfig(obj);
            _logger.LogInfo($"Loaded initial child generation config from {path}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"InitialChildGenerationConfigProvider: Failed to parse config: {ex.Message}");
            _cached = new InitialChildGenerationConfig();
        }

        return _cached;
    }

    private InitialChildGenerationConfig ParseConfig(JObject obj)
    {
        var config = new InitialChildGenerationConfig();

        if (obj["defaults"] is JObject defaults)
        {
            var minAge = defaults.Value<int?>("min_age") ?? config.Defaults.MinAge;
            var maxAge = defaults.Value<int?>("max_age") ?? config.Defaults.MaxAge;
            ValidateAgeOrdering(ref minAge, ref maxAge, "defaults");
            config.Defaults.MinAge = minAge;
            config.Defaults.MaxAge = maxAge;
            config.Defaults.FemaleRatio = ValidateRatio(defaults.Value<double?>("female_ratio"), config.Defaults.FemaleRatio, "defaults.female_ratio");
            config.Defaults.ChildCountMultiplier = ValidateMultiplier(defaults.Value<double?>("child_count_multiplier"), config.Defaults.ChildCountMultiplier, "defaults.child_count_multiplier");
        }

        config.ExcludedCultures = obj["excluded_cultures"]?.ToObject<System.Collections.Generic.List<string>>()
                                  ?? config.ExcludedCultures;
        config.ExcludedClans = obj["excluded_clans"]?.ToObject<System.Collections.Generic.List<string>>()
                               ?? config.ExcludedClans;

        if (obj["culture_overrides"] is JArray cultureArray)
        {
            foreach (var token in cultureArray)
            {
                if (token is not JObject co) continue;
                var minAge = co.Value<int?>("min_age");
                var maxAge = co.Value<int?>("max_age");
                if (minAge.HasValue && maxAge.HasValue)
                {
                    var minA = minAge.Value; var maxA = maxAge.Value;
                    ValidateAgeOrdering(ref minA, ref maxA, $"culture_overrides[{co.Value<string>("culture_id")}]");
                    minAge = minA; maxAge = maxA;
                }
                config.CultureOverrides.Add(new CultureOverride
                {
                    CultureId = co.Value<string>("culture_id"),
                    MinAge = minAge,
                    MaxAge = maxAge,
                    FemaleRatio = ValidateRatio(co.Value<double?>("female_ratio"), null, $"culture_overrides[{co.Value<string>("culture_id")}].female_ratio"),
                    ChildCountMultiplier = ValidateMultiplier(co.Value<double?>("child_count_multiplier"), null, $"culture_overrides[{co.Value<string>("culture_id")}].child_count_multiplier"),
                });
            }
        }

        if (obj["clan_overrides"] is JArray clanArray)
        {
            foreach (var token in clanArray)
            {
                if (token is not JObject co) continue;
                var minAge = co.Value<int?>("min_age");
                var maxAge = co.Value<int?>("max_age");
                if (minAge.HasValue && maxAge.HasValue)
                {
                    var minA = minAge.Value; var maxA = maxAge.Value;
                    ValidateAgeOrdering(ref minA, ref maxA, $"clan_overrides[{co.Value<string>("clan_id")}]");
                    minAge = minA; maxAge = maxA;
                }
                config.ClanOverrides.Add(new ClanOverride
                {
                    ClanId = co.Value<string>("clan_id"),
                    MinAge = minAge,
                    MaxAge = maxAge,
                    FemaleRatio = ValidateRatio(co.Value<double?>("female_ratio"), null, $"clan_overrides[{co.Value<string>("clan_id")}].female_ratio"),
                    ChildCountMultiplier = ValidateMultiplier(co.Value<double?>("child_count_multiplier"), null, $"clan_overrides[{co.Value<string>("clan_id")}].child_count_multiplier"),
                    FixedChildCount = co.Value<int?>("fixed_child_count"),
                });
            }
        }

        return config;
    }

    // Phase 9b #126 P1 — validate FemaleRatio (must be finite in [0, 1]). NaN propagates through
    // `_random.NextDouble() < NaN` as `false` so all children would be male; Infinity has the same
    // bias toward false. Out-of-range positive values clamp toward all-female. Revert to default
    // (or compiled fallback) on any invalid input.
    private double ValidateRatio(double? value, double? fallback, string fieldPath)
    {
        if (!value.HasValue) return fallback ?? 0.5; // legacy default
        if (!FiniteFloatValidator.IsFiniteInRange(value.Value, 0.0, 1.0))
        {
            _logger.LogWarning($"InitialChildGeneration: {fieldPath}={value.Value} invalid (NaN/Inf/out-of-range [0,1]) — using fallback {fallback ?? 0.5}");
            return fallback ?? 0.5;
        }
        return value.Value;
    }

    // Phase 9b #126 P1 — validate ChildCountMultiplier (must be finite ≥ 0). Negative or NaN/Inf
    // would push child counts to nonsense via `Math.Ceiling(baseCount * X) -> (int)`. Revert on
    // invalid.
    private double ValidateMultiplier(double? value, double? fallback, string fieldPath)
    {
        if (!value.HasValue) return fallback ?? 1.0;
        if (!FiniteFloatValidator.IsFiniteAtLeast(value.Value, 0.0))
        {
            _logger.LogWarning($"InitialChildGeneration: {fieldPath}={value.Value} invalid (NaN/Inf/negative) — using fallback {fallback ?? 1.0}");
            return fallback ?? 1.0;
        }
        return value.Value;
    }

    // Phase 9b #126 P2 — enforce MinAge <= MaxAge. `Random.Next(min, max)` throws if min > max,
    // aborting generation for all clans on first error. Swap on inversion + log.
    private void ValidateAgeOrdering(ref int minAge, ref int maxAge, string scope)
    {
        if (minAge > maxAge)
        {
            _logger.LogWarning($"InitialChildGeneration: {scope} min_age={minAge} > max_age={maxAge} — swapping");
            (minAge, maxAge) = (maxAge, minAge);
        }
    }
}
