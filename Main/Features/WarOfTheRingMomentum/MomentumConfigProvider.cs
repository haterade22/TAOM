using System;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.WarOfTheRingMomentum.Models;

namespace TAOM.Features.WarOfTheRingMomentum;

/// <summary>
/// Loads + validates <c>momentum/momentum.json</c>. Mirrors CultureConversionConfigProvider:
/// <see cref="Lazy{T}"/> singleton, semantic validation with per-field revert-to-default +
/// warning, NaN/Infinity guards on float fields. Reuse.Singleton — changes require a full
/// game restart, not a save-load.
/// </summary>
public class MomentumConfigProvider : IMomentumConfigProvider
{
    private const int MaxIntValue = 1000000;

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<MomentumConfig> _config;

    public MomentumConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<MomentumConfig>(LoadConfig);
    }

    public MomentumConfig GetConfig() => _config.Value;

    private MomentumConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "momentum", "momentum.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"MomentumConfigProvider: momentum.json not found at {path}, using defaults");
            return new MomentumConfig();
        }

        MomentumConfig parsed;
        try
        {
            var json = File.ReadAllText(path);
            parsed = JsonConvert.DeserializeObject<MomentumConfig>(json) ?? new MomentumConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"MomentumConfigProvider: Failed to parse momentum.json: {ex.Message}");
            return new MomentumConfig();
        }

        return Validate(parsed);
    }

    private MomentumConfig Validate(MomentumConfig parsed)
    {
        // Newtonsoft leaves nested objects null when the JSON omits them entirely.
        parsed.Events = parsed.Events ?? new MomentumEventValuesConfig();
        parsed.DurationsHours = parsed.DurationsHours ?? new MomentumDurationsConfig();
        parsed.Player = parsed.Player ?? new MomentumPlayerConfig();

        var defaults = new MomentumConfig();
        var rejected = false;

        if (parsed.VictoryThreshold < 1 || parsed.VictoryThreshold > MaxIntValue)
        {
            Warn($"victoryThreshold={parsed.VictoryThreshold} outside [1,{MaxIntValue}]", defaults.VictoryThreshold);
            parsed.VictoryThreshold = defaults.VictoryThreshold;
            rejected = true;
        }

        rejected |= ValidateEventValue("maxBattleMomentum", () => parsed.Events.MaxBattleMomentum, v => parsed.Events.MaxBattleMomentum = v, defaults.Events.MaxBattleMomentum);
        rejected |= ValidateEventValue("siegeMomentum", () => parsed.Events.SiegeMomentum, v => parsed.Events.SiegeMomentum = v, defaults.Events.SiegeMomentum);
        rejected |= ValidateEventValue("raidMomentum", () => parsed.Events.RaidMomentum, v => parsed.Events.RaidMomentum = v, defaults.Events.RaidMomentum);
        rejected |= ValidateEventValue("armyMomentum", () => parsed.Events.ArmyMomentum, v => parsed.Events.ArmyMomentum = v, defaults.Events.ArmyMomentum);
        rejected |= ValidateEventValue("maxStrengthMomentum", () => parsed.Events.MaxStrengthMomentum, v => parsed.Events.MaxStrengthMomentum = v, defaults.Events.MaxStrengthMomentum);
        rejected |= ValidateEventValue("killMomentumPerHundred", () => parsed.Events.KillMomentumPerHundred, v => parsed.Events.KillMomentumPerHundred = v, defaults.Events.KillMomentumPerHundred);

        rejected |= ValidateDuration("battleWon", () => parsed.DurationsHours.BattleWon, v => parsed.DurationsHours.BattleWon = v, defaults.DurationsHours.BattleWon);
        rejected |= ValidateDuration("siege", () => parsed.DurationsHours.Siege, v => parsed.DurationsHours.Siege = v, defaults.DurationsHours.Siege);
        rejected |= ValidateDuration("raid", () => parsed.DurationsHours.Raid, v => parsed.DurationsHours.Raid = v, defaults.DurationsHours.Raid);
        rejected |= ValidateDuration("armyGathered", () => parsed.DurationsHours.ArmyGathered, v => parsed.DurationsHours.ArmyGathered = v, defaults.DurationsHours.ArmyGathered);
        rejected |= ValidateDuration("relativeStrength", () => parsed.DurationsHours.RelativeStrength, v => parsed.DurationsHours.RelativeStrength = v, defaults.DurationsHours.RelativeStrength);
        rejected |= ValidateDuration("enemiesKilled", () => parsed.DurationsHours.EnemiesKilled, v => parsed.DurationsHours.EnemiesKilled = v, defaults.DurationsHours.EnemiesKilled);

        if (!FiniteFloatValidator.IsFiniteInRange(parsed.Player.ParticipationMultiplier, 1f, 100f))
        {
            Warn($"participationMultiplier={parsed.Player.ParticipationMultiplier} must be finite in [1,100]", defaults.Player.ParticipationMultiplier);
            parsed.Player.ParticipationMultiplier = defaults.Player.ParticipationMultiplier;
            rejected = true;
        }

        if (parsed.Player.MinimumPlayerEventsForVictory < 0 || parsed.Player.MinimumPlayerEventsForVictory > 1000)
        {
            Warn($"minimumPlayerEventsForVictory={parsed.Player.MinimumPlayerEventsForVictory} outside [0,1000]", defaults.Player.MinimumPlayerEventsForVictory);
            parsed.Player.MinimumPlayerEventsForVictory = defaults.Player.MinimumPlayerEventsForVictory;
            rejected = true;
        }

        // Strictly > 1: the strength award divides by (ratio − 1).
        if (!FiniteFloatValidator.IsFinite(parsed.StrengthRatioForMaxMomentum) || parsed.StrengthRatioForMaxMomentum <= 1f)
        {
            Warn($"strengthRatioForMaxMomentum={parsed.StrengthRatioForMaxMomentum} must be finite and > 1", defaults.StrengthRatioForMaxMomentum);
            parsed.StrengthRatioForMaxMomentum = defaults.StrengthRatioForMaxMomentum;
            rejected = true;
        }

        if (rejected)
            _logger.LogWarning("MomentumConfigProvider: momentum.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo("MomentumConfigProvider: Loaded momentum.json");

        return parsed;
    }

    private bool ValidateEventValue(string name, Func<int> get, Action<int> set, int fallback)
    {
        if (get() >= 0 && get() <= MaxIntValue) return false;
        Warn($"{name}={get()} outside [0,{MaxIntValue}]", fallback);
        set(fallback);
        return true;
    }

    private bool ValidateDuration(string name, Func<int> get, Action<int> set, int fallback)
    {
        if (get() >= 1 && get() <= 100000) return false;
        Warn($"durationsHours.{name}={get()} outside [1,100000]", fallback);
        set(fallback);
        return true;
    }

    private void Warn(string what, object fallback)
    {
        _logger.LogWarning($"MomentumConfigProvider: {what}, reverting to default {fallback}");
    }
}
