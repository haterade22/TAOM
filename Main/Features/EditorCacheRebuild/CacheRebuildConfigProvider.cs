using System;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.EditorCacheRebuild;

public class CacheRebuildConfigProvider : ICacheRebuildConfigProvider
{
    private static readonly string[] ValidVerbosityLevels = { "error", "warn", "info", "debug" };

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly int _maxParallelism;
    private readonly Lazy<CacheRebuildConfig> _config;

    public CacheRebuildConfigProvider(IPathService pathService, IModLogger logger)
        : this(pathService, logger, Environment.ProcessorCount)
    {
    }

    internal CacheRebuildConfigProvider(IPathService pathService, IModLogger logger, int maxParallelism)
    {
        _pathService = pathService;
        _logger = logger;
        _maxParallelism = maxParallelism;
        _config = new Lazy<CacheRebuildConfig>(LoadConfig);
    }

    public CacheRebuildConfig GetConfig() => _config.Value;

    private CacheRebuildConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "configs", "cache_rebuild_config.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"CacheRebuildConfigProvider: cache_rebuild_config.json not found at {path}, using defaults");
            return new CacheRebuildConfig();
        }

        CacheRebuildConfig parsed;
        try
        {
            var json = File.ReadAllText(path);
            parsed = JsonConvert.DeserializeObject<CacheRebuildConfig>(json) ?? new CacheRebuildConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"CacheRebuildConfigProvider: Failed to parse cache_rebuild_config.json: {ex.Message}");
            return new CacheRebuildConfig();
        }

        return Validate(parsed);
    }

    private CacheRebuildConfig Validate(CacheRebuildConfig parsed)
    {
        var defaults = new CacheRebuildConfig();
        var rejected = false;

        if (parsed.Parallelism < 1 || parsed.Parallelism > _maxParallelism)
        {
            _logger.LogWarning($"CacheRebuildConfigProvider: parallelism={parsed.Parallelism} outside [1,{_maxParallelism}], reverting to default {defaults.Parallelism}");
            parsed.Parallelism = defaults.Parallelism;
            rejected = true;
        }

        if (parsed.CheckpointEvery < 1 || parsed.CheckpointEvery > 1000)
        {
            _logger.LogWarning($"CacheRebuildConfigProvider: checkpointEvery={parsed.CheckpointEvery} outside [1,1000], reverting to default {defaults.CheckpointEvery}");
            parsed.CheckpointEvery = defaults.CheckpointEvery;
            rejected = true;
        }

        if (parsed.IncrementalMaxChanged < 0 || parsed.IncrementalMaxChanged > 200)
        {
            _logger.LogWarning($"CacheRebuildConfigProvider: incrementalMaxChanged={parsed.IncrementalMaxChanged} outside [0,200], reverting to default {defaults.IncrementalMaxChanged}");
            parsed.IncrementalMaxChanged = defaults.IncrementalMaxChanged;
            rejected = true;
        }

        if (!FiniteFloatValidator.IsFiniteInRange(parsed.IncrementalSpatialRadius, 0.1f, 100.0f))
        {
            _logger.LogWarning($"CacheRebuildConfigProvider: incrementalSpatialRadius={parsed.IncrementalSpatialRadius} not in finite [0.1,100.0], reverting to default {defaults.IncrementalSpatialRadius}");
            parsed.IncrementalSpatialRadius = defaults.IncrementalSpatialRadius;
            rejected = true;
        }

        if (parsed.SmokeTestPairs < 1 || parsed.SmokeTestPairs > 100)
        {
            _logger.LogWarning($"CacheRebuildConfigProvider: smokeTestPairs={parsed.SmokeTestPairs} outside [1,100], reverting to default {defaults.SmokeTestPairs}");
            parsed.SmokeTestPairs = defaults.SmokeTestPairs;
            rejected = true;
        }

        if (!FiniteFloatValidator.IsFiniteInRange(parsed.SmokeTestDistanceTolerance, 1e-8f, 1e-2f))
        {
            _logger.LogWarning($"CacheRebuildConfigProvider: smokeTestDistanceTolerance={parsed.SmokeTestDistanceTolerance} not in finite [1e-8,1e-2], reverting to default {defaults.SmokeTestDistanceTolerance}");
            parsed.SmokeTestDistanceTolerance = defaults.SmokeTestDistanceTolerance;
            rejected = true;
        }

        if (Array.IndexOf(ValidVerbosityLevels, parsed.LogVerbosity?.ToLowerInvariant()) < 0)
        {
            _logger.LogWarning($"CacheRebuildConfigProvider: logVerbosity='{parsed.LogVerbosity}' not in [error,warn,info,debug], reverting to default '{defaults.LogVerbosity}'");
            parsed.LogVerbosity = defaults.LogVerbosity;
            rejected = true;
        }
        else
        {
            parsed.LogVerbosity = parsed.LogVerbosity.ToLowerInvariant();
        }

        if (rejected)
            _logger.LogWarning("CacheRebuildConfigProvider: cache_rebuild_config.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo($"CacheRebuildConfigProvider: Loaded cache_rebuild_config.json (parallelism={parsed.Parallelism}, incremental={parsed.EnableIncremental})");

        return parsed;
    }
}
