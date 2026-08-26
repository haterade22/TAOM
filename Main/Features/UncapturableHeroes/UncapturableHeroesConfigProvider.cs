using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.UncapturableHeroes.Domain;

namespace TAOM.Features.UncapturableHeroes;

/// <summary>
/// Validating boundary loader for <c>uncapturable_heroes/uncapturable_heroes_config.json</c>
/// (DreadAuraConfigProvider pattern): a missing file gives defaults + warn, a parse failure gives
/// defaults + error, and a null list reverts that one field to the compiled default with a warning
/// (csharp-architecture.md "Config Providers MUST Validate").
///
/// There are no numeric fields here, so there is nothing for <c>FiniteFloatValidator</c> to guard.
/// If a numeric knob is ever added, it must be range-checked through that validator FIRST, because
/// a bare min/max comparison is false for NaN and lets the bad value through.
///
/// Not validated here, deliberately: race names and hero StringIds. Race names need the FaceGen
/// registry, which is not populated at config-load time, so <see cref="UncapturableRegistry"/>
/// validates them lazily on first resolve and skips + warns per entry. Hero ids are pinned instead
/// by ShippedUncapturableHeroesConfigTests against the shipped lord data.
/// </summary>
public sealed class UncapturableHeroesConfigProvider : IUncapturableHeroesConfigProvider
{
    // Replace, not append-merge: Json.NET's default ObjectCreationHandling appends JSON collection
    // entries onto the constructor-populated defaults, so an author who lists one hero would get
    // their entry PLUS every compiled one. That failure is silent and looks correct in game.
    private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
    {
        ObjectCreationHandling = ObjectCreationHandling.Replace,
    };

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<UncapturableHeroesConfig> _config;

    public UncapturableHeroesConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<UncapturableHeroesConfig>(LoadConfig);
    }

    public UncapturableHeroesConfig GetConfig() => _config.Value;

    private UncapturableHeroesConfig LoadConfig()
    {
        var path = Path.Combine(
            _pathService.ModuleDataPath, "uncapturable_heroes", "uncapturable_heroes_config.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning(
                $"UncapturableHeroesConfigProvider: uncapturable_heroes_config.json not found at {path}, using defaults");
            return new UncapturableHeroesConfig();
        }

        UncapturableHeroesConfig parsed;
        try
        {
            var json = File.ReadAllText(path);
            parsed = JsonConvert.DeserializeObject<UncapturableHeroesConfig>(json, SerializerSettings)
                     ?? new UncapturableHeroesConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                $"UncapturableHeroesConfigProvider: Failed to parse uncapturable_heroes_config.json: {ex.Message}");
            return new UncapturableHeroesConfig();
        }

        return Validate(parsed);
    }

    private UncapturableHeroesConfig Validate(UncapturableHeroesConfig parsed)
    {
        var defaults = new UncapturableHeroesConfig();
        var rejected = false;

        var sanitized = new UncapturableHeroesConfig
        {
            Enabled = parsed.Enabled,
            AnnounceEscape = parsed.AnnounceEscape,
            HeroSets = ValidateList(parsed.HeroSets, defaults.HeroSets, nameof(parsed.HeroSets), ref rejected),
            HeroIds = ValidateList(parsed.HeroIds, defaults.HeroIds, nameof(parsed.HeroIds), ref rejected),
            UncapturableRaces = ValidateList(
                parsed.UncapturableRaces, defaults.UncapturableRaces, nameof(parsed.UncapturableRaces), ref rejected),
            ExcludeHeroIds = ValidateList(
                parsed.ExcludeHeroIds, defaults.ExcludeHeroIds, nameof(parsed.ExcludeHeroIds), ref rejected),
        };

        if (rejected)
            _logger.LogWarning("UncapturableHeroesConfigProvider: uncapturable_heroes_config.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo("UncapturableHeroesConfigProvider: Loaded uncapturable_heroes_config.json");

        return sanitized;
    }

    // An EMPTY list is a legitimate "nothing of this kind" switch and passes through; only a null
    // (a JSON `null`, or a missing key under Replace semantics) reverts to the compiled default.
    private List<string> ValidateList(List<string> value, List<string> fallback, string field, ref bool rejected)
    {
        if (value == null)
        {
            _logger.LogWarning($"UncapturableHeroesConfigProvider: {field} is null, reverting to defaults");
            rejected = true;
            return fallback;
        }

        return value;
    }
}
