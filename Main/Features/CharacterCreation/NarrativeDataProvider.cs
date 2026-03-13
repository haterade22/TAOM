using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation.Models;

namespace TAOM.Features.CharacterCreation;

public class NarrativeDataProvider : INarrativeDataProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly ConcurrentDictionary<string, IReadOnlyList<NarrativeOptionDefinition>> _cache = new();

    public NarrativeDataProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public IReadOnlyList<NarrativeOptionDefinition> LoadMenuOptions(string menuName)
    {
        return _cache.GetOrAdd(menuName, LoadFromFile);
    }

    public IReadOnlyList<NarrativeOptionDefinition> GetMenuOptionsForCulture(string menuName, string cultureId)
    {
        var all = LoadMenuOptions(menuName);
        return all.Where(o => string.Equals(o.CultureId, cultureId, StringComparison.OrdinalIgnoreCase))
                  .ToList()
                  .AsReadOnly();
    }

    private IReadOnlyList<NarrativeOptionDefinition> LoadFromFile(string menuName)
    {
        var path = GetMenuPath(menuName);
        if (path == null || !File.Exists(path))
        {
            _logger.LogWarning($"Narrative menu file not found: {path}");
            return Array.Empty<NarrativeOptionDefinition>();
        }

        try
        {
            var json = File.ReadAllText(path);
            var array = JArray.Parse(json);
            var options = new List<NarrativeOptionDefinition>();

            foreach (var item in array)
            {
                options.Add(ParseOption(item));
            }

            _logger.LogInfo($"Loaded {options.Count} narrative options from {menuName}");
            return options.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to parse {menuName} menu: {ex.Message}");
            return Array.Empty<NarrativeOptionDefinition>();
        }
    }

    private static NarrativeOptionDefinition ParseOption(JToken item)
    {
        return new NarrativeOptionDefinition
        {
            StringId = item.Value<string>("string_id") ?? "",
            CultureId = item.Value<string>("culture_id") ?? "",
            Text = item.Value<string>("text") ?? "",
            Description = item.Value<string>("description") ?? "",
            Skills = item["skills"]?.ToObject<string[]>() ?? Array.Empty<string>(),
            Attribute = item.Value<string>("attribute") ?? "",
            OccupationType = item.Value<string>("occupation_type") ?? "",
            FocusToAdd = item.Value<int?>("focus_to_add") ?? 1,
            SkillLevelToAdd = item.Value<int?>("skill_level_to_add") ?? 10,
            AttributeLevelToAdd = item.Value<int?>("attribute_level_to_add") ?? 1
        };
    }

    private string GetMenuPath(string menuName)
    {
        var moduleDataPath = _pathService?.ModuleDataPath;
        if (string.IsNullOrEmpty(moduleDataPath))
            return null;

        return Path.Combine(moduleDataPath, "charactercreation", $"{menuName}_menu.json");
    }
}
