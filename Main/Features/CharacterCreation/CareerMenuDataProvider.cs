using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation.Models;

namespace TAOM.Features.CharacterCreation;

public class CareerMenuDataProvider : ICareerMenuDataProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private IReadOnlyList<CareerMenuOptionDefinition> _cached;

    public CareerMenuDataProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public IReadOnlyList<CareerMenuOptionDefinition> LoadCareerMenuOptions()
    {
        if (_cached != null)
            return _cached;

        var path = GetFilePath();
        if (path == null || !File.Exists(path))
        {
            _logger.LogWarning($"Career menu file not found: {path}");
            _cached = Array.Empty<CareerMenuOptionDefinition>();
            return _cached;
        }

        try
        {
            var json = File.ReadAllText(path);
            var array = JArray.Parse(json);
            var options = new List<CareerMenuOptionDefinition>();

            foreach (var item in array)
            {
                options.Add(ParseOption(item));
            }

            _logger.LogInfo($"Loaded {options.Count} career menu options");
            _cached = options.AsReadOnly();
            return _cached;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to parse career menu: {ex.Message}");
            _cached = Array.Empty<CareerMenuOptionDefinition>();
            return _cached;
        }
    }

    public CareerMenuOptionDefinition GetOptionForCareer(string careerStringId)
    {
        var all = LoadCareerMenuOptions();
        return all.FirstOrDefault(o =>
            string.Equals(o.CareerStringId, careerStringId, StringComparison.OrdinalIgnoreCase));
    }

    private static CareerMenuOptionDefinition ParseOption(JToken item)
    {
        return new CareerMenuOptionDefinition
        {
            CareerStringId = item.Value<string>("career_string_id") ?? "",
            Skills = item["skills"]?.ToObject<string[]>() ?? Array.Empty<string>(),
            Attribute = item.Value<string>("attribute") ?? "",
            FocusToAdd = item.Value<int?>("focus_to_add") ?? 1,
            SkillLevelToAdd = item.Value<int?>("skill_level_to_add") ?? 10,
            AttributeLevelToAdd = item.Value<int?>("attribute_level_to_add") ?? 1
        };
    }

    private string GetFilePath()
    {
        var moduleDataPath = _pathService?.ModuleDataPath;
        if (string.IsNullOrEmpty(moduleDataPath))
            return null;

        return Path.Combine(moduleDataPath, "charactercreation", "career_menu.json");
    }
}
