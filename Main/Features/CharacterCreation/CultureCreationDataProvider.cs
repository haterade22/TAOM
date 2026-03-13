using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation.Models;

namespace TAOM.Features.CharacterCreation;

public class CultureCreationDataProvider : ICultureCreationDataProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private List<CultureCreationData> _cultures;

    public CultureCreationDataProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public IReadOnlyList<CultureCreationData> LoadCultures()
    {
        if (_cultures != null)
            return _cultures;

        _cultures = new List<CultureCreationData>();
        var path = Path.Combine(_pathService.ModuleDataPath, "charactercreation", "cultures.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"CultureCreationDataProvider: cultures.json not found: {path}");
            return _cultures;
        }

        try
        {
            var json = File.ReadAllText(path);
            var array = JArray.Parse(json);

            foreach (var token in array)
            {
                if (token is not JObject obj) continue;

                _cultures.Add(new CultureCreationData
                {
                    CultureId = obj.Value<string>("culture_id") ?? "",
                    Races = obj["races"]?.ToObject<string[]>() ?? Array.Empty<string>(),
                    StartingSettlement = obj.Value<string>("starting_settlement") ?? "",
                    DefaultAge = obj.Value<float?>("default_age") ?? 30f,
                    DefaultWeight = obj.Value<float?>("default_weight") ?? 0.5f,
                    DefaultBuild = obj.Value<float?>("default_build") ?? 0.5f,
                    FocusToAdd = obj.Value<int?>("focus_to_add") ?? 1,
                    SkillLevelToAdd = obj.Value<int?>("skill_level_to_add") ?? 10,
                });
            }

            _logger.LogInfo($"Loaded {_cultures.Count} culture creation entries from cultures.json");
        }
        catch (Exception ex)
        {
            _logger.LogError($"CultureCreationDataProvider: Failed to parse cultures.json: {ex.Message}");
        }

        return _cultures;
    }

    public CultureCreationData GetCultureData(string cultureId)
    {
        var cultures = LoadCultures();
        return cultures.FirstOrDefault(c =>
            string.Equals(c.CultureId, cultureId, StringComparison.OrdinalIgnoreCase));
    }
}
