using System;
using System.IO;
using Newtonsoft.Json.Linq;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
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

    private static InitialChildGenerationConfig ParseConfig(JObject obj)
    {
        var config = new InitialChildGenerationConfig();

        if (obj["defaults"] is JObject defaults)
        {
            config.Defaults.MinAge = defaults.Value<int?>("min_age") ?? config.Defaults.MinAge;
            config.Defaults.MaxAge = defaults.Value<int?>("max_age") ?? config.Defaults.MaxAge;
            config.Defaults.FemaleRatio = defaults.Value<double?>("female_ratio") ?? config.Defaults.FemaleRatio;
            config.Defaults.ChildCountMultiplier = defaults.Value<double?>("child_count_multiplier") ?? config.Defaults.ChildCountMultiplier;
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
                config.CultureOverrides.Add(new CultureOverride
                {
                    CultureId = co.Value<string>("culture_id"),
                    MinAge = co.Value<int?>("min_age"),
                    MaxAge = co.Value<int?>("max_age"),
                    FemaleRatio = co.Value<double?>("female_ratio"),
                    ChildCountMultiplier = co.Value<double?>("child_count_multiplier"),
                });
            }
        }

        if (obj["clan_overrides"] is JArray clanArray)
        {
            foreach (var token in clanArray)
            {
                if (token is not JObject co) continue;
                config.ClanOverrides.Add(new ClanOverride
                {
                    ClanId = co.Value<string>("clan_id"),
                    MinAge = co.Value<int?>("min_age"),
                    MaxAge = co.Value<int?>("max_age"),
                    FemaleRatio = co.Value<double?>("female_ratio"),
                    ChildCountMultiplier = co.Value<double?>("child_count_multiplier"),
                    FixedChildCount = co.Value<int?>("fixed_child_count"),
                });
            }
        }

        return config;
    }
}
