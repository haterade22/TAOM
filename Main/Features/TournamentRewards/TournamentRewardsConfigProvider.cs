using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.TournamentRewards;

public interface ITournamentRewardsConfigProvider
{
    TournamentRewardsCatalog GetCatalog();
}

/// <summary>
/// Validating loader for <c>tournament_rewards/tournament_rewards.json</c> (the CultureDoctrine pattern): a
/// missing file gives every culture vanilla's factor with a warning, a parse failure the same with an error, and
/// a parseable-but-invalid factor reverts to the default row's with a warning (csharp-architecture.md "Config
/// Providers MUST Validate"). A culture row's missing field inherits the default row's. Read once per process.
/// </summary>
public sealed class TournamentRewardsConfigProvider : ITournamentRewardsConfigProvider
{
    private const string Tag = "TournamentRewardsConfigProvider";
    private const string DefaultKey = "default";
    private const float MinFactor = 0f;
    private const float MaxFactor = 10f;

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<TournamentRewardsCatalog> _catalog;

    public TournamentRewardsConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _catalog = new Lazy<TournamentRewardsCatalog>(Load);
    }

    public TournamentRewardsCatalog GetCatalog() => _catalog.Value;

    private TournamentRewardsCatalog Load()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "tournament_rewards", "tournament_rewards.json");
        if (!File.Exists(path))
        {
            _logger.LogWarning($"{Tag}: tournament_rewards.json not found at {path}, every culture gets vanilla's tournament rewards");
            return TournamentRewardsCatalog.AllNeutral();
        }

        ConfigDto? parsed;
        try
        {
            parsed = JsonConvert.DeserializeObject<ConfigDto>(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            _logger.LogError($"{Tag}: failed to parse tournament_rewards.json: {ex.Message}");
            return TournamentRewardsCatalog.AllNeutral();
        }

        return Validate(parsed?.Cultures);
    }

    private TournamentRewardsCatalog Validate(Dictionary<string, FactorsDto?>? cultures)
    {
        if (cultures == null)
        {
            _logger.LogWarning($"{Tag}: tournament_rewards.json has no 'cultures' object, every culture gets vanilla's tournament rewards");
            return TournamentRewardsCatalog.AllNeutral();
        }

        var rejected = false;
        var rows = new Dictionary<string, FactorsDto?>(StringComparer.Ordinal);
        foreach (var pair in cultures)
        {
            var key = pair.Key?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(key))
            {
                _logger.LogWarning($"{Tag}: a row has an empty culture key, dropping it");
                rejected = true;
                continue;
            }
            rows[key!] = pair.Value;
        }

        TournamentCultureFactors fallback;
        if (rows.TryGetValue(DefaultKey, out var defaultRow))
            fallback = Build(DefaultKey, defaultRow, TournamentCultureFactors.Neutral, ref rejected);
        else
        {
            _logger.LogWarning($"{Tag}: no 'default' row, cultures without a row get vanilla's tournament rewards");
            rejected = true;
            fallback = TournamentCultureFactors.Neutral;
        }

        var byCulture = new Dictionary<string, TournamentCultureFactors>(StringComparer.Ordinal);
        foreach (var pair in rows)
            if (pair.Key != DefaultKey)
                byCulture[pair.Key] = Build(pair.Key, pair.Value, fallback, ref rejected);

        if (rejected)
            _logger.LogWarning($"{Tag}: tournament_rewards.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo($"{Tag}: loaded tournament_rewards.json ({byCulture.Count} culture row(s) plus default)");
        return new TournamentRewardsCatalog(byCulture, fallback);
    }

    private TournamentCultureFactors Build(string key, FactorsDto? row, TournamentCultureFactors inherit, ref bool rejected) =>
        new(Factor(key, "renown", row?.Renown, inherit.Renown, ref rejected),
            Factor(key, "influence", row?.Influence, inherit.Influence, ref rejected),
            Factor(key, "skill_xp", row?.SkillXp, inherit.SkillXp, ref rejected));

    private float Factor(string key, string field, float? value, float inherit, ref bool rejected)
    {
        if (value == null)
            return inherit;
        if (FiniteFloatValidator.IsFiniteInRange(value.Value, MinFactor, MaxFactor))
            return value.Value;
        _logger.LogWarning($"{Tag}: cultures['{key}'].{field} = {value.Value} is not a finite factor in [{MinFactor}, {MaxFactor}], using {inherit}");
        rejected = true;
        return inherit;
    }

    private sealed class ConfigDto
    {
        [JsonProperty("cultures")]
        public Dictionary<string, FactorsDto?>? Cultures { get; set; }
    }

    private sealed class FactorsDto
    {
        [JsonProperty("renown")]
        public float? Renown { get; set; }

        [JsonProperty("influence")]
        public float? Influence { get; set; }

        [JsonProperty("skill_xp")]
        public float? SkillXp { get; set; }
    }
}
