using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.FactionMap.Models;

namespace TAOM.Features.FactionMap;

public class FactionConfigProvider : IFactionConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;

    public FactionConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public Dictionary<string, RegionData> LoadRegions()
    {
        var result = new Dictionary<string, RegionData>();
        var path = Path.Combine(_pathService.ModuleDataPath, "factionmap", "regions.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"FactionConfigProvider: regions.json not found: {path}");
            return result;
        }

        try
        {
            var json = File.ReadAllText(path);
            var root = JObject.Parse(json);

            foreach (var prop in root.Properties())
            {
                if (prop.Value is not JObject obj) continue;

                var bbox = obj["norm_bbox"]?.ToObject<float[]>();
                var capitalPos = obj["capital_pos"]?.ToObject<float[]>();

                result[prop.Name] = new RegionData
                {
                    FactionId = obj.Value<string>("faction") ?? "",
                    BBoxX = bbox is { Length: >= 1 } ? bbox[0] : 0f,
                    BBoxY = bbox is { Length: >= 2 } ? bbox[1] : 0f,
                    BBoxW = bbox is { Length: >= 3 } ? bbox[2] : 0f,
                    BBoxH = bbox is { Length: >= 4 } ? bbox[3] : 0f,
                    CapitalX = capitalPos is { Length: >= 1 } ? capitalPos[0] : -1f,
                    CapitalY = capitalPos is { Length: >= 2 } ? capitalPos[1] : -1f,
                };
            }

            _logger.LogInfo($"Loaded {result.Count} regions from regions.json");
        }
        catch (Exception ex)
        {
            _logger.LogError($"FactionConfigProvider: Failed to parse regions.json: {ex.Message}");
        }

        return result;
    }

    public Dictionary<string, FactionData> LoadFactions()
    {
        var result = new Dictionary<string, FactionData>();
        var path = Path.Combine(_pathService.ModuleDataPath, "factionmap", "factions.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"FactionConfigProvider: factions.json not found: {path}");
            return result;
        }

        try
        {
            var json = File.ReadAllText(path);
            var root = JObject.Parse(json);

            foreach (var prop in root.Properties())
            {
                if (prop.Value is not JObject obj) continue;

                var bonuses = FactionDataParser.ParseBonuses(obj["bonuses"] as JArray);
                var perks = FactionDataParser.ParsePerks(obj["perks"] as JArray);
                var specialUnit = FactionDataParser.ParseSpecialUnit(obj["special_unit"] as JObject);

                result[prop.Name] = new FactionData
                {
                    Name = obj.Value<string>("name") ?? "",
                    Color = obj.Value<string>("color") ?? "",
                    Playable = obj.Value<bool?>("playable") ?? false,
                    GameFaction = obj.Value<string>("game_faction") ?? "",
                    Description = obj.Value<string>("description") ?? "",
                    Image = obj.Value<string>("image") ?? "",
                    Side = obj.Value<string>("side") ?? "neutral",
                    Traits = obj["traits"]?.ToObject<string[]>() ?? Array.Empty<string>(),
                    Bonuses = bonuses,
                    SpecialUnit = specialUnit,
                    Perks = perks,
                    Strengths = obj["strengths"]?.ToObject<string[]>() ?? Array.Empty<string>(),
                    Weaknesses = obj["weaknesses"]?.ToObject<string[]>() ?? Array.Empty<string>(),
                    Difficulty = obj.Value<int?>("difficulty") ?? 0,
                };
            }

            _logger.LogInfo($"Loaded {result.Count} factions from factions.json");
        }
        catch (Exception ex)
        {
            _logger.LogError($"FactionConfigProvider: Failed to parse factions.json: {ex.Message}");
        }

        return result;
    }
}
