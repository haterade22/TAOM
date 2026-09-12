using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.LordPartyTemplates.Domain;

namespace TAOM.Features.LordPartyTemplates;

/// <summary>
/// Validating boundary loader for <c>lord_party_templates/lord_party_templates.json</c>
/// (the <c>BannerBearerConfigProvider</c> shape): missing file, unparseable file and a null map
/// all become an empty map plus a log line, and every entry with a blank or prefixed id is dropped
/// with a warning naming it. The feature is then simply inert for that hero, which is vanilla.
///
/// Not validated here, deliberately: whether the hero or the template EXISTS. Both need
/// <c>MBObjectManager</c>, which is empty at config-load time. The patch resolves the template at
/// spawn and warns once per hero when it cannot; <c>ShippedLordPartyTemplateTests</c> pins the
/// shipped file offline.
/// </summary>
public class LordPartyTemplateConfigProvider : ILordPartyTemplateConfigProvider
{
    // Replace, not append-merge: Json.NET's default ObjectCreationHandling appends JSON entries
    // onto the constructor-populated dictionary.
    private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
    {
        ObjectCreationHandling = ObjectCreationHandling.Replace,
    };

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<LordPartyTemplateConfig> _config;

    public LordPartyTemplateConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<LordPartyTemplateConfig>(LoadConfig);
    }

    public LordPartyTemplateConfig GetConfig() => _config.Value;

    private LordPartyTemplateConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "lord_party_templates", "lord_party_templates.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"LordPartyTemplateConfigProvider: lord_party_templates.json not found at {path}, no lord overrides");
            return new LordPartyTemplateConfig();
        }

        LordPartyTemplateConfig parsed;
        try
        {
            var json = File.ReadAllText(path);
            parsed = JsonConvert.DeserializeObject<LordPartyTemplateConfig>(json, SerializerSettings) ?? new LordPartyTemplateConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"LordPartyTemplateConfigProvider: Failed to parse lord_party_templates.json: {ex.Message}");
            return new LordPartyTemplateConfig();
        }

        return Validate(parsed);
    }

    private LordPartyTemplateConfig Validate(LordPartyTemplateConfig parsed)
    {
        var sanitized = new LordPartyTemplateConfig();
        var rejected = false;

        if (parsed.Overrides == null)
        {
            _logger.LogWarning("LordPartyTemplateConfigProvider: overrides is null, no lord overrides");
            rejected = true;
        }
        else
        {
            foreach (var entry in parsed.Overrides)
            {
                if (string.IsNullOrWhiteSpace(entry.Key))
                {
                    _logger.LogWarning("LordPartyTemplateConfigProvider: an entry has a blank hero id, dropping it");
                    rejected = true;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.Value))
                {
                    _logger.LogWarning($"LordPartyTemplateConfigProvider: '{entry.Key}' maps to a blank template id, dropping it");
                    rejected = true;
                    continue;
                }

                // Both sides are bare StringIds. A "Hero." or "PartyTemplate." prefix copied from
                // the XML would never match anything, so it is loud here rather than silent in play.
                if (entry.Key.Contains(".") || entry.Value.Contains("."))
                {
                    _logger.LogWarning(
                        $"LordPartyTemplateConfigProvider: '{entry.Key}' -> '{entry.Value}' carries a prefix; " +
                        "ids are bare StringIds (lord_1_34, not Hero.lord_1_34), dropping it");
                    rejected = true;
                    continue;
                }

                sanitized.Overrides[entry.Key] = entry.Value;
            }
        }

        if (rejected)
            _logger.LogWarning("LordPartyTemplateConfigProvider: lord_party_templates.json contained invalid entries. See prior warnings for details.");
        else
            _logger.LogInfo($"LordPartyTemplateConfigProvider: Loaded lord_party_templates.json, {sanitized.Overrides.Count} lord override(s)");

        return sanitized;
    }
}
