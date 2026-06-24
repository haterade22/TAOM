using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TaleWorlds.Core;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.NavalTravel;

/// <summary>
/// Loads + validates <c>naval_travel/naval_travel_config.json</c>. Per the "Config Providers MUST
/// Validate" architecture rule: parse failures and semantically-invalid values fall back to compiled
/// defaults with a warning. <see cref="NavalTravelConfig.EmbarkThresholdDistance"/> is finite-checked
/// (NaN/Infinity rejected before range), and every <see cref="NavalTravelConfig.NavalTerrainTypeIds"/>
/// entry must be a known <see cref="TerrainType"/> id. Cached for the process lifetime
/// (Reuse.Singleton) — edits require an app restart.
/// </summary>
public class NavalTravelConfigProvider : INavalTravelConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<NavalTravelConfig> _config;

    public NavalTravelConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<NavalTravelConfig>(LoadConfig);
    }

    public NavalTravelConfig GetConfig() => _config.Value;

    private NavalTravelConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "naval_travel", "naval_travel_config.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"NavalTravelConfigProvider: naval_travel_config.json not found at {path}, using defaults");
            return new NavalTravelConfig();
        }

        NavalTravelConfig parsed;
        try
        {
            var json = File.ReadAllText(path);
            parsed = JsonConvert.DeserializeObject<NavalTravelConfig>(json) ?? new NavalTravelConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"NavalTravelConfigProvider: Failed to parse naval_travel_config.json: {ex.Message}");
            return new NavalTravelConfig();
        }

        return Validate(parsed);
    }

    private NavalTravelConfig Validate(NavalTravelConfig parsed)
    {
        var defaults = new NavalTravelConfig();
        var rejected = false;

        var threshold = parsed.EmbarkThresholdDistance;
        if (!FiniteFloatValidator.IsFiniteInRange(threshold, 0f, NavalTravelConfig.MaxEmbarkThresholdDistance))
        {
            _logger.LogWarning($"NavalTravelConfigProvider: embarkThresholdDistance='{threshold}' is not finite within " +
                $"[0, {NavalTravelConfig.MaxEmbarkThresholdDistance}], reverting to default {defaults.EmbarkThresholdDistance}");
            threshold = defaults.EmbarkThresholdDistance;
            rejected = true;
        }

        var terrainIds = ValidateTerrainIds(parsed.NavalTerrainTypeIds, defaults, ref rejected);

        var boatScale = parsed.BoatScale;
        // (0, MaxBoatScale]; min 0.001 excludes a zero/invisible boat. IsFiniteInRange rejects NaN/Infinity first.
        if (!FiniteFloatValidator.IsFiniteInRange(boatScale, 0.001f, NavalTravelConfig.MaxBoatScale))
        {
            _logger.LogWarning($"NavalTravelConfigProvider: boatScale='{boatScale}' is not finite within " +
                $"(0, {NavalTravelConfig.MaxBoatScale}], reverting to default {defaults.BoatScale}");
            boatScale = defaults.BoatScale;
            rejected = true;
        }

        var boatMeshName = parsed.BoatMeshName;
        if (string.IsNullOrWhiteSpace(boatMeshName))
        {
            _logger.LogWarning($"NavalTravelConfigProvider: boatMeshName is empty, reverting to default '{defaults.BoatMeshName}'");
            boatMeshName = defaults.BoatMeshName;
            rejected = true;
        }

        var sailModifierKey = parsed.SailModifierKey;
        if (string.IsNullOrWhiteSpace(sailModifierKey))
        {
            _logger.LogWarning($"NavalTravelConfigProvider: sailModifierKey is empty, reverting to default '{defaults.SailModifierKey}'");
            sailModifierKey = defaults.SailModifierKey;
            rejected = true;
        }

        var sanitized = new NavalTravelConfig
        {
            Enabled = parsed.Enabled,
            ApplyToPlayer = parsed.ApplyToPlayer,
            ApplyToAi = parsed.ApplyToAi,
            EmbarkThresholdDistance = threshold,
            NavalTerrainTypeIds = terrainIds,
            RenderBoatVisual = parsed.RenderBoatVisual,
            BoatMeshName = boatMeshName,
            BoatScale = boatScale,
            SailModifierKey = sailModifierKey,
        };

        if (rejected)
            _logger.LogWarning("NavalTravelConfigProvider: naval_travel_config.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo("NavalTravelConfigProvider: Loaded naval_travel_config.json");

        return sanitized;
    }

    private int[] ValidateTerrainIds(int[] ids, NavalTravelConfig defaults, ref bool rejected)
    {
        if (ids == null || ids.Length == 0)
        {
            _logger.LogWarning("NavalTravelConfigProvider: navalTerrainTypeIds is empty, reverting to default set");
            rejected = true;
            return defaults.NavalTerrainTypeIds;
        }

        var valid = new List<int>();
        foreach (var id in ids)
        {
            if (Enum.IsDefined(typeof(TerrainType), id))
            {
                if (!valid.Contains(id))
                    valid.Add(id);
            }
            else
            {
                _logger.LogWarning($"NavalTravelConfigProvider: navalTerrainTypeIds entry '{id}' is not a known TerrainType, dropping");
                rejected = true;
            }
        }

        if (valid.Count == 0)
        {
            _logger.LogWarning("NavalTravelConfigProvider: navalTerrainTypeIds had no valid entries, reverting to default set");
            rejected = true;
            return defaults.NavalTerrainTypeIds;
        }

        return valid.ToArray();
    }
}
